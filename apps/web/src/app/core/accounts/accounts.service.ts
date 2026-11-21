import { DestroyRef, Injectable, Signal, computed, inject, signal } from '@angular/core';

import { hostnameToUnicode } from '../connection/idn';
import { UserRole, sanitizeRoles } from '../users/user-profile';

/** Індекс: впорядкований список `id` акаунтів (JSON-масив рядків). */
const INDEX_KEY = 'yavir.accounts';
/** Запис акаунта: `yavir.account.<id>`. */
const ACCOUNT_KEY_PREFIX = 'yavir.account.';
/**
 * `id` активного акаунта **цієї вкладки** (у `sessionStorage`). Порожній рядок — у вкладці свідомо
 * нема активного акаунта; ключа нема взагалі — вкладка нова й ще нічого не вибирала.
 */
const ACTIVE_KEY = 'yavir.activeAccount';
/**
 * Підказка для нової вкладки (у `localStorage`): `id` акаунта, активного у вкладці, яку
 * користувач бачив останньою.
 */
const LAST_ACTIVE_KEY = 'yavir.lastActiveAccount';

/**
 * Акаунт на цьому пристрої = пара «сервер + користувач». Запис містить усе, що
 * потрібно, щоб працювати від його імені й намалювати його плитку без запитів:
 * адресу підключення, токени та знімок даних (імʼя, прізвище, аватар, назва ГО).
 */
export interface StoredAccount {
  readonly id: string;
  /** Адреса підключення цього акаунта (origin сервера), напр. `http://localhost:5065`. */
  readonly server: string;
  /**
   * Чим акаунт відрізняється від інших на цьому сервері (`sub` токена, а без
   * нього — пошта чи інша стабільна ознака). Разом із `server` визначає, чи це
   * той самий акаунт, і за ним розпізнаються колізії `id`.
   */
  readonly identity: string;
  /** `id` користувача на сервері (claim `sub` токена); порожній, якщо токен його не містив. */
  readonly userId: string;
  /** Пошта, з якою ввійшли (з `/users/me` її отримати не можна, тож пишемо під час входу). */
  readonly email: string;
  /**
   * Сесія застаріла: сервер відхилив токени цього акаунта. Такий акаунт не
   * видаляється, а лише позначається (`true`) і пропускається під час пошуку
   * чинного; повторний вхід у нього знову робить його чинним (`false`).
   */
  readonly outdated: boolean;
  /**
   * Сервер акаунта не відповів (мережа, 5xx). Незалежний від `outdated`: токени можуть бути
   * чинними, просто до сервера не дістатись. Такий акаунт пропускається при першому проході
   * пошуку чинного, а перевіряється лише коли доступних не знайшлось. Успішна перевірка (чи
   * повторний вхід) скидає в `false`.
   */
  readonly unavailable: boolean;
  readonly firstName: string;
  readonly lastName: string;
  /** По батькові (для підказки на плитці в меню акаунтів); порожній рядок, якщо нема. */
  readonly patronymic: string;
  readonly avatarUrl: string | null;
  /** Назва ГО (`GET /api/v1/ngo`); `null` — ще нема або немає права на перегляд. */
  readonly ngoName: string | null;
  /**
   * Посилання на логотип ГО (`logoUrl` з `GET /api/v1/ngo`), як його віддав
   * сервер (може бути відносним до адреси сервера); `null` — логотипа нема або
   * ще невідомо.
   */
  readonly ngoLogoUrl: string | null;
  /**
   * Мініатюра аватара: плитка акаунта у вікні акаунтів малюється з неї, без
   * запитів (закрите зображення можна завантажити лише з токеном активного
   * акаунта, а плитки показують і неактивні). `null` — ще нема.
   */
  readonly avatarThumb: AccountThumbnail | null;
  /** Мініатюра логотипа ГО для значка на плитці; `null` — ще нема. */
  readonly ngoLogoThumb: AccountThumbnail | null;
  /**
   * Права користувача для побудови інтерфейсу (`GET /api/v1/users/me/permissions`):
   * унікальні рядки, як їх віддав сервер. Оновлюються разом із профілем (при
   * кожній зміні сторінки); порожньо — ще не отримано чи прав нема.
   */
  readonly permissions: readonly string[];
  /**
   * Ролі користувача з його профілю (`roles` відповіді `GET /api/v1/users/me`),
   * оновлюються разом із профілем; порожньо — ще не отримано чи ролей нема.
   */
  readonly roles: readonly UserRole[];
  readonly accessToken: string;
  readonly refreshToken: string;
  /**
   * Коли отримано цю пару токенів (вхід або обмін), мс. За цим, а не за
   * самим токеном (refresh-токен непрозорий), рахується, чи не час оновити
   * токени неактивного акаунта. `0` — невідомо (оновлюється при першій нагоді).
   */
  readonly tokensIssuedAt: number;
  readonly addedAt: number;
  readonly lastUsedAt: number;
}

