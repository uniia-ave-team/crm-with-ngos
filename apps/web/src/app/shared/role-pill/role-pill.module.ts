import { CommonModule } from '@angular/common';
import { NgModule } from '@angular/core';

import { IonicModule } from '@ionic/angular/lazy';

import { RolePillComponent } from './role-pill.component';

/** Плашка ролі (`<app-role-pill>`); підключається в модулі, де потрібна. */
@NgModule({
  declarations: [RolePillComponent],
  imports: [CommonModule, IonicModule],
  exports: [RolePillComponent],
})
export class RolePillModule {}
