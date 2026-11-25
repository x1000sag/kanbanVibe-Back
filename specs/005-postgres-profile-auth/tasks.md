---
description: "Задачи реализации профиля учётной записи и авторизации класса C"
---

# Tasks: Профиль учётной записи и авторизация класса C

**Input**: Design documents from `/specs/005-postgres-profile-auth/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Тесты включены. Конституция II требует тесты критического пути (авторизация, запись доски, миграция, смена секрета). План (раздел Testing) и research.md, решение 16, задают xUnit, `WebApplicationFactory` и Testcontainers.PostgreSql. Внутри истории тесты пишутся первыми и должны падать до реализации.

**Organization**: Задачи сгруппированы по пользовательским историям, чтобы каждую историю можно было реализовать и проверить отдельно.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: можно выполнять параллельно (разные файлы, нет зависимости от незавершённой задачи)
- **[Story]**: история (`US1`…`US5`). У Setup, Foundational и Polish метки истории нет
- В описании указан точный путь к файлу

## Path Conventions

- Проекты: `kanban-cli/src/`, тесты: `kanban-cli/tests/`
- Решение: `kanban-cli/Kanban.sln`
- Контракт HTTP: `specs/005-postgres-profile-auth/contracts/account-api.yaml`
- Журнал оператора: `specs/005-postgres-profile-auth/contracts/operator-audit.md`
- Блокировка пакетов: `RestorePackagesWithLockFile` в `Directory.Build.props`. После каждого нового `PackageReference` обновлять `packages.lock.json` того же проекта. Патч Npgsql 9.0.x и Testcontainers 4.x не выдумывать: его фиксирует lock-файл при restore

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Новые проекты учётки и пакеты, без поведения историй

- [X] T001 [P] Создать проект `kanban-cli/src/Account.Domain/Account.Domain.csproj` (TFM `net10.0`, `Nullable`, `ImplicitUsings`) без ссылок на EF Core, ASP.NET и PostgreSQL и сгенерировать `kanban-cli/src/Account.Domain/packages.lock.json`
- [X] T002 [P] Создать проект `kanban-cli/src/Account.Infrastructure/Account.Infrastructure.csproj` со ссылкой на `Account.Domain` и пакетами `Microsoft.EntityFrameworkCore` 9.0.0, `Microsoft.EntityFrameworkCore.Design` 9.0.0 (`PrivateAssets` all), `Npgsql.EntityFrameworkCore.PostgreSQL` 9.0.x, `Microsoft.Extensions.Identity.Core` 10.0.13; сгенерировать `kanban-cli/src/Account.Infrastructure/packages.lock.json`
- [X] T003 Включить оба проекта в папку `src` решения `kanban-cli/Kanban.sln` после T001 и T002
- [X] T004 [P] Добавить `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.13 в `kanban-cli/src/Kanban.Api/Kanban.Api.csproj` и обновить `kanban-cli/src/Kanban.Api/packages.lock.json`. Ссылку на проекты Account сюда не добавлять
- [X] T005 [P] Добавить `Testcontainers.PostgreSql` 4.x и выровнять `Microsoft.AspNetCore.Mvc.Testing` на 10.0.13 в `kanban-cli/tests/Kanban.Tests/Kanban.Tests.csproj`, обновить `kanban-cli/tests/Kanban.Tests/packages.lock.json`. Ссылки на проекты Account и `Kanban.Api` сюда не добавлять

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Схема, хэш, JWT, тело отказа и тестовый хост. Без этого ни одна история не начинается

**⚠️ CRITICAL**: Пока эта фаза не завершена, истории не начинать

