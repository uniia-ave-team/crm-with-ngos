import { CommonModule } from '@angular/common';
import { NgModule } from '@angular/core';
import { IonicModule } from '@ionic/angular/lazy';
import { TranslatePipe } from '@ngx-translate/core';

import { SidePanelComponent } from './side-panel.component';

/** Бокове вікно (`<app-side-panel>`); підключається в модулі, де потрібне. */
@NgModule({
  declarations: [SidePanelComponent],
  imports: [CommonModule, IonicModule, TranslatePipe],
  exports: [SidePanelComponent],
})
export class SidePanelModule {}
