# CRM API — Backend Service

> Modular multi-tenant REST API for NGO CRM management. Handles authentication, member directories, role-based access control, custom user fields, dynamic custom tables and records, table templates, and file storage — built on ASP.NET Core with a clean CQRS / Clean Architecture design.

---

## Tech Stack

| Component | Technology | Version |
|---|---|---|
| Runtime & Language | .NET / C# | 10.0 / 14 |
| Web Framework | ASP.NET Core Web API | 10.0 |
| ORM | Entity Framework Core | 10.0 |
| Database | PostgreSQL (via Npgsql) | 17+ |
| CQRS / Mediator | MediatR | 14.x |
| Validation | FluentValidation | 12.x |
| Object Mapping | Mapster | 10.x |
| Auth | ASP.NET Core Identity + JWT Bearer | 10.0 |
| API Docs | Scalar + OpenAPI 3.1 | 2.x |
| Logging | Serilog (console + rolling files) | 4.x / 10.x |
| Style Analysis | StyleCop.Analyzers | 1.2.x |
| Test Framework | xUnit + NSubstitute + Shouldly | 2.x / 6.x / 4.x |
| Integration Tests | Testcontainers.PostgreSql + Respawn | 4.x / 7.x |

---

## Repository Structure

```
apps/api/
├── Crm.slnx                         # .NET solution file (new XML format)
├── Directory.Build.props             # Solution-wide MSBuild properties & analyzer config
├── Dockerfile                        # Multi-stage build: sdk:10.0 → aspnet:10.0-alpine
│
└── src/
    ├── Crm.Domain/                   # Core business models — entities, enums, interfaces, exceptions
    │   ├── Entities/                 # Domain entities (User, Ngo, Role, CustomTable, CustomRecord, TableTemplate, etc.)
    │   ├── Enums/                    # Domain enumerations (AccessRight, FieldDataType, status flags)
    │   ├── Exceptions/               # Base domain exception types
    │   └── Interfaces/               # Repository contracts (IRepository<T>, IFieldSchema)
    │
    ├── Crm.Application/              # Use-case layer — CQRS handlers, DTOs, validators
    │   ├── Features/                 # Vertical slices: Users, Ngos, Roles, System, LoginPageImages
    │   ├── Dtos/                     # Request / Response transfer objects
    │   ├── Interfaces/               # Service abstractions (IIdentityService, ITokenService, etc.)
    │   └── Common/                   # Shared behaviors (validation pipeline, transaction behavior)
    │
    ├── Crm.Infrastructure/           # Infrastructure implementations
    │   ├── Persistence/              # EF Core DbContext (ApplicationDbContext)
    │   ├── Migrations/               # EF Core database migrations
    │   ├── Repositories/             # PostgreSQL repository implementations
    │   ├── Services/                 # Identity, token, file storage service implementations
    │   ├── Options/                  # Strongly-typed config classes (JwtOptions, etc.)
    │   ├── BackgroundServices/       # Hosted background workers
    │   └── RateLimiting/             # Rate-limiting policy configuration
    │
    ├── Crm.Api/                      # Presentation layer — HTTP entry point
    │   ├── Controllers/V1/           # Versioned REST controllers (Auth, Users, Ngos, Roles, etc.)
    │   ├── Middleware/               # Global exception handler (ProblemDetails RFC 9457)
    │   ├── Extensions/               # Service registration & pipeline configuration
    │   ├── Security/                 # Authorization policies and requirements
    │   ├── HealthChecks/             # Custom health check implementations
    │   ├── OpenApi/                  # Scalar / OpenAPI transformers
    │   ├── appsettings.json          # Base configuration (non-secret defaults)
    │   └── Program.cs                # App entry point — DI wiring, migration, seeding
    │
    ├── Crm.Cli/                      # Standalone admin CLI (disaster recovery tooling)
    │   ├── Commands/                 # Command implementations (e.g., reset-password)
    │   ├── Parsing/                  # CLI argument parser
    │   └── Program.cs                # CLI entry point
    │
    ├── Crm.Domain.Tests/             # Unit tests — domain entity invariants
    ├── Crm.Application.Tests/        # Unit tests — CQRS handlers and validators
    ├── Crm.Infrastructure.Tests/     # Integration tests — services and repositories
    ├── Crm.Architecture.Tests/       # Architecture fitness tests (layer dependency rules)
    └── Crm.Api.Tests/                # End-to-end integration tests (Testcontainers + WebApplicationFactory)
```

