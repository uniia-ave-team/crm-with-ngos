import { Component, inject } from '@angular/core';

import { AccountRestoreService } from './core/accounts/account-restore.service';
import { ConnectionService } from './core/connection/connection.service';
import { LayoutService, LayoutSection } from './core/layout/layout.service';

@Component({
  selector: 'app-root',
  templateUrl: 'app.component.html',
  styleUrls: ['app.component.scss'],
  standalone: false,
})
export class AppComponent {
  protected readonly layout = inject(LayoutService);

  /** Іде пошук чинного акаунта — на весь час показуємо прелоадер. */
  protected readonly accountSearch = inject(AccountRestoreService).searching;

  /** Сервер у демо-режимі: над оболонкою стоїть банер (`app-demo-banner`), і вона займає решту висоти. */
  protected readonly demo = inject(ConnectionService).isDemo;

  // Ключі перекладу (не готовий текст) — заголовок другої панелі
  // перекладається через `| translate` у шаблоні.
  protected readonly titleKeys: Record<LayoutSection, string> = {
    'knowledge-base': 'page.knowledgeBase',
    planner: 'page.planner',
    settings: 'settings.menu.title',
  };
}
