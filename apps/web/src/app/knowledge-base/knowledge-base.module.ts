import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { IonicModule } from '@ionic/angular/lazy';
import { TranslatePipe } from '@ngx-translate/core';

import { KnowledgeBasePageRoutingModule } from './knowledge-base-routing.module';

import { KnowledgeBasePage } from './knowledge-base.page';

@NgModule({
  imports: [
    CommonModule,
    FormsModule,
    IonicModule,
    TranslatePipe,
    KnowledgeBasePageRoutingModule
  ],
  declarations: [KnowledgeBasePage]
})
export class KnowledgeBasePageModule {}
