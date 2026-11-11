import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { IonicModule } from '@ionic/angular/lazy';
import { TranslatePipe } from '@ngx-translate/core';

import { AccountAvatarModule } from '../shared/account-avatar/account-avatar.module';
import { ReloginFormComponent } from './relogin-form/relogin-form.component';
import { ReloginPageRoutingModule } from './relogin-routing.module';
import { ReloginPage } from './relogin.page';

@NgModule({
  imports: [CommonModule, FormsModule, IonicModule, TranslatePipe, AccountAvatarModule, ReloginPageRoutingModule],
  declarations: [ReloginPage, ReloginFormComponent],
})
export class ReloginPageModule {}
