import { NgModule } from '@angular/core';
import { PreloadAllModules, RouterModule, Routes } from '@angular/router';

import { authGuard } from './core/auth/auth.guard';
import { connectionGuard } from './core/connection/connection.guard';

// Усі сторінки, крім /login, проходять два стражі. authGuard: шукає чинний
// акаунт серед збережених (як сторінка входу), перший знайдений стає поточним;
// акаунтів нема чи всі застарілі — переадресація на /login. connectionGuard:
// без підключення до API активного акаунта — переадресація на /login з помилкою.
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
  },
  {
    path: 'planner',
    loadChildren: () => import('./planner/planner.module').then((m) => m.PlannerPageModule),
    canActivate: [authGuard, connectionGuard],
    data: { layoutSection: 'planner' },
  },
  {
    path: 'knowledge-base',
    loadChildren: () => import('./knowledge-base/knowledge-base.module').then((m) => m.KnowledgeBasePageModule),
    canActivate: [authGuard, connectionGuard],
    data: { layoutSection: 'knowledge-base' },
  },
  {
    path: 'colleagues',
    loadChildren: () => import('./colleagues/colleagues.module').then((m) => m.ColleaguesPageModule),
    canActivate: [authGuard, connectionGuard],
  },
  {
    path: 'settings',
    loadChildren: () => import('./settings/settings.module').then((m) => m.SettingsPageModule),
    canActivate: [authGuard, connectionGuard],
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
