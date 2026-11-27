import { HttpClient } from '@angular/common/http';
import { InjectionToken, Injectable, PendingTasks, Signal, computed, effect, inject, signal, untracked } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';

import { TranslateService } from '@ngx-translate/core';

import { AccountsService } from '../accounts/accounts.service';
import { AuthService } from '../auth/auth.service';
import { PERMISSIONS, Permission } from '../auth/permissions';
import { ConnectionService } from '../connection/connection.service';
import { CustomTableDetails, CustomTablesService } from '../custom-tables/custom-tables.service';
import { RequestFailure } from '../http/request-failure';
import { REQUEST_TIMEOUT_MS } from '../http/timeouts';
import { CurrentUserService } from '../users/current-user.service';
import {
  KnowledgeBaseAction,
  KnowledgeBaseDetails,
  KnowledgeBaseDraft,
  KnowledgeBaseEntry,
  KnowledgeBaseFolder,
  KnowledgeBaseInsertPlace,
  KnowledgeBaseItemType,
  KnowledgeBasePage,
  isKnowledgeBaseFolder,
  toKnowledgeBaseEntries,
  withHomePage,
} from './knowledge-base';

/**
 * Шлях загального запиту змісту бази знань (`GET`, увесь зміст організації одним деревом із папками,
 * таблицями й сторінками). Запиту ще нема — його попросили в бекенду; шлях умовний.
 */
export const KNOWLEDGE_BASE_TREE_PATH = '/api/v1/knowledge-base/tree';

/** Зразковий зміст у збірці застосунку (той самий формат, що й відповідь `…/tree`). */
export const KNOWLEDGE_BASE_SAMPLE_URL = 'assets/knowledge-base/sample.json';

/**
 * Звідки береться зміст:
 * - `customtables` (зараз) — список таблиць організації (`GET /api/v1/customtables`): у системі
 *   поки лише таблиці, тож зміст — таблиці в корені, без папок і сторінок;
 * - `tree` — загальний запит `…/knowledge-base/tree` (коли бекенд його зробить, досить змінити
 *   значення за замовчуванням);
 * - `sample` — зразковий файл у збірці (для перевірки вигляду папок і сторінок).
 */
export type KnowledgeBaseSource = 'customtables' | 'tree' | 'sample';

export const KNOWLEDGE_BASE_SOURCE = new InjectionToken<KnowledgeBaseSource>('KNOWLEDGE_BASE_SOURCE', {
  providedIn: 'root',
  factory: () => 'customtables',
});

/**
 * Права дій для кожного типу: створити, змінити, видалити. Для сторінок і папок прав на сервері
 * ще нема — `null`, і дій з ними інтерфейс не пропонує (TODO, коли зʼявляться).
 */
const KNOWLEDGE_BASE_RIGHTS: Record<KnowledgeBaseItemType, { create: Permission; update: Permission; delete: Permission } | null> = {
  customtable: { create: PERMISSIONS.createCustomTable, update: PERMISSIONS.updateCustomTable, delete: PERMISSIONS.deleteCustomTable },
  page: null,
  folder: null,
};

/** Ключ першого (й поки єдиного) стовпчика таблиці без шаблону: сервер вимагає хоча б один. */
export const EMPTY_TABLE_FIELD_KEY = 'name';

/** Результат дії: виконано, не вдалося (як у запиту) чи тип ще не підтримується (`unsupported`). */
/** `gone` — матеріал уже видалили (наприклад, на іншому пристрої). */
export type KnowledgeBaseResult = 'done' | RequestFailure | 'unsupported' | 'gone';

/**
 * Матеріал, відкритий у вікні «Редагувати»: назва й опис для форми, а для таблиці — ще вся її
 * структура (поля), яку `saveEdit` поверне серверу без змін.
 */
export interface KnowledgeBaseEditing extends KnowledgeBaseDetails {
  readonly entry: KnowledgeBaseEntry;
  readonly table: CustomTableDetails | null;
}

/** Стан змісту: завантажується, є (`entries`) чи не вдалося. */
export interface KnowledgeBaseState {
  readonly status: 'loading' | 'ready' | 'failed';
  readonly entries: readonly KnowledgeBaseEntry[];
}

/**
 * Зміст «Бази знань» (`knowledge-base.ts`): з нього бокове меню будує дерево, а сторінка —
 * «Посібник». Підвантажується при першому зверненні (`load`) і далі спільний; повтор після
 * невдачі — знову `load`. Відповідь (див. `KNOWLEDGE_BASE_SOURCE`) перевіряється поелементно, а
 * на початок змісту додається головна сторінка (`withHomePage`): сервер її не віддає.
 *
 * Дії з матеріалами (`create`, `openForEdit` / `saveEdit`, `delete`) обирають запит за типом
 * матеріалу: зараз є лише таблиці (`/api/v1/customtables`), для сторінок і папок — TODO
 * (`unsupported`). Які дії доступні, кажуть `canCreate` і `can` (права з `KNOWLEDGE_BASE_RIGHTS`).
 */
