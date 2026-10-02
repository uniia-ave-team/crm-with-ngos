---
title: Автентифікація й права доступу
description: JWT, refresh- та invitation-токени, права AccessRight і їх перевірка.
sidebar:
  order: 3
---

## Автентифікація

API використовує JWT Bearer. Параметри — у секції `JwtOptions`.

| Токен      | Що це                                                         | Термін дії                    |
| ---------- | ------------------------------------------------------------- | ----------------------------- |
| Access     | JWT з ідентифікатором користувача, email і ідентифікаторами ролей (`role_id`) | `AccessTokenExpiryMinutes`    |
| Refresh    | Випадковий рядок, збережений у базі; обмінюється на нову пару токенів | `RefreshTokenExpiryDays`      |
| Invitation | JWT для реєстрації за запрошенням: організація й роль нового користувача | `InvitationTokenExpiryHours`  |

Прострочені refresh-токени періодично видаляє фонова служба
`ExpiredTokensCleanupService`.

## Права

Право — значення переліку `AccessRight` у `Crm.Domain`:

| Область                     | Права                                                                                     |
| --------------------------- | ----------------------------------------------------------------------------------------- |
| Користувачі                 | `ViewUser`, `CreateUser`, `UpdateUser`, `DisableUser`, `DeleteUser`, `AssignRoleToUser`, `ViewEmergencyContact`, `ViewCustomFields` |
| Ролі                        | `ManageRole`                                                                              |
| Організації                 | `ViewNgo`, `CreateNgo`, `UpdateNgo`                                                       |
| Зображення сторінки входу   | `ViewLoginPageImages`, `CreateLoginPageImages`, `DeleteLoginPageImages`                   |

- Права належать **ролям**, а не користувачам. Вони зберігаються як claims
  ролі типу `Permission` зі значенням `Permissions.{Назва}`, наприклад
  `Permissions.ViewUser` (`PermissionExtensions.ToClaimValue`).
- Користувач може мати кілька ролей; його права — об'єднання прав ролей.
- Системна роль `Admin` створюється під час першого запуску й має всі
  права. Її права, назву змінити й саму роль видалити через API не можна.

## Перевірка на ендпоінті

```csharp
[HttpDelete("{id:guid}")]
[HasAccessRight(AccessRight.DeleteUser)]
public async Task<IActionResult> DeleteUser(Guid id, CancellationToken cancellationToken)
```

- `[HasAccessRight(...)]` — це `[Authorize]` з політикою
  `Permissions.{Назва}`. Політики створюються на льоту
  (`AccessRightPolicyProvider`), реєструвати кожну окремо не потрібно.
- `AccessRightAuthorizationHandler` бере ідентифікатори ролей з токена й
  перевіряє через `IPermissionService`, чи є потрібне право серед
  об'єднаних прав усіх ролей користувача.
- Ендпоінти, доступні будь-якому автентифікованому користувачеві
  (наприклад, власний профіль `GET /api/v1/users/me`), позначені
  `[Authorize]`.
- У [довіднику API](../../api/) опис операції закінчується рядком
  «Required Permissions» з переліком потрібних прав
  (`AccessRightOperationTransformer`).

## Кеш прав

Щоб не звертатися до бази на кожен запит, права ролей зберігаються в
`IMemoryCache` (`RolePermissionsCache`, ключ `RoleAccessRights_{roleId}`).

- Кеш заповнюється для всіх ролей під час запуску.
- Команди, що змінюють права ролі або видаляють роль, оновлюють кеш.
- Якщо ролі в кеші немає, права читаються з бази й кешуються; у лог
  пишеться попередження про промах кешу.

:::caution
Кеш живе в пам'яті процесу. Якщо API колись працюватиме в кількох
екземплярах, зміна прав в одному з них не потрапить у кеш інших —
знадобиться розподілений кеш або інвалідація між екземплярами.
:::

## Нове право

1. Додати значення в `AccessRight` з наступним номером.
2. Позначити ендпоінт `[HasAccessRight(AccessRight.НовеПраво)]`.
3. Роль `Admin` отримає нове право під час наступного запуску:
   `AdminRoleSeeder` додає їй усі відсутні права. Іншим ролям право
   призначають через API ролей.

Наявні значення не перейменовують: у базі права зберігаються за назвою
(`Permissions.ViewUser`), тож після перейменування ролі втратять право.
