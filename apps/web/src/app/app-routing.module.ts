import { NgModule } from '@angular/core';
import { PreloadAllModules, RouterModule, Routes } from '@angular/router';

import { connectionGuard } from './core/connection/connection.guard';

// Усі сторінки, крім /login, пускає лише connectionGuard: без підключення до
// API (за наданою адресою з localStorage або за доменом сторінки) —
// переадресація на /login з помилкою.
const routes: Routes = [
  {
    path: '',
    redirectTo: 'login',
    pathMatch: 'full',
  },
  {
    path: 'calendar',
    loadChildren: () => import('./calendar/calendar.module').then((m) => m.CalendarPageModule),
    canActivate: [connectionGuard],
  },
  {
    path: 'planner',
    loadChildren: () => import('./planner/planner.module').then((m) => m.PlannerPageModule),
    canActivate: [connectionGuard],
    data: { layoutSection: 'planner' },
  },
  {
    path: 'knowledge-base',
    loadChildren: () => import('./knowledge-base/knowledge-base.module').then((m) => m.KnowledgeBasePageModule),
    canActivate: [connectionGuard],
    data: { layoutSection: 'knowledge-base' },
  },
  {
    path: 'colleagues',
    loadChildren: () => import('./colleagues/colleagues.module').then((m) => m.ColleaguesPageModule),
    canActivate: [connectionGuard],
  },
  {
    path: 'settings',
    loadChildren: () => import('./settings/settings.module').then((m) => m.SettingsPageModule),
    canActivate: [connectionGuard],
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
