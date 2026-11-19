# AGENTS.md: KanbanVibe Backend

Этот репозиторий использует OpenCode и Spec Kit. Агенты обязаны прочитать этот файл перед началом работы. Проект: KanbanVibe — профессиональный backend (C# / .NET), а не учебный прототип. Источник норм — `.specify/memory/constitution.md` (v2.0.0): расширяемость, надёжность, функциональность. Язык артефактов — русский.

## Правила Проекта

- Нормы качества задаёт конституция. «Минимальный diff», YAGNI и «достаточно для занятия» не отменяют тесты критического пути, явную ошибку, миграцию и версию контракта.
- Все артефакты (docs, specs, ADR) — на русском языке.
- Опубликованный контракт и схему не ломайте молча. Несовместимое изменение — только через версию или миграцию и ADR.
- Не коммитьте изменения без явной просьбы.
- Комментарии в коде — только там, где это экономит время чтения.
- Фиксируйте результаты (изменённые файлы, команды, вывод) в сессии.

## Ветки

Модель: `main` ← `development/v1.0.0` ← `feature/*`. Сейчас рабочая ветка фичи — `feature/auth`. Ветка `development/v1.0.0` ведёт линию версии 1.0.0.

- `development/v1.0.0` ответвлять от `main`.
- Новую `feature/*` ответвлять от `development/v1.0.0`.
- Ветку задачи ответвлять от текущей `feature/*` (`feature/auth`), не от `main` и не от `development/v1.0.0`.
- PR агента открывать с base = текущая `feature/*` (сейчас `feature/auth`). В `main` и в `development/*` напрямую не открывать.

Продвижение фичи: ветка задачи → `feature/*` → `development/v1.0.0` → `main`. Каждый шаг — отдельный PR.

## Версии и теги

- Релиз — это git-тег `vMAJOR.MINOR.PATCH`. Тег `v1.0.0` соответствует ветке `development/v1.0.0`.
- Тег ставит тот, кто выпускает релиз, на коммите `main` после merge `development/v1.0.0` → `main`. Пока версия не выпущена, тег не создавать (настройка CI/CD релизом не является).
- Push тега `v*.*.*` запускает CD: сборка Windows x86 и GitHub Release с этим архивом. Push в `main` без тега только заливает артефакт, Release не создаёт.

## Spec-Driven Workflow (Spec Kit)

Источник истины по процессу — `.specify/memory/constitution.md`. Команды Spec Kit — в `.opencode/commands/speckit.*.md` (спец-команды OpenCode).

Основной цикл для новой функциональности:

1. `/speckit.constitution` — зафиксировать/обновить принципы проекта.
2. `/speckit.specify` — создать спецификацию в `specs/NNN-feature-name/spec.md`.
3. `/speckit.clarify` — устранить неоднозначности до планирования.
4. `/speckit.plan` — получить `specs/NNN-feature-name/plan.md` с Constitution Check по принципам I–V.
5. `/speckit.tasks` — получить `specs/NNN-feature-name/tasks.md`.
6. `/speckit.implement` — реализовать задачи по порядку.
7. `/speckit.analyze`, `/speckit.checklist` — проверить согласованность артефактов.

Правила:

- Не начинайте реализацию фичи без `spec.md` и `tasks.md`; мелкие исправления и документацию можно делать напрямую, если поведение и контракт не меняются.
- Изменения кода должны соответствовать `plan.md` и задачам из `tasks.md`.
- При изменении поведения обновляйте `docs/` и ADR, чтобы артефакты не расходились.
- Объём фазы фиксирует спецификация. Дорожная карта задаёт направление и не снижает планку конституции.

## Skills как инструменты

Skills лежат в `.agents/skills/<name>/SKILL.md` и доступны агенту через skill tool. Не загружайте все skills сразу: определите, что для задачи нужен соответствующий skill, и прочитайте его перед работой.

| Skill | Когда использовать |
| --- | --- |
| `csharp-docs` | Добавление/правка XML-документации публичного C# API. |
| `dotnet-best-practices` | Изменение .NET/C# кода, ревью структуры решения. |
| `frontend-design` | Создание или переработка Vue UI: палитра, типографика, layout. |
| `find-skills` | Нужной возможности нет в проекте — поиск и установка внешнего skill. |

Если для запуска тестов доступен skill `run-tests` — используйте его для подбора точных команд `dotnet test`. Если skill недоступен, запускайте `dotnet test` напрямую.

## MCP

- `context7` — внешняя документация по C#/.NET, Vue и используемым библиотекам. Используйте для проверки актуального синтаксиса и API.
- `mcp-docs-ts` — локальный RAG по `./docs` (`docs.list`, `docs.read`, `docs.search`), если подключён. Если MCP недоступен, читайте файлы `./docs` напрямую; блокировать задачу из-за этого не нужно.

MCP — вспомогательный источник. Локальные файлы репозитория и официальная документация приоритетнее предположений.

## Стек и Команды

- Backend: C#/.NET, solution `kanban-cli/Kanban.sln` (проекты `src/Kanban.Api`, `src/Kanban.Cli`, `src/Kanban.Domain`, `src/Kanban.Infrastructure`, `src/UserProfile.Api`, `tests/Kanban.Tests`).
- Целевая архитектура: веб-API .NET, PostgreSQL, JWT, одна доска на пользователя. JSON-файл доски и SQLite профиля — переходное состояние, не норма.
- Frontend живёт в репозитории kanbanVibe-front (Vue SPA). Не создавайте каталог `frontend/` в этом репозитории вне отдельной спецификации.
- Сборка: `dotnet build kanban-cli/Kanban.sln`.
- Тесты: `dotnet test kanban-cli/Kanban.sln` (или точная команда из skill `run-tests`).
- CI: `.github/workflows/ci.yml` — сборка и тесты solution на каждый push и pull request.
- CD: `.github/workflows/cd.yml` — публикация Windows x86 (`win-x86`, framework-dependent) при push в `main` и при теге `v*.*.*`.
- Запуск API: `dotnet run --project kanban-cli/src/Kanban.Api`.
- Запуск CLI: `dotnet run --project kanban-cli/src/Kanban.Cli -- <команда>`.

## Навигация

- Конституция проекта: `.specify/memory/constitution.md`
- Спецификации фич: `specs/NNN-feature-name/` (`spec.md`, `plan.md`, `tasks.md`)
- Продуктовые требования: `docs/product-requirements.md`
- Пользовательские сценарии: `docs/user-flows.md`
- Термины: `docs/terms.md`
- ADR: `docs/adr/` (`adr-0001-domain-classes.md`, `adr-0002-cli-ui.md`)

## Минимальный Контекст

- Основные сущности: Board, Column, Card. Инварианты — в конституции, принцип IV.
- Целевая модель доступа: учётная запись, JWT, своя доска пользователя. Анонимная общая доска не является целевой нормой.
- CLI и API при ошибке возвращают понятный результат; CLI — ненулевой код выхода.
- Vue SPA работает с тем же доменом и контрактом, что и backend.
