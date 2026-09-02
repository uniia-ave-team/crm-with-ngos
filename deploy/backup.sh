#!/usr/bin/env bash
#
# Створює резервну копію бази та вивантажує її в каталог backups/ поруч зі
# скриптом — для копіювання на зовнішній носій або в хмарне сховище.
#
#   ./backup.sh
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="$SCRIPT_DIR/.env"
OUT_DIR="$SCRIPT_DIR/backups"
cd "$SCRIPT_DIR"

info() { printf '\033[0;34m==>\033[0m %s\n' "$1"; }
fail() { printf '\033[0;31mПомилка:\033[0m %s\n' "$1" >&2; exit 1; }

[ -f "$ENV_FILE" ] || fail "Файл .env не знайдено. Спершу виконайте ./install.sh"

# shellcheck disable=SC1090
set -a; . "$ENV_FILE"; set +a

compose() {
  local files=(-f "$SCRIPT_DIR/docker-compose.yml")
  [ "${CRM_MODE:-public}" = "local" ] && files+=(-f "$SCRIPT_DIR/docker-compose.local.yml")
  [ -n "${CRM_COMPOSE_EXTRA:-}" ] && files+=(-f "$SCRIPT_DIR/$CRM_COMPOSE_EXTRA")
  docker compose --env-file "$ENV_FILE" "${files[@]}" "$@"
}

compose ps --status running --services 2>/dev/null | grep -qx db \
  || fail "Контейнер бази не запущений. Запустіть інсталяцію: docker compose up -d"

STAMP="$(date +%Y%m%d-%H%M%S)"
FILE="crm-${CRM_VERSION:-latest}-${STAMP}.sql.gz"

info "Створення копії"
compose exec -T db sh -c "
  set -e
  mkdir -p /backups/manual
  pg_dump -U '${POSTGRES_USER}' -d '${POSTGRES_DB}' --clean --if-exists | gzip -1 > '/backups/manual/${FILE}'
"

mkdir -p "$OUT_DIR"
compose cp "db:/backups/manual/${FILE}" "$OUT_DIR/${FILE}"

cat <<EOF

  Копію збережено: ${OUT_DIR}/${FILE}

  Скопіюйте файл на зовнішній носій або в хмарне сховище. Разом із файлом
  зберігайте значення POSTGRES_PASSWORD і AUTH_SIGNING_KEY з .env — без них
  копію неможливо відновити.

EOF
