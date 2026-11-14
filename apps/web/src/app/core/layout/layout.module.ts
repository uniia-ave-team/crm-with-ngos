import { CommonModule } from '@angular/common';
import { NgModule } from '@angular/core';
import { RouterModule } from '@angular/router';
import { IonicModule } from '@ionic/angular/lazy';
import { TranslatePipe } from '@ngx-translate/core';

import { AccountAvatarModule } from '../accounts/account-avatar/account-avatar.module';
import { SignInFormModule } from '../auth/sign-in-form/sign-in-form.module';
import { AvatarModule } from '../../shared/avatar/avatar.module';
import { ModalModule } from '../../shared/modal/modal.module';
import { PopoverModule } from '../../shared/popover/popover.module';
import { AccountMenuComponent } from './account-menu/account-menu.component';
import { AddAccountComponent } from './add-account/add-account.component';
import { PrimaryNavComponent } from './primary-nav/primary-nav.component';
import { SecondaryPanelComponent } from './secondary-panel/secondary-panel.component';

@NgModule({
  declarations: [PrimaryNavComponent, SecondaryPanelComponent, AccountMenuComponent, AddAccountComponent],
  // TranslatePipe стандалонний (ngx-translate 18 більше не має NgModule),
  // тому додається напряму в imports поряд з CommonModule/IonicModule.
  imports: [
    CommonModule,
    RouterModule,
    IonicModule,
    TranslatePipe,
    AccountAvatarModule,
    SignInFormModule,
    AvatarModule,
    ModalModule,
    PopoverModule,
  ],
  exports: [PrimaryNavComponent, SecondaryPanelComponent],
})
export class LayoutModule {}
