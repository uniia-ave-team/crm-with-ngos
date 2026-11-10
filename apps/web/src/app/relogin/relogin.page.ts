import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { Router } from '@angular/router';

import { AccountRestoreService } from '../core/accounts/account-restore.service';
import { AccountsService, StoredAccount } from '../core/accounts/accounts.service';
import { AuthService, LOGIN_URL, REDIRECT_URL } from '../core/auth/auth.service';
import { ConnectionService } from '../core/connection/connection.service';
import { LanguageService } from '../core/i18n/language.service';
import { mobileLogoBase } from '../core/i18n/mobile-logo';
import { DEFAULT_LOGIN_IMAGE } from '../core/login-page-images/login-page-images.service';
import { AccountCaption, AccountTile, accountCaption, toAccountTiles } from '../shared/account-avatar/account-tile';

/** Що показано справа: список акаунтів чи форма входу. */
type ReloginView = 'list' | 'form';

/** Для якого акаунта відкрито форму: збережений (`id`) чи новий (`id: null`), з адресою й поштою, що вписуються спочатку. */
interface FormTarget {
  readonly id: string | null;
  readonly server: string;
  readonly email: string;
}

/**
 * Сторінка повторного входу (`/relogin`): вибір акаунта, як у Google. Зображення зліва, справа
 * список збережених акаунтів і кнопка додавання, а після вибору — форма входу. Без стражів і без
 * бічного меню, як `/login` і `/join`: відкривається за будь-якого стану сесії.
 *
 * - Перевірка при вході: у памʼяті нема жодного акаунта (повторно входити нікуди) —
 *   переадресація на `/login`; те саме, якщо акаунти зникнуть, поки сторінка відкрита.
 * - Одразу після відкриття недоступні (але не застарілі) акаунти пробують оновити токени
 *   (`AuthService.refreshStored`): вдалося — акаунт знову доступний, токени нові; ні — лишається як є.
 * - Вибір зі списку: застарілий акаунт (незалежно від доступності) — форма; інший (доступний чи
 *   недоступний) перемикається (`AccountRestoreService.activate`, запит до сервера): вдалося —
 *   він активний, і відкривається `REDIRECT_URL`, ні — форма. «Додати акаунт» — форма для нового.
 * - Форма (`ReloginFormComponent`): зображення збоку береться з сервера за адресою у формі;
 *   «Назад» повертає до списку, смітник забуває акаунт (видаляє його дані з памʼяті) і теж повертає до списку.
 */
@Component({
  selector: 'app-relogin',
  templateUrl: './relogin.page.html',
  styleUrls: ['./relogin.page.scss'],
  standalone: false,
})
export class ReloginPage {
  private readonly accountsStore = inject(AccountsService);
  private readonly router = inject(Router);
  private readonly languageService = inject(LanguageService);
  private readonly restore = inject(AccountRestoreService);
  private readonly auth = inject(AuthService);
  private readonly connection = inject(ConnectionService);

  /** Усі збережені акаунти (за порядком додавання), у тому числі застарілі й недоступні. */
  protected readonly accounts = computed<readonly AccountTile[]>(() =>
    toAccountTiles(this.accountsStore.accounts(), this.accountsStore.activeId()),
  );

  protected readonly view = signal<ReloginView>('list');
  protected readonly formTarget = signal<FormTarget | null>(null);
  /** Зображення збоку: стандартне, а у формі — з сервера за адресою. */
  protected readonly image = signal(DEFAULT_LOGIN_IMAGE);
  /** Іде перемикання на акаунт: повторні клацання ігноруються. */
  protected readonly switching = signal(false);

  /** Вордмарк «Явір» вгорі (як на сторінці входу; `.svg` / `_dark.svg`). */
  protected readonly logoBase = computed(() => mobileLogoBase(this.languageService.language()));

  constructor() {
    effect(() => {
      const hasAccounts = this.accountsStore.accounts().length > 0;
      untracked(() => {
        if (!hasAccounts) {
          void this.router.navigateByUrl(LOGIN_URL);
        }
      });
    });
    void this.refreshUnavailable();
  }

  /** Підпис рядка: «ПІБ — Назва ГО» і, якщо з акаунтом негаразд, його стан (ті самі тексти, що в підказці плитки меню). */
  protected caption(account: AccountTile, unavailableText: string, outdatedText: string): AccountCaption {
    return accountCaption(account, unavailableText, outdatedText);
  }

  /** Недоступні, але не застарілі акаунти: пробуємо оновити їхні токени (разом, не чекаючи один одного). */
  private async refreshUnavailable(): Promise<void> {
    const ids = this.accountsStore
      .accounts()
      .filter((account) => account.unavailable && !account.outdated)
      .map((account) => account.id);
    await Promise.all(ids.map((id) => this.auth.refreshStored(id)));
  }

  /** Клацання по акаунту в списку. */
  protected async choose(tile: AccountTile): Promise<void> {
    const account = this.accountsStore.accounts().find((candidate) => candidate.id === tile.id);
    if (!account || this.switching()) {
      return;
    }
    if (account.outdated) {
      this.openForm(account);
      return;
    }
    this.switching.set(true);
    try {
      if ((await this.restore.activate(account.id)) === 'ok') {
        void this.router.navigateByUrl(REDIRECT_URL, { onSameUrlNavigation: 'reload' });
        return;
      }
    } finally {
      this.switching.set(false);
    }
    // Не вдалося (токени відхилено чи сервер мовчить): форма з даними цього акаунта, якщо він ще збережений.
    const failed = this.accountsStore.accounts().find((candidate) => candidate.id === account.id);
    if (failed) {
      this.openForm(failed);
    }
  }

  /** «Додати акаунт»: форма для нового акаунта, адреса — поточна (домен сторінки), пошта порожня. */
  protected addAccount(): void {
    this.formTarget.set({ id: null, server: this.connection.pageServerUrl, email: '' });
    this.view.set('form');
  }

  private openForm(account: StoredAccount): void {
    this.formTarget.set({ id: account.id, server: account.server, email: account.email });
    this.view.set('form');
  }

  /** «Назад»: до списку, із стандартним зображенням. */
  protected backToList(): void {
    this.view.set('list');
    this.formTarget.set(null);
    this.image.set(DEFAULT_LOGIN_IMAGE);
  }

  /** Смітник: забуває акаунт цієї форми (його дані зникають з памʼяті) і повертає до списку. */
  protected removeAccount(): void {
    const id = this.formTarget()?.id;
    if (id) {
      this.accountsStore.remove(id);
    }
    this.backToList();
  }

  /** Зображення збоку не завантажилося (битий файл, посилання) — повертаємо стандартне. */
  protected onImageError(): void {
    if (this.image() !== DEFAULT_LOGIN_IMAGE) {
      this.image.set(DEFAULT_LOGIN_IMAGE);
    }
  }
}
