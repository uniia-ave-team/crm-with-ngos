import { Component, computed, effect, inject, input, OnDestroy, signal, untracked, WritableSignal } from '@angular/core';
import { Router } from '@angular/router';

import { NO_CONNECTION_ERROR } from '../core/connection/connection.guard';
import { AuthService, LoginResult } from '../core/auth/auth.service';
import { ConnectionService } from '../core/connection/connection.service';
import { AppLanguage, LanguageService } from '../core/i18n/language.service';
import { mobileLogoBase } from '../core/i18n/mobile-logo';
import { CreateNgoResult, NgoService } from '../core/ngo/ngo.service';
import { CreateUserResult, UsersService } from '../core/users/users.service';

/**
 * Куди переходить користувач, коли вхід у систему завершено: після входу (ГО
 * вже є), після створення ГО й після успішного оновлення сесії. Щоб змінити
 * цю адресу — міняти лише тут.
 */
const REDIRECT_URL = '/planner';

/**
 * Що показує сторінка: перевірку підключення (або запит статусу системи),
 * крок 1 (адреса сервера), форму створення організації (у системі ще нема ГО),
 * форму налаштування адміністратора, помилку налаштування або форму входу.
 */
type LoginView = 'loading' | 'step1' | 'ngo' | 'admin' | 'setup-error' | 'login';

/** Помилка поля назви або логотипа у формі організації. */
type NgoNameError = 'required' | 'too-long';
type NgoLogoError = 'type' | 'size';

/**
 * Пароль адміністратора — ті самі умови, що перевіряє сервер (`IdentityOptions`
 * в налаштуваннях API): від 8 символів, велика й мала латинська літера, цифра
 * та спецсимвол (будь-що, крім латинських літер і цифр).
 */
const ADMIN_PASSWORD_MIN_LENGTH = 8;
const ADMIN_PASSWORD_REQUIREMENTS = [
  { key: 'length', icon: '8', test: (password: string) => password.length >= ADMIN_PASSWORD_MIN_LENGTH },
  { key: 'lowercase', icon: 'a', test: (password: string) => /[a-z]/.test(password) },
  { key: 'uppercase', icon: 'A', test: (password: string) => /[A-Z]/.test(password) },
  { key: 'digit', icon: '123', test: (password: string) => /[0-9]/.test(password) },
  { key: 'special', icon: '#', test: (password: string) => /[^a-zA-Z0-9]/.test(password) },
] as const;
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

const UNIIA_UKRAINIAN_LANGUAGES: readonly AppLanguage[] = ['uk', 'crh', 'be'];

const NGO_NAME_MAX_LENGTH = 200;
/** Логотип: лише растрові/SVG-зображення до 2 МБ. */
const NGO_LOGO_TYPES = ['image/png', 'image/jpeg', 'image/webp', 'image/svg+xml'];
const NGO_LOGO_MAX_BYTES = 2 * 1024 * 1024;

/** Помилка над формою кроку 1. */
type LoginError = 'invalid' | 'unreachable' | 'unhealthy' | 'no-connection';

@Component({
  selector: 'app-login',
  templateUrl: './login.page.html',
  styleUrls: ['./login.page.scss'],
  standalone: false,
})
export class LoginPage implements OnDestroy {
  protected readonly connection = inject(ConnectionService);
  private readonly ngoService = inject(NgoService);
  private readonly usersService = inject(UsersService);
  private readonly router = inject(Router);
  private readonly languageService = inject(LanguageService);

  /** Вордмарк «Явір» вгорі форми входу (як мобільне лого меню; `.svg` / `_dark.svg`). */
  protected readonly logoBase = computed(() => mobileLogoBase(this.languageService.language()));

  /** Лого Унії: для української, кримськотатарської й білоруської — укр., для інших — англ. */
  protected readonly uniiaLogo = computed(() =>
    UNIIA_UKRAINIAN_LANGUAGES.includes(this.languageService.language()) ? 'Uniia' : 'Uniia_en',
  );
  private readonly auth = inject(AuthService);