- [X] T006 Добавить `ProjectReference` на `Account.Domain` и `Account.Infrastructure` в `kanban-cli/src/Kanban.Api/Kanban.Api.csproj` и на `Kanban.Api`, `Account.Domain`, `Account.Infrastructure` в `kanban-cli/tests/Kanban.Tests/Kanban.Tests.csproj`
- [X] T007 [P] Завести каталог отказов в `kanban-cli/src/Account.Domain/Refusal.cs`: тело только `code`, без фразы для человека, без ProblemDetails, без пароля. Коды: `nick_taken`, `nick_invalid`, `password_invalid`, `sign_in_failed`, `no_active_access`, `foreign_data`, `display_name_invalid`, `password_change_rejected`, `store_unavailable`, `request_invalid`, `card_invalid`, `object_missing`. Рядом зафиксировать вторичный HTTP из research.md §4 (409, 400, 400, 401, 401, 403, 400, 403, 503, 400, 400, 404)
- [X] T008 [P] Завести правила текста в `kanban-cli/src/Account.Domain/TextRules.cs`: длина в кодовых точках Unicode (`Rune`), не в UTF-16 и не в графемах; без NFC/NFD. Ник и отображаемое имя: снять краевые пробелы через `char.IsWhiteSpace`, затем пустота и длина; пробелы внутри сохраняются; регистр ника не выравнивается. Пароль не обрезается; пароль из одних пробелов допустим. Заголовок и метки карточки не обрезаются
- [X] T009 [P] Описать учётку в `kanban-cli/src/Account.Infrastructure/Persistence/AccountEntity.cs`, таблица `account`: `account_id` UUID, первичный ключ; `nick` не пусто, `char_length` 1..64, без краевых пробелов, уникален `COLLATE "C"`; `display_name` не пусто, `char_length` 1..128, без краевых пробелов, уникальность не нужна; `password_hash` не пусто, строка Identity V3, не пароль. Колонки открытого пароля нет
- [X] T010 [P] Описать доску в `kanban-cli/src/Account.Infrastructure/Persistence/BoardEntity.cs`, таблица `board`: `board_id` UUID, первичный ключ; `account_id` не пусто, уникален, внешний ключ на `account`, `ON DELETE RESTRICT`. Имени доски и ника нет
- [X] T011 [P] Описать колонку в `kanban-cli/src/Account.Infrastructure/Persistence/ColumnEntity.cs`, таблица `column`: `column_id` UUID, первичный ключ; `board_id` не пусто, внешний ключ на `board`, `ON DELETE RESTRICT`; `name` не пусто и хранится буквально; `position` `smallint`, уникальна в пределах `board_id`. Уникальность имени: индекс `(board_id, lower(name))` и точная пара `(board_id, name)`
- [X] T012 [P] Описать карточку в `kanban-cli/src/Account.Infrastructure/Persistence/CardEntity.cs`, таблица `card`: `card_id` UUID, первичный ключ, задаёт сервер; `column_id` не пусто, внешний ключ на `column`, `ON DELETE CASCADE`; `title` не пусто, буквально, без обрезки; `description` — NULL допустим, переданная пустая строка хранится пустой строкой; `deadline` `date` или NULL, без времени суток; `created_at` `timestamptz` UTC, не NULL, дробная часть секунды нулевая. Колонок `board_id`, ника и имени владельца нет
- [X] T013 [P] Описать метку в `kanban-cli/src/Account.Infrastructure/Persistence/CardTagEntity.cs`, таблица `card_tag`: первичный ключ `(card_id, tag)`; `card_id` — внешний ключ на `card`, `ON DELETE CASCADE`; `tag` не пусто и буквально; дополнительная уникальность `(card_id, lower(tag))`. Отдельной таблицы-справочника тегов нет
- [X] T014 [P] Описать доступ в `kanban-cli/src/Account.Infrastructure/Persistence/AccessEntity.cs`, таблица `access`: `access_id` UUID, первичный ключ (он же `jti`); `account_id` не пусто, внешний ключ на `account`, не уникален; `issued_at` `timestamptz` UTC, целая секунда; `ended_at` `timestamptz` или NULL. Колонки `expires_at` нет. Источник обращения в этой таблице не хранится
- [X] T015 [P] Описать журнал владельца в `kanban-cli/src/Account.Infrastructure/Persistence/AccountAuditEventEntity.cs`, таблица `account_audit_event`: `event_id` UUID, первичный ключ; `account_id` не пусто, внешний ключ на `account`, `ON DELETE RESTRICT`; `occurred_at` `timestamptz` UTC, целая секунда; `event_type` одно из `sign_in`, `access_end`, `password_change`, `display_name_change`, `foreign_access_denied`, `column_deleted`, `card_deleted`; `outcome` `success` или `failure`; `source` NULL или строка 1..45 и обязателен тогда и только тогда, когда `event_type = sign_in`; `object_name` NULL или текст и обязателен тогда и только тогда, когда тип `column_deleted` или `card_deleted`. Колонок ника, имени и пароля нет
- [X] T016 [P] Описать журнал без владельца в `kanban-cli/src/Account.Infrastructure/Persistence/UnownedAuditEventEntity.cs`, таблица `unowned_audit_event`: `event_id` UUID, первичный ключ; `attempted_nick` не пусто, `char_length` 1..64, буквально; `occurred_at` `timestamptz` UTC, целая секунда; `event_type` только `sign_in`; `outcome` только `failure`; `source` не пусто, 1..45. Колонки `account_id` нет. Пароль не пишется
- [X] T017 [P] Завести правило жизни доступа в `kanban-cli/src/Account.Domain/AccessLifetime.cs`: доступ жив, пока `ended_at` пусто и `now < issued_at + 12 часов`; `issued_at` обрезается вниз до целой секунды UTC; истёкший доступ не получает отдельную запись «истёк»; мёртвый доступ снова живым не становится; смена пароля и имени строки `access` не меняет; новый вход не заполняет `ended_at` у старых строк. Часы брать из `TimeProvider`, не из `DateTime.UtcNow`
- [X] T018 Собрать `kanban-cli/src/Account.Infrastructure/AccountDbContext.cs`: физические имена таблиц как в data-model.md, ограничения T009–T016 во Fluent API, `COLLATE "C"` на `nick`, запросы только LINQ (без `FromSqlRaw`, `ExecuteSqlRaw` и склейки SQL). Метод `AddAccountStore` регистрирует контекст по строке `KANBAN_ACCOUNT_CONNECTION` (роль `kanban_app`)
- [X] T019 Добавить начальную миграцию с рабочим `Down` в `kanban-cli/src/Account.Infrastructure/Migrations/` и фабрику `kanban-cli/src/Account.Infrastructure/DesignTimeDbContextFactory.cs`, которая читает `KANBAN_MIGRATOR_CONNECTION`. `Up` создаёт таблицы T009–T016 и не создаёт `expires_at`, колонку ника в журнале владельца, `account_id` в журнале без владельца, `board_id` на карточке и открытый пароль. Имя таблицы `column` экранировать: в PostgreSQL это зарезервированное слово. CHECK: `char_length(nick)` 1..64 и ник без краевых пробелов; `char_length(display_name)` 1..128 и имя без краевых пробелов; `password_hash` не пуст; `source` журнала владельца обязателен ровно при `sign_in` и длиной 1..45; `object_name` обязателен ровно при удалении колонки или карточки; у журнала без владельца тип только `sign_in`, исход только `failure`, `source` 1..45, `attempted_nick` 1..64. Индекс ника `COLLATE "C"`. Триггер `BEFORE UPDATE` отвергает смену `nick`. Триггер отвергает `DELETE` учётки. Триггер отвергает `UPDATE` и `DELETE` обеих таблиц журнала для любой роли. `Down` снимает только таблицы этой фичи. Владелец таблиц — `kanban_migrator`. Роль `kanban_app` получает DML учётки, доски, колонок, карточек, меток и доступа, `SELECT` и `INSERT` журнала владельца, только `INSERT` журнала без владельца, и не получает `UPDATE`/`DELETE` журналов и `SELECT` журнала без владельца. Роль `kanban_operator` получает только `SELECT` журнала без владельца
- [X] T020 [P] Реализовать хэш в `kanban-cli/src/Account.Infrastructure/PasswordHasher.cs`: `PasswordHasher<object>`, Identity V3, параметры по умолчанию, число итераций не снижать. В хранилище только строка хэша. Для отсутствующего ника вызывать `VerifyHashedPassword` на постоянном учебном хэше, который не принадлежит ни одной учётке и может лежать в коде
- [X] T021 [P] Реализовать выпуск JWT в `kanban-cli/src/Account.Infrastructure/AccessTokenIssuer.cs`: HS256, `iss` и `aud` равны `kanban-account`, `sub` — id учётки, `jti` — id доступа, `iat` и `exp` в секундах, `exp = iat + 43200`. Ключ — `KANBAN_JWT_SIGNING_KEY`, не короче 32 байт, в репозитории ключа нет. Refresh-токена нет
- [X] T022 [P] Читать настройку в `kanban-cli/src/Kanban.Api/Account/AccountOptions.cs` только из окружения: `KANBAN_ACCOUNT_CONNECTION`, `KANBAN_MIGRATOR_CONNECTION`, `KANBAN_OPERATOR_CONNECTION`, `KANBAN_JWT_SIGNING_KEY`. В `kanban-cli/src/Kanban.Api/appsettings.json` и `kanban-cli/src/Kanban.Api/appsettings.Development.json` значений этих секретов нет. Ключ короче 32 байт или пустая строка приложения — явный отказ старта, в лог сам ключ и строка подключения не пишутся
- [X] T023 [P] Сделать отказ HTTP в `kanban-cli/src/Kanban.Api/Account/AccountExceptionMiddleware.cs`: ответ `{"code":"<код>"}` и ничего больше; `additionalProperties` запрещены. Неверный JSON, не строки, лишние поля и тело больше 16 КиБ — `request_invalid` (400), а не ProblemDetails и не HTML. Недоступная база или незафиксированная транзакция — `store_unavailable` (503), не `sign_in_failed`. Структурный лог ошибки без пароля, хэша, токена, тела запроса и строки подключения
- [X] T024 [P] Сделать `GET /v1/health/ready` в `kanban-cli/src/Kanban.Api/Account/HealthController.cs`: 200 и пустое тело, пока база принимает запрос; 503 и `{"code":"store_unavailable"}`, когда не принимает. Маршрут без токена
- [X] T025 Подключить учётку в `kanban-cli/src/Kanban.Api/Program.cs`: `AddAccountStore`, JwtBearer с `ClockSkew` ноль и проверкой `iss`/`aud` = `kanban-account`, `TimeProvider`, лимит тела 16 КиБ на методах этой фичи, middleware из T023. Не вызывать `EnsureCreated` и `Database.Migrate` для этой базы. Добавить `public partial class Program`. Существующий `BoardRepository` для `data/board.json` и PascalCase контроллера `kanban-cli/src/Kanban.Api/Controllers/BoardController.cs` не менять. Модели `/v1` сериализовать как в контракте: camelCase (`nick`, `displayName`, `accessToken`, `expiresAt`, `createdAt`), срок карточки — строка `YYYY-MM-DD` или null
- [X] T026 Поднять хост в `kanban-cli/tests/Kanban.Tests/Account/AccountApiFactory.cs`: `WebApplicationFactory` для `Program`, контейнер PostgreSQL 17 через Testcontainers, роли `kanban_migrator`, `kanban_app`, `kanban_operator` с паролями, сгенерированными в тесте, миграция от имени владельца, приложение от имени `kanban_app`, подменяемый `TimeProvider`. Секреты в репозиторий не записывать

