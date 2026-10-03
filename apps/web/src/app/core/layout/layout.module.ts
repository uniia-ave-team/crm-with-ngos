import { CommonModule } from '@angular/common';
import { NgModule } from '@angular/core';
import { RouterModule } from '@angular/router';
import { IonicModule } from '@ionic/angular/lazy';
import { TranslatePipe } from '@ngx-translate/core';

import { PrimaryNavComponent } from './primary-nav/primary-nav.component';
import { SecondaryPanelComponent } from './secondary-panel/secondary-panel.component';

@NgModule({
  declarations: [PrimaryNavComponent, SecondaryPanelComponent],
  // TranslatePipe стандалонний (ngx-translate 18 більше не має NgModule),
  // тому додається напряму в imports поряд з CommonModule/IonicModule.
  imports: [CommonModule, RouterModule, IonicModule, TranslatePipe],
  exports: [PrimaryNavComponent, SecondaryPanelComponent],
})
export class LayoutModule {}
