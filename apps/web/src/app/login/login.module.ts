import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { IonicModule } from '@ionic/angular/lazy';
import { TranslatePipe } from '@ngx-translate/core';

import { PasswordFieldsModule } from '../shared/password-fields/password-fields.module';
import { LoginDemoComponent } from './login-demo/login-demo.component';
import { LoginPageRoutingModule } from './login-routing.module';

import { LoginPage } from './login.page';

@NgModule({
  imports: [
    CommonModule,
    FormsModule,
    IonicModule,
    TranslatePipe,
    PasswordFieldsModule,
    LoginPageRoutingModule
  ],
  declarations: [LoginPage, LoginDemoComponent]
})
export class LoginPageModule {}