---

## Prerequisites

| Requirement | Version | Notes |
|---|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | **10.0** | Required to build and run all projects |
| [PostgreSQL](https://www.postgresql.org/) | **17+** | Or use Docker — see below |
| [Docker](https://www.docker.com/) | Any recent | Optional; required for containerized runs and integration tests |
| [just](https://github.com/casey/just) | Latest | Optional task runner; commands are available via `dotnet` CLI as well |

Verify your installation:

```bash
dotnet --version   # should print 10.x.x
```

---

## Development Setup

### 1. Clone the repository

```bash
git clone <repository-url>
cd crm-with-ngos
```

### 2. Configure user secrets

Sensitive values (`ConnectionStrings`, `JwtOptions:Secret`) are managed via [.NET User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) and are **never** committed to source control.

From the repository root, run the `just setup` recipe to scaffold secrets automatically:

```bash
just setup
```

This generates a cryptographically secure JWT secret and sets the local PostgreSQL connection string for `Crm.Api`.

To configure manually, run from `apps/api/`:

```bash
# API project
dotnet user-secrets set "ConnectionStrings:PostgreSqlConnection" \
    "Host=localhost;Port=5432;Database=crm_db;Username=postgres;Password=<your-password>" \
    --project src/Crm.Api

dotnet user-secrets set "JwtOptions:Secret" "<min-16-char-random-string>" \
    --project src/Crm.Api
```

`Crm.Cli` uses the same `UserSecretsId` as `Crm.Api`, so it reads these secrets too.

### 3. Start the database

```bash
# Via Docker Compose from the repo root:
docker compose up -d db

# Or via just:
just db
```

### 4. Run the API

```bash
# Hot-reload development server (from apps/api/):
dotnet watch run --project src/Crm.Api --launch-profile http

# Or from the repository root via just:
just api
```

| Endpoint | URL |
|---|---|
| API base | `http://localhost:5065` |
| Interactive docs (Scalar) | `http://localhost:5065/scalar` |
| Health check | `http://localhost:5065/health` |

> **Note:** On first startup the application automatically applies pending EF Core migrations and seeds initial data (within a 1-minute timeout).

---

## Commands

All `dotnet` commands are run from `apps/api/`. `just` commands are run from the **repository root**.

| Command | Description | Category |
|---|---|---|
| `dotnet watch run --project src/Crm.Api --launch-profile http` | Start API with hot reload | Dev |
| `just api` | Start API hot-reload server via just | Dev |
| `just db` | Start PostgreSQL container via Docker Compose | Dev |
| `just setup` | Scaffold user secrets (JWT secret + connection string) | Dev / Onboarding |
| `dotnet build Crm.slnx` | Build all projects in the solution | Build |
| `dotnet publish src/Crm.Api/Crm.Api.csproj -c Release -o ./dist/api` | Publish API release build | Build |
| `dotnet publish src/Crm.Cli/Crm.Cli.csproj -c Release -o ./dist/cli` | Publish CLI release build | Build |
| `just cli <args>` | Run the admin CLI against the local database | Admin |
| `just cli-docker <args>` | Run the admin CLI inside the Docker API container | Admin |
| `dotnet build src/Crm.Api -p:GenerateOpenApi=true` | Regenerate `openapi.json` contract file | Build / Contract |
| `just contract-spec` | Regenerate OpenAPI spec via just | Build / Contract |
| `dotnet test Crm.slnx` | Run all tests in the solution | Test |
| `dotnet test src/Crm.Domain.Tests` | Domain unit tests only | Test |
| `dotnet test src/Crm.Application.Tests` | Application unit tests only | Test |
| `dotnet test src/Crm.Infrastructure.Tests` | Infrastructure integration tests only | Test |
| `dotnet test src/Crm.Architecture.Tests` | Architecture fitness tests only | Test |
| `dotnet test src/Crm.Api.Tests` | End-to-end API integration tests only | Test |
| `dotnet format Crm.slnx` | Apply code formatting across the solution | Lint |
| `dotnet format Crm.slnx --verify-no-changes` | Check formatting without modifying files (CI) | Lint |
| `git diff --exit-code -- ../../packages/api-contract/openapi.json` | Assert OpenAPI contract is in sync with code | Lint / Contract |

---

## Configuration Reference

All settings live in `appsettings.json` (committed, non-secret defaults). Secret and environment-specific overrides use [.NET User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) locally, and `appsettings.Production.json` (git-ignored) in production.

### Connection Strings

| Key | Required | Default | Description |
|---|---|---|---|
| `ConnectionStrings:PostgreSqlConnection` | ✅ **Required** | `"your_connection_string_here"` | Full PostgreSQL connection string |

### JWT Options (`JwtOptions`)

| Key | Required | Default | Description |
|---|---|---|---|
| `JwtOptions:Secret` | ✅ **Required** | *(none)* | HMAC signing secret — minimum 16 characters. Set via user-secrets. |
| `JwtOptions:Issuer` | ✅ **Required** | `"CrmApi"` | JWT `iss` claim value |
| `JwtOptions:Audience` | ✅ **Required** | `"CrmClient"` | JWT `aud` claim value |
| `JwtOptions:AccessTokenExpiryMinutes` | Optional | `15` (prod) / `60` (dev) | Access token lifetime in minutes (range: 1–60) |
| `JwtOptions:RefreshTokenExpiryDays` | Optional | `7` | Refresh token lifetime in days (range: 1–365) |
| `JwtOptions:InvitationTokenExpiryHours` | Optional | `48` | Invitation link validity in hours |
| `JwtOptions:ResetPasswordTokenExpiryHours` | Optional | `72` | Password reset link validity in hours |

### CORS Settings (`CorsSettings`)

| Key | Required | Default | Description |
|---|---|---|---|
| `CorsSettings:AllowedOrigins` | Optional | `["http://localhost:4200"]` | Array of allowed CORS origins |

### Rate Limiting (`RateLimiting`)

| Key | Required | Default | Description |
|---|---|---|---|
| `RateLimiting:WindowInSeconds` | Optional | `1` | Sliding window duration in seconds |
| `RateLimiting:PermitLimit` | Optional | `50` | Max requests allowed per window |
| `RateLimiting:QueueLimit` | Optional | `0` | Max queued requests when the permit limit is reached |

### File Storage (`FileStorage`)

| Key | Required | Default | Description |
|---|---|---|---|
| `FileStorage:UploadDirectory` | ✅ **Required** | `"uploads"` (dev) / `/data/uploads` (Docker) | Directory path for uploaded files |
| `FileStorage:BaseUrl` | ✅ **Required** | `"http://localhost:5065"` | Public base URL for constructing file access URLs |

### Logging (`SerilogOptions`)

| Key | Required | Default | Description |
|---|---|---|---|
| `SerilogOptions:LogPath` | ✅ **Required** | `"Logs"` (dev) / `/data/logs` (Docker) | Directory for log files |
| `SerilogOptions:AllLogsFileName` | Optional | `"all-.log"` | Rolling filename pattern for all-level logs |
| `SerilogOptions:ErrorLogsFileName` | Optional | `"errors-.log"` | Rolling filename pattern for error-level logs |
| `SerilogOptions:RetainedFileCountLimit` | Optional | `5` | Max retained all-log files (range: 1–365) |
| `SerilogOptions:RetainedErrorFileCountLimit` | Optional | `31` | Max retained error-log files (range: 1–365) |

### Identity Options (`IdentityOptions`)

| Key | Required | Default | Description |
|---|---|---|---|
| `IdentityOptions:RequiredLength` | Optional | `8` | Minimum password length |
| `IdentityOptions:RequireDigit` | Optional | `true` | Password must contain a digit |
| `IdentityOptions:RequireNonAlphanumeric` | Optional | `true` | Password must contain a special character |
| `IdentityOptions:RequireUppercase` | Optional | `true` | Password must contain an uppercase letter |
| `IdentityOptions:RequireLowercase` | Optional | `true` | Password must contain a lowercase letter |
| `IdentityOptions:RequireUniqueEmail` | Optional | `true` | Email addresses must be unique across all users |

---

## Testing & Code Quality

### Run all tests

```bash
dotnet test Crm.slnx
```

Integration tests in `Crm.Api.Tests` and `Crm.Infrastructure.Tests` spin up a real PostgreSQL instance automatically via **Testcontainers** — no manual database setup required.

### Run a specific test project

```bash
dotnet test src/Crm.Api.Tests             # End-to-end integration tests
dotnet test src/Crm.Application.Tests     # CQRS handler & validator unit tests
dotnet test src/Crm.Domain.Tests          # Domain entity invariant tests
dotnet test src/Crm.Architecture.Tests    # Enforce layer dependency rules
dotnet test src/Crm.Infrastructure.Tests  # Repository & service integration tests
```

### Code formatting

StyleCop rules are enforced solution-wide via `Directory.Build.props` (`TreatWarningsAsErrors = true`).

```bash
# Apply formatting
dotnet format Crm.slnx

# Verify formatting without modifying files (use in CI pipelines)
dotnet format Crm.slnx --verify-no-changes
```

### OpenAPI contract validation

The `openapi.json` contract (located at `packages/api-contract/openapi.json` in the monorepo root) must stay in sync with the API source code. Verify in CI with:

```bash
dotnet build src/Crm.Api -p:GenerateOpenApi=true
git diff --exit-code -- ../../packages/api-contract/openapi.json
```

---

## CLI Utility — `Crm.Cli`

`Crm.Cli` is a standalone administration tool for maintenance and disaster recovery scenarios (e.g., regaining system access without a web interface). It shares the same database connection and user-secrets configuration as `Crm.Api`.

### Available commands

#### `reset-password` — Reset a user's password

Resets the password for the administrator with the specified email address (users without the `Admin` role are refused), rotates the security stamp, and **invalidates all active refresh tokens** for that account.

```bash
dotnet run --project src/Crm.Cli -- reset-password --email <email>
# Shorthand:
dotnet run --project src/Crm.Cli -- reset-password -e <email>
```

**Example interaction:**

```text
$ dotnet run --project src/Crm.Cli -- reset-password -e admin@example.com
Enter new password: ********
Confirm new password: ********
info: Password for user 'admin@example.com' was successfully updated.
```

#### `--help` — List all available commands

```bash
dotnet run --project src/Crm.Cli -- --help
```

### Running the CLI in different environments

**Development (from `apps/api/`):**

```bash
dotnet run --project src/Crm.Cli -- reset-password -e admin@example.com
```

**Development (from the repository root):**

```bash
just cli reset-password -e admin@example.com
```

**Published binary:**

```bash
dotnet publish src/Crm.Cli/Crm.Cli.csproj -c Release -o ./dist/cli
./dist/cli/Crm.Cli reset-password -e admin@example.com
```

**Inside a running Docker container:**

The API image ships the CLI in `/app/cli` with a `crm` wrapper on `PATH`. It uses the container's connection string, so it works against the same database as the running API.

```bash
docker compose exec api crm reset-password -e admin@example.com
# Or via just:
just cli-docker reset-password -e admin@example.com
```

When standard input is redirected, the CLI reads the password and its confirmation from two lines of input instead of prompting. CI uses this to smoke-test the command:

```bash
printf 'New-Passw0rd\nNew-Passw0rd\n' | docker compose exec -T api crm reset-password -e admin@example.com
```

### CLI output and configuration

The CLI logs only to the console; nothing is written to files. By default it prints its own messages (`Information` and above) and only warnings and errors from `Microsoft.*` and `System.*`. Errors go to standard error. Colors are used only in an interactive terminal. Expected failures (unknown email, not an administrator, password rejected by the policy) are printed as one line; unexpected ones include the exception details.

Settings come from `appsettings.json`, then environment variables, then variables prefixed with `CRM_CLI_`. The prefixed ones apply to the CLI only, even inside the API container, which shares its environment with the API:

```bash
# Show EF Core and other framework logs while diagnosing a failure
docker compose exec -e CRM_CLI_Logging__LogLevel__Microsoft=Information api crm reset-password -e admin@example.com
```

| Variable | Default | Purpose |
|---|---|---|
| `CRM_CLI_Logging__LogLevel__Default` | `Information` | Level for the CLI's own messages |
| `CRM_CLI_Logging__LogLevel__Microsoft` | `Warning` | Level for EF Core, Identity, Data Protection |
| `CRM_CLI_Logging__Console__FormatterOptions__ColorBehavior` | `Default` | `Enabled` / `Disabled` to force colors on or off |
| `CRM_CLI_ConnectionStrings__PostgreSqlConnection` | from the container | Point the CLI at a different database |

---

## Docker

The `Dockerfile` is a multi-stage build targeting `aspnet:10.0-alpine`. It **must be built from the repository root** (the build context includes `global.json` and the full monorepo structure):

```bash
docker build -f apps/api/Dockerfile -t crm-api .
```

The image:
- Installs `icu-libs` and `krb5-libs` (required by Npgsql on Alpine)
- Runs as a non-root user (`$APP_UID`)
- Exposes port **8080**
- Mounts `/data/uploads` and `/data/logs` as named volumes
- Includes a health check hitting `GET /health` every 10 seconds

Provide the required configuration at runtime via environment variables:

```bash
docker run \
  -e "ConnectionStrings__PostgreSqlConnection=Host=db;Database=crm;Username=postgres;Password=secret" \
  -e "JwtOptions__Secret=<your-secret>" \
  -p 8080:8080 \
  crm-api
```

> **Note:** .NET uses `__` (double underscore) as the hierarchy separator when overriding nested settings via environment variables.

---

## 🛠 Технологічний стек

| Компонент / Бібліотека | Призначення |
|------------------------|-------------|
| **.NET 10 / C# 14** | Основна платформа та мова розробки |
| **ASP.NET Core Web API** | HTTP REST API з підтримкою версіонування (`/api/v1/...`) |
| **Entity Framework Core 10** | ORM для доступу до даних |
| **PostgreSQL 17 (Npgsql)** | Реляційна база даних |
| **MediatR** | Реалізація шаблону Mediator для CQRS (Commands / Queries) |
| **FluentValidation** | Автоматична валідація запитів у конвеєрі MediatR |
| **Mapster** | Високопродуктивний мапінг об'єктів (DTO ⇄ Domain Entities) |
| **ASP.NET Core Identity + JWT** | Автентифікація, ролі, права доступу та генерація токенів |
| **Scalar + OpenAPI 3.1** | Генерація специфікації API та інтерактивна документація |
| **Serilog** | Структуроване логування подій у консоль та ротовані файли |

---

## 📁 Структура проєктів

Рішення організовано у файлі `Crm.slnx`:

```
apps/api/
├── Crm.slnx                     # Файл рішення .NET
├── Dockerfile                   # Багатоетапна збірка API у контейнер
├── src/
│   ├── Crm.Domain/              # Корневий шар бізнес-моделей
│   │   ├── Entities/            # Доменні сутності (User, Ngo, Role, CustomTable, CustomRecord, TableTemplate тощо)
│   │   ├── Enums/               # Переліки прав (AccessRight), типів полів (FieldDataType), статусів тощо
│   │   ├── Exceptions/          # Базові доменні винятки
│   │   └── Interfaces/          # Інтерфейси репозиторіїв (IRepository, IFieldSchema)
│   │
│   ├── Crm.Application/         # Шар бізнес-логіки та сценаріїв
│   │   ├── Behaviors/           # MediatR pipeline behaviors (валідація, транзакції)
│   │   ├── Dtos/                # Об'єкти передачі даних (Requests, Responses)
│   │   ├── Features/            # Вертикальні зрізи функцій (Commands, Queries, Handlers)
│   │   ├── Interfaces/          # Інтерфейси сервісів (IIdentityService, ITokenService тощо)
│   │   └── Validators/          # Правила валідації FluentValidation
│   │
│   ├── Crm.Infrastructure/      # Шар реалізації інфраструктури
│   │   ├── Data/                # EF Core ApplicationDbContext та міграції
│   │   ├── Entities/            # Сутності Identity (AuthUser, AuthRole)
│   │   ├── Options/             # Класи типізованої конфігурації (JwtOptions, CustomIdentityOptions)
│   │   ├── Repositories/        # Реалізація репозиторіїв для PostgreSQL
│   │   └── Services/            # Реалізація сервісів токенів, файлів, Identity
│   │
│   ├── Crm.Api/                 # Веб-застосунок (Presentation Layer)
│   │   ├── Controllers/V1/      # REST-контролери (Users, Ngo, System, Invitations тощо)
│   │   ├── Middleware/          # Обробка винятків (ProblemDetails) та безпека
│   │   ├── appsettings.json     # Базова конфігурація
│   │   └── Program.cs           # Точка входу веб-сервера та налаштування DI
│   │
│   └── Crm.Cli/                 # Консольна утиліта адміністрування (CLI)
│       ├── Abstractions/        # Інтерфейс команд ICliCommand
│       ├── Commands/            # Реалізація команд
│       ├── Extensions/          # DI та конфігурація для консолі
│       ├── Parsing/             # Парсер аргументів командного рядка
│       └── Program.cs           # Точка входу CLI застосунку
│
└── tests/                       # Проєкти автоматизованого тестування
    ├── Crm.Domain.Tests/        # Модульні тести доменних сутностей
    ├── Crm.Application.Tests/   # Тести обробників і валідаторів
    ├── Crm.Infrastructure.Tests/# Тести сервісів та бази даних
    ├── Crm.Architecture.Tests/  # Архітектурні тести
    └── Crm.Api.Tests/           # Інтеграційні тести ендпоінтів
```

---

## 🚀 Швидкий старт та розробка

### 1. Вимоги
* [.NET 10 SDK](https://dotnet.microsoft.com/download)
* [PostgreSQL 17](https://www.postgresql.org/) (або запущений контейнер через `just db` чи `docker compose up -d db`)

### 2. Первинне налаштування секретів
У корені репозиторію виконайте:
```bash
just setup
```
Команда налаштовує локальні [user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) для `Crm.Api`: генерує безпечний `JwtOptions:Secret` та задає підключення до локальної бази даних.

### 3. Запуск API
```bash
# З кореня репозиторію через just:
just api

# Або напряму з каталогу apps/api:
dotnet watch run --project src/Crm.Api --launch-profile http
```
API доступне за адресою: `http://localhost:5065`  
Документація Scalar: `http://localhost:5065/scalar`

---

## 🖥 Консольна утиліта `Crm.Cli`

Проєкт **`Crm.Cli`** — це автономна утиліта командного рядка, призначена для адміністративного обслуговування системи, автоматизації скриптів та аварійного відновлення (disaster recovery) доступу без використання вебінтерфейсу.

### Доступні команди CLI

#### 1. Скидання пароля адміністратора (`reset-password`)
Скидає пароль адміністратора з указаною електронною поштою (користувачам без ролі `Admin` відмовляє), оновлює security stamp та **інвалідує всі активні refresh-токени**.

* **Синтаксис:**
  ```bash
  dotnet run --project src/Crm.Cli -- reset-password --email <email>
  # або скорочено:
  dotnet run --project src/Crm.Cli -- reset-password -e <email>
  ```
* **Параметри:**
  * `--email`, `-e` *(обов'язковий)*: Email-адреса облікового запису.
* **Приклад взаємодії:**
  ```text
  $ dotnet run --project src/Crm.Cli -- reset-password -e admin@uniia.org
  Enter new password: ********
  Confirm new password: ********
  info: Password for user 'admin@uniia.org' was successfully updated.
  ```

#### 2. Довідка (`--help` / `-h`)
Виводить перелік усіх зареєстрованих команд утиліти та їхній опис.
```bash
dotnet run --project src/Crm.Cli -- --help
```

---

### Як запускати CLI

#### Локально для розробки (з папки `apps/api`):
```bash
# Переглянути доступні команди
dotnet run --project src/Crm.Cli -- --help

# Виконати скидання пароля
dotnet run --project src/Crm.Cli -- reset-password -e admin@example.com
```

#### З кореня всього репозиторію:
```bash
just cli reset-password -e admin@example.com
# або напряму:
dotnet run --project apps/api/src/Crm.Cli -- reset-password -e admin@example.com
```
`Crm.Cli` має той самий `UserSecretsId`, що й `Crm.Api`, тому бере рядок підключення, який записав `just setup`.

#### Запуск скомпільованого бінарника (Publish):
```bash
# Збірка релізу
dotnet publish src/Crm.Cli/Crm.Cli.csproj -c Release -o ./dist/cli

# Запуск
./dist/cli/Crm.Cli reset-password -e admin@example.com
# або через dotnet
dotnet ./dist/cli/Crm.Cli.dll reset-password -e admin@example.com
```

#### У середовищі Docker / на сервері:
Образ API містить CLI у `/app/cli` і обгортку `crm` у `PATH`. Вона бере рядок підключення контейнера, тобто працює з тією самою базою, що й запущений API:
```bash
docker compose exec api crm reset-password -e admin@example.com
# або через just:
just cli-docker reset-password -e admin@example.com
```
Якщо стандартний ввід перенаправлено, CLI не питає пароль, а читає його й підтвердження з двох рядків вводу. Так CI перевіряє команду:
```bash
printf 'New-Passw0rd\nNew-Passw0rd\n' | docker compose exec -T api crm reset-password -e admin@example.com
```

#### Вивід і налаштування CLI
CLI пише логи лише в консоль, у файли нічого не потрапляє. За замовчуванням видно власні повідомлення утиліти (`Information` і вище) та лише попередження й помилки `Microsoft.*` і `System.*`. Помилки йдуть у stderr. Кольори вмикаються тільки в інтерактивному терміналі. Очікувані невдачі (невідомий email, користувач не адміністратор, пароль не відповідає політиці) займають один рядок, неочікувані — з деталями винятку.

Налаштування читаються з `appsettings.json`, потім зі змінних середовища, потім зі змінних з префіксом `CRM_CLI_`. Префіксні діють лише на CLI, навіть у контейнері API, який ділить з API змінні середовища:
```bash
# Показати логи EF Core та інших бібліотек, коли треба розібратися з помилкою
docker compose exec -e CRM_CLI_Logging__LogLevel__Microsoft=Information api crm reset-password -e admin@example.com
```

| Змінна | За замовчуванням | Призначення |
|---|---|---|
| `CRM_CLI_Logging__LogLevel__Default` | `Information` | Рівень власних повідомлень CLI |
| `CRM_CLI_Logging__LogLevel__Microsoft` | `Warning` | Рівень EF Core, Identity, Data Protection |
| `CRM_CLI_Logging__Console__FormatterOptions__ColorBehavior` | `Default` | `Enabled` / `Disabled` — примусово ввімкнути чи вимкнути кольори |
| `CRM_CLI_ConnectionStrings__PostgreSqlConnection` | з контейнера | Підключити CLI до іншої бази |

---

## 🧪 Тестування та перевірки якості коду

Всі команди можна запускати з кореневого каталогу за допомогою `just` або напряму через .NET CLI:

```bash
# Форматування коду
dotnet format Crm.slnx

# Запуск модульних та інтеграційних тестів
dotnet test Crm.slnx

# Перевірка синхронізації контракту openapi.json з кодом
dotnet build src/Crm.Api -p:GenerateOpenApi=true
git diff --exit-code -- ../../packages/api-contract/openapi.json
```