**Checkpoint**: Схема, хэш, JWT и хост готовы. Истории можно вести по приоритету

---

## Phase 3: User Story 1 — Регистрация и вход (Priority: P1) 🎯 MVP

**Goal**: Человек создаёт учётную запись ником и паролем и входит этой парой. Повторный ник не создаёт вторую учётку. Неверная пара не открывает доступ и не показывает, что именно не совпало

**Independent Test**: Создать учётную запись новым ником и войти верным паролем. Отдельно проверить отказ в повторном нике и отказ в неверной паре

### Tests for User Story 1

> Писать эти тесты первыми и убедиться, что они падают до реализации

- [X] T027 [P] [US1] Написать интеграционные тесты регистрации в `kanban-cli/tests/Kanban.Tests/Account/RegistrationTests.cs` по `POST /v1/accounts` из `specs/005-postgres-profile-auth/contracts/account-api.yaml`. Ник `marina` и пароль создают учётку: 201, `Location` `/v1/accounts/marina`, тело только `nick` и `displayName`, оба равны `marina`, слова пароля нет. Повтор ника — 409 и ровно `{"code":"nick_taken"}`, прежние имя, хэш и доска не меняются. `marina` и `Marina` — две учётки. Ник `  marina  ` сохраняется как `marina`, следующий `marina` — `nick_taken`. Пустой ник и ник из пробелов — `nick_invalid`; пустой пароль и пароль длиннее 1024 кодовых точек — `password_invalid`; коды разные; значение не обрезается. Ник в 64 кодовые точки и пароль в 1024 принимаются, следующий символ отвергается целиком. Пароль может совпадать с ником, содержать пробелы и состоять из одних пробелов. Два одновременных создания одного нового ника оставляют одну учётку и один хэш; второе получает `nick_taken`. Ник с кавычками и текстом, похожим на SQL, хранится буквально. Тело не JSON, не строки, с лишним полем или больше 16 КиБ — `request_invalid`. В той же транзакции появляются одна доска и колонки `ToDo`, `InProgress`, `Done` на позициях 0, 1, 2. Токен этот вызов не выдаёт. Отказ регистрации в журнал не пишется
- [X] T028 [P] [US1] Написать интеграционные тесты входа в `kanban-cli/tests/Kanban.Tests/Account/SignInTests.cs` по `POST /v1/sessions`. Успех — 200, тело только `accessToken` и `expiresAt`; JWT HS256, `iss` и `aud` равны `kanban-account`, `sub` — id учётки, `jti` — id строки `access`, `exp = iat + 43200`, `expiresAt` не позже выдачи плюс 12 часов. Неверный пароль и несуществующий ник — оба 401 и ровно `{"code":"sign_in_failed"}`; учётка не создаётся и не меняется; по коду нельзя понять, есть ли ник. Пустая пара, пустой ник, ник длиннее 64 и пароль длиннее 1024 на входе тоже `sign_in_failed`, не `nick_invalid` и не `password_invalid`. Для отсутствующего ника вызывается проверка учебного хэша. Второй успешный вход того же ника создаёт вторую строку `access` и не заполняет `ended_at` у первой; токены разные. В ответе пароля нет

