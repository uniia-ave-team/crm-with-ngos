---
title: Шари й залежності
description: З яких проєктів складається API і як вони залежать один від одного.
sidebar:
  order: 1
---

API побудований за принципами чистої архітектури: бізнес-правила не
залежать від бази, вебу й сторонніх сервісів.

```
Crm.Api ──────────────▶ Crm.Application ──▶ Crm.Domain
   │                          ▲
   └──▶ Crm.Infrastructure ───┘
```

Стрілка означає `ProjectReference`. `Crm.Infrastructure` залежить від
`Crm.Application` і `Crm.Domain`; `Crm.Api` складає все разом.

## Проєкти

| Проєкт               | Що містить                                                                                       |
| -------------------- | ------------------------------------------------------------------------------------------------ |
| `Crm.Domain`         | Сутності, перелічення (зокрема `AccessRight`), доменні винятки, константи, інтерфейси репозиторіїв. Не залежить ні від чого |
| `Crm.Application`    | Сценарії використання: команди й запити MediatR, обробники, валідатори FluentValidation, DTO, інтерфейси сервісів, поведінки конвеєра |
| `Crm.Infrastructure` | EF Core і PostgreSQL, ASP.NET Core Identity, міграції й сидинг, репозиторії, JWT, кеш прав, фонові служби, налаштування Mapster |
| `Crm.Api`            | Контролери, версіонування, автентифікація й авторизація, обробка винятків, обмеження частоти запитів, health checks, OpenAPI, логування запитів |

## Де шукати

| Що                                  | Де                                                                 |
| ----------------------------------- | ------------------------------------------------------------------ |
| Ендпоінти                           | `Crm.Api/Controllers/V1`                                           |
| Команди й запити                    | `Crm.Application/Dtos/<Область>/Commands`, `.../Queries`           |
| Обробники                           | `Crm.Application/Features/<Область>/Commands`, `.../Queries`       |
| Валідатори                          | `Crm.Application/Features/<Область>/Validators`                    |
| Мапінг Mapster                      | `Crm.Application/Features/<Область>/Mappings`, `Crm.Infrastructure/Mappings` |
| `DbContext`, конфігурації сутностей | `Crm.Infrastructure/Persistence`                                   |
| Міграції                            | `Crm.Infrastructure/Migrations`                                    |
| Реєстрація сервісів                 | `*ServiceCollectionExtensions` у кожному шарі, `Crm.Api/Extensions/HostingExtensions.cs` |

Області (`<Область>`): `Users`, `Roles`, `Ngos`, `LoginPageImages`,
`System`.

## Дані

- `ApplicationDbContext` успадковує `IdentityDbContext` з власними типами
  `AuthUser`, `AuthRole` і ключами `Guid`.
- PostgreSQL через Npgsql; рядок підключення —
  `ConnectionStrings:PostgreSqlConnection`.
- Під час запуску API застосовує міграції, потім сидери (`AdminRoleSeeder`
  створює роль `Admin` з усіма правами) і заповнює кеш прав ролей.

## Конфігурація

Секції `appsettings.json`:

| Секція              | Що налаштовує                                            |
| ------------------- | -------------------------------------------------------- |
| `ConnectionStrings` | Підключення до PostgreSQL                                |
| `JwtOptions`        | Ключ підпису, термін дії access-, refresh- та invitation-токенів |
| `IdentityOptions`   | Вимоги до паролів і користувачів Identity                |
| `CorsSettings`      | Дозволені origin для політики `AllowFrontendClient`      |
| `RateLimiting`      | Ліміти політики `Global`                                 |
| `SerilogOptions`    | Шляхи й ротація файлів логів                             |

Секрети (`ConnectionStrings:PostgreSqlConnection`, `JwtOptions:Secret`) у
репозиторій не потрапляють: локально їх записує `just setup` у
user-secrets.

## Вебклієнт

`apps/web` — Ionic 9 і Angular 22 на `NgModule`. Клієнт API генерується з
`openapi.json` у `src/app/api/generated`
([Зміна API](../../development/api-contract/)).
