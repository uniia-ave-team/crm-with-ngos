import { NgModule } from '@angular/core';
import { PreloadAllModules, RouterModule, Routes } from '@angular/router';

const routes: Routes = [
  {
    path: '',
    redirectTo: 'login',
    pathMatch: 'full',
  },
  {
    path: 'calendar',
    loadChildren: () => import('./calendar/calendar.module').then((m) => m.CalendarPageModule),
  },
  {
    path: 'planner',
    loadChildren: () => import('./planner/planner.module').then((m) => m.PlannerPageModule),
    data: { layoutSection: 'planner' },
  },
  {
    path: 'knowledge-base',
    loadChildren: () => import('./knowledge-base/knowledge-base.module').then((m) => m.KnowledgeBasePageModule),
    data: { layoutSection: 'knowledge-base' },
  },
  {
    path: 'colleagues',
    loadChildren: () => import('./colleagues/colleagues.module').then((m) => m.ColleaguesPageModule),
  },
  {
    path: 'settings',
    loadChildren: () => import('./settings/settings.module').then((m) => m.SettingsPageModule),
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
