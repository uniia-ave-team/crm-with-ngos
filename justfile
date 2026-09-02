# Єдина точка входу для команд бекенду та фронтенду.
#
# Встановлення: winget install Casey.Just | brew install just | cargo install just

set windows-shell := ["powershell.exe", "-NoLogo", "-Command"]
set dotenv-load := false

compose     := "docker compose --env-file deploy/.env -f deploy/docker-compose.yml"
compose_dev := compose + " -f deploy/docker-compose.dev.yml"

# Показати доступні команди.
default:
    @just --list

# Створити deploy/.env із шаблону. Наявний файл не перезаписується.
init:
    @just _copy-env
    @echo "deploy/.env створено. Заповніть значення перед 'just up'."

[unix]
_copy-env:
    @test -f deploy/.env || cp deploy/.env.example deploy/.env

[windows]
_copy-env:
    @if (-not (Test-Path deploy/.env)) { Copy-Item deploy/.env.example deploy/.env }

# --- Запуск стека ------------------------------------------------------------

# Весь продукт у Docker у тій самій конфігурації, що й в адміністратора.
up:
    {{compose}} up -d --wait

down:
    {{compose}} down

# Зупинити й видалити том бази даних. Знищує всі локальні дані.
reset:
    {{compose}} down -v

logs service="":
    {{compose}} logs -f --tail=200 {{service}}

ps:
    {{compose}} ps

# --- Розробка ----------------------------------------------------------------

# Бекенд: база та опублікований образ клієнта в Docker, API на хості.
dev-api:
    {{compose_dev}} up -d --wait db web
    @echo "Запустіть API з apps/api (dotnet watch). PostgreSQL: localhost:5432, клієнт: http://localhost:4200"

# Фронтенд: база та опублікований образ API в Docker, Angular на хості.
dev-web:
    {{compose_dev}} up -d --wait db api
    @echo "Виконайте 'npm start' в apps/web. API: http://localhost:5080 (проксі /api налаштовано в proxy.conf.json)"

# Оновити образи :edge іншої частини.
refresh:
    {{compose_dev}} pull

# psql до бази розробки.
psql:
    {{compose}} exec db psql -U crm -d crm

# --- Контракт ----------------------------------------------------------------

# Перегенерувати packages/api-contract/openapi.json з API та Angular-клієнт
# зі специфікації. Виконувати після зміни будь-якого ендпоінта.
contract: contract-spec contract-client

contract-spec:
    @echo "TODO: вивантажити OpenAPI з apps/api у packages/api-contract/openapi.json"

contract-client:
    @echo "TODO: згенерувати apps/web/src/app/api/generated зі специфікації"

# --- Збірка ------------------------------------------------------------------

# Зібрати обидва образи локально з тегами, ідентичними CI.
build version="dev":
    docker build -t ghcr.io/uniia-ave-team/crm-api:{{version}} apps/api
    docker build -t ghcr.io/uniia-ave-team/crm-web:{{version}} apps/web

# --- Якість ------------------------------------------------------------------

fmt:
    @echo "TODO: dotnet format apps/api && npm --prefix apps/web run format"

lint:
    @echo "TODO: dotnet build -warnaserror && npm --prefix apps/web run lint"
