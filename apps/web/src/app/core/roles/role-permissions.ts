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
 * Чи може користувач надати групу прав іншим: лише якщо має **всі** її права (AND).
 * Об'єднана група (кілька кодів в одному тумблері) без хоча б одного права недоступна.
 */
export function canGrantGroup(group: PermissionGroup, own: readonly string[]): boolean {
  return group.permissions.every((permission) => own.includes(permission));
}

/**
 * Права без налаштування (внизу таблиці «Назви прав»), які мають усі: їх додають до
 * кожної нової ролі в будь-якому випадку. `CreateNgo` сюди не входить: воно лише в
 * першого користувача, а не в усіх.
 */
export const ALWAYS_GRANTED_PERMISSIONS: readonly string[] = ['Permissions.ViewNgo', 'Permissions.ViewTableTemplate'];

/**
 * Права нової ролі: усі права вибраних груп (у порядку груп) і права без налаштування,
 * без повторів.
 */
export function claimsOfGroups(enabledIds: ReadonlySet<string>): readonly string[] {
  const selected = PERMISSION_GROUPS.filter((group) => enabledIds.has(group.id)).flatMap((group) => group.permissions);
  return [...new Set([...selected, ...ALWAYS_GRANTED_PERMISSIONS])];
}

/** Які права додати ролі й які прибрати від неї, щоб вона відповідала вибору у формі. */
export interface ClaimsChange {
  readonly add: readonly string[];
  readonly remove: readonly string[];
}

/**
 * Різниця прав ролі при оновленні. `original` — права ролі з сервера, `permissions` — права
 * увімкнених груп із форми, `own` — права користувача. Зачіпаються лише групи, які користувач
 * може надати (є всі права групи, AND): група, увімкнена у формі, додає права, яких у ролі нема;
 * вимкнена прибирає ті свої права, що в ролі є (так виправляється й часткова група). Недоступні
 * користувачеві групи й права поза групами лишаються, як були. Права без налаштування
 * (`ALWAYS_GRANTED_PERMISSIONS`) додаються, якщо їх у ролі нема.
 */
export function diffClaims(original: readonly string[], permissions: readonly string[], own: readonly string[]): ClaimsChange {
  const add: string[] = [];
  const remove: string[] = [];
  for (const group of PERMISSION_GROUPS) {
    if (!canGrantGroup(group, own)) {
      continue;
    }
    if (group.permissions.every((permission) => permissions.includes(permission))) {
      add.push(...group.permissions.filter((permission) => !original.includes(permission)));
    } else {
      remove.push(...group.permissions.filter((permission) => original.includes(permission)));
    }
  }
  add.push(...ALWAYS_GRANTED_PERMISSIONS.filter((permission) => !original.includes(permission) && !add.includes(permission)));
  return { add, remove };
}

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
