import { Component, computed, input, output, signal } from '@angular/core';

import { RolesView } from '../roles-panel/roles-panel.component';

/** Дані нового колеги з форми. */
export interface NewColleague {
  readonly email: string;
  /** `id` вибраних ролей (в порядку списку ролей). */
  readonly roleIds: readonly string[];
}

/**
 * Форма нового колеги (зміст бічного вікна після «Додати»): пошта й ролі (обидва
 * обовʼязкові: сервер вимагає хоча б одну роль), ролі вибирають плашками (`app-role-pill`: вибрана — заливка й `checkmark-circle`,
 * невибрана — рамка й порожнє коло `ellipse-outline`).
 * Ролі приходять входом `roles` (стан запиту, як у вікні «Ролі»). «Додати»
 * перевіряє пошту й роль і віддає дані подією `save`; сама нічого не надсилає. Поки
 * іде відправка (`saving`), кнопка вимкнена; помилка відправки (`error`) стоїть над
 * кнопкою; `reset()` очищає форму.
 */
@Component({
  selector: 'app-colleague-form',
  templateUrl: './colleague-form.component.html',
  styleUrls: ['../../settings/settings-section.scss', './colleague-form.component.scss'],
  standalone: false,
})
export class ColleagueFormComponent {
  readonly roles = input.required<RolesView>();
  /** Іде відправка: кнопка вимкнена, повторне натискання нічого не робить. */
  readonly saving = input(false);
  /** Ключ у мовному файлі помилки відправки (показується над кнопкою); `null` — без помилки. */
  readonly error = input<string | null>(null);
  /** Дані з форми готові до відправки (пошту заповнено, роль вибрано). */
  readonly save = output<NewColleague>();

  protected readonly email = signal('');
  /** Вибрані ролі (`id`). */
  protected readonly selected = signal<ReadonlySet<string>>(new Set());
  /** Була спроба відправки: помилку пошти показуємо лише після неї. */
  private readonly attempted = signal(false);

  protected readonly emailError = computed(() => {
    if (!this.attempted()) {
      return null;
    }
    const email = this.email().trim();
    if (!email) {
      return 'colleagues.colleagueForm.error.emailRequired';
    }
    return EMAIL_PATTERN.test(email) ? null : 'colleagues.colleagueForm.error.emailInvalid';
  });

  /** Помилка ролей: після спроби відправки без жодної вибраної ролі. */
  protected readonly rolesError = computed(() =>
    this.attempted() && this.selected().size === 0 ? 'colleagues.colleagueForm.error.rolesRequired' : null,
  );

  /** Очищає форму: порожня пошта, жодної ролі, без помилок (після успішної відправки). */
  reset(): void {
    this.email.set('');
    this.selected.set(new Set());
    this.attempted.set(false);
  }

  protected onEmailInput(event: Event): void {
    this.email.set((event.target as HTMLInputElement).value);
  }

  protected toggle(id: string): void {
    const selected = new Set(this.selected());
    if (!selected.delete(id)) {
      selected.add(id);
    }
    this.selected.set(selected);
  }

  protected submit(): void {
    if (this.saving()) {
      return;
    }
    this.attempted.set(true);
    if (this.emailError() || this.rolesError()) {
      return;
    }
    this.save.emit({
      email: this.email().trim(),
      roleIds: this.roles().roles.filter((role) => this.selected().has(role.id)).map((role) => role.id),
    });
  }
}

/** Мінімальна перевірка пошти: щось, `@`, щось, крапка, щось (решту перевіряє сервер). */
const EMAIL_PATTERN = /^\S+@\S+\.\S+$/;
