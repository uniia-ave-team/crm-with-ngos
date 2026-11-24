import { Component, computed, effect, inject, OnDestroy, signal, untracked, viewChild, WritableSignal } from '@angular/core';
import { Router } from '@angular/router';
import { NgTemplateOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IonButton, IonContent, IonIcon, IonInput } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

import { AccountRestoreService } from '../core/accounts/account-restore.service';
import { AccountsService } from '../core/accounts/accounts.service';
import { AuthService, LOGIN_URL, LoginResult, REDIRECT_URL, signedOutUrl } from '../core/auth/auth.service';
import { PasswordPolicyService } from '../core/auth/password-policy.service';
import { AfterSignIn, SignInFormComponent } from '../core/auth/sign-in-form/sign-in-form.component';
import { ConnectionService, DemoAccount } from '../core/connection/connection.service';
import { serverAddressToUnicode } from '../core/connection/idn';
import { LanguageService } from '../core/i18n/language.service';
import { mobileLogoBase, uniiaLogoBase } from '../core/i18n/logos';
import { IMAGE_MAX_BYTES, IMAGE_MAX_MEGABYTES, IMAGE_TYPES, IMAGE_TYPE_NAMES } from '../core/images/image-types';
import { NGO_NAME_MAX_LENGTH } from '../core/ngo/logo-rules';
import { DEFAULT_LOGIN_IMAGE, LoginPageImagesService } from '../core/login-page-images/login-page-images.service';
import { CreateNgoResult, NgoService, UploadNgoLogoResult } from '../core/ngo/ngo.service';
import { CreateUserResult, UsersService } from '../core/users/users.service';
import { isEmail } from '../shared/email/email';
import { ListTextPipe } from '../shared/list-text/list-text.pipe';
import { isStrongPassword } from '../shared/password-fields/password-rules';
import { PasswordFieldsComponent } from '../shared/password-fields/password-fields.component';
import { LoginDemoComponent } from './login-demo/login-demo.component';

/**
 * Що показує сторінка: перевірку підключення (або запит статусу системи),
 * крок 1 (адреса сервера), форму створення організації (у системі ще нема ГО),
 * форму налаштування адміністратора, помилку налаштування або форму входу.
 */
type LoginView = 'loading' | 'step1' | 'ngo' | 'admin' | 'setup-error' | 'login';

/** Помилка поля назви або логотипа у формі організації. */
type NgoNameError = 'required' | 'too-long';
type NgoLogoError = 'type' | 'size';

/** Помилка над формою кроку 1. */
type LoginError = 'invalid' | 'unreachable' | 'unhealthy';

@Component({
  selector: 'app-login',
  templateUrl: './login.page.html',
  styleUrls: ['./login.page.scss'],
  imports: [
    IonButton,
    IonContent,
    IonIcon,
    IonInput,
    SignInFormComponent,
    LoginDemoComponent,
    NgTemplateOutlet,
    FormsModule,
    PasswordFieldsComponent,
    ListTextPipe,
    TranslatePipe,
  ],
})
export class LoginPage implements OnDestroy {
  protected readonly connection = inject(ConnectionService);
  private readonly auth = inject(AuthService);
  private readonly accounts = inject(AccountsService);
  private readonly restoreService = inject(AccountRestoreService);
  private readonly ngoService = inject(NgoService);
  private readonly usersService = inject(UsersService);
  private readonly passwordPolicies = inject(PasswordPolicyService);
  private readonly languageService = inject(LanguageService);
  private readonly loginPageImages = inject(LoginPageImagesService);
  private readonly router = inject(Router);

  /** Вордмарк «Явір» вгорі форми входу (як мобільне лого меню; `.svg` / `_dark.svg`). */
  protected readonly logoBase = computed(() => mobileLogoBase(this.languageService.language()));

  /** Лого Унії за мовою (`uniiaLogoBase`): той самий принцип вибору, що й для лого «Явір». */
  protected readonly uniiaLogo = computed(() => uniiaLogoBase(this.languageService.language()));

  /**
   * Умови до пароля адміністратора — поточного сервера (на ньому його й створюють); змінилась
   * адреса сервера — умови беруться з нового.
   */
  protected readonly passwordPolicy = computed(() => {
    const server = this.connection.serverUrl();
    // Сам виклик (кеш і запит) — не частина обчислення: стежимо лише за сигналом умов.
    return untracked(() => this.passwordPolicies.policy(server))();
  });

  /** Те, що введено в поле кроку 1; початково — раніше надана адреса. */
  protected readonly address = signal(this.connection.serverAddress() ?? '');

  /** Адреса сервера для повідомлень: кириличний домен кирилицею, а не записом браузера (`xn--…`). */
  protected readonly shownServerUrl = computed(() => serverAddressToUnicode(this.connection.serverUrl()));

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

  /** Іде перевірка поточного сервера після натискання «змінити сервер». */
  protected readonly resettingServer = signal(false);

