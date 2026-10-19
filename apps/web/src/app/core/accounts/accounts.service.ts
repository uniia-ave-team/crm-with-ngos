import { Injectable, Signal, computed, signal } from '@angular/core';

/** Індекс: впорядкований список `id` акаунтів (JSON-масив рядків). */
const INDEX_KEY = 'yavir.accounts';
/** Запис акаунта: `yavir.account.<id>`. */
const ACCOUNT_KEY_PREFIX = 'yavir.account.';
/** `id` активного акаунта. */
const ACTIVE_KEY = 'yavir.activeAccount';

export const ACCOUNT_SCHEMA_VERSION = 1;

/**
 * Акаунт на цьому пристрої = пара «сервер + користувач». Запис містить усе, що
 * потрібно, щоб працювати від його імені й намалювати його плитку без запитів:
 * адресу підключення, токени та знімок даних (імʼя, прізвище, аватар, назва ГО).
 */
export interface StoredAccount {
  readonly version: number;
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
   * Застарілим (`outdated`) акаунт став через те, що його сервер не відповів, а
   * не через відхилені токени. Лише причина для показу (меню акаунтів); на
   * решту логіки не впливає. Повторний вхід скидає в `false`.
   */
  readonly unavailable: boolean;
  readonly firstName: string;
  readonly lastName: string;
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
   * Права користувача для побудови інтерфейсу (`GET /api/v1/users/me/permissions`):
   * унікальні рядки, як їх віддав сервер. Оновлюються разом із профілем (при
   * кожній зміні сторінки); порожньо — ще не отримано чи прав нема.
   */
  readonly permissions: readonly string[];
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

/** Що потрібно для додавання акаунта: сервер, токени й те, за чим його впізнати. */
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
  readonly firstName?: string;
  readonly lastName?: string;
  readonly avatarUrl?: string | null;
  readonly ngoName?: string | null;
  readonly ngoLogoUrl?: string | null;
  readonly permissions?: readonly string[];
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
    | 'avatarUrl'
    | 'ngoName'
    | 'ngoLogoUrl'
    | 'permissions'
    | 'accessToken'
    | 'refreshToken'
    | 'tokensIssuedAt'
    | 'lastUsedAt'
  >
>;

/**
 * Акаунти цього пристрою в `localStorage`: «запис на акаунт + індекс».
 *
 * - `yavir.accounts` — індекс (масив `id` за порядком додавання);
 * - `yavir.account.<id>` — запис акаунта (див. `StoredAccount`);
 * - `yavir.activeAccount` — `id` активного акаунта.
 *
 * Кожен запис пише лише той, хто оновлює саме цей акаунт (оновлення токенів
 * іде під Web Lock в `AuthService`), тож вкладки не затирають чужі зміни; для
 * інших вкладок зміни приходять подією `storage`. Сирітські записи й
 * непридатні дані прибираються під час читання. Сховище може бути недоступне
 * (приватний режим) — тоді акаунти живуть лише в памʼяті.
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

  constructor() {
    this.reload();
    // Інша вкладка змінила акаунти (додала, оновила токени, вийшла) — підхоплюємо.
    window.addEventListener('storage', (event) => {
      if (
        event.key === null ||
        event.key === INDEX_KEY ||
        event.key === ACTIVE_KEY ||
        event.key.startsWith(ACCOUNT_KEY_PREFIX)
      ) {
        this.reload();
      }
    });
  }

  /**
   * `id` акаунта: домен сервера (лише латинські літери) і короткий відбиток
   * сервера та ознаки користувача, напр. `localhost-a3f9c01b`. Домен у `id`
   * напряму показує, чий це акаунт, а відбиток розрізняє порти, протоколи й
   * користувачів. Це лише основа: справжню унікальність гарантує `add()`,
   * що розводить колізії числовим суфіксом.
   */
  static accountId(server: string, identity: string): string {
    return `${domainLetters(server)}-${hash(`${server}|${identity}`)}`;
  }


