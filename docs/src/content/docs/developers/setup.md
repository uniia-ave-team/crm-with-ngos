---
title: Перший запуск
description: Інструменти, налаштування й запуск API, вебклієнта та бази на локальній машині.
sidebar:
  order: 1
---

Локально в Docker працює лише PostgreSQL. API і вебклієнт запускаються
напряму, з hot reload.

## Інструменти

| Інструмент                                                        | Версія                                         |
| ----------------------------------------------------------------- | ---------------------------------------------- |
| [.NET SDK](https://dotnet.microsoft.com/download)                 | 10 (зафіксована в `global.json`)               |
| [Node.js](https://nodejs.org)                                     | 24 (зафіксована в `.nvmrc`)                    |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | будь-яка актуальна                             |
| [just](https://just.systems)                                      | будь-яка актуальна                             |

Встановлення `just`:

```powershell
winget install Casey.Just      # Windows
```

```bash
brew install just              # macOS
```

На Windows команди `just` виконуються у Windows PowerShell; окремо
встановлювати PowerShell 7 не потрібно.

## Налаштування

Один раз після клонування:

```bash
just setup
```

Команда перевіряє наявність інструментів, відновлює пакети .NET і npm (API,
вебклієнт, цей сайт), локальні інструменти .NET (`dotnet-ef`,
`reportgenerator`) і записує в
[user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets)
проєкту `Crm.Api`:

- `ConnectionStrings:PostgreSqlConnection` — підключення до бази з
  `compose.yaml`;
- `JwtOptions:Secret` — випадковий ключ підпису JWT.

Наявні секрети не перезаписуються, тому `just setup` можна запускати
повторно, наприклад після оновлення залежностей.

## Запуск

Кожна команда — в окремому терміналі:

```bash
just db       # PostgreSQL 17 у Docker, localhost:5432
just api      # API з hot reload, http://localhost:5065
just web      # вебклієнт, http://localhost:4200
```

Під час запуску API застосовує міграції та заповнює базу початковими даними.

Вебклієнт звертається до API за відносним шляхом `/api/v1/...`: `ng serve`
перенаправляє `/api` на `http://localhost:5065`, тому CORS у розробці не
задіяний.

## Перевірка

| Адреса                                     | Що має бути                              |
| ------------------------------------------ | ---------------------------------------- |
| <http://localhost:5065/health>             | Статус `Healthy`, зокрема бази даних     |
| <http://localhost:5065/scalar>             | Інтерактивна документація API            |
| <http://localhost:4200>                    | Вебклієнт                                |

## Типові проблеми

**`just db` не стартує.** Docker Desktop не запущений або порт 5432 зайнятий
іншим PostgreSQL. Зупиніть локальну службу PostgreSQL або змініть порт у
`compose.yaml` і рядок підключення в user-secrets.

**API падає з помилкою підключення до бази.** База ще не запущена
(`just db`) або в user-secrets інший рядок підключення. Подивитися
секрети: `dotnet user-secrets list --project apps/api/src/Crm.Api`.

**Потрібна чиста база.** `just db-reset` видаляє контейнер разом з даними;
наступний `just db` і запуск API створять базу заново.
