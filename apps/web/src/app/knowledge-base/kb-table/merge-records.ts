import { CustomRecord, CustomRecordData } from '../../core/custom-tables/custom-records.service';

/**
 * Запис у виді таблиці: `key` — сталий ключ рядка (рядок не перемальовується, коли новий запис
 * отримує `id` від сервера, тож курсор лишається в клітинці), `serverId` — `id` на сервері (`null` —
 * ще не створено), `data` — значення полів за ключами.
 */
export interface LocalRecord {
  readonly key: string;
  readonly serverId: string | null;
  readonly data: CustomRecordData;
}

/** Що не чіпати під час злиття: рядки з незбереженими змінами й клітинку, в якій зараз курсор. */
export interface MergeGuard {
  /** Ключі рядків (`LocalRecord.key`) з незбереженими змінами. */
  readonly dirtyKeys: ReadonlySet<string>;
  /** Клітинка з курсором (ключ рядка й ключ поля) або `null`. */
  readonly focused: { readonly key: string; readonly field: string } | null;
}

/**
 * Записи з сервера (свіжа перевірка) поверх показаних: лише те, що змінилося. Незмінений запис
 * лишається тим самим обʼєктом, а в зміненому — лише змінені значення (тож перемальовуються лише
 * змінені клітинки). Порядок — як на сервері; нові записи додаються, видалені на сервері зникають.
 * Рядки з незбереженими змінами лишаються як є, клітинка з курсором — теж, новий рядок, ще не
 * створений на сервері (`serverId` — `null`), — у кінці.
 */
export function mergeRecords(local: readonly LocalRecord[], server: readonly CustomRecord[], guard: MergeGuard): readonly LocalRecord[] {
  const byServerId = new Map(local.filter((record) => record.serverId !== null).map((record) => [record.serverId!, record]));
  const merged = server.map((incoming): LocalRecord => {
    const current = byServerId.get(incoming.id);
    if (!current) {
      return { key: incoming.id, serverId: incoming.id, data: incoming.data };
    }
    if (guard.dirtyKeys.has(current.key)) {
      return current;
    }
    let data: Record<string, unknown> | null = null;
    for (const [field, value] of Object.entries(incoming.data)) {
      const isFocused = guard.focused?.key === current.key && guard.focused.field === field;
      if (!isFocused && current.data[field] !== value) {
        data ??= { ...current.data };
        data[field] = value;
      }
    }
    return data ? { ...current, data } : current;
  });
  // Видалений на сервері рядок із незбереженими змінами лишається (зміна піде, і сервер відповість).
  const serverIds = new Set(server.map((record) => record.id));
  const keptDirty = local.filter((record) => record.serverId !== null && !serverIds.has(record.serverId) && guard.dirtyKeys.has(record.key));
  const unsaved = local.filter((record) => record.serverId === null);
  const result = [...merged, ...keptDirty, ...unsaved];
  // Нічого не змінилося — той самий масив (без зайвого перемальовування).
  return result.length === local.length && result.every((record, index) => record === local[index]) ? local : result;
}