  /**
   * Query-параметр `?error=…` (прив'язується роутером через
   * bindToComponentInputs): connectionGuard переадресовує сюди з
   * `error=no-connection`, коли користувач відкрив іншу сторінку без
   * підключення до API.
   */
  readonly error = input<string | undefined>();

  /** Те, що введено в поле кроку 1; початково — раніше надана адреса. */
  protected readonly address = signal(this.connection.serverAddress() ?? '');

  /** Введене не схоже на адресу сервера (валідація до будь-якого запиту). */
  private readonly invalid = signal(false);

  /** Оновлення токенів уже запускали (для поточної перевірки підключення). */
  private refreshStarted = false;


  /**
   * Чи надсилали форму кроку 1. Доки ні, перевірка, що тече при завантаженні
   * сторінки, показується як «завантаження», а не як форма кроку 1, що
   * миготить перед формою входу. Після надсилання форма лишається на місці
   * (з вимкненою кнопкою), поки йде перевірка нової адреси.
   */
  private readonly submitted = signal(false);

  /**
   * Користувач натиснув кнопку «змінити сервер»: показуємо крок 1, навіть
   * якщо поточний сервер працює, доки не буде надано нову адресу.
   */
  private readonly forceAddressStep = signal(false);

  protected readonly view = computed<LoginView>(() => {
    if (this.forceAddressStep()) {
      return 'step1';
    }
    const status = this.connection.apiStatus();
    if (status === 'healthy') {
      // Форму показуємо лише коли відомо, чи є в системі ГО (або запит
      // статусу не вдався — тоді не тримаємо людину на «завантаженні»).
      const state = this.connection.systemStatusState();
      if (state === 'idle' || state === 'loading') {
        return 'loading';
      }
      return this.setupView();
    }
    if ((status === 'unknown' || status === 'checking') && !this.submitted()) {
      return 'loading';
    }
    return 'step1';
  });

  /**
   * Що показати за статусом системи й наявністю токенів:
   * - `isSetupComplete`: без токенів — форма входу; з токенами — оновлюємо їх
   *   (`refreshSession`) і переходимо на «Планер», а якщо не вдалося — вихід,
   *   і форма входу;
   * - немає адміна — форма реєстрації адміністратора;
   * - адмін є, ГО нема: з токенами — форма створення ГО, без — форма входу;
   * - адмін і ГО є, а налаштування не завершене — помилка.
   * Якщо статус отримати не вдалося — форма входу.
   */
  private setupView(): LoginView {
    const status = this.connection.systemStatus();
    if (!status) {
      return 'login';
    }
    const hasTokens = this.auth.hasTokens();
    if (status.isSetupComplete) {
      return hasTokens ? 'loading' : 'login';
    }
    if (!status.hasAdmin) {
      return 'admin';
    }
    if (!status.hasNgo) {
      return hasTokens ? 'ngo' : 'login';
    }
    return 'setup-error';
  }

  /**
   * Помилка над формою кроку 1. Про недоступність/нездорове API кажемо, коли
   * користувач уже надавав адресу або його переадресували сюди через
   * відсутність підключення. Якщо ж просто відкрито /login і API за доменом
   * сторінки не знайшлося, це не помилка, а звичайний привід для кроку 1.
   */
  protected readonly formError = computed<LoginError | null>(() => {
    if (this.invalid()) {
      return 'invalid';
    }
    const status = this.connection.apiStatus();
    const failed = status === 'unreachable' || status === 'unhealthy';
    if (this.connection.serverAddress() && failed) {
      return status;
    }
    // Адреси ще не надавали, але нас сюди переадресували через відсутність
    // підключення (за доменом сторінки API не знайдено) — кажемо про це.
    if (failed && this.error() === NO_CONNECTION_ERROR) {
      return 'no-connection';
    }
    return null;
  });

