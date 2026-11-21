import { Component } from '@angular/core';
import { IonButtons, IonContent, IonHeader, IonMenuButton, IonTitle, IonToolbar } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-knowledge-base',
  templateUrl: './knowledge-base.page.html',
  styleUrls: ['./knowledge-base.page.scss'],
  imports: [IonButtons, IonContent, IonHeader, IonMenuButton, IonTitle, IonToolbar, TranslatePipe],
})
export class KnowledgeBasePage {}
