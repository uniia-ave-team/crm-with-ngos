# Крок 2. Контракт API — план реалізації

> **Для агентів:** виконується в гілці `feat/api-contract` (відгалужена від
> `chore/dev-tooling`). Кроки позначаються чекбоксами (`- [ ]`).

**Мета:** `packages/api-contract/openapi.json` генерується з коду .NET, з нього
генерується Angular-клієнт; `just ci` ловить розбіжність контракту з кодом.

**Spec:** [`../specs/2026-10-02-devops-and-docs-design.md`](../specs/2026-10-02-devops-and-docs-design.md),
розділ 3.

**Стек:** `Microsoft.Extensions.ApiDescription.Server` 10.0.12,
`Asp.Versioning.OpenApi` 10.2.3, `ng-openapi-gen` 1.1.0.

## Глобальні обмеження

- Мова: коміти й код англійською, документація українською.
- Документ `v1`, маршрути `/api/v1/...` не змінюються.
- Поведінка API в рантаймі не змінюється.
- Згенерований клієнт не комітиться.

---

### Task 1: Генерація `openapi.json` під час збірки

**Files:**
- Modify: `apps/api/src/Crm.Api/Crm.Api.csproj`, `apps/api/src/Crm.Api/Program.cs`
- Create: `packages/api-contract/openapi.json` (генерується)

- [x] Пакет `Microsoft.Extensions.ApiDescription.Server`; генерація лише з
  `-p:GenerateOpenApi=true`, вихід `packages/api-contract/openapi.json`.
- [x] Міграції й сидинг пропускаються, коли точка входу —
  `GetDocument.Insider` (генератор виконує `Program.cs` повністю).
- [x] Перевірка: `dotnet build apps/api/src/Crm.Api -p:GenerateOpenApi=true`
  створює OpenAPI 3.1 з 27 шляхами.

### Task 2: Якість документа

**Files:**
- Modify: `apps/api/src/Crm.Api/Extensions/ApiVersioningExtensions.cs`
- Create: `apps/api/src/Crm.Api/OpenApi/Transformers/OperationIdOperationTransformer.cs`,
  `apps/api/src/Crm.Api/OpenApi/Transformers/JsonContentTypeOperationTransformer.cs`

- [x] Заголовок `CRM API` через `AddOpenApi(options => options.Document...)`.
- [x] `operationId` = `{Controller}{Action}`: 43 операції, усі унікальні.
- [x] Лише `application/json` там, де форматери оголошують ще `text/plain`,
  `text/json`, `application/*+json`.
- [x] Перевірка: API в режимі розробки стартує, застосовує міграції,
  `/openapi/v1.json` має заголовок `CRM API`.

### Task 3: Angular-клієнт

**Files:**
- Create: `apps/web/ng-openapi-gen.json`
- Modify: `apps/web/package.json`, `apps/web/package-lock.json`, `apps/web/eslint.config.js`

- [x] `generate:api` запускається перед `start`, `build`, `test`, `test:ci`.
- [x] `src/app/api/generated` виключено з lint (у `.gitignore` уже був).
- [x] Перевірка: 43 функції без попереджень генератора; згенерований код
  компілюється з `strict`, `noUnusedLocals`, `noUnusedParameters`.

### Task 4: Команди й документація

**Files:**
- Modify: `justfile`, `README.md`, `CONTRIBUTING.md`, spec

- [x] `just contract`, `contract-spec`, `contract-client`, `contract-check`;
  `contract-check` входить у `just ci`.
- [x] Розділ «Зміна API» в `CONTRIBUTING.md`.
- [x] Перевірка: `just ci` проходить.
