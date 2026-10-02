---
title: Тести
description: Тестові проєкти, бібліотеки й команди запуску.
sidebar:
  order: 8
---

## Команди

```bash
just test             # тести API і вебклієнта
just test-api         # лише API
just test-web         # лише вебклієнт
just test-coverage    # тести API з HTML-звітом покриття
```

Звіт покриття — `apps/api/TestResults/Report/index.html`. Згенерований
код (`*.g.cs`, `*.generated.cs`) у покриття не входить.

## API

Кожен шар має власний тестовий проєкт:

| Проєкт                     | Що тестувати                                              | Бібліотеки                                    |
| -------------------------- | --------------------------------------------------------- | --------------------------------------------- |
| `Crm.Domain.Tests`         | Сутності, розширення, доменні винятки                     | xUnit, Shouldly, Bogus                        |
| `Crm.Application.Tests`    | Обробники й валідатори з підмінами сервісів               | + NSubstitute                                 |
| `Crm.Infrastructure.Tests` | Репозиторії й сервіси з реальною PostgreSQL               | + Testcontainers, Respawn                     |
| `Crm.Api.Tests`            | Ендпоінти через `WebApplicationFactory`                   | + `Microsoft.AspNetCore.Mvc.Testing`, Testcontainers, Respawn |
| `Crm.Architecture.Tests`   | Правила залежностей між шарами                            | NetArchTest                                   |

Зараз тести є лише в `Crm.Domain.Tests`; інші проєкти підготовлені й
чекають на перші тести.

Для тестів з базою в проєктах уже підключені Testcontainers (PostgreSQL
у контейнері на час тестів) і Respawn (очищення бази між тестами). Такі
тести потребують запущеного Docker, але не `just db`. У CI Docker є на
раннері.

### Домовленості

- Назва тестового класу — `{КласЩоТестується}Tests`, методу — без
  підкреслень, у вигляді «що робить і що очікуємо»:
  `ToClaimValueShouldReturnCorrectFormattedString`.
- Перевірки — через Shouldly (`result.ShouldBe(...)`), тестові дані —
  через Bogus.
- Структура папок тестового проєкту повторює структуру проєкту, що
  тестується.

## Вебклієнт

Тести — Vitest через `ng test`; файли `*.spec.ts` поруч з компонентами.
`just test-web` запускає їх один раз без режиму спостереження
(конфігурація `ci`).
