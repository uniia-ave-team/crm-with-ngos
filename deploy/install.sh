#!/usr/bin/env bash
#
# Перше встановлення: створює .env, генерує секрети, запускає стек і чекає на
# готовність усіх сервісів.
#
#   ./install.sh                  сервер із публічним доменом
#   ./install.sh --local          власне обладнання без публічного домену
#
# Без запитань:
#   DOMAIN=crm.example.org ACME_EMAIL=admin@example.org ./install.sh
#   DOMAIN=192.168.1.20 ./install.sh --local
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="$SCRIPT_DIR/.env"
cd "$SCRIPT_DIR"

CRM_MODE="public"
for arg in "$@"; do
  case "$arg" in
    --local) CRM_MODE="local" ;;
    -h|--help)
      sed -n '2,12p' "$0" | sed 's/^# \{0,1\}//'
      exit 0
      ;;
    *) printf 'Невідомий параметр: %s\n' "$arg" >&2; exit 2 ;;
  esac
done

info() { printf '\033[0;34m==>\033[0m %s\n' "$1"; }
fail() { printf '\033[0;31mПомилка:\033[0m %s\n' "$1" >&2; exit 1; }

compose() {
  local files=(-f "$SCRIPT_DIR/docker-compose.yml")
  [ "$CRM_MODE" = "local" ] && files+=(-f "$SCRIPT_DIR/docker-compose.local.yml")
  [ -n "${CRM_COMPOSE_EXTRA:-}" ] && files+=(-f "$SCRIPT_DIR/$CRM_COMPOSE_EXTRA")
  docker compose --env-file "$ENV_FILE" "${files[@]}" "$@"
}

random_secret() {
  if command -v openssl >/dev/null 2>&1; then
    openssl rand -hex 32
  else
    head -c 32 /dev/urandom | od -An -tx1 | tr -d ' \n'
  fi
}

set_env() {
  local key="$1" value="$2"
  if grep -q "^${key}=" "$ENV_FILE"; then
    sed -i.bak "s|^${key}=.*|${key}=${value}|" "$ENV_FILE"
    rm -f "$ENV_FILE.bak"
  else
    printf '%s=%s\n' "$key" "$value" >>"$ENV_FILE"
  fi
}

# --- Перевірки ---------------------------------------------------------------

command -v docker >/dev/null 2>&1 || fail "Docker не встановлено. Інструкція: https://docs.docker.com/engine/install/"
docker compose version >/dev/null 2>&1 || fail "Відсутній плагін Docker Compose. Встановіть пакет docker-compose-plugin."
docker info >/dev/null 2>&1 || fail "Немає доступу до Docker. Перевірте, що службу запущено та що користувач входить до групи docker."

[ -f "$ENV_FILE" ] && fail "Файл .env уже існує: інсталяцію вже налаштовано. Для оновлення використовуйте ./update.sh"

# --- Параметри ---------------------------------------------------------------

DOMAIN="${DOMAIN:-}"
ACME_EMAIL="${ACME_EMAIL:-}"

if [ -z "$DOMAIN" ]; then
  [ -t 0 ] || fail "Змінна DOMAIN не задана, інтерактивний ввід недоступний."
  if [ "$CRM_MODE" = "local" ]; then
    read -rp "Ім'я або IP-адреса, за якою відкриватиметься CRM [localhost]: " DOMAIN
    DOMAIN="${DOMAIN:-localhost}"
  else
    read -rp "Доменне ім'я, яке вказує на цей сервер (наприклад, crm.example.org): " DOMAIN
  fi
fi

if [ "$CRM_MODE" = "public" ] && [ -z "$ACME_EMAIL" ]; then
  [ -t 0 ] || fail "Змінна ACME_EMAIL не задана, інтерактивний ввід недоступний."
  read -rp "Електронна адреса для повідомлень про сертифікат: " ACME_EMAIL
fi
[ "$CRM_MODE" = "local" ] && ACME_EMAIL="${ACME_EMAIL:-local@localhost}"

[ -n "$DOMAIN" ] || fail "Доменне ім'я не може бути порожнім."
[ -n "$ACME_EMAIL" ] || fail "Електронна адреса не може бути порожньою."

info "Створення .env"
cp "$SCRIPT_DIR/.env.example" "$ENV_FILE"
chmod 600 "$ENV_FILE"

set_env CRM_MODE "$CRM_MODE"
set_env DOMAIN "$DOMAIN"
set_env ACME_EMAIL "$ACME_EMAIL"
set_env POSTGRES_PASSWORD "$(random_secret)"
set_env AUTH_SIGNING_KEY "$(random_secret)"

# --- Запуск ------------------------------------------------------------------

info "Завантаження образів"
compose pull

info "Запуск сервісів; схема бази даних створюється автоматично"
compose up -d --wait

cat <<EOF

  Встановлення завершено. Адреса: https://${DOMAIN}

EOF

if [ "$CRM_MODE" = "public" ]; then
  cat <<EOF
  Сертифікат видається під час першого запиту; це триває кілька секунд. Якщо
  сторінка не відкривається, переконайтеся, що ${DOMAIN} вказує на цей сервер
  і що порти 80 та 443 доступні з інтернету.

EOF
else
  cat <<EOF
  Сертифікат виданий локальним центром сертифікації. Щоб браузери довіряли
  йому, встановіть кореневий сертифікат на кожному робочому місці:

    docker compose cp caddy:/data/caddy/pki/authorities/local/root.crt ./crm-root-ca.crt

  Інструкція для Windows, macOS та Linux — docs/deployment/own-hardware.md.

EOF
fi

cat <<EOF
  Файл $ENV_FILE містить секрети. Збережіть його копію поза сервером:
  без AUTH_SIGNING_KEY і POSTGRES_PASSWORD резервну копію бази неможливо
  використати.

  Резервні копії бази створюються автоматично щодня в томі crm_backups.
  Як їх вивантажити та зберігати поза сервером — docs/deployment/backups.md.

  Журнали:   cd $SCRIPT_DIR && docker compose logs -f
  Оновлення: $SCRIPT_DIR/update.sh

EOF
