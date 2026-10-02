# Флоу розробки, CI і документація — дизайн

Дата: 2026-10-02. Статус: погоджено.

## Мета

Зручний і відтворюваний флоу розробки для `apps/api` (.NET 10) і `apps/web`
(Ionic Angular), автоматичні перевірки в CI, автоматичні релізи, сайт
документації на GitHub Pages та API-документація, що генерується з коду .NET
і публікується після злиття в `main`.

Розгортання (Docker-образи, compose для серверів, інсталятори) не входить у цей
дизайн і буде спроєктоване окремо.

## Рішення

| Питання                      | Рішення                                                        |
| ---------------------------- | -------------------------------------------------------------- |
| Наявна документація, `deploy/` | Видаляються; документація пишеться з нуля під фактичний стан |
| Версіонування API            | Лишається `/api/v{version}` через `Asp.Versioning`, документ `v1` |
| Структура CI                 | Один `ci.yml` з фільтром змін і підсумковою перевіркою `ci-ok`; логіка в `justfile` |
| Релізи                       | release-please, одна версія на весь продукт                     |
| Локальна розробка            | PostgreSQL у Docker; API і клієнт запускаються на хості          |
| Клієнт API для Angular       | Генерується з `openapi.json` через `ng-openapi-gen`              |
| Якість і безпека             | Перевірка назви PR, Dependabot, CodeQL, secret scanning GitHub, Trivy |
| Сайт документації            | Starlight (Astro), українською                                   |
| API-документація             | `openapi.json` під час збірки + статична сторінка Scalar         |
| Хостинг                      | GitHub Pages (репозиторій публічний)                             |
| Мова                         | Коміти, гілки, теги, PR, код і коментарі — англійською; документація та інтерфейс — українською (`.cursor/rules/language.mdc`) |

## 1. Структура репозиторію

```
.github/
  workflows/
    ci.yml
    pr-title.yml
    codeql.yml
    release-please.yml
    docs.yml
  dependabot.yml
  CODEOWNERS
  pull_request_template.md
apps/api/                       ASP.NET Core API (структура коду не змінюється)
apps/web/                       Ionic Angular клієнт
  proxy.conf.json               проксі /api -> http://localhost:5065
  ng-openapi-gen.json           конфігурація генератора клієнта
packages/api-contract/
  openapi.json                  генерується з коду, комітиться
docs/                           сайт Starlight (власний package.json)
  superpowers/specs/            дизайн-документи (не публікуються)
compose.yaml                    PostgreSQL для розробки
justfile                        усі команди
global.json                     версія .NET SDK
.nvmrc                          версія Node.js
release-please-config.json
.release-please-manifest.json
README.md                       що це, швидкий старт, посилання на сайт
CONTRIBUTING.md                 коротко, посилання на сайт
```

Видаляються: `deploy/`, `docs/deployment/`, `docs/architecture/`,
`docs/development.md`, `.github/workflows/README.md`, `apps/api/README.md`,
`apps/web/README.md`, `packages/api-contract/README.md`,
`apps/api/generate_test_report.bat`. Правила `.gitignore`, що стосуються
`deploy/`, прибираються.

## 2. Локальний флоу розробки

### Інструменти

.NET SDK 10 (версія зафіксована в `global.json`), Node.js LTS (`.nvmrc`),
Docker Desktop, `just`.

### Команди `justfile`

| Команда              | Дія                                                                     |
| -------------------- | ----------------------------------------------------------------------- |
| `just setup`         | Перевірка інструментів, `dotnet restore`, `npm ci` (web, docs), user-secrets для API |
| `just db`            | PostgreSQL 17 у Docker, порт 5432, іменований том                        |
| `just db-down`       | Зупинити базу                                                            |
| `just db-reset`      | Зупинити базу й видалити том                                             |
| `just api`           | `dotnet watch` для `Crm.Api`, `http://localhost:5065`                    |
| `just web`           | `ng serve`, `http://localhost:4200`, проксі `/api` на API                |
| `just docs`          | Локальний сервер сайту документації                                      |
| `just contract`      | `openapi.json` з коду API, потім генерація Angular-клієнта               |
| `just fmt`           | `dotnet format` і форматування вебклієнта                                |
| `just lint`          | Перевірка форматування й lint обох частин                                |
| `just test`          | Тести API та вебклієнта                                                  |
| `just test-coverage` | Тести API з покриттям і HTML-звітом (заміна `generate_test_report.bat`)  |
| `just ci`            | Усе, що виконує CI, локально                                             |