/**
 * Що потрібно для додавання акаунта: сервер, токени й те, за чим його впізнати. Це все, що
 * відомо одразу після входу; знімок профілю (імʼя, аватар, ГО, права, ролі) приходить пізніше
 * окремими запитами й записується через `update`.
 */
export interface NewAccount {
  readonly server: string;
  readonly accessToken: string;
  readonly refreshToken: string;
  /**
   * Чим акаунт відрізняється від інших на тому ж сервері: `sub` токена, а якщо
   * його нема — щось стабільне (наприклад, пошта).
   */
  readonly identity: string;
  readonly userId?: string;
  readonly email?: string;
}

/**
 * Мініатюра зображення в знімку акаунта: сам стиснутий малюнок (`data:`-посилання)
 * і адреса, з якої його зроблено (за нею видно, що зображення змінилось і
 * мініатюру треба зробити заново).
 */
export interface AccountThumbnail {
  readonly url: string;
  readonly data: string;
}

/** Частина запису, яку можна змінювати: знімок, токени, службові поля. */
export type AccountPatch = Partial<
  Pick<
    StoredAccount,
    | 'userId'
    | 'email'
    | 'outdated'
    | 'unavailable'
    | 'firstName'
    | 'lastName'
    | 'patronymic'
    | 'avatarUrl'
    | 'ngoName'
    | 'ngoLogoUrl'
    | 'avatarThumb'
    | 'ngoLogoThumb'
    | 'permissions'
    | 'roles'
    | 'accessToken'
    | 'refreshToken'
    | 'tokensIssuedAt'
    | 'lastUsedAt'
  >
>;

/**
 * Акаунти цього пристрою. Самі акаунти спільні для всіх вкладок і лежать у `localStorage`
 * («запис на акаунт + індекс»), а активний акаунт у кожної вкладки свій (`sessionStorage`):
 *
 * - `yavir.accounts` — індекс (масив `id` за порядком додавання);
 * - `yavir.account.<id>` — запис акаунта (див. `StoredAccount`);
 * - `yavir.activeAccount` (`sessionStorage`) — `id` активного акаунта цієї вкладки. Він
 *   переживає перезавантаження сторінки й не залежить від того, що вибрано в інших вкладках;
 * - `yavir.lastActiveAccount` — підказка для нової вкладки: акаунт вкладки, яку користувач
 *   бачив останньою. Нова вкладка починає з нього й далі живе своїм вибором.
 *
 * Чужі зміни самих акаунтів вкладка бачить: акаунт, з якого вийшли в іншій вкладці, зникає й
 * тут, а якщо інша вкладка позначила активний тут акаунт застарілим (сервер відхилив його
 * токени), він перестає бути активним і тут. «Сервер не відповів» кожна вкладка зʼясовує сама.
 *
 * Вкладки не затирають зміни одна одної: токени оновлюються під Web Lock в `AuthService`, а
 * будь-яка інша зміна запису (`update`) спершу підхоплює зі сховища пару токенів, якщо її
 * встигла оновити інша вкладка. Решту чужих змін вкладка отримує подією `storage`.
 *
 * Під час читання запис перевіряється по полях: поле не того типу стає значенням за
 * замовчуванням, а запис без `id`, сервера чи токенів (або з чужим `id`) вважається нечитним —
 * він зникає і з індексу, і зі сховища. Записи, яких в індексі нема, не шукаються: інша
 * вкладка пише спершу запис, а потім індекс, тож таке прибирання могло б видалити щойно
 * доданий акаунт.
 *
 * Сховище може бути недоступне (браузер блокує дані сайтів, ізольований фрейм). Це
 * перевіряється один раз на старті, і тоді акаунти живуть лише в памʼяті: `reload` їх не
 * чіпає, тож працювати можна, доки вкладку не закрили й не перезавантажили.
 */
