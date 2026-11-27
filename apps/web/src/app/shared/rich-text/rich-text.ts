/**
 * Текст із простим форматуванням: HTML лише з `<b>`, `<i>`, `<u>`, `<s>` і `<br>` (переноси рядків).
 * Так зберігається значення текстової клітинки таблиці, коли частину тексту виділили напівжирним,
 * курсивом, підкресленням чи закресленням. Усе інше (теги, атрибути, стилі) відкидається, лишається
 * текст.
 */

/** Теги, які лишаються (синоніми зводяться до них: `strong` → `b` тощо). */
const TAGS: Readonly<Record<string, string>> = {
  B: 'b',
  STRONG: 'b',
  I: 'i',
  EM: 'i',
  U: 'u',
  INS: 'u',
  S: 's',
  STRIKE: 's',
  DEL: 's',
};

/** Блоки, після яких — перенос рядка (так редактор браузера оформлює новий рядок). */
const BLOCKS = new Set(['DIV', 'P', 'LI']);

/** Елементи, вміст яких — не текст для людини (код, стилі, вбудовані об'єкти): відкидаються цілком. */
const DROPPED = new Set(['SCRIPT', 'STYLE', 'TEMPLATE', 'NOSCRIPT', 'IFRAME', 'OBJECT', 'EMBED', 'SVG', 'MATH']);

const escape = (text: string) => text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

/** Скільки очищених значень памʼятати: таблиця очищує кожну клітинку на кожне перемальовування. */
const CACHE_SIZE = 1000;
const cache = new Map<string, string>();

/**
 * HTML з редактора (чи з сервера) → лише дозволене форматування; порожній текст — порожній рядок.
 * Значення з сервера може бути будь-яким (записати його можна й в обхід застосунку, через API), тож
 * усе, що йде в `innerHTML`, проходить тут: розбір — у документі `DOMParser`, де скрипти не
 * виконуються й зображення не вантажаться, а назад лишаються тільки текст і теги `b`, `i`, `u`,
 * `s`, `br` без атрибутів.
 */
export function sanitizeRichText(html: string): string {
  if (!/[<&]/.test(html)) {
    return html;
  }
  const cached = cache.get(html);
  if (cached !== undefined) {
    return cached;
  }
  const doc = new DOMParser().parseFromString(`<body>${html}</body>`, 'text/html');
  // Перенос у самому кінці редактор додає «про запас» — він не потрібен.
  const out = walk(doc.body).replace(/(<br>)+$/, '');
  if (cache.size >= CACHE_SIZE) {
    cache.delete(cache.keys().next().value!);
  }
  cache.set(html, out);
  return out;
}

function walk(node: Node): string {
  let out = '';
  node.childNodes.forEach((child) => {
    if (child.nodeType === Node.TEXT_NODE) {
      out += escape(child.textContent ?? '');
      return;
    }
    if (child.nodeType !== Node.ELEMENT_NODE) {
      return;
    }
    const element = child as Element;
    if (DROPPED.has(element.tagName.toUpperCase())) {
      return;
    }
    if (element.tagName === 'BR') {
      out += '<br>';
      return;
    }
    const inner = walk(element);
    const tag = TAGS[element.tagName];
    if (tag) {
      out += inner ? `<${tag}>${inner}</${tag}>` : '';
    } else if (BLOCKS.has(element.tagName)) {
      out += (out && !out.endsWith('<br>') ? '<br>' : '') + inner;
    } else {
      out += inner;
    }
  });
  return out;
}

/** Лише текст (для копіювання): переноси — `\n`, без тегів. */
export function richTextToPlain(html: string): string {
  if (!/[<&]/.test(html)) {
    return html;
  }
  const doc = new DOMParser().parseFromString(`<body>${sanitizeRichText(html).replace(/<br>/g, '\n')}</body>`, 'text/html');
  return doc.body.textContent ?? '';
}

/**
 * Колір тла всієї клітинки, збережений у самому значенні HTML-тегом: усе значення загорнуте в
 * `<div style="background-color: #rrggbb">…</div>` (окремого місця для стилів клітинок сервер не
 * має; так колір видно й у будь-якому іншому показі HTML). У тексті й копіюванні тегу нема.
 * Значення зі старою позначкою `<!--bg:#rrggbb-->` на початку теж читаються (колір не губиться).
 */
const BACKGROUND = /^<div style="background-color: ?(#[0-9a-f]{6});?">([\s\S]*)<\/div>$/i;
const OLD_BACKGROUND = /^<!--bg:(#[0-9a-f]{6})-->([\s\S]*)$/i;

/** Значення → колір тла (`null` — нема) і сам текст із форматуванням. */
export function splitBackground(value: string | null | undefined): { readonly background: string | null; readonly html: string } {
  const text = value ?? '';
  const match = BACKGROUND.exec(text) ?? OLD_BACKGROUND.exec(text);
  return match ? { background: match[1].toLowerCase(), html: match[2] } : { background: null, html: text };
}

/** Колір тла й текст → значення (без кольору — лише текст). */
export function joinBackground(background: string | null, html: string): string {
  return background ? `<div style="background-color: ${background.toLowerCase()}">${html}</div>` : html;
}
