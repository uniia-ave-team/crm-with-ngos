import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { IonicModule } from '@ionic/angular/lazy';
import { TranslatePipe } from '@ngx-translate/core';

import { AvatarModule } from '../shared/avatar/avatar.module';
import { DataTableModule } from '../shared/data-table/data-table.module';
import { PopoverModule } from '../shared/popover/popover.module';
import { RolePillModule } from '../shared/role-pill/role-pill.module';
import { SidePanelModule } from '../shared/side-panel/side-panel.module';

import { ColleaguesPageRoutingModule } from './colleagues-routing.module';

import { ColleagueActionsComponent } from './colleague-actions/colleague-actions.component';
import { ColleagueFormComponent } from './colleague-form/colleague-form.component';
import { ColleaguesPage } from './colleagues.page';
import { RoleFormComponent } from './role-form/role-form.component';
import { RolesPanelComponent } from './roles-panel/roles-panel.component';

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
    RolePillModule,
    ColleaguesPageRoutingModule
  ],
  declarations: [ColleaguesPage, ColleagueActionsComponent, ColleagueFormComponent, RolesPanelComponent, RoleFormComponent],
  // Зміст бічного вікна експортується, щоб його можна було перевірити окремо від сторінки.
  exports: [ColleagueActionsComponent, ColleagueFormComponent, RolesPanelComponent, RoleFormComponent],
})
export class ColleaguesPageModule {}
