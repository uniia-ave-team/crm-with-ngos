import { CommonModule } from '@angular/common';
import { NgModule } from '@angular/core';
import { IonicModule } from '@ionic/angular/lazy';

import { ModalComponent } from './modal.component';

/** Модальне вікно по центру екрана (`<app-modal>`); підключається в модулі, де потрібне. */
@NgModule({
  declarations: [ModalComponent],
  imports: [CommonModule, IonicModule],
  exports: [ModalComponent],
})
export class ModalModule {}
