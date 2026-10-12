import { Component, DestroyRef, ElementRef, computed, effect, inject, signal, untracked } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

import { AccountsService } from '../../core/accounts/accounts.service';
import { CurrentUserService } from '../../core/users/current-user.service';
import { UserProfile } from '../../core/users/user-profile';
import { UsersService } from '../../core/users/users.service';
import { LANGUAGES, LanguageOption } from '../../core/i18n/languages.generated';
import { isPhoneNumber } from '../../shared/phone/phone-number';

/** Що показати в блоці над формою за результатом останнього збереження. */
type SaveResult = 'success' | 'invalid' | 'forbidden' | 'unreachable' | 'error';

const ERROR_PREFIX = 'settings.personal.error.';

/** Значення `PronounCategory` на сервері (категорія займенників для звертання). */
const PRONOUN_CATEGORY = { masculine: 1, feminine: 2, neutral: 3 } as const;

/** Вибір у займенниках: один із трьох готових варіантів або «Інше» з власним текстом. */
export type PronounChoice = 'she' | 'he' | 'they' | 'other';

/** Ключі підписів готових варіантів у мовному файлі. */
const PRONOUN_LABELS = { she: 'pronounShe', he: 'pronounHe', they: 'pronounThey' } as const;

/**
 * Вкладка «Дані профілю» (`/settings/personal-data`). Під час відкриття
 * оновлює профіль із сервера (`GET /users/{id}`) і заповнює з нього поля;
 * «Зберегти» перевіряє обовʼязкові поля й шле `PUT /users/{id}`.
 *
 * Фото поки не зберігається: аватар у запиті завжди `null`, а обране зображення
 * лише показується в прев'ю.
 */
@Component({
  selector: 'app-settings-personal-data',
  templateUrl: './personal-data.page.html',
  styleUrls: ['../settings-section.scss', './personal-data.page.scss'],
  standalone: false,
})
export class PersonalDataPage {
  private readonly accounts = inject(AccountsService);
  private readonly currentUser = inject(CurrentUserService);
  private readonly users = inject(UsersService);
  private readonly translate = inject(TranslateService);
  private readonly host: ElementRef<HTMLElement> = inject(ElementRef);

  /** Поточний користувач (профіль із `GET /api/v1/users/me`); `null`, доки не завантажено. */
  protected readonly user = this.currentUser.user;

  // Значення полів форми. Заповнюються з профілю (див. effect нижче), далі їх
  // змінює користувач.
  protected readonly lastName = signal('');
  protected readonly firstName = signal('');
  protected readonly patronymic = signal('');
  protected readonly position = signal('');
  protected readonly email = signal('');
  protected readonly phone = signal('');
  protected readonly emergencyContact = signal('');
  protected readonly country = signal('');
  /** Код обраної мови інтерфейсу (імʼя файлу перекладів); порожньо — не обрано. */
  protected readonly preferredLanguage = signal('');

  /**
   * Мови для списку: усі, для яких є файл перекладів. Якщо в профілі збережено
   * код, якого в цьому списку нема, він додається як є (назва — сам код), щоб
   * збереження не стерло чужий вибір.
   */
  protected readonly languageOptions = computed<readonly LanguageOption[]>(() => {
    const current = this.preferredLanguage();
    return current && !LANGUAGES.some((language) => language.code === current)
      ? [...LANGUAGES, { code: current, name: current }]
      : LANGUAGES;
  });

  // Займенники: один варіант (радіокнопки); «Інше» має власний текст.
  protected readonly pronounChoice = signal<PronounChoice | null>(null);
  protected readonly otherPronounsText = signal('');
  /** Вибрано «Інше» у займенниках: показуємо поле для власних займенників. */
  protected readonly otherPronouns = computed(() => this.pronounChoice() === 'other');

  /** Була спроба збереження: помилки обовʼязкових полів показуємо лише після неї. */
  private readonly attempted = signal(false);
  protected readonly saving = signal(false);
  /** Результат останнього збереження (блок над формою); `null` — ще не було. */
  protected readonly result = signal<SaveResult | null>(null);

  // Помилки обовʼязкових полів (ключі перекладу; `null` — помилки нема).
  private readonly requiredError = (value: () => string) =>
    computed(() => (this.attempted() && !value().trim() ? `${ERROR_PREFIX}required` : null));
  protected readonly lastNameError = this.requiredError(() => this.lastName());
  protected readonly firstNameError = this.requiredError(() => this.firstName());
  protected readonly emailError = this.requiredError(() => this.email());

