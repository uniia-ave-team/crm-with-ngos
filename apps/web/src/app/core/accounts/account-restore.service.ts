import { Injectable, Signal, inject, signal } from '@angular/core';

import { AuthService } from '../auth/auth.service';
import { ConnectionService } from '../connection/connection.service';
import { CurrentUserService } from '../users/current-user.service';
import { AccountsService } from './accounts.service';

/**
 * - `restored` — знайдено акаунт, що ще чинний, і він тепер активний;
 * - `none` — чинного акаунта нема (акаунтів нема або сервер відхилив їхні токени);
 * - `unavailable` — чинного нема, і причиною був хоч один акаунт, чий сервер не
 *   відповів (мережа, 5xx): користувачу варто показати помилку підключення.
 */
export type RestoreResult = 'restored' | 'none' | 'unavailable';

/** Що вийшло з перевірки одного акаунта. */
type AccountCheck = 'ok' | 'stale' | 'unavailable';

/**
 * Пошук чинного акаунта (сторінка входу та захисні перевірки інших сторінок):
 * проходить акаунти по черзі (активний першим, далі за порядком додавання) і
 * бере перший, що пройшов перевірку.
 *
 * Перевірка акаунта = сервер відповідає (`/health`) → токени придатні (за
 * потреби оновлюються) → сервер підтверджує вхід (`/users/me`). Результати:
 * - пройшов → стає активним, пошук завершено;
 * - не пройшов (сервер відхилив токени **або** не відповідає, помилка мережі чи
 *   5xx) → позначається `outdated: true` (не видаляється) і більше не
 *   перевіряється; якщо він був активним, активним стає інший чинний.
 * Чинних не лишилося — памʼять очищається повністю (`wipe`, як при виході;
 * тимчасово, згодом це буде замінено). Якщо хоч один акаунт не пройшов саме
 * через те, що його сервер не відповів, результат — `unavailable`, а не
 * `none`: на сторінці входу показується помилка підключення.
 */
@Injectable({ providedIn: 'root' })
export class AccountRestoreService {
  private readonly accounts = inject(AccountsService);
  private readonly connection = inject(ConnectionService);
  private readonly auth = inject(AuthService);
  private readonly currentUser = inject(CurrentUserService);

  private readonly searchingSignal = signal(false);
  /**
   * Іде пошук: акаунти по черзі стають активними, тож «активного нема» в цей
   * час не означає виходу (на цьому тримається `SessionService`).
   */
  readonly searching: Signal<boolean> = this.searchingSignal.asReadonly();

  private inFlight: Promise<RestoreResult> | null = null;

  /** Шукає чинний акаунт; виклики, що збіглися в часі, діляться одним пошуком. */
  restore(): Promise<RestoreResult> {
    if (!this.inFlight) {
      this.searchingSignal.set(true);
      this.inFlight = this.search().finally(() => {
        this.searchingSignal.set(false);
        this.inFlight = null;
      });
    }
    return this.inFlight;
  }

  private async search(): Promise<RestoreResult> {
    this.accounts.reload();
    let unavailable = false;
    const activeId = this.accounts.activeId();
    // Застарілі вже відомо, що не підійдуть: перевіряємо лише решту.
    const candidates = this.accounts
      .accounts()
      .filter((account) => !account.outdated)
      .sort((a, b) => Number(b.id === activeId) - Number(a.id === activeId));

    for (const candidate of candidates) {
      // Попередній застарілий акаунт міг очистити память (усі застаріли) — шукати нічого.
      if (this.accounts.accounts().length === 0) {
        break;
      }
      this.accounts.setActive(candidate.id);
      const check = await this.check();
      if (check === 'ok') {
        this.accounts.update(candidate.id, { lastUsedAt: Date.now() });
        return 'restored';
      }
      // Недоступний сервер — теж застарілий акаунт; причину запамʼятовуємо в
      // записі (для меню акаунтів) і в результаті пошуку.
      unavailable ||= check === 'unavailable';
      this.accounts.markOutdated(candidate.id, check === 'unavailable');
    }

    // Чинного не знайдено: усі акаунти вже позначено застарілими, а коли
    // останній став застарілим, памʼять очистилася. Лишилися акаунти, застарілі
    // ще до пошуку, — очищаємо й їх.
    if (this.accounts.accounts().length > 0) {
      this.accounts.wipe();
    }
    // Підключення востаннє перевіряли на сервері останнього акаунта; після
    // перебору воно має знову вказувати на звичайну ціль (введена адреса чи
    // домен сторінки). Нічого не перебирали — ціль не змінювалась.
    if (candidates.length > 0) {
      await this.connection.checkApi();
    }
    return unavailable ? 'unavailable' : 'none';
  }

  /**
   * Перевіряє активний акаунт (на його ж сервері): `ok` — сервер відповідає,
   * токени придатні й вхід підтверджено; `stale` — сервер відхилив токени;
   * `unavailable` — сервер не відповів. Для акаунта `stale` і `unavailable`
   * однакові (він стає застарілим), а відрізняються лише причиною в результаті
   * пошуку.
   */
  private async check(): Promise<AccountCheck> {
    if ((await this.connection.checkApi()) !== 'healthy') {
      return 'unavailable';
    }
    if (this.auth.accessTokenExpired()) {
      const refreshed = await this.auth.refresh();
      if (refreshed === 'invalid' || refreshed === 'unauthorized') {
        return 'stale';
      }
      if (refreshed !== 'ok') {
        return 'unavailable';
      }
    }
    const result = await this.currentUser.ensure();
    return result === 'ok' ? 'ok' : result === 'unauthorized' ? 'stale' : 'unavailable';
  }
}
