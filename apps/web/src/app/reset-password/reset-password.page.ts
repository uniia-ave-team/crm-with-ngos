import { Component, computed, effect, inject, signal, untracked, WritableSignal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { IonButton, IonContent, IonInput } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

import { RELOGIN_URL } from '../core/auth/auth.service';
import { PasswordPolicyService } from '../core/auth/password-policy.service';
import { ConnectionService } from '../core/connection/connection.service';
import { RequestFailure } from '../core/http/request-failure';
import { SuccessNoticeService } from '../core/notice/success-notice.service';
import { UsersService } from '../core/users/users.service';
import { isPasswordTooLong, isStrongPassword } from '../shared/password-fields/password-rules';
import { PasswordFieldsComponent } from '../shared/password-fields/password-fields.component';
import { tokenFromFragment } from '../core/auth/link-token';

/**
 * Сторінка нового пароля за посиланням (`/reset-password#token=…`): колега переходить за
 * посиланням із вікна «Посилання для скидання пароля створено» й задає новий пароль. Без стражів
 * і без бічного меню, вигляд — картка, як на `/join` (стилі спільні з нею). Сервер — той, на домен
 * якого веде посилання (`ConnectionService.pageServerUrl`), а не сервер активного акаунта.
 *
 * Спершу — перевірка токена (`GET /api/v1/users/reset-password/validate`, «Перевіряємо посилання…»):
 * сервер відхилив токен (400: строк минув, ним уже скористалися чи він пошкоджений) або токена в
 * посиланні нема — замість форми «Посилання неактивне…» з «Назад»; інша невдача (сервер не відповів,
 * помилка сервера) — «Не вдалося перевірити посилання…» з «Спробувати ще раз».
 *
 * Форма: пошта акаунта, пароль якого скидають (з перевірки, змінити не можна — як на `/join`),
 * «Пароль» і «Повторити пароль» (`app-password-fields`, умови сервера, щонайбільше 128
 * символів), «Відправити» — `POST /api/v1/users/reset-password` з токеном і паролем; успіх веде на
 * `/relogin` з плашкою «Пароль змінено…» (`SuccessNoticeService`; без запису в історії, тож «Назад» не
 * повертає форму з використаним посиланням). Пароль, якого сервер не прийняв, — помилка під полем,
 * форма лишається. Інша невдача замінює форму повідомленням за причиною й кнопкою
 * «Назад» (на `/relogin`). «Скасувати» — теж на `/relogin`; той, у кого в браузері нема
 * збережених акаунтів, звідти потрапляє на `/login`.
 */
@Component({
  selector: 'app-reset-password',
  templateUrl: './reset-password.page.html',
  styleUrls: ['../join/join.page.scss', './reset-password.page.scss'],
  imports: [IonButton, IonContent, IonInput, PasswordFieldsComponent, TranslatePipe],
})
export class ResetPasswordPage {
  private readonly connection = inject(ConnectionService);
  private readonly users = inject(UsersService);
  private readonly router = inject(Router);
  private readonly successNotice = inject(SuccessNoticeService);

  /** Умови до пароля сервера, на якому задають пароль (`PasswordPolicyService`). */
  protected readonly passwordPolicy = inject(PasswordPolicyService).policy(this.connection.pageServerUrl);

  /** Фрагмент адреси сторінки (`#token=…`). */
  private readonly fragment = toSignal(inject(ActivatedRoute).fragment, { initialValue: null });
  /** Токен із фрагмента адреси (`#token=…`, див. `link-token.ts`). */
  protected readonly token = computed(() => tokenFromFragment(this.fragment()));

  /** Перевірка токена перед формою: іде, токен недійсний, не вдалася (можна повторити) чи пройшла. */
  protected readonly check = signal<'loading' | 'inactive' | 'failed' | 'valid'>('loading');
  /** Пошта акаунта, пароль якого скидають (з перевірки токена). */
  protected readonly email = signal('');
  /** Перевірка, що тече: відповідь на застарілу (інший токен) нічого не змінює. */
  private checkId = 0;

  protected readonly password = signal('');
  protected readonly passwordRepeat = signal('');
  protected readonly saving = signal(false);
  private readonly submitted = signal(false);
  /** Невдача запиту; поки її нема — форма. */
  private readonly sendFailure = signal<RequestFailure | null>(null);
  /** Сервер не прийняв пароль (400 через сам пароль): помилка під полем, форма лишається. */
  private readonly passwordRejected = signal(false);

  /** Ключ повідомлення замість форми: посилання неактивне чи надіслати не вдалося; `null` — форма. */
  protected readonly failureKey = computed(() => {
    if (this.check() === 'inactive') {
      return 'resetPassword.inactive';
    }
    const failure = this.sendFailure();
    return failure ? `resetPassword.failure.${failure}` : null;
  });

  constructor() {
    effect(() => {
      const token = this.token();
      untracked(() => void this.validate(token));
    });
  }

  /** «Спробувати ще раз» після невдалої перевірки: та сама перевірка заново. */
  protected retry(): void {
    void this.validate(this.token());
  }

  // Помилки показуємо лише після спроби надіслати форму; `null` — все гаразд.
  protected readonly passwordError = computed(() => {
    if (!this.submitted()) {
      return null;
    }
    const password = this.password();
    if (!password) {
      return 'passwordRequired';
    }
    if (this.passwordRejected()) {
      return 'passwordRejected';
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
    // Пароль змінили — відмова сервера стосувалася попереднього.
    if (field === this.password) {
      this.passwordRejected.set(false);
    }
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
    if (result === 'passwordRejected') {
      this.passwordRejected.set(true);
      return;
    }
    if (result !== 'reset') {
      this.sendFailure.set(result);
      return;
    }
    this.successNotice.show('resetPassword.done');
    // Без запису в історії: «Назад» не повертає форму з уже використаним посиланням.
    void this.router.navigateByUrl(RELOGIN_URL, { replaceUrl: true });
  }

  /** Перевіряє токен посилання й бере з відповіді пошту акаунта; без токена — «неактивне». */
  private async validate(token: string | undefined): Promise<void> {
    const id = ++this.checkId;
    if (!token) {
      this.check.set('inactive');
      return;
    }
    this.check.set('loading');
    const details = await this.users.validatePasswordReset(this.connection.pageServerUrl, token);
    if (id !== this.checkId) {
      return;
    }
    if (typeof details === 'string') {
      this.check.set(details === 'invalid' ? 'inactive' : 'failed');
      return;
    }
    this.email.set(details.email);
    this.check.set('valid');
  }

  /** «Скасувати» чи «Назад»: на `/relogin`. */
  protected leave(): void {
    void this.router.navigateByUrl(RELOGIN_URL);
  }
}
