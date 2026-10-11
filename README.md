# CRM для громадських організацій

CRM з відкритим кодом для громадських організацій: користувачі та ролі з
гнучкими правами доступу, профілі організацій, запрошення, власні поля
профілю.

> **Статус: у розробці.** Розгортання на сервер ще не підготовлене.

**Документація:** <https://uniia-ave-team.github.io/crm-with-ngos/>
— швидкий старт, розгортання, посібник користувача,
[розділ для розробників](https://uniia-ave-team.github.io/crm-with-ngos/developers/setup/)
і [довідник API](https://uniia-ave-team.github.io/crm-with-ngos/api/).

## Стек

| Частина   | Технології                                                        |
| --------- | ----------------------------------------------------------------- |
| API       | .NET 10, ASP.NET Core, EF Core 10, PostgreSQL 17, MediatR, FluentValidation, Mapster, Serilog |
| Вебклієнт | Angular 22, Ionic 9, TypeScript, Vitest                           |
| Розробка  | just, Docker Compose (база, API й вебклієнт)                      |

## Структура репозиторію

```
apps/api/                ASP.NET Core API (рішення Crm.slnx), Dockerfile
apps/web/                Ionic Angular клієнт, Dockerfile і nginx.conf
packages/api-contract/   openapi.json — контракт API, генерується з коду
docs/                    сайт документації (Starlight)
compose.yaml             локальний стек: PostgreSQL, API, вебклієнт
justfile                 усі команди розробки
```

## Швидкий старт

Потрібні [Docker Desktop](https://www.docker.com/products/docker-desktop/)
і [just](https://just.systems).

```powershell
winget install Casey.Just      # Windows
```

```bash
brew install just              # macOS
```

### Усе в Docker

```bash
just up       # зібрати й запустити: вебклієнт http://localhost:8080, API http://localhost:5065
just down     # зупинити; дані лишаються
just reset    # зупинити й видалити всі локальні дані
```

### Розробка з hot reload

Додатково потрібні [.NET SDK 10](https://dotnet.microsoft.com/download) і
[Node.js 24](https://nodejs.org). Один раз після клонування:

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
| `just up`                 | Зібрати й запустити все в Docker                     |
| `just down` / `just reset` | Зупинити контейнери / і видалити всі локальні дані  |
| `just logs`               | Логи API й вебклієнта в Docker                       |
| `just db`                 | Запустити лише базу в Docker                         |
| `just psql`               | Консоль `psql` до бази розробки                      |
| `just api` / `just web`   | Запустити API / вебклієнт                            |
| `just docs`               | Запустити сайт документації локально                 |
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

Див. [CONTRIBUTING.md](CONTRIBUTING.md) і розділ
[«Для розробників»](https://uniia-ave-team.github.io/crm-with-ngos/developers/workflow/)
на сайті документації.

## Ліцензія

Ще не визначена. До появи файлу `LICENSE` код не має дозволу на використання.
