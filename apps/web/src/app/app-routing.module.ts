import { NgModule } from '@angular/core';
import { PreloadAllModules, RouterModule, Routes } from '@angular/router';

import { authGuard } from './core/auth/auth.guard';
import { requireAnyPermission } from './core/auth/permission.guard';
import { ADMIN_PANEL_PERMISSIONS, COLLEAGUES_PERMISSIONS } from './core/auth/permissions';
import { connectionGuard } from './core/connection/connection.guard';

// Усі сторінки, крім /login, проходять два стражі. authGuard: шукає чинний
// акаунт серед збережених (як сторінка входу), перший знайдений стає поточним;
// акаунтів нема чи всі застарілі — переадресація на /login. connectionGuard:
// без підключення до API активного акаунта — переадресація на /login з помилкою.
// `runGuardsAndResolvers: 'always'`: стражі спрацьовують при кожній зміні сторінки
// (також між вкладками налаштувань), а не лише коли маршрут активується вперше —
// authGuard на цьому перепитує профіль (`GET /users/me`).
const routes: Routes = [
  {
    path: '',
    redirectTo: 'login',
    pathMatch: 'full',
  },
  {
    path: 'calendar',
    loadChildren: () => import('./calendar/calendar.module').then((m) => m.CalendarPageModule),
    canActivate: [authGuard, connectionGuard],
    runGuardsAndResolvers: 'always',
  },
  {
    path: 'planner',
    loadChildren: () => import('./planner/planner.module').then((m) => m.PlannerPageModule),
    canActivate: [authGuard, connectionGuard],
    runGuardsAndResolvers: 'always',
    data: { layoutSection: 'planner' },
  },
  {
    path: 'knowledge-base',
    loadChildren: () => import('./knowledge-base/knowledge-base.module').then((m) => m.KnowledgeBasePageModule),
    canActivate: [authGuard, connectionGuard],
    runGuardsAndResolvers: 'always',
    data: { layoutSection: 'knowledge-base' },
  },
  {
    path: 'colleagues',
    loadChildren: () => import('./colleagues/colleagues.module').then((m) => m.ColleaguesPageModule),
    // Без права ViewUser — на REDIRECT_URL (те саме, що ховає пункт меню «Колеги»).
    canActivate: [authGuard, connectionGuard, requireAnyPermission(COLLEAGUES_PERMISSIONS)],
    runGuardsAndResolvers: 'always',
  },
  {
    path: 'admin-panel',
    loadChildren: () => import('./admin-panel/admin-panel.module').then((m) => m.AdminPanelPageModule),
    // Без жодного з прав адмінки — на REDIRECT_URL (те саме, що ховає пункт меню «Адмін»).
    canActivate: [authGuard, connectionGuard, requireAnyPermission(ADMIN_PANEL_PERMISSIONS)],
    runGuardsAndResolvers: 'always',
  },
  {
    path: 'settings',
    loadChildren: () => import('./settings/settings.module').then((m) => m.SettingsPageModule),
    canActivate: [authGuard, connectionGuard],
    runGuardsAndResolvers: 'always',
    data: { layoutSection: 'settings' },
  },
  {
    path: 'login',
    loadChildren: () => import('./login/login.module').then((m) => m.LoginPageModule),
    // Сторінка входу не має бічного меню взагалі (ні рейла, ні другої
    // панелі) — читається в app.component.ts через LayoutService.hideShell.
    data: { layout: 'none' },
  },
];

@NgModule({
  imports: [
    RouterModule.forRoot(routes, { preloadingStrategy: PreloadAllModules, bindToComponentInputs: true }),
  ],
  exports: [RouterModule],
})
export class AppRoutingModule {}
