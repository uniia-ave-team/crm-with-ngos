import { AppLanguage } from './language.service';

/*
 * Лого, що залежать від мови інтерфейсу. Обидва працюють однаково: у таблиці — лише мови з
 * власним варіантом файлу, а решта мов, зокрема щойно додані, отримують варіант за
 * замовчуванням.
 */

/**
 * Мови з власним варіантом мобільного лого «Явір» (`assets/logo_mobile_<варіант>.svg`):
 * вордмарк із текстом кирилицею. Решта — латинський.
 */
const MOBILE_LOGO_VARIANT: Readonly<Partial<Record<AppLanguage, string>>> = {
  uk: 'ukrainian',
  be: 'be',
};

const DEFAULT_MOBILE_LOGO_VARIANT = 'latin';

/**
 * Базове імʼя файлу мобільного лого «Явір» для мови (без `.svg`/`_dark.svg`): вордмарк з
 * текстом є в окремих мовних варіантах.
 */
export function mobileLogoBase(language: AppLanguage): string {
  return `logo_mobile_${MOBILE_LOGO_VARIANT[language] ?? DEFAULT_MOBILE_LOGO_VARIANT}`;
}

/**
 * Мови, для яких показується український варіант лого Унії (`assets/logos/Uniia.svg`,
 * для темної теми — `assets/logos_white/`). Решта — англійський (`Uniia_en.svg`).
 */
const UNIIA_LOGO_VARIANT: Readonly<Partial<Record<AppLanguage, string>>> = {
  uk: 'Uniia',
  crh: 'Uniia',
  be: 'Uniia',
};

const DEFAULT_UNIIA_LOGO_VARIANT = 'Uniia_en';

/** Імʼя файлу лого Унії для мови (без `.svg`); однакове для світлої й темної теми. */
export function uniiaLogoBase(language: AppLanguage): string {
  return UNIIA_LOGO_VARIANT[language] ?? DEFAULT_UNIIA_LOGO_VARIANT;
}