Команди CI-джоб (`api-ci`, `web-ci`, `contract-check`, `docs-build`) також
визначені в `justfile`; workflow лише встановлює інструменти й викликає їх.

### Секрети

`just setup` записує в user-secrets проєкту `Crm.Api`
(`UserSecretsId` уже задано):

- `ConnectionStrings:PostgreSqlConnection` — рядок підключення до бази з
  `compose.yaml`;
- `JwtOptions:Secret` — випадкове значення, якщо секрет ще не задано.

Наявні значення не перезаписуються. У репозиторії секретів немає.

### Проксі

`apps/web/proxy.conf.json` перенаправляє `/api` на `http://localhost:5065`.
Клієнт звертається до API за відносним шляхом `/api/v1/...`, тому CORS у
розробці не задіяний, а маршрутизація збігається з майбутньою схемою
«один origin».

## 3. Контракт API

### Генерація `openapi.json`

- `Crm.Api` отримує пакет `Microsoft.Extensions.ApiDescription.Server`.
- Генерація під час збірки вмикається властивістю `GenerateOpenApi=true`
  (`OpenApiGenerateDocuments` залежить від неї); звичайна збірка її не
  виконує.
- Документ `v1` записується в `packages/api-contract/openapi.json`
  (`OpenApiDocumentsDirectory`, `--file-name openapi`).
- Описи операцій і моделей беруться з XML-коментарів
  (`GenerateDocumentationFile` уже ввімкнено).
- Генератор (`GetDocument.Insider`) виконує `Program.cs` повністю, тому
  міграції та сидинг пропускаються, коли точка входу запущена ним; база під
  час генерації не потрібна.
- Заголовок документа — `CRM API` (задається через колбек
  `AddOpenApi` бібліотеки `Asp.Versioning`, інакше його перезаписує її
  трансформер).
- `operationId` має вигляд `{Controller}{Action}`; з нього генератор клієнта
  бере назви функцій.
- Там, де JSON-форматери додатково оголошують `text/plain`, `text/json` або
  `application/*+json`, документ містить лише `application/json`; поведінка
  API не змінюється.

### Генерація Angular-клієнта

- `ng-openapi-gen` (devDependency `apps/web`), вихід —
  `apps/web/src/app/api/generated` (у `.gitignore`).
- Скрипт `generate:api` в `apps/web/package.json` запускається автоматично
  перед `start`, `build` і `test` (`prestart`, `prebuild`, `pretest`).
  Фронтенд-розробникові не потрібен .NET: клієнт будується із закоміченого
  `openapi.json`.

### Перевірка в CI

- Специфікація перегенеровується з коду; якщо вона відрізняється від
  закоміченої, джоба падає з підказкою виконати `just contract`.
- `oasdiff breaking` порівнює специфікацію PR зі специфікацією в `main` і пише
  список ламких змін у підсумок джоби. Ламкі зміни не блокують PR: API
  внутрішній і змінюється разом із клієнтом.

## 4. CI

### `ci.yml`

Тригери: `pull_request` у `main`, `push` у `main`.

| Джоба      | Умова запуску                          | Дії                                                            |
| ---------- | -------------------------------------- | -------------------------------------------------------------- |
| `changes`  | завжди                                 | `dorny/paths-filter`: `api`, `web`, `docs`, `contract`         |
| `api`      | змінено `api`                          | `just api-ci`: `dotnet format --verify-no-changes`, збірка, тести |
| `contract` | змінено `api` або `contract`           | `just contract-check`, `oasdiff` у підсумок                     |
| `web`      | змінено `web` або `contract`           | `just web-ci`: `npm ci`, генерація клієнта, lint, тести, production-збірка |
| `docs`     | змінено `docs` або `contract`          | `just docs-build`                                               |
| `trivy`    | завжди                                 | `trivy fs`: вразливі залежності npm, помилки конфігурації; SARIF у вкладку Security |
| `ci-ok`    | завжди, після всіх                     | Падає, якщо будь-яка джоба завершилась `failure` або `cancelled`; `skipped` вважається успіхом |

`ci-ok` — єдина обов'язкова перевірка CI в правилах гілки `main`.

Тести API використовують Testcontainers; раннер `ubuntu-latest` має Docker.

Вразливі NuGet-пакети виявляє вбудований NuGetAudit під час `dotnet restore`
(у .NET 10 перевіряються й транзитивні залежності); через
`TreatWarningsAsErrors` збірка з такими пакетами падає. Trivy не дублює цю
перевірку, тому lock-файли NuGet не потрібні.

### Інші workflow

