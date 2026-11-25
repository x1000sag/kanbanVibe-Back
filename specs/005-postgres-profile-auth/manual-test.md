# Ручной алгоритм: профиль учётной записи и авторизация

Пошаговый прогон решения `005-postgres-profile-auth`, который человек выполняет сам. Источник поведения — [spec.md](spec.md). Имена полей и коды — [contracts/account-api.yaml](contracts/account-api.yaml). Запуск хранилища и API совпадает с [quickstart.md](quickstart.md). Этот файл не заменяет спецификацию, план, задачи и контракт.

Каждый шаг ниже — действие и ожидаемый результат. Если результат другой, прогон останавливают: следующие шаги опираются на уже созданные учётки.

Прогон покрывает вход, уникальный ник, смену пароля, профиль (ник, изменяемое имя, доска и карточки), коды отказа и контейнер PostgreSQL. Срок доступа в 12 часов здесь виден по полю `expiresAt`: оно не позже момента выдачи плюс 12 часов. Ждать двенадцать часов не нужно. Сдвиг часов проверяет автоматический тест.

Пароли ниже — только для этого локального прогона.

Отказ — это HTTP-статус и JSON с одним полем `code`. Других полей нет, фразы для человека нет, пароля нет. Успешный JSON может быть напечатан с отступами. Сравнивают поля и статус. Пробелы и переносы в JSON на результат не влияют.

| Код | HTTP |
| --- | --- |
| `nick_taken` | 409 |
| `nick_invalid` | 400 |
| `password_invalid` | 400 |
| `sign_in_failed` | 401 |
| `no_active_access` | 401 |
| `foreign_data` | 403 |
| `display_name_invalid` | 400 |
| `password_change_rejected` | 403 |
| `store_unavailable` | 503 |
| `request_invalid` | 400 |
| `card_invalid` | 400 |
| `object_missing` | 404 |

Первые девять кодов — различимые ситуации спецификации. Три последних контракт тоже отдаёт, и этот прогон их вызывает.

## Подготовка

Нужны:

- дерево с этой фичей: проекты `Account.Domain` и `Account.Infrastructure`, маршруты `/v1`, `docker/postgres/Dockerfile`;
- SDK .NET из `global.json` (`10.0.404` или новее в пределах `latestFeature`);
- Docker;
- `curl` и `python3`;
- свободные порты `5432` и `5050`.

Инструмент `dotnet-ef` линейки 9.0. В проекте пакет `Microsoft.EntityFrameworkCore.Design` версии `9.0.1`. Если команды `dotnet ef` нет:

```bash
dotnet tool install --global dotnet-ef --version 9.0.1
```

Из корня репозитория, в обоих терминалах:

```bash
export COMPOSE_PROJECT_NAME=kanban-manual-005
export POSTGRES_PASSWORD=local-migrator-secret
export APP_DB_PASSWORD=local-app-secret
export OPERATOR_DB_PASSWORD=local-operator-secret
export KANBAN_JWT_SIGNING_KEY=local-signing-key-32bytes-minimum
export KANBAN_MIGRATOR_CONNECTION="Host=localhost;Port=5432;Database=kanban_account;Username=kanban_migrator;Password=${POSTGRES_PASSWORD}"
export KANBAN_ACCOUNT_CONNECTION="Host=localhost;Port=5432;Database=kanban_account;Username=kanban_app;Password=${APP_DB_PASSWORD}"
export KANBAN_OPERATOR_CONNECTION="Host=localhost;Port=5432;Database=kanban_account;Username=kanban_operator;Password=${OPERATOR_DB_PASSWORD}"
export BASE=http://localhost:5050
```

`COMPOSE_PROJECT_NAME` держит том этого прогона отдельно от других проектов compose. `KANBAN_JWT_SIGNING_KEY` нужен, чтобы compose прочитал файл, и нужен процессу API. Ключ короче 32 байт API не примет.

Функция вызова, в терминале, из которого идут шаги:

```bash
api() {
  local method="$1" path="$2" data="${3-}" token="${4-}"
  local hdr
  hdr="$(mktemp)"
  local args=(-sS -D "$hdr" -o /tmp/kb-body -X "$method" "${BASE}${path}")
  if [ -n "$data" ]; then
    args+=(-H 'Content-Type: application/json' --data-binary "$data")
  fi
  if [ -n "$token" ]; then
    args+=(-H "Authorization: Bearer ${token}")
  fi
  curl "${args[@]}"
  local status location
  status="$(awk 'BEGIN{c=""} /^HTTP/{c=$2} END{print c}' "$hdr")"
  location="$(awk 'tolower($1)=="location:"{print $2}' "$hdr" | tr -d '\r')"
  echo "status: ${status}"
  if [ -n "$location" ]; then echo "location: ${location}"; fi
  echo "body:"
  cat /tmp/kb-body
  echo
  rm -f "$hdr"
}
```

Имена полей в теле строго такие, как в команде: `nick`, `password`, `displayName`, `currentPassword`, `newPassword`, `columnName`, `title`. Лишнее поле даёт `request_invalid`.

Чистый том этого проекта:

```bash
docker compose down -v
```

Ожидание: сервисы проекта `kanban-manual-005` не запущены. Том `kanban-manual-005_kanban_account_data` удалён, если был. Чужие проекты compose эта команда не трогает.

Хранилище отдельно от API:

```bash
docker compose up --build -d postgres
```

Ожидание: сервис `postgres` собирается из `docker/postgres/Dockerfile` и работает. Сервис `api` не запущен. Процесс `Kanban.Api` ещё не запущен.

Дождаться приёма соединений:

