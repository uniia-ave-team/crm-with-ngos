import { CommonModule } from '@angular/common';
import { NgModule } from '@angular/core';

import { AvatarComponent } from './avatar.component';

/** Аватар користувача (`<app-avatar>`); підключається в модулі, де він потрібен. */
@NgModule({
  declarations: [AvatarComponent],
  imports: [CommonModule],
  exports: [AvatarComponent],
})
export class AvatarModule {}