@Injectable({ providedIn: 'root' })
export class KnowledgeBaseService {
  private readonly http = inject(HttpClient);
  private readonly connection = inject(ConnectionService);
  private readonly source = inject(KNOWLEDGE_BASE_SOURCE);
  private readonly customTables = inject(CustomTablesService);
  private readonly currentUser = inject(CurrentUserService);
  private readonly translate = inject(TranslateService);
  /** Завантаження змісту — задача застосунку: `whenStable` (тести, пререндер) його дочікується. */
  private readonly pendingTasks = inject(PendingTasks);

  private readonly auth = inject(AuthService);
  private readonly accounts = inject(AccountsService);

  private readonly stateSignal = signal<KnowledgeBaseState>({ status: 'loading', entries: [] });
  readonly state: Signal<KnowledgeBaseState> = this.stateSignal.asReadonly();
  /** Для кого завантажено зміст: акаунт (`AuthService.sessionId`) і сервер; інший — зміст чужий. */
  private loadedFor: string | null = null;

  constructor() {
    // Інший акаунт (вихід, вхід, перемикання) — зміст попередньої організації забуваємо. Тимчасове
    // перемикання (пошук акаунта) зміст не скидає, як і профіль у `CurrentUserService`.
    effect(() => {
      const owner = this.owner();
      const switching = this.accounts.switching();
      untracked(() => {
        if (!switching && this.loadedFor !== null && this.loadedFor !== owner) {
          this.loadedFor = null;
          this.request = null;
          this.stateSignal.set({ status: 'loading', entries: [] });
        }
      });
    });
  }

  /** Чий зміст потрібен зараз: активний акаунт на своєму сервері. */
  private owner(): string {
    return `${this.auth.sessionId() ?? ''}|${this.connection.serverUrl()}`;
  }

  private request: Promise<void> | null = null;

  private readonly insertRequestSignal = signal<KnowledgeBaseInsertPlace | null>(null);
  /**
   * Останнє натискання «+» (у плашці папки, біля «База знань», плаваюча кнопка на телефоні): куди
   * додати матеріал. Функції додавання ще нема — коли зʼявиться, вона братиме місце звідси.
   */
  readonly insertRequest: Signal<KnowledgeBaseInsertPlace | null> = this.insertRequestSignal.asReadonly();

  /** Папки змісту (для вибору «з папки» / «до папки» при переміщенні). */
  readonly folders: Signal<readonly KnowledgeBaseFolder[]> = computed(() => this.stateSignal().entries.filter(isKnowledgeBaseFolder));

  /** Підвантажує зміст, якщо його ще нема (чи минулий раз не вдалося); запит, що йде, не дублює. */
  load(): Promise<void> {
    if (this.stateSignal().status === 'ready' && this.loadedFor === this.owner()) {
      return Promise.resolve();
    }
    return this.startFetch();
  }

  /** Завантажує зміст заново (після створення матеріалу: місце в списку визначає сервер). */
  reload(): Promise<void> {
    return this.startFetch();
  }

  /** Натиснули «+»: запамʼятовує, куди додати матеріал (див. `insertRequest`). */
  requestInsert(place: KnowledgeBaseInsertPlace): void {
    this.insertRequestSignal.set(place);
  }

  /** Вікно «Додати» взяло запит у роботу (наступний «+» відкриє його знову). */
  clearInsertRequest(): void {
    this.insertRequestSignal.set(null);
  }

  /** Чи можна щось створити (є право створювати хоч один тип): від цього залежать кнопки «+». */
  readonly canCreate: Signal<boolean> = computed(() => this.creatableTypes().length > 0);

  /** Типи, які користувач може створити (поки лише таблиці з `CreateCustomTable`). */
  readonly creatableTypes: Signal<readonly KnowledgeBaseItemType[]> = computed(() => {
    const granted = this.currentUser.permissions();
    return (Object.keys(KNOWLEDGE_BASE_RIGHTS) as KnowledgeBaseItemType[]).filter((type) => {
      const rights = KNOWLEDGE_BASE_RIGHTS[type];
      return !!rights && granted.includes(rights.create);
    });
  });

  /**
   * Чи доступна дія з матеріалом чи папкою: «Редагувати» — з правом зміни його типу, «Видалити» —
   * з правом видалення, «Додати в папку» — якщо можна щось створити. «Перемістити» — запланована
   * (сервер ще не вміє), тож `false`.
   */
  can(action: KnowledgeBaseAction, entry: KnowledgeBaseEntry): boolean {
    if (entry.type === 'home') {
      return false;
    }
    const rights = KNOWLEDGE_BASE_RIGHTS[entry.type];
    const granted = this.currentUser.permissions();
    switch (action) {
      case 'add':
        return isKnowledgeBaseFolder(entry) && this.canCreate();
      case 'edit':
        return !!rights && granted.includes(rights.update);
      case 'delete':
        return !!rights && granted.includes(rights.delete);
      case 'move':
        return false;
    }
  }

