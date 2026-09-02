#!/usr/bin/env bash
#
# Оновлення CRM на комп'ютері з macOS до найновішої версії.
# Відкривається подвійним кліком у Finder.
#
set -euo pipefail
cd "$(dirname "$0")/.."

pause() { echo; read -rp "Натисніть Enter, щоб закрити вікно"; }

if ./update.sh latest; then
  pause
else
  pause
  exit 1
fi