```bash
for _ in 1 2 3 4 5 6 7 8 9 10; do
  docker compose exec -T postgres pg_isready -U postgres && break
  sleep 2
done
```

Ожидание: `pg_isready` сообщает, что база принимает соединения.

Схема ролью владельца:

```bash
dotnet ef database update \
  --project kanban-cli/src/Account.Infrastructure \
  --startup-project kanban-cli/src/Kanban.Api \
  --connection "$KANBAN_MIGRATOR_CONNECTION"
```

Ожидание: команда завершается без ошибки. Первая сборка может скачивать пакеты; шаг закончен, когда миграция применена.

Во втором терминале, с теми же переменными:

```bash
ASPNETCORE_URLS=http://localhost:5050 ASPNETCORE_HTTPS_PORTS= \
  dotnet run --project kanban-cli/src/Kanban.Api --no-launch-profile
```

Ожидание: процесс слушает `http://localhost:5050` и остаётся запущенным до шага перезапуска. Пустая `ASPNETCORE_HTTPS_PORTS` нужна, чтобы вызов по HTTP не уходил в перенаправление. Если готовность ниже вернёт `307`, API запущен не этой командой: его останавливают и запускают снова так, как написано здесь.

## 1. Контейнер PostgreSQL

### Шаг 1. Контейнер отдельно от API

**Действие**

```bash
docker compose ps --services --status running
docker compose exec -T postgres postgres -V
docker volume ls --format '{{.Name}}' | grep '^kanban-manual-005_kanban_account_data$'
```

**Ожидаемый результат**

- В списке запущенных сервисов есть `postgres` и нет `api`.
- Версия сервера — PostgreSQL 17.
- Том `kanban-manual-005_kanban_account_data` есть.
- Процесс API — это `dotnet` во втором терминале, не процесс внутри контейнера.

### Шаг 2. База и роли

**Действие**

```bash
docker compose exec -T -e PGPASSWORD="$POSTGRES_PASSWORD" postgres \
  psql -w -h 127.0.0.1 -U postgres -d postgres -c \
  "SELECT datname FROM pg_database WHERE datname = 'kanban_account';"
docker compose exec -T -e PGPASSWORD="$POSTGRES_PASSWORD" postgres \
  psql -w -h 127.0.0.1 -U postgres -d kanban_account -c \
  "SELECT rolname FROM pg_roles WHERE rolname IN ('kanban_migrator','kanban_app','kanban_operator') ORDER BY 1;"
```

**Ожидаемый результат**

- База `kanban_account` есть.
- Роли `kanban_app`, `kanban_migrator` и `kanban_operator` есть. Их создаёт первый запуск пустого тома скриптом `docker/postgres/init/01-roles.sh`.

### Шаг 3. Владелец таблиц

**Действие**

```bash
docker compose exec -T -e PGPASSWORD="$POSTGRES_PASSWORD" postgres \
  psql -w -h 127.0.0.1 -U postgres -d kanban_account -c \
  "SELECT c.relname, pg_get_userbyid(c.relowner) AS owner
   FROM pg_class c
   JOIN pg_namespace n ON n.oid = c.relnamespace
   WHERE n.nspname = 'public' AND c.relkind = 'r'
   ORDER BY 1;"
```

**Ожидаемый результат**

- Владелец каждой таблицы `public`, включая `__EFMigrationsHistory`, — `kanban_migrator`.
- Среди таблиц есть `account`, `board`, `column`, `card`, `card_tag`, `access`, `account_audit_event`, `unowned_audit_event`.
- `kanban_app` не владеет ни одной из них.

### Шаг 4. Роль приложения и роль оператора

**Действие**

```bash
docker compose exec -T -e PGPASSWORD="$APP_DB_PASSWORD" postgres \
  psql -w -h 127.0.0.1 -U kanban_app -d kanban_account -c \
  "SELECT 1 FROM unowned_audit_event LIMIT 1" || true
docker compose exec -T -e PGPASSWORD="$OPERATOR_DB_PASSWORD" postgres \
  psql -w -h 127.0.0.1 -U kanban_operator -d kanban_account -c \
  "SELECT count(*) FROM unowned_audit_event;"
```

**Ожидаемый результат**

- Вызов от `kanban_app` заканчивается отказом доступа (`permission denied`) и не печатает строки журнала.
- Вызов от `kanban_operator` завершается успешно. Пока входов не было, счётчик равен 0.

Команды идут в контейнер по `127.0.0.1` с паролем роли. `|| true` только не даёт отказу `kanban_app` оборвать вставленный блок: текст отказа на экране и есть результат.

### Шаг 5. Готовность при живой базе

**Действие**

```bash
api GET /v1/health/ready
```

**Ожидаемый результат**

- Статус `200`.
- Тело пустое.

## 2. Уникальный ник

Создание учётки само по себе вход не открывает. В ответе нет `accessToken` и нет пароля.

### Шаг 6. Ник с пробелами по краям

**Действие**

```bash
api POST /v1/accounts '{"nick":"  marina  ","password":"любой-пароль"}'
```

**Ожидаемый результат**

- Статус `201`.
- `Location` содержит путь `/v1/accounts/marina`.
- В теле `nick` и `displayName` равны `marina`.
- Поля `password` нет. Слова `любой-пароль` в теле нет.

### Шаг 7. Повтор ника

**Действие**

```bash
api POST /v1/accounts '{"nick":"marina","password":"другой-секрет"}'
```

**Ожидаемый результат**

- Статус `409`, тело `{"code":"nick_taken"}`.
- Новая учётка не появляется. Что пароль первой учётки остался `любой-пароль`, проверяет шаг 19.

