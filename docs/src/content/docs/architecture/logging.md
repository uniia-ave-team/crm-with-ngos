---
title: Логування
description: Serilog, файли логів, ідентифікатори подій і правила запису логів у коді.
sidebar:
  order: 4
---

## Куди пишуться логи

API використовує [Serilog](https://serilog.net):

| Приймач  | Що потрапляє                                                  |
| -------- | ------------------------------------------------------------- |
| Консоль  | Усі події                                                     |
| `all-.log`    | Усі події, щоденна ротація                               |
| `errors-.log` | Події рівня `Error` і вище, щоденна ротація              |

Папка, назви файлів і кількість збережених файлів — у секції
`SerilogOptions`. Serilog також читає секцію `Serilog`
(`ReadFrom.Configuration`); її поки немає, тож діють рівні за
замовчуванням. Щоб змінити рівні, додайте секцію `Serilog` з
`MinimumLevel` в `appsettings*.json`.

Кожен HTTP-запит записується одним рядком (`UseSerilogRequestLogging`).
Рівень залежить від результату (`SerilogLogLevelResolver`): відповіді
4xx — `Warning`, 5xx і необроблені винятки — `Error`.

## Як писати логи в коді

Логи пишуться через методи, згенеровані атрибутом `[LoggerMessage]`, а не
через `logger.LogInformation(...)`: так немає зайвих алокацій, а кожна
подія має сталий ідентифікатор.

```csharp
public partial class CreateRoleCommandHandler(
    IRoleIdentityService identityService,
    ILogger<CreateRoleCommandHandler> logger) : IRequestHandler<CreateRoleCommand, Guid>
{
    public async Task<Guid> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        LogCreatingRole(logger, request.Name);
        // ...
    }

    [LoggerMessage(EventId = LogEventIds.CreatingRole, Level = LogLevel.Information,
        Message = "Initiating creation of role: {RoleName}")]
    private static partial void LogCreatingRole(ILogger logger, string roleName);
}
```

- Клас з такими методами — `partial`.
- Повідомлення англійською, з іменованими параметрами `{RoleName}`, а не
  інтерполяцією: параметри стають окремими полями структурованого логу.

## Ідентифікатори подій

Ідентифікатори зібрані в `Crm.Application/Common/Consts/LogEventIds.cs` і
згруповані за тисячами:

| Діапазон | Область                              |
| -------- | ------------------------------------ |
| 1000     | Ролі й кеш прав                      |
| 2000     | Користувачі й автентифікація         |
| 3000     | Організації                          |
| 4000     | Транзакції                           |
| 5000     | Безпека й токени                     |
| 6000     | Глобальні помилки й middleware       |
| 7000     | Зображення сторінки входу            |
| 8000     | Фонові служби                        |

Нова подія отримує наступний вільний номер у діапазоні своєї області.
