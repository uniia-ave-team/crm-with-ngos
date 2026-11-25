import { Component, DestroyRef, ElementRef, computed, effect, inject, input, output, signal, untracked, viewChild } from '@angular/core';
import { TranslateService, TranslatePipe } from '@ngx-translate/core';
import { IonIcon } from '@ionic/angular';

import { IMAGE_MAX_BYTES, IMAGE_MAX_MEGABYTES, IMAGE_TYPES, IMAGE_TYPE_NAMES } from '../../core/images/image-types';
import { LANGUAGES, LanguageOption } from '../../core/i18n/languages.generated';
import { PRONOUN_CATEGORY } from '../../core/users/pronouns';
import { UserProfile } from '../../core/users/user-profile';
import { ProfileUpdate } from '../../core/users/users.service';
import { AvatarComponent } from '../avatar/avatar.component';
import { ListTextPipe } from '../list-text/list-text.pipe';
import { isPhoneNumber } from '../phone/phone-number';

const ERROR_PREFIX = 'settings.personal.error.';

/** Вибір у займенниках: один із трьох готових варіантів або «Інше» з власним текстом. */
export type PronounChoice = 'she' | 'he' | 'they' | 'other';

/** Ключі підписів готових варіантів у мовному файлі. */
const PRONOUN_LABELS = { she: 'pronounShe', he: 'pronounHe', they: 'pronounThey' } as const;

/** Категорія займенників (`PronounCategory` на сервері) кожного готового варіанта. */
const PRONOUN_CHOICE_CATEGORY = {
  she: PRONOUN_CATEGORY.feminine,
  he: PRONOUN_CATEGORY.masculine,
  they: PRONOUN_CATEGORY.neutral,
} as const;

/** Що віддає «Зберегти»: тіло `PUT` профілю й обраний файл аватара (`null` — не обрано). */
export interface ProfileFormValue {
  readonly profile: ProfileUpdate;
  readonly avatar: File | null;
}

/**
 * Форма даних профілю: прізвище, імʼя, по батькові, аватар, займенники, телефон, екстрений
 * контакт, країна й мова інтерфейсу та «Зберегти». Її показують «Дані профілю» в налаштуваннях
 * (власний профіль) і бічне вікно «Редагувати» на сторінці колег (профіль колеги).
 *
 *   <app-profile-form idPrefix="settings" [profile]="user()" [saving]="saving()"
 *                     (changed)="…" (save)="onSave($event)" />
 *
 * Поля заповнюються з `profile`, доки в них нічого не змінювали; далі їх змінює користувач, а
 * новий `profile` (його можуть перепитати у фоні) введеного не стирає. `markSaved()` після
 * вдалого збереження знову привʼязує поля до `profile`. «Зберегти» перевіряє обовʼязкові поля
 * й віддає подією `save` тіло запиту (порожні необовʼязкові поля — `null`) і обраний файл
 * аватара; сама форма нічого не надсилає. `changed` — змінено поле чи натиснуто «Зберегти»:
 * попередній результат збереження вже неактуальний. `clearAvatar()` забуває обраний файл (після
 * його завантаження). Поки `saving`, кнопка вимкнена.
 *
 * Те, що передано всередину (`ng-content`), стоїть перед «Зберегти» — наприклад, ролі колеги;
 * у `save` воно не входить: його значення тримає сторінка.
 *
 * `idPrefix` — початок `id` полів (щоб дві форми на сторінках у стеку Ionic не мали однакових
 * `id`). Без `showEmergencyContact` поля екстреного контакту нема, а в тілі він `null`.
 */
@Component({
  selector: 'app-profile-form',
  templateUrl: './profile-form.component.html',
  styleUrls: ['../../settings/settings-section.scss', '../../settings/avatar-block.scss', './profile-form.component.scss'],
  imports: [AvatarComponent, IonIcon, ListTextPipe, TranslatePipe],
})
export class ProfileFormComponent {
  private readonly translate = inject(TranslateService);

  /** Профіль, з якого заповнюються поля; `null` — ще нема. */
  readonly profile = input<UserProfile | null>(null);
  /** Назва ГО: входить у колір аватара, як і в боковому меню. */
  readonly ngoName = input('');
  /** Іде збереження: кнопка вимкнена, повторне натискання нічого не робить. */
  readonly saving = input(false);
  readonly idPrefix = input('profile');
  readonly showEmergencyContact = input(true);
  /** Ключ підпису поля власних займенників («Інше»). */
  readonly otherPronounsLabelKey = input('settings.personal.pronounOtherInput');

