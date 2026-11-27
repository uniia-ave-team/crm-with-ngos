/**
 * Усі права сервера так, як їх віддає `GET /api/v1/users/me/permissions` (значення
 * `AccessRight` з префіксом `Permissions.`, у порядку сервера). Єдине місце, де записано
 * назви прав: за ними інтерфейс щось показує чи ховає, і з них складаються групи прав у
 * формі ролі (`core/roles/role-permissions.ts`).
 */
export const PERMISSIONS = {
  viewUser: 'Permissions.ViewUser',
  createUser: 'Permissions.CreateUser',
  updateUser: 'Permissions.UpdateUser',
  disableUser: 'Permissions.DisableUser',
  deleteUser: 'Permissions.DeleteUser',
  assignRoleToUser: 'Permissions.AssignRoleToUser',
  viewEmergencyContact: 'Permissions.ViewEmergencyContact',
  viewCustomFields: 'Permissions.ViewCustomFields',
  canResetPassword: 'Permissions.CanResetPassword',
  manageRole: 'Permissions.ManageRole',
  createNgo: 'Permissions.CreateNgo',
  updateNgo: 'Permissions.UpdateNgo',
  viewLoginPageImages: 'Permissions.ViewLoginPageImages',
  createLoginPageImages: 'Permissions.CreateLoginPageImages',
  deleteLoginPageImages: 'Permissions.DeleteLoginPageImages',
  viewTableTemplate: 'Permissions.ViewTableTemplate',
  createTableTemplate: 'Permissions.CreateTableTemplate',
  updateTableTemplate: 'Permissions.UpdateTableTemplate',
  deleteTableTemplate: 'Permissions.DeleteTableTemplate',
  viewCustomTable: 'Permissions.ViewCustomTable',
  createCustomTable: 'Permissions.CreateCustomTable',
  updateCustomTable: 'Permissions.UpdateCustomTable',
  deleteCustomTable: 'Permissions.DeleteCustomTable',
  viewCustomRecord: 'Permissions.ViewCustomRecord',
  createCustomRecord: 'Permissions.CreateCustomRecord',
  updateCustomRecord: 'Permissions.UpdateCustomRecord',
  deleteCustomRecord: 'Permissions.DeleteCustomRecord',
} as const;

/** Назва одного права сервера (одне зі значень `PERMISSIONS`). */
export type Permission = (typeof PERMISSIONS)[keyof typeof PERMISSIONS];

/*
 * Списки нижче — «квитки на вхід»: з якими правами користувача пускає на сторінку чи показує
 * йому блок. Достатньо будь-якого одного права зі списку (`hasAnyPermission`).
 *
 * Це не перелік усіх прав, на які сторінка реагує. У список входять лише ті, без яких
 * сторінка чи блок не мають сенсу (наприклад, без `ViewUser` сервер не віддасть таблицю
 * колег). Решту прав сторінка перевіряє сама, кожне окремо, там, де воно потрібне: кнопки
 * «Додати» й «Ролі», дії в меню рядка, колонки таблиці (`colleagues.page.ts`), редагування
 * організації й дії в галереї (`admin-panel`).
 *
 * Хто читає списки:
 * - `ADMIN_PANEL_PERMISSIONS`, `COLLEAGUES_PERMISSIONS`, `KNOWLEDGE_BASE_PERMISSIONS` — пункт бічного меню
 *   (`primary-nav`: без права пункту нема) і страж маршруту (`requireAnyPermission` в
 *   `app.routes.ts`: без права на сторінку не пускає й за прямим посиланням);
 * - `GALLERY_PERMISSIONS` — форма організації (`ngo-form`): чи показувати галерею.
 */

/** Права для керування сторінкою адміністрування: достатньо **будь-якого** одного з них. */
export const ADMIN_PANEL_PERMISSIONS: readonly string[] = [
  PERMISSIONS.updateNgo,
  PERMISSIONS.createLoginPageImages,
  PERMISSIONS.deleteLoginPageImages,
];

/** Права галереї зображень сторінки входу: галерею видно, якщо є хоча б одне з них. */
export const GALLERY_PERMISSIONS: readonly string[] = [
  PERMISSIONS.createLoginPageImages,
  PERMISSIONS.deleteLoginPageImages,
];

/** Права вкладки «Колеги»: достатньо `ViewUser` (пункт меню й страж маршруту). */
export const COLLEAGUES_PERMISSIONS: readonly string[] = [PERMISSIONS.viewUser];

/**
 * Права «Бази знань»: пункт меню, страж маршруту й зміст сторінки. Поки в базі лише таблиці,
 * тож досить `ViewCustomTable`; зʼявляться сторінки й папки — сюди додадуться їхні права
 * перегляду (достатньо буде будь-якого).
 */
export const KNOWLEDGE_BASE_PERMISSIONS: readonly string[] = [PERMISSIONS.viewCustomTable];

/** Права менеджера шаблонів таблиць (кнопка в шапці бокового меню й страж маршруту). */
export const TABLE_TEMPLATES_PERMISSIONS: readonly string[] = [PERMISSIONS.viewTableTemplate];

/** Чи є в `granted` хоча б одне з прав `required` (порожній `required` — не підходить ніхто). */
export function hasAnyPermission(granted: readonly string[], required: readonly string[]): boolean {
  return required.some((permission) => granted.includes(permission));
}
