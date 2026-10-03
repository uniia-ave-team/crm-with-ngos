import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { IonicModule } from '@ionic/angular/lazy';
import { TranslatePipe } from '@ngx-translate/core';

import { PlannerPageRoutingModule } from './planner-routing.module';

import { PlannerPage } from './planner.page';

@NgModule({
  imports: [
    CommonModule,
    FormsModule,
    IonicModule,
    TranslatePipe,
    PlannerPageRoutingModule
  ],
  declarations: [PlannerPage]
})
export class PlannerPageModule {}
