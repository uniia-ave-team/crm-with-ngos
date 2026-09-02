# API

Рішення ASP.NET Core ще не створене. Каталог самодостатній: `Crm.sln`
розташовується тут, а не в корені репозиторію, щоб IDE не індексувала
`apps/web` разом із `node_modules`.

## Стек

- .NET 10, ASP.NET Core
- Entity Framework Core 10, Npgsql
- PostgreSQL 17
- OpenAPI через вбудований `Microsoft.AspNetCore.OpenApi`

## Очікувана структура

```
Crm.sln
Directory.Build.props          спільні налаштування збірки; версію підставляє CI
Directory.Packages.props       централізоване керування версіями пакетів
Dockerfile                     sdk:10.0 -> aspnet:10.0-noble-chiseled
entrypoint.sh                  міграції під advisory lock, потім запуск застосунку
src/
  Crm.Api/                     ендпоінти, DI, генерація OpenAPI
  Crm.Application/             сценарії використання
  Crm.Domain/                  Contact, Organisation, Deal, Pipeline
  Crm.Infrastructure/          EF Core, Migrations/, інтеграції
```

## Обмеження, що випливають зі способу постачання

**Порт.** Контейнер слухає порт 8080 (`ASPNETCORE_HTTP_PORTS`); саме туди
проксує Caddy згідно з конфігурацією в `deploy/docker-compose.yml`.

**Міграції.** Застосовуються при старті контейнера на серверах, до яких команда
не має доступу. Реалізація — `dotnet ef migrations bundle`: самодостатній
виконуваний файл у образі, який `entrypoint.sh` запускає перед застосунком під
PostgreSQL advisory lock. Правила написання міграцій —
[docs/development.md](../../docs/development.md#міграції-бази).

**Перевірка стану.** Chiseled-образ не містить оболонки та `curl`. Healthcheck
для Compose реалізується або окремим мінімальним виконуваним файлом у образі,
або інструкцією `HEALTHCHECK` у Dockerfile.

**Конфігурація.** Усі параметри читаються зі змінних середовища у стандартній
нотації ASP.NET Core (`ConnectionStrings__Default`, `Auth__SigningKey`).
Файли `appsettings.*.json` у образі не містять нічого специфічного для
інсталяції.

**Мова відповідей.** Повідомлення про помилки, що доходять до користувача,
повертаються українською; технічні логи — англійською.
