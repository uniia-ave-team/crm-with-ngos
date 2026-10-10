import { Component, inject } from '@angular/core';

import { AccountRestoreService } from './core/accounts/account-restore.service';
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

  // Ключі перекладу (не готовий текст) — заголовок другої панелі
  // перекладається через `| translate` у шаблоні.
  protected readonly titleKeys: Record<LayoutSection, string> = {
    'knowledge-base': 'page.knowledgeBase',
    planner: 'page.planner',
  };
}
