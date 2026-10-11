import { CommonModule } from '@angular/common';
import { NgModule } from '@angular/core';
import { RouterModule } from '@angular/router';
import { IonicModule } from '@ionic/angular/lazy';
import { TranslatePipe } from '@ngx-translate/core';

import { AlphaOutlineModule } from '../../shared/alpha-outline/alpha-outline.module';
import { AvatarModule } from '../../shared/avatar/avatar.module';
import { PopoverModule } from '../../shared/popover/popover.module';
import { AccountMenuComponent } from './account-menu/account-menu.component';
import { PrimaryNavComponent } from './primary-nav/primary-nav.component';
import { SecondaryPanelComponent } from './secondary-panel/secondary-panel.component';

@NgModule({
  declarations: [PrimaryNavComponent, SecondaryPanelComponent, AccountMenuComponent],
  // TranslatePipe стандалонний (ngx-translate 18 більше не має NgModule),
  // тому додається напряму в imports поряд з CommonModule/IonicModule.
  imports: [CommonModule, RouterModule, IonicModule, TranslatePipe, AvatarModule, PopoverModule, AlphaOutlineModule],
  exports: [PrimaryNavComponent, SecondaryPanelComponent],
})
export class LayoutModule {}