  /** Дані форми готові до відправки (обовʼязкові поля заповнено). */
  readonly save = output<ProfileFormValue>();
  /** Змінено поле чи натиснуто «Зберегти». */
  readonly changed = output<void>();

  // Значення полів форми.
  protected readonly lastName = signal('');
  protected readonly firstName = signal('');
  protected readonly patronymic = signal('');
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

  /** Користувач уже змінив щось у полях: новий `profile` поля більше не перезаписує. */
  private readonly edited = signal(false);
  /** Була спроба збереження: помилки обовʼязкових полів показуємо лише після неї. */
  private readonly attempted = signal(false);

  // Помилки обовʼязкових полів (ключі перекладу; `null` — помилки нема).
  private readonly requiredError = (value: () => string) =>
    computed(() => (this.attempted() && !value().trim() ? `${ERROR_PREFIX}required` : null));
  protected readonly lastNameError = this.requiredError(() => this.lastName());
  protected readonly firstNameError = this.requiredError(() => this.firstName());

  /** Телефон необовʼязковий, але якщо вказаний — має бути схожим на номер. */
  protected readonly phoneError = computed(() => {
    const phone = this.phone().trim();
    return this.attempted() && phone && !isPhoneNumber(phone) ? `${ERROR_PREFIX}phoneInvalid` : null;
  });

  /** Не обрано жодних займенників (обовʼязкове поле — група). */
  protected readonly pronounsError = computed(() =>
    this.attempted() && this.pronounChoice() === null ? `${ERROR_PREFIX}pronounsRequired` : null,
  );
  /** Вибрано «Інше», але власних займенників не вказано. */
  protected readonly otherPronounsError = computed(() =>
    this.attempted() && this.otherPronouns() && !this.otherPronounsText().trim()
      ? `${ERROR_PREFIX}pronounOtherRequired`
      : null,
  );

  /** Значення `accept` вибору файлу аватара (типи з `core/images/image-types.ts`). */
  protected readonly avatarTypes = IMAGE_TYPES.join(',');
  /** Назви дозволених форматів для підказки й помилки (зʼєднує канал `listText`). */
  protected readonly imageTypeNames = IMAGE_TYPE_NAMES;
  /** Найбільший розмір зображення в мегабайтах для підказки й помилки (`{{size}}`). */
  protected readonly imageMaxMegabytes = IMAGE_MAX_MEGABYTES;
  /** Ключ помилки під блоком аватара (файл не підійшов); `null` — помилки нема. */
  protected readonly avatarError = signal<string | null>(null);
  /** Імʼя обраного файлу аватара; `null` — файл не обрано. */
  protected readonly fileName = signal<string | null>(null);
  /** Обраний файл аватара (його віддає «Зберегти»); `null` — файл не обрано. */
  private readonly chosenAvatar = signal<File | null>(null);
  private readonly avatarInput = viewChild<ElementRef<HTMLInputElement>>('avatarFile');

  /**
   * Прев'ю обраного зображення (`blob:`-посилання на файл у браузері); `null` —
   * файл не обрано. Поки воно є, аватар показує його замість поточного.
   */
  protected readonly previewUrl = signal<string | null>(null);

  /** Що показує аватар: обране зображення, інакше аватар із профілю. */
  protected readonly avatarUrl = computed(() => this.previewUrl() ?? this.profile()?.avatarUrl);

  constructor() {
    // Посилання на обране зображення живе, поки є форма.
    inject(DestroyRef).onDestroy(() => this.releasePreview());

    // Профіль — у поля форми, доки в них нічого не змінювали.
    effect(() => {
      const profile = this.profile();
      untracked(() => {
        if (!this.edited()) {
          this.fillFrom(profile);
        }
      });
    });
  }

  /** Збережено: помилки ховаються, а поля знову йдуть за `profile`. */
  markSaved(): void {
    this.attempted.set(false);
    this.edited.set(false);
    this.fillFrom(this.profile());
  }

  /** Аватар завантажено: обраний файл і превʼю більше не потрібні (тепер аватар — з сервера). */
  clearAvatar(): void {
    this.releasePreview();
    this.fileName.set(null);
    this.chosenAvatar.set(null);
    const input = this.avatarInput()?.nativeElement;
    if (input) {
      input.value = '';
    }
  }

  protected id(name: string): string {
    return `${this.idPrefix()}-${name}`;
  }

