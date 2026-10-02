# CRM для громадських організацій

CRM з відкритим кодом для громадських організацій: користувачі та ролі з
гнучкими правами доступу, профілі організацій, запрошення, власні поля
профілю.

> **Статус: у розробці.** Розгортання на сервер ще не підготовлене.

## Стек

| Частина   | Технології                                                        |
| --------- | ----------------------------------------------------------------- |
| API       | .NET 10, ASP.NET Core, EF Core 10, PostgreSQL 17, MediatR, FluentValidation, Mapster, Serilog |
| Вебклієнт | Angular 22, Ionic 9, TypeScript, Vitest                           |
| Розробка  | just, Docker (лише база даних)                                    |

## Структура репозиторію

```
apps/api/                ASP.NET Core API (рішення Crm.slnx)
apps/web/                Ionic Angular клієнт
packages/api-contract/   openapi.json — контракт API, генерується з коду
compose.yaml             PostgreSQL для розробки
justfile                 усі команди розробки
```

## Швидкий старт

Потрібні: [.NET SDK 10](https://dotnet.microsoft.com/download),
[Node.js 24](https://nodejs.org), [Docker Desktop](https://www.docker.com/products/docker-desktop/)
і [just](https://just.systems).

```powershell
winget install Casey.Just      # Windows
```

```bash
brew install just              # macOS
```

Один раз після клонування:

```bash
just setup    # залежності .NET і npm, локальні секрети API
```

Щоразу для роботи — кожна команда в окремому терміналі:

```bash
just db       # PostgreSQL у Docker
just api      # API з hot reload: http://localhost:5065
just web      # вебклієнт: http://localhost:4200
```

Інтерактивна документація API (Scalar) доступна в режимі розробки за адресою
<http://localhost:5065/scalar>.

## Команди

`just` без аргументів показує всі команди. Основні:

| Команда                   | Дія                                                  |
| ------------------------- | ---------------------------------------------------- |
| `just setup`              | Встановити залежності й налаштувати секрети          |
| `just db` / `just db-down` | Запустити / зупинити базу                           |
| `just db-reset`           | Видалити локальну базу разом з даними                |
| `just psql`               | Консоль `psql` до бази розробки                      |
| `just api` / `just web`   | Запустити API / вебклієнт                            |
| `just migration-add Name` | Створити міграцію EF Core                            |
| `just contract`           | Оновити `openapi.json` і Angular-клієнт після зміни API |
| `just fmt`                | Відформатувати код                                   |
| `just lint`               | Перевірити форматування й lint                       |
| `just test`               | Запустити всі тести                                  |
| `just test-coverage`      | Тести API зі звітом покриття                         |
| `just ci`                 | Усі перевірки, які виконує CI                        |

## Конфігурація API

`just setup` записує в
[user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets)
проєкту `Crm.Api` рядок підключення до бази з `compose.yaml` та випадковий
JWT-секрет. Наявні значення не перезаписуються. Решта налаштувань — у
`apps/api/src/Crm.Api/appsettings*.json`.

## Участь у проєкті

Див. [CONTRIBUTING.md](CONTRIBUTING.md).

## Ліцензія

Ще не визначена. До появи файлу `LICENSE` код не має дозволу на використання.
