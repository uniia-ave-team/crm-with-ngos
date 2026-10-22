import { NgModule } from '@angular/core';

import { ProtectedSrcDirective } from './protected-src.directive';

/** Директива `appProtectedSrc` для зображень за авторизацією; підключається в модулі, де потрібна. */
@NgModule({
  imports: [ProtectedSrcDirective],
  exports: [ProtectedSrcDirective],
})
export class ProtectedSrcModule {}
