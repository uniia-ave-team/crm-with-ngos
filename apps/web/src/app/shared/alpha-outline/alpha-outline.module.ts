import { NgModule } from '@angular/core';

import { AlphaOutlineDirective } from './alpha-outline.directive';

/** Контур за альфа-каналом зображення (`appAlphaOutline`); підключається в модулі, де потрібен. */
@NgModule({
  declarations: [AlphaOutlineDirective],
  exports: [AlphaOutlineDirective],
})
export class AlphaOutlineModule {}
