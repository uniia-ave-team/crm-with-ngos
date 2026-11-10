import { Component, DestroyRef, computed, effect, inject, input, linkedSignal, output, signal, untracked } from '@angular/core';
import { Router } from '@angular/router';

import { accountToReplace, loginErrorKey } from '../../core/accounts/account-login';
import { AccountsService } from '../../core/accounts/accounts.service';
import { AuthService, REDIRECT_URL } from '../../core/auth/auth.service';
import { ConnectionService, parseServerAddress } from '../../core/connection/connection.service';
import { DEFAULT_LOGIN_IMAGE, LoginPageImagesService } from '../../core/login-page-images/login-page-images.service';

const EMAIL_REQUIRED = 'login.form.error.emailRequired';
const PASSWORD_REQUIRED = 'login.form.error.passwordRequired';
/** Пауза після останнього символу в адресі, перед перевіркою сервера. */
const CHECK_DELAY_MS = 500;

/** Що відомо про сервер за адресою у формі. */
type ServerState = 'checking' | 'ok' | 'unavailable' | 'invalid';

/** Помилка над формою: ключ перекладу. */
interface BannerError {
  readonly key: string;
}

/**
 * Форма входу на сторінці повторного входу (`/relogin`): «Назад», заголовок «Вхід в акаунт» зі
 * смітником (лише для збереженого акаунта), адреса сервера, пошта, пароль, «Ввійти» й «Забули пароль?».
 *
 * - Адреса й пошта початково з акаунта (`server`, `email`) або порожня пошта й адреса сторінки
 *   (для нового акаунта). При кожній зміні адреси (після паузи, поки друкують) перевіряється
 *   сервер (`GET /health`), і лише якщо він здоровий, з нього береться зображення збоку
 *   (`imageChange`); інакше під полем пишеться, що сервер недоступний, а зображення стандартне.
 * - «Ввійти» активна лише коли сервер здоровий. Вхід (`POST /api/v1/auth/login`) оновлює акаунт
 *   (якщо пошта збіглася з поштою акаунта) чи додає новий, робить його активним і відкриває
 *   `REDIRECT_URL`; помилка лишає форму з повідомленням над нею.
 * - Смітник просить сторінку забути акаунт (`remove`), «Назад» — повернутись до списку (`back`).
 */
@Component({
  selector: 'app-relogin-form',
  templateUrl: './relogin-form.component.html',
  styleUrls: ['./relogin-form.component.scss'],
  standalone: false,
})
export class ReloginFormComponent {
  private readonly connection = inject(ConnectionService);
  private readonly accounts = inject(AccountsService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly images = inject(LoginPageImagesService);

  /** Ключ акаунта, у який входять знову; `null` — додається новий (смітника нема). */
  readonly accountId = input<string | null>(null);
  /** Адреса сервера, вписана спочатку. */
  readonly initialServer = input('');
  /** Пошта, вписана спочатку. */
  readonly initialEmail = input('');

  readonly back = output<void>();
  readonly remove = output<void>();
  /** Зображення збоку: з сервера за адресою або стандартне. */
  readonly imageChange = output<string>();

  protected readonly address = linkedSignal(() => this.initialServer());
  protected readonly email = linkedSignal(() => this.initialEmail());
  protected readonly password = signal('');
  protected readonly saving = signal(false);
  protected readonly banner = signal<BannerError | null>(null);
  private readonly submitted = signal(false);

  protected readonly serverState = signal<ServerState>('checking');
  /** Origin сервера, якщо адресу вдалося розібрати. */
  private readonly origin = computed(() => parseServerAddress(this.address())?.origin ?? null);

  /** Помилка під полем адреси: сервер недоступний або адреса не схожа на адресу. */
  protected readonly addressError = computed<{ key: string; params?: { url: string } } | null>(() => {
    const state = this.serverState();
    if (state === 'invalid') {
      return { key: 'login.step1.error.invalid' };
    }
    return state === 'unavailable' ? { key: 'login.step1.error.unreachable', params: { url: this.origin() ?? '' } } : null;
  });
  protected readonly emailError = computed(() => (this.submitted() && !this.email().trim() ? EMAIL_REQUIRED : null));
  protected readonly passwordError = computed(() => (this.submitted() && !this.password() ? PASSWORD_REQUIRED : null));

  private timer: ReturnType<typeof setTimeout> | null = null;
  /** Номер останньої перевірки: відповідь на попередню адресу відкидається. */
  private checkId = 0;
  private checkedOnce = false;

  constructor() {
    effect(() => {
      const address = this.address();
      untracked(() => this.scheduleCheck(address));
    });
    inject(DestroyRef).onDestroy(() => {
      this.checkId += 1;
      this.clearTimer();
    });
  }

  protected onFieldChange(field: { set(value: string): void }, value: string | number | null | undefined): void {
    field.set(value == null ? '' : String(value));
  }

  private scheduleCheck(address: string): void {
    this.clearTimer();
    const id = ++this.checkId;
    const origin = parseServerAddress(address)?.origin;
    if (!origin) {
      this.serverState.set('invalid');
      this.imageChange.emit(DEFAULT_LOGIN_IMAGE);
      return;
    }
    this.serverState.set('checking');
    // Першу перевірку (відкрили форму) робимо одразу, наступні — коли користувач перестав друкувати.
    const delay = this.checkedOnce ? CHECK_DELAY_MS : 0;
    this.checkedOnce = true;
    this.timer = setTimeout(() => void this.checkServer(origin, id), delay);
  }

  private async checkServer(origin: string, id: number): Promise<void> {
    this.timer = null;
    const healthy = await this.connection.probeApi(origin);
    if (id !== this.checkId) {
      return;
    }
    if (!healthy) {
      this.serverState.set('unavailable');
      this.imageChange.emit(DEFAULT_LOGIN_IMAGE);
      return;
    }
    this.serverState.set('ok');
    const image = await this.images.random(origin);
    if (id === this.checkId) {
      this.imageChange.emit(image?.url ?? DEFAULT_LOGIN_IMAGE);
    }
  }

  private clearTimer(): void {
    if (this.timer !== null) {
      clearTimeout(this.timer);
      this.timer = null;
    }
  }

  protected async submit(): Promise<void> {
    const origin = this.origin();
    if (this.saving() || this.serverState() !== 'ok' || !origin) {
      return;
    }
    this.submitted.set(true);
    if (this.emailError() || this.passwordError()) {
      return;
    }

    this.saving.set(true);
    this.banner.set(null);
    const email = this.email().trim();
    const login = await this.auth.login(email, this.password(), origin, accountToReplace(this.accounts.accounts(), this.accountId(), email));
    this.saving.set(false);
    if (login !== 'ok') {
      this.banner.set({ key: `login.form.error.${loginErrorKey(login)}` });
      return;
    }

    // Акаунт уже активний: підключення (стан API, статус системи) досі належить іншому серверу,
    // тож перевіряємо його заново, а тоді відкриваємо першу сторінку.
    this.password.set('');
    await this.connection.checkApi();
    void this.router.navigateByUrl(REDIRECT_URL, { onSameUrlNavigation: 'reload' });
  }
}
