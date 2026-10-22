import { Component, DestroyRef, ElementRef, computed, effect, inject, signal, untracked, viewChild } from '@angular/core';

import { AccountsService } from '../../core/accounts/accounts.service';
import { GALLERY_PERMISSIONS, PERMISSIONS, hasAnyPermission } from '../../core/auth/permissions';
import { ConnectionService } from '../../core/connection/connection.service';
import { NGO_LOGO_MAX_BYTES, NGO_LOGO_TYPES, NGO_NAME_MAX_LENGTH } from '../../core/ngo/logo-rules';
import { NgoService, UpdateNgoResult } from '../../core/ngo/ngo.service';
import { CurrentUserService } from '../../core/users/current-user.service';
import { AdminImagesGalleryComponent } from '../images-gallery/images-gallery.component';

/** Чому обраний файл не підійшов (ключ у `admin.error.*`). */
type LogoError = 'logoType' | 'logoSize';

/** Чим закінчилася частина збереження, що не вдалася (ключі в `admin.result.*Failed.*`). */
type PartFailure = Exclude<UpdateNgoResult, 'updated'>;

/** Рядок результату збереження: що саме змінено (або не вдалося змінити). */
interface ResultLine {
  /** Ключ тексту в мовному файлі. */
  readonly key: string;
  readonly ok: boolean;
}

/**
 * Форма організації на сторінці «Адмін»: назва й логотип (мініатюра, вибір
 * файлу, підказки — як у блоці аватара на «Даних профілю»). Початкові значення —
 * знімок організації в записі активного акаунта (`ngoName`, `ngoLogoUrl`), який
 * оновлюється разом із профілем при кожній зміні сторінки.
 *
 * Галерея зображень сторінки входу (`app-admin-images-gallery`) — теж частина
 * цієї форми, а «Зберегти» під нею одна на все.
 *
 * Що показується, залежить від прав користувача: назва й логотип — лише з
 * `Permissions.UpdateNgo`, галерея — з `Permissions.CreateLoginPageImages` або
 * `Permissions.DeleteLoginPageImages` (які саме кнопки в ній, вирішує сама
 * галерея).
 *
 * «Зберегти» (докладно — в `submit()`): з правом `UpdateNgo` назва йде
 * `PUT /api/v1/ngo`, а якщо обрано файл логотипа — ще й додатковим запитом
 * `POST /api/v1/ngo/logo` із самим зображенням; не обрано — цього запиту нема.
 * Галерея зберігає свої зміни окремо. Результат — блок із рядками про те, що
 * змінено й що не вдалося.
 */
@Component({
  selector: 'app-admin-ngo-form',
  templateUrl: './ngo-form.component.html',
  styleUrls: ['../../settings/settings-section.scss', '../../settings/avatar-block.scss', './ngo-form.component.scss'],
  standalone: false,
})
export class AdminNgoFormComponent {
  private readonly accounts = inject(AccountsService);
  private readonly connection = inject(ConnectionService);
  private readonly ngo = inject(NgoService);
  private readonly permissions = inject(CurrentUserService).permissions;

  /** Іде збереження: кнопка заблокована. */
  protected readonly saving = signal(false);
  /**
   * Рядки результату останнього збереження: що саме змінено й що не вдалося.
   * Порожньо — ще не зберігали (або нічого не змінювали, або поле відредагували).
   */
  protected readonly resultLines = signal<readonly ResultLine[]>([]);
  /** Усі рядки результату — успіх (зелений блок); інакше блок червоний. */
  protected readonly resultOk = computed(() => this.resultLines().every((line) => line.ok));

  /** Галерея зображень сторінки входу (її нема, якщо немає прав її бачити). */
  private readonly gallery = viewChild(AdminImagesGalleryComponent);

  /** Може змінювати назву й логотип організації. */
  protected readonly canEditNgo = computed(() => this.permissions().includes(PERMISSIONS.updateNgo));
  /** Бачить галерею зображень сторінки входу (може додавати або видаляти). */
  protected readonly canSeeGallery = computed(() => hasAnyPermission(this.permissions(), GALLERY_PERMISSIONS));

  protected readonly nameMaxLength = NGO_NAME_MAX_LENGTH;
  protected readonly logoTypes = NGO_LOGO_TYPES.join(',');

  protected readonly name = signal('');
  /** Назву вже редагували: оновлення знімка її більше не перезаписує. */
  private readonly nameEdited = signal(false);
  /** Була спроба збереження: помилки полів показуємо лише після неї. */
  private readonly attempted = signal(false);

  /** Імʼя обраного файлу логотипа; `null` — файл не обрано. */
  protected readonly fileName = signal<string | null>(null);
  /** Обраний файл логотипа (його надсилає збереження); `null` — файл не обрано. */
  private readonly chosenLogo = signal<File | null>(null);
  private readonly logoInput = viewChild<ElementRef<HTMLInputElement>>('logoFile');
  /** Мініатюра обраного файлу (`blob:`-посилання); `null` — файл не обрано. */
  protected readonly previewUrl = signal<string | null>(null);
  protected readonly logoError = signal<LogoError | null>(null);

  /** Що показує мініатюра: обраний файл, інакше поточний логотип організації. */
  protected readonly logoUrl = computed(() => this.previewUrl() ?? this.accounts.active()?.ngoLogoUrl ?? null);

  /** Адреса логотипа, який не вдалося завантажити (для неї лишається порожній блок). */
  private readonly failedLogoUrl = signal<string | null>(null);

