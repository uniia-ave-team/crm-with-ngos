import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';

import { KnowledgeBasePage } from './knowledge-base.page';

const routes: Routes = [
  {
    path: '',
    component: KnowledgeBasePage
  }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class KnowledgeBasePageRoutingModule {}
