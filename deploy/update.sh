#!/usr/bin/env bash
#
# Оновлення встановленої інсталяції. Перед оновленням створює резервну копію
# бази: міграції схеми застосовуються автоматично й є незворотними.
#
#   ./update.sh            найновіший реліз
#   ./update.sh 1.4.2      конкретний реліз
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="$SCRIPT_DIR/.env"
KEEP_BACKUPS=5
cd "$SCRIPT_DIR"

info() { printf '\033[0;34m==>\033[0m %s\n' "$1"; }
warn() { printf '\033[0;33mУвага:\033[0m %s\n' "$1" >&2; }
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

CURRENT_VERSION="${CRM_VERSION:-latest}"
TARGET_VERSION="${1:-latest}"

info "Оновлення: ${CURRENT_VERSION} -> ${TARGET_VERSION}"

# --- Резервна копія ----------------------------------------------------------
# Зберігається в томі backups (каталог pre-update/) поряд з автоматичними
# копіями, які створює сервіс db-backup.

STAMP="$(date +%Y%m%d-%H%M%S)"
BACKUP_FILE="crm-${CURRENT_VERSION}-${STAMP}.sql.gz"

if compose ps --status running --services 2>/dev/null | grep -qx db; then
  info "Резервна копія бази: pre-update/${BACKUP_FILE}"
  compose exec -T db sh -c "
    set -e
    mkdir -p /backups/pre-update
    pg_dump -U '${POSTGRES_USER}' -d '${POSTGRES_DB}' --clean --if-exists | gzip -1 > '/backups/pre-update/${BACKUP_FILE}'
  " || fail "Не вдалося створити резервну копію. Оновлення скасовано, нічого не змінено."
else
  warn "Контейнер бази не запущений; резервну копію пропущено."
fi

# --- Оновлення ---------------------------------------------------------------

if grep -q '^CRM_VERSION=' "$ENV_FILE"; then
  sed -i.bak "s|^CRM_VERSION=.*|CRM_VERSION=${TARGET_VERSION}|" "$ENV_FILE"
  rm -f "$ENV_FILE.bak"
else
  printf 'CRM_VERSION=%s\n' "$TARGET_VERSION" >>"$ENV_FILE"
fi

info "Завантаження образів"
compose pull

info "Перезапуск; міграції схеми застосовуються автоматично"
if ! compose up -d --wait; then
  cat <<EOF >&2

  Сервіси не перейшли у справний стан.

  Повернення до версії ${CURRENT_VERSION}:

    sed -i 's|^CRM_VERSION=.*|CRM_VERSION=${CURRENT_VERSION}|' .env
    docker compose up -d --wait

  Якщо схему вже було мігровано, додатково відновіть резервну копію:

    docker compose exec -T db sh -c \\
      'gunzip -c /backups/pre-update/${BACKUP_FILE} | psql -U ${POSTGRES_USER} -d ${POSTGRES_DB}'

EOF
  exit 1
fi

# --- Прибирання --------------------------------------------------------------

compose exec -T db sh -c "
  ls -1t /backups/pre-update/*.sql.gz 2>/dev/null | tail -n +$((KEEP_BACKUPS + 1)) | xargs -r rm -f
" || true
docker image prune -f >/dev/null 2>&1 || true

info "Встановлено версію ${TARGET_VERSION}. Адреса: https://${DOMAIN}"
