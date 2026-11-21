import uk from '../../../assets/i18n/uk.json';
import { joinList } from '../../shared/list-text/list-text';
import { IMAGE_MAX_MEGABYTES, IMAGE_TYPE_NAMES } from './image-types';

/**
 * Для тестів: текст із мовного файлу з підставленими значеннями — таким, яким його бачить
 * користувач: перелік форматів на місці `{{types}}` (`kind`: «та» чи «або» перед останнім) і
 * найбільший розмір на місці `{{size}}`.
 */
export function withImageTypes(text: string, kind: 'and' | 'or'): string {
  return withImageSize(text.replace('{{types}}', joinList(IMAGE_TYPE_NAMES, uk.common.list.separator, uk.common.list[kind])));
}

/** Для тестів: текст із найбільшим розміром зображення на місці `{{size}}`. */
export function withImageSize(text: string): string {
  return text.replace('{{size}}', String(IMAGE_MAX_MEGABYTES));
}
