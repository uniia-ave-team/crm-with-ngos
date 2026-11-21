import { Component, DestroyRef, computed, effect, inject, input, linkedSignal, output, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { IonButton, IonInput } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

import { accountToReplace, loginErrorKey } from '../../accounts/account-login';
import { AccountsService } from '../../accounts/accounts.service';
import { ConnectionService, parseServerAddress } from '../../connection/connection.service';
import { serverAddressToUnicode } from '../../connection/idn';
import { AuthService } from '../auth.service';

const EMAIL_REQUIRED = 'login.form.error.emailRequired';
const PASSWORD_REQUIRED = 'login.form.error.passwordRequired';
/** Пауза після останнього символу в адресі, перед перевіркою сервера. */
const CHECK_DELAY_MS = 500;

/** Що відомо про сервер за адресою у формі. */
type ServerState = 'checking' | 'ok' | 'unavailable' | 'invalid';

/** Помилка під полем адреси: ключ перекладу й параметри (`{{url}}`). */
interface AddressError {
  readonly key: string;
  readonly params?: { readonly url: string };
}

/**
 * Що робить той, хто показує форму, після успішного входу (акаунт уже активний): наприклад,
 * перевіряє підключення й відкриває першу сторінку. Поки воно триває, форма лишається
 * зайнятою (кнопка вимкнена).
 */
export type AfterSignIn = () => Promise<void>;

/**
 * Форма входу: пошта, пароль, «Ввійти» й «Забули пароль?» (поки без дії), а якщо
 * `withServer` — ще й поле адреси сервера перед ними. Одна на сторінку входу (`/login`,
 * сервер уже визначено раніше, тож поля адреси нема), сторінку повторного входу
 * (`/relogin`) і вікно входу в акаунт із меню акаунтів (обидва з полем адреси).
 *
 *   <app-sign-in-form [afterSignIn]="openApp" />
 *   <app-sign-in-form [withServer]="true" [initialServer]="account.server"
 *     [initialEmail]="account.email" [accountId]="account.id"
 *     [afterSignIn]="openApp" (serverChange)="loadImage($event)" />
 *
 * - **Сервер.** Без поля адреси входимо на поточному сервері (`ConnectionService.serverUrl`).
 *   З полем: адреса початково `initialServer`; після кожної зміни (після паузи, поки друкують;
 *   перша перевірка одразу) за нею шукається API (`ConnectionService.resolveServer`, `GET /health`:
 *   адреса без порту — спершу як є, далі з портом 5065; з портом — лише він). Недоступний сервер
 *   чи не схожа на адресу — повідомлення під полем, «Ввійти» вимкнена. `serverChange` віддає
 *   origin, за яким знайшлося здорове API (його й запамʼятає акаунт), або `null`.
 * - **Вхід.** `POST /api/v1/auth/login` на цьому сервері. Якщо `accountId` вказує на
 *   збережений акаунт із тією самою поштою, вхід перезаписує його, інакше додає новий
 *   (`AuthService.login`); акаунт стає активним. Далі викликається `afterSignIn`.
 * - **Помилки.** Порожні пошта чи пароль — під полями після першої спроби; відповідь сервера —
 *   блоком над формою, форму можна надіслати ще раз. Там само показується `error` — помилка
 *   того, хто показує форму (наприклад, після входу не вдалося отримати стан системи); вона
 *   живе в нього, бо форма могла за цей час зникнути й зʼявитись знову. `attempt` сповіщає про
 *   нову спробу входу, щоб її прибрати.
 * - **Готовий акаунт.** `signInAs` вписує пошту й пароль і входить тим самим шляхом (кнопки
 *   «Ввійти як …» демо-блоку сторінки входу); `busy` — іде вхід.
 */
@Component({
  selector: 'app-sign-in-form',
  templateUrl: './sign-in-form.component.html',
  styleUrls: ['./sign-in-form.component.scss'],
  imports: [IonButton, IonInput, FormsModule, TranslatePipe],
})
export class SignInFormComponent {
  private readonly connection = inject(ConnectionService);
  private readonly accounts = inject(AccountsService);
  private readonly auth = inject(AuthService);

  /** Показувати поле адреси сервера (і перевіряти сервер перед входом). */
  readonly withServer = input(false);
  /** Ключ збереженого акаунта, який вхід може перезаписати; `null` — додається новий. */
  readonly accountId = input<string | null | undefined>(null);
  /** Адреса сервера, вписана спочатку (лише з полем адреси). */
  readonly initialServer = input('');
  /** Пошта, вписана спочатку. */
  readonly initialEmail = input('');
  readonly afterSignIn = input<AfterSignIn | null>(null);
  /** Помилка від того, хто показує форму (ключ перекладу): стає над формою, якщо своєї нема. */
  readonly error = input<string | null>(null);

  /** Origin здорового сервера за адресою у формі або `null` (лише з полем адреси). */
  readonly serverChange = output<string | null>();
  /** Почалась нова спроба входу (поля заповнено, запит зараз піде). */
  readonly attempt = output<void>();

  private static nextId = 0;
  /** Унікальний префікс `id` полів: форм на сторінці може бути кілька. */
  protected readonly idPrefix = `sign-in-${SignInFormComponent.nextId++}`;

  /** Адреса в полі; кириличний домен показано кирилицею, а не записом браузера (`xn--…`). */
  protected readonly address = linkedSignal(() => serverAddressToUnicode(this.initialServer()));
  protected readonly email = linkedSignal(() => this.initialEmail());
  protected readonly password = signal('');
  protected readonly saving = signal(false);
  /** Іде вхід (чи те, що після нього): форма зайнята. */
  readonly busy = this.saving.asReadonly();
  /** Помилка входу (ключ перекладу). */
  private readonly banner = signal<string | null>(null);
  /** Помилка над формою: своя (відповідь сервера на вхід) або передана ззовні. */
  protected readonly shownError = computed(() => this.banner() ?? this.error());
  private readonly submitted = signal(false);

  private readonly serverState = signal<ServerState>('checking');
  /** Origin адреси з поля, як її введено (для тексту помилки), якщо її вдалося розібрати. */
  private readonly typedOrigin = computed(() => parseServerAddress(this.address())?.origin ?? null);
  /**
   * Origin, за яким знайшлося здорове API адреси з поля (без порту — адреса як є або з портом
   * 5065): на ньому йде вхід, і його запамʼятовує акаунт. `null` — ще не знайдено.
   */
  private server: string | null = null;

  /** Вхід можливий: без поля адреси завжди, з полем — лише коли сервер здоровий. */
  protected readonly serverReady = computed(() => !this.withServer() || this.serverState() === 'ok');

  protected readonly addressError = computed<AddressError | null>(() => {
    if (!this.withServer()) {
      return null;
    }
    const state = this.serverState();
    if (state === 'invalid') {
      return { key: 'login.step1.error.invalid' };
    }
    return state === 'unavailable' ? { key: 'login.step1.error.unreachable', params: { url: this.typedOrigin() ?? '' } } : null;
  });
  protected readonly emailError = computed(() => (this.submitted() && !this.email().trim() ? EMAIL_REQUIRED : null));
  protected readonly passwordError = computed(() => (this.submitted() && !this.password() ? PASSWORD_REQUIRED : null));

  private timer: ReturnType<typeof setTimeout> | null = null;
  /** Номер останньої перевірки: відповідь на попередню адресу відкидається. */
  private checkId = 0;
  private checkedOnce = false;

  constructor() {
    effect(() => {
      const withServer = this.withServer();
      const address = this.address();
      untracked(() => {
        if (withServer) {
          this.scheduleCheck(address);
        }
      });
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
    this.server = null;
    if (!parseServerAddress(address)) {
      this.serverState.set('invalid');
      this.serverChange.emit(null);
      return;
    }
    this.serverState.set('checking');
    // Першу перевірку (відкрили форму) робимо одразу, наступні — коли користувач перестав друкувати.
    const delay = this.checkedOnce ? CHECK_DELAY_MS : 0;
    this.checkedOnce = true;
    this.timer = setTimeout(() => void this.checkServer(address, id), delay);
  }

  private async checkServer(address: string, id: number): Promise<void> {
    this.timer = null;
    const server = await this.connection.resolveServer(address);
    if (id !== this.checkId) {
      return;
    }
    this.server = server;
    this.serverState.set(server ? 'ok' : 'unavailable');
    this.serverChange.emit(server);
  }

  private clearTimer(): void {
    if (this.timer !== null) {
      clearTimeout(this.timer);
      this.timer = null;
    }
  }

  /**
   * Входить під готовим акаунтом (кнопка «Ввійти як …» демо-блоку сторінки входу): вписує пошту й
   * пароль у форму й надсилає її звичайним шляхом — з тими самими помилками й `afterSignIn`. Поки
   * форма зайнята, нічого не змінює.
   */
  signInAs(email: string, password: string): Promise<void> {
    if (this.saving()) {
      return Promise.resolve();
    }
    this.email.set(email);
    this.password.set(password);
    return this.submit();
  }

  protected async submit(): Promise<void> {
    const server = this.withServer() ? this.server : this.connection.serverUrl();
    if (this.saving() || !this.serverReady() || !server) {
      return;
    }
    this.submitted.set(true);
    if (this.emailError() || this.passwordError()) {
      return;
    }

    this.saving.set(true);
    this.banner.set(null);
    this.attempt.emit();
    const email = this.email().trim();
    const login = await this.auth.login(email, this.password(), server, accountToReplace(this.accounts.accounts(), this.accountId(), email));
    if (login !== 'ok') {
      this.saving.set(false);
      this.banner.set(`login.form.error.${loginErrorKey(login)}`);
      return;
    }

    // Акаунт уже активний; пароль у формі більше не потрібен.
    this.password.set('');
    try {
      await this.afterSignIn()?.();
    } finally {
      // Навіть якщо те, що йде після входу, зламалось, форма не має лишитися зайнятою.
      this.saving.set(false);
    }
  }
}
