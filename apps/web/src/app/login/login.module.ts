import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { IonicModule } from '@ionic/angular/lazy';
import { TranslatePipe } from '@ngx-translate/core';

import { SignInFormModule } from '../core/auth/sign-in-form/sign-in-form.module';
import { PasswordFieldsModule } from '../shared/password-fields/password-fields.module';
import { LoginPageRoutingModule } from './login-routing.module';

import { LoginPage } from './login.page';

@NgModule({
  imports: [
    CommonModule,
    FormsModule,
    IonicModule,
    TranslatePipe,
    PasswordFieldsModule,
    SignInFormModule,
    LoginPageRoutingModule
  ],
  declarations: [LoginPage]
})
export class LoginPageModule {}
