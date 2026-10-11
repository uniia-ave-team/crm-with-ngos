import { CommonModule } from '@angular/common';
import { NgModule } from '@angular/core';
import { IonicModule } from '@ionic/angular/lazy';
import { TranslatePipe } from '@ngx-translate/core';

import { PreloaderComponent } from './preloader.component';

/** Прелоадер на всю сторінку (`<app-preloader>`). */
@NgModule({
  declarations: [PreloaderComponent],
  imports: [CommonModule, IonicModule, TranslatePipe],
  exports: [PreloaderComponent],
})
export class PreloaderModule {}
