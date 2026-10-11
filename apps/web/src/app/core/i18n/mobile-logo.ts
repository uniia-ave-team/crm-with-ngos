import { AppLanguage } from './language.service';

/** Яка пара mobile-логотипу (assets/logo_mobile_*.svg) відповідає мові. */
const MOBILE_LOGO_VARIANT: Record<AppLanguage, string> = {
  uk: 'ukrainian',
  en: 'latin',
  crh: 'latin',
  // Реальних білоруських файлів лого ще нема — тимчасово дублюють
  // українські (див. apps/web/src/assets/logo_mobile_be*.svg).
  be: 'be',
};

/**
 * Базове імʼя файлу mobile-логотипу для мови (без `.svg`/`_dark.svg`):
 * вордмарк з текстом є в окремих мовних варіантах.
 */
export function mobileLogoBase(language: AppLanguage): string {
  return `logo_mobile_${MOBILE_LOGO_VARIANT[language]}`;
}
