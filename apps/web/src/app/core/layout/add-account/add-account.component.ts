import { Component, computed, inject, signal, viewChild } from '@angular/core';
import { Router } from '@angular/router';

import { ModalComponent } from '../../../shared/modal/modal.component';
import { AccountsService } from '../../accounts/accounts.service';
import { AuthService, REDIRECT_URL } from '../../auth/auth.service';
import { ConnectionService, parseServerAddress } from '../../connection/connection.service';
import { RequestFailure } from '../../http/request-failure';

const EMAIL_REQUIRED = 'login.form.error.emailRequired';
const PASSWORD_REQUIRED = 'login.form.error.passwordRequired';

/** Помилка над формою: ключ перекладу й параметри (`{{url}}` у тексті про недоступний сервер). */
interface BannerError {
  readonly key: string;
  readonly params?: { readonly url: string };
}

/**
 * Вікно «Додати акаунт» (відкривається кнопкою «+» у меню акаунтів): адреса сервера
 * (початково поточна або передана в `open`, її можна змінити), пошта й пароль. Усі тексти — зі сторінки входу.
 *
 *   <app-add-account #addAccount />
 *   addAccount.open();                                  // порожня пошта
 *   addAccount.open('a@organization.org');              // пошта вже вписана
 *   addAccount.open('a@organization.org', account.id);  // те саме; а якщо пошта входу збігається
 *                                                       // з поштою акаунта — він перезаписується
 *   addAccount.open(account.email, account.id, account.server); // ще й адреса сервера акаунта
 *
 * «Ввійти» перевіряє сервер за адресою (`GET /health`), входить на ньому
 * (`POST /api/v1/auth/login`) і додає акаунт до збережених як активний (або перезаписує акаунт
 * за переданим ключем, якщо пошта збіглася); тоді вікно
 * закривається, підключення перевіряється вже на сервері нового акаунта, а застосунок
 * переходить на `REDIRECT_URL`. Помилка лишає вікно відкритим, щоб надіслати форму ще раз.
 * «Забули пароль?» поки лише на місці (як на сторінці входу).
 */
@Component({
  selector: 'app-add-account',
  templateUrl: './add-account.component.html',
  styleUrls: ['./add-account.component.scss'],
  standalone: false,
})
export class AddAccountComponent {
  private readonly connection = inject(ConnectionService);
  private readonly accounts = inject(AccountsService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  private readonly modal = viewChild.required(ModalComponent);
  /** Ключ акаунта, який вхід може перезаписати (`open(…, accountId)`). */
  private accountId: string | undefined;

  protected readonly address = signal('');
  protected readonly email = signal('');
  protected readonly password = signal('');
  protected readonly saving = signal(false);
  private readonly submitted = signal(false);
  protected readonly banner = signal<BannerError | null>(null);

  // Помилки полів показуємо лише після спроби надіслати форму; `null` — все гаразд.
  protected readonly addressError = computed(() =>
    this.submitted() && !parseServerAddress(this.address()) ? 'login.step1.error.invalid' : null,
  );
  protected readonly emailError = computed(() => (this.submitted() && !this.email().trim() ? EMAIL_REQUIRED : null));
  protected readonly passwordError = computed(() => (this.submitted() && !this.password() ? PASSWORD_REQUIRED : null));

  /**
   * Відкриває вікно: пароль порожній, помилок нема. Необовʼязково:
   * - `email` одразу вписується в поле пошти;
   * - `server` вписується в поле адреси замість поточного сервера (як є: його можна змінити);
   * - `accountId` — ключ (`id`) збереженого акаунта: якщо при вході пошта збігається з поштою
   *   цього акаунта, вхід перезаписує його дані, а не додає новий акаунт (так входять
   *   в акаунт, сесію якого завершено). Сам ключ нічого не підставляє у форму.
   */
  open(email?: string, accountId?: string, server?: string): void {
    this.accountId = accountId;
    this.address.set(server ?? this.connection.serverUrl());
    this.email.set(email ?? '');
    this.password.set('');
    this.submitted.set(false);
    this.banner.set(null);
    this.saving.set(false);
    this.modal().open();
  }

  /** Закриває вікно (проміс виконується, коли воно зникло). */
  close(): Promise<void> {
    return this.modal().close();
  }

  protected onFieldChange(field: { set(value: string): void }, value: string | number | null | undefined): void {
    field.set(value == null ? '' : String(value));
  }

  /**
   * Ключ акаунта, який цей вхід перезапише: переданий ключ вказує на збережений акаунт, і в
   * нього така сама пошта, як у тієї, з якою входять. Інакше — `undefined` (вхід додасть
   * акаунт або оновить той, що вже є для цього користувача на сервері).
   */
  private accountToReplace(email: string): string | undefined {
    const saved = this.accountId === undefined ? undefined : this.accounts.accounts().find((a) => a.id === this.accountId);
    return saved && sameEmail(saved.email, email) ? saved.id : undefined;
  }

  protected async submit(): Promise<void> {
    if (this.saving()) {
      return;
    }
    this.submitted.set(true);
    const server = parseServerAddress(this.address())?.origin;
    if (!server || this.emailError() || this.passwordError()) {
      return;
    }

    this.saving.set(true);
    this.banner.set(null);
    if (!(await this.connection.probeApi(server))) {
      this.saving.set(false);
      this.banner.set({ key: 'login.step1.error.unreachable', params: { url: server } });
      return;
    }

    const email = this.email().trim();
    const login = await this.auth.login(email, this.password(), server, this.accountToReplace(email));
    this.saving.set(false);
    if (login !== 'ok') {
      this.banner.set({ key: `login.form.error.${loginErrorKey(login)}` });
      return;
    }

    // Новий акаунт уже активний: підключення (стан API, статус системи) досі належить
    // попередньому серверу, тож перевіряємо його заново, а тоді відкриваємо першу сторінку.
    this.password.set('');
    await this.modal().close();
    await this.connection.checkApi();
    void this.router.navigateByUrl(REDIRECT_URL, { onSameUrlNavigation: 'reload' });
  }
}

function sameEmail(a: string, b: string): boolean {
  return a.trim().toLowerCase() === b.trim().toLowerCase();
}

function loginErrorKey(result: RequestFailure): string {
  switch (result) {
    case 'invalid':
      return 'invalid';
    case 'unauthorized':
      return 'unauthorized';
    case 'unreachable':
      return 'unreachable';
    default:
      return 'error';
  }
}
