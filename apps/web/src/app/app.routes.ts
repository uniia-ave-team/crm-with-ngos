import { Route, Routes } from '@angular/router';

import { authGuard } from './core/auth/auth.guard';
import { requireAnyPermission } from './core/auth/permission.guard';
import { ADMIN_PANEL_PERMISSIONS, COLLEAGUES_PERMISSIONS, KNOWLEDGE_BASE_PERMISSIONS, PLANNER_PERMISSIONS } from './core/auth/permissions';
import { connectionGuard } from './core/connection/connection.guard';

/**
 * Те, що потрібне кожній сторінці для того, хто увійшов (усі, крім /login, /join,
 * /reset-password і /relogin):
 * два стражі й їхній запуск при кожній зміні сторінки. Нова така сторінка бере це цілком
 * (`...SIGNED_IN`), тож забути стража не вийде; сторінка з правами додає свій страж після цих.
 *
 * - `authGuard` шукає чинний акаунт серед збережених (як сторінка входу), перший знайдений стає
 *   поточним; доступного нема — переадресація на /relogin (збережені акаунти є) чи /login.
 * - `connectionGuard`: сервер активного акаунта не відповідає — акаунт позначається
 *   недоступним, переадресація на /relogin.
 * - `runGuardsAndResolvers: 'always'`: стражі спрацьовують при кожній зміні сторінки (також між
 *   вкладками налаштувань), а не лише коли маршрут активується вперше — authGuard на цьому
 *   перепитує профіль (`GET /users/me`).
 */
const SIGNED_IN = {
  canActivate: [authGuard, connectionGuard],
  runGuardsAndResolvers: 'always',
} as const satisfies Pick<Route, 'canActivate' | 'runGuardsAndResolvers'>;

export const routes: Routes = [
  {
    path: '',
    redirectTo: 'login',
    pathMatch: 'full',
  },
  {
    // Стартова сторінка (`REDIRECT_URL`): веде на першу доступну за правами (див. `start-url.ts`),
    // а якщо такої нема — лишається з порожнім станом і бічним меню (звідти — Адмін і налаштування).
    path: 'start',
    loadComponent: () => import('./start/start.page').then((m) => m.StartPage),
    ...SIGNED_IN,
  },
  {
    path: 'calendar',
    loadComponent: () => import('./calendar/calendar.page').then((m) => m.CalendarPage),
    ...SIGNED_IN,
  },
  {
    path: 'planner',
    loadComponent: () => import('./planner/planner.page').then((m) => m.PlannerPage),
    // Без права ViewProject — на REDIRECT_URL (те саме, що ховає пункт меню «Планер»).
    ...SIGNED_IN,
    canActivate: [...SIGNED_IN.canActivate, requireAnyPermission(PLANNER_PERMISSIONS)],
    data: { layoutSection: 'planner' },
  },
  {
    // Планер одного проєкту (клік по проєкту в боковому меню): та сама сторінка, `id` проєкту
    // приходить її входом `id`.
    path: 'planner/projects/:id',
    loadComponent: () => import('./planner/planner.page').then((m) => m.PlannerPage),
    ...SIGNED_IN,
    canActivate: [...SIGNED_IN.canActivate, requireAnyPermission(PLANNER_PERMISSIONS)],
    data: { layoutSection: 'planner' },
  },
  {
    path: 'knowledge-base',
    loadChildren: () => import('./knowledge-base/knowledge-base.routes').then((m) => m.KNOWLEDGE_BASE_ROUTES),
    // Без права ViewCustomTable — на REDIRECT_URL (те саме, що ховає пункт меню «База знань»).
    ...SIGNED_IN,
    canActivate: [...SIGNED_IN.canActivate, requireAnyPermission(KNOWLEDGE_BASE_PERMISSIONS)],
    data: { layoutSection: 'knowledge-base' },
  },
  {
    path: 'colleagues',
    loadComponent: () => import('./colleagues/colleagues.page').then((m) => m.ColleaguesPage),
    // Без права ViewUser — на REDIRECT_URL (те саме, що ховає пункт меню «Колеги»).
    ...SIGNED_IN,
    canActivate: [...SIGNED_IN.canActivate, requireAnyPermission(COLLEAGUES_PERMISSIONS)],
  },
  {
    path: 'admin-panel',
    loadComponent: () => import('./admin-panel/admin-panel.page').then((m) => m.AdminPanelPage),
    // Без жодного з прав адмінки — на REDIRECT_URL (те саме, що ховає пункт меню «Адмін»).
    ...SIGNED_IN,
    canActivate: [...SIGNED_IN.canActivate, requireAnyPermission(ADMIN_PANEL_PERMISSIONS)],
  },
  {
    path: 'settings',
    loadChildren: () => import('./settings/settings.routes').then((m) => m.SETTINGS_ROUTES),
    ...SIGNED_IN,
    data: { layoutSection: 'settings' },
  },
  {
    path: 'login',
    loadComponent: () => import('./login/login.page').then((m) => m.LoginPage),
    // Сторінка входу не має бічного меню взагалі (ні рейла, ні другої
    // панелі) — читається в app.component.ts через LayoutService.hideShell.
    data: { layout: 'none' },
  },
  {
    path: 'join',
    loadComponent: () => import('./join/join.page').then((m) => m.JoinPage),
    // Сторінка запрошення: без стражів (колега ще не має акаунта на цьому сервері) і
    // без бічного меню — як /login.
    data: { layout: 'none' },
  },
  {
    path: 'reset-password',
    loadComponent: () => import('./reset-password/reset-password.page').then((m) => m.ResetPasswordPage),
    // Новий пароль за посиланням: без стражів (колега може не мати акаунта в цьому браузері) і
    // без бічного меню — як /join.
    data: { layout: 'none' },
  },
  {
    path: 'relogin',
    loadComponent: () => import('./relogin/relogin.page').then((m) => m.ReloginPage),
    // Сторінка повторного входу: без стражів і без бічного меню, як /login й /join.
    data: { layout: 'none' },
  },
  {
    // Невідома адреса (стара закладка, помилка в посиланні) — туди ж, що й корінь: на /login,
    // а звідти пошук акаунта відкриває першу сторінку чи форму входу. Без цього навігація
    // падала б з помилкою, а екран лишався б порожнім. Саме 'login', а не '': переадресацію
    // кореня роутер слідом уже не виконав би й лишився б на порожньому `/`.
    path: '**',
    redirectTo: 'login',
  },
];
