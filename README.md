# KanbanVibe Backend

ASP.NET Core API и CLI для Kanban-доски (перенесено из `ITMOv2` → `practices/practice_05`, ветка `integration/practice_05`).

## Требования

- [.NET SDK 10](https://dotnet.microsoft.com/download) (см. `global.json`)

## Локальный запуск

```bash
cd kanban-cli
dotnet restore Kanban.sln
dotnet run --project src/Kanban.Api
```

API по умолчанию: **http://localhost:5050** (профиль `http` в `launchSettings.json`).

### Эндпоинты доски

- `GET /api/board` — получить доску
- `PUT /api/board` — сохранить доску

Дополнительно в solution: `Kanban.Cli`, `UserProfile.Api` (SQLite).

### Тесты

```bash
cd kanban-cli
dotnet test Kanban.sln
```

## Docker

```bash
docker compose up --build
```

Сервис `api` слушает порт **5050**.

## Spec Kit

В репозитории настроен [GitHub Spec Kit](https://github.github.com/spec-kit/) (каталог `.specify/`, интеграция OpenCode). Переинициализация при необходимости:

```bash
uv tool install specify-cli
specify init --here --force --integration opencode --ignore-agent-tools
```

## Структура

| Путь | Назначение |
|------|------------|
| `kanban-cli/` | Solution `Kanban.sln`, API, CLI, домен |
| `docs/` | Продуктовая документация и ADR |
| `specs/` | Спецификации фич Spec Kit |
| `.specify/` | Шаблоны и скрипты Spec Kit |