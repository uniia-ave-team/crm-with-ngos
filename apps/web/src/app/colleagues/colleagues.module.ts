import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { IonicModule } from '@ionic/angular/lazy';
import { TranslatePipe } from '@ngx-translate/core';

import { ColleaguesPageRoutingModule } from './colleagues-routing.module';

import { ColleaguesPage } from './colleagues.page';

@NgModule({
  imports: [
    CommonModule,
    FormsModule,
    IonicModule,
    TranslatePipe,
    ColleaguesPageRoutingModule
  ],
  declarations: [ColleaguesPage]
})
export class ColleaguesPageModule {}
