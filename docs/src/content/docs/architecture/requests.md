---
title: Обробка запитів
description: Шлях HTTP-запиту від контролера через MediatR до бази та обробка помилок.
sidebar:
  order: 2
---

```
HTTP ─▶ rate limiting ─▶ автентифікація JWT ─▶ авторизація (права)
     ─▶ контролер ─▶ IMediator.Send
     ─▶ ValidationBehavior ─▶ TransactionalBehavior ─▶ обробник
```

## Контролер

Контролер лише приймає запит, перевіряє права й передає команду або запит
у MediatR. Бізнес-логіки в контролерах немає.

```csharp
[HttpGet]
[HasAccessRight(AccessRight.ViewUser)]
public async Task<ActionResult<PagedResult<UserDto>>> GetUsers(
    [FromQuery] GetUsersQuery query, CancellationToken cancellationToken)
{
    var result = await mediator.Send(query, cancellationToken);
    return Ok(result);
}
```

Маршрути — `api/v{version:apiVersion}/[controller]` з `[ApiVersion("1.0")]`,
тобто `/api/v1/users`.

## Команди й запити

- **Команда** змінює стан, **запит** лише читає. Обидва — `record`, що
  реалізує `IRequest` або `IRequest<T>`.
- Назва відображає дію: `CreateRoleCommand`, `GetUserProfileQuery`.
- Обробник — `IRequestHandler<TRequest, TResponse>` з тією ж назвою й
  суфіксом `Handler`.

```csharp
public record CreateRoleCommand(
    string Name,
    string? FeminitiveName,
    string? PluralName) : IRequest<Guid>, ITransactionalCommand;
```

## Поведінки конвеєра

Виконуються в порядку реєстрації (`AddApplicationServices`):

1. **`ValidationBehavior`** — запускає всі валідатори FluentValidation для
   запиту. Якщо є помилки, кидає `ValidationException`, і обробник не
   викликається.
2. **`TransactionalBehavior`** — лише для запитів з маркером
   `ITransactionalCommand`: відкриває транзакцію через `IUnitOfWork`,
   комітить після успішного обробника й відкочує, якщо він кинув виняток.

Команда, яка змінює кілька записів або викликає кілька сервісів,
позначається `ITransactionalCommand`, щоб зміни застосувались атомарно.

## Помилки

Винятки перехоплює `GlobalExceptionHandler` і повертає
[Problem Details](https://www.rfc-editor.org/rfc/rfc9457). Код відповіді
визначає `ExceptionExtensions.MapToStatusCode`:

| Виняток                                                            | Код |
| ------------------------------------------------------------------ | --- |
| `EntityNotFoundException`, `EntitiesNotFoundException`             | 404 |
| `EntityAlreadyExistsException`                                     | 409 |
| `ValidationException`, `UserOperationException`, `ArgumentException`, `InvalidOperationException` | 400 |
| `InvalidCredentialException`                                       | 401 |
| `UnauthorizedAccessException`                                      | 403 |
| Будь-який інший                                                    | 500 |

Обробник не повертає коди помилок вручну: він кидає відповідний виняток.

## Обмеження частоти запитів

Усі контролери використовують політику `Global` (`IpRateLimiterPolicy`):
ліміт рахується за IP-адресою клієнта, параметри — у секції
`RateLimiting`. Перевищення ліміту — відповідь 429 і попередження в лозі.
