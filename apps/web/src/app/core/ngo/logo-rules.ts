import { LOGO_IMAGE_TYPES } from '../images/image-types';

/** Які зображення можна обрати логотипом організації (`.png .webp .jpeg .jpg .gif .svg`). */
export const NGO_LOGO_TYPES: readonly string[] = LOGO_IMAGE_TYPES;

/** Найбільший розмір файлу логотипа, байт (2 МБ). */
export const NGO_LOGO_MAX_BYTES = 2 * 1024 * 1024;

/** Найдовша назва організації, символів. */
export const NGO_NAME_MAX_LENGTH = 200;
