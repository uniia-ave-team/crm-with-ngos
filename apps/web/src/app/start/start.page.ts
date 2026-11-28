import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { IonContent } from '@ionic/angular';

import { freshPermissions } from '../core/auth/permission.guard';
import { EmptyStateComponent } from '../shared/empty-state/empty-state.component';
import { startUrl } from './start-url';

/**
 * Стартова сторінка (`/start`, `REDIRECT_URL`): сюди веде кожен новий вхід (форма входу, повторний
 * вхід, «Додати акаунт», перемикання акаунта, реєстрація за запрошенням, лого) і сторінка, на яку
 * бракує прав. Бере свіжі права (`freshPermissions`) і переходить на першу доступну сторінку
 * (`startUrl`: Планер, База знань, Колеги) без запису в історії, тож «Назад» на неї не повертає.
 * Жодна не доступна — лишається тут і показує порожній стан (`app-empty-state`): з бічного меню
 * лишаються «Адмін» (якщо є права) і налаштування профілю (вікно аватара). Поки права
 * перевіряються — нічого. Вхід не вдався — нікуди не веде: переадресацію на вхід робить `authGuard`.
 */
@Component({
  selector: 'app-start',
  templateUrl: './start.page.html',
  styleUrls: ['./start.page.scss'],
  imports: [EmptyStateComponent, IonContent],
})
export class StartPage {
  private readonly router = inject(Router);
  /** Доступної сторінки нема: показуємо порожній стан. */
  protected readonly empty = signal(false);

  constructor() {
    void freshPermissions().then((granted) => {
      if (granted === null) {
        return;
      }
      const url = startUrl(granted);
      if (url) {
        void this.router.navigateByUrl(url, { replaceUrl: true });
      } else {
        this.empty.set(true);
      }
    });
  }
}
