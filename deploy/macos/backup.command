#!/usr/bin/env bash
#
# Резервна копія бази у каталог deploy/backups.
# Відкривається подвійним кліком у Finder.
#
set -euo pipefail
cd "$(dirname "$0")/.."

pause() { echo; read -rp "Натисніть Enter, щоб закрити вікно"; }

if ./backup.sh; then
  open ./backups
  pause
else
  pause
  exit 1
fi