### Implementation for User Story 1

- [X] T029 [P] [US1] Реализовать решение о регистрации в `kanban-cli/src/Account.Domain/Registration.cs`: сначала ник (после обрезки краевых пробелов пусто или длиннее 64 кодовых точек — `nick_invalid`), затем пароль (пусто или длиннее 1024 — `password_invalid`). Уникальность ника здесь не читать. Обрезки значения до предела нет
- [X] T030 [P] [US1] Реализовать решение о входе в `kanban-cli/src/Account.Domain/SignIn.cs`: любая неуспешная пара, включая пустые и слишком длинные поля, — один код `sign_in_failed`. Учётку вход не создаёт. Коды `nick_invalid` и `password_invalid` на входе не используются
- [X] T031 [US1] Реализовать `kanban-cli/src/Account.Infrastructure/RegistrationService.cs`: одна транзакция вставляет учётку (`display_name` равен уже обрезанному нику, в колонке только хэш), одну доску и три колонки `ToDo`, `InProgress`, `Done` с позициями 0, 1, 2. Гонка уникального ника становится `nick_taken`, частичной учётки нет. Журнал и строка `access` здесь не пишутся. Сбой фиксации — `store_unavailable`
- [X] T032 [US1] Реализовать `kanban-cli/src/Account.Infrastructure/SignInService.cs`: одна транзакция создаёт строку `access` (`ended_at` пусто, `issued_at` — целая секунда UTC) и возвращает JWT. Старые строки того же ника не гасятся. Если базы нет, ответ `store_unavailable`, не `sign_in_failed`. Запись журнала в эту транзакцию добавит US4, отдельным коммитом её не делать
- [X] T033 [US1] Добавить `POST /v1/accounts` в `kanban-cli/src/Kanban.Api/Account/AccountsController.cs` по схеме `CreateAccount` и ответам `Identity` контракта: 201 без токена и без пароля, 400 `nick_invalid` / `password_invalid` / `request_invalid`, 409 `nick_taken`, 503 `store_unavailable`
- [X] T034 [US1] Добавить `POST /v1/sessions` в `kanban-cli/src/Kanban.Api/Account/SessionsController.cs` по схемам `SignIn` и `Session`: 200, 400 `request_invalid`, 401 `sign_in_failed`, 503 `store_unavailable`. Поле `error_description` не отправлять

**Checkpoint**: Регистрация и вход проверяются без профиля, журнала и смены пароля

---

## Phase 4: User Story 2 — Свой профиль и своя доска (Priority: P1)

**Goal**: После входа владелец видит свой ник, имя и одну доску. Чужой человек и человек без действующего доступа этого не видят и не меняют. Передать доступ нельзя

**Independent Test**: Войти владельцем и прочитать ник, имя и доску, включая карточку, которую он сам поместил. Вторым человеком и без входа убедиться, что те же данные недоступны

### Tests for User Story 2

> Писать эти тесты первыми и убедиться, что они падают до реализации

- [X] T035 [P] [US2] Написать тесты профиля в `kanban-cli/tests/Kanban.Tests/Account/ProfileTests.cs` по `GET /v1/accounts/{nick}`. Ответ 200: `nick`, `displayName`, ровно одна доска без имени, колонки по `position` — `ToDo`, `InProgress`, `Done`, карточек нет, пароля в теле нет. Нет заголовка, битый токен, чужая подпись, завершённый доступ и доступ старше 12 часов дают один код `no_active_access` (401), без текста для человека; данные не меняются. Чужой ник в пути, даже если такого ника нет, — `foreign_data` (403) без содержимого и без пароля. Сначала проверяется живой доступ, и только потом ник пути. Ник пути сравнивается точно, краевые пробелы в пути не снимаются. В `kanban-cli/src/Kanban.Api` нет маршрута, который отдаёт доску другому нику или группе. Новый вход прежней парой после завершения или истечения находит ту же учётку
- [X] T036 [P] [US2] Написать тесты доски в `kanban-cli/tests/Kanban.Tests/Account/BoardTests.cs` по `POST /v1/accounts/{nick}/cards`, `DELETE /v1/accounts/{nick}/cards/{cardId}` и `DELETE /v1/accounts/{nick}/columns/{columnName}`. Карточка с заголовком `Купить молоко`, описанием, одной меткой и сроком `2026-11-20` возвращается владельцу: есть id, заголовок, описание, метки без повторов, срок `YYYY-MM-DD`, `createdAt` в UTC; имени владельца на карточке нет. Другая учётка эту карточку не видит и не меняет. Пустой заголовок, срок не `YYYY-MM-DD` и повтор метки без учёта регистра (`OrdinalIgnoreCase`) — `card_invalid`, карточка не сохраняется частично. Заголовок из пробелов допустим: он не пустой. `description` отсутствует или null — в базе NULL; переданная пустая строка хранится пустой строкой. Своя колонка не найдена и своя карточка не найдена — `object_missing`; чужой id не ищется и даёт `foreign_data`, если ник пути чужой. Удаление своей карточки и своей колонки — 204, содержимое больше не читается и не становится данными другой учётки. Заголовок `'; DROP TABLE account;--` читается обратно буквально, вторая учётка на месте. Колонки в ответе по `position`, карточки по `created_at`, затем по id. Добавления колонки, переименования, правки карточки и переноса нет
- [X] T037 [P] [US2] Написать тесты срока доступа в `kanban-cli/tests/Kanban.Tests/Account/AccessLifetimeTests.cs`. `DELETE /v1/sessions/current` с первым из двух токенов ставит `ended_at` только этой строке: первый токен затем получает `no_active_access`, второй по-прежнему читает профиль, учётка не удалена. Сдвиг `TimeProvider` на 12 часов вперёд даёт тому же токену `no_active_access` без ожидания реального времени; учётка и доска на месте; новый вход снова открывает доступ. `ClockSkew` не удлиняет доступ сверх 12 часов. Смена пароля в этой истории строки `access` не проверяется