  /** Телефон необовʼязковий, але якщо вказаний — має бути схожим на номер. */
  protected readonly phoneError = computed(() => {
    const phone = this.phone().trim();
    return this.attempted() && phone && !isPhoneNumber(phone) ? `${ERROR_PREFIX}phoneInvalid` : null;
  });

  /** Не обрано жодних займенників (обовʼязкове поле — група). */
  protected readonly pronounsError = computed(() =>
    this.attempted() && !this.hasAnyPronoun() ? `${ERROR_PREFIX}pronounsRequired` : null,
  );
  /** Вибрано «Інше», але власних займенників не вказано. */
  protected readonly otherPronounsError = computed(() =>
    this.attempted() && this.otherPronouns() && !this.otherPronounsText().trim()
      ? `${ERROR_PREFIX}pronounOtherRequired`
      : null,
  );

  /** Ключ тексту блока над формою. */
  protected readonly resultKey = computed(() => {
    const result = this.result();
    return result ? `settings.personal.result.${result}` : null;
  });

  /** Назва ГО активного акаунта: входить у колір аватара, як і в боковому меню. */
  protected readonly ngoName = computed(() => this.accounts.active()?.ngoName ?? '');

  /** Імʼя обраного файлу аватара (завантаження на сервер ще нема). */
  protected readonly fileName = signal<string | null>(null);

  /**
   * Прев'ю обраного зображення (`blob:`-посилання на файл у браузері); `null` —
   * файл не обрано. Поки воно є, аватар показує його замість поточного.
   */
  protected readonly previewUrl = signal<string | null>(null);

  /** Що показує аватар: обране зображення, інакше аватар із профілю. */
  protected readonly avatarUrl = computed(() => this.previewUrl() ?? this.user()?.avatarUrl);

  constructor() {
    // Посилання на обране зображення живе, поки відкрита вкладка.
    inject(DestroyRef).onDestroy(() => this.releasePreview());

    // Профіль (з /users/me, з GET /users/{id} чи після збереження) — у поля форми.
    effect(() => {
      const user = this.user();
      untracked(() => this.fillFrom(user));
    });

    // Відкрили вкладку — підтягуємо детальний профіль (`GET /users/{id}`): поля
    // форми й знімок акаунта в `localStorage` оновлюються з відповіді.
    void this.currentUser.refresh();
  }

  protected onFieldChange(field: { set(value: string): void }, event: Event): void {
    field.set((event.target as HTMLInputElement).value);
    this.result.set(null);
  }

  protected onPronounChange(choice: PronounChoice, event: Event): void {
    if ((event.target as HTMLInputElement).checked) {
      this.pronounChoice.set(choice);
      this.result.set(null);
    }
  }

  protected onFileChosen(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.releasePreview();

    // Не зображення (фільтр діалогу обійдено) або файл не обрано: лишається
    // поточний аватар, а напис «Файлу не обрано.».
    if (!file || !file.type.startsWith('image/')) {
      this.fileName.set(null);
      input.value = '';
      return;
    }
    this.fileName.set(file.name);
    this.previewUrl.set(URL.createObjectURL(file));
  }

  /**
   * «Зберегти»: перевіряє обовʼязкові поля й, якщо все гаразд, шле `PUT
   * /users/{id}`. Порожні необовʼязкові поля йдуть як `null`. Пошти в запиті
   * нема: API не дозволяє змінити її цим запитом.
   */
  protected async submit(): Promise<void> {
    if (this.saving()) {
      return;
    }
    this.attempted.set(true);
    this.result.set(null);
    if (
      this.lastNameError() ||
      this.firstNameError() ||
      this.emailError() ||
      this.phoneError() ||
      this.pronounsError() ||
      this.otherPronounsError()
    ) {
      return;
    }
    const id = this.user()?.id ?? this.accounts.active()?.userId;
    if (!id) {
      this.result.set('error');
      return;
    }

    const body = this.buildBody();
    this.saving.set(true);
    const outcome = await this.users.updateProfile(id, body);
    this.saving.set(false);

    if (outcome === 'updated') {
      this.attempted.set(false);
      this.result.set('success');
      this.scrollToTop();
      // Те, що збережено, одразу видно в меню й знімку акаунта, без нового запиту.
      this.currentUser.applyLocal({
        firstName: body.firstName ?? '',
        lastName: body.lastName ?? '',
        patronymic: body.patronymic,
        internalPosition: body.internalPosition,
        phoneNumber: body.phoneNumber,
        country: body.country,
        emergencyContact: body.emergencyContact,
        preferredLanguage: body.preferredLanguage,
        pronouns: body.pronouns,
      });
      return;
    }
    this.result.set(
      outcome === 'invalid' || outcome === 'forbidden' || outcome === 'unreachable' ? outcome : 'error',
    );
    this.scrollToTop();
  }

