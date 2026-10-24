import { CommonModule } from '@angular/common';
import { NgModule } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { IonicModule } from '@ionic/angular/lazy';
import { TranslatePipe } from '@ngx-translate/core';

import { PasswordFieldsComponent } from './password-fields.component';

/** Поля нового пароля з умовами й повтором (`<app-password-fields>`); підключається в модулі, де потрібні. */
@NgModule({
  declarations: [PasswordFieldsComponent],
  imports: [CommonModule, FormsModule, IonicModule, TranslatePipe],
  exports: [PasswordFieldsComponent],
})
export class PasswordFieldsModule {}