### Implementation for User Story 2

- [X] T038 [P] [US2] Реализовать правила доски в `kanban-cli/src/Account.Domain/BoardRules.cs`: имя колонки уникально в доске сравнением `OrdinalIgnoreCase`; метки одной карточки уникальны тем же сравнением, повтор отвергается целиком до записи; пустой заголовок — `card_invalid`, заголовок из пробелов допустим; срок либо null, либо дата `YYYY-MM-DD`; id карточки назначает сервер и не переиспользует. Индекс `lower(...)` в базе не заменяет эту проверку. Новых действий, кроме создания карточки, удаления карточки и удаления колонки, не добавлять
- [X] T039 [US2] Реализовать проверку доступа в `kanban-cli/src/Account.Infrastructure/AccessGuard.cs`: подпись JWT, затем строка `access`. Нет токена, битый токен, чужая подпись, пустой `ended_at` при уже наступившем `issued_at + 12 часов` и заполненный `ended_at` — один код `no_active_access`. Ник пути не равен нику доступа — `foreign_data`, даже если ника нет; существование цели и чужие id не раскрываются. Порядок: сначала доступ, потом ник пути, потом форма
- [X] T040 [P] [US2] Реализовать чтение в `kanban-cli/src/Account.Infrastructure/ProfileReader.cs`: ник, отображаемое имя, колонки по `position`, карточки по `created_at`, затем по `card_id`. Пароля, хэша и имени доски в модели ответа нет. Успешное чтение журнал не пишет
- [X] T041 [US2] Реализовать запись доски в `kanban-cli/src/Account.Infrastructure/BoardWriter.cs`: создание карточки и меток одной транзакцией; физическое удаление карточки; физическое удаление колонки с каскадом карточек. Неверная карточка не оставляет частичной строки. Журнал удаления подключит US4 в ту же транзакцию
- [X] T042 [US2] Добавить `GET /v1/accounts/{nick}` в `kanban-cli/src/Kanban.Api/Account/AccountsController.cs` по схеме `Profile`: 200, 401 `no_active_access`, 403 `foreign_data`, 503 `store_unavailable`
- [X] T043 [US2] Добавить три маршрута доски в `kanban-cli/src/Kanban.Api/Account/BoardController.cs` и `DELETE /v1/sessions/current` в `kanban-cli/src/Kanban.Api/Account/SessionsController.cs` после T039 и T041. Карточка: 201 по схеме `Card`, 400 `card_invalid` или `request_invalid`, 404 `object_missing`. Оба удаления: 204 с пустым телом. Завершение доступа гасит только `jti` предъявленного токена и не удаляет учётку, доску и журнал. Чужой ник на этих маршрутах — `foreign_data`

**Checkpoint**: Профиль и доска владельца читаются и меняются только его живым доступом. Журнал ещё не обязателен для этой проверки

---

## Phase 5: User Story 3 — Смена имени и пароля (Priority: P2)

**Goal**: Владелец меняет отображаемое имя и пароль. Ник остаётся. Имя не уникально. Новый пароль — любое непустое значение 1..1024 кодовых точек, названное вместе с текущим

**Independent Test**: Войти, сменить имя, сменить пароль, прочитать профиль в том же доступе, завершить доступ и войти новым паролем. Старый пароль больше не входит

### Tests for User Story 3

> Писать эти тесты первыми и убедиться, что они падают до реализации

- [X] T044 [P] [US3] Написать тесты имени в `kanban-cli/tests/Kanban.Tests/Account/DisplayNameTests.cs` по `PUT /v1/accounts/{nick}/display-name`. Имя `Марина К.` — 200, тело `Identity`, ник прежний, следующее чтение показывает новое имя, чужие имена не меняются, одно и то же имя у двух людей допустимо. Пустое имя, имя из пробелов и имя длиннее 128 кодовых точек после обрезки краёв — `display_name_invalid`, прежнее имя на месте, обрезки нет. Пробелы внутри сохраняются. Без живого доступа — `no_active_access`, имя не меняется. Чужой ник — `foreign_data`. Имя с кавычками сохраняется буквально. Карточка отдельной копии имени не получает. Строки `access` не меняются
- [X] T045 [P] [US3] Написать тесты пароля в `kanban-cli/tests/Kanban.Tests/Account/PasswordChangeTests.cs` по `PUT /v1/accounts/{nick}/password`. Новый пароль пустой или длиннее 1024 кодовых точек проверяется до сверки текущего и даёт `password_invalid`, хэш не меняется. Текущий не назван или неверен при допустимом новом — `password_change_rejected` (403), хэш прежний. Тот же непустой пароль — успешная смена. Успех — 204 и пустое тело, пароля в ответе нет, тот же токен ещё читает профиль, ни одна строка `access` не гасится. После завершения доступа старый пароль, если он отличался, даёт `sign_in_failed`, новый входит. Пароль из одних пробелов принимается. Без доступа — `no_active_access`. Чужой ник — `foreign_data`

### Implementation for User Story 3

- [X] T046 [P] [US3] Реализовать смену имени в `kanban-cli/src/Account.Domain/DisplayNameChange.cs`: обрезать краевые пробелы, затем отвергнуть пустое значение и значение длиннее 128 кодовых точек кодом `display_name_invalid`. Уникальность не проверять. Молчаливой обрезки нет
- [X] T047 [P] [US3] Реализовать смену пароля в `kanban-cli/src/Account.Domain/PasswordChange.cs`: длина нового пароля (1..1024 кодовых точки, пустой отвергнут, пробелы не отбрасываются) проверяется до сверки текущего. Недопустимый новый — `password_invalid`. Неверный или неназванный текущий при допустимом новом — `password_change_rejected`
- [X] T048 [US3] Реализовать `kanban-cli/src/Account.Infrastructure/AccountUpdateService.cs`: смена имени — одна транзакция, правка `display_name` на месте, истории имени нет. Смена пароля — одна транзакция, на месте только новый `password_hash`, строки `access` не меняются. Старое значение после фиксации не читается ни как пароль, ни как второе имя. Сбой — `store_unavailable`, частичной записи нет
- [X] T049 [US3] Добавить `PUT /v1/accounts/{nick}/display-name` и `PUT /v1/accounts/{nick}/password` в `kanban-cli/src/Kanban.Api/Account/AccountsController.cs` по схемам `DisplayNameChange`, `PasswordChange` и `Identity`. Имя: 200. Пароль: 204 и пустое тело. Коды отказа — из T044 и T045