@Injectable({ providedIn: 'root' })
export class AccountsService {
  private readonly accountsSignal = signal<readonly StoredAccount[]>([]);
  /** Усі акаунти за порядком додавання. */
  readonly accounts: Signal<readonly StoredAccount[]> = this.accountsSignal.asReadonly();

  private readonly activeIdSignal = signal<string | null>(null);
  /** `id` активного акаунта. */
  readonly activeId: Signal<string | null> = this.activeIdSignal.asReadonly();

  /** Активний акаунт; `null` — акаунтів нема або жоден не вибрано. */
  readonly active: Signal<StoredAccount | null> = computed(
    () => this.accountsSignal().find((account) => account.id === this.activeIdSignal()) ?? null,
  );

  private readonly switchingSignal = signal(false);
  /**
   * Іде пошук чинного акаунта чи перемикання на інший (`AccountRestoreService`): активними на мить
   * стають акаунти, які лише перевіряються, тож «активного нема» чи «активний інший» у цей час
   * не означає ні виходу, ні справжньої зміни акаунта. Прапорець живе тут, у сховищі, бо на нього
   * зважають сервіси, нижчі за пошук: профіль, сесія, меню.
   */
  readonly switching: Signal<boolean> = this.switchingSignal.asReadonly();

  /** Чи читається сховище браузера; ні — акаунти й активний живуть лише в памʼяті. */
  private readonly storageAvailable = canReadStorage();

  private readonly storageFullSignal = signal(false);
  /**
   * Сховище читається, але браузер не дав у нього записати (переповнене): зміни акаунтів (нові
   * токени, мініатюри, вихід) живуть лише в памʼяті до перезавантаження сторінки. Застосунок
   * показує про це спливаюче попередження; скидає його `dismissStorageFull`. Коли сховища нема
   * зовсім (дані сайтів заблоковано), це не помилка запису, і прапорець не вмикається.
   */
  readonly storageFull: Signal<boolean> = this.storageFullSignal.asReadonly();

  constructor() {
    this.reload();
    this.rememberLastActive();
    // Інша вкладка змінила акаунти (додала, оновила токени, вийшла) — підхоплюємо. Який акаунт
    // активний в іншій вкладці, нас не стосується: підказка `LAST_ACTIVE_KEY` сюди не входить.
    const onStorage = (event: StorageEvent): void => {
      if (event.key === null || event.key === INDEX_KEY || event.key.startsWith(ACCOUNT_KEY_PREFIX)) {
        this.reload();
      }
    };
    // Користувач повернувся до цієї вкладки — нова вкладка тепер має починати з її акаунта.
    const onVisibilityChange = (): void => this.rememberLastActive();
    window.addEventListener('storage', onStorage);
    document.addEventListener('visibilitychange', onVisibilityChange);
    // У застосунку сервіс живе, доки відкрита вкладка. Слухачів знімаємо для тестів, де сервіс
    // створюється заново: інакше попередній екземпляр і далі писав би у сховище.
    inject(DestroyRef).onDestroy(() => {
      window.removeEventListener('storage', onStorage);
      document.removeEventListener('visibilitychange', onVisibilityChange);
    });
  }

  /**
   * `id` акаунта: домен сервера (лише літери, латинські й українські) і короткий відбиток
   * сервера та ознаки користувача, напр. `localhost-a3f9c01b` чи `прикладукр-5d0c7e21`. Домен у `id`
   * напряму показує, чий це акаунт, а відбиток розрізняє порти, протоколи й
   * користувачів. Це лише основа: справжню унікальність гарантує `add()`,
   * що розводить колізії числовим суфіксом.
   */
  static accountId(server: string, identity: string): string {
    return `${domainLetters(server)}-${hash(`${server}|${identity}`)}`;
  }