### Шаг 8. Регистр различает учётки

**Действие**

```bash
api POST /v1/accounts '{"nick":"Marina","password":"другой"}'
```

**Ожидаемый результат**

- Статус `201`.
- `nick` и `displayName` равны `Marina`.
- Учётка `marina` из шага 6 остаётся отдельной.

### Шаг 9. Пустой ник и ник из пробелов

**Действие**

```bash
api POST /v1/accounts '{"nick":"","password":"ok"}'
api POST /v1/accounts '{"nick":"   ","password":"ok"}'
```

**Ожидаемый результат**

- Оба ответа: статус `400`, тело `{"code":"nick_invalid"}`.
- Учётка не создаётся. Значение не заменяется на обрезку до пустой строки и не сохраняется.

### Шаг 10. Длина ника: 65 отвергается целиком, 64 принимается

**Действие**

```bash
python3 - <<'PY'
import json
open("/tmp/kb-n65.json","w").write(json.dumps({"nick": "n"*65, "password": "ok"}))
open("/tmp/kb-n64.json","w").write(json.dumps({"nick": "n"*64, "password": "ok-64"}))
PY
api POST /v1/accounts "$(cat /tmp/kb-n65.json)"
api POST /v1/accounts "$(cat /tmp/kb-n64.json)"
```

**Ожидаемый результат**

- Первый ответ: `400` и `{"code":"nick_invalid"}`.
- Второй ответ: `201`, `nick` длиной 64 из букв `n`, `displayName` равен этому нику.
- Ник из этих 64 символов свободен: отклонённые 65 символов его не заняли.

### Шаг 11. Пустой пароль и пароль из пробелов

**Действие**

```bash
api POST /v1/accounts '{"nick":"empty-pwd","password":""}'
api POST /v1/accounts '{"nick":"space-pwd","password":"   "}'
```

**Ожидаемый результат**

- Пустой пароль: `400` и `{"code":"password_invalid"}`. Этот код отличается от `nick_invalid` шага 9. Учётки `empty-pwd` нет.
- Пароль из трёх пробелов: `201`, `nick` равен `space-pwd`. Пробелы в пароле не отбрасываются, поэтому такой пароль не считается пустым. В теле пароля нет.

### Шаг 12. Длина пароля: 1025 отвергается, 1024 принимается

**Действие**

```bash
python3 - <<'PY'
import json
open("/tmp/kb-p1025.json","w").write(json.dumps({"nick": "long-pwd", "password": "p"*1025}))
open("/tmp/kb-p1024.json","w").write(json.dumps({"nick": "long-pwd", "password": "p"*1024}))
open("/tmp/kb-p1024-in.json","w").write(json.dumps({"nick": "long-pwd", "password": "p"*1024}))
PY
api POST /v1/accounts "$(cat /tmp/kb-p1025.json)"
api POST /v1/accounts "$(cat /tmp/kb-p1024.json)"
```

**Ожидаемый результат**

- Первый ответ: `400` и `{"code":"password_invalid"}`.
- Второй ответ: `201`, `nick` равен `long-pwd`.
- В обоих телах нет цепочки из букв `p` длиной 1024 или 1025.

### Шаг 13. Лишнее поле

**Действие**

```bash
api POST /v1/accounts '{"nick":"extra-nick","password":"ok","extra":1}'
```

**Ожидаемый результат**

- Статус `400`, тело `{"code":"request_invalid"}`.
- Учётка `extra-nick` не создаётся.

### Шаг 14. Два создания одного ника одновременно

Функцию `api` здесь не используют: она пишет тело в один и тот же файл.

**Действие**

```bash
curl -sS -o /tmp/race-a.json -w 'A %{http_code}\n' \
  -H 'Content-Type: application/json' \
  --data-binary '{"nick":"race-nick","password":"p-one"}' \
  -X POST "$BASE/v1/accounts" &
curl -sS -o /tmp/race-b.json -w 'B %{http_code}\n' \
  -H 'Content-Type: application/json' \
  --data-binary '{"nick":"race-nick","password":"p-two"}' \
  -X POST "$BASE/v1/accounts" &
wait
echo "A body:"; cat /tmp/race-a.json; echo
echo "B body:"; cat /tmp/race-b.json; echo
api POST /v1/sessions '{"nick":"race-nick","password":"p-one"}'
api POST /v1/sessions '{"nick":"race-nick","password":"p-two"}'
```

**Ожидаемый результат**

- Среди A и B ровно один статус `201` и ровно один статус `409`.
- Тело `409` — `{"code":"nick_taken"}`. В телах нет паролей.
- Вход удаётся ровно одним из паролей `p-one` и `p-two`: один ответ `200` с `accessToken`, другой `401` и `{"code":"sign_in_failed"}`. Какой пароль вошёл, совпадает с тем созданием, которое получило `201`.

### Шаг 15. Ник и пароль с кавычкой хранятся буквально

**Действие**

```bash
cat > /tmp/kb-quote.json <<'EOF'
{"nick":"a';drop","password":"secret"}
EOF
api POST /v1/accounts "$(cat /tmp/kb-quote.json)"
api POST /v1/sessions '{"nick":"a'"'"';drop","password":"secret"}'
api POST /v1/sessions '{"nick":"marina","password":"любой-пароль"}'
```

**Ожидаемый результат**

- Создание: `201`, `nick` равен `a';drop`.
- Вход этой парой: `200` и есть `accessToken`. Сравнение прошло по буквальному нику и паролю.
- Вход `marina` с паролем `любой-пароль`: `200`. Учётка `marina` на месте. Чужой ник с кавычкой её не удалил и не подменил.

