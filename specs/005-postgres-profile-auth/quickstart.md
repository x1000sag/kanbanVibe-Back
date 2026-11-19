# Quickstart: Профиль учётной записи и авторизация класса C

Проверка после реализации. Команды запускают хранилище отдельно от API и проходят сценарии контракта. Схема таблиц — [data-model.md](data-model.md). Поля и коды — [contracts/account-api.yaml](contracts/account-api.yaml). Журнал оператора — [contracts/operator-audit.md](contracts/operator-audit.md). Тела миграций и код тестов здесь не приводятся.

Значения паролей ниже — только для локального прогона. В репозиторий их не кладут.

## Предусловия

- Git-дерево с реализацией этой фичи (проекты `Account.Domain`, `Account.Infrastructure`, маршруты `/v1` в `Kanban.Api`, `docker/postgres/Dockerfile`).
- SDK .NET из `global.json` (`10.0.404` или более новый в пределах `latestFeature`).
- Docker, способный собрать сервис `postgres`.
- `curl` и клиент `psql` не обязательны для `dotnet test`. Они нужны для ручных шагов ниже.

Из корня репозитория:

```bash
export POSTGRES_PASSWORD=local-migrator-secret
export APP_DB_PASSWORD=local-app-secret
export OPERATOR_DB_PASSWORD=local-operator-secret
export KANBAN_JWT_SIGNING_KEY=local-signing-key-32bytes-minimum
export KANBAN_MIGRATOR_CONNECTION="Host=localhost;Port=5432;Database=kanban_account;Username=kanban_migrator;Password=${POSTGRES_PASSWORD}"
export KANBAN_ACCOUNT_CONNECTION="Host=localhost;Port=5432;Database=kanban_account;Username=kanban_app;Password=${APP_DB_PASSWORD}"
export KANBAN_OPERATOR_CONNECTION="Host=localhost;Port=5432;Database=kanban_account;Username=kanban_operator;Password=${OPERATOR_DB_PASSWORD}"
```

## Хранилище отдельно от API

```bash
docker compose up --build postgres
```

Ожидание: контейнер базы работает, процесс `Kanban.Api` не запущен. Повторный запуск с тем же томом не стирает уже созданные учётки.

В другом сеансе, после того как база принимает соединения:

```bash
dotnet ef database update \
  --project kanban-cli/src/Account.Infrastructure \
  --startup-project kanban-cli/src/Kanban.Api \
  --connection "$KANBAN_MIGRATOR_CONNECTION"
```

Ожидание: схема из data-model применена ролью владельца. Роль `kanban_app` не владеет таблицами.

## API

```bash
ASPNETCORE_URLS=http://localhost:5050 ASPNETCORE_HTTPS_PORTS= \
  dotnet run --project kanban-cli/src/Kanban.Api --no-launch-profile
```

Базовый URL ручных шагов: `http://localhost:5050`.

Готовность:

```bash
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5050/v1/health/ready
```

Ожидание: `200`, пока база доступна.

## Сценарии

### Создать две учётки, различающиеся регистром

```bash
curl -s -D - -o /tmp/marina.json \
  -H 'Content-Type: application/json' \
  -d '{"nick":"  marina  ","password":"любой-пароль"}' \
  http://localhost:5050/v1/accounts
curl -s -w '\n%{http_code}\n' \
  -H 'Content-Type: application/json' \
  -d '{"nick":"Marina","password":"другой"}' \
  http://localhost:5050/v1/accounts
curl -s -w '\n%{http_code}\n' \
  -H 'Content-Type: application/json' \
  -d '{"nick":"marina","password":"ещё"}' \
  http://localhost:5050/v1/accounts
```

Ожидание: первое создание — `201`, в теле ник `marina` и то же имя, слова пароля нет. Второе — `201` для `Marina`. Третье — `409` и `{"code":"nick_taken"}` без других полей. Пароль первой учётки не меняется.

Пустой ник и пустой пароль дают разные коды: `nick_invalid` и `password_invalid`.

### Войти дважды и погасить только один доступ

```bash
curl -s -H 'Content-Type: application/json' \
  -d '{"nick":"marina","password":"не-тот"}' \
  http://localhost:5050/v1/sessions
curl -s -H 'Content-Type: application/json' \
  -d '{"nick":"нет-такого","password":"любой-пароль"}' \
  http://localhost:5050/v1/sessions
```

Ожидание: оба ответа `401` и один и тот же `{"code":"sign_in_failed"}`.

```bash
curl -s -H 'Content-Type: application/json' \
  -d '{"nick":"marina","password":"любой-пароль"}' \
  http://localhost:5050/v1/sessions
```

