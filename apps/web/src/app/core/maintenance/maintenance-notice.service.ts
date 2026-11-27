import { DestroyRef, Injectable, Signal, computed, effect, inject, signal, untracked } from '@angular/core';

import { MaintenanceNoticeDto } from '../../api/generated/models';
import { AccountsService } from '../accounts/accounts.service';
import { ConnectionService } from '../connection/connection.service';
import { LanguageService } from '../i18n/language.service';

/** Як часто перераховується, чи роботи вже почались або скінчились (мс). */
const CLOCK_TICK_MS = 60_000;
/** Як часто перепитувати статус (нове оголошення): вкладка, відкрита зранку, побачить денне. */
export const STATUS_REFRESH_MS = 5 * 60_000;

/** Що показати в оголошенні: ключ заголовка з параметрами й повідомлення сервера. */
export interface MaintenanceNoticeView {
  /** Ключ заголовка в `common.maintenance.*`: роботи заплановані чи вже тривають. */
  readonly titleKey: 'common.maintenance.scheduled' | 'common.maintenance.ongoing';
  /** Початок і кінець робіт, відформатовані мовою інтерфейсу (`{{start}}`, `{{end}}`). */
  readonly params: { readonly start: string; readonly end: string };
  readonly message: string;
}

/**
 * Оголошення сервера про планові технічні роботи (`notice` у `GET /api/v1/system/status`).
 *
 * Статус системи запитується, щойно підключення до сервера підтверджено й статусу ще нема
 * (`ConnectionService.systemStatusState` — `idle`): при старті й після кожної нової перевірки
 * підключення (зміна сервера, перемикання акаунта). Поки йде пошук чинного акаунта, не запитуємо:
 * пошук по черзі перевіряє сервери збережених акаунтів, і статус кожного з них нікому не потрібен.
 * Сторінка входу запитує статус і сама: новий запит заміняє попередній, тож іде один.
 *
 * Щоб побачити оголошення, опубліковане пізніше, статус тихо перепитується раз на
 * `STATUS_REFRESH_MS` (поки вкладку видно) і щойно на вкладку повернулися
 * (`ConnectionService.refreshSystemStatus`).
 *
 * Оголошення видно, поки роботи не скінчились (`endsAt`); заголовок каже, заплановані вони чи
 * вже тривають, і перемикається сам (годинник раз на хвилину). Закрите кнопкою оголошення не
 * показується до перезавантаження сторінки; інше оголошення (інший текст чи час) — показується.
 * Оголошення з датами, які не вдалося прочитати, не показується: незрозуміло, чи воно ще чинне.
 */
@Injectable({ providedIn: 'root' })
export class MaintenanceNoticeService {
  private readonly connection = inject(ConnectionService);
  private readonly accounts = inject(AccountsService);
  private readonly languageService = inject(LanguageService);

  private readonly now = signal(Date.now());
  /** Оголошення, яке закрили (за `noticeKey`). */
  private readonly dismissedKey = signal<string | null>(null);

  private readonly notice: Signal<MaintenanceNoticeDto | null> = computed(
    () => this.connection.systemStatus()?.notice ?? null,
  );

  /** Що показати; `null` — оголошення нема, воно вже неактуальне чи його закрили. */
  readonly view: Signal<MaintenanceNoticeView | null> = computed(() => {
    const notice = this.notice();
    if (!notice || noticeKey(notice) === this.dismissedKey()) {
      return null;
    }
    const start = Date.parse(notice.startsAt);
    const end = Date.parse(notice.endsAt);
    const now = this.now();
    if (Number.isNaN(start) || Number.isNaN(end) || now >= end) {
      return null;
    }
    const format = new Intl.DateTimeFormat(this.languageService.language(), { dateStyle: 'medium', timeStyle: 'short' });
    return {
      titleKey: now >= start ? 'common.maintenance.ongoing' : 'common.maintenance.scheduled',
      params: { start: format.format(start), end: format.format(end) },
      message: notice.message.trim(),
    };
  });

  constructor() {
    effect(() => {
      const healthy = this.connection.apiStatus() === 'healthy';
      const idle = this.connection.systemStatusState() === 'idle';
      const searching = this.accounts.switching();
      untracked(() => {
        if (healthy && idle && !searching) {
          void this.connection.loadSystemStatus();
        }
      });
    });

    const timer = setInterval(() => this.now.set(Date.now()), CLOCK_TICK_MS);
    const refresh = () => {
      if (typeof document !== 'undefined' && document.visibilityState === 'visible' && !this.accounts.switching()) {
        void this.connection.refreshSystemStatus();
      }
    };
    const refreshTimer = setInterval(refresh, STATUS_REFRESH_MS);
    if (typeof document !== 'undefined') {
      document.addEventListener('visibilitychange', refresh);
    }
    inject(DestroyRef).onDestroy(() => {
      clearInterval(timer);
      clearInterval(refreshTimer);
      if (typeof document !== 'undefined') {
        document.removeEventListener('visibilitychange', refresh);
      }
    });
  }

  /**
   * Плашка оголошення зникла (`didDismiss`): закритим вважається лише закрите кнопкою «Закрити»
   * (`role` — `cancel`). Плашка зникає й сама, коли статус перепитується чи оголошення вже неактуальне,
   * — тоді його не закривали, і нове чи те саме оголошення має показатися знову.
   */
  onDismissed(role: string | undefined): void {
    if (role === 'cancel') {
      this.dismiss();
    }
  }

  /** Закриває поточне оголошення (до перезавантаження сторінки). */
  dismiss(): void {
    const notice = this.notice();
    if (notice) {
      this.dismissedKey.set(noticeKey(notice));
    }
  }
}

/** Чим одне оголошення відрізняється від іншого: зміна тексту чи часу — нове оголошення. */
function noticeKey(notice: MaintenanceNoticeDto): string {
  return JSON.stringify([notice.message, notice.startsAt, notice.endsAt]);
}
