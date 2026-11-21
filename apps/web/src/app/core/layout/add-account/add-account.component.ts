import { Component, computed, inject, signal, viewChild } from '@angular/core';
import { Router } from '@angular/router';
import { IonIcon } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

import { ModalComponent } from '../../../shared/modal/modal.component';
import { AccountsService } from '../../accounts/accounts.service';
import { REDIRECT_URL } from '../../auth/auth.service';
import { AfterSignIn, SignInFormComponent } from '../../auth/sign-in-form/sign-in-form.component';
import { ConnectionService } from '../../connection/connection.service';

/** Для чого відкрито вікно: що вписано у форму спочатку й який акаунт вхід може перезаписати. */
interface AddAccountTarget {
  readonly accountId: string | undefined;
  readonly server: string;
  readonly email: string;
}

/**
 * Вікно входу в акаунт (відкривається кнопкою «+» у меню акаунтів, клацанням по застарілому
 * акаунту (`needsSignIn`) або коли перемкнутися на акаунт не вдалося): заголовок і форма входу з
 * полем адреси сервера
 * (`app-sign-in-form`: перевірка сервера, вхід, помилки — усе в ній).
 *
 *   <app-add-account #addAccount />
 *   addAccount.open();                                  // новий акаунт: поточний сервер, порожня пошта
 *   addAccount.open('a@organization.org');              // пошта вже вписана
 *   addAccount.open(account.email, account.id, account.server); // вхід у збережений акаунт
 *
 * - `email` одразу вписується в поле пошти, `server` — у поле адреси замість поточного сервера
 *   (його можна змінити);
 * - `accountId` — ключ (`id`) збереженого акаунта. З ним заголовок «Ввійти в акаунт» (а не
 *   «Додати акаунт»), поруч із хрестиком стоїть смітник (видаляє цей акаунт з памʼяті й закриває
 *   вікно), а вхід із тією самою поштою перезаписує цей акаунт, а не додає новий.
 *
 * Після входу акаунт уже активний: вікно закривається, підключення перевіряється вже на його
 * сервері, а застосунок переходить на `REDIRECT_URL`. Помилка лишає вікно відкритим.
 */
@Component({
  selector: 'app-add-account',
  templateUrl: './add-account.component.html',
  styleUrls: ['./add-account.component.scss'],
  imports: [ModalComponent, IonIcon, SignInFormComponent, TranslatePipe],
})
export class AddAccountComponent {
  private readonly connection = inject(ConnectionService);
  private readonly accounts = inject(AccountsService);
  private readonly router = inject(Router);

  private readonly modal = viewChild.required(ModalComponent);

  /** Для чого вікно відкрито зараз; `null` — закрите. */
  private readonly target = signal<AddAccountTarget | null>(null);
  /**
   * Форма малюється для кожного відкриття заново (новий обʼєкт — нова форма): так вона
   * забуває введене минулого разу, а поки вікно закрите, її нема й сервер ніхто не перевіряє.
   */
  protected readonly forms = computed(() => {
    const target = this.target();
    return target ? [target] : [];
  });

  /** Ключ акаунта, для якого відкрито вікно (тоді показується смітник). */
  protected readonly accountId = computed(() => this.target()?.accountId);
  /** Заголовок вікна (ключ перекладу): для збереженого акаунта «Ввійти в акаунт», для нового «Додати акаунт». */
  protected readonly titleKey = computed(() => (this.accountId() ? 'nav.account.signIn' : 'nav.account.add'));

  /** Відкриває вікно з порожньою формою; що вписати спочатку — див. опис класу. */
  open(email?: string, accountId?: string, server?: string): void {
    this.target.set({ accountId, server: server ?? this.connection.serverUrl(), email: email ?? '' });
    this.modal().open();
  }

  /** Закриває вікно (проміс виконується, коли воно зникло). */
  close(): Promise<void> {
    return this.modal().close();
  }

  /** Вікно зникло (хрестик, затемнення, Esc, вхід) — форми більше нема. */
  protected onClosed(): void {
    this.target.set(null);
  }

  /**
   * Смітник: забуває акаунт, який відкрито вікном (його запис і токени зникають з памʼяті), і
   * закриває вікно. Так прибирають застарілий чи недоступний акаунт, що вже не потрібен.
   */
  protected async remove(): Promise<void> {
    const id = this.accountId();
    if (id) {
      this.accounts.remove(id);
    }
    await this.modal().close();
  }

  /**
   * Після входу новий акаунт уже активний: підключення (стан API, статус системи) досі належить
   * попередньому серверу, тож перевіряємо його заново, а тоді відкриваємо першу сторінку.
   */
  protected readonly openApp: AfterSignIn = async () => {
    await this.modal().close();
    await this.connection.checkApi();
    void this.router.navigateByUrl(REDIRECT_URL, { onSameUrlNavigation: 'reload' });
  };
}
