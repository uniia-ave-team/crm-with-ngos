import { Component, input, output } from '@angular/core';
import { IonIcon } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

/**
 * Шапка вікна в `app-modal`: заголовок і хрестик (`common.close`) в один рядок, як у вікні
 * «Запрошення створено». Хрестик віддає подію `dismiss`; сам вікно не закриває.
 *
 *   <app-dialog-header [title]="…" (dismiss)="dialog.close()" />
 */
@Component({
  selector: 'app-dialog-header',
  templateUrl: './dialog-header.component.html',
  styleUrls: ['./dialog-header.component.scss'],
  imports: [IonIcon, TranslatePipe],
})
export class DialogHeaderComponent {
  readonly title = input.required<string>();
  readonly dismiss = output<void>();
}
