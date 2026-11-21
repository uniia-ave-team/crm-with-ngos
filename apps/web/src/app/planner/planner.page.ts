import { Component } from '@angular/core';
import { IonButtons, IonContent, IonHeader, IonMenuButton, IonTitle, IonToolbar } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-planner',
  templateUrl: './planner.page.html',
  styleUrls: ['./planner.page.scss'],
  imports: [IonButtons, IonContent, IonHeader, IonMenuButton, IonTitle, IonToolbar, TranslatePipe],
})
export class PlannerPage {}