  /**
   * Пошук чинного акаунта на старті завершено, і жодного не знайдено (знайдено —
   * сторінка переходить на `REDIRECT_URL`, не показуючи форм). Поки триває —
   * «Перевіряємо підключення…».
   */
  private readonly restoreDone = signal(false);
  /** Пошук чинного акаунта (`restoreAccounts`) зараз триває. */
  private restoring = false;

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
   * Помилка над формою кроку 1. Про недоступність чи нездорове API кажемо, лише коли
   * користувач уже надавав адресу. Якщо ж просто відкрито /login і API за доменом
   * сторінки не знайшлося, це не помилка, а звичайний привід для кроку 1.
   */
  protected readonly formError = computed<LoginError | null>(() => {
    if (this.invalid()) {
      return 'invalid';
    }
    const status = this.connection.apiStatus();
    const failed = status === 'unreachable' || status === 'unhealthy';
    return this.connection.serverAddress() && failed ? status : null;
  });

  /** Форма створення організації. */
  protected readonly ngoName = signal('');
  protected readonly ngoLogo = signal<File | null>(null);
  protected readonly ngoLogoPreview = signal<string | null>(null);
  /** Значення `accept` вибору файлу логотипа (типи з `core/images/image-types.ts`). */
  protected readonly logoTypes = IMAGE_TYPES.join(',');
  /** Назви дозволених форматів для підказки й помилки (зʼєднує канал `listText`). */
  protected readonly imageTypeNames = IMAGE_TYPE_NAMES;
  /** Найбільший розмір зображення в мегабайтах для підказки й помилки (`{{size}}`). */
  protected readonly imageMaxMegabytes = IMAGE_MAX_MEGABYTES;
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

  /**
   * Помилка форми входу, що сталася вже після входу (ключ перекладу): живе тут, а не у формі,
   * бо поки запитується стан системи, сторінка показує «завантаження» і форма зникає.
   */
  protected readonly loginError = signal<string | null>(null);

  /** Форма входу (`app-sign-in-form`): кнопки демо-блоку входять через неї. */
  private readonly signInForm = viewChild(SignInFormComponent);
  /** Іде вхід у формі входу — кнопки демо-блоку вимкнено. */
  protected readonly signingIn = computed(() => this.signInForm()?.busy() ?? false);

  /** Після завершення демо-сеансу вже була нова спроба входу (її помилку покаже сама форма). */
  private readonly signInAttempted = signal(false);
  /**
   * Сюди повернуло завершення демо-сеансу (`AuthService.demoSessionEnded`): над формою входу —
   * пояснення, доки не почнеться нова спроба входу.
   */
  protected readonly demoSessionEnded = computed(() => this.auth.demoSessionEnded() && !this.signInAttempted());

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
    return isEmail(email) ? null : 'emailInvalid';
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
    return isStrongPassword(password, this.passwordPolicy()) ? null : 'passwordWeak';
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
    this.startRestore();