  /**
   * Читає все зі сховища заново: акаунти — зі спільного, активний — зі свого (нова вкладка —
   * з підказки). Активним не стає акаунт, якого вже нема чи який застарів (поза перемиканням:
   * під час нього застарілий акаунт якраз перевіряється); тоді вкладка лишається без активного.
   *
   * Без сховища читати нічого: те, що є в памʼяті, лишається як було. Інакше порожнє
   * «прочитане» стерло б акаунт одразу після входу (стражі викликають це при кожному переході).
   */
  reload(): void {
    if (!this.storageAvailable) {
      return;
    }
    const ids = this.readIndex();
    const accounts: StoredAccount[] = [];
    for (const id of ids) {
      const account = this.readRecord(id);
      if (account) {
        accounts.push(account);
      } else {
        // Нечитний запис не лишаємо у сховищі: у ньому можуть бути токени.
        this.removeKey(`${ACCOUNT_KEY_PREFIX}${id}`);
      }
    }
    // Записи, яких нема (або вони нечитні), з індексу прибираємо.
    if (accounts.length !== ids.length) {
      this.writeIndex(accounts.map((account) => account.id));
    }
    const own = this.readActive();
    // `null` — вкладка ще нічого не вибирала (нова): починає з акаунта останньої видимої.
    const wanted = own ?? this.read(LAST_ACTIVE_KEY);
    const account = accounts.find((candidate) => candidate.id === wanted);
    const activeId = account && (this.switchingSignal() || !account.outdated) ? account.id : null;
    // Закріплюємо вибір за вкладкою: інакше вона й далі йшла б за підказкою або знову
    // підхопила б акаунт, який зник, а потім зʼявився з тим самим `id`.
    if (own !== (activeId ?? '')) {
      this.writeActive(activeId);
    }
    this.accountsSignal.set(accounts);
    this.activeIdSignal.set(activeId);
  }

  /**
   * Додає акаунт і робить його активним. Якщо такий (той самий сервер і
   * користувач) уже є — оновлює його токени, знімок лишається.
   *
   * `replaceId` — ключ акаунта, який треба перезаписати новими даними (сервер, користувач,
   * токени), а не створювати новий: запис і його `id` лишаються. Знімок профілю (імʼя,
   * аватар, ГО, права) лишається, лише якщо це той самий сервер і користувач; інакше він
   * очищається, бо належав комусь іншому. Якщо новий сервер і користувач уже мають інший
   * запис, той зникає: один користувач на сервері — один акаунт. Невідомий ключ ігнорується.
   */
  add(input: NewAccount, replaceId?: string): StoredAccount {
    const now = Date.now();
    // Той самий сервер і користувач — це той самий акаунт, хоч би який у нього `id`.
    const same = this.accountsSignal().find(
      (account) => account.server === input.server && account.identity === input.identity,
    );
    const target = replaceId === undefined ? undefined : this.accountsSignal().find((account) => account.id === replaceId);
    const existing = target ?? same;
    const duplicate = target && same && same.id !== target.id ? same : undefined;
    // Знімок профілю лишається тільки тому самому користувачеві на тому ж сервері.
    const snapshot = existing && existing.server === input.server && existing.identity === input.identity ? existing : undefined;
    const id = existing?.id ?? this.freeId(AccountsService.accountId(input.server, input.identity));
    const account: StoredAccount = {
      id,
      server: input.server,
      identity: input.identity,
      userId: input.userId ?? snapshot?.userId ?? '',
      email: input.email ?? snapshot?.email ?? '',
      // Вхід (нові токени) повертає акаунт у чинні.
      outdated: false,
      unavailable: false,
      firstName: snapshot?.firstName ?? '',
      lastName: snapshot?.lastName ?? '',
      patronymic: snapshot?.patronymic ?? '',
      avatarUrl: snapshot?.avatarUrl ?? null,
      ngoName: snapshot?.ngoName ?? null,
      ngoLogoUrl: snapshot?.ngoLogoUrl ?? null,
      avatarThumb: snapshot?.avatarThumb ?? null,
      ngoLogoThumb: snapshot?.ngoLogoThumb ?? null,
      permissions: snapshot?.permissions ?? [],
      roles: snapshot?.roles ?? [],
      accessToken: input.accessToken,
      refreshToken: input.refreshToken,
      tokensIssuedAt: now,
      addedAt: existing?.addedAt ?? now,
      lastUsedAt: now,
    };
    this.writeRecord(account);
    let accounts = existing
      ? this.accountsSignal().map((current) => (current.id === id ? account : current))
      : [...this.accountsSignal(), account];
    if (duplicate) {
      this.removeKey(`${ACCOUNT_KEY_PREFIX}${duplicate.id}`);
      accounts = accounts.filter((current) => current.id !== duplicate.id);
    }
    if (!existing || duplicate) {
      this.writeIndex(accounts.map((current) => current.id));
    }
    this.accountsSignal.set(accounts);
    this.setActive(id);
    return account;
  }