Токены этого шага дальше не нужны. Следующий успешный вход `marina` сохраняют заново.

## 3. Вход

### Шаг 16. Неверный пароль и неизвестный ник

**Действие**

```bash
api POST /v1/sessions '{"nick":"marina","password":"не-тот"}'
cp /tmp/kb-body /tmp/kb-wrong.json
api POST /v1/sessions '{"nick":"нет-такого","password":"любой-пароль"}'
cp /tmp/kb-body /tmp/kb-missing.json
api POST /v1/sessions '{"nick":"нет-такого","password":"любой-пароль"}'
python3 - <<'PY'
import json
a=json.load(open("/tmp/kb-wrong.json"))
b=json.load(open("/tmp/kb-missing.json"))
assert a==b=={"code":"sign_in_failed"}
assert "любой-пароль" not in open("/tmp/kb-wrong.json").read()
assert "не-тот" not in open("/tmp/kb-wrong.json").read()
print("same sign_in_failed")
PY
```

**Ожидаемый результат**

- Все три ответа: статус `401`, тело ровно `{"code":"sign_in_failed"}`.
- По коду не видно, существует ли ник.
- Учётка `marina` не меняется, учётка `нет-такого` этим входом не создаётся.
- Паролей в телах нет.

Пустой пароль и ник длиннее 64 символов на входе тоже дают `sign_in_failed`. Коды `password_invalid` и `nick_invalid` относятся к созданию учётки.

### Шаг 17. Неизвестный ник виден оператору и не содержит пароль

**Действие**

```bash
docker compose exec -T -e PGPASSWORD="$OPERATOR_DB_PASSWORD" postgres \
  psql -w -h 127.0.0.1 -U kanban_operator -d kanban_account -c \
  "SELECT attempted_nick, event_type, outcome, source
   FROM unowned_audit_event
   WHERE attempted_nick = 'нет-такого'
   ORDER BY occurred_at;"
```

**Ожидаемый результат**

- Две строки, потому что шаг 16 отправил два таких входа.
- `event_type` — `sign_in`, `outcome` — `failure`, `attempted_nick` — `нет-такого`.
- `source` заполнен, в обеих строках совпадает и не содержит пароль.
- Колонки с паролем в выдаче нет.

### Шаг 18. После отказа ник всё ещё свободен

**Действие**

```bash
api POST /v1/accounts '{"nick":"нет-такого","password":"после"}'
docker compose exec -T -e PGPASSWORD="$OPERATOR_DB_PASSWORD" postgres \
  psql -w -h 127.0.0.1 -U kanban_operator -d kanban_account -c \
  "SELECT count(*) FROM unowned_audit_event WHERE attempted_nick = 'нет-такого';"
```

**Ожидаемый результат**

- Создание: `201`. Значит, неудачные входы шага 16 учётку не создали. Иначе ответ был бы `nick_taken`.
- Счётчик оператора по этому нику по-прежнему 2. Создание учётки старые строки не стирает.

### Шаг 19. Успешный вход

**Действие**

```bash
api POST /v1/sessions '{"nick":"marina","password":"любой-пароль"}'
python3 - <<'PY'
import json, base64, re
from datetime import datetime, timezone, timedelta
body=json.load(open("/tmp/kb-body"))
token=body["accessToken"]
assert set(body)=={"accessToken","expiresAt"}
assert "любой-пароль" not in token
payload=token.split(".")[1]
payload += "=" * (-len(payload) % 4)
data=json.loads(base64.urlsafe_b64decode(payload))
assert data["iss"]=="kanban-account"
aud=data["aud"]
assert aud=="kanban-account" or aud==["kanban-account"]
assert int(data["exp"])-int(data["iat"])==43200
raw=body["expiresAt"].replace("Z","+00:00")
raw=re.sub(r"(\.\d{6})\d+", r"\1", raw)
exp=datetime.fromisoformat(raw)
now=datetime.now(timezone.utc)
assert now < exp <= now+timedelta(hours=12, seconds=2)
print("expiresAt", exp.isoformat())
PY
TOKEN_1="$(python3 -c 'import json; print(json.load(open("/tmp/kb-body"))["accessToken"])')"
```

**Ожидаемый результат**

- Статус `200`.
- В теле только `accessToken` и `expiresAt`.
- `expiresAt` позже текущего момента и не позже чем через 12 часов.
- В токене издатель и получатель — `kanban-account`, разница `exp` и `iat` — 43200 секунд.
- Пароля `любой-пароль` в теле и в токене нет.
- Этот вход подтверждает, что повтор шага 7 не сменил пароль на `другой-секрет`.

`TOKEN_1` сохраняют до следующего вызова `api`: тот перезаписывает `/tmp/kb-body`.

### Шаг 20. Второй вход не гасит первый

**Действие**

```bash
api POST /v1/sessions '{"nick":"marina","password":"любой-пароль"}'
TOKEN_2="$(python3 -c 'import json; print(json.load(open("/tmp/kb-body"))["accessToken"])')"
python3 - "$TOKEN_1" "$TOKEN_2" <<'PY'
import sys
assert sys.argv[1] != sys.argv[2]
print("tokens differ")
PY
api GET /v1/accounts/marina '' "$TOKEN_1"
api GET /v1/accounts/marina '' "$TOKEN_2"
```

**Ожидаемый результат**

- Второй вход тоже `200`, токен отличается от `TOKEN_1`.
- Оба чтения профиля — `200`.

### Шаг 21. Завершение гасит только предъявленный доступ

**Действие**

