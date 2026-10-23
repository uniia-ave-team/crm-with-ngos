import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { IonicModule } from '@ionic/angular/lazy';
import { TranslatePipe } from '@ngx-translate/core';

import { AvatarModule } from '../shared/avatar/avatar.module';
import { DataTableModule } from '../shared/data-table/data-table.module';

import { ColleaguesPageRoutingModule } from './colleagues-routing.module';

import { ColleaguesPage } from './colleagues.page';

@NgModule({
  imports: [
    CommonModule,
    FormsModule,
    IonicModule,
    TranslatePipe,
    AvatarModule,
    DataTableModule,
    ColleaguesPageRoutingModule
  ],
  declarations: [ColleaguesPage]
})
export class ColleaguesPageModule {}
