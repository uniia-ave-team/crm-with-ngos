import { CommonModule } from '@angular/common';
import { NgModule } from '@angular/core';
import { IonicModule } from '@ionic/angular/lazy';

import { AlphaOutlineModule } from '../alpha-outline/alpha-outline.module';
import { AvatarModule } from '../avatar/avatar.module';
import { ProtectedSrcModule } from '../protected-src/protected-src.module';
import { AccountAvatarComponent } from './account-avatar.component';

/** Аватар акаунта зі значками стану й логотипом ГО (`<app-account-avatar>`). */
@NgModule({
  declarations: [AccountAvatarComponent],
  imports: [CommonModule, IonicModule, AvatarModule, AlphaOutlineModule, ProtectedSrcModule],
  exports: [AccountAvatarComponent],
})
export class AccountAvatarModule {}
