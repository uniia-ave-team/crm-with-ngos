import { Component, computed, inject, input } from '@angular/core';
import { IonIcon } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

import { ActionBarContent, ActionBarService } from './action-bar.service';

/**
 * Смуга дій, закріплена внизу змісту сторінки: сторінка ставить її в `ion-content` зі
 * `slot="fixed"` (Ionic тримає такий елемент у видимій області змісту, і він не прокручується), а
 * сам компонент — `position: absolute` знизу на всю ширину. На ній — групи кнопок-іконок (назва — у
 * підказці й `aria-label`; групи рознесено по всій ширині, між ними — риска), які показав вид через `ActionBarService`;
 * недоступні — вимкнені. На телефоні (до 600px) групи завжди у два рядки порівну (а не як вийде в
 * браузера). Нема змісту — нема й смуги.
 *
 * Натискання не забирає фокус (`pointerdown` без типової дії): поле, для якого показано дії
 * (наприклад, клітинка таблиці), лишається вибраним.
 *
 *   <ion-content>
 *     <app-action-bar slot="fixed" />
 *     …
 *   </ion-content>
 *
 * Вбудована (`inline`) — у потоці сторінки чи вікна (наприклад, над рядком стовпчиків у формі
 * шаблону), з рамкою й заокругленням; зміст — вхід `content` (без нього — з `ActionBarService`).
 *
 *   <app-action-bar [inline]="true" [content]="toolbar()" />
 */
@Component({
  selector: 'app-action-bar',
  templateUrl: './action-bar.component.html',
  styleUrls: ['./action-bar.component.scss'],
  imports: [IonIcon, TranslatePipe],
  host: {
    '[class.action-bar--shown]': 'shown() !== null',
    '[class.action-bar--inline]': 'inline()',
    '(pointerdown)': '$event.preventDefault()',
  },
})
export class ActionBarComponent {
  private readonly service = inject(ActionBarService);
  /** Зміст напряму (вбудована смуга); не задано — зміст із `ActionBarService`. */
  readonly content = input<ActionBarContent | null | undefined>(undefined);
  /** У потоці сторінки чи вікна, а не закріплена внизу змісту. */
  readonly inline = input(false);
  /** Що показати: вхід `content`, а без нього — зміст сервісу. */
  protected readonly shown = computed(() => {
    const content = this.content();
    return content !== undefined ? content : this.service.content();
  });

  /** З якої групи на телефоні починається другий рядок (половина груп, перший рядок — не менший). */
  protected middle(groups: number): number {
    return Math.ceil(groups / 2);
  }
}
