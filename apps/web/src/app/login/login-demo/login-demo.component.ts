import { Component, input, output } from '@angular/core';
import { IonButton } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

import { DemoAccount, DemoInfo } from '../../core/connection/connection.service';

/**
 * Демо-блок сторінки входу (сервер у демо-режимі): окрема рамка з поясненням, що це демо й
 * скільки триває сеанс, попередженням, що розробка ще триває (можливі помилки, деякі сервіси ще
 * недоступні), і кнопкою «Ввійти як …» на кожен демо-акаунт. Сам нічого не надсилає:
 * обраний акаунт віддає сторінці (`signIn`), а та входить через форму входу звичайним шляхом.
 */
@Component({
  selector: 'app-login-demo',
  templateUrl: './login-demo.component.html',
  styleUrls: ['./login-demo.component.scss'],
  imports: [IonButton, TranslatePipe],
})
export class LoginDemoComponent {
  /** Демо-режим сервера: акаунти й скільки хвилин від входу триває сеанс (потім дані скидаються). */
  readonly demo = input.required<DemoInfo>();
  /** Іде вхід — кнопки вимкнено. */
  readonly disabled = input(false);

  readonly signIn = output<DemoAccount>();
}