  protected onFieldChange(field: { set(value: string): void }, event: Event): void {
    field.set((event.target as HTMLInputElement).value);
    this.edited.set(true);
    this.changed.emit();
  }

  protected onPronounChange(choice: PronounChoice, event: Event): void {
    if ((event.target as HTMLInputElement).checked) {
      this.pronounChoice.set(choice);
      this.edited.set(true);
      this.changed.emit();
    }
  }

  protected onFileChosen(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.releasePreview();

    this.avatarError.set(null);

    // Файл не обрано, він не з дозволених типів (фільтр діалогу можна обійти) або завеликий
    // для сервера: лишається поточний аватар, а напис «Файлу не обрано.»; для невідповідного
    // файлу — ще й помилка під блоком.
    const problem = !file ? null : !IMAGE_TYPES.includes(file.type) ? 'avatarType' : file.size > IMAGE_MAX_BYTES ? 'avatarSize' : null;
    if (!file || problem) {
      this.fileName.set(null);
      this.chosenAvatar.set(null);
      if (problem) {
        this.avatarError.set(`${ERROR_PREFIX}${problem}`);
      }
      input.value = '';
      return;
    }
    this.fileName.set(file.name);
    this.chosenAvatar.set(file);
    this.previewUrl.set(URL.createObjectURL(file));
  }

  /** «Зберегти»: перевіряє поля й, якщо все гаразд, віддає тіло запиту й файл аватара. */
  protected submit(): void {
    if (this.saving()) {
      return;
    }
    this.attempted.set(true);
    this.changed.emit();
    if (
      this.lastNameError() ||
      this.firstNameError() ||
      this.phoneError() ||
      this.pronounsError() ||
      this.otherPronounsError()
    ) {
      return;
    }
    this.save.emit({ profile: this.buildBody(), avatar: this.chosenAvatar() });
  }

  /** Тіло запиту: порожні рядки — `null`; фото йде окремим запитом. */
  private buildBody(): ProfileUpdate {
    const nullable = (value: string) => value.trim() || null;
    return {
      // Імʼя й прізвище обовʼязкові (форму без них не надіслати), і сервер приймає їх лише рядками.
      firstName: this.firstName().trim(),
      lastName: this.lastName().trim(),
      patronymic: nullable(this.patronymic()),
      phoneNumber: nullable(this.phone()),
      country: nullable(this.country()),
      // Без поля (нема права бачити контакт) — `null`: сервер тоді контакт не змінює.
      emergencyContact: this.showEmergencyContact() ? nullable(this.emergencyContact()) : null,
      preferredLanguage: nullable(this.preferredLanguage()),
      pronouns: this.pronounsText(),
      pronounCategory: this.pronounCategory(),
    };
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
    return this.translate.instant(`settings.personal.${PRONOUN_LABELS[choice]}`);
  }

  /** Категорія займенників: за готовим варіантом; «Інше» категорії не має (`null`). */
  private pronounCategory(): number | null {
    const choice = this.pronounChoice();
    return choice === null || choice === 'other' ? null : PRONOUN_CHOICE_CATEGORY[choice];
  }

  /** Заповнює поля з профілю; без профілю нічого не робить. */
  private fillFrom(profile: UserProfile | null): void {
    if (!profile) {
      return;
    }
    this.lastName.set(profile.lastName ?? '');
    this.firstName.set(profile.firstName ?? '');
    this.patronymic.set(profile.patronymic ?? '');
    this.phone.set(profile.phoneNumber ?? '');
    this.emergencyContact.set(profile.emergencyContact ?? '');
    this.country.set(profile.country ?? '');
    this.preferredLanguage.set(profile.preferredLanguage ?? '');
    this.fillPronouns(profile.pronounCategory ?? null, profile.pronouns ?? '');
  }

  /**
   * Займенники профілю назад у радіокнопки. Готовий варіант вибирає категорія (`pronounCategory`),
   * а не текст: текст записано мовою, яка була на момент збереження, і за ним варіант не впізнати.
   * Без категорії текст — це «Інше» з власним текстом; нема й тексту — нічого не вибрано.
   */
  private fillPronouns(category: number | null, pronouns: string): void {
    const known = (Object.keys(PRONOUN_CHOICE_CATEGORY) as Exclude<PronounChoice, 'other'>[]).find(
      (choice) => PRONOUN_CHOICE_CATEGORY[choice] === category,
    );
    const text = pronouns.trim();
    this.pronounChoice.set(known ?? (text ? 'other' : null));
    this.otherPronounsText.set(known ? '' : text);
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