  /** Форма створення організації. */
  protected readonly ngoName = signal('');
  protected readonly ngoLogo = signal<File | null>(null);
  protected readonly ngoLogoPreview = signal<string | null>(null);
  /** Файл тягнуть над зоною завантаження логотипа. */
  protected readonly logoDragOver = signal(false);
  protected readonly ngoSaving = signal(false);
  /** Чим закінчилася остання спроба створити організацію (не `created`). */
  protected readonly ngoSaveError = signal<Exclude<CreateNgoResult, 'created'> | null>(null);
  private readonly ngoSubmitted = signal(false);
  protected readonly ngoLogoError = signal<NgoLogoError | null>(null);

  protected readonly ngoNameError = computed<NgoNameError | null>(() => {
    const name = this.ngoName().trim();
    if (!name) {
      return this.ngoSubmitted() ? 'required' : null;
    }
    return name.length > NGO_NAME_MAX_LENGTH ? 'too-long' : null;
  });

  /**
   * Помилки форми ГО для блоку над формою (ключі перекладу): лише відповідь
   * сервера на створення; помилки назви й логотипа — під своїми полями.
   */
  protected readonly ngoBannerErrors = computed<string[]>(() => {
    const saveError = this.ngoSaveError();
    return saveError ? [`login.ngo.saveError.${saveError}`] : [];
  });

  /** Форма входу. */
  protected readonly loginEmail = signal('');
  protected readonly loginPassword = signal('');
  protected readonly loginSaving = signal(false);
  private readonly loginSubmitted = signal(false);
  /** Ключ помилки запиту (`login.form.error.<ключ>`) або `null`. */
  private readonly loginSaveError = signal<string | null>(null);

  protected readonly loginEmailError = computed(() =>
    this.loginSubmitted() && !this.loginEmail().trim() ? 'emailRequired' : null,
  );
  protected readonly loginPasswordError = computed(() =>
    this.loginSubmitted() && !this.loginPassword() ? 'passwordRequired' : null,
  );
  /** Відповідь сервера — блоком над формою (ключ перекладу). */
  protected readonly loginBannerError = computed(() => {
    const key = this.loginSaveError();
    return key ? `login.form.error.${key}` : null;
  });

  /** Форма адміністратора. */
  protected readonly adminEmail = signal('');
  protected readonly adminLastName = signal('');
  protected readonly adminFirstName = signal('');
  protected readonly adminPassword = signal('');
  protected readonly adminPasswordRepeat = signal('');
  private readonly adminSubmitted = signal(false);
  protected readonly adminSaving = signal(false);
  /** Ключ помилки запиту (`login.admin.saveError.<ключ>`) або `null`. */
  private readonly adminSaveError = signal<string | null>(null);
  /**
   * Адміністратора вже створено на сервері (але вхід не вдався): повторна
   * спроба не створює його вдруге, а лише повторює вхід.
   */
  private adminCreated = false;