```bash
api DELETE /v1/sessions/current '' "$TOKEN_1"
api GET /v1/accounts/marina '' "$TOKEN_1"
api GET /v1/accounts/marina '' "$TOKEN_2"
api POST /v1/sessions '{"nick":"marina","password":"любой-пароль"}'
TOKEN_1="$(python3 -c 'import json; print(json.load(open("/tmp/kb-body"))["accessToken"])')"
```

**Ожидаемый результат**

- Завершение: `204`, тело пустое.
- Старый `TOKEN_1`: `401` и `{"code":"no_active_access"}`.
- `TOKEN_2`: по-прежнему `200`. Учётка не удалена.
- Новый вход паролем `любой-пароль` снова `200`. Его токен дальше называется `TOKEN_1`.

### Шаг 22. Нет доступа без годного токена

**Действие**

```bash
api GET /v1/accounts/marina
api GET /v1/accounts/marina '' 'not-a-token'
```

**Ожидаемый результат**

- Оба ответа: `401` и `{"code":"no_active_access"}`.
- Профиля, доски и пароля в теле нет.

### Шаг 23. Вход снимает пробелы только у ника

**Действие**

```bash
api POST /v1/sessions '{"nick":"  marina  ","password":"любой-пароль"}'
api POST /v1/sessions '{"nick":"marina","password":"любой-пароль "}'
```

**Ожидаемый результат**

- Ник с пробелами по краям: `200`. Сохранённый ник — `marina`.
- Пароль с хвостовым пробелом: `401` и `{"code":"sign_in_failed"}`. Пробел в пароле — часть пароля, действующий пароль не меняется.

### Шаг 24. Вход паролем из пробелов и паролем длиной 1024

**Действие**

```bash
api POST /v1/sessions '{"nick":"space-pwd","password":""}'
api POST /v1/sessions '{"nick":"space-pwd","password":"   "}'
api POST /v1/sessions "$(cat /tmp/kb-p1024-in.json)"
```

**Ожидаемый результат**

- Пустой пароль для `space-pwd`: `401` и `sign_in_failed`.
- Те же три пробела, что при создании: `200`.
- Пароль из 1024 букв `p` для `long-pwd`: `200`. Отклонённые на шаге 12 лишние символы паролем не стали.

## 4. Профиль: ник, имя, доска и карточки

Дальше рабочий доступ `marina` — `TOKEN_1` после шага 21. `TOKEN_2` тоже ещё жив.

### Шаг 25. Свой профиль новой учётки

**Действие**

```bash
api GET /v1/accounts/marina '' "$TOKEN_1"
python3 - <<'PY'
import json
body=json.load(open("/tmp/kb-body"))
assert body["nick"]=="marina"
assert body["displayName"]=="marina"
names=[c["name"] for c in body["board"]["columns"]]
assert names==["ToDo","InProgress","Done"]
assert all(c["cards"]==[] for c in body["board"]["columns"])
assert "password" not in json.dumps(body)
print("empty board")
PY
```

**Ожидаемый результат**

- Статус `200`.
- `nick` и `displayName` равны `marina`.
- Ровно одна доска, у неё только поле `columns`.
- Колонки по порядку: `ToDo`, `InProgress`, `Done`. Карточек нет.
- Пароля в теле нет.

### Шаг 26. Карточка на своей доске

**Действие**

```bash
api POST /v1/accounts/marina/cards \
  '{"columnName":"ToDo","title":"Купить молоко","description":"2л","tags":["дом"],"deadline":"2026-11-20"}' \
  "$TOKEN_1"
python3 - <<'PY'
import json
card=json.load(open("/tmp/kb-body"))
assert set(card)=={"id","title","description","tags","deadline","createdAt"}
assert card["title"]=="Купить молоко"
assert card["description"]=="2л"
assert card["tags"]==["дом"]
assert card["deadline"]=="2026-11-20"
assert card["createdAt"].endswith("Z") or card["createdAt"].endswith("+00:00")
print(card["id"])
PY
CARD_ID="$(python3 -c 'import json; print(json.load(open("/tmp/kb-body"))["id"])')"
```

**Ожидаемый результат**

- Статус `201`.
- В карточке есть `id`, заголовок `Купить молоко`, описание `2л`, метка `дом`, срок `2026-11-20` и время создания во всемирном виде.
- Полей имени владельца и пароля нет.

`CARD_ID` сохраняют сразу, до следующего вызова `api`.

### Шаг 27. Карточка видна владельцу в колонке ToDo

**Действие**

```bash
api GET /v1/accounts/marina '' "$TOKEN_1"
python3 - "$CARD_ID" <<'PY'
import json, sys
body=json.load(open("/tmp/kb-body"))
cols={c["name"]: c["cards"] for c in body["board"]["columns"]}
assert [c["id"] for c in cols["ToDo"]]==[sys.argv[1]]
assert cols["InProgress"]==[] and cols["Done"]==[]
card=cols["ToDo"][0]
assert card["title"]=="Купить молоко"
assert "displayName" not in card and "nick" not in card
print("card on ToDo")
PY
```

**Ожидаемый результат**

- Статус `200`.
- Карточка лежит в `ToDo` и отсутствует в `InProgress` и `Done`.
- На карточке нет ника и нет отображаемого имени.

### Шаг 28. Смена имени не меняет ник и не переписывает карточку

**Действие**

