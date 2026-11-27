import { Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
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
  MenuController,
} from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

import { AccountsService } from './core/accounts/accounts.service';
import { TABLE_TEMPLATES_PERMISSIONS, hasAnyPermission } from './core/auth/permissions';
import { ConnectionService } from './core/connection/connection.service';
import { KbAddDialogComponent } from './core/knowledge-base/kb-add-dialog/kb-add-dialog.component';
import { KnowledgeBaseService } from './core/knowledge-base/knowledge-base.service';
import { DemoBannerComponent } from './core/layout/demo-banner/demo-banner.component';
import { LayoutService, LayoutSection } from './core/layout/layout.service';
import { MaintenanceNoticeService } from './core/maintenance/maintenance-notice.service';
import { SuccessNoticeService } from './core/notice/success-notice.service';
import { PrimaryNavComponent } from './core/layout/primary-nav/primary-nav.component';
import { SecondaryPanelComponent } from './core/layout/secondary-panel/secondary-panel.component';
import { CurrentUserService } from './core/users/current-user.service';
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
    DemoBannerComponent,
    PrimaryNavComponent,
    SecondaryPanelComponent,
    PreloaderComponent,
    KbAddDialogComponent,
    RouterLink,
  ],
})
export class AppComponent {
  protected readonly layout = inject(LayoutService);

  protected readonly accounts = inject(AccountsService);
  /** Оголошення сервера про технічні роботи (плашка вгорі). */
  protected readonly maintenance = inject(MaintenanceNoticeService);
  /** Повідомлення про успіх, що переживає перехід на іншу сторінку (плашка вгорі). */
  protected readonly successNotice = inject(SuccessNoticeService);
  protected readonly knowledgeBase = inject(KnowledgeBaseService);
  /** Бокове меню (друга панель) відкрите як шторка (на телефоні): тоді видно плаваючу «+». */
  protected readonly menuOpen = signal(false);

  /** Іде пошук чинного акаунта чи перемикання на інший — на весь час показуємо прелоадер. */
  protected readonly accountSearch = this.accounts.switching;

  /** Сервер у демо-режимі: над оболонкою стоїть банер (`app-demo-banner`), і вона займає решту висоти. */
  protected readonly demo = inject(ConnectionService).isDemo;

  // Ключі перекладу (не готовий текст) — заголовок другої панелі
  // перекладається через `| translate` у шаблоні.
  protected readonly titleKeys: Record<LayoutSection, string> = {
    'knowledge-base': 'page.knowledgeBase',
    planner: 'page.planner',
    settings: 'settings.menu.title',
  };

  private readonly currentUser = inject(CurrentUserService);
  private readonly router = inject(Router);
  private readonly menu = inject(MenuController);

  /** Кнопка менеджера шаблонів бази знань — лише з `ViewTableTemplate`. */
  protected readonly canViewTemplates = computed(() => hasAnyPermission(this.currentUser.permissions(), TABLE_TEMPLATES_PERMISSIONS));

  /** Плаваюча кнопка менеджера шаблонів (телефон): закриває бокове меню й відкриває менеджер. */
  protected async openTemplatesFromMenu(): Promise<void> {
    await this.menu.close('secondary');
    await this.router.navigateByUrl('/knowledge-base/templates');
  }

  /** «+» біля «База знань» (чи плаваюча на телефоні): додати сторінку в корінь змісту. */
  protected addKnowledgeBasePage(): void {
    this.knowledgeBase.requestInsert({ folder: null });
  }
}