  // Помилки показуємо лише після спроби надіслати форму; `null` — все гаразд.
  protected readonly adminEmailError = computed(() => {
    if (!this.adminSubmitted()) {
      return null;
    }
    const email = this.adminEmail().trim();
    if (!email) {
      return 'emailRequired';
    }
    return EMAIL_PATTERN.test(email) ? null : 'emailInvalid';
  });
  protected readonly adminLastNameError = computed(() =>
    this.adminSubmitted() && !this.adminLastName().trim() ? 'lastNameRequired' : null,
  );
  protected readonly adminFirstNameError = computed(() =>
    this.adminSubmitted() && !this.adminFirstName().trim() ? 'firstNameRequired' : null,
  );
  /** Помилка прізвища й відповідь сервера — блоком над формою (ключі перекладу). */
  protected readonly adminBannerErrors = computed<string[]>(() => {
    const lastName = this.adminLastNameError();
    const save = this.adminSaveError();
    return [lastName && `login.admin.error.${lastName}`, save && `login.admin.saveError.${save}`].filter(
      (key): key is string => !!key,
    );
  });
  /** Умови до пароля й чи виконано кожну (оновлюється під час введення). */
  protected readonly adminPasswordRequirements = computed(() => {
    const password = this.adminPassword();
    return ADMIN_PASSWORD_REQUIREMENTS.map(({ key, icon, test }) => ({ key, icon, met: test(password) }));
  });
  /** Повтор пароля збігається з паролем (порожній повтор не рахується). */
  protected readonly adminPasswordsMatch = computed(
    () => !!this.adminPasswordRepeat() && this.adminPasswordRepeat() === this.adminPassword(),
  );
  protected readonly adminPasswordError = computed(() => {
    if (!this.adminSubmitted()) {
      return null;
    }
    const password = this.adminPassword();
    if (!password) {
      return 'passwordRequired';
    }
    return this.adminPasswordRequirements().every((requirement) => requirement.met) ? null : 'passwordWeak';
  });
  protected readonly adminPasswordRepeatError = computed(() => {
    if (!this.adminSubmitted()) {
      return null;
    }
    const repeat = this.adminPasswordRepeat();
    if (!repeat) {
      return 'passwordRepeatRequired';
    }
    return repeat === this.adminPassword() ? null : 'passwordMismatch';
  });

  constructor() {
    // Щойно підключення є — запитуємо стан первинного налаштування системи
    // (від нього залежить, що показати: форму організації чи вхід).
    // untracked: виклик нічого не читає з сигналів сторінки, тож ефект має
    // реагувати лише на зміну `apiStatus`.
    effect(() => {
      // Нова перевірка підключення (інша адреса тощо) — починаємо спочатку.
      const healthy = this.connection.apiStatus() === 'healthy';
      untracked(() => {
        this.refreshStarted = false;
        if (healthy) {
          this.auth.restoreTokens();
          this.connection.loadSystemStatus();
        }
      });
    });

    // Налаштування завершене й токени є — один раз оновлюємо їх.
    effect(() => {
      const complete = this.connection.systemStatus()?.isSetupComplete === true;
      if (complete && this.auth.hasTokens()) {
        untracked(() => void this.refreshSession());
      }
    });
  }

  /**
   * Оновлює токени (`POST /api/v1/auth/refresh-token`) і переходить на
   * «Планер». Не вдалося — вихід із системи: токени забуто, і сторінка
   * показує форму входу.
   */
  private async refreshSession(): Promise<void> {
    if (this.refreshStarted) {
      return;
    }
    this.refreshStarted = true;
    const result = await this.auth.refresh();
    if (result === 'ok') {
      void this.router.navigateByUrl(REDIRECT_URL);
    } else {
      this.auth.clearTokens();
    }
  }

  protected onAddressChange(value: string | number | null | undefined): void {
    this.address.set(value == null ? '' : String(value));
    this.invalid.set(false);
  }

  protected submit(): void {
    if (this.connection.apiStatus() === 'checking') {
      return;
    }
    this.submitted.set(true);
    const accepted = this.connection.setServerAddress(this.address());
    this.invalid.set(!accepted);
    if (accepted) {
      this.forceAddressStep.set(false);
    }
  }

  /** Іде перевірка поточного сервера після натискання «змінити сервер». */
  protected readonly resettingServer = signal(false);