  /** Чи є для матеріалу чи папки хоч одна дія (інакше трьох крапок не показуємо). */
  hasActions(entry: KnowledgeBaseEntry): boolean {
    return (['add', 'edit', 'move', 'delete'] as const).some((action) => this.can(action, entry));
  }

  /**
   * Створює матеріал чи папку з вікна «Додати» й після успіху перезавантажує зміст. Таблиця — `POST
   * /api/v1/customtables/from-template` (з шаблоном) чи `POST /api/v1/customtables` (без шаблону;
   * сервер вимагає хоча б один стовпчик, тож один — текстовий «Назва»).
   */
  async create(draft: KnowledgeBaseDraft): Promise<KnowledgeBaseResult> {
    let result: KnowledgeBaseResult;
    switch (draft.type) {
      case 'customtable': {
        // TODO: папки таблиці (`draft.folderId`) — коли сервер їх підтримуватиме.
        const created = draft.templateId
          ? await this.customTables.createFromTemplate({ templateId: draft.templateId, name: draft.title, description: draft.description })
          : await this.customTables.create({
              name: draft.title,
              description: draft.description,
              fields: [
                {
                  name: this.translate.instant('knowledgeBase.dialogs.add.emptyTableField') as string,
                  key: EMPTY_TABLE_FIELD_KEY,
                  dataType: 0,
                  isRequired: false,
                  order: 0,
                },
              ],
            });
        result = typeof created === 'string' ? created : 'done';
        break;
      }
      case 'page':
      case 'folder':
        // TODO: створення сторінок і папок — коли на сервері зʼявляться запити для них.
        return 'unsupported';
    }
    if (result === 'done') {
      await this.reload();
    }
    return result;
  }

  /** Підвантажує назву й опис для вікна «Редагувати» (таблиця — `GET /api/v1/customtables/{id}`). */
  async openForEdit(entry: KnowledgeBaseEntry): Promise<KnowledgeBaseEditing | RequestFailure | 'unsupported' | 'gone'> {
    switch (entry.type) {
      case 'customtable': {
        const table = await this.customTables.get(entry.id);
        return typeof table === 'string' ? table : { entry, table, title: table.name, description: table.description };
      }
      case 'home':
      case 'page':
      case 'folder':
        // TODO: редагування сторінок і папок — коли на сервері зʼявляться запити для них.
        return 'unsupported';
    }
  }

  /**
   * Зберігає назву й опис. Таблиця — `PUT /api/v1/customtables/{id}`: сервер приймає назву й опис лише
   * разом зі структурою, а поле, якого нема в списку, видаляє разом з усіма його значеннями. Тож
   * структура — свіжа, з `GET` перед самим збереженням, а не та, що була при відкритті вікна (інакше
   * колонку, яку тим часом додав хтось інший, було б видалено).
   */
  async saveEdit(editing: KnowledgeBaseEditing, details: KnowledgeBaseDetails): Promise<KnowledgeBaseResult> {
    let result: KnowledgeBaseResult;
    switch (editing.entry.type) {
      case 'customtable':
        result = editing.table ? await this.saveTable(editing.table.id, details) : 'error';
        break;
      case 'home':
      case 'page':
      case 'folder':
        // TODO: редагування сторінок і папок.
        return 'unsupported';
    }
    if (result === 'done') {
      this.rename(editing.entry.id, details.title);
    }
    return result;
  }

  /** Таблиця зі свіжою структурою (`GET`) і новими назвою й описом. */
  private async saveTable(id: string, details: KnowledgeBaseDetails): Promise<KnowledgeBaseResult> {
    const fresh = await this.customTables.get(id);
    if (typeof fresh === 'string') {
      return fresh;
    }
    return this.customTables.update({ ...fresh, name: details.title, description: details.description });
  }

  /** Видаляє матеріал чи папку (таблиця — `DELETE /api/v1/customtables/{id}`). */
  async delete(entry: KnowledgeBaseEntry): Promise<KnowledgeBaseResult> {
    let result: KnowledgeBaseResult;
    switch (entry.type) {
      case 'customtable':
        result = await this.customTables.remove(entry.id);
        break;
      case 'home':
      case 'page':
      case 'folder':
        // TODO: видалення сторінок і папок (папки — разом із вмістом).
        return 'unsupported';
    }
    if (result === 'done') {
      this.remove(entry.id);
    }
    return result;
  }

