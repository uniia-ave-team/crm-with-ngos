import { Component, inject } from '@angular/core';

import { AccountsService } from './core/accounts/accounts.service';
import { LayoutService, LayoutSection } from './core/layout/layout.service';

@Component({
  selector: 'app-root',
  templateUrl: 'app.component.html',
  styleUrls: ['app.component.scss'],
  standalone: false,
})
export class AppComponent {
  protected readonly layout = inject(LayoutService);

  /** Іде пошук чинного акаунта чи перемикання на інший — на весь час показуємо прелоадер. */
  protected readonly accountSearch = inject(AccountsService).switching;

  // Ключі перекладу (не готовий текст) — заголовок другої панелі
  // перекладається через `| translate` у шаблоні.
  protected readonly titleKeys: Record<LayoutSection, string> = {
    'knowledge-base': 'page.knowledgeBase',
    planner: 'page.planner',
    settings: 'settings.menu.title',
  };
}