  /**
   * Кнопка «змінити сервер» (праворуч угорі кожної форми). Спершу один раз
   * перевіряє поточний сервер (`/health`); тоді повністю очищає `localStorage`
   * (адреса сервера, токени, мова тощо), забуває введене у формах і повертає на
   * крок 1. Якщо сервер був здоровий — його адресу підставлено в поле кроку 1,
   * щоб її було легко поправити.
   */
  protected async resetServer(): Promise<void> {
    if (this.resettingServer()) {
      return;
    }
    this.resettingServer.set(true);
    // Беремо до очищення: після нього надана адреса вже забута.
    const currentAddress = this.connection.serverAddress() ?? this.connection.domain;
    const healthy = await this.connection.probeApi();

    this.auth.clearTokens();
    this.connection.clearServerAddress();
    try {
      localStorage.clear();
    } catch {
      // Сховище недоступне — нічого очищати.
    }
    this.resetForms();
    this.address.set(healthy ? currentAddress : '');
    this.invalid.set(false);
    this.submitted.set(false);
    this.forceAddressStep.set(true);
    this.resettingServer.set(false);
  }

  /** Забуває все, що введено й показано у формах входу, адміна та ГО. */
  private resetForms(): void {
    this.loginEmail.set('');
    this.loginPassword.set('');
    this.loginSubmitted.set(false);
    this.loginSaveError.set(null);
    this.adminEmail.set('');
    this.adminLastName.set('');
    this.adminFirstName.set('');
    this.adminPassword.set('');
    this.adminPasswordRepeat.set('');
    this.adminSubmitted.set(false);
    this.adminSaveError.set(null);
    this.adminCreated = false;
    this.ngoName.set('');
    this.removeLogo();
    this.ngoSubmitted.set(false);
    this.ngoSaveError.set(null);
  }

  ngOnDestroy(): void {
    this.revokePreview();
  }

  protected onNgoNameChange(value: string | number | null | undefined): void {
    this.ngoName.set(value == null ? '' : String(value));
  }

