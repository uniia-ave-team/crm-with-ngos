import { Component, computed, effect, inject, input, OnDestroy, signal, untracked, WritableSignal } from '@angular/core';

import { NO_CONNECTION_ERROR } from '../core/connection/connection.guard';
import { ConnectionService } from '../core/connection/connection.service';
import { CreateNgoResult, NgoService } from '../core/ngo/ngo.service';

/**
 * Що показує сторінка: перевірку підключення (або запит статусу системи),
 * крок 1 (адреса сервера), форму створення організації (у системі ще нема ГО),
 * форму налаштування адміністратора, помилку налаштування або форму входу.
 */
type LoginView = 'loading' | 'step1' | 'ngo' | 'admin' | 'setup-error' | 'login';

/** Помилка поля назви або логотипа у формі організації. */
type NgoNameError = 'required' | 'too-long';
type NgoLogoError = 'type' | 'size';

/** Пароль адміністратора: довше за 5 символів + велика, мала літера й цифра. */
const ADMIN_PASSWORD_MIN_LENGTH = 6;
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

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

  protected readonly view = computed<LoginView>(() => {
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
   * Що показати за статусом системи:
   * - `isSetupComplete` — форма входу;
   * - немає ГО — форма створення ГО;
   * - ГО є, адміна нема — форма налаштування адміністратора;
   * - ГО й адмін є, а налаштування не завершене — помилка.
   * Якщо статус не вдалося отримати — форма входу.
   */
  private setupView(): LoginView {
    const status = this.connection.systemStatus();
    if (!status || status.isSetupComplete) {
      return 'login';
    }
    if (!status.hasNgo) {
      return 'ngo';
    }
    return status.hasAdmin ? 'setup-error' : 'admin';
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

  /** Форма адміністратора. */
  protected readonly adminEmail = signal('');
  protected readonly adminLastName = signal('');
  protected readonly adminFirstName = signal('');
  protected readonly adminPassword = signal('');
  protected readonly adminPasswordRepeat = signal('');
  private readonly adminSubmitted = signal(false);

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
  /** Помилка прізвища — блоком над формою (ключ перекладу). */
  protected readonly adminBannerErrors = computed<string[]>(() => {
    const code = this.adminLastNameError();
    return code ? [`login.admin.error.${code}`] : [];
  });
  protected readonly adminPasswordError = computed(() => {
    if (!this.adminSubmitted()) {
      return null;
    }
    const password = this.adminPassword();
    if (!password) {
      return 'passwordRequired';
    }
    const strong =
      password.length >= ADMIN_PASSWORD_MIN_LENGTH &&
      /\p{Lu}/u.test(password) &&
      /\p{Ll}/u.test(password) &&
      /\d/.test(password);
    return strong ? null : 'passwordWeak';
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
      if (this.connection.apiStatus() === 'healthy') {
        untracked(() => this.connection.loadSystemStatus());
      }
    });
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
    this.invalid.set(!this.connection.setServerAddress(this.address()));
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
   * `logoUrl` завжди `null`). Після успіху перезапитує статус системи —
   * `hasNgo` стає `true`, і сторінка переходить до форми входу.
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
      this.connection.loadSystemStatus();
    } else {
      this.ngoSaveError.set(result);
    }
  }

  protected onAdminFieldChange(
    field: WritableSignal<string>,
    value: string | number | null | undefined,
  ): void {
    field.set(value == null ? '' : String(value));
  }

  /**
   * Перевіряє форму адміністратора ще до будь-якого запиту. Сам запит
   * поки не готуємо: після успішної перевірки нічого не відбувається.
   */
  protected submitAdmin(): void {
    this.adminSubmitted.set(true);
  }

  private revokePreview(): void {
    const url = this.ngoLogoPreview();
    if (url) {
      URL.revokeObjectURL(url);
      this.ngoLogoPreview.set(null);
    }
  }
}
