import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { IonicModule } from '@ionic/angular/lazy';
import { TranslatePipe } from '@ngx-translate/core';

import { AdminPanelPageRoutingModule } from './admin-panel-routing.module';

import { AdminPanelPage } from './admin-panel.page';
import { AdminImagesGalleryComponent } from './images-gallery/images-gallery.component';
import { AdminNgoFormComponent } from './ngo-form/ngo-form.component';

@NgModule({
  imports: [
    CommonModule,
    FormsModule,
    IonicModule,
    TranslatePipe,
    AdminPanelPageRoutingModule
  ],
  declarations: [AdminPanelPage, AdminNgoFormComponent, AdminImagesGalleryComponent]
})
export class AdminPanelPageModule {}
