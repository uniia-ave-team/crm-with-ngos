/**
 * Зміст «Бази знань» — те, що показує бокове меню, і з чого будується «Посібник». Сервер (запит
 * `GET …/tree`, див. `KnowledgeBaseService`) віддає масив, у якому кожен елемент — матеріал
 * (`id`, тип і назва) чи папка (`type: "folder"`, `id`, назва й матеріали в ній, `items`):
 *
 *   [
 *     { "id": "f1", "type": "folder", "title": "Розділ",
 *       "items": [{ "id": "p1", "type": "page", "title": "Документ" }] },
 *     { "id": "t1", "type": "customtable", "title": "Таблиця" }
 *   ]
 *
 * Головної сторінки («Посібник») у відповіді нема: її додає клієнт на початок масиву
 * (`withHomePage`), а назву «Головна» бере з мовного файлу.
 */

/** Тип матеріалу: головна («Посібник», лише на клієнті), таблиця чи сторінка (документ). */
export type KnowledgeBasePageType = 'home' | 'customtable' | 'page';

/** Матеріал бази знань: `id`, тип і назва (у головної назви нема, див. вище). */
export interface KnowledgeBasePage {
  readonly id: string;
  readonly type: KnowledgeBasePageType;
  readonly title: string;
}

/** Папка: `id`, назва й матеріали в ній (`items`; лише таблиці й сторінки). */
export interface KnowledgeBaseFolder {
  readonly id: string;
  readonly type: 'folder';
  readonly title: string;
  readonly items: readonly KnowledgeBasePage[];
}

export type KnowledgeBaseEntry = KnowledgeBasePage | KnowledgeBaseFolder;

export function isKnowledgeBaseFolder(entry: KnowledgeBaseEntry): entry is KnowledgeBaseFolder {
  return entry.type === 'folder';
}

/**
 * Куди додати новий матеріал, вибране кнопкою «+»: у папку (її `id` і назва) чи в корінь змісту
 * (`folder` — `null`).
 */
export interface KnowledgeBaseInsertPlace {
  readonly folder: { readonly id: string; readonly title: string } | null;
}

/** Дія з меню біля трьох крапок: додати в папку, редагувати, перемістити чи видалити. */
export type KnowledgeBaseAction = 'add' | 'edit' | 'move' | 'delete';

/** Що можна створити кнопкою «+»: таблицю, сторінку чи папку. */
export type KnowledgeBaseItemType = 'customtable' | 'page' | 'folder';

/**
 * Типи у списку «Тип» вікна «Додати» (у цьому порядку) з ключами підписів. Поки сервер уміє лише
 * таблиці, тож список вимкнений, а вибрана таблиця; сторінки й папки — TODO (див.
 * `KnowledgeBaseService.create`).
 */
export const KNOWLEDGE_BASE_ITEM_TYPES: readonly { readonly type: KnowledgeBaseItemType; readonly labelKey: string }[] = [
  { type: 'customtable', labelKey: 'knowledgeBase.types.customtable' },
  { type: 'page', labelKey: 'knowledgeBase.types.page' },
  { type: 'folder', labelKey: 'knowledgeBase.types.folder' },
];

/** Новий матеріал чи папка з вікна «Додати». */
export interface KnowledgeBaseDraft {
  readonly type: KnowledgeBaseItemType;
  readonly title: string;
  /** `null` — опису нема. */
  readonly description: string | null;
  /** Шаблон таблиці «за основу» (лише для таблиці); `null` — без шаблону. */
  readonly templateId: string | null;
  /** Папка, куди додати; `null` — корінь змісту. */
  readonly folderId: string | null;
}

/** Назва й опис матеріалу чи папки у вікні «Редагувати». */
export interface KnowledgeBaseDetails {
  readonly title: string;
  readonly description: string | null;
}

/** Іконка Ionicons для кожного типу матеріалу (поле `icon` бачить перевірка іконок `check-icons`). */
export const KNOWLEDGE_BASE_PAGE_TYPES: Record<KnowledgeBasePageType, { readonly icon: string }> = {
  home: { icon: 'home-outline' },
  page: { icon: 'document-outline' },
  customtable: { icon: 'grid-outline' },
};

/** Типи матеріалів, які приходять із сервера (головна — лише на клієнті). */
const SERVER_PAGE_TYPES: readonly KnowledgeBasePageType[] = ['customtable', 'page'];

/** Адреса «Бази знань» (головна — «Посібник»). */
export const KNOWLEDGE_BASE_URL = '/knowledge-base';

/**
 * Адреса, на якій відкривається матеріал: таблиця — `/knowledge-base/tables/{id}`, сторінка —
 * `/knowledge-base/pages/{id}` (вид сторінки запланований), головна — сам «Посібник». Папка не
 * відкривається — `null`.
 */
export function knowledgeBaseLink(entry: KnowledgeBaseEntry): readonly string[] | null {
  switch (entry.type) {
    case 'home':
      return [KNOWLEDGE_BASE_URL];
    case 'customtable':
      return [KNOWLEDGE_BASE_URL, 'tables', entry.id];
    case 'page':
      return [KNOWLEDGE_BASE_URL, 'pages', entry.id];
    case 'folder':
      return null;
  }
}

/** Матеріал чи папка змісту за `id` (у корені чи в папці); нема — `null`. */
export function findKnowledgeBaseEntry(entries: readonly KnowledgeBaseEntry[], id: string): KnowledgeBaseEntry | null {
  for (const entry of entries) {
    if (entry.id === id) {
      return entry;
    }
    if (isKnowledgeBaseFolder(entry)) {
      const page = entry.items.find((item) => item.id === id);
      if (page) {
        return page;
      }
    }
  }
  return null;
}

/** Головна сторінка, яку клієнт додає на початок змісту (у відповіді сервера її нема). */
export const KNOWLEDGE_BASE_HOME: KnowledgeBasePage = { id: 'home', type: 'home', title: '' };

/** Зміст із головною на початку. */
export function withHomePage(entries: readonly KnowledgeBaseEntry[]): KnowledgeBaseEntry[] {
  return [KNOWLEDGE_BASE_HOME, ...entries];
}

/**
 * Зміст із відповіді, як він є (`unknown`): лише придатні елементи. Кожен має непорожні `id` і
 * назву; папка — ще масив `items` (у ній лише таблиці й сторінки, вкладені папки відкидаються),
 * матеріал — відомий тип (`customtable`, `page`). Невідомі типи (сервер може додати нові)
 * пропускаються; не масив — порожньо.
 */
export function toKnowledgeBaseEntries(value: unknown): KnowledgeBaseEntry[] {
  if (!Array.isArray(value)) {
    return [];
  }
  return value.flatMap((item: unknown): KnowledgeBaseEntry[] => {
    const record = (item ?? {}) as Record<string, unknown>;
    if (record['type'] === 'folder') {
      const id = text(record['id']);
      const title = text(record['title']);
      const items = Array.isArray(record['items']) ? record['items'].flatMap(toPage) : [];
      return id && title ? [{ id, type: 'folder', title, items }] : [];
    }
    return toPage(item);
  });
}

function toPage(value: unknown): KnowledgeBasePage[] {
  const record = (value ?? {}) as Record<string, unknown>;
  const type = record['type'] as KnowledgeBasePageType;
  const id = text(record['id']);
  const title = text(record['title']);
  return id && title && SERVER_PAGE_TYPES.includes(type) ? [{ id, type, title }] : [];
}

const text = (value: unknown): string => (typeof value === 'string' ? value.trim() : '');
