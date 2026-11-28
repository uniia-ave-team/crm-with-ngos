import { COLLEAGUES_PERMISSIONS, KNOWLEDGE_BASE_PERMISSIONS, PLANNER_PERMISSIONS, hasAnyPermission } from '../core/auth/permissions';

/**
 * Сторінки, на які веде стартова сторінка, у порядку переваги, і права, з якими кожна доступна —
 * ті самі, що показують її пункт у бічному меню й пускають на неї (страж маршруту).
 */
const START_PAGES: readonly { readonly url: string; readonly requiresAny: readonly string[] }[] = [
  { url: '/planner', requiresAny: PLANNER_PERMISSIONS },
  { url: '/knowledge-base', requiresAny: KNOWLEDGE_BASE_PERMISSIONS },
  { url: '/colleagues', requiresAny: COLLEAGUES_PERMISSIONS },
];

/** Перша доступна за правами `granted` сторінка (Планер, База знань, Колеги); жодної — `null`. */
export function startUrl(granted: readonly string[]): string | null {
  return START_PAGES.find((page) => hasAnyPermission(granted, page.requiresAny))?.url ?? null;
}