- **`pr-title.yml`** — `amannn/action-semantic-pull-request` на подіях
  `opened`, `edited`, `synchronize`. Типи Conventional Commits; допустимі
  scope: `api`, `web`, `docs`, `ci`, `deps`; scope необов'язковий.
- **`codeql.yml`** — мови `csharp` (`build-mode: none`) і
  `javascript-typescript`; на PR у `main`, push у `main` і щотижня.
- **`release-please.yml`** — на push у `main`. Тип релізу `simple` з однією
  версією для всього репозиторію. Додаткові файли з версією:
  `apps/api/Directory.Build.props` (`<Version>`) і `apps/web/package.json`.
  Злиття реліз-PR створює тег `vX.Y.Z` і GitHub Release з changelog.
- **`docs.yml`** — на push у `main` при зміні `docs/**` або
  `packages/api-contract/**`, а також вручну. Збирає сайт, кладе
  `openapi.json` поруч зі сторінкою Scalar і публікує через
  `actions/deploy-pages`.
- **`dependabot.yml`** — щотижня: `nuget` (`/apps/api`), `npm` (`/apps/web`,
  `/docs`), `github-actions` (`/`). Групи: Angular та Ionic; ASP.NET Core та
  EF Core; тестові пакети; GitHub Actions. Назви PR відповідають
  Conventional Commits (`chore(deps): ...`).

Trivy сканує образи, коли з'являться Dockerfile-и (поза межами цього дизайну).

## 5. Сайт документації

- Starlight у `docs/`, власний `package.json`, українська — локаль за
  замовчуванням.
- Адреса: `https://uniia-ave-team.github.io/crm-with-ngos/`
  (`site` і `base` в `astro.config.mjs`).
- Вбудований пошук Starlight (Pagefind).

### Розділи

| Розділ                    | Зміст                                                                 |
| ------------------------- | --------------------------------------------------------------------- |
| Початок роботи            | Інструменти, `just setup`, перший запуск, структура репозиторію        |
| Розробка                  | Гілки й коміти, PR і перевірки, зміна API та контракт, міграції EF Core, тести, релізи |
| Архітектура               | Шари Domain / Application / Infrastructure / Api, MediatR і поведінки (валідація, транзакції), права доступу (`AccessRight`, policy-based авторизація), логування Serilog |
| Архітектурні рішення      | 0001 монорепозиторій і одна версія; 0002 версіонування `/api/v1`; 0003 Starlight і GitHub Pages |
| API                       | Посилання на сторінку Scalar                                          |

Розділ про розгортання з'явиться разом із відповідним дизайном.

### Сторінка API

`docs/public/api/index.html` — статична сторінка Scalar
(`@scalar/api-reference` з CDN), що завантажує `./openapi.json`. Під час
збірки `docs.yml` і `just docs-build` копіюють
`packages/api-contract/openapi.json` у `docs/public/api/`. Скопійований файл
у `.gitignore`. Адреса:
`https://uniia-ave-team.github.io/crm-with-ngos/api/`.

## 6. Налаштування GitHub (вручну, потрібні права адміністратора)

1. Зробити репозиторій публічним.
2. Settings → Pages → Source: GitHub Actions.
3. Settings → Code security: увімкнути secret scanning і push protection.
4. Ruleset для `main`: обов'язкові перевірки `ci-ok` і `pr-title`; лише squash
   merge; заборона прямого push і force push.
5. Перевірити, що команди з `CODEOWNERS` існують в організації та мають права
   на запис.

## Порядок реалізації

Кожен крок — окрема гілка й pull request.

1. **`chore/dev-tooling`** — `global.json`, `.nvmrc`, `compose.yaml`,
   `justfile`, user-secrets, `proxy.conf.json`, видалення `deploy/` і старої
   документації, короткі `README.md` і `CONTRIBUTING.md`, оновлений
   `CODEOWNERS`.
2. **`feat/api-contract`** — генерація `openapi.json`, `ng-openapi-gen`,
   `just contract`.
3. **`ci/pipeline`** — `ci.yml`, `pr-title.yml`, `codeql.yml`,
   `dependabot.yml`, шаблон PR.
4. **`docs/site`** — Starlight, сторінка Scalar, `docs.yml`.
5. **`ci/release-please`** — release-please.

## Поза межами

- Dockerfile-и, публікація образів, compose для серверів, інсталятори.
- Версіонована API-документація (`/api/vX.Y/`).
- Довідник класів C# (DocFX).
- Git-хуки, звіт про покриття в PR, централізоване керування версіями NuGet.
