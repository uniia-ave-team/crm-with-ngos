import { Component, DestroyRef, computed, effect, inject, signal, untracked, viewChild } from '@angular/core';

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
 * «Зберегти»: з правом `UpdateNgo` перевіряє назву, отримує посилання на
 * логотип (`resolveLogoUrl`, поки порожня функція) і шле `PUT /api/v1/ngo` з
 * назвою й посиланням. Результат — зелений блок (успіх) або червоний (помилка).
 * Збереження галереї поки лише вигляд.
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

  /**
   * Посилання на логотип, який уже є в системі (`logoUrl` з `GET /api/v1/ngo`,
   * що його знімок акаунта отримує при відкритті сторінки). Лежить у прихованому
   * полі форми — звідти його бере збереження, коли новий логотип не обирали.
   */
  protected readonly existingLogoUrl = computed(() => this.accounts.active()?.ngoLogoUrl ?? '');

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
    this.previewUrl.set(URL.createObjectURL(file));
    this.resultLines.set([]);
  }

  /**
   * «Зберегти» — одна на всю форму, але частини незалежні, кожну веде своє право:
   * - назва й логотип — лише `UpdateNgo`: перевіряється назва (порожня — помилка
   *   під полем і без запиту), отримується посилання на логотип, і якщо назва чи
   *   логотип змінилися — `PUT /api/v1/ngo`;
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
      const logoUrl = await this.resolveLogoUrl();
      const nameChanged = name !== (this.accounts.active()?.ngoName ?? '');
      const logoChanged = (logoUrl ?? '') !== this.existingLogoUrl();
      // Нічого не змінилося — нічого й слати.
      if (nameChanged || logoChanged) {
        const outcome = await this.ngo.update(name, logoUrl);
        if (outcome === 'updated') {
          if (nameChanged) {
            lines.push({ key: 'admin.result.name', ok: true });
          }
          if (logoChanged) {
            lines.push({ key: 'admin.result.logo', ok: true });
          }
        } else {
          lines.push({ key: `admin.result.ngoFailed.${outcome}`, ok: false });
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

  /**
   * Посилання на логотип для `PUT /api/v1/ngo`. Задум: є право `UpdateNgo` —
   * якщо користувач обрав новий файл, завантажити його й повернути отримане
   * посилання; якщо не обирав — повернути посилання, що вже є в системі (поле
   * `existingLogoUrl` у прихованому інпуті форми).
   *
   * Поки порожня функція: завантаження файлу ще нема, тож завжди `null`.
   */
  protected async resolveLogoUrl(): Promise<string | null> {
    return null;
  }

  private releasePreview(): void {
    const url = this.previewUrl();
    if (url) {
      URL.revokeObjectURL(url);
      this.previewUrl.set(null);
    }
  }
}
