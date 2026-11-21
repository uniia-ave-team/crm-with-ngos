import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { IonButtons, IonContent, IonHeader, IonMenuButton, IonTitle, IonToolbar } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

@Component({
  selector: 'app-settings',
  templateUrl: './settings.page.html',
  styleUrls: ['./settings.page.scss'],
  imports: [IonButtons, IonContent, IonHeader, IonMenuButton, IonTitle, IonToolbar, RouterOutlet, TranslatePipe],
})
export class SettingsPage {}