  /**
   * Результат показується над формою, а кнопка внизу: після відповіді прокручуємо
   * вміст сторінки (його `ion-content`) нагору, щоб блок було видно.
   */
  private scrollToTop(): void {
    const content = this.host.nativeElement.closest('ion-content') as HTMLIonContentElement | null;
    void content?.scrollToTop?.(300);
  }

  /** Тіло запиту: порожні рядки — `null`, фото — завжди `null`. */
  private buildBody() {
    const nullable = (value: string) => value.trim() || null;
    return {
      firstName: nullable(this.firstName()),
      lastName: nullable(this.lastName()),
      patronymic: nullable(this.patronymic()),
      internalPosition: nullable(this.position()),
      phoneNumber: nullable(this.phone()),
      country: nullable(this.country()),
      emergencyContact: nullable(this.emergencyContact()),
      preferredLanguage: nullable(this.preferredLanguage()),
      pronouns: this.pronounsText(),
      pronounCategory: this.pronounCategory(),
      avatarUrl: null,
    };
  }

  private hasAnyPronoun(): boolean {
    return this.pronounChoice() !== null;
  }

  /**
   * Текст займенників для сервера: підпис обраного варіанта або власний текст
   * «Інше»; `null`, якщо нічого не обрано чи «Інше» без тексту.
   */
  private pronounsText(): string | null {
    const choice = this.pronounChoice();
    if (choice === null) {
      return null;
    }
    if (choice === 'other') {
      return this.otherPronounsText().trim() || null;
    }
    return this.label(choice);
  }

  /** Категорія займенників: за готовим варіантом; «Інше» категорії не має (`null`). */
  private pronounCategory(): number | null {
    switch (this.pronounChoice()) {
      case 'she':
        return PRONOUN_CATEGORY.feminine;
      case 'he':
        return PRONOUN_CATEGORY.masculine;
      case 'they':
        return PRONOUN_CATEGORY.neutral;
      default:
        return null;
    }
  }

  private label(choice: Exclude<PronounChoice, 'other'>): string {
    return this.translate.instant(`settings.personal.${PRONOUN_LABELS[choice]}`);
  }

  /** Заповнює поля з профілю; без профілю нічого не робить. */
  private fillFrom(user: UserProfile | null): void {
    if (!user) {
      return;
    }
    this.lastName.set(user.lastName ?? '');
    this.firstName.set(user.firstName ?? '');
    this.patronymic.set(user.patronymic ?? '');
    this.position.set(user.internalPosition ?? '');
    this.email.set(user.email ?? '');
    this.phone.set(user.phoneNumber ?? '');
    this.emergencyContact.set(user.emergencyContact ?? '');
    this.country.set(user.country ?? '');
    this.preferredLanguage.set(user.preferredLanguage ?? '');
    this.fillPronouns(user.pronouns ?? '');
  }

  /**
   * Розкладає рядок `pronouns` профілю назад у радіокнопки: точний збіг із
   * підписом готового варіанта вибирає його, будь-що інше (зокрема кілька
   * значень, збережених раніше) іде в «Інше» як власний текст.
   */
  private fillPronouns(pronouns: string): void {
    const text = pronouns.trim();
    if (!text) {
      this.pronounChoice.set(null);
      this.otherPronounsText.set('');
      return;
    }
    const known = (Object.keys(PRONOUN_LABELS) as Exclude<PronounChoice, 'other'>[]).find(
      (choice) => this.label(choice) === text,
    );
    if (known) {
      this.pronounChoice.set(known);
      this.otherPronounsText.set('');
    } else {
      this.pronounChoice.set('other');
      this.otherPronounsText.set(text);
    }
  }

  /** Звільняє памʼять, яку тримає посилання на попереднє обране зображення. */
  private releasePreview(): void {
    const url = this.previewUrl();
    if (url) {
      URL.revokeObjectURL(url);
      this.previewUrl.set(null);
    }
  }
}
