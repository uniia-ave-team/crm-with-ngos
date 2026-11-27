import { DOCUMENT } from '@angular/common';
import { Component, computed, inject, input, output } from '@angular/core';
import { TranslateService, TranslatePipe } from '@ngx-translate/core';
import { IonIcon } from '@ionic/angular';

import { LanguageService } from '../../core/i18n/language.service';
import { linkWithToken } from '../../core/auth/link-token';

/** Стан дій з посиланням: нічого не робили, скопійовано, не вдалося скопіювати, не вдалося поділитися. */
export type InvitationStatus = 'idle' | 'copied' | 'failed' | 'shareFailed';

/**
 * Про що посилання: запрошення колеги (`/join`, тексти `colleagues.invitation.*`) чи скидання
 * пароля (`/reset-password`, тексти `colleagues.passwordReset.*`; кнопки, статуси й підписи
 * пошти та дати — спільні, з `colleagues.invitation.*`).
 */
export type InvitationKind = 'invitation' | 'passwordReset';

/** Сторінка застосунку, на яку веде посилання кожного виду. */
const KIND_PATH: Record<InvitationKind, string> = { invitation: '/join', passwordReset: '/reset-password' };

/** Що віддається системі поширення (`navigator.share`): заголовок, підпис і саме посилання. */
export interface InvitationShare {
  readonly title: string;
  readonly text: string;
  readonly url: string;
}

/**
 * Зміст модального вікна «Запрошення створено» (`app-modal`): посилання, яким адмін ділиться з
 * колегою (`<адреса застосунку на домені сервера>/join#token=<токен>`, див. `link`), далі
 * пошта, для якої створено запрошення, і до
 * коли воно діє. Посилання в один рядок із горизонтальною прокруткою, виділяється одним кліком;
 * справа від нього дві кнопки: копіювання (`copy-outline`) і «Поділитися» (`share-social`, через
 * вбудовані засоби браузера чи системи; без підтримки `navigator.share` її нема). Разом із
 * посиланням поширюється підпис із мовного файлу: хто й куди запрошує і до коли діє посилання.
 * Закривається хрестиком у рівень із заголовком (як у бічного вікна). Копіювання, поширення й
 * закриття віддає подіями `copyLink` (з посиланням), `shareLink` (з даними для поширення) та
 * `dismiss`; сам нічого не робить, а як усе пройшло, приходить входом `status`.
 *
 * Те саме вікно показує й посилання для скидання пароля колеги (`kind="passwordReset"`): інші
 * заголовок, підказка й підпис поширення, а посилання веде на `/reset-password#token=…`.
 */
@Component({
  selector: 'app-invitation-dialog',
  templateUrl: './invitation-dialog.component.html',
  styleUrls: ['./invitation-dialog.component.scss'],
  imports: [IonIcon, TranslatePipe],
})
export class InvitationDialogComponent {
  private readonly language = inject(LanguageService).language;
  private readonly translate = inject(TranslateService);
  private readonly document = inject(DOCUMENT);
  private readonly page = this.document.location;

  /** Пошта, для якої створено запрошення. */
  readonly email = input.required<string>();
  /** Токен запрошення (стає параметром `token` посилання). */
  readonly token = input.required<string>();
  /**
   * Origin сервера, на якому створено запрошення: `<протокол>://<хост>` (API за зворотним проксі на
   * origin застосунку) або `<протокол>://<хост>:5065`.
   */
  readonly server = input.required<string>();
  /** До коли діє запрошення (ISO-дата); порожньо, коли сервер дати не віддав. */
  readonly expiresAt = input('');
  /** Назва організації, що користується системою (з підпису поширення); порожньо — підпис без неї. */
  readonly ngoName = input('');
  readonly status = input<InvitationStatus>('idle');
  readonly kind = input<InvitationKind>('invitation');

  /** Натиснули кнопку копіювання: віддається саме посилання. */
  readonly copyLink = output<string>();
  /** Натиснули «Поділитися»: заголовок, підпис і посилання для системи поширення. */
  readonly shareLink = output<InvitationShare>();
  /** Натиснули хрестик. */
  readonly dismiss = output<void>();

  /** Чи вміє браузер (система) поширювати: без цього кнопки «Поділитися» нема. */
  protected readonly canShare = typeof this.document.defaultView?.navigator?.share === 'function';

  /**
   * Посилання для колеги: `<адреса застосунку>/join#token=<токен>` (для скидання пароля —
   * `/reset-password#token=<токен>`; токен у фрагменті й закодований для адреси, див. `link-token.ts`).
   * Застосунок і API живуть на одному хості (API — на origin застосунку за зворотним проксі або на
   * порту 5065), а `/join` і `/reset-password` шукають сервер за доменом сторінки (спершу її origin,
   * далі порт 5065). Тому посилання веде на хост сервера, де створено запрошення: якщо це той
   * самий хост, що й у цієї сторінки, — на цю ж адресу (так і під час розробки, коли застосунок
   * на іншому порту); якщо інший — на той хост зі стандартним портом.
   */
  protected readonly link = computed(() => linkWithToken(this.appOrigin(), KIND_PATH[this.kind()], this.token()));

  /** Ключ тексту, що залежить від виду посилання (`title`, `hint`, `link`, `share*`). */
  protected text(name: string): string {
    return `colleagues.${this.kind()}.${name}`;
  }

  private readonly appOrigin = computed(() => {
    let server: URL;
    try {
      server = new URL(this.server());
    } catch {
      return this.page.origin;
    }
    return server.hostname === this.page.hostname ? this.page.origin : `${server.protocol}//${server.hostname}`;
  });

  protected share(): void {
    const t = (key: string, params?: Record<string, string>) => this.translate.instant(key, params) as string;
    const ngo = this.ngoName().trim();
    const date = this.expires();
    const intro = ngo ? t(this.text('shareIntro'), { ngo }) : t(this.text('shareIntroNoNgo'));
    const outro = date ? t(this.text('shareOutro'), { date }) : t(this.text('shareOutroNoDate'));
    this.shareLink.emit({ title: t(this.text('shareTitle')), text: `${intro} ${outro}`, url: this.link() });
  }

  /** До коли діє запрошення, словами за мовою інтерфейсу; порожньо, коли дати нема чи вона некоректна. */
  protected readonly expires = computed(() => {
    const value = this.expiresAt();
    const date = value ? new Date(value) : null;
    return date && !Number.isNaN(date.getTime())
      ? new Intl.DateTimeFormat(this.language(), { dateStyle: 'medium', timeStyle: 'short' }).format(date)
      : '';
  });
}