**Checkpoint**: Имя и пароль меняются отдельно от журнала. Тот же доступ после смены пароля жив

---

## Phase 6: User Story 4 — Журнал и очистка старых значений (Priority: P2)

**Goal**: Важные события защиты записаны и не правятся действиями профиля и доски. Заменённые пароль и имя и удалённое содержимое доски не возвращаются. Чужой журнал не читается. Неудачный вход в несуществующий ник виден только оператору

**Independent Test**: Выполнить успешный вход, неудачный вход, завершение доступа, смену пароля, смену имени, отказ в чужом доступе и удаление карточки. Проверить записи журнала владельца и убедиться, что старые значения не читаются

### Tests for User Story 4

> Писать эти тесты первыми и убедиться, что они падают до реализации

- [X] T050 [P] [US4] Написать тесты журнала владельца в `kanban-cli/tests/Kanban.Tests/Account/JournalTests.cs` по `GET /v1/accounts/{nick}/journal` и схемам `Journal` и `JournalEvent`. Для существующего ника есть записи успешного входа, неудачного входа, завершения доступа, смены пароля, смены имени, отказа в чужих данных и удаления колонки или карточки. У каждой записи время UTC до секунды, ник, тип и исход `success` или `failure`. Ник в ответе приходит соединением с учёткой, а не из колонки события. `source` есть ровно у `sign_in`: один и тот же адрес без порта даёт одну строку, разные адреса — разные, IPv4-mapped IPv6 приводится к IPv4, отсутствия адреса даёт постоянную строку `unknown`, заголовок `X-Forwarded-For` не учитывается, длина не больше 45, сам источник не пароль. `object_name` есть ровно у удаления: у колонки это имя колонки, карточки колонки в журнал поименно не копируются; у карточки это заголовок, без описания, меток и срока. Неудача смены пароля (и неверный текущий, и недопустимый новый) и недопустимое имя пишутся тем же типом с исходом `failure` и без старого и нового значения. Отказ в чужих данных пишется тому, кому отказали, с пустым `object_name`; жертва отдельную строку не получает. Обычное чтение своей доски новую строку не добавляет. Отказ регистрации не пишется. Пустой ник и ник длиннее 64 на входе в журнал оператора не попадают. В теле журнала нет пароля, хэша и отображаемого имени. Чужой `GET` — `foreign_data`, записи не меняются. Действие профиля или доски не выполняет `UPDATE` и `DELETE` журнала. После смены пароля или имени читается только действующее имя, прежний пароль не читается ни как пароль, ни как текст. Удалённая карточка или колонка не возвращается
- [X] T051 [P] [US4] Написать тест оператора в `kanban-cli/tests/Kanban.Tests/Account/OperatorAuditTests.cs` по `specs/005-postgres-profile-auth/contracts/operator-audit.md`. Неудачный вход в ник длиной 1..64, которого нет, пишет `unowned_audit_event` и не появляется ни в чьём `GET .../journal`. Создание такой учётки позже эту строку в её журнал не переносит. Роль `kanban_app` не делает `SELECT` этой таблицы. Роль `kanban_operator` читает только `occurred_at`, `attempted_nick`, `event_type`, `outcome`, `source` запросом из контракта; пароля в строке нет. `UPDATE` и `DELETE` отвергает триггер. HTTP-маршрута журнала оператора нет

### Implementation for User Story 4

- [X] T052 [P] [US4] Описать событие в `kanban-cli/src/Account.Domain/AuditEvent.cs`: типы `sign_in`, `access_end`, `password_change`, `display_name_change`, `foreign_access_denied`, `column_deleted`, `card_deleted`; исход `success` или `failure`; `source` обязателен ровно для `sign_in`; `object_name` обязателен ровно для удаления колонки или карточки. Значения пароля и прежнего имени в событие не входят
- [X] T053 [P] [US4] Определить источник обращения в `kanban-cli/src/Account.Infrastructure/RequestSource.cs`: адрес TCP-узла без порта; IPv4-mapped IPv6 к IPv4; иначе каноническая строка `IPAddress`; нет адреса — `unknown`; `X-Forwarded-For` не читать; строка не длиннее 45
- [X] T054 [P] [US4] Добавить чтение `GET /v1/accounts/{nick}/journal` в `kanban-cli/src/Account.Infrastructure/JournalReader.cs` и `kanban-cli/src/Kanban.Api/Account/JournalController.cs`. Ник подставляется соединением. Порядок устойчивый: `occurred_at`, затем `event_id`. Ответы 200, 401 `no_active_access`, 403 `foreign_data`, 503 `store_unavailable`. Пароля в DTO нет
- [X] T055 [US4] Писать журнал через `kanban-cli/src/Account.Infrastructure/AuditWriter.cs` в той же транзакции, что и само действие, из `kanban-cli/src/Account.Infrastructure/SignInService.cs` (успех и неудача существующего ника — `account_audit_event`; ник 1..64, которого нет, — `unowned_audit_event`), из завершения доступа в `kanban-cli/src/Kanban.Api/Account/SessionsController.cs` (`access_end` / `success`, источник пуст), из `kanban-cli/src/Account.Infrastructure/AccountUpdateService.cs`, из удалений в `kanban-cli/src/Account.Infrastructure/BoardWriter.cs` и из отказа в `kanban-cli/src/Account.Infrastructure/AccessGuard.cs`. Сбой вставки откатывает действие и отвечает `store_unavailable`, а не `sign_in_failed` и не успехом. Регистрацию в `RegistrationService` не журналировать. Вторую миграцию не добавлять: таблицы и триггеры уже в T019

**Checkpoint**: Журнал владельца и журнал оператора читаются по своим правилам, старые значения не возвращаются

---

