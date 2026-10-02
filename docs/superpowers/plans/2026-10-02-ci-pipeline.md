# Крок 3. CI — план реалізації

> **Для агентів:** виконується в гілці `ci/pipeline` (відгалужена від
> `feat/api-contract`). Кроки позначаються чекбоксами (`- [ ]`).

**Мета:** кожен PR і push у `main` перевіряються тими самими командами
`just`, що й локально; підсумкова перевірка `ci-ok` і `pr-title` придатні як
обов'язкові в правилах гілки; залежності й код скануються на вразливості.

**Spec:** [`../specs/2026-10-02-devops-and-docs-design.md`](../specs/2026-10-02-devops-and-docs-design.md),
розділ 4.

**Стек:** GitHub Actions: `actions/checkout` v7, `actions/setup-dotnet` v6,
`actions/setup-node` v7, `github/codeql-action` v4, `dorny/paths-filter`
v4.0.3, `extractions/setup-just` v4, `aquasecurity/trivy-action` v0.36.0,
`oasdiff/oasdiff-action` v0.1.18, `amannn/action-semantic-pull-request`
v6.1.1.

## Глобальні обмеження

- Мова: коміти й код англійською, документація українською.
- Логіка перевірок — у `justfile`; workflow лише встановлює інструменти.
- Сторонні actions закріплені за SHA коміту.
- Шаблонів PR та issue немає.

---

### Task 1: `ci.yml`

**Files:**
- Create: `.github/workflows/ci.yml`

- [x] `changes`: фільтри `api`, `web`, `contract`, `tooling`.
- [x] `api`: `just api-ci`, `just contract-check`; у PR — `oasdiff breaking`
  у підсумок джоби, без блокування.
- [x] `web`: Node з `.nvmrc`, кеш npm, `just web-ci`.
- [x] `trivy`: `fs`, вразливості з виправленнями й помилки конфігурації,
  SARIF у Security; завантаження не валить джобу, поки code scanning
  недоступний.
- [x] `ci-ok`: падає лише на `failure` або `cancelled`.

### Task 2: Інші workflow і Dependabot

**Files:**
- Create: `.github/workflows/pr-title.yml`, `.github/workflows/codeql.yml`,
  `.github/dependabot.yml`

- [x] `pr-title`: типи Conventional Commits, scope `api`, `web`, `docs`,
  `ci`, `deps`, `deps-dev`.
- [x] `codeql`: `csharp`, `javascript-typescript`, `actions`;
  `build-mode: none`.
- [x] Dependabot: `nuget`, `npm`, `github-actions`, щопонеділка, з групами.

### Task 3: Перевірка

- [x] `actionlint` (разом із shellcheck) — без зауважень.
- [x] `check-jsonschema`: `dependabot.yml` і всі workflow відповідають
  схемам.
- [x] У контейнері `mcr.microsoft.com/dotnet/sdk:10.0` (Linux, як раннер):
  `dotnet format --verify-no-changes` проходить; `openapi.json`,
  згенерований з нуля, побайтово збігається із закоміченим.
- [x] Перший запуск на GitHub (PR #4): `ci-ok`, `pr-title`, усі джоби
  зелені; виконано 14 тестів API, `contract-check`, `oasdiff`, lint, тести й
  збірка вебклієнта; результати Trivy потрапили у вкладку Security.
- [x] Назва PR без типу Conventional Commits валить `pr-title`.

### Task 4: Документація

**Files:**
- Modify: `CONTRIBUTING.md`, spec

- [x] Обов'язкові перевірки й неблокувальні сканери в `CONTRIBUTING.md`.
- [x] Spec узгоджено з реалізацією; шаблон PR прибрано.
