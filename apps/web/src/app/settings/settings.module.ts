import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { IonicModule } from '@ionic/angular/lazy';
import { TranslatePipe } from '@ngx-translate/core';

import { AvatarModule } from '../shared/avatar/avatar.module';
import { PasswordFieldsModule } from '../shared/password-fields/password-fields.module';
import { SettingsPageRoutingModule } from './settings-routing.module';

import { NotificationsPage } from './notifications/notifications.page';
import { PasswordPage } from './password/password.page';
import { PersonalDataPage } from './personal-data/personal-data.page';
import { SettingsPage } from './settings.page';

@NgModule({
  imports: [
    CommonModule,
    FormsModule,
    IonicModule,
    TranslatePipe,
    AvatarModule,
    PasswordFieldsModule,
    SettingsPageRoutingModule
  ],
  declarations: [SettingsPage, PersonalDataPage, PasswordPage, NotificationsPage]
})
export class SettingsPageModule {}
