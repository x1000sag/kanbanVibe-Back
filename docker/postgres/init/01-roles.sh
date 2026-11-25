#!/bin/bash
set -euo pipefail

: "${POSTGRES_PASSWORD:?POSTGRES_PASSWORD is required}"
: "${APP_DB_PASSWORD:?APP_DB_PASSWORD is required}"
: "${OPERATOR_DB_PASSWORD:?OPERATOR_DB_PASSWORD is required}"

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" <<-EOSQL
    CREATE DATABASE kanban_account;
EOSQL

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname kanban_account <<-EOSQL
    CREATE ROLE kanban_migrator LOGIN PASSWORD '${POSTGRES_PASSWORD}';
    CREATE ROLE kanban_app LOGIN PASSWORD '${APP_DB_PASSWORD}';
    CREATE ROLE kanban_operator LOGIN PASSWORD '${OPERATOR_DB_PASSWORD}';
    GRANT CONNECT ON DATABASE kanban_account TO kanban_migrator, kanban_app, kanban_operator;
    GRANT CREATE ON DATABASE kanban_account TO kanban_migrator;
    GRANT ALL ON SCHEMA public TO kanban_migrator;
    ALTER SCHEMA public OWNER TO kanban_migrator;
EOSQL