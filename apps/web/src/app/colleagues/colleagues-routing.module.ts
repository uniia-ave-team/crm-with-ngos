import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';

import { ColleaguesPage } from './colleagues.page';

const routes: Routes = [
  {
    path: '',
    component: ColleaguesPage
  }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class ColleaguesPageRoutingModule {}
