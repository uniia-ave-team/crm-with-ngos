import { Component, computed, inject, signal, viewChild } from '@angular/core';

import { UsersService } from '../../core/users/users.service';
import { PasswordFieldsComponent } from '../../shared/password-fields/password-fields.component';
import { isStrongPassword } from '../../shared/password-fields/password-rules';

/** Що показати в блоці над формою за результатом останньої спроби. */
type PasswordResult = 'success' | 'failed' | 'unreachable' | 'error';

const ERROR_PREFIX = 'settings.password.error.';

/**
 * Вкладка «Зміна паролю» (`/settings/password`): поточний пароль, новий, його
 * повтор і кнопка. Перевірка перед запитом — на льоту після першої спроби
 * (як у формі адміністратора на сторінці входу); сервер лишається головним, а
 * клієнт лише швидко підказує.
 */
@Component({
  selector: 'app-settings-password',
  templateUrl: './password.page.html',
  styleUrls: ['../settings-section.scss', './password.page.scss'],
  standalone: false,
})
export class PasswordPage {
  private readonly users = inject(UsersService);
  private readonly passwordFields = viewChild(PasswordFieldsComponent);

  protected readonly currentPassword = signal('');
  protected readonly newPassword = signal('');
  protected readonly repeatPassword = signal('');

  /** Була спроба зміни: помилки полів показуємо лише після неї. */
  private readonly attempted = signal(false);
  protected readonly saving = signal(false);
  /** Результат останнього запиту (блок над формою); `null` — ще не було. */
  protected readonly result = signal<PasswordResult | null>(null);

  /** Ключі помилок під полями (`null` — помилки нема). */
  protected readonly currentError = computed(() =>
    this.attempted() && !this.currentPassword() ? `${ERROR_PREFIX}currentRequired` : null,
  );

  protected readonly newPasswordError = computed(() => {
    if (!this.attempted()) {
      return null;
    }
    const password = this.newPassword();
    if (!password) {
      return `${ERROR_PREFIX}passwordRequired`;
    }
    if (!isStrongPassword(password)) {
      return `${ERROR_PREFIX}passwordWeak`;
    }
    return password === this.currentPassword() ? `${ERROR_PREFIX}sameAsCurrent` : null;
  });

  protected readonly repeatError = computed(() => {
    if (!this.attempted()) {
      return null;
    }
    const repeat = this.repeatPassword();
    if (!repeat) {
      return `${ERROR_PREFIX}passwordRepeatRequired`;
    }
    return repeat === this.newPassword() ? null : `${ERROR_PREFIX}passwordMismatch`;
  });

  /** Ключ тексту блока над формою. */
  protected readonly resultKey = computed(() => {
    const result = this.result();
    return result ? `settings.password.result.${result}` : null;
  });

  /** Зміна будь-якого поля: попередній результат уже не про те, що в полях. */
  protected onFieldChange(field: { set(value: string): void }, value: string): void {
    field.set(value);
    this.result.set(null);
  }

  protected async submit(): Promise<void> {
    if (this.saving()) {
      return;
    }
    this.attempted.set(true);
    this.result.set(null);
    if (this.currentError() || this.newPasswordError() || this.repeatError()) {
      return;
    }

    this.saving.set(true);
    const outcome = await this.users.changePassword(this.currentPassword(), this.newPassword());
    this.saving.set(false);

    if (outcome === 'changed') {
      this.currentPassword.set('');
      this.newPassword.set('');
      this.repeatPassword.set('');
      // Поля порожні, але користувач їх торкався: без скидання вони червоніють.
      this.passwordFields()?.resetState();
      this.attempted.set(false);
      this.result.set('success');
      return;
    }
    // Сервер не відрізняє «поточний пароль невірний» від іншого відхилення
    // (400); 401, що дійшов сюди, — теж відмова за цими даними.
    this.result.set(
      outcome === 'unreachable' ? 'unreachable' : outcome === 'error' ? 'error' : 'failed',
    );
  }
}