  /** Читає все зі сховища заново. */
  reload(): void {
    const ids = this.readIndex();
    const accounts: StoredAccount[] = [];
    for (const id of ids) {
      const account = this.readRecord(id);
      if (account) {
        accounts.push(account);
      }
    }
    // Записи, яких нема (або вони зіпсовані), з індексу прибираємо.
    if (accounts.length !== ids.length) {
      this.writeIndex(accounts.map((account) => account.id));
    }
    const storedActive = this.read(ACTIVE_KEY);
    this.accountsSignal.set(accounts);
    this.activeIdSignal.set(accounts.some((account) => account.id === storedActive) ? storedActive : null);
  }

  /**
   * Додає акаунт і робить його активним. Якщо такий (той самий сервер і
   * користувач) уже є — оновлює його токени, знімок лишається.
   */
  add(input: NewAccount): StoredAccount {
    const now = Date.now();
    // Той самий сервер і користувач — це той самий акаунт, хоч би який у нього `id`.
    const existing = this.accountsSignal().find(
      (account) => account.server === input.server && account.identity === input.identity,
    );
    const id = existing?.id ?? this.freeId(AccountsService.accountId(input.server, input.identity));
    const account: StoredAccount = {
      version: ACCOUNT_SCHEMA_VERSION,
      id,
      server: input.server,
      identity: input.identity,
      userId: input.userId ?? existing?.userId ?? '',
      email: input.email ?? existing?.email ?? '',
      // Вхід (нові токени) повертає акаунт у чинні.
      outdated: false,
      unavailable: false,
      firstName: input.firstName ?? existing?.firstName ?? '',
      lastName: input.lastName ?? existing?.lastName ?? '',
      avatarUrl: input.avatarUrl !== undefined ? input.avatarUrl : (existing?.avatarUrl ?? null),
      ngoName: input.ngoName !== undefined ? input.ngoName : (existing?.ngoName ?? null),
      ngoLogoUrl: input.ngoLogoUrl !== undefined ? input.ngoLogoUrl : (existing?.ngoLogoUrl ?? null),
      permissions: input.permissions ?? existing?.permissions ?? [],
      accessToken: input.accessToken,
      refreshToken: input.refreshToken,
      tokensIssuedAt: now,
      addedAt: existing?.addedAt ?? now,
      lastUsedAt: now,
    };
    this.writeRecord(account);
    const accounts = existing
      ? this.accountsSignal().map((current) => (current.id === id ? account : current))
      : [...this.accountsSignal(), account];
    if (!existing) {
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

  /** Змінює поля запису акаунта (`id` якого вже є); не існує — нічого не робить. */
  update(id: string, patch: AccountPatch): void {
    const current = this.accountsSignal().find((account) => account.id === id);
    if (!current) {
      return;
    }
    const next: StoredAccount = { ...current, ...patch };
    this.writeRecord(next);
    this.accountsSignal.set(this.accountsSignal().map((account) => (account.id === id ? next : account)));
  }

  /**
   * Позначає акаунт застарілим (`outdated: true`; `unavailable` — причина: сервер
   * не відповів, а не відхилив токени): його не видаляємо. Якщо він
   * був активним, активним стає перший чинний акаунт за порядком додавання.
   * Чинних не лишилося (застарілими стали **всі**) — памʼять очищається
   * повністю (`wipe`).
   */
  markOutdated(id: string, unavailable = false): void {
    if (!this.accountsSignal().some((account) => account.id === id)) {
      return;
    }
    this.update(id, { outdated: true, unavailable });
    this.keepActiveAccount(id);
  }

  /**
   * Видаляє акаунт. Якщо він був активним, активним стає перший чинний акаунт
   * за порядком додавання; чинних не лишилося — памʼять очищається повністю
   * (`wipe`).
   */
  remove(id: string): void {
    const accounts = this.accountsSignal().filter((account) => account.id !== id);
    if (accounts.length === this.accountsSignal().length) {
      return;
    }
    this.removeKey(`${ACCOUNT_KEY_PREFIX}${id}`);
    this.writeIndex(accounts.map((account) => account.id));
    this.accountsSignal.set(accounts);
    this.keepActiveAccount(id);
  }

  /**
   * Після того як акаунт `changedId` застарів чи був видалений: чинних акаунтів
   * нема взагалі — `wipe()` (як при виході; тимчасово, згодом це буде замінено);
   * цей акаунт був активним — активним стає перший чинний.
   */
  private keepActiveAccount(changedId: string): void {
    const valid = this.accountsSignal().find((account) => !account.outdated);
    if (!valid) {
      this.wipe();
      return;
    }
    if (this.activeIdSignal() === changedId) {
      this.setActive(valid.id);
    }
  }

  /** Робить акаунт активним (`null` — зняти вибір). Неіснуючий `id` ігнорується. */
  setActive(id: string | null): void {
    if (id !== null && !this.accountsSignal().some((account) => account.id === id)) {
      return;
    }
    if (id === null) {
      this.removeKey(ACTIVE_KEY);
    } else {
      this.write(ACTIVE_KEY, id);
    }
    this.activeIdSignal.set(id);
  }

  /**
   * Підхоплює з `localStorage` запис акаунта, який міг оновити інша вкладка.
   * Повертає його, якщо він відрізняється від того, що в памʼяті (і
   * застосовує), інакше `null`.
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

  /** Стирає все сховище браузера для цього застосунку й забуває акаунти. */
  wipe(): void {
    try {
      localStorage.clear();
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
      const value: unknown = JSON.parse(raw);
      return isStoredAccount(value) && value.id === id ? normalize(value) : null;
    } catch {
      return null;
    }
  }

  private writeRecord(account: StoredAccount): void {
    this.write(`${ACCOUNT_KEY_PREFIX}${account.id}`, JSON.stringify(account));
  }

  private read(key: string): string | null {
    try {
      return localStorage.getItem(key);
    } catch {
      return null;
    }
  }

  private write(key: string, value: string): void {
    try {
      localStorage.setItem(key, value);
    } catch {
      // Сховище недоступне або переповнене — лишаємося лише в памʼяті.
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

/** Доповнює запис полями за замовчуванням (на випадок запису неповної версії). */
function normalize(account: StoredAccount): StoredAccount {
  return {
    ...account,
    version: account.version ?? ACCOUNT_SCHEMA_VERSION,
    userId: account.userId ?? '',
    identity: account.identity ?? '',
    email: account.email ?? '',
    outdated: account.outdated === true,
    unavailable: account.unavailable === true,
    firstName: account.firstName ?? '',
    lastName: account.lastName ?? '',
    avatarUrl: account.avatarUrl ?? null,
    ngoName: account.ngoName ?? null,
    ngoLogoUrl: account.ngoLogoUrl ?? null,
    permissions: Array.isArray(account.permissions)
      ? account.permissions.filter((permission): permission is string => typeof permission === 'string')
      : [],
    tokensIssuedAt: account.tokensIssuedAt ?? 0,
    addedAt: account.addedAt ?? 0,
    lastUsedAt: account.lastUsedAt ?? 0,
  };
}

function isStoredAccount(value: unknown): value is StoredAccount {
  if (value === null || typeof value !== 'object') {
    return false;
  }
  const record = value as Record<string, unknown>;
  return (
    typeof record['id'] === 'string' &&
    typeof record['server'] === 'string' &&
    typeof record['accessToken'] === 'string' &&
    typeof record['refreshToken'] === 'string'
  );
}

/**
 * Домен сервера лише з латинських літер (нижній регістр): `crm.example.org` →
 * `crmexampleorg`; цифри, крапки, дефіси й решту символів викинуто. Порожній
 * результат (наприклад, IP-адреса) — `server`; довжину обмежено.
 */
function domainLetters(server: string): string {
  let host = server;
  try {
    host = new URL(server).hostname;
  } catch {
    // Не посилання — беремо як є.
  }
  return host.toLowerCase().replace(/[^a-z]/g, '').slice(0, 40) || 'server';
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
