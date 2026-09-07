#!/usr/bin/env bash
set -e

TEMP_OVERRIDE="docker-compose.import-temp-ports.yaml"

cleanup() {
  echo "Cleaning up temporary override file..."
  rm -f "$TEMP_OVERRIDE"

  ./down.sh
}
trap cleanup EXIT

./down.sh

if [ -f .env ]; then
  export $(grep -v '^#' .env | xargs)
fi

POSTGRES_HOST="${POSTGRES_HOST:-localhost}"
POSTGRES_PORT="${POSTGRES_PORT:-5432}"
POSTGRES_USER="${POSTGRES_USER:-postgres}"
POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-postgres}"
OBJECT_STORE_PORT="${OBJECT_STORE_PORT:-8080}"
OBJECT_STORE_ENDPOINT="http://localhost:${OBJECT_STORE_PORT}/"

read -e -p "Enter path to old installation (folder with components, data, launcher, staticdata): " OLD_PATH

if [ ! -d "$OLD_PATH" ]; then
  echo "Error: Path '$OLD_PATH' does not exist."
  exit 1
fi

cat <<EOF > "$TEMP_OVERRIDE"
services:
  postgres:
    ports:
      - "${POSTGRES_PORT}:${POSTGRES_PORT}"
  object-store:
    ports:
      - "${OBJECT_STORE_PORT}:${OBJECT_STORE_PORT}"
EOF

COMPOSE_FILES=(-f docker-compose.yaml)
if [ -f docker-compose.override.yaml ]; then
  COMPOSE_FILES+=(-f docker-compose.override.yaml)
fi
COMPOSE_FILES+=(-f "$TEMP_OVERRIDE")

echo "Starting postgres and object-store containers..."
docker compose "${COMPOSE_FILES[@]}" up -d postgres object-store

sleep 3

dotnet ./migrator/Solace.Db.Migrator.dll \
  --skip-intro \
  --old-path "$OLD_PATH" \
  --host "$POSTGRES_HOST" \
  --port "$POSTGRES_PORT" \
  --user "$POSTGRES_USER" \
  --password "$POSTGRES_PASSWORD" \
  --endpoint "$OBJECT_STORE_ENDPOINT" \
  -y