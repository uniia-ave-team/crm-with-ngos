#!/usr/bin/env bash
#
# Встановлення або запуск CRM на комп'ютері з macOS і Docker Desktop.
# Відкривається подвійним кліком у Finder.
#
set -euo pipefail
cd "$(dirname "$0")/.."

pause() { echo; read -rp "Натисніть Enter, щоб закрити вікно"; }
fail() { echo; printf '\033[0;31mПомилка:\033[0m %s\n' "$1"; pause; exit 1; }

command -v docker >/dev/null 2>&1 \
  || fail "Docker Desktop не встановлено. Завантажте його з https://www.docker.com/products/docker-desktop/ і повторіть."
docker info >/dev/null 2>&1 \
  || fail "Docker Desktop не запущений. Відкрийте його, дочекайтеся стану 'Engine running' і повторіть."

if [ -f .env ]; then
  echo "==> Конфігурація вже існує. Запуск наявної інсталяції"
  docker compose -f docker-compose.yml -f docker-compose.local.yml up -d --wait
else
  DOMAIN=localhost ./install.sh --local
fi

docker compose -f docker-compose.yml -f docker-compose.local.yml \
  cp caddy:/data/caddy/pki/authorities/local/root.crt ./crm-root-ca.crt
echo "==> Додавання сертифіката до довірених. Підтвердьте паролем у вікні macOS"
security add-trusted-cert -r trustRoot -k "$HOME/Library/Keychains/login.keychain-db" ./crm-root-ca.crt \
  || echo "Сертифікат не додано автоматично. Інструкція — docs/deployment/own-hardware.md"

cat <<EOF

  CRM запущена. Адреса: https://localhost

  Файл deploy/.env містить секрети. Збережіть його копію в менеджері паролів.

  CRM працює, доки увімкнений цей комп'ютер і запущений Docker Desktop.
  Після перезавантаження достатньо знову відкрити install.command.

EOF

open https://localhost
pause
