import { Routes } from '@angular/router';

import { authGuard } from './core/auth/auth.guard';
import { requireAnyPermission } from './core/auth/permission.guard';
import { ADMIN_PANEL_PERMISSIONS, COLLEAGUES_PERMISSIONS } from './core/auth/permissions';
import { connectionGuard } from './core/connection/connection.guard';

// Усі сторінки, крім /login, /join і /relogin, проходять два стражі. authGuard: шукає чинний
// акаунт серед збережених (як сторінка входу), перший знайдений стає поточним;
// акаунтів нема чи всі застарілі — переадресація на /login. connectionGuard:
// без підключення до API активного акаунта — переадресація на /login з помилкою.
// `runGuardsAndResolvers: 'always'`: стражі спрацьовують при кожній зміні сторінки
// (також між вкладками налаштувань), а не лише коли маршрут активується вперше —
// authGuard на цьому перепитує профіль (`GET /users/me`).
export const routes: Routes = [
  {
    path: '',
    redirectTo: 'login',
    pathMatch: 'full',
  },
  {
    path: 'calendar',
    loadComponent: () => import('./calendar/calendar.page').then((m) => m.CalendarPage),
    canActivate: [authGuard, connectionGuard],
    runGuardsAndResolvers: 'always',
  },
  {
    path: 'planner',
    loadComponent: () => import('./planner/planner.page').then((m) => m.PlannerPage),
    canActivate: [authGuard, connectionGuard],
    runGuardsAndResolvers: 'always',
    data: { layoutSection: 'planner' },
  },
  {
    path: 'knowledge-base',
    loadComponent: () => import('./knowledge-base/knowledge-base.page').then((m) => m.KnowledgeBasePage),
    canActivate: [authGuard, connectionGuard],
    runGuardsAndResolvers: 'always',
    data: { layoutSection: 'knowledge-base' },
  },
  {
    path: 'colleagues',
    loadComponent: () => import('./colleagues/colleagues.page').then((m) => m.ColleaguesPage),
    // Без права ViewUser — на REDIRECT_URL (те саме, що ховає пункт меню «Колеги»).
    canActivate: [authGuard, connectionGuard, requireAnyPermission(COLLEAGUES_PERMISSIONS)],
    runGuardsAndResolvers: 'always',
  },
  {
    path: 'admin-panel',
    loadComponent: () => import('./admin-panel/admin-panel.page').then((m) => m.AdminPanelPage),
    // Без жодного з прав адмінки — на REDIRECT_URL (те саме, що ховає пункт меню «Адмін»).
    canActivate: [authGuard, connectionGuard, requireAnyPermission(ADMIN_PANEL_PERMISSIONS)],
    runGuardsAndResolvers: 'always',
  },
  {
    path: 'settings',
    loadChildren: () => import('./settings/settings.routes').then((m) => m.SETTINGS_ROUTES),
    canActivate: [authGuard, connectionGuard],
    runGuardsAndResolvers: 'always',
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
    path: 'relogin',
    loadComponent: () => import('./relogin/relogin.page').then((m) => m.ReloginPage),
    // Сторінка повторного входу: без стражів і без бічного меню, як /login й /join.
    data: { layout: 'none' },
  },
];
