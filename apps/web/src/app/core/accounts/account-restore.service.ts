import { Injectable, Signal, inject, signal } from '@angular/core';

import { AuthService } from '../auth/auth.service';
import { ConnectionService } from '../connection/connection.service';
import { CurrentUserService } from '../users/current-user.service';
import { AccountsService } from './accounts.service';

/**
 * - `restored` — знайдено акаунт, що ще чинний, і він тепер активний;
 * - `none` — доступного акаунта нема (акаунтів нема або сервер відхилив їхні токени);
 * - `unavailable` — доступного нема, і причиною був хоч один акаунт, чий сервер не
 *   відповів (мережа, 5xx): користувачу варто показати помилку підключення.
 */
export type RestoreResult = 'restored' | 'none' | 'unavailable';

/** Що вийшло з перевірки одного акаунта. */
type AccountCheck = 'ok' | 'stale' | 'unavailable';

/**
 * Чим закінчилося перемикання на акаунт (`activate`): `ok` — акаунт активний; `stale` —
 * сервер відхилив його токени; `unavailable` — сервер не відповів. У двох останніх випадках
 * активним лишається попередній акаунт.
 */
export type ActivateResult = AccountCheck;

/**
 * Пошук чинного акаунта (сторінка входу та захисні перевірки інших сторінок). Акаунти бувають
 * застарілі (`outdated`: сервер відхилив токени), недоступні (`unavailable`: сервер не
 * відповів) і доступні (ні те, ні інше). Перевірка акаунта = сервер відповідає (`/health`) →
 * токени придатні (за потреби оновлюються) → сервер підтверджує вхід (`/users/me`).
 *
 * Два проходи:
 * 1. доступні акаунти по черзі (активний першим, далі за порядком додавання); перший, що
 *    пройшов перевірку, стає активним, і пошук завершено. Не пройшов: сервер відхилив токени —
 *    `outdated`, сервер не відповів — `unavailable` (акаунт не видаляється й не
 *    перевіряється вдруге в цьому пошуку);
 * 2. доступного не знайшлось — недоступні акаунти, які такими були ще до пошуку (щойно
 *    недоступні з першого проходу не повторюються), по черзі перевіряються знову, кожен
 *    починаючи з `/health` власного сервера. Той, що пройшов, стає активним (`unavailable`
 *    знімається).
 * Застарілі пропускаються: потрібен повторний вхід. Якщо не пройшов жоден — доступних акаунтів
 * нема, активного нема, а акаунти лишаються в памʼяті (цим займеться сторінка повторного
 * входу): очищає памʼять лише вихід. Хоч один акаунт, чий сервер не відповів, — результат
 * `unavailable`, а не `none`.
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

  /**
   * Перемикає на акаунт `id` (клацання по його плитці): перевіряє його так само, як пошук
   * чинного акаунта (сервер відповідає → токени придатні, за потреби оновлюються → сервер
   * підтверджує вхід), а під час перевірки він активний — підключення й токени беруться
   * від активного.
   * - перевірка пройшла → акаунт лишається активним і стає чинним (`outdated: false`), якщо
   *   був застарілим: так «сервер не відповідав» знімається, коли сервер знову відповів;
   * - не пройшла → активним повертається попередній акаунт, а цей позначається: сервер
   *   відхилив токени — застарілим (`markOutdated`), не відповів — недоступним
   *   (`markUnavailable`); підключення й профіль знову належать попередньому.
   * На час перевірки так само, як і під час пошуку, вмикається `searching`: тимчасове
   * перемикання активного акаунта не вважається виходом. Невідомий `id` — `unavailable`
   * без жодних змін.
   */
  async activate(id: string): Promise<ActivateResult> {
    if (!this.accounts.accounts().some((account) => account.id === id)) {
      return 'unavailable';
    }
    // Іде пошук чинного акаунта (старт застосунку) — дочекаємось його, щоб не перемикати паралельно.
    await this.inFlight?.catch(() => undefined);

    const previous = this.accounts.activeId();
    this.searchingSignal.set(true);
    try {
      this.accounts.setActive(id);
      const check = await this.check();
      if (check === 'ok') {
        this.accounts.update(id, { outdated: false, unavailable: false, lastUsedAt: Date.now() });
        return 'ok';
      }
      // Спершу повертаємо попередній, інакше позначення активного акаунта застарілим
      // саме підбере «перший чинний».
      this.accounts.setActive(previous);
      this.markFailed(id, check);
      await this.connection.checkApi();
      // Профіль скинувся при перемиканні на цей акаунт — повертаємо профіль попереднього
      // (у фоні: чекати на нього не треба). Попереднього не було — повертати нічого.
      if (previous) {
        void this.currentUser.ensure();
      }
      return check;
    } finally {
      this.searchingSignal.set(false);
    }
  }

  private async search(): Promise<RestoreResult> {
    this.accounts.reload();
    const activeId = this.accounts.activeId();
    const all = this.accounts.accounts();
    // Перший прохід: доступні (активний першим). Другий: недоступні ще до пошуку; застарілі
    // пропускаємо в обох — їм потрібен повторний вхід.
    const available = all
      .filter((account) => !account.outdated && !account.unavailable)
      .sort((a, b) => Number(b.id === activeId) - Number(a.id === activeId));
    const retried = all.filter((account) => !account.outdated && account.unavailable);
    let unavailable = false;
    let checked = 0;

    for (const candidate of [...available, ...retried]) {
      // Акаунт могли забути, поки йшла перевірка попереднього (вихід в іншій вкладці).
      if (!this.accounts.accounts().some((account) => account.id === candidate.id)) {
        continue;
      }
      // Недоступні (другий прохід) йдуть після доступних, тож до них доходимо, лише коли
      // доступного не знайшлось.
      checked += 1;
      this.accounts.setActive(candidate.id);
      const check = await this.check();
      if (check === 'ok') {
        this.accounts.update(candidate.id, { unavailable: false, lastUsedAt: Date.now() });
        return 'restored';
      }
      unavailable ||= check === 'unavailable';
      this.markFailed(candidate.id, check);
    }

    // Нічого не знайдено: активного акаунта нема, самі акаунти лишаються (позначені вище).
    this.accounts.setActive(null);
    // Підключення востаннє перевіряли на сервері останнього акаунта; після перебору воно має
    // знову вказувати на звичайну ціль (введена адреса чи домен сторінки). Нічого не
    // перебирали — ціль не змінювалась.
    if (checked > 0) {
      await this.connection.checkApi();
    }
    return unavailable ? 'unavailable' : 'none';
  }

  /** Записує, чим скінчилась невдала перевірка: токени відхилено — `outdated`, сервер мовчить — `unavailable`. */
  private markFailed(id: string, check: AccountCheck): void {
    if (check === 'stale') {
      this.accounts.markOutdated(id);
    } else if (check === 'unavailable') {
      this.accounts.markUnavailable(id);
    }
  }

  /**
   * Перевіряє активний акаунт (на його ж сервері): `ok` — сервер відповідає,
   * токени придатні й вхід підтверджено; `stale` — сервер відхилив токени (акаунт
   * `outdated`); `unavailable` — сервер не відповів (акаунт `unavailable`).
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
