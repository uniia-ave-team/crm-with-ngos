import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { IonicModule } from '@ionic/angular/lazy';
import { TranslatePipe } from '@ngx-translate/core';

import { AdminPanelPageRoutingModule } from './admin-panel-routing.module';

import { AdminPanelPage } from './admin-panel.page';

@NgModule({
  imports: [
    CommonModule,
    FormsModule,
    IonicModule,
    TranslatePipe,
    AdminPanelPageRoutingModule
  ],
  declarations: [AdminPanelPage]
})
export class AdminPanelPageModule {}