```bash
api PUT /v1/accounts/marina/display-name '{"displayName":"  Марина К.  "}' "$TOKEN_1"
api GET /v1/accounts/marina '' "$TOKEN_1"
python3 - "$CARD_ID" <<'PY'
import json, sys
body=json.load(open("/tmp/kb-body"))
assert body["nick"]=="marina"
assert body["displayName"]=="Марина К."
card=next(c for col in body["board"]["columns"] for c in col["cards"] if c["id"]==sys.argv[1])
assert card["title"]=="Купить молоко"
assert card["description"]=="2л"
assert card["tags"]==["дом"]
assert card["deadline"]=="2026-11-20"
assert "displayName" not in card
print("name changed once")
PY
```

**Ожидаемый результат**

- Смена имени: `200`, в теле `nick` равен `marina`, `displayName` равен `Марина К.` Пробелы по краям сняты.
- Следующее чтение профиля показывает то же имя и прежний ник.
- Заголовок, описание, метка и срок карточки те же. Отдельной копии имени на карточке нет.

Отдельного вызова смены ника в контракте нет. Чтение профиля показывает прежний ник.

### Шаг 29. Недопустимое имя не затирает прежнее

**Действие**

```bash
api PUT /v1/accounts/marina/display-name '{"displayName":""}' "$TOKEN_1"
api PUT /v1/accounts/marina/display-name '{"displayName":"   "}' "$TOKEN_1"
python3 - <<'PY'
import json
open("/tmp/kb-name129.json","w").write(json.dumps({"displayName": "я"*129}))
PY
api PUT /v1/accounts/marina/display-name "$(cat /tmp/kb-name129.json)" "$TOKEN_1"
api GET /v1/accounts/marina '' "$TOKEN_1"
```

**Ожидаемый результат**

- Три смены: каждая `400` и `{"code":"display_name_invalid"}`.
- Чтение профиля: `displayName` по-прежнему `Марина К.`, `nick` по-прежнему `marina`. Имя из 129 символов не обрезано до 128 и не записано.

### Шаг 30. Одинаковое имя у двух людей

**Действие**

```bash
api POST /v1/sessions '{"nick":"Marina","password":"другой"}'
TOKEN_MARINA="$(python3 -c 'import json; print(json.load(open("/tmp/kb-body"))["accessToken"])')"
api PUT /v1/accounts/Marina/display-name '{"displayName":"Марина К."}' "$TOKEN_MARINA"
api GET /v1/accounts/Marina '' "$TOKEN_MARINA"
api GET /v1/accounts/marina '' "$TOKEN_1"
```

**Ожидаемый результат**

- Вход `Marina`: `200`.
- Смена имени: `200`, `nick` равен `Marina`, `displayName` равен `Марина К.`
- Оба профиля показывают имя `Марина К.` и разные ники: `Marina` и `marina`.
- На доске `Marina` нет карточки `Купить молоко`.

### Шаг 31. Чужая доска

**Действие**

```bash
api GET /v1/accounts/marina '' "$TOKEN_MARINA"
api GET /v1/accounts/Marina '' "$TOKEN_1"
```

**Ожидаемый результат**

- Оба ответа: `403` и `{"code":"foreign_data"}`.
- В теле нет заголовка карточки, нет описания и нет пароля.
- Вызова, который открыл бы свою доску другому нику, у этих маршрутов нет: чужой путь отвечает `foreign_data`.

### Шаг 32. Текст карточки с кавычками хранится буквально

**Действие**

```bash
cat > /tmp/kb-card.json <<'EOF'
{"columnName":"ToDo","title":"'; DROP TABLE account;--","tags":["дом"]}
EOF
api POST /v1/accounts/marina/cards "$(cat /tmp/kb-card.json)" "$TOKEN_1"
LITERAL_ID="$(python3 -c 'import json; print(json.load(open("/tmp/kb-body"))["id"])')"
api GET /v1/accounts/marina '' "$TOKEN_1"
api GET /v1/accounts/Marina '' "$TOKEN_MARINA"
```

**Ожидаемый результат**

- Создание: `201`, `title` равен `'; DROP TABLE account;--`.
- В профиле `marina` этот заголовок лежит в `ToDo` рядом с `Купить молоко`.
- Профиль `Marina` по-прежнему `200` со своим ником. Вторая учётка и её доска на месте.
- `LITERAL_ID` нужен после удаления первой карточки.

### Шаг 33. Чужой не удаляет карточку

**Действие**

```bash
api DELETE "/v1/accounts/marina/cards/${CARD_ID}" '' "$TOKEN_MARINA"
api GET /v1/accounts/marina '' "$TOKEN_1"
```

**Ожидаемый результат**

- Удаление: `403` и `{"code":"foreign_data"}`.
- Чтение владельца: карточка `Купить молоко` всё ещё на доске.

### Шаг 34. Владелец удаляет карточку

**Действие**

```bash
api DELETE "/v1/accounts/marina/cards/${CARD_ID}" '' "$TOKEN_1"
api GET /v1/accounts/marina '' "$TOKEN_1"
python3 - "$CARD_ID" "$LITERAL_ID" <<'PY'
import json, sys
body=json.load(open("/tmp/kb-body"))
cards=[c for col in body["board"]["columns"] for c in col["cards"]]
ids=[c["id"] for c in cards]
titles=[c["title"] for c in cards]
assert sys.argv[1] not in ids
assert sys.argv[2] in ids
assert "Купить молоко" not in titles
assert "'; DROP TABLE account;--" in titles
print("milk card gone")
PY
```

**Ожидаемый результат**

- Удаление: `204`, тело пустое.
- В профиле нет заголовка `Купить молоко` и нет `CARD_ID`.
- Карточка `LITERAL_ID` с заголовком `'; DROP TABLE account;--` остаётся в `ToDo`.

### Шаг 35. Нет своей карточки и нет своей колонки

**Действие**

