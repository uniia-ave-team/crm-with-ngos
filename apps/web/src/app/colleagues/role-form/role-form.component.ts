import { Component, OnInit, computed, inject, input, output, signal } from '@angular/core';

import { PERMISSION_GROUPS, canGrantGroup } from '../../core/roles/role-permissions';
import { ROLE_NAME_MAX_LENGTH } from '../../core/roles/role-rules';
import { Role, RoleDetails } from '../../core/roles/roles.service';
import { CurrentUserService } from '../../core/users/current-user.service';

/** Дані нової ролі з форми: порожні необовʼязкові поля — `null`. */
export interface NewRole {
  readonly name: string;
  readonly feminitiveName: string | null;
  readonly pluralName: string | null;
  /** Права вибраних груп (у порядку груп, без повторів); без вибору — порожньо. */
  readonly permissions: readonly string[];
}

/**
 * Форма ролі (зміст бічного вікна «Ролі» після «Додати роль» чи «Редагувати»): назва
 * (обовʼязкова), фемінітив і множина та перемикачі прав (`PERMISSION_GROUPS`; вибрані
 * групи потрапляють у `save` правами `permissions`). Користувач не може надати права,
 * яких не має сам: перемикач групи доступний, лише якщо є **всі** її права (AND),
 * інакше він вимкнений. У формі редагування (`role` з `claims`) перемикач групи
 * увімкнений, коли в ролі є **всі** права групи; часткова група (є лише частина
 * прав) лишається вимкненою й позначається іконкою-попередженням із поясненням,
 * що проблему виправить оновлення ролі; недоступні користувачеві групи показують
 * стан ролі, але змінити його не можна. «Додати» перевіряє назву й
 * віддає дані подією `save`; сама нічого не надсилає. Без входу `role` це форма
 * нової ролі («Додати»), з ним — форма редагування: поля заповнені назвами цієї
 * ролі, перемикачі прав за її `claims`, заголовок «Редагування ролі», кнопка «Зберегти».
 */
@Component({
  selector: 'app-role-form',
  templateUrl: './role-form.component.html',
  styleUrls: ['../../settings/settings-section.scss', './role-form.component.scss'],
  standalone: false,
})
export class RoleFormComponent implements OnInit {
  /** Роль, яку редагують (початкові значення полів); без неї — нова роль. */
  readonly role = input<Role | RoleDetails | null>(null);
  /** Іде відправка: кнопка вимкнена, повторне натискання нічого не робить. */
  readonly saving = input(false);
  /** Ключ у мовному файлі помилки відправки (показується над кнопкою); `null` — без помилки. */
  readonly error = input<string | null>(null);
  /** Дані з форми готові до відправки (назву заповнено). */
  readonly save = output<NewRole>();

  protected readonly maxLength = ROLE_NAME_MAX_LENGTH;
  protected readonly permissionGroups = PERMISSION_GROUPS;
  private readonly ownPermissions = inject(CurrentUserService).permissions;
  /** `id` груп, які користувач може надати: у нього є всі їхні права. */
  protected readonly grantable = computed<ReadonlySet<string>>(
    () => new Set(PERMISSION_GROUPS.filter((group) => canGrantGroup(group, this.ownPermissions())).map((group) => group.id)),
  );

  protected readonly name = signal('');
  protected readonly feminitiveName = signal('');
  protected readonly pluralName = signal('');
  /** Групи, у яких роль має лише частину прав (`id` групи): проблема ролі, її виправить оновлення. */
  protected readonly partialGroups = signal<ReadonlySet<string>>(new Set());
  /** Увімкнені групи прав (`id` групи). */
  protected readonly enabledGroups = signal<ReadonlySet<string>>(new Set());
  /** Була спроба відправки: помилку назви показуємо лише після неї. */
  private readonly attempted = signal(false);

  protected readonly nameError = computed(() =>
    this.attempted() && !this.name().trim() ? 'colleagues.roleForm.error.nameRequired' : null,
  );

  ngOnInit(): void {
    const role = this.role();
    if (role) {
      this.name.set(role.name);
      this.feminitiveName.set(role.feminitiveName ?? '');
      this.pluralName.set(role.pluralName ?? '');
      const claims = 'claims' in role ? role.claims : [];
      const has = (group: (typeof PERMISSION_GROUPS)[number]) => group.permissions.filter((permission) => claims.includes(permission)).length;
      this.enabledGroups.set(new Set(PERMISSION_GROUPS.filter((group) => has(group) === group.permissions.length).map((group) => group.id)));
      this.partialGroups.set(
        new Set(PERMISSION_GROUPS.filter((group) => has(group) > 0 && has(group) < group.permissions.length).map((group) => group.id)),
      );
    }
  }

  protected onInput(field: { set(value: string): void }, event: Event): void {
    field.set((event.target as HTMLInputElement).value);
  }

  protected togglePermission(id: string, event: Event): void {
    const checkbox = event.target as HTMLInputElement;
    if (!this.grantable().has(id)) {
      // Права, яких нема в самого користувача, змінити не можна, навіть обійшовши вимкнений
      // перемикач: він повертається до стану, який мав (вимкнений для нової ролі, як у ролі для існуючої).
      checkbox.checked = this.enabledGroups().has(id);
      return;
    }
    const enabled = new Set(this.enabledGroups());
    if (checkbox.checked) {
      enabled.add(id);
    } else {
      enabled.delete(id);
    }
    this.enabledGroups.set(enabled);
  }

  protected submit(): void {
    if (this.saving()) {
      return;
    }
    this.attempted.set(true);
    if (!this.name().trim()) {
      return;
    }
    const nullable = (value: string) => value.trim() || null;
    this.save.emit({
      name: this.name().trim(),
      feminitiveName: nullable(this.feminitiveName()),
      pluralName: nullable(this.pluralName()),
      permissions: PERMISSION_GROUPS.filter((group) => this.enabledGroups().has(group.id)).flatMap((group) => group.permissions),
    });
  }
}
