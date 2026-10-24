import { CommonModule } from '@angular/common';
import { NgModule } from '@angular/core';

import { ProtectedSrcModule } from '../protected-src/protected-src.module';
import { AvatarComponent } from './avatar.component';

/** Аватар користувача (`<app-avatar>`); підключається в модулі, де він потрібен. */
@NgModule({
  declarations: [AvatarComponent],
  imports: [CommonModule, ProtectedSrcModule],
  exports: [AvatarComponent],
})
export class AvatarModule {}