```bash
api DELETE /v1/accounts/marina/cards/00000000-0000-0000-0000-000000000000 '' "$TOKEN_1"
api POST /v1/accounts/marina/cards '{"columnName":"Missing","title":"Купить молоко"}' "$TOKEN_1"
```

**Ожидаемый результат**

- Оба ответа: `404` и `{"code":"object_missing"}`.
- Имя `Missing` не совпадает с `ToDo`, `InProgress` и `Done`. Колонка не создаётся, карточка не появляется.
- Имя колонки в этом шаге — `Missing`. Имя `todo` совпало бы с уже существующей колонкой `ToDo`: поиск колонки при создании карточки не учитывает регистр.

### Шаг 36. Пустой заголовок

**Действие**

```bash
api POST /v1/accounts/marina/cards '{"columnName":"ToDo","title":""}' "$TOKEN_1"
```

**Ожидаемый результат**

- Статус `400`, тело `{"code":"card_invalid"}`.
- Новая карточка на доске не появляется.

## 5. Смена пароля

Перед сменой живы два доступа `marina`: `TOKEN_1` и `TOKEN_2`. Пароль всё ещё `любой-пароль`.

### Шаг 37. Неверный текущий пароль

**Действие**

```bash
api PUT /v1/accounts/marina/password \
  '{"currentPassword":"не-тот","newPassword":"новый-секрет"}' \
  "$TOKEN_1"
api POST /v1/sessions '{"nick":"marina","password":"любой-пароль"}'
api POST /v1/sessions '{"nick":"marina","password":"новый-секрет"}'
```

**Ожидаемый результат**

- Смена: `403` и `{"code":"password_change_rejected"}`. Паролей в теле нет.
- Вход старым паролем: `200`.
- Вход паролем `новый-секрет`: `401` и `sign_in_failed`. Действующий пароль остался прежним.
- Токен этого успешного входа дальше не используют, чтобы не путать его с `TOKEN_1`.

### Шаг 38. Пустой новый пароль

**Действие**

```bash
api PUT /v1/accounts/marina/password \
  '{"currentPassword":"любой-пароль","newPassword":""}' \
  "$TOKEN_1"
api POST /v1/sessions '{"nick":"marina","password":"любой-пароль"}'
```

**Ожидаемый результат**

- Смена: `400` и `{"code":"password_invalid"}`.
- Старый пароль по-прежнему входит: `200`.

### Шаг 39. Смена на тот же пароль

**Действие**

```bash
api PUT /v1/accounts/marina/password \
  '{"currentPassword":"любой-пароль","newPassword":"любой-пароль"}' \
  "$TOKEN_1"
api GET /v1/accounts/marina '' "$TOKEN_1"
```

**Ожидаемый результат**

- Смена: `204`, тело пустое.
- Тот же токен читает профиль: `200`, `nick` равен `marina`, `displayName` равен `Марина К.`

### Шаг 40. Новый пароль, прежние доступы живы

**Действие**

```bash
api PUT /v1/accounts/marina/password \
  '{"currentPassword":"любой-пароль","newPassword":"новый-секрет"}' \
  "$TOKEN_1"
api GET /v1/accounts/marina '' "$TOKEN_1"
api GET /v1/accounts/marina '' "$TOKEN_2"
```

**Ожидаемый результат**

- Смена: `204`, тело пустое. Пароля `новый-секрет` в ответе нет.
- И `TOKEN_1`, и `TOKEN_2` читают профиль: `200`. Смена пароля эти доступы не завершает.
- В профиле по-прежнему ник `marina`, имя `Марина К.` и карточка с буквальным заголовком. Карточки `Купить молоко` нет.

### Шаг 41. Старый пароль больше не входит

**Действие**

```bash
api DELETE /v1/sessions/current '' "$TOKEN_1"
api GET /v1/accounts/marina '' "$TOKEN_1"
api GET /v1/accounts/marina '' "$TOKEN_2"
api POST /v1/sessions '{"nick":"marina","password":"любой-пароль"}'
api POST /v1/sessions '{"nick":"marina","password":"новый-секрет"}'
TOKEN_3="$(python3 -c 'import json; print(json.load(open("/tmp/kb-body"))["accessToken"])')"
```

**Ожидаемый результат**

- Завершение `TOKEN_1`: `204`.
- Чтение старым `TOKEN_1`: `401` и `no_active_access`.
- `TOKEN_2`: всё ещё `200`.
- Вход паролем `любой-пароль`: `401` и `sign_in_failed`.
- Вход паролем `новый-секрет`: `200`. Этот токен — `TOKEN_3`.

### Шаг 42. Чужой не меняет пароль

**Действие**

```bash
api PUT /v1/accounts/marina/password \
  '{"currentPassword":"другой","newPassword":"чужой-секрет"}' \
  "$TOKEN_MARINA"
api POST /v1/sessions '{"nick":"marina","password":"новый-секрет"}'
api POST /v1/sessions '{"nick":"marina","password":"чужой-секрет"}'
```

**Ожидаемый результат**

- Смена с токеном `Marina` по пути `marina`: `403` и `{"code":"foreign_data"}`.
- `новый-секрет` входит: `200`.
- `чужой-секрет` не входит: `401` и `sign_in_failed`.

### Шаг 43. В таблице нет открытого пароля

**Действие**

```bash
docker compose exec -T -e PGPASSWORD="$POSTGRES_PASSWORD" postgres \
  psql -w -h 127.0.0.1 -U postgres -d kanban_account -c \
  "SELECT nick, display_name,
          position('любой-пароль' in password_hash) AS old_plain,
          position('новый-секрет' in password_hash) AS new_plain,
          password_hash = 'новый-секрет' AS hash_equals_password
   FROM account
   WHERE nick = 'marina';"
```

