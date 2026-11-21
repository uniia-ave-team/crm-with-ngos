/**
 * Формати зображень, які можна завантажити: аватар користувача, логотип організації й
 * зображення сторінки входу. Список один на всі три випадки, як і на сервері
 * (`FileStorageConstants.AllowedExtensions`: `.jpg .jpeg .png .webp .gif .bmp .svg`).
 *
 * Єдине місце, де цей список задано. З нього беруться:
 * - `IMAGE_TYPES` — MIME-типи для вибору файлу (`accept`) і перевірки вибраного файлу;
 * - `IMAGE_TYPE_NAMES` — назви форматів для текстів на сторінках (підказки під полем і
 *   повідомлення про непідтримуваний файл).
 */
const IMAGE_FORMATS: readonly { readonly type: string; readonly names: readonly string[] }[] = [
  { type: 'image/png', names: ['PNG'] },
  { type: 'image/webp', names: ['WebP'] },
  // `.jpg` і `.jpeg` — один тип, але користувач бачить обидва розширення.
  { type: 'image/jpeg', names: ['JPEG', 'JPG'] },
  { type: 'image/gif', names: ['GIF'] },
  { type: 'image/bmp', names: ['BMP'] },
  { type: 'image/svg+xml', names: ['SVG'] },
];

/** MIME-типи дозволених зображень. */
export const IMAGE_TYPES: readonly string[] = IMAGE_FORMATS.map((format) => format.type);

/**
 * Назви дозволених форматів у порядку показу. Це масив, а не готовий рядок: як зʼєднати
 * елементи (роздільник, сполучник, напрям письма), вирішує мова інтерфейсу (канал `listText`).
 */
export const IMAGE_TYPE_NAMES: readonly string[] = IMAGE_FORMATS.flatMap((format) => format.names);

/**
 * Найбільший розмір зображення, яке приймає сервер, у мегабайтах — для текстів підказок і
 * помилок (`{{size}}`). Ліміт один для всіх зображень: аватара, логотипа ГО й зображень сторінки
 * входу (`FileLimitConstants.MaxImageUploadSize` на сервері).
 */
export const IMAGE_MAX_MEGABYTES = 5;

/**
 * Запас на обгортку запиту: сервер рахує ліміт на весь запит (`multipart/form-data` з полями й
 * межами), а не лише на файл, тож файл рівно на `IMAGE_MAX_MEGABYTES` уже не пройшов би.
 */
const UPLOAD_ENVELOPE_RESERVE_BYTES = 64 * 1024;

/** Найбільший розмір самого файлу зображення, байт, який іще вміщається в ліміт сервера. */
export const IMAGE_MAX_BYTES = IMAGE_MAX_MEGABYTES * 1024 * 1024 - UPLOAD_ENVELOPE_RESERVE_BYTES;
