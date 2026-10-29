import { Component, OnInit, computed, input, output, signal } from '@angular/core';

import { PERMISSION_GROUPS } from '../../core/roles/role-permissions';
import { ROLE_NAME_MAX_LENGTH } from '../../core/roles/role-rules';
import { Role } from '../../core/roles/roles.service';

/** Дані нової ролі з форми: порожні необовʼязкові поля — `null`. */
export interface NewRole {
  readonly name: string;
  readonly feminitiveName: string | null;
  readonly pluralName: string | null;
}

/**
 * Форма ролі (зміст бічного вікна «Ролі» після «Додати роль» чи «Редагувати»): назва
 * (обовʼязкова), фемінітив і множина та перемикачі прав (`PERMISSION_GROUPS`;
 * поки лише показуються, у `save` не потрапляють). «Додати» перевіряє назву й
 * віддає дані подією `save`; сама нічого не надсилає. Без входу `role` це форма
 * нової ролі («Додати»), з ним — форма редагування: поля заповнені назвами цієї
 * ролі, заголовок «Редагування ролі», кнопка «Зберегти» (тумблери прав поки не
 * заповнюються).
 */
@Component({
  selector: 'app-role-form',
  templateUrl: './role-form.component.html',
  styleUrls: ['../../settings/settings-section.scss', './role-form.component.scss'],
  standalone: false,
})
export class RoleFormComponent implements OnInit {
  /** Роль, яку редагують (початкові значення полів); без неї — нова роль. */
  readonly role = input<Role | null>(null);
  /** Дані з форми готові до відправки (назву заповнено). */
  readonly save = output<NewRole>();

  protected readonly maxLength = ROLE_NAME_MAX_LENGTH;
  protected readonly permissionGroups = PERMISSION_GROUPS;

  protected readonly name = signal('');
  protected readonly feminitiveName = signal('');
  protected readonly pluralName = signal('');
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
    }
  }

  protected onInput(field: { set(value: string): void }, event: Event): void {
    field.set((event.target as HTMLInputElement).value);
  }

  protected togglePermission(id: string, event: Event): void {
    const enabled = new Set(this.enabledGroups());
    if ((event.target as HTMLInputElement).checked) {
      enabled.add(id);
    } else {
      enabled.delete(id);
    }
    this.enabledGroups.set(enabled);
  }

  protected submit(): void {
    this.attempted.set(true);
    if (!this.name().trim()) {
      return;
    }
    const nullable = (value: string) => value.trim() || null;
    this.save.emit({
      name: this.name().trim(),
      feminitiveName: nullable(this.feminitiveName()),
      pluralName: nullable(this.pluralName()),
    });
  }
}