**Ожидаемый результат**

- Строка `marina` есть, `display_name` равен `Марина К.`
- `old_plain` и `new_plain` равны 0, `hash_equals_password` равен `f`.
- Открытого старого и нового пароля в сохранённом значении нет.

## 6. Остановка контейнера и сохранность данных

### Шаг 44. Перезапуск API на том же контейнере

**Действие**

Остановить процесс API во втором терминале (Ctrl+C) и запустить его той же командой, с теми же переменными:

```bash
ASPNETCORE_URLS=http://localhost:5050 ASPNETCORE_HTTPS_PORTS= \
  dotnet run --project kanban-cli/src/Kanban.Api --no-launch-profile
```

Когда процесс снова слушает порт, в первом терминале:

```bash
api GET /v1/health/ready
api POST /v1/sessions '{"nick":"marina","password":"новый-секрет"}'
api GET /v1/accounts/marina '' "$TOKEN_2"
```

**Ожидаемый результат**

- Готовность: `200`, тело пустое.
- Вход новым паролем: `200`.
- `TOKEN_2`, выданный до перезапуска, снова читает профиль: `200`.
- В профиле ник `marina`, имя `Марина К.`, в `ToDo` есть заголовок `'; DROP TABLE account;--` и нет `Купить молоко`.
- Контейнер `postgres` в этом шаге остаётся запущенным. Профиль прочитан из него после нового старта процесса API.

### Шаг 45. Остановить только контейнер

**Действие**

```bash
docker compose stop postgres
```

**Ожидаемый результат**

- Сервис `postgres` остановлен.
- Процесс API во втором терминале остаётся запущенным. Его не переключают на другую базу и не перезапускают.

### Шаг 46. Отказ, пока хранилище недоступно

Ответ может прийти не сразу: драйвер ждёт отказ соединения. Код после ожидания тот же.

**Действие**

```bash
api GET /v1/health/ready
api POST /v1/accounts '{"nick":"half-nick","password":"секрет"}'
api POST /v1/sessions '{"nick":"marina","password":"новый-секрет"}'
api GET /v1/accounts/marina '' "$TOKEN_2"
api PUT /v1/accounts/marina/display-name '{"displayName":"Чужое имя"}' "$TOKEN_2"
api PUT /v1/accounts/marina/password \
  '{"currentPassword":"новый-секрет","newPassword":"не-записался"}' \
  "$TOKEN_2"
```

**Ожидаемый результат**

- Все шесть ответов: статус `503`, тело `{"code":"store_unavailable"}`.
- В телах нет пароля, нет имени `Чужое имя`, нет доски и нет карточек.
- Готовность: `503` и тот же код. Тело ответа не пустое.

### Шаг 47. После запуска контейнера данные целы

**Действие**

```bash
docker compose start postgres
for _ in 1 2 3 4 5 6 7 8 9 10; do
  docker compose exec -T postgres pg_isready -U postgres && break
  sleep 2
done
for _ in 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15; do
  code="$(curl -sS -o /tmp/kb-ready -w '%{http_code}' "$BASE/v1/health/ready" || true)"
  if [ "$code" = "200" ]; then
    break
  fi
  sleep 2
done
api GET /v1/health/ready
api POST /v1/accounts '{"nick":"half-nick","password":"секрет"}'
api POST /v1/sessions '{"nick":"marina","password":"не-записался"}'
api POST /v1/sessions '{"nick":"marina","password":"новый-секрет"}'
TOKEN_4="$(python3 -c 'import json; print(json.load(open("/tmp/kb-body"))["accessToken"])')"
api GET /v1/accounts/marina '' "$TOKEN_4"
```

Цикл готовности ждёт, пока API снова видит базу. Сам API не перезапускают и строку подключения не меняют.

**Ожидаемый результат**

- Последняя готовность: `200`, тело пустое.
- Создание `half-nick`: `201`. Учётка не была записана, пока контейнер стоял.
- Пароль `не-записался`: `401` и `sign_in_failed`. Смена во время остановки не записалась.
- Пароль `новый-секрет`: `200`.
- Профиль: `nick` равен `marina`, `displayName` равен `Марина К.`
- В `ToDo` есть буквальный заголовок `'; DROP TABLE account;--`. Заголовка `Купить молоко` нет.

## Сводка кодов отказа

| Код | HTTP | Где вызван |
| --- | --- | --- |
| `nick_taken` | 409 | шаги 7 и 14 |
| `nick_invalid` | 400 | шаги 9 и 10 |
| `password_invalid` | 400 | шаги 11, 12 и 38 |
| `sign_in_failed` | 401 | шаги 14, 16, 23, 24, 37, 41, 42 и 47; один и тот же код, когда пароль неверен и когда ника нет |
| `no_active_access` | 401 | шаги 21 и 22 |
| `foreign_data` | 403 | шаги 31, 33 и 42 |
| `display_name_invalid` | 400 | шаг 29 |
| `password_change_rejected` | 403 | шаг 37 |
| `store_unavailable` | 503 | шаг 46 |
| `request_invalid` | 400 | шаг 13 |
| `card_invalid` | 400 | шаг 36 |
| `object_missing` | 404 | шаг 35 |

Прогон пройден, если каждый шаг дал указанный статус, код и состояние учётки, а сводка закрыта без пропуска.

Остановка после прогона: во втором терминале остановить API, затем `docker compose stop postgres`. Том остаётся. Повторный прогон начинают с `docker compose down -v` в подготовке, иначе старые ники ответят `nick_taken`.
