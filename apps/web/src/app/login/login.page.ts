import { Component, computed, effect, inject, input, OnDestroy, signal, untracked, WritableSignal } from '@angular/core';
import { Router } from '@angular/router';

import { NO_CONNECTION_ERROR } from '../core/connection/connection.guard';
import { AccountRestoreService } from '../core/accounts/account-restore.service';
import { AccountsService } from '../core/accounts/accounts.service';
import { AuthService, LoginResult, REDIRECT_URL } from '../core/auth/auth.service';
import { ConnectionService } from '../core/connection/connection.service';
import { AppLanguage, LanguageService } from '../core/i18n/language.service';
import { mobileLogoBase } from '../core/i18n/mobile-logo';
import { NGO_LOGO_MAX_BYTES, NGO_LOGO_TYPES, NGO_NAME_MAX_LENGTH } from '../core/ngo/logo-rules';
import { LoginPageImagesService } from '../core/login-page-images/login-page-images.service';
import { CreateNgoResult, NgoService, UploadNgoLogoResult } from '../core/ngo/ngo.service';
import { CreateUserResult, UsersService } from '../core/users/users.service';
import { isStrongPassword } from '../shared/password-fields/password-rules';

/**
 * Що показує сторінка: перевірку підключення (або запит статусу системи),
 * крок 1 (адреса сервера), форму створення організації (у системі ще нема ГО),
 * форму налаштування адміністратора, помилку налаштування або форму входу.
 */
type LoginView = 'loading' | 'step1' | 'ngo' | 'admin' | 'setup-error' | 'login';

/** Зображення форми входу за замовчуванням: його лишаємо, коли в системі нема власних. */
const DEFAULT_LOGIN_IMAGE = 'assets/login_image.jpg';

/** Помилка поля назви або логотипа у формі організації. */
type NgoNameError = 'required' | 'too-long';
type NgoLogoError = 'type' | 'size';

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

