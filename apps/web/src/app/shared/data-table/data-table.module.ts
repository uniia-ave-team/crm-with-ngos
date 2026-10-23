import { CommonModule } from '@angular/common';
import { NgModule } from '@angular/core';
import { IonicModule } from '@ionic/angular/lazy';
import { TranslatePipe } from '@ngx-translate/core';

import { DataTableComponent } from './data-table.component';

/** Таблиця з даних (`<app-data-table>`); підключається в модулі, де потрібна. */
@NgModule({
  declarations: [DataTableComponent],
  imports: [CommonModule, IonicModule, TranslatePipe],
  exports: [DataTableComponent],
})
export class DataTableModule {}
