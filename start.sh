#!/usr/bin/env bash
# Boxora — one command to bring the whole thing up.
#
#   ./start.sh                 database + migrations + build + seed (if empty) + run
#   ./start.sh --smoke         run the offline check suite and exit
#   ./start.sh --seed          re-seed demo claims and exit
#   ./start.sh --embed         embed any new/changed legal passages and exit
#   ./start.sh --migrate-only  apply Flyway migrations and exit
#   ./start.sh --reset         drop and recreate the database, then carry on
#   ./start.sh --no-seed       skip seeding even when the database is empty
set -euo pipefail

cd "$(dirname "$0")"
PROJ=src/JbAutoAi

# --- config ---------------------------------------------------------------------

[[ -f .env ]] || { cp .env.example .env; echo "created .env from .env.example"; }
set -a; . ./.env; set +a

: "${PG_CONTAINER:=counted-db-1}"
: "${PG_SUPERUSER:=postgres}"
: "${PG_HOST:=localhost}"
: "${PG_PORT:=5432}"
: "${PG_DB:=jb_auto_ai}"
: "${PG_USER:=jbauto}"
: "${PG_PASSWORD:=jbauto_dev_pw}"
: "${ASPNETCORE_URLS:=http://localhost:8080}"

export PG_CONNSTRING="${PG_CONNSTRING:-Host=$PG_HOST;Port=$PG_PORT;Database=$PG_DB;Username=$PG_USER;Password=$PG_PASSWORD}"
export FLYWAY_URL="jdbc:postgresql://$PG_HOST:$PG_PORT/$PG_DB"
export FLYWAY_USER="$PG_USER"
export FLYWAY_PASSWORD="$PG_PASSWORD"
export ASPNETCORE_URLS

RESET=0 NO_SEED=0 MODE=run
for arg in "$@"; do
  case "$arg" in
    --reset) RESET=1 ;;
    --no-seed) NO_SEED=1 ;;
    --smoke) MODE=smoke ;;
    --seed) MODE=seed ;;
    --embed) MODE=embed ;;
    --migrate-only) MODE=migrate ;;
    *) echo "unknown option: $arg" >&2; exit 2 ;;
  esac
done

say() { printf '\033[38;5;173m▸\033[0m %s\n' "$1"; }
die() { printf '\033[31m✗\033[0m %s\n' "$1" >&2; exit 1; }

psu() { docker exec -i "$PG_CONTAINER" psql -v ON_ERROR_STOP=1 -U "$PG_SUPERUSER" "$@"; }

# --- database --------------------------------------------------------------------

docker info >/dev/null 2>&1 || die "Docker is not running. Start Docker Desktop and retry."
docker ps --format '{{.Names}}' | grep -qx "$PG_CONTAINER" \
  || die "Container '$PG_CONTAINER' is not running. Start it, or set PG_CONTAINER in .env."

if [[ $RESET == 1 ]]; then
  say "dropping database $PG_DB"
  psu -d postgres -c "DROP DATABASE IF EXISTS \"$PG_DB\" WITH (FORCE);" >/dev/null
fi

if ! psu -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='$PG_DB'" | grep -q 1; then
  say "creating database $PG_DB"
  psu -d postgres -c "CREATE DATABASE \"$PG_DB\";" >/dev/null
fi

say "ensuring role $PG_USER and pgvector"
psu -d postgres -c "DO \$\$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='$PG_USER') THEN
    CREATE ROLE $PG_USER LOGIN PASSWORD '$PG_PASSWORD';
  END IF;
END \$\$;" >/dev/null
psu -d postgres -c "ALTER ROLE $PG_USER PASSWORD '$PG_PASSWORD';" >/dev/null
psu -d postgres -c "ALTER DATABASE \"$PG_DB\" OWNER TO $PG_USER;" >/dev/null
psu -d "$PG_DB" -c "CREATE EXTENSION IF NOT EXISTS vector;" >/dev/null
psu -d "$PG_DB" -c "ALTER SCHEMA public OWNER TO $PG_USER; GRANT ALL ON SCHEMA public TO $PG_USER;" >/dev/null

# --- migrations --------------------------------------------------------------------

if command -v flyway >/dev/null 2>&1; then
  say "flyway migrate"
  flyway -configFiles=flyway.conf migrate | grep -Ev '^$|release notes|^See ' || true
else
  say "flyway not installed — applying db/migration/*.sql with psql"
  for f in $(ls db/migration/V*.sql | sort) $(ls db/migration/R*.sql | sort); do
    echo "    $f"
    psu -d "$PG_DB" -f - < "$f" >/dev/null
  done
fi

[[ $MODE == migrate ]] && { say "migrations applied"; exit 0; }

# --- build -----------------------------------------------------------------------------

say "building"
dotnet build "$PROJ" -v q --nologo >/dev/null

case "$MODE" in
  smoke) exec dotnet run --project "$PROJ" --no-build -- --smoke ;;
  seed)  exec dotnet run --project "$PROJ" --no-build -- --seed ;;
  embed) exec dotnet run --project "$PROJ" --no-build -- --embed ;;
esac

# --- corpus embeddings + demo data ---------------------------------------------------------

if [[ "${AZURE_OPENAI_KEY:-REPLACE_ME}" != "REPLACE_ME" && -n "${AZURE_OPENAI_KEY:-}" ]]; then
  say "embedding new legal passages"
  dotnet run --project "$PROJ" --no-build -- --embed
else
  say "no AZURE_OPENAI_KEY — model calls stub out, legal retrieval runs lexical-only"
fi

CLAIMS=$(psu -d "$PG_DB" -tAc "SELECT count(*) FROM claims")
if [[ "$CLAIMS" == "0" && $NO_SEED == 0 ]]; then
  say "seeding demo claims"
  dotnet run --project "$PROJ" --no-build -- --seed
fi

say "starting on $ASPNETCORE_URLS"
exec dotnet run --project "$PROJ" --no-build
