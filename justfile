# Single entry point for development commands. Run `just` to list them.
#
# Install just: winget install Casey.Just | brew install just

set windows-shell := ["powershell.exe", "-NoLogo", "-NoProfile", "-Command"]

solution := "apps/api/Crm.slnx"
api_project := "apps/api/src/Crm.Api"
infrastructure_project := "apps/api/src/Crm.Infrastructure"
web_dir := "apps/web"
test_results := "apps/api/TestResults"
compose := "docker compose -f compose.yaml"
db_connection := "Host=localhost;Port=5432;Database=crm;Username=crm;Password=crm"

# List available commands.
default:
    @just --list --unsorted

# --- Setup -------------------------------------------------------------------

# Install dependencies and configure local secrets. Safe to re-run.
setup: _check-tools
    dotnet restore {{solution}}
    dotnet tool restore
    npm --prefix {{web_dir}} ci
    @just _secrets

[windows]
_check-tools:
    @foreach ($tool in 'dotnet', 'node', 'npm', 'docker') { if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) { throw "Required tool '$tool' is not installed. See README.md." } }

[unix]
_check-tools:
    #!/usr/bin/env sh
    for tool in dotnet node npm docker; do
        command -v "$tool" >/dev/null 2>&1 || { echo "Required tool '$tool' is not installed. See README.md."; exit 1; }
    done

[windows]
_secrets:
    @$existing = dotnet user-secrets list --project {{api_project}} | Out-String; \
    if ($existing -notmatch 'ConnectionStrings:PostgreSqlConnection') { dotnet user-secrets set 'ConnectionStrings:PostgreSqlConnection' '{{db_connection}}' --project {{api_project}} | Out-Null; Write-Host 'user-secrets: set ConnectionStrings:PostgreSqlConnection' }; \
    if ($existing -notmatch 'JwtOptions:Secret') { $bytes = New-Object byte[] 48; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes); dotnet user-secrets set 'JwtOptions:Secret' ([Convert]::ToBase64String($bytes)) --project {{api_project}} | Out-Null; Write-Host 'user-secrets: set JwtOptions:Secret' }

[unix]
_secrets:
    #!/usr/bin/env sh
    set -eu
    existing="$(dotnet user-secrets list --project {{api_project}})"
    case "$existing" in
        *ConnectionStrings:PostgreSqlConnection*) ;;
        *) dotnet user-secrets set 'ConnectionStrings:PostgreSqlConnection' '{{db_connection}}' --project {{api_project}} >/dev/null
           echo 'user-secrets: set ConnectionStrings:PostgreSqlConnection' ;;
    esac
    case "$existing" in
        *JwtOptions:Secret*) ;;
        *) dotnet user-secrets set 'JwtOptions:Secret' "$(head -c 48 /dev/urandom | base64 | tr -d '\n')" --project {{api_project}} >/dev/null
           echo 'user-secrets: set JwtOptions:Secret' ;;
    esac

# --- Database ----------------------------------------------------------------

# Start PostgreSQL in Docker (localhost:5432).
db:
    {{compose}} up -d --wait db

# Stop PostgreSQL. Data is kept.
db-down:
    {{compose}} down

# Stop PostgreSQL and delete all local data.
db-reset:
    {{compose}} down -v

# Open psql in the development database.
psql:
    {{compose}} exec db psql -U crm -d crm

# Add an EF Core migration, e.g. `just migration-add AddContacts`.
migration-add name:
    dotnet ef migrations add {{name}} --project {{infrastructure_project}} --startup-project {{api_project}}

# Remove the last EF Core migration if it has not been applied.
migration-remove:
    dotnet ef migrations remove --project {{infrastructure_project}} --startup-project {{api_project}}

# --- Run ---------------------------------------------------------------------

# Run the API with hot reload on http://localhost:5065.
api:
    dotnet watch run --project {{api_project}} --launch-profile http

# Run the web client on http://localhost:4200; /api is proxied to the API.
web:
    npm --prefix {{web_dir}} start

# --- API contract ------------------------------------------------------------

# Regenerate packages/api-contract/openapi.json and the Angular client. Run after changing any endpoint.
contract: contract-spec contract-client

# Write packages/api-contract/openapi.json from the API code.
contract-spec:
    dotnet build {{api_project}} -p:GenerateOpenApi=true

# Generate the Angular client in apps/web/src/app/api/generated from openapi.json.
contract-client:
    npm --prefix {{web_dir}} run generate:api

# Fail if the committed openapi.json does not match the API code.
contract-check: contract-spec
    git diff --exit-code -- packages/api-contract/openapi.json

# --- Quality -----------------------------------------------------------------

# Format API code and auto-fix web lint issues.
fmt:
    dotnet format {{solution}}
    npm --prefix {{web_dir}} run lint:fix

# Check formatting and lint without changing files.
lint:
    dotnet format {{solution}} --verify-no-changes
    npm --prefix {{web_dir}} run lint

# Run all tests.
test: test-api test-web

test-api:
    dotnet test {{solution}}

test-web:
    npm --prefix {{web_dir}} run test:ci

# Run API tests with coverage and build an HTML report.
test-coverage: _clean-test-results
    dotnet test {{solution}} --results-directory {{test_results}} --collect:"XPlat Code Coverage" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.ExcludeByFile="**/*.generated.cs,**/*.g.cs"
    dotnet reportgenerator -reports:"{{test_results}}/**/coverage.cobertura.xml" -targetdir:"{{test_results}}/Report" -reporttypes:Html -filefilters:"-*.generated.cs;-*.g.cs" -classfilters:"-*Microsoft.AspNetCore.OpenApi*" -verbosity:Error
    @echo "Coverage report: {{test_results}}/Report/index.html"

[windows]
_clean-test-results:
    @if (Test-Path {{test_results}}) { Remove-Item -Recurse -Force {{test_results}} }

[unix]
_clean-test-results:
    @rm -rf {{test_results}}

# --- CI ----------------------------------------------------------------------

# Run every check CI runs.
ci: api-ci contract-check web-ci

api-ci:
    dotnet restore {{solution}}
    dotnet format {{solution}} --verify-no-changes --no-restore
    dotnet build {{solution}} --no-restore --configuration Release
    dotnet test {{solution}} --no-build --configuration Release

web-ci:
    npm --prefix {{web_dir}} ci
    npm --prefix {{web_dir}} run lint
    npm --prefix {{web_dir}} run test:ci
    npm --prefix {{web_dir}} run build