    // Стан первинного налаштування системи (від нього залежить, що показати: форму адміна,
    // організації чи входу) запитує сам `ConnectionService`, щойно підключення є.

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
    const server = this.connection.serverUrl();
    const image = await this.loginPageImages.random(server);
    // Поки йшов запит, змінили адресу: відповідь від попереднього сервера вже не потрібна (для
    // нового зображення запитує той самий ефект).
    if (server !== this.connection.serverUrl()) {
      return;
    }
    this.loginImage.set(image?.url ?? DEFAULT_LOGIN_IMAGE);
  }

  /** Зображення форми не завантажилося (битий файл, посилання) — повертаємо стандартне. */
  protected onLoginImageError(): void {
    if (this.loginImage() !== DEFAULT_LOGIN_IMAGE) {
      this.loginImage.set(DEFAULT_LOGIN_IMAGE);
    }
  }

  /**
   * Ionic тримає сторінку в стеку, і повернення на /login бере той самий екземпляр, без
   * конструктора. Якщо пошук тоді закінчився переходом геть (на /relogin чи «Планер»),
   * `restoreDone` лишився `false`, і без нового пошуку сторінка вічно «перевіряла б
   * підключення». Так буває, коли на /relogin видалили останній акаунт.
   */
  ionViewWillEnter(): void {
    if (!this.restoreDone() && !this.restoring) {
      this.startRestore();
    }
  }

  /**
   * Спершу шукаємо чинний акаунт серед збережених: знайшли — на «Планер», ні — показуємо
   * вхід, реєстрацію тощо. Акаунтів нема — шукати нічого, форми одразу.
   */
  private startRestore(): void {
    if (this.accounts.accounts().length === 0) {
      this.restoreDone.set(true);
    } else {
      void this.restoreAccounts();
    }
  }

  /**
   * Шукає чинний акаунт серед збережених (`AccountRestoreService`; застарілі
   * чистить). Знайшовся — переходимо на `REDIRECT_URL`; ні — на повторний вхід (збережені акаунти лишились) або відкриваємо форми.
   */
  private async restoreAccounts(): Promise<void> {
    this.restoring = true;
    const result = await this.restoreService.restore().finally(() => (this.restoring = false));
    if (result === 'restored') {
      // `restoreDone` лишається `false`: до переходу сторінка показує «завантаження».
      void this.router.navigateByUrl(REDIRECT_URL);
      return;
    }
    // Доступного акаунта нема, але збережені лишились (застарілі чи недоступні) — їх відновлюють
    // на сторінці повторного входу; форми тут лишаються, коли акаунтів нема взагалі.
    const url = signedOutUrl(this.accounts);
    if (url !== LOGIN_URL) {
      void this.router.navigateByUrl(url);
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

  /**
   * Кнопка «змінити сервер» (праворуч угорі кожної форми). Спершу один раз
   * перевіряє поточний сервер (`/health`); тоді очищає все сховище браузера для
   * застосунку (`AccountsService.wipe`: акаунти з токенами, мову, решту в
   * `localStorage` і `sessionStorage` вкладки), забуває надану адресу сервера (вона
   * живе в памʼяті `ConnectionService`) і введене у формах і повертає на крок 1.
   * Якщо сервер був здоровий — його адресу підставлено в поле кроку 1, щоб її було
   * легко поправити.
   */
  protected async resetServer(): Promise<void> {
    if (this.resettingServer()) {
      return;
    }
    this.resettingServer.set(true);
    // Беремо до очищення: після нього акаунти й надана адреса вже забуті. Адреси не надавали —
    // хост (із портом, якщо він нестандартний), за яким знайшлося API домену сторінки.
    const currentAddress =
      this.accounts.active()?.server ?? this.connection.serverAddress() ?? new URL(this.connection.serverUrl()).host;
    const healthy = await this.connection.probeApi();

    this.accounts.wipe();
    this.connection.clearServerAddress();
    this.resetForms();
    this.address.set(healthy ? serverAddressToUnicode(currentAddress) : '');
    this.invalid.set(false);
    this.submitted.set(false);
    this.forceAddressStep.set(true);
    this.resettingServer.set(false);
  }

  /** Забуває все, що введено й показано у формах входу, адміна та ГО. */
  private resetForms(): void {
    this.loginError.set(null);
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
    if (!IMAGE_TYPES.includes(file.type)) {
      this.ngoLogoError.set('type');
      return;
    }
    if (file.size > IMAGE_MAX_BYTES) {
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

  /** Почалась нова спроба входу у формі: її власна помилка замінить і помилку сторінки, і пояснення. */
  protected onSignInAttempt(): void {
    this.loginError.set(null);
    this.signInAttempted.set(true);
  }

  /**
   * Кнопка «Ввійти як …» демо-блоку: форма входу вписує пошту й пароль демо-акаунта й входить
   * звичайним шляхом (з тими самими помилками й `afterLogin`).
   */
  protected signInDemo(account: DemoAccount): Promise<void> {
    return this.signInForm()?.signInAs(account.email, account.password) ?? Promise.resolve();
  }

  /**
   * Після входу у формі входу (`app-sign-in-form`; токени вже записано): запитується статус
   * системи. Немає ГО — лишаємось на сторінці, і вона показує форму створення ГО. ГО є — одразу
   * на «Планер». Статус отримати не вдалося — форма показує про це помилку (`loginError`).
   */
  protected readonly afterLogin: AfterSignIn = async () => {
    const status = await this.connection.loadSystemStatus();
    if (!status) {
      this.loginError.set('login.form.error.statusFailed');
    } else if (status.hasNgo) {
      void this.router.navigateByUrl(REDIRECT_URL);
    }
    // Немає ГО: view() за статусом і токенами сам покаже форму створення ГО.
  };

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
        this.adminSaveError.set(adminCreateErrorKey(created));
        return;
      }
      this.adminCreated = true;
    }

    const login = await this.auth.login(email, password);
    this.adminSaving.set(false);
    if (login === 'ok') {
      this.connection.loadSystemStatus();
    } else {
      this.adminSaveError.set(adminLoginErrorKey(login));
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

/** Ключ помилки створення адміністратора (`login.admin.saveError.<ключ>`) за відповіддю сервера. */
function adminCreateErrorKey(result: Exclude<CreateUserResult, 'created'>): string {
  switch (result) {
    case 'invalid':
      return 'createInvalid';
    case 'unreachable':
      return 'createUnreachable';
    default:
      return 'createError';
  }
}

/**
 * Ключ помилки входу щойно створеного адміністратора (`login.admin.saveError.<ключ>`): тексти
 * інші, ніж у формі входу (`loginErrorKey` з `account-login.ts`), — у них сказано, що
 * адміністратора вже створено.
 */
function adminLoginErrorKey(result: Exclude<LoginResult, 'ok'>): string {
  switch (result) {
    case 'storageFull':
      return 'loginStorageFull';
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