  /**
   * Що малювати в мініатюрі: адреса логотипа (відносну рахуємо від адреси
   * сервера) або `null` — тоді порожній блок із пунктиром.
   */
  protected readonly logoSrc = computed(() => {
    const url = this.logoUrl()?.trim();
    if (!url || url === this.failedLogoUrl()) {
      return null;
    }
    try {
      return new URL(url, `${this.connection.serverUrl()}/`).toString();
    } catch {
      return null;
    }
  });

  protected readonly nameError = computed(() =>
    this.attempted() && !this.name().trim() ? 'admin.error.nameRequired' : null,
  );

  constructor() {
    // Посилання на обраний файл живе, поки відкрита сторінка.
    inject(DestroyRef).onDestroy(() => this.releasePreview());

    // Назва з знімка організації — у поле, доки користувач її не змінював.
    effect(() => {
      const saved = this.accounts.active()?.ngoName ?? '';
      untracked(() => {
        if (!this.nameEdited()) {
          this.name.set(saved);
        }
      });
    });
  }

  protected onLogoError(): void {
    this.failedLogoUrl.set(this.logoUrl()?.trim() ?? null);
  }

  protected onNameChange(event: Event): void {
    this.name.set((event.target as HTMLInputElement).value);
    this.nameEdited.set(true);
    this.resultLines.set([]);
  }

  protected onFileChosen(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.releasePreview();
    this.fileName.set(null);
    this.chosenLogo.set(null);
    this.logoError.set(null);

    if (!file) {
      return;
    }
    // Фільтр діалогу можна обійти: перевіряємо тип і розмір самі.
    if (!NGO_LOGO_TYPES.includes(file.type)) {
      this.logoError.set('logoType');
      input.value = '';
      return;
    }
    if (file.size > NGO_LOGO_MAX_BYTES) {
      this.logoError.set('logoSize');
      input.value = '';
      return;
    }
    this.fileName.set(file.name);
    this.chosenLogo.set(file);
    this.previewUrl.set(URL.createObjectURL(file));
    this.resultLines.set([]);
  }

  /**
   * «Зберегти» — одна на всю форму, але частини незалежні, кожну веде своє право:
   * - назва й логотип — лише `UpdateNgo`: перевіряється назва (порожня — помилка
   *   під полем і без запиту); якщо назва змінилася — `PUT /api/v1/ngo`; а якщо
   *   обрано файл логотипа — після цього додатковий запит
   *   `POST /api/v1/ngo/logo` із самим зображенням (файл не обрано — запиту нема);
   * - галерея — лише `CreateLoginPageImages` (завантаження) і
   *   `DeleteLoginPageImages` (видалення): свої запити, не залежні ні від назви,
   *   ні від `UpdateNgo`.
   * Форма може бути збережена не повністю, тому результат — кілька рядків у блоці
   * над формою: що саме змінено («Назву… змінено», «Логотип… змінено»,
   * «Зображення… змінено») і що не вдалося (з причиною). Усі рядки успішні —
   * блок зелений, є невдача — червоний. Нічого не змінювалось — блоку нема.
   */
  protected async submit(): Promise<void> {
    this.attempted.set(true);
    this.resultLines.set([]);
    const gallery = this.gallery();
    // Назву надсилаємо, лише коли можна її змінювати й вона заповнена; порожня назва
    // не заважає зберегти галерею.
    const saveNgo = this.canEditNgo() && !this.nameError();
    if (this.saving() || (!saveNgo && !gallery)) {
      return;
    }

    this.saving.set(true);
    const lines: ResultLine[] = [];

    if (saveNgo) {
      const name = this.name().trim();
      const nameChanged = name !== (this.accounts.active()?.ngoName ?? '');
      let ngoReady = true;

      if (nameChanged) {
        // `logoUrl` у цьому запиті завжди `null`: логотип іде окремим запитом нижче
        // (а будь-яке не-null значення сервер прийняв би за посилання й видалив
        // наявний файл логотипа).
        const outcome = await this.ngo.update(name, null);
        if (outcome === 'updated') {
          lines.push({ key: 'admin.result.name', ok: true });
        } else {
          ngoReady = false;
          lines.push({ key: `admin.result.ngoFailed.${outcome}`, ok: false });
        }
      }

      // Логотип — лише коли файл обрано (і назва, якщо її міняли, збереглася).
      const file = this.chosenLogo();
      if (file && ngoReady) {
        const outcome = await this.ngo.uploadLogo(file);
        if (outcome === 'uploaded') {
          lines.push({ key: 'admin.result.logo', ok: true });
          this.clearLogoSelection();
        } else {
          lines.push({ key: `admin.result.logoFailed.${outcome}`, ok: false });
        }
      }
    }
    if (gallery) {
      const outcome = await gallery.save();
      if (outcome === 'saved') {
        lines.push({ key: 'admin.result.images', ok: true });
      } else if (outcome !== 'nothing') {
        const failure: PartFailure = outcome;
        lines.push({ key: `admin.result.imagesFailed.${failure}`, ok: false });
      }
    }

    this.saving.set(false);
    this.resultLines.set(lines);
  }

  /** Логотип завантажено: обраний файл і його превʼю більше не потрібні (тепер логотип — з сервера). */
  private clearLogoSelection(): void {
    this.releasePreview();
    this.fileName.set(null);
    this.chosenLogo.set(null);
    const input = this.logoInput()?.nativeElement;
    if (input) {
      input.value = '';
    }
  }

  private releasePreview(): void {
    const url = this.previewUrl();
    if (url) {
      URL.revokeObjectURL(url);
      this.previewUrl.set(null);
    }
  }
}