  /** Перший вільний `id` на основі `base`: колізія з іншим акаунтом розводиться суфіксом `-2`, `-3`… */
  private freeId(base: string): string {
    const taken = new Set(this.accountsSignal().map((account) => account.id));
    let id = base;
    for (let n = 2; taken.has(id); n++) {
      id = `${base}-${n}`;
    }
    return id;
  }

  /**
   * Змінює поля запису акаунта (`id` якого вже є); не існує — нічого не робить.
   *
   * У сховище пишеться запис цілком, разом із токенами. Тому спершу підхоплюється пара
   * токенів, яку могла оновити інша вкладка (`syncFromStorage`): подія `storage` про це
   * приходить не одразу, і без цього запис із памʼяті повернув би у сховище стару пару.
   */
  update(id: string, patch: AccountPatch): void {
    this.syncFromStorage(id);
    const current = this.accountsSignal().find((account) => account.id === id);
    if (!current) {
      return;
    }
    const next: StoredAccount = { ...current, ...patch };
    this.writeRecord(next);
    this.accountsSignal.set(this.accountsSignal().map((account) => (account.id === id ? next : account)));
  }

  /**
   * Позначає акаунт застарілим (`outdated: true`): сервер відповів, але відхилив його токени.
   * Сервер при цьому відповів, тож `unavailable` скидається. Акаунт не видаляємо. Якщо він
   * був активним, активного акаунта більше нема (`clearActiveIf`). Невідомий `id` ігнорується.
   */
  markOutdated(id: string): void {
    this.update(id, { outdated: true, unavailable: false });
    this.clearActiveIf(id);
  }

  /**
   * Позначає акаунт недоступним (`unavailable: true`): його сервер не відповів (мережа,
   * 5xx). `outdated` лишається як був: токени тут ні до чого. Якщо акаунт був активним,
   * активного більше нема, як у `markOutdated`. Невідомий `id` ігнорується.
   */
  markUnavailable(id: string): void {
    this.update(id, { unavailable: true });
    this.clearActiveIf(id);
  }

  /**
   * Видаляє акаунт. Якщо він був активним, активного акаунта більше нема (`clearActiveIf`).
   */
  remove(id: string): void {
    const accounts = this.accountsSignal().filter((account) => account.id !== id);
    if (accounts.length === this.accountsSignal().length) {
      return;
    }
    this.removeKey(`${ACCOUNT_KEY_PREFIX}${id}`);
    this.writeIndex(accounts.map((account) => account.id));
    this.accountsSignal.set(accounts);
    this.clearActiveIf(id);
  }

  /**
   * Знімає вибір активного акаунта, якщо активним був `changedId`. Викликається після того,
   * як цей акаунт застарів, став недоступним чи був видалений. Інший акаунт саме не підхоплюється:
   * користувач обирає його сам (сторінка повторного входу, меню акаунтів), а пошук чинного
   * (`AccountRestoreService`) вибирає його явно. Самі акаунти лишаються в памʼяті, її не
   * очищає нічого, крім виходу.
   */
  private clearActiveIf(changedId: string): void {
    if (this.activeIdSignal() === changedId) {
      this.setActive(null);
    }
  }

  /** Вмикає й вимикає `switching`; це робить лише той, хто тимчасово перемикає активний акаунт. */
  setSwitching(switching: boolean): void {
    this.switchingSignal.set(switching);
    this.rememberLastActive();
  }

  /**
   * Чи лежить акаунт у сховищі браузера таким, як у памʼяті: запис із тими самими токенами є і
   * значиться в індексі. Ні — запис не вдалося записати (сховище переповнене), і після
   * перезавантаження сторінки акаунта не буде. Коли сховища нема зовсім (акаунти свідомо живуть
   * у памʼяті, див. опис класу) — так.
   */
  isStored(id: string): boolean {
    if (!this.storageAvailable) {
      return true;
    }
    const current = this.accountsSignal().find((account) => account.id === id);
    const stored = this.readRecord(id);
    return !!current && !!stored && stored.refreshToken === current.refreshToken && this.readIndex().includes(id);
  }

  /**
   * Робить акаунт активним у цій вкладці (`null` — зняти вибір). Неіснуючий `id` ігнорується.
   * Інших вкладок це не зачіпає.
   */
  setActive(id: string | null): void {
    if (id !== null && !this.accountsSignal().some((account) => account.id === id)) {
      return;
    }
    this.writeActive(id);
    this.activeIdSignal.set(id);
    this.rememberLastActive();
  }

