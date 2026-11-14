import { Component, inject, input, output } from '@angular/core';
import { Router } from '@angular/router';
import { IonIcon } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

import { REDIRECT_URL } from '../../core/auth/auth.service';
import { AfterSignIn, SignInFormComponent } from '../../core/auth/sign-in-form/sign-in-form.component';
import { ConnectionService } from '../../core/connection/connection.service';
import { DEFAULT_LOGIN_IMAGE, LoginPageImagesService } from '../../core/login-page-images/login-page-images.service';

/**
 * Форма входу на сторінці повторного входу (`/relogin`): «Назад», заголовок «Вхід в акаунт» зі
 * смітником (лише для збереженого акаунта) і сама форма входу з полем адреси сервера
 * (`app-sign-in-form`: перевірка сервера, вхід, помилки — усе в ній).
 *
 * - Адреса й пошта початково з акаунта (`initialServer`, `initialEmail`) або адреса сторінки й
 *   порожня пошта (для нового акаунта).
 * - Зображення збоку (`imageChange`): коли сервер за адресою у формі здоровий, з нього береться
 *   випадкове зображення; інакше — стандартне.
 * - Після входу акаунт уже активний: підключення перевіряється на його сервері, і відкривається
 *   `REDIRECT_URL`.
 * - Смітник просить сторінку забути акаунт (`remove`), «Назад» — повернутись до списку (`back`).
 */
@Component({
  selector: 'app-relogin-form',
  templateUrl: './relogin-form.component.html',
  styleUrls: ['./relogin-form.component.scss'],
  imports: [IonIcon, SignInFormComponent, TranslatePipe],
})
export class ReloginFormComponent {
  private readonly connection = inject(ConnectionService);
  private readonly router = inject(Router);
  private readonly images = inject(LoginPageImagesService);

  /** Ключ акаунта, у який входять знову; `null` — додається новий (смітника нема). */
  readonly accountId = input<string | null>(null);
  /** Адреса сервера, вписана спочатку. */
  readonly initialServer = input('');
  /** Пошта, вписана спочатку. */
  readonly initialEmail = input('');

  readonly back = output<void>();
  readonly remove = output<void>();
  /** Зображення збоку: з сервера за адресою або стандартне. */
  readonly imageChange = output<string>();

  /** Номер останнього запиту зображення: відповідь для попереднього сервера відкидається. */
  private imageRequest = 0;

  /** Сервер у формі змінився: здоровий — беремо з нього зображення, ні (`null`) — стандартне. */
  protected async onServerChange(origin: string | null): Promise<void> {
    const request = ++this.imageRequest;
    if (!origin) {
      this.imageChange.emit(DEFAULT_LOGIN_IMAGE);
      return;
    }
    const image = await this.images.random(origin);
    if (request === this.imageRequest) {
      this.imageChange.emit(image?.url ?? DEFAULT_LOGIN_IMAGE);
    }
  }

  /**
   * Після входу акаунт уже активний: підключення (стан API, статус системи) досі належить
   * іншому серверу, тож перевіряємо його заново, а тоді відкриваємо першу сторінку.
   */
  protected readonly openApp: AfterSignIn = async () => {
    await this.connection.checkApi();
    void this.router.navigateByUrl(REDIRECT_URL, { onSameUrlNavigation: 'reload' });
  };
}