  protected onLogoSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    // Дозволяємо вибрати той самий файл ще раз після видалення.
    input.value = '';
    if (file) {
      this.setLogo(file);
    }
  }

  protected onLogoDragOver(event: DragEvent): void {
    // Без preventDefault браузер не дозволить скинути файл на елемент.
    event.preventDefault();
    this.logoDragOver.set(true);
  }

  protected onLogoDragLeave(event: DragEvent): void {
    // dragleave спрацьовує і при переході на дочірні елементи — ігноруємо.
    const zone = event.currentTarget as HTMLElement;
    if (!zone.contains(event.relatedTarget as Node | null)) {
      this.logoDragOver.set(false);
    }
  }

  protected onLogoDrop(event: DragEvent): void {
    event.preventDefault();
    this.logoDragOver.set(false);
    const file = event.dataTransfer?.files?.[0];
    if (file) {
      this.setLogo(file);
    }
  }

  private setLogo(file: File): void {
    if (!NGO_LOGO_TYPES.includes(file.type)) {
      this.ngoLogoError.set('type');
      return;
    }
    if (file.size > NGO_LOGO_MAX_BYTES) {
      this.ngoLogoError.set('size');
      return;
    }
    this.ngoLogoError.set(null);
    this.revokePreview();
    this.ngoLogo.set(file);
    this.ngoLogoPreview.set(URL.createObjectURL(file));
  }

  protected removeLogo(): void {
    this.revokePreview();
    this.ngoLogo.set(null);
    this.ngoLogoError.set(null);
  }

  /**
   * Створює організацію з введеною назвою (логотип поки не надсилається:
   * `logoUrl` завжди `null`). Після успіху користувач уже зареєстрований і
   * має токени (вхід виконано після створення адміна), тож його одразу
   * переадресовано на «Планер».
   */
  protected async submitNgo(): Promise<void> {
    if (this.ngoSaving()) {
      return;
    }
    this.ngoSubmitted.set(true);
    if (this.ngoNameError() || this.ngoLogoError()) {
      return;
    }
    this.ngoSaving.set(true);
    this.ngoSaveError.set(null);
    const result = await this.ngoService.create(this.ngoName().trim());
    this.ngoSaving.set(false);
    if (result === 'created') {
      void this.router.navigateByUrl(REDIRECT_URL);
    } else {
      this.ngoSaveError.set(result);
    }
  }

  protected onFieldChange(
    field: WritableSignal<string>,
    value: string | number | null | undefined,
  ): void {
    field.set(value == null ? '' : String(value));
  }

  /**
   * Вхід (`POST /api/v1/auth/login`): токени записуються (`AuthService`), далі
   * запитується статус системи. Немає ГО — лишаємось на сторінці, і вона
   * показує форму створення ГО (токени вже є). ГО є — одразу на «Планер».
   */
  protected async submitLogin(): Promise<void> {
    if (this.loginSaving()) {
      return;
    }
    this.loginSubmitted.set(true);
    if (this.loginEmailError() || this.loginPasswordError()) {
      return;
    }

    this.loginSaving.set(true);
    this.loginSaveError.set(null);
    const login = await this.auth.login(this.loginEmail().trim(), this.loginPassword());
    if (login !== 'ok') {
      this.loginSaving.set(false);
      this.loginSaveError.set(loginFormErrorKey(login));
      return;
    }

    // Токени свіжі — оновлювати їх одразу після входу не треба.
    this.refreshStarted = true;
    const status = await this.connection.loadSystemStatus();
    this.loginSaving.set(false);
    if (!status) {
      this.loginSaveError.set('statusFailed');
    } else if (status.hasNgo) {
      void this.router.navigateByUrl(REDIRECT_URL);
    }
    // Немає ГО: view() за статусом і токенами сам покаже форму створення ГО.
  }

  /**
   * Перевіряє форму адміністратора; якщо все гаразд — створює користувача
   * (`POST /api/v1/users`), одразу входить під ним тими самими даними
   * (`POST /api/v1/auth/login`) і перезапитує статус системи: адмін уже є,
   * тож сторінка переходить до форми створення ГО.
   */
  protected async submitAdmin(): Promise<void> {
    if (this.adminSaving()) {
      return;
    }
    this.adminSubmitted.set(true);
    if (
      this.adminEmailError() ||
      this.adminLastNameError() ||
      this.adminFirstNameError() ||
      this.adminPasswordError() ||
      this.adminPasswordRepeatError()
    ) {
      return;
    }

    this.adminSaving.set(true);
    this.adminSaveError.set(null);
    const email = this.adminEmail().trim();
    const password = this.adminPassword();

    if (!this.adminCreated) {
      const created = await this.usersService.create({
        firstName: this.adminFirstName().trim(),
        lastName: this.adminLastName().trim(),
        email,
        password,
        confirmPassword: this.adminPasswordRepeat(),
      });
      if (created !== 'created') {
        this.adminSaving.set(false);
        this.adminSaveError.set(createUserErrorKey(created));
        return;
      }
      this.adminCreated = true;
    }

    const login = await this.auth.login(email, password);
    this.adminSaving.set(false);
    if (login === 'ok') {
      this.connection.loadSystemStatus();
    } else {
      this.adminSaveError.set(loginErrorKey(login));
    }
  }

  private revokePreview(): void {
    const url = this.ngoLogoPreview();
    if (url) {
      URL.revokeObjectURL(url);
      this.ngoLogoPreview.set(null);
    }
  }
}

function createUserErrorKey(result: Exclude<CreateUserResult, 'created'>): string {
  switch (result) {
    case 'invalid':
      return 'createInvalid';
    case 'unreachable':
      return 'createUnreachable';
    default:
      return 'createError';
  }
}

function loginErrorKey(result: Exclude<LoginResult, 'ok'>): string {
  switch (result) {
    case 'invalid':
      return 'loginInvalid';
    case 'unauthorized':
      return 'loginUnauthorized';
    case 'unreachable':
      return 'loginUnreachable';
    default:
      return 'loginError';
  }
}

function loginFormErrorKey(result: Exclude<LoginResult, 'ok'>): string {
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