Повторить успешный вход ещё раз и сохранить оба `accessToken`. Ожидание: два разных токена. Запрос профиля с первым токеном и с вторым оба возвращают `200`.

```bash
curl -s -H "Authorization: Bearer $TOKEN_1" http://localhost:5050/v1/accounts/marina
curl -s -X DELETE -H "Authorization: Bearer $TOKEN_1" http://localhost:5050/v1/sessions/current
curl -s -o /dev/null -w '%{http_code}\n' -H "Authorization: Bearer $TOKEN_1" http://localhost:5050/v1/accounts/marina
curl -s -o /dev/null -w '%{http_code}\n' -H "Authorization: Bearer $TOKEN_2" http://localhost:5050/v1/accounts/marina
```

Ожидание: профиль содержит ник `marina`, имя `marina`, колонки `ToDo`, `InProgress`, `Done` и ни одной карточки. Пароля в теле нет. После завершения первый токен получает `no_active_access`, второй по-прежнему `200`. Учётка не удалена.

Без заголовка доступа тот же профиль — `401` и `no_active_access`.

### Своя карточка и чужой ник

От имени `TOKEN_2`:

```bash
curl -s -H "Authorization: Bearer $TOKEN_2" -H 'Content-Type: application/json' \
  -d '{"columnName":"ToDo","title":"Купить молоко","description":"2л","tags":["дом"],"deadline":"2026-11-20"}' \
  http://localhost:5050/v1/accounts/marina/cards
```

Ожидание: `201`, в карточке есть id, заголовок, описание, одна метка, срок `2026-11-20`, время создания в UTC. Имени владельца на карточке нет.

Вход под `Marina` и чтение `http://localhost:5050/v1/accounts/marina` этим токеном — `403` и `{"code":"foreign_data"}` без карточки и без пароля. Своя доска `Marina` этой карточки не содержит.

Текст заголовка `'; DROP TABLE account;--` сохраняется буквально, вторая учётка остаётся.

### Имя, пароль, журнал

Смена имени на `Марина К.` возвращает `200`, ник прежний. Следующее чтение профиля показывает новое имя. Карточка отдельной копии имени не содержит.

Смена пароля с неверным текущим — `403` и `password_change_rejected`. Верный текущий и новый пароль — `204`, тело пустое, тот же токен всё ещё читает профиль. После завершения этого токена старый пароль получает `sign_in_failed`, новый пароль входит.

```bash
curl -s -H "Authorization: Bearer $TOKEN" http://localhost:5050/v1/accounts/marina/journal
```

Ожидание: есть успешный вход, неудачный вход в `marina`, завершение доступа, смена имени, смена пароля, отказ чужого доступа (у того, кому отказали), удаление — если его выполнили. У входа заполнен `source`. Пароля и старого имени в записях нет. Обычное чтение доски новой строки не добавляет.

Неудачный вход `нет-такого` в этом журнале отсутствует. Его видно оператору:

```bash
psql "$KANBAN_OPERATOR_CONNECTION" -c \
  "SELECT occurred_at, attempted_nick, event_type, outcome, source FROM unowned_audit_event ORDER BY occurred_at, event_id;"
```

Ожидание: строка с ником `нет-такого`, результатом `failure` и без пароля. После создания учётки `нет-такого` эта строка в её `GET .../journal` не появляется.

### Срок 12 часов

Не ждать часы вручную. Тест с подменённым `TimeProvider` сдвигает время на 12 часов вперёд: тот же токен получает `no_active_access`, учётка и доска на месте, новый вход прежним паролем снова открывает доступ.

### Хранилище остановлено

Остановить только сервис `postgres`, API не перезапускать с другой базой.

```bash
docker compose stop postgres
curl -s -w '\n%{http_code}\n' -H 'Content-Type: application/json' \
  -d '{"nick":"новая","password":"секрет"}' \
  http://localhost:5050/v1/accounts
```

Ожидание: `503` и `{"code":"store_unavailable"}`. Новой учётки нет. Готовность тоже `503`.

Запустить `postgres` снова и войти прежним ником `marina`: учётка, имя и доска те же.

## Автоматические тесты

Docker для этой команды нужен: тесты критического пути сами поднимают PostgreSQL.

```bash
dotnet test kanban-cli/Kanban.sln
```

Ожидание: сборка и тесты проходят. В наборе есть регистрация и гонка ника, один код неуспешного входа, два живых доступа и завершение только одного, чужие данные, смена имени и пароля, журнал, буквальный текст с кавычками, остановка хранилища без частичной записи, отказ роли приложения читать журнал оператора. Пароль не встречается в телах ответов.

Полный перечень утверждений — сценарии `spec.md`. Этот файл их не копирует вторым сводом.
