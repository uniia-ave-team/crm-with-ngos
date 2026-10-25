/**
 * Права користувача так, як їх віддає `GET /api/v1/users/me/permissions`
 * (значення `AccessRight` сервера з префіксом `Permissions.`). Тут лише ті,
 * за якими клієнт щось показує чи ховає.
 */
export const PERMISSIONS = {
  viewUser: 'Permissions.ViewUser',
  createUser: 'Permissions.CreateUser',
  updateUser: 'Permissions.UpdateUser',
  disableUser: 'Permissions.DisableUser',
  deleteUser: 'Permissions.DeleteUser',
  assignRoleToUser: 'Permissions.AssignRoleToUser',
  canResetPassword: 'Permissions.CanResetPassword',
  manageRole: 'Permissions.ManageRole',
  viewEmergencyContact: 'Permissions.ViewEmergencyContact',
  viewCustomFields: 'Permissions.ViewCustomFields',
  updateNgo: 'Permissions.UpdateNgo',
  viewLoginPageImages: 'Permissions.ViewLoginPageImages',
  createLoginPageImages: 'Permissions.CreateLoginPageImages',
  deleteLoginPageImages: 'Permissions.DeleteLoginPageImages',
} as const;

/** Права сторінки адміністрування: достатньо **будь-якого** одного з них. */
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

/** Чи є в `granted` хоча б одне з прав `required` (порожній `required` — не підходить ніхто). */
export function hasAnyPermission(granted: readonly string[], required: readonly string[]): boolean {
  return required.some((permission) => granted.includes(permission));
}
