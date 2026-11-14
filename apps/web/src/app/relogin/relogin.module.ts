import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';

import { IonicModule } from '@ionic/angular/lazy';
import { TranslatePipe } from '@ngx-translate/core';

import { AccountAvatarModule } from '../core/accounts/account-avatar/account-avatar.module';
import { SignInFormModule } from '../core/auth/sign-in-form/sign-in-form.module';
import { ReloginFormComponent } from './relogin-form/relogin-form.component';
import { ReloginPageRoutingModule } from './relogin-routing.module';
import { ReloginPage } from './relogin.page';

@NgModule({
  imports: [CommonModule, IonicModule, TranslatePipe, AccountAvatarModule, SignInFormModule, ReloginPageRoutingModule],
  declarations: [ReloginPage, ReloginFormComponent],
})
export class ReloginPageModule {}