## Phase 7: User Story 5 — Отдельное хранилище в поставке (Priority: P2)

**Goal**: Оператор поднимает PostgreSQL отдельно от приложения из материалов поставки. Пока хранилище недоступно, вызывающая сторона получает `store_unavailable`, а учётка, имя и пароль не остаются изменёнными наполовину

**Independent Test**: Из материалов поставки запустить хранилище отдельно от приложения, создать учётную запись, заново запустить приложение на том же хранилище и увидеть ту же учётку. Затем остановить хранилище и убедиться, что создание и смена пароля не оставляют частичных данных

### Tests for User Story 5

> Писать эти тесты первыми и убедиться, что они падают до реализации

- [X] T056 [P] [US5] Написать тесты недоступной базы в `kanban-cli/tests/Kanban.Tests/Account/StoreUnavailableTests.cs`. При остановленном PostgreSQL создание учётки, вход, чтение профиля, смена имени и смена пароля возвращают 503 и ровно `{"code":"store_unavailable"}`, без пароля, без данных учётки и без текста для человека. Новая учётка не появляется, имя не меняется наполовину, действующий пароль остаётся прежним. `GET /v1/health/ready` тоже 503. После запуска той же базы прежняя учётка, имя и доска на месте. Запасного чтения из `data/board.json` и SQLite нет
- [X] T057 [P] [US5] Написать тест миграции и ролей в `kanban-cli/tests/Kanban.Tests/Account/MigrationRoleTests.cs`. `Up` применяет схему data-model.md ролью `kanban_migrator`; `kanban_app` таблицами не владеет; `Down` снимает таблицы этой фичи. Пока схемы нет, запрос API даёт `store_unavailable`, а не создание схемы процессом приложения

### Implementation for User Story 5

- [X] T058 [P] [US5] Собрать образ в `docker/postgres/Dockerfile` (`FROM postgres:17`, не `latest` и не образ API) и сценарий `docker/postgres/init/01-roles.sh`: база `kanban_account`, роли `kanban_migrator`, `kanban_app`, `kanban_operator`. Пароли только из `POSTGRES_PASSWORD`, `APP_DB_PASSWORD`, `OPERATOR_DB_PASSWORD`, без значения по умолчанию в файлах репозитория
- [X] T059 [P] [US5] Добавить сервис `postgres` в `docker-compose.yml`, собранный из `docker/postgres/Dockerfile` и отдельный от сервиса `api`. Команда `docker compose up --build postgres` не запускает процесс `Kanban.Api`. Повторный запуск с тем же томом не стирает учётки. Корневой `Dockerfile` по-прежнему публикует только API и копию базы не содержит
- [X] T060 [P] [US5] Дописать в `.github/workflows/cd.yml` укладку корневого `Dockerfile`, `docker/postgres/Dockerfile` и `docker-compose.yml` в архив `kanban-win-x86.zip` рядом с win-x86. Публикацию `dotnet publish` и условие GitHub Release только на тег `v*.*.*` не менять. Тег этой задачей не ставить
- [X] T061 [US5] Закрепить одну главную копию в `kanban-cli/src/Kanban.Api/Program.cs` и службах `kanban-cli/src/Account.Infrastructure/`: доска владельца читается и пишется только в PostgreSQL. Не читать её из `data/board.json` и не читать учётку из SQLite. Контракт `kanban-cli/src/Kanban.Api/Controllers/BoardController.cs` (`/api/board`) и `kanban-cli/src/UserProfile.Api/Controllers/ProfilesController.cs` (`/profiles`) не менять

**Checkpoint**: Хранилище запускается отдельно, остановка не оставляет частичной записи, поставка содержит Dockerfile базы

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Документы смены поведения, сквозные проверки и прогон quickstart

- [X] T062 [P] Добавить `docs/adr/adr-0003-postgres-account-store.md` на русском: учётка, доступ, доска владельца и оба журнала хранятся в PostgreSQL; `data/board.json` из `docs/adr/adr-0001-domain-classes.md` остаётся хранилищем локального CLI и переходного `/api/board`, не доски владельца. ADR-0001 не переписывать
- [X] T063 [P] Поправить документы, где JSON ещё назван единственным хранилищем доски: `docs/product-requirements.md`, `docs/terms.md`, `docs/user-flows.md`, `README.md` и `kanban-cli/src/Kanban.Api/README.md`. Развести локальную доску CLI (`data/board.json`) и доску владельца в PostgreSQL. Контракт `/profiles` не описывать как учётку этой фичи
- [X] T064 [P] Добавить набор из 20 значений с кавычками и обрывками команд в `kanban-cli/tests/Kanban.Tests/Account/LiteralTextTests.cs`: значения, записанные в ник, отображаемое имя или заголовок карточки, читаются буквально; число чужих учёток и содержимое чужих досок не меняются; пароль в ответах не встречается
- [X] T065 Просмотреть структурные логи в `kanban-cli/src/Account.Infrastructure/` и `kanban-cli/src/Kanban.Api/Account/` и убрать из них пароль, хэш, токен, тело запроса и строку подключения
- [X] T066 Убедиться, что `kanban-cli/src/Kanban.Cli/Program.cs` не получает удалённый вход и не вызывает `/v1`. CLI остаётся локальным клиентом JSON-доски. Если отказ сервиса когда-либо показывается из CLI, код переводится в русское сообщение, а выход ненулевой; этот вызов данной фичей не добавлять
- [X] T067 Пройти ручные шаги `specs/005-postgres-profile-auth/quickstart.md` и команду `dotnet test kanban-cli/Kanban.sln`. В прогоне есть регистрация и гонка ника, один код неуспешного входа, два живых доступа и завершение только одного, чужие данные, смена имени и пароля, журнал, буквальный текст, остановка хранилища без частичной записи и отказ роли приложения читать журнал оператора

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: без зависимостей
- **Foundational (Phase 2)**: после Setup. Блокирует все истории
- **User Stories (Phase 3+)**: после Foundational. Дальше — по порядку приоритета, если истории не разведены по людям
- **Polish (Phase 8)**: после историй, которые затрагивает задача. T067 — после всех историй

### User Story Dependencies

