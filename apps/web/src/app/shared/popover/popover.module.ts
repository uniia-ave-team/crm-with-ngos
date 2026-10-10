import { CommonModule } from '@angular/common';
import { NgModule } from '@angular/core';
import { IonicModule } from '@ionic/angular/lazy';

import { PopoverComponent } from './popover.component';

/** Спливаюче вікно (`<app-popover>`); підключається в модулі, де потрібне. */
@NgModule({
  declarations: [PopoverComponent],
  imports: [CommonModule, IonicModule],
  exports: [PopoverComponent],
})
export class PopoverModule {}
