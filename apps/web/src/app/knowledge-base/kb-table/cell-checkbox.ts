import { RichTextFormat } from '../../shared/rich-text/rich-text.directive';
import { joinBackground, richTextToPlain, splitBackground } from '../../shared/rich-text/rich-text';

/**
 * Прапорець у клітинці таблиці: вставляється кнопкою смуги дій і зберігається рядком — символом `☑`
 * (увімкнений) чи `☐` (вимкнений). Як і текст, його можна форматувати: колір тла клітинки (тег
 * `div` зі стилем, див. `rich-text.ts`) і напівжирний, курсив, підкреслення, закреслення (теги
 * навколо символу — на всю клітинку, виділяти в прапорці нічого).
 */
export const CHECKBOX_ON = '☑';
export const CHECKBOX_OFF = '☐';

/** Тексти, що стають увімкненим прапорцем (решта — вимкненим). */
const TRUE_TEXT = /^(?:так|т|true|yes|y|1|\+|✓|✔|☑|x|х)$/iu;
/** Тексти, що стають вимкненим прапорцем без втрат (решта тексту зникає — спершу питаємо). */
const FALSE_TEXT = /^(?:ні|н|false|no|n|0|-|—|✗|✘|☐)$/iu;

/** Теги форматування (як у `sanitizeRichText`). */
const FORMAT_TAGS: Readonly<Record<RichTextFormat, string>> = { bold: 'b', italic: 'i', underline: 'u', strikethrough: 's' };

const plain = (value: string | null | undefined) => richTextToPlain(splitBackground(value).html).trim();

/** Прапорець у клітинці (без огляду на форматування): `true` / `false`; не прапорець — `null`. */
export function checkboxState(value: string | null | undefined): boolean | null {
  const text = plain(value);
  return text === CHECKBOX_ON ? true : text === CHECKBOX_OFF ? false : null;
}

/** Увімкнути чи вимкнути прапорець, лишивши його форматування й колір тла. */
export function setCheckbox(value: string, checked: boolean): string {
  return value.replace(/[☑☐]/u, checked ? CHECKBOX_ON : CHECKBOX_OFF);
}

/** Прапорець замість тексту клітинки («так», «+», «1»… — увімкнений, решта — вимкнений); колір тла лишається. */
export function toCheckbox(value: string | null | undefined): string {
  return joinBackground(splitBackground(value).background, TRUE_TEXT.test(plain(value)) ? CHECKBOX_ON : CHECKBOX_OFF);
}

/** Текст, який зникне, якщо вставити прапорець (порожнє, «так», «ні» тощо не зникають). */
export function checkboxLosesText(value: string | null | undefined): boolean {
  const text = plain(value);
  return text !== '' && !TRUE_TEXT.test(text) && !FALSE_TEXT.test(text);
}

/** Чи прапорець має це форматування (тег навколо символу). */
export function checkboxHasFormat(value: string, format: RichTextFormat): boolean {
  return new RegExp(`<${FORMAT_TAGS[format]}>`).test(splitBackground(value).html);
}

/** Увімкнути чи вимкнути форматування прапорця: тег навколо символу; колір тла лишається. */
export function toggleCheckboxFormat(value: string, format: RichTextFormat): string {
  const { background, html } = splitBackground(value);
  const tag = FORMAT_TAGS[format];
  const toggled = checkboxHasFormat(value, format) ? html.replace(`<${tag}>`, '').replace(`</${tag}>`, '') : `<${tag}>${html}</${tag}>`;
  return joinBackground(background, toggled);
}
