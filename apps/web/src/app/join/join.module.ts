import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { IonicModule } from '@ionic/angular/lazy';
import { TranslatePipe } from '@ngx-translate/core';

import { PasswordFieldsModule } from '../shared/password-fields/password-fields.module';
import { JoinPageRoutingModule } from './join-routing.module';

import { JoinPage } from './join.page';

@NgModule({
  imports: [CommonModule, FormsModule, IonicModule, TranslatePipe, PasswordFieldsModule, JoinPageRoutingModule],
  declarations: [JoinPage],
})
export class JoinPageModule {}
