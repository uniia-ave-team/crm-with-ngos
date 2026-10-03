# Крок 1. Інструменти розробки — план реалізації

> **Для агентів:** виконується послідовно в гілці `chore/dev-tooling`. Кроки
> позначаються чекбоксами (`- [x]`).

**Мета:** локальний флоу розробки однією командою `just`: база в Docker, API і
вебклієнт на хості, перевірки якості, ті самі команди, що використовуватиме CI.

**Spec:** [`../specs/2026-10-02-devops-and-docs-design.md`](../specs/2026-10-02-devops-and-docs-design.md),
розділи 1 і 2.

**Стек:** just 1.58, Docker Compose, .NET SDK 10 (local tools у
`dotnet-tools.json`: `dotnet-ef`, `dotnet-reportgenerator-globaltool`),
Node.js 24, Angular CLI 22.

## Глобальні обмеження

- Коміти, гілки, коментарі в коді й конфігураціях — англійською; документація —
  українською (`.cursor/rules/language.mdc`).
- Рецепти `justfile` працюють у PowerShell (Windows) і `sh` (macOS, Linux).
- Порти: PostgreSQL `5432`, API `http://localhost:5065` (профіль `http`),
  вебклієнт `http://localhost:4200`.
- Обліковий запис бази розробки: база `crm`, користувач `crm`, пароль `crm`.
- Секрети лише в user-secrets; наявні значення не перезаписуються.

## Базовий стан (виконано перед планом)

- [x] `apps/api/.editorconfig`: `end_of_line = lf`, щоб збігатися з
  `.gitattributes`; `dotnet format --verify-no-changes` проходить.
- [x] Виправлено тест `PermissionExtensionsTests` після перейменування
  `ManageRolePermissions` на `ManageRole`; 14 тестів проходять.
- [x] Вебклієнт: `ng lint`, `ng test --configuration ci`, production-збірка
  проходять.

---

### Task 1: Версії інструментів і база розробки

**Files:**
- Create: `global.json`, `.nvmrc`, `compose.yaml`, `dotnet-tools.json`

- [x] `global.json`: SDK `10.0.100`, `rollForward: latestFeature`.
- [x] `.nvmrc`: `24`.
- [x] `compose.yaml`: сервіс `db` (`postgres:17-alpine`), порт
  `127.0.0.1:5432:5432`, том `db_data`, healthcheck `pg_isready`.
- [x] Local tool manifest: `dotnet new tool-manifest`,
  `dotnet tool install dotnet-ef --version 10.0.12`,
  `dotnet tool install dotnet-reportgenerator-globaltool`.
- [x] Перевірка: `docker compose up -d --wait db` завершується успіхом;
  `dotnet tool restore` відновлює обидва інструменти.

### Task 2: `justfile`

**Files:**
- Replace: `justfile`

Рецепти: `setup`, `db`, `db-down`, `db-reset`, `psql`, `api`, `web`,
`migration-add`, `migration-remove`, `fmt`, `lint`, `test`, `test-api`,
`test-web`, `test-coverage`, `ci`, `api-ci`, `web-ci`. Приватні:
`_check-tools`, `_secrets`, `_clean-test-results` з варіантами `[windows]` і
`[unix]`.

- [x] Перевірка: `just --list` показує всі публічні рецепти.
- [x] Перевірка: `just setup` на чистій машині записує
  `ConnectionStrings:PostgreSqlConnection` і `JwtOptions:Secret`; повторний
  запуск їх не змінює.

### Task 3: Проксі вебклієнта

**Files:**
- Create: `apps/web/proxy.conf.json`
- Modify: `apps/web/angular.json` (`serve.options.proxyConfig`)

- [x] `/api` → `http://localhost:5065`.
- [x] Перевірка: `just db`, `just api`, `just web`; запит
  `GET http://localhost:4200/api/v1/system/status` повертає `200`.

### Task 4: Перевірки якості через `just`

- [x] `just lint` → код виходу 0.
- [x] `just test` → 14 тестів API, 3 тести вебклієнта, код виходу 0.
- [x] `just test-coverage` → `apps/api/TestResults/Report/index.html`.

### Task 5: Прибирання і нова документація

**Files:**
- Delete: `deploy/`, `docs/deployment/`, `docs/architecture/`,
  `docs/development.md`, `.github/workflows/README.md`, `apps/api/README.md`,
  `apps/web/README.md`, `packages/api-contract/README.md`,
  `apps/api/generate_test_report.bat`
- Modify: `.gitignore`, `.gitattributes` (прибрати правила для `deploy/` і
  `*.command`), `.github/CODEOWNERS`
- Replace: `README.md`, `CONTRIBUTING.md`

- [x] `README.md`: що це, стек, структура, швидкий старт, основні команди.
- [x] `CONTRIBUTING.md`: гілки, коміти англійською за Conventional Commits,
  squash merge, перевірки перед PR, міграції.
- [x] Перевірка: у репозиторії немає посилань на видалені файли
  (`rg "deploy/|docs/deployment|docs/development.md|generate_test_report"`).

### Task 6: Коміти

- [x] Окремі коміти: інструменти й база; `justfile`; проксі; прибирання й
  документація.
