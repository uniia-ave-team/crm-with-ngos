import { PERMISSIONS, Permission } from '../auth/permissions';

/** Група прав, яку в ролі вмикають одним перемикачем (`id` — ключ назви в мовному файлі). */
export interface PermissionGroup {
  readonly id: string;
  /** Права групи (значення `PERMISSIONS`). */
  readonly permissions: readonly Permission[];
  /**
   * Пункт, від якого цей залежить (`id` групи): без нього цей пункт вимкнений і не входить у
   * права ролі. У формі він показаний підпунктом одразу під своїм.
   */
  readonly parent?: string;
}

const groupOf = (id: string, ...permissions: Permission[]): PermissionGroup => ({ id, permissions });
const subgroupOf = (parent: string, id: string, ...permissions: Permission[]): PermissionGroup => ({ id, permissions, parent });

/**
 * Права, які вибирають у ролі, у порядку форми: кожна група складається із загальних прав
 * (`PERMISSIONS`). Права, що є в усіх чи видаються автоматично (`CreateNgo`,
 * `ViewTableTemplate`), сюди не входять.
 */
export const PERMISSION_GROUPS: readonly PermissionGroup[] = [
  // Усе, що стосується колег, — підпункти перегляду колег: без нього не відкрити сторінку колег
  // (там і ролі), тож решта прав без нього нічого не дає.
  groupOf('viewUsers', PERMISSIONS.viewUser),
  subgroupOf('viewUsers', 'viewEmergencyContacts', PERMISSIONS.viewEmergencyContact),
  subgroupOf('viewUsers', 'viewCustomFields', PERMISSIONS.viewCustomFields),
  subgroupOf('viewUsers', 'editUsers', PERMISSIONS.createUser, PERMISSIONS.updateUser),
  subgroupOf('viewUsers', 'resetPasswords', PERMISSIONS.canResetPassword),
  subgroupOf('viewUsers', 'disableUsers', PERMISSIONS.disableUser, PERMISSIONS.deleteUser),
  subgroupOf('viewUsers', 'manageRoles', PERMISSIONS.manageRole, PERMISSIONS.assignRoleToUser),
  groupOf(
    'editNgo',
    PERMISSIONS.updateNgo,
    PERMISSIONS.viewLoginPageImages,
    PERMISSIONS.createLoginPageImages,
    PERMISSIONS.deleteLoginPageImages,
    PERMISSIONS.createTableTemplate,
    PERMISSIONS.updateTableTemplate,
    PERMISSIONS.deleteTableTemplate,
  ),
  // Перегляд таблиць — і самі таблиці, і їхні записи: без `ViewCustomRecord` сервер не віддає записів
  // (`GET /api/v1/customrecords`), і таблиця була б лише рядком заголовків.
  groupOf('viewTables', PERMISSIONS.viewCustomTable, PERMISSIONS.viewCustomRecord),
  // Керувати таблицями без їх перегляду нема як.
  subgroupOf(
    'viewTables',
    'manageTables',
    PERMISSIONS.createCustomTable,
    PERMISSIONS.updateCustomTable,
    PERMISSIONS.deleteCustomTable,
    PERMISSIONS.createCustomRecord,
    PERMISSIONS.updateCustomRecord,
    PERMISSIONS.deleteCustomRecord,
  ),
  // Доступ до планера — проєкти й завдання в них; керувати без доступу нема як.
  groupOf('accessPlanner', PERMISSIONS.viewProject, PERMISSIONS.viewTask),
  subgroupOf('accessPlanner', 'manageProjects', PERMISSIONS.createProject, PERMISSIONS.updateProject, PERMISSIONS.deleteProject),
  subgroupOf('accessPlanner', 'manageTasks', PERMISSIONS.createTask, PERMISSIONS.updateTask, PERMISSIONS.deleteTask),
];

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
 * першого користувача, а не в усіх. Організацію бачить кожен, хто ввійшов, без окремого
 * права (`ViewNgo` сервер прибрав і відхиляє).
 */
export const ALWAYS_GRANTED_PERMISSIONS: readonly Permission[] = [PERMISSIONS.viewTableTemplate];

/**
 * Незадіяні права: інтерфейс на них ще ніде не спирається, тож у формі ролі перемикача нема й з
 * інтерфейсу їх ролі не дати. Коли функцію зроблять — право переходить у `PERMISSION_GROUPS`.
 */
export const UNUSED_PERMISSIONS: readonly Permission[] = [PERMISSIONS.manageUserActionTokens];

/** Чи діє група за вибором `enabledIds`: її ввімкнено, а якщо вона підпункт — то й пункт, від якого вона залежить. */
export function groupActive(group: PermissionGroup, enabledIds: ReadonlySet<string>): boolean {
  return enabledIds.has(group.id) && (!group.parent || enabledIds.has(group.parent));
}

/**
 * Права ролі за вибором у формі: усі права груп, що діють (`groupActive`, у порядку груп), і
 * права без налаштування, без повторів. Це те, що форма ролі віддає в `permissions`.
 */
export function claimsOfGroups(enabledIds: ReadonlySet<string>): readonly string[] {
  const selected = PERMISSION_GROUPS.filter((group) => groupActive(group, enabledIds)).flatMap((group) => group.permissions);
  return [...new Set([...selected, ...ALWAYS_GRANTED_PERMISSIONS])];
}

/** Які права додати ролі й які прибрати від неї, щоб вона відповідала вибору у формі. */
export interface ClaimsChange {
  readonly add: readonly string[];
  readonly remove: readonly string[];
}

/**
 * Різниця прав ролі при оновленні. `original` — права ролі з сервера, `permissions` — права
 * з форми (`claimsOfGroups`), `own` — права користувача. Зачіпаються лише групи, які користувач
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
