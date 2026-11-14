import { Component } from '@angular/core';
import { IonContent } from '@ionic/angular';

import { AdminNgoFormComponent } from './ngo-form/ngo-form.component';

@Component({
  selector: 'app-admin-panel',
  templateUrl: './admin-panel.page.html',
  styleUrls: ['./admin-panel.page.scss'],
  imports: [IonContent, AdminNgoFormComponent],
})
export class AdminPanelPage {}