  /**
   * Записує активний акаунт цієї вкладки як підказку для нової. Лише коли вкладку видно (нова
   * вкладка має починати з акаунта тієї, де користувач щойно був, а не фонової) і не під час
   * перемикання (там активними на мить стають акаунти, які лише перевіряються). Вкладка без
   * активного акаунта підказку не чіпає. Акаунт, чийого запису в сховищі нема (вхід при
   * переповненому сховищі, який потім скасовується), підказкою не стає: інакше вона вказувала б
   * на акаунт, якого нема, а підказка до нього була б уже затерта.
   */
  private rememberLastActive(): void {
    const id = this.activeIdSignal();
    if (id !== null && !this.switchingSignal() && document.visibilityState === 'visible' && this.hasRecord(id)) {
      this.write(LAST_ACTIVE_KEY, id);
    }
  }

  /** Чи лежить запис акаунта в сховищі (без доступу до сховища — так, бо писати однаково нікуди). */
  private hasRecord(id: string): boolean {
    return !this.storageAvailable || this.readRecord(id) !== null;
  }

  /**
   * Підхоплює з `localStorage` запис акаунта, токени якого могла оновити інша вкладка.
   * Якщо refresh-токен у сховищі не той, що в памʼяті, бере в памʼять увесь запис зі сховища
   * й повертає його; інакше (той самий токен, запису чи акаунта нема) — `null`.
   */
  syncFromStorage(id: string): StoredAccount | null {
    const stored = this.readRecord(id);
    const current = this.accountsSignal().find((account) => account.id === id);
    if (!stored || !current || stored.refreshToken === current.refreshToken) {
      return null;
    }
    this.accountsSignal.set(this.accountsSignal().map((account) => (account.id === id ? stored : account)));
    return stored;
  }

  /**
   * Стирає все сховище браузера для цього застосунку (спільне й цієї вкладки) і забуває
   * акаунти. Інші вкладки дізнаються про це подією `storage` й теж лишаються без акаунтів.
   */
  wipe(): void {
    try {
      localStorage.clear();
      sessionStorage.clear();
    } catch {
      // Сховище недоступне — нічого очищати.
    }
    this.accountsSignal.set([]);
    this.activeIdSignal.set(null);
  }

  // ---- читання/запис ---------------------------------------------------

  private readIndex(): string[] {
    const raw = this.read(INDEX_KEY);
    if (!raw) {
      return [];
    }
    try {
      const value: unknown = JSON.parse(raw);
      return Array.isArray(value) ? value.filter((id): id is string => typeof id === 'string') : [];
    } catch {
      return [];
    }
  }

  private writeIndex(ids: readonly string[]): void {
    this.write(INDEX_KEY, JSON.stringify(ids));
  }

  private readRecord(id: string): StoredAccount | null {
    const raw = this.read(`${ACCOUNT_KEY_PREFIX}${id}`);
    if (!raw) {
      return null;
    }
    try {
      const account = toStoredAccount(JSON.parse(raw));
      return account?.id === id ? account : null;
    } catch {
      return null;
    }
  }

  private writeRecord(account: StoredAccount): void {
    this.write(`${ACCOUNT_KEY_PREFIX}${account.id}`, JSON.stringify(account));
  }

  /** Активний акаунт цієї вкладки: `id`, порожній рядок («нема») або `null` (ще не вибирали). */
  private readActive(): string | null {
    try {
      return sessionStorage.getItem(ACTIVE_KEY);
    } catch {
      return null;
    }
  }

  private writeActive(id: string | null): void {
    try {
      sessionStorage.setItem(ACTIVE_KEY, id ?? '');
    } catch {
      // Сховище недоступне — вибір живе лише в памʼяті.
    }
  }

  private read(key: string): string | null {
    try {
      return localStorage.getItem(key);
    } catch {
      return null;
    }
  }

  /** Попередження про переповнене сховище показано (закрито) або вже не потрібне. */
  dismissStorageFull(): void {
    this.storageFullSignal.set(false);
  }

  private write(key: string, value: string): void {
    try {
      localStorage.setItem(key, value);
    } catch {
      // Сховище недоступне або переповнене — лишаємося лише в памʼяті.
      if (this.storageAvailable) {
        this.storageFullSignal.set(true);
      }
    }
  }

  private removeKey(key: string): void {
    try {
      localStorage.removeItem(key);
    } catch {
      // Сховище недоступне — нічого прибирати.
    }
  }
}

