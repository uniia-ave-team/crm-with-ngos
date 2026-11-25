import { Component, inject } from '@angular/core';
import {
  IonApp,
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonIcon,
  IonMenu,
  IonRouterOutlet,
  IonSplitPane,
  IonTitle,
  IonToast,
  IonToolbar,
} from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

import { AccountsService } from './core/accounts/accounts.service';
import { LayoutService, LayoutSection } from './core/layout/layout.service';
import { MaintenanceNoticeService } from './core/maintenance/maintenance-notice.service';
import { PrimaryNavComponent } from './core/layout/primary-nav/primary-nav.component';
import { SecondaryPanelComponent } from './core/layout/secondary-panel/secondary-panel.component';
import { PreloaderComponent } from './shared/preloader/preloader.component';

@Component({
  selector: 'app-root',
  templateUrl: 'app.component.html',
  styleUrls: ['app.component.scss'],
  imports: [
    IonApp,
    IonButton,
    IonButtons,
    IonContent,
    IonHeader,
    IonIcon,
    IonMenu,
    IonRouterOutlet,
    IonSplitPane,
    IonTitle,
    IonToast,
    IonToolbar,
    TranslatePipe,
    PrimaryNavComponent,
    SecondaryPanelComponent,
    PreloaderComponent,
  ],
})
export class AppComponent {
  protected readonly layout = inject(LayoutService);

  protected readonly accounts = inject(AccountsService);
  /** Оголошення сервера про технічні роботи (плашка вгорі). */
  protected readonly maintenance = inject(MaintenanceNoticeService);

  /** Іде пошук чинного акаунта чи перемикання на інший — на весь час показуємо прелоадер. */
  protected readonly accountSearch = this.accounts.switching;

  // Ключі перекладу (не готовий текст) — заголовок другої панелі
  // перекладається через `| translate` у шаблоні.
  protected readonly titleKeys: Record<LayoutSection, string> = {
    'knowledge-base': 'page.knowledgeBase',
    planner: 'page.planner',
    settings: 'settings.menu.title',
  };
}
