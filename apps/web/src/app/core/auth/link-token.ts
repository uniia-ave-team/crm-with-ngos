/**
 * Токен у посиланні запрошення (`/join`) і скидання пароля (`/reset-password`) — у фрагменті адреси
 * (`#token=…`), а не в query (`?token=…`): фрагмент браузер не надсилає серверу, тож токена нема в
 * журналах проксі (Cloudflare, Traefik) і веб-сервера, і він не потрапляє в `Referer`.
 */

/** Посилання `<origin><path>#token=<токен>` (токен закодовано для адреси). */
export function linkWithToken(origin: string, path: string, token: string): string {
  return `${origin}${path}#token=${encodeURIComponent(token)}`;
}

/** Токен із фрагмента адреси (`token=…`); нема — `undefined`. */
export function tokenFromFragment(fragment: string | null | undefined): string | undefined {
  return (fragment && new URLSearchParams(fragment).get('token')) || undefined;
}
