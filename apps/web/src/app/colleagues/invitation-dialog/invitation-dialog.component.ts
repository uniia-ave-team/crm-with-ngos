import { DOCUMENT } from '@angular/common';
import { Component, computed, inject, input, output } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

import { LanguageService } from '../../core/i18n/language.service';

/** Стан дій з посиланням: нічого не робили, скопійовано, не вдалося скопіювати, не вдалося поділитися. */
export type InvitationStatus = 'idle' | 'copied' | 'failed' | 'shareFailed';

/** Що віддається системі поширення (`navigator.share`): заголовок, підпис і саме посилання. */
export interface InvitationShare {
  readonly title: string;
  readonly text: string;
  readonly url: string;
}

/**
 * Зміст модального вікна «Запрошення створено» (`app-modal`): посилання, яким адмін ділиться з
 * колегою (`<поточний домен>/join?token=<токен>`), далі пошта, для якої створено запрошення, і до
 * коли воно діє. Посилання в один рядок із горизонтальною прокруткою, виділяється одним кліком;
 * справа від нього дві кнопки: копіювання (`copy-outline`) і «Поділитися» (`share-social`, через
 * вбудовані засоби браузера чи системи; без підтримки `navigator.share` її нема). Разом із
 * посиланням поширюється підпис із мовного файлу: хто й куди запрошує і до коли діє посилання.
 * Закривається хрестиком у рівень із заголовком (як у бічного вікна). Копіювання, поширення й
 * закриття віддає подіями `copyLink` (з посиланням), `shareLink` (з даними для поширення) та
 * `dismiss`; сам нічого не робить, а як усе пройшло, приходить входом `status`.
 */
@Component({
  selector: 'app-invitation-dialog',
  templateUrl: './invitation-dialog.component.html',
  styleUrls: ['./invitation-dialog.component.scss'],
  standalone: false,
})
export class InvitationDialogComponent {
  private readonly language = inject(LanguageService).language;
  private readonly translate = inject(TranslateService);
  private readonly document = inject(DOCUMENT);
  private readonly origin = this.document.location.origin;

  /** Пошта, для якої створено запрошення. */
  readonly email = input.required<string>();
  /** Токен запрошення (стає параметром `token` посилання). */
  readonly token = input.required<string>();
  /** До коли діє запрошення (ISO-дата); порожньо, коли сервер дати не віддав. */
  readonly expiresAt = input('');
  /** Назва організації, що користується системою (з підпису поширення); порожньо — підпис без неї. */
  readonly ngoName = input('');
  readonly status = input<InvitationStatus>('idle');

  /** Натиснули кнопку копіювання: віддається саме посилання. */
  readonly copyLink = output<string>();
  /** Натиснули «Поділитися»: заголовок, підпис і посилання для системи поширення. */
  readonly shareLink = output<InvitationShare>();
  /** Натиснули хрестик. */
  readonly dismiss = output<void>();

  /** Чи вміє браузер (система) поширювати: без цього кнопки «Поділитися» нема. */
  protected readonly canShare = typeof this.document.defaultView?.navigator?.share === 'function';

  /** Посилання для колеги: `<поточний домен>/join?token=<токен>` (токен кодується для адреси). */
  protected readonly link = computed(() => `${this.origin}/join?token=${encodeURIComponent(this.token())}`);

  protected share(): void {
    const t = (key: string, params?: Record<string, string>) => this.translate.instant(key, params) as string;
    const ngo = this.ngoName().trim();
    const date = this.expires();
    const intro = ngo ? t('colleagues.invitation.shareIntro', { ngo }) : t('colleagues.invitation.shareIntroNoNgo');
    const outro = date ? t('colleagues.invitation.shareOutro', { date }) : t('colleagues.invitation.shareOutroNoDate');
    this.shareLink.emit({ title: t('colleagues.invitation.shareTitle'), text: `${intro} ${outro}`, url: this.link() });
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