  /** Папка, у якій лежить матеріал; `null` — у корені змісту (без папки). */
  folderOf(id: string): KnowledgeBaseFolder | null {
    return this.folders().find((folder) => folder.items.some((item) => item.id === id)) ?? null;
  }

  // Зміни змісту в памʼяті: дерево й «Посібник» оновлюються одразу, без повторного завантаження.
  // `rename` і `remove` викликаються після успішного запиту (`saveEdit`, `delete`); `move` — поки
  // лише в памʼяті (переміщення заплановане: запиту на сервері нема).

  /** Перейменовує матеріал чи папку. */
  rename(id: string, title: string): void {
    const renamePage = (page: KnowledgeBasePage): KnowledgeBasePage => (page.id === id ? { ...page, title } : page);
    this.updateEntries((entries) =>
      entries.map((entry) =>
        isKnowledgeBaseFolder(entry)
          ? { ...entry, title: entry.id === id ? title : entry.title, items: entry.items.map(renamePage) }
          : renamePage(entry),
      ),
    );
  }

  /** Видаляє матеріал чи папку (папку — разом з усім її змістом). */
  remove(id: string): void {
    this.updateEntries((entries) =>
      entries
        .filter((entry) => entry.id !== id)
        .map((entry) => (isKnowledgeBaseFolder(entry) ? { ...entry, items: entry.items.filter((item) => item.id !== id) } : entry)),
    );
  }

  /** Переносить матеріал у кінець папки `folderId` чи змісту (`null` — без папки). */
  move(id: string, folderId: string | null): void {
    const page = this.findPage(id);
    if (!page) {
      return;
    }
    this.updateEntries((entries) => {
      const without = entries
        .filter((entry) => entry.id !== id)
        .map((entry) => (isKnowledgeBaseFolder(entry) ? { ...entry, items: entry.items.filter((item) => item.id !== id) } : entry));
      if (folderId === null) {
        return [...without, page];
      }
      return without.map((entry) => (isKnowledgeBaseFolder(entry) && entry.id === folderId ? { ...entry, items: [...entry.items, page] } : entry));
    });
  }

  /** Матеріал (не папка) за `id` — у корені чи в папці. */
  private findPage(id: string): KnowledgeBasePage | null {
    for (const entry of this.stateSignal().entries) {
      if (isKnowledgeBaseFolder(entry)) {
        const page = entry.items.find((item) => item.id === id);
        if (page) {
          return page;
        }
      } else if (entry.id === id) {
        return entry;
      }
    }
    return null;
  }

  private updateEntries(change: (entries: readonly KnowledgeBaseEntry[]) => readonly KnowledgeBaseEntry[]): void {
    const state = this.stateSignal();
    if (state.status === 'ready') {
      this.stateSignal.set({ status: 'ready', entries: change(state.entries) });
    }
  }

  /** Запит змісту; той, що вже йде, не дублює. */
  private startFetch(): Promise<void> {
    if (!this.request) {
      const done = this.pendingTasks.add();
      const request: Promise<void> = this.fetch().finally(() => {
        // Тим часом акаунт змінився й пішов новий запит — його не чіпаємо.
        if (this.request === request) {
          this.request = null;
        }
        done();
      });
      this.request = request;
    }
    return this.request;
  }

  private async fetch(): Promise<void> {
    // Перезавантаження (після створення) не ховає наявний зміст, поки йде запит.
    if (this.stateSignal().status !== 'ready') {
      this.stateSignal.set({ status: 'loading', entries: [] });
    }
    const owner = this.owner();
    const entries = await this.fetchEntries();
    // Поки йшов запит, акаунт змінився — це зміст іншої організації.
    if (owner !== this.owner()) {
      return;
    }
    this.loadedFor = owner;
    this.stateSignal.set(entries ? { status: 'ready', entries: withHomePage(entries) } : { status: 'failed', entries: [] });
  }

  /** Зміст із джерела `KNOWLEDGE_BASE_SOURCE`; не вдалося — `null`. */
  private async fetchEntries(): Promise<KnowledgeBaseEntry[] | null> {
    if (this.source === 'customtables') {
      const tables = await this.customTables.listAll();
      return typeof tables === 'string' ? null : tables.map((table) => ({ id: table.id, type: 'customtable', title: table.name }));
    }
    const url = this.source === 'tree' ? `${this.connection.serverUrl()}${KNOWLEDGE_BASE_TREE_PATH}` : KNOWLEDGE_BASE_SAMPLE_URL;
    try {
      const body = await firstValueFrom(this.http.get<unknown>(url).pipe(timeout(REQUEST_TIMEOUT_MS)));
      return toKnowledgeBaseEntries(body);
    } catch (error) {
      console.warn('[KnowledgeBaseService] Не вдалося завантажити зміст бази знань', error);
      return null;
    }
  }
}
