import { CustomTableField } from '../../core/custom-tables/custom-tables.service';

/** Початок `id` нового поля, поки сервер не дав справжній (у `PUT` таке поле йде з `id: null`). */
export const NEW_FIELD_PREFIX = 'new:';

export const isNewField = (field: CustomTableField) => field.id.startsWith(NEW_FIELD_PREFIX);


/**
 * Свої зміни структури поверх свіжої з сервера. Сервер приймає структуру лише цілком і поле, якого
 * нема в списку, видаляє разом з усіма його значеннями, тож надсилати свій старий список не можна:
 * колонку, яку тим часом додав хтось інший, було б видалено. Тому за основу — свіжа структура
 * (`fresh`, з `GET` перед самим збереженням), а на неї — лише те, що змінили тут відносно
 * останньої відомої (`base`; `local` — що зараз у виді):
 * - видалені тут поля (були в `base`, нема в `local`) — прибираються;
 * - перейменовані тут (назва в `local` інша, ніж у `base`) — з новою назвою; чужі перейменування
 *   лишаються;
 * - нові тут (тимчасовий `id` з `NEW_FIELD_PREFIX`) — одразу після свого сусіда зліва в `local`
 *   (найближчого наявного), на початку, якщо такого нема.
 * Решта — як на сервері (чужі нові поля, порядок за позиціями). `local` — у своєму порядку.
 * Позиції — заново, 0, 1, 2…
 */
export function mergeFields(
  base: readonly CustomTableField[],
  local: readonly CustomTableField[],
  fresh: readonly CustomTableField[],
): readonly CustomTableField[] {
  const baseById = new Map(base.map((field) => [field.id, field]));
  const localById = new Map(local.filter((field) => !isNewField(field)).map((field) => [field.id, field]));
  const deleted = new Set(base.filter((field) => !localById.has(field.id)).map((field) => field.id));

  // Сервер може віддати поля не за порядком — спершу за позицією.
  const result: CustomTableField[] = [...fresh]
    .sort((a, b) => Number(a.order) - Number(b.order))
    .filter((field) => !deleted.has(field.id))
    .map((field) => {
      const mine = localById.get(field.id);
      const before = baseById.get(field.id);
      return mine && before && mine.name !== before.name ? { ...field, name: mine.name } : field;
    });

  let anchor = -1;
  for (const field of local) {
    if (!isNewField(field)) {
      const at = result.findIndex((candidate) => candidate.id === field.id);
      if (at >= 0) {
        anchor = at;
      }
      continue;
    }
    result.splice(++anchor, 0, field);
  }
  return result.map((field, order) => ({ ...field, order }));
}