const UNIIA_UKRAINIAN_LANGUAGES: readonly AppLanguage[] = ['uk', 'crh', 'be'];


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
  private readonly accounts = inject(AccountsService);
  private readonly restoreService = inject(AccountRestoreService);
  private readonly languageService = inject(LanguageService);
  private readonly loginPageImages = inject(LoginPageImagesService);

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

  /**
   * Пошук чинного акаунта на старті завершено, і жодного не знайдено (знайдено —
   * сторінка переходить на `REDIRECT_URL`, не показуючи форм). Поки триває —
   * «Перевіряємо підключення…».
   */
  private readonly restoreDone = signal(false);

  /**
   * Зображення збоку форми входу: випадкове з тих, що завантажені в системі
   * (`GET /api/v1/loginpageimages/random`); поки запит іде, а також коли зображень
   * нема чи отримати його не вдалося — зображення за замовчуванням.
   */
  protected readonly loginImage = signal(DEFAULT_LOGIN_IMAGE);
  /** Сервер, для якого вже запитували випадкове зображення (на один сервер — один запит). */
  private loginImageRequestedFor: string | null = null;

  protected readonly view = computed<LoginView>(() => {
    if (!this.restoreDone()) {
      return 'loading';
    }
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
   * - `isSetupComplete`: без акаунта — форма входу; з активним акаунтом (щойно
   *   ввійшли) — одразу на «Планер»;
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
  /** Значення `accept` вибору файлу логотипа (типи з `core/images/image-types.ts`). */
  protected readonly logoTypes = NGO_LOGO_TYPES.join(',');
  /** Файл тягнуть над зоною завантаження логотипа. */
  protected readonly logoDragOver = signal(false);
  protected readonly ngoSaving = signal(false);
  /** Чим закінчилася остання спроба створити організацію (не `created`). */
  protected readonly ngoSaveError = signal<Exclude<CreateNgoResult, 'created'> | null>(null);
  /**
   * Організацію вже створено (перший запит вдався), але логотип не завантажився:
   * повторне «Продовжити» не створює її вдруге, а лише повторює логотип. Назва
   * в цей час не змінюється (її вже збережено).
   */
  protected readonly ngoCreated = signal(false);
  /** Чим закінчилася остання спроба завантажити логотип (не `uploaded`). */
  protected readonly ngoLogoUploadError = signal<Exclude<UploadNgoLogoResult, 'uploaded'> | null>(null);
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
   * Помилки форми ГО для блоку над формою (ключі перекладу): відповіді сервера
   * на створення й на завантаження логотипа; помилки назви й файлу — під своїми
   * полями.
   */
  protected readonly ngoBannerErrors = computed<string[]>(() => {
    const saveError = this.ngoSaveError();
    const uploadError = this.ngoLogoUploadError();
    return [
      ...(saveError ? [`login.ngo.saveError.${saveError}`] : []),
      ...(uploadError ? [`login.ngo.logoUploadError.${uploadError}`] : []),
    ];
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
  /** По батькові: необовʼязкове, перевірки нема; порожнє йде в запит як `null`. */
  protected readonly adminPatronymic = signal('');
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
  protected readonly adminPasswordError = computed(() => {
    if (!this.adminSubmitted()) {
      return null;
    }
    const password = this.adminPassword();
    if (!password) {
      return 'passwordRequired';
    }
    return isStrongPassword(password) ? null : 'passwordWeak';
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
    // Спершу шукаємо чинний акаунт серед збережених: знайшли — на «Планер»,
    // ні — показуємо вхід, реєстрацію тощо. Акаунтів нема — шукати нічого,
    // форми одразу.
    if (this.accounts.accounts().length === 0) {
      this.restoreDone.set(true);
    } else {
      void this.restoreAccounts();
    }

    // Щойно пошук завершено й підключення є — запитуємо стан первинного
    // налаштування системи (від нього залежить, що показати: форму адміна,
    // організації чи входу). untracked: виклик нічого не читає з сигналів
    // сторінки, тож ефект має реагувати лише на `restoreDone` і `apiStatus`.
    effect(() => {
      const done = this.restoreDone();
      const healthy = this.connection.apiStatus() === 'healthy';
      untracked(() => {
        if (done && healthy) {
          this.connection.loadSystemStatus();
        }
      });
    });

    // Показано форму входу — підтягуємо її зображення з сервера (раз на сервер).
    effect(() => {
      const showingForm = this.view() === 'login';
      const server = this.connection.serverUrl();
      untracked(() => {
        if (showingForm && this.loginImageRequestedFor !== server) {
          this.loginImageRequestedFor = server;
          void this.loadLoginImage();
        }
      });
    });

    // Налаштування завершене, а акаунт активний (щойно ввійшли або
    // зареєстрували адміна) — одразу на `REDIRECT_URL`.
    effect(() => {
      const complete = this.connection.systemStatus()?.isSetupComplete === true;
      if (complete && this.auth.hasTokens()) {
        untracked(() => void this.router.navigateByUrl(REDIRECT_URL));
      }
    });
  }

  private async loadLoginImage(): Promise<void> {
    const image = await this.loginPageImages.random();
    // Для іншого сервера зображення вже не те (змінили адресу, поки йшов запит).
    this.loginImage.set(image?.url ?? DEFAULT_LOGIN_IMAGE);
  }

  /** Зображення форми не завантажилося (битий файл, посилання) — повертаємо стандартне. */
  protected onLoginImageError(): void {
    if (this.loginImage() !== DEFAULT_LOGIN_IMAGE) {
      this.loginImage.set(DEFAULT_LOGIN_IMAGE);
    }
  }

  /**
   * Шукає чинний акаунт серед збережених (`AccountRestoreService`; застарілі
   * чистить). Знайшовся — переходимо на `REDIRECT_URL`; ні — відкриваємо форми.
   */
  private async restoreAccounts(): Promise<void> {
    const result = await this.restoreService.restore();
    if (result === 'restored') {
      // `restoreDone` лишається `false`: до переходу сторінка показує «завантаження».
      void this.router.navigateByUrl(REDIRECT_URL);
      return;
    }
    this.restoreDone.set(true);
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
    // Беремо до очищення: після нього акаунти й надана адреса вже забуті.
    const currentAddress = this.accounts.active()?.server ?? this.connection.serverAddress() ?? this.connection.domain;
    const healthy = await this.connection.probeApi();

    this.accounts.wipe();
    this.connection.clearServerAddress();
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
    this.adminPatronymic.set('');
    this.adminPassword.set('');
    this.adminPasswordRepeat.set('');
    this.adminSubmitted.set(false);
    this.adminSaveError.set(null);
    this.adminCreated = false;
    this.ngoName.set('');
    this.removeLogo();
    this.ngoSubmitted.set(false);
    this.ngoSaveError.set(null);
    this.ngoCreated.set(false);
    this.ngoLogoUploadError.set(null);
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
   * Створює організацію з введеною назвою (`POST /api/v1/ngo`; `logoUrl` у ньому
   * завжди `null`), а якщо обрано логотип — після цього першого запиту
   * завантажує саме зображення (`POST /api/v1/ngo/logo`). Після успіху
   * користувач уже зареєстрований і має токени (вхід виконано після створення
   * адміна), тож його одразу переадресовано на «Планер».
   *
   * Організація створюється один раз: якщо логотип не завантажився, вона вже є,
   * і повторне «Продовжити» повторює лише завантаження (або, коли логотип
   * прибрали, одразу веде далі).
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
    this.ngoLogoUploadError.set(null);

    if (!this.ngoCreated()) {
      const created = await this.ngoService.create(this.ngoName().trim());
      if (created !== 'created') {
        this.ngoSaving.set(false);
        this.ngoSaveError.set(created);
        return;
      }
      this.ngoCreated.set(true);
    }

    const logo = this.ngoLogo();
    if (logo) {
      const uploaded = await this.ngoService.uploadLogo(logo);
      if (uploaded !== 'uploaded') {
        this.ngoSaving.set(false);
        this.ngoLogoUploadError.set(uploaded);
        return;
      }
    }

    this.ngoSaving.set(false);
    void this.router.navigateByUrl(REDIRECT_URL);
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
        patronymic: this.adminPatronymic().trim() || null,
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
