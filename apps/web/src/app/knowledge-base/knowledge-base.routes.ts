import { Routes } from '@angular/router';

import { requireAnyPermission } from '../core/auth/permission.guard';
import { TABLE_TEMPLATES_PERMISSIONS } from '../core/auth/permissions';
import { KbGuideComponent } from './kb-guide/kb-guide.component';
import { KbPageComponent } from './kb-page/kb-page.component';
import { KbTableComponent } from './kb-table/kb-table.component';
import { KbTemplatesComponent } from './kb-templates/kb-templates.component';
import { KnowledgeBasePage } from './knowledge-base.page';

/**
 * Види основного блоку «Бази знань» — усередині сторінки (її `router-outlet`), кожен за своєю
 * адресою:
 * - `/knowledge-base` — «Посібник» (зміст);
 * - `/knowledge-base/tables/:id` — таблиця (`id` приходить входом `id` виду);
 * - `/knowledge-base/pages/:id` — сторінка (документ; вид запланований);
 * - `/knowledge-base/templates` — менеджер шаблонів таблиць (лише з `ViewTableTemplate`).
 */
export const KNOWLEDGE_BASE_ROUTES: Routes = [
  {
    path: '',
    component: KnowledgeBasePage,
    children: [
      { path: '', pathMatch: 'full', component: KbGuideComponent },
      { path: 'tables/:id', component: KbTableComponent },
      { path: 'pages/:id', component: KbPageComponent },
      { path: 'templates', component: KbTemplatesComponent, canActivate: [requireAnyPermission(TABLE_TEMPLATES_PERMISSIONS)] },
      { path: '**', redirectTo: '' },
    ],
  },
];
