import { AccountThumbnail, StoredAccount } from '../../core/accounts/accounts.service';

/** Акаунт у списку: знімок із запису акаунта (імʼя, аватар, назва й логотип ГО). */
export interface AccountTile {
  readonly id: string | number;
  readonly firstName: string | null | undefined;
  readonly lastName: string | null | undefined;
  readonly patronymic?: string | null;
  readonly avatarUrl: string | null | undefined;
  /** Назва організації акаунта (впливає на колір аватара). */
  readonly ngoName?: string | null;
  /** Логотип ГО (будь-яке зображення з прозорістю); без нього значка на плитці нема. */
  readonly logoUrl?: string | null;
  /** Поточний (активний) акаунт: плитка отримує клас `active` і білу рамку. */
  readonly active?: boolean;
  /** Сесію акаунта завершено (токени відхилено): сірий аватар і жовтий значок помилки ліворуч. */
  readonly outdated?: boolean;
  /**
   * Сервер акаунта не відповів. Незалежний від `outdated`: сам по собі дає жовтий значок
   * синхронізації, а разом із `outdated` — червоний значок помилки.
   */
  readonly unavailable?: boolean;
}

/** Підпис акаунта: рядок «ПІБ — Назва ГО» і, якщо з акаунтом негаразд, його стан (кожен може бути порожнім). */
export interface AccountCaption {
  readonly heading: string;
  readonly status: string;
}

/**
 * Підпис акаунта: `heading` — «ПІБ — Назва ГО» (що з цього є), `status` — «сервер не відповів»
 * (`unavailableText`, має перевагу) або «сеанс закінчився» (`outdatedText`), якщо з акаунтом
 * негаразд, інакше порожньо.
 */
export function accountCaption(account: AccountTile, unavailableText: string, outdatedText: string): AccountCaption {
  const fullName = [account.lastName, account.firstName, account.patronymic]
    .map((part) => part?.trim())
    .filter(Boolean)
    .join(' ');
  return {
    heading: [fullName, account.ngoName?.trim()].filter(Boolean).join(' — '),
    status: account.unavailable ? unavailableText : account.outdated ? outdatedText : '',
  };
}

/** Підпис як один текст із двох рядків (для `title`); нічого показати — `null`. */
export function accountTooltip(account: AccountTile, unavailableText: string, outdatedText: string): string | null {
  const { heading, status } = accountCaption(account, unavailableText, outdatedText);
  return [heading, status].filter(Boolean).join('\n') || null;
}

/**
 * Акаунти для плиток: усі збережені (за порядком додавання), у тому числі застарілі. Дані
 * беруться зі знімка в записі акаунта, тож жодних запитів немає.
 */
export function toAccountTiles(accounts: readonly StoredAccount[], activeId: string | null): AccountTile[] {
  return accounts.map((account) => ({
    id: account.id,
    firstName: account.firstName,
    lastName: account.lastName,
    patronymic: account.patronymic,
    // Плитка малюється з мініатюр у знімку акаунта (`data:`-посилання, без запитів):
    // закрите зображення можна завантажити лише з токеном активного акаунта, а плитки
    // показують і неактивні. Мініатюра, зроблена зі старого аватара чи логотипа, не годиться.
    avatarUrl: thumbnailData(account.avatarThumb, account.avatarUrl),
    ngoName: account.ngoName,
    logoUrl: thumbnailData(account.ngoLogoThumb, account.ngoLogoUrl),
    active: account.id === activeId,
    outdated: account.outdated,
    unavailable: account.unavailable,
  }));
}

/** `data:`-посилання мініатюри, якщо вона зроблена з поточної адреси зображення; інакше `null` (на плитці будуть літери чи не буде значка). */
function thumbnailData(thumbnail: AccountThumbnail | null, url: string | null): string | null {
  return thumbnail && url && thumbnail.url === url ? thumbnail.data : null;
}
