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
  web/                  Ionic Angular клієнт
packages/
  api-contract/
    openapi.json        контракт API, генерується з коду .NET
docs/                   цей сайт (Starlight)
  superpowers/          дизайн-документи й плани (не публікуються)
.github/
  workflows/            CI, CodeQL, перевірка назви PR, публікація сайту
  dependabot.yml        оновлення залежностей
  CODEOWNERS            відповідальні за частини репозиторію
compose.yaml            PostgreSQL для розробки
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
| `just db` / `just db-down` | Запустити / зупинити базу                                  |
| `just db-reset`            | Видалити локальну базу разом з даними                      |
| `just psql`                | Консоль `psql` до бази розробки                            |
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
`docs-build`), тому результат локального `just ci` збігається з CI.