/** Чи дає браузер читати сховище: коли дані сайтів заблоковано, вже саме звернення кидає помилку. */
function canReadStorage(): boolean {
  try {
    localStorage.getItem(INDEX_KEY);
    sessionStorage.getItem(ACTIVE_KEY);
    return true;
  } catch {
    return false;
  }
}

/**
 * Запис акаунта з того, що лежить у сховищі. Сховищу не довіряємо: його могла записати інша
 * версія застосунку чи хтось вручну. Без `id`, сервера чи токенів це не акаунт (`null`); решта
 * полів не того типу (або відсутні) стають значеннями за замовчуванням.
 */
function toStoredAccount(value: unknown): StoredAccount | null {
  if (value === null || typeof value !== 'object') {
    return null;
  }
  const record = value as Record<string, unknown>;
  const { id, server, accessToken, refreshToken } = record;
  if (
    typeof id !== 'string' ||
    typeof server !== 'string' ||
    typeof accessToken !== 'string' ||
    typeof refreshToken !== 'string'
  ) {
    return null;
  }
  const permissions = record['permissions'];
  return {
    id,
    server,
    identity: textOrEmpty(record['identity']),
    userId: textOrEmpty(record['userId']),
    email: textOrEmpty(record['email']),
    outdated: record['outdated'] === true,
    unavailable: record['unavailable'] === true,
    firstName: textOrEmpty(record['firstName']),
    lastName: textOrEmpty(record['lastName']),
    patronymic: textOrEmpty(record['patronymic']),
    avatarUrl: textOrNull(record['avatarUrl']),
    ngoName: textOrNull(record['ngoName']),
    ngoLogoUrl: textOrNull(record['ngoLogoUrl']),
    avatarThumb: thumbnailOrNull(record['avatarThumb']),
    ngoLogoThumb: thumbnailOrNull(record['ngoLogoThumb']),
    permissions: Array.isArray(permissions)
      ? permissions.filter((permission): permission is string => typeof permission === 'string')
      : [],
    roles: sanitizeRoles(record['roles']),
    accessToken,
    refreshToken,
    tokensIssuedAt: timeOrZero(record['tokensIssuedAt']),
    addedAt: timeOrZero(record['addedAt']),
    lastUsedAt: timeOrZero(record['lastUsedAt']),
  };
}

function textOrEmpty(value: unknown): string {
  return typeof value === 'string' ? value : '';
}

function textOrNull(value: unknown): string | null {
  return typeof value === 'string' ? value : null;
}

/** Момент часу в мс; не число (чи не скінченне) — `0`, тобто «невідомо». */
function timeOrZero(value: unknown): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : 0;
}

/** Мініатюра з сховища: лише коректна пара рядків (адреса й `data:`-посилання), інакше `null`. */
function thumbnailOrNull(value: unknown): AccountThumbnail | null {
  if (value === null || typeof value !== 'object') {
    return null;
  }
  const { url, data } = value as Record<string, unknown>;
  return typeof url === 'string' && typeof data === 'string' && data.startsWith('data:image/') ? { url, data } : null;
}

/**
 * Домен сервера лише з літер, латинських та українських (нижній регістр): `crm.example.org` →
 * `crmexampleorg`, `приклад.укр` → `прикладукр`; цифри, крапки, дефіси й решту символів
 * викинуто. Порожній результат (наприклад, IP-адреса) — `server`; довжину обмежено.
 *
 * Кириличний домен (зона `.укр`) у посиланні записано латиницею (`xn--…`), тому спершу його
 * треба розкодувати (`hostnameToUnicode`): інакше в `id` потрапили б літери цього запису.
 */
function domainLetters(server: string): string {
  let host = server;
  try {
    host = new URL(server).hostname;
  } catch {
    // Не посилання — беремо як є.
  }
  // Українська абетка: «а»–«щ», «ь», «ю», «я» та «ґ», «є», «і», «ї» (без «ъ», «ы», «э»).
  return hostnameToUnicode(host).toLowerCase().replace(/[^a-zа-щьюяґєії]/g, '').slice(0, 40) || 'server';
}

/** Короткий безпечний для ключа відбиток рядка (FNV-1a). */
function hash(text: string): string {
  let value = 0x811c9dc5;
  for (let i = 0; i < text.length; i++) {
    value ^= text.charCodeAt(i);
    value = Math.imul(value, 0x01000193) >>> 0;
  }
  return value.toString(16).padStart(8, '0');
}