- **User Story 1 (P1)**: после Foundational. От других историй не зависит. Создаёт учётку, доску и три колонки, но не отдаёт их по HTTP
- **User Story 2 (P1)**: после US1. Ей нужен выданный доступ и доска, созданная регистрацией. Проверка профиля и карточки сама по себе журнала не требует
- **User Story 3 (P2)**: после US1 и после `GET` профиля из US2 (независимая проверка читает профиль в том же доступе). Журнала не требует
- **User Story 4 (P2)**: после US1, US2 и US3. События входа, завершения доступа, чужого отказа, удаления, смены имени и смены пароля должны уже происходить; эта история дописывает журнал в их транзакции и открывает чтение
- **User Story 5 (P2)**: файлы поставки (T058–T060) можно писать сразу после Foundational, они не зависят от историй. Независимая проверка создания учётки ждёт US1. Проверка, что имя и пароль не записываются наполовину, ждёт US3. Отображение исключения в `store_unavailable` уже есть в T023

### Within Each User Story

- Тесты истории — первыми, и они падают до реализации
- Правила домена — до служб инфраструктуры
- Службы — до маршрутов
- История завершена, когда проходит её независимая проверка

### Parallel Opportunities

- T001, T002, T004 и T005 — разные проекты. T003 ждёт T001 и T002, потому что правит тот же `kanban-cli/Kanban.sln`
- T007–T017 — разные файлы домена и сущностей. T018 ждёт сущности. T019 ждёт T018. T020, T021 и T022 параллельны между собой
- T023 и T024 параллельны. T025 собирает их в хосте. T026 ждёт T025
- В каждой истории тесты помечены `[P]` и пишутся вместе
- T029 и T030 параллельны; T031 и T032 — нет: каждая ждёт своё решение домена и правит свой файл службы последовательно после него
- T038 и T040 параллельны. T039 ждёт правило жизни доступа из Foundational. T041 ждёт T038. T042 ждёт T039 и T040. T043 ждёт T039 и T041, поэтому метки `[P]` нет: маршруты зависят от проверки доступа и записи доски
- T046 и T047 параллельны. T048 ждёт оба
- T052 и T053 параллельны. T054 параллелен подготовке писателя и не ждёт T055. T055 ждёт T052 и T053 и правит службы предыдущих историй
- T056 и T057 параллельны. T058, T059 и T060 параллельны. T061 не параллелен правке `Program.cs` из других незавершённых задач
- T062, T063 и T064 параллельны

---

## Parallel Example: User Story 1

```bash
# Тесты вместе, до реализации:
Task: "T027 RegistrationTests.cs"
Task: "T028 SignInTests.cs"

# Правила домена вместе, после упавших тестов:
Task: "T029 Registration.cs"
Task: "T030 SignIn.cs"
```

## Parallel Example: User Story 2

```bash
Task: "T035 ProfileTests.cs"
Task: "T036 BoardTests.cs"
Task: "T037 AccessLifetimeTests.cs"

# После T039, T040 и T041 контроллеры лежат в разных файлах.
# Метки [P] у них нет: каждый ждёт свою службу.
Task: "T042 GET профиля в AccountsController.cs"
Task: "T043 BoardController.cs и DELETE /v1/sessions/current"
```

## Parallel Example: User Story 3

```bash
Task: "T044 DisplayNameTests.cs"
Task: "T045 PasswordChangeTests.cs"

Task: "T046 DisplayNameChange.cs"
Task: "T047 PasswordChange.cs"
```

## Parallel Example: User Story 4

```bash
Task: "T050 JournalTests.cs"
Task: "T051 OperatorAuditTests.cs"

Task: "T052 AuditEvent.cs"
Task: "T053 RequestSource.cs"

Task: "T054 JournalReader.cs и JournalController.cs"
```

## Parallel Example: User Story 5

```bash
Task: "T056 StoreUnavailableTests.cs"
Task: "T057 MigrationRoleTests.cs"

Task: "T058 docker/postgres/Dockerfile и 01-roles.sh"
Task: "T059 docker-compose.yml"
Task: "T060 .github/workflows/cd.yml"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Завершить Phase 1: Setup
2. Завершить Phase 2: Foundational
3. Завершить Phase 3: User Story 1
4. **Остановиться и проверить**: независимый тест US1 — регистрация, повтор ника, неуспешный вход одним кодом
5. Это контрольная точка входа, не поставка фичи. Спецификация требует все истории и FR-001–FR-024; приоритет задаёт порядок проверки

### Incremental Delivery

1. Setup + Foundational
2. US1 — вход работает
3. US2 — свой профиль и своя доска, чужие данные закрыты
4. US3 — имя и пароль
5. US4 — журнал владельца, журнал оператора, очистка заменённых значений
6. US5 — отдельный контейнер, архив CD, отказ при остановленной базе
7. Polish — ADR, docs, набор из 20 буквальных значений, quickstart и `dotnet test kanban-cli/Kanban.sln`

### Parallel Team Strategy

1. Вместе закончить Setup и Foundational
2. Дальше US2 ждёт US1, US3 ждёт чтения профиля, US4 ждёт действий US1–US3
3. T058–T060 (образ базы, compose, CD) можно вести параллельно с US1, не трогая файлы служб
4. Не делить один файл между двумя незавершёнными задачами: `AccountsController.cs`, `SessionsController.cs` и `Program.cs` править последовательно

---

## Notes

- `[P]` — разные файлы и нет зависимости от незавершённой задачи
- Метка `[USn]` только у задач историй
- Не входят в фичу и отдельных задач не имеют: группы и передача доступа, смена ника, удаление учётки, восстановление пароля, правила состава пароля, блокировка после ошибок, refresh-токен, правка карточки, добавление и переименование колонки, перенос карточки, несколько досок, поля старого профиля (фамилия, почта, дата рождения, фотография)
- `Kanban.Domain` не ссылается на Account, HTTP и PostgreSQL
- Секреты (`KANBAN_JWT_SIGNING_KEY`, пароли ролей) в репозиторий не класть
- Коммит — после каждой задачи или логической группы. Остановиться на checkpoint и проверить историю отдельно
