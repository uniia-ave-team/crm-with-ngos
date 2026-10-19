---
title: Структура репозиторію
description: Де що лежить у монорепозиторії та які команди є в justfile.
sidebar:
  order: 2
---

Увесь код — в одному репозиторії з однією версією.

```
apps/
  api/                  ASP.NET Core API, рішення Crm.slnx
    src/Crm.Cli/        адмін-утиліта (скидання пароля)
    Dockerfile          образ API разом з адмін-утилітою
  web/                  Ionic Angular клієнт
    Dockerfile          образ вебклієнта (nginx)
    nginx.conf          роздача статики й кешування
packages/
  api-contract/
    openapi.json        контракт API, генерується з коду .NET
docs/                   цей сайт (Starlight)
  superpowers/          дизайн-документи й плани (не публікуються)
.github/
  workflows/            CI, CodeQL, перевірка назви PR, публікація сайту
  dependabot.yml        оновлення залежностей
  CODEOWNERS            відповідальні за частини репозиторію
compose.yaml            локальний стек: PostgreSQL, API, вебклієнт
.dockerignore           що потрапляє в контекст збірки образів
justfile                усі команди розробки
global.json             версія .NET SDK
.nvmrc                  версія Node.js
dotnet-tools.json       локальні інструменти .NET
```

## Команди

`just` без аргументів показує всі команди з описами.

| Команда                    | Дія                                                        |
| -------------------------- | ---------------------------------------------------------- |
| `just setup`               | Встановити залежності й налаштувати секрети                |
| `just db`                  | Запустити лише базу в Docker                               |
| `just up`                  | Зібрати й запустити все в Docker                           |
| `just down`                | Зупинити контейнери, дані лишаються                        |
| `just reset`               | Зупинити контейнери й видалити всі локальні дані           |
| `just logs`                | Логи API й вебклієнта в Docker                             |
| `just psql`                | Консоль `psql` до бази розробки                            |
| `just cli <аргументи>`     | Адмін-утиліта `Crm.Cli` з локальною базою                  |
| `just cli-docker <аргументи>` | Адмін-утиліта в контейнері API                          |
| `just api` / `just web`    | Запустити API / вебклієнт                                  |
| `just docs`                | Запустити цей сайт локально                                |
| `just migration-add Name`  | Створити міграцію EF Core                                  |
| `just migration-remove`    | Видалити останню незастосовану міграцію                    |
| `just contract`            | Оновити `openapi.json` і Angular-клієнт після зміни API    |
| `just fmt`                 | Відформатувати код                                         |
| `just lint`                | Перевірити форматування й lint                             |
| `just test`                | Запустити всі тести                                        |
| `just test-coverage`       | Тести API зі звітом покриття                               |
| `just ci`                  | Усі перевірки, які виконує CI                              |

CI викликає ті самі рецепти (`api-ci`, `web-ci`, `contract-check`,
`docs-build`, `up`), тому результат локального `just ci` збігається з CI.
Збірку образів `just ci` не запускає: це робить `just up`.
