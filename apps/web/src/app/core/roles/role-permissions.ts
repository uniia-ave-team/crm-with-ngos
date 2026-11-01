/** Група прав, яку в ролі вмикають одним перемикачем (`id` — ключ назви в мовному файлі). */
export interface PermissionGroup {
  readonly id: string;
  /** Права групи (значення `AccessRight` сервера з префіксом `Permissions.`). */
  readonly permissions: readonly string[];
}

const group = (id: string, ...rights: string[]): PermissionGroup => ({
  id,
  permissions: rights.map((right) => `Permissions.${right}`),
});

/**
 * Права, які вибирають у ролі, у порядку форми. Права, що є в усіх чи
 * видаються автоматично (`ViewNgo`, `CreateNgo`, `ViewTableTemplate`), сюди не
 * входять.
 */
export const PERMISSION_GROUPS: readonly PermissionGroup[] = [
  group('viewUsers', 'ViewUser'),
  group('editUsers', 'CreateUser', 'UpdateUser'),
  group('disableUsers', 'DisableUser', 'DeleteUser'),
  group('resetPasswords', 'CanResetPassword'),
  group('manageRoles', 'ManageRole', 'AssignRoleToUser'),
  group('viewCustomFields', 'ViewCustomFields'),
  group('viewEmergencyContacts', 'ViewEmergencyContact'),
  group(
    'editNgo',
    'UpdateNgo',
    'ViewLoginPageImages',
    'CreateLoginPageImages',
    'DeleteLoginPageImages',
    'CreateTableTemplate',
    'UpdateTableTemplate',
    'DeleteTableTemplate',
  ),
  group('viewTables', 'ViewCustomTable'),
  group(
    'manageTables',
    'CreateCustomTable',
    'UpdateCustomTable',
    'DeleteCustomTable',
    'ViewCustomRecord',
    'CreateCustomRecord',
    'UpdateCustomRecord',
    'DeleteCustomRecord',
  ),
];
