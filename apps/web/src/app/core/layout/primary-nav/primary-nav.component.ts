import { Component, Signal, computed, inject } from '@angular/core';

import { LanguageService } from '../../i18n/language.service';
import { mobileLogoBase } from '../../i18n/mobile-logo';

interface NavItem {
  readonly labelKey: string;
  readonly url: string;
  readonly icon: string;
  readonly iconActive: string;
}

/**
 * Перша (завжди видима) панель бічного меню: лого, основна навігація,
 * аватар і кнопка «Зоря». На десктопі — вертикальна колонка зліва;
 * на телефоні — верхня панель з аватаром і нижнє меню з пунктами навігації.
 * Обидва варіанти розмітки живуть в одному шаблоні й перемикаються CSS
 * медіазапитом, щоб не дублювати логіку активного пункту.
 */
@Component({
  selector: 'app-primary-nav',
  templateUrl: './primary-nav.component.html',
  styleUrls: ['./primary-nav.component.scss'],
  standalone: false,
})
export class PrimaryNavComponent {
  private readonly languageService = inject(LanguageService);

  protected readonly navItems: readonly NavItem[] = [
    { labelKey: 'nav.calendar', url: '/calendar', icon: 'calendar-outline', iconActive: 'calendar' },
    { labelKey: 'nav.planner', url: '/planner', icon: 'checkbox-outline', iconActive: 'checkbox' },
    { labelKey: 'nav.knowledgeBase', url: '/knowledge-base', icon: 'bulb-outline', iconActive: 'bulb' },
    { labelKey: 'nav.colleagues', url: '/colleagues', icon: 'people-outline', iconActive: 'people' },
  ];

  // Плейсхолдер, доки немає профілю користувача (див. apps/web/README.md,
  // розділ «Ще не зроблено» — автентифікація ще не реалізована).
  protected readonly userInitials = 'ЄВ';

  // Мобільний вордмарк-логотип (з текстом) є в окремих мовних варіантах
  // файлів — на відміну від іконки в десктопному рейлі (assets/logo.svg),
  // де тексту немає й перемикати мову не треба.
  protected readonly mobileLogoBase: Signal<string> = computed(
    () => mobileLogoBase(this.languageService.language()),
  );
}
