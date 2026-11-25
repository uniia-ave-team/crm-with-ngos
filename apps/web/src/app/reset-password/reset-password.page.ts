import { Component, computed, inject, input, signal, WritableSignal } from '@angular/core';
import { Router } from '@angular/router';
import { IonButton, IonContent } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

import { RELOGIN_URL } from '../core/auth/auth.service';
import { PasswordPolicyService } from '../core/auth/password-policy.service';
import { ConnectionService } from '../core/connection/connection.service';
import { RequestFailure } from '../core/http/request-failure';
import { SuccessNoticeService } from '../core/notice/success-notice.service';
import { UsersService } from '../core/users/users.service';
import { isPasswordTooLong, isStrongPassword } from '../shared/password-fields/password-rules';
import { PasswordFieldsComponent } from '../shared/password-fields/password-fields.component';

/**
 * Сторінка нового пароля за посиланням (`/reset-password?token=…`): колега переходить за
 * посиланням із вікна «Посилання для скидання пароля створено» й задає новий пароль. Без стражів
 * і без бічного меню, вигляд — картка, як на `/join` (стилі спільні з нею). Сервер — той, на домен
 * якого веде посилання (`ConnectionService.pageServerUrl`), а не сервер активного акаунта.
 *
 * Форма: «Пароль» і «Повторити пароль» (`app-password-fields`, умови сервера, щонайбільше 128
 * символів), «Відправити» — `POST /api/v1/users/reset-password` з токеном і паролем; успіх веде на
 * `/relogin` з плашкою «Пароль змінено…» (`SuccessNoticeService`). Невдача (чи посилання без токена) замінює форму повідомленням за причиною й кнопкою
 * «Назад» (на `/relogin`). «Скасувати» — теж на `/relogin`; той, у кого в браузері нема
 * збережених акаунтів, звідти потрапляє на `/login`.
 */
@Component({
  selector: 'app-reset-password',
  templateUrl: './reset-password.page.html',
  styleUrls: ['../join/join.page.scss', './reset-password.page.scss'],
  imports: [IonButton, IonContent, PasswordFieldsComponent, TranslatePipe],
})
export class ResetPasswordPage {
  private readonly connection = inject(ConnectionService);
  private readonly users = inject(UsersService);
  private readonly router = inject(Router);
  private readonly successNotice = inject(SuccessNoticeService);

  /** Умови до пароля сервера, на якому задають пароль (`PasswordPolicyService`). */
  protected readonly passwordPolicy = inject(PasswordPolicyService).policy(this.connection.pageServerUrl);

  /** Токен із query-параметра `?token=…` (прив'язується роутером через bindToComponentInputs). */
  readonly token = input<string | undefined>();

  protected readonly password = signal('');
  protected readonly passwordRepeat = signal('');
  protected readonly saving = signal(false);
  private readonly submitted = signal(false);
  /** Невдача запиту; поки її нема — форма. */
  private readonly sendFailure = signal<RequestFailure | null>(null);

  /** Ключ повідомлення замість форми: невдача запиту чи посилання без токена; `null` — форма. */
  protected readonly failureKey = computed(() => {
    const failure = this.sendFailure() ?? (this.token() ? null : 'unauthorized');
    return failure ? `resetPassword.failure.${failure}` : null;
  });

  // Помилки показуємо лише після спроби надіслати форму; `null` — все гаразд.
  protected readonly passwordError = computed(() => {
    if (!this.submitted()) {
      return null;
    }
    const password = this.password();
    if (!password) {
      return 'passwordRequired';
    }
    if (isPasswordTooLong(password)) {
      return 'passwordTooLong';
    }
    return isStrongPassword(password, this.passwordPolicy()) ? null : 'passwordWeak';
  });
  protected readonly passwordRepeatError = computed(() => {
    if (!this.submitted()) {
      return null;
    }
    const repeat = this.passwordRepeat();
    if (!repeat) {
      return 'passwordRepeatRequired';
    }
    return repeat === this.password() ? null : 'passwordMismatch';
  });

  protected onFieldChange(field: WritableSignal<string>, value: string | number | null | undefined): void {
    field.set(value == null ? '' : String(value));
  }

  /** «Відправити»: перевіряє поля й, якщо все гаразд, задає новий пароль; успіх — на `/relogin`. */
  protected async submit(): Promise<void> {
    if (this.saving()) {
      return;
    }
    this.submitted.set(true);
    if (this.passwordError() || this.passwordRepeatError()) {
      return;
    }
    this.saving.set(true);
    const result = await this.users.resetPassword(this.connection.pageServerUrl, this.token() ?? '', this.password());
    this.saving.set(false);
    if (result !== 'reset') {
      this.sendFailure.set(result);
      return;
    }
    this.successNotice.show('resetPassword.done');
    void this.router.navigateByUrl(RELOGIN_URL);
  }

  /** «Скасувати» чи «Назад»: на `/relogin`. */
  protected leave(): void {
    void this.router.navigateByUrl(RELOGIN_URL);
  }
}
