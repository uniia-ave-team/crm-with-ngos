import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';

import { IonicModule } from '@ionic/angular/lazy';

import { ReloginPageRoutingModule } from './relogin-routing.module';
import { ReloginPage } from './relogin.page';

@NgModule({
  imports: [CommonModule, IonicModule, ReloginPageRoutingModule],
  declarations: [ReloginPage],
})
export class ReloginPageModule {}
