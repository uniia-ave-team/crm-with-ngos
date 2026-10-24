/**
 * Які зображення можна обрати: аватар користувача й зображення сторінки входу
 * (`.png .webp .jpeg .jpg .gif`; `.jpg` і `.jpeg` — один тип `image/jpeg`).
 * Єдине місце, де цей список задано: на нього спираються вибір файлу
 * (`accept`), перевірка вибраного файлу й тексти в мовному файлі.
 */
export const IMAGE_TYPES: readonly string[] = ['image/png', 'image/webp', 'image/jpeg', 'image/gif'];

/** Логотип організації: ті самі типи, що й для зображень, і ще `.svg`. */
export const LOGO_IMAGE_TYPES: readonly string[] = [...IMAGE_TYPES, 'image/svg+xml'];
