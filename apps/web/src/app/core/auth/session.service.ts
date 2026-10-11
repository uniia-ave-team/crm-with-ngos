import { Injectable, effect, inject, untracked } from '@angular/core';
import { Router } from '@angular/router';

import { AccountRestoreService } from '../accounts/account-restore.service';
import { AccountsService } from '../accounts/accounts.service';
import { AuthService, LOGIN_URL } from './auth.service';

/** За скільки до кінця терміну access-токен оновлюється заздалегідь. */
const REFRESH_LEAD_MS = 60_000;
/** Найменша пауза перед пасивним оновленням (щоб не крутитись, якщо токен живе дуже коротко). */
const MIN_DELAY_MS = 5_000;
/** Пауза перед повтором, якщо пасивне оновлення не вдалося через мережу. */
const RETRY_DELAY_MS = 30_000;
/** Як часто перевіряється, чи не час оновити токени неактивних акаунтів (без запитів, якщо не час). */
const INACTIVE_CHECK_INTERVAL_MS = 60 * 60_000;

/**
 * Підтримує сесію у фоні (запускається при старті застосунку):
 * - пасивно оновлює токени, коли наближається кінець терміну access-токена
 *   (таймер за `exp`, а також одразу, коли вкладка знову стає видимою — у
 *   фонових вкладках таймери можуть сильно запізнюватися);
 * - оновлює токени **неактивних** акаунтів, у яких минула половина терміну
 *   refresh-токена (`AuthService.refreshStoredIfDue`): після завершення пошуку
 *   акаунта (старт), коли вкладка знову стає видимою й раз на годину;
 * - якщо токени зникли (вихід в іншій вкладці, невдале оновлення), а ми не на
 *   сторінці входу — переносить на /login.
 */
@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly auth = inject(AuthService);
  private readonly accounts = inject(AccountsService);
  private readonly router = inject(Router);
  private readonly restore = inject(AccountRestoreService);

  private timer: ReturnType<typeof setTimeout> | null = null;
  private hadTokens = false;
  private inactiveRefresh: Promise<void> | null = null;

  constructor() {
    // Токени з'явилися або змінилися (вхід, оновлення) — перепланувати оновлення.
    effect(() => {
      const token = this.auth.accessToken();
      untracked(() => this.schedule(token));
    });

    // Токени зникли, поки користувач на закритій сторінці — на сторінку входу.
    // Під час пошуку чинного акаунта активний акаунт перемикається й тимчасово
    // «зникає» — це не вихід, тож тоді нічого не робимо.
    effect(() => {
      const hasTokens = this.auth.hasTokens();
      const searching = this.restore.searching();
      untracked(() => {
        if (searching) {
          return;
        }
        if (this.hadTokens && !hasTokens && !this.router.url.split('?')[0].startsWith(LOGIN_URL)) {
          void this.router.navigateByUrl(LOGIN_URL);
        }
        this.hadTokens = hasTokens;
      });
    });

    // Пошук акаунта завершено (старт застосунку) — час подбати про неактивні.
    effect(() => {
      if (!this.restore.searching()) {
        untracked(() => void this.refreshInactiveAccounts());
      }
    });

    document.addEventListener('visibilitychange', () => {
      if (document.visibilityState === 'visible') {
        this.onVisible();
        void this.refreshInactiveAccounts();
      }
    });
    setInterval(() => void this.refreshInactiveAccounts(), INACTIVE_CHECK_INTERVAL_MS);
  }

  /**
   * Оновлює токени неактивних акаунтів, яким уже час. Не під час пошуку акаунта
   * (активний тоді перемикається) і не паралельно з попереднім проходом.
   */
  private refreshInactiveAccounts(): Promise<void> {
    if (this.restore.searching()) {
      return Promise.resolve();
    }
    if (!this.inactiveRefresh) {
      this.inactiveRefresh = (async () => {
        for (const account of [...this.accounts.accounts()]) {
          await this.auth.refreshStoredIfDue(account.id);
        }
      })().finally(() => {
        this.inactiveRefresh = null;
      });
    }
    return this.inactiveRefresh;
  }

  /** Вкладка знову видима: якщо терміну лишилося мало (або вже нема) — оновлюємо одразу, інакше просто перепланувуємо. */
  private onVisible(): void {
    const msLeft = this.auth.accessTokenMsLeft();
    if (this.auth.accessToken() && msLeft !== null && msLeft <= REFRESH_LEAD_MS) {
      this.clearTimer();
      void this.refreshNow();
    } else {
      this.schedule(this.auth.accessToken());
    }
  }

  private schedule(token: string | null): void {
    this.clearTimer();
    if (!token) {
      return;
    }
    const msLeft = this.auth.accessTokenMsLeft();
    if (msLeft === null) {
      return;
    }
    const delay = msLeft > REFRESH_LEAD_MS ? msLeft - REFRESH_LEAD_MS : Math.max(msLeft / 2, MIN_DELAY_MS);
    this.timer = setTimeout(() => void this.refreshNow(), delay);
  }

  private async refreshNow(): Promise<void> {
    this.timer = null;
    const result = await this.auth.refresh();
    if (result === 'ok') {
      return; // новий токен перепланує оновлення через effect
    }
    if (!this.auth.hasTokens()) {
      return; // токени відхилено й видалено — друга effect перенесе на /login
    }
    // Мережева помилка: токен ще чинний — повторимо трохи згодом.
    this.timer = setTimeout(() => void this.refreshNow(), RETRY_DELAY_MS);
  }

  private clearTimer(): void {
    if (this.timer !== null) {
      clearTimeout(this.timer);
      this.timer = null;
    }
  }
}
