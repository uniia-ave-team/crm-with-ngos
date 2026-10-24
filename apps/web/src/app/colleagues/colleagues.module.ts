import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { IonicModule } from '@ionic/angular/lazy';
import { TranslatePipe } from '@ngx-translate/core';

import { AvatarModule } from '../shared/avatar/avatar.module';
import { DataTableModule } from '../shared/data-table/data-table.module';
import { PopoverModule } from '../shared/popover/popover.module';
import { SidePanelModule } from '../shared/side-panel/side-panel.module';

import { ColleaguesPageRoutingModule } from './colleagues-routing.module';

import { ColleagueActionsComponent } from './colleague-actions/colleague-actions.component';
import { ColleaguesPage } from './colleagues.page';

@NgModule({
  imports: [
    CommonModule,
    FormsModule,
    IonicModule,
    TranslatePipe,
    AvatarModule,
    DataTableModule,
    PopoverModule,
    SidePanelModule,
    ColleaguesPageRoutingModule
  ],
  declarations: [ColleaguesPage, ColleagueActionsComponent],
  // Список дій експортується, щоб його можна було перевірити окремо від сторінки.
  exports: [ColleagueActionsComponent],
})
export class ColleaguesPageModule {}
