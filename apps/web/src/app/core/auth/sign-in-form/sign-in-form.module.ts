import { CommonModule } from '@angular/common';
import { NgModule } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { IonicModule } from '@ionic/angular/lazy';
import { TranslatePipe } from '@ngx-translate/core';

import { SignInFormComponent } from './sign-in-form.component';

/** Форма входу (`<app-sign-in-form>`): сторінки `login` і `relogin`, вікно входу в акаунт. */
@NgModule({
  declarations: [SignInFormComponent],
  imports: [CommonModule, FormsModule, IonicModule, TranslatePipe],
  exports: [SignInFormComponent],
})
export class SignInFormModule {}
