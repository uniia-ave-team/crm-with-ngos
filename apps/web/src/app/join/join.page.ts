import { Component, computed, effect, inject, signal, untracked, WritableSignal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { IonButton, IonContent, IonInput } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

import { AuthService, REDIRECT_URL } from '../core/auth/auth.service';
import { PasswordPolicyService } from '../core/auth/password-policy.service';
import { ConnectionService } from '../core/connection/connection.service';
import { InvitationFailure, InvitationsService } from '../core/invitations/invitations.service';
import { isPasswordTooLong, isStrongPassword } from '../shared/password-fields/password-rules';
import { PasswordFieldsComponent } from '../shared/password-fields/password-fields.component';
import { tokenFromFragment } from '../core/auth/link-token';

/**
 * Що показує сторінка: перевірку сервера й токена, помилку «сервера не знайдено», помилку
 * «посилання неактивне» (сервер відхилив токен), помилку «не вдалося перевірити» (збій мережі чи
 * сервера, з кнопкою повтору) або форму реєстрації.
 */
type JoinView = 'loading' | 'no-server' | 'invalid' | 'exists' | 'check-failed' | 'form';

/**
 * Сторінка запрошення (`/join#token=…`): колега переходить за посиланням із
 * діалогу «Додати колегу», завершує реєстрацію й одразу входить. Без стражів і
 * без бічного меню. Сервер — той, на домен якого веде посилання
 * (`ConnectionService.pageServerUrl`), а не сервер активного акаунта.
 */
@Component({
  selector: 'app-join',
  templateUrl: './join.page.html',
  styleUrls: ['./join.page.scss'],
  imports: [IonButton, IonContent, IonInput, RouterLink, FormsModule, PasswordFieldsComponent, TranslatePipe],
})
export class JoinPage {
  private readonly connection = inject(ConnectionService);
  private readonly invitations = inject(InvitationsService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  /** Умови до пароля сервера, на якому задають пароль (`PasswordPolicyService`). */
  protected readonly passwordPolicy = inject(PasswordPolicyService).policy(this.connection.pageServerUrl);

  /** Фрагмент адреси сторінки (`#token=…`). */
  private readonly fragment = toSignal(inject(ActivatedRoute).fragment, { initialValue: null });
  /** Токен із фрагмента адреси (`#token=…`, див. `link-token.ts`). */
  protected readonly token = computed(() => tokenFromFragment(this.fragment()));

  protected readonly view = signal<JoinView>('loading');

  /** Пошта з відповіді сервера на перевірку токена: у формі вона неактивна. */
  protected readonly email = signal('');

  protected readonly lastName = signal('');
  protected readonly firstName = signal('');
  /** По батькові: необовʼязкове, перевірки нема; порожнє йде в запит як `null`. */
  protected readonly patronymic = signal('');
  protected readonly password = signal('');
  protected readonly passwordRepeat = signal('');
  protected readonly saving = signal(false);
  private readonly submitted = signal(false);
  /** Ключ помилки запиту (`join.saveError.<ключ>`) або `null`. */
  private readonly saveError = signal<string | null>(null);

  // Помилки показуємо лише після спроби надіслати форму; `null` — все гаразд.
  protected readonly lastNameError = computed(() =>
    this.submitted() && !this.lastName().trim() ? 'lastNameRequired' : null,
  );
  protected readonly firstNameError = computed(() =>
    this.submitted() && !this.firstName().trim() ? 'firstNameRequired' : null,
  );
  protected readonly passwordError = computed(() => {
    if (!this.submitted()) {
      return null;
    }
    const password = this.password();
    if (!password) {
      return 'passwordRequired';
    }
    if (isPasswordTooLong(password)) {
      return 'passwordTooLong';
    }
    return isStrongPassword(password, this.passwordPolicy()) ? null : 'passwordWeak';
  });
  protected readonly passwordRepeatError = computed(() => {
    if (!this.submitted()) {
      return null;
    }
    const repeat = this.passwordRepeat();
    if (!repeat) {
      return 'passwordRepeatRequired';
    }
    return repeat === this.password() ? null : 'passwordMismatch';
  });

  /** Помилка прізвища й відповідь сервера — блоком над формою (ключі перекладу), як на сторінці входу. */
  protected readonly bannerErrors = computed<string[]>(() => {
    const lastName = this.lastNameError();
    const save = this.saveError();
    return [lastName && `join.error.${lastName}`, save && `join.saveError.${save}`].filter(
      (key): key is string => !!key,
    );
  });

  /** Запит перевірки, що тече: відповідь на застарілий (інший токен) не має нічого змінити. */
  private loadId = 0;

  constructor() {
    effect(() => {
      const token = this.token();
      untracked(() => void this.load(token));
    });
  }

  /**
   * Спершу перевіряє, чи є сервер за посиланням (`GET /health`), тоді — токен
   * (`GET /api/v1/invitations/validate`). Сервер відхилив токен (400) або токена в посиланні
   * нема — «посилання неактивне»: причини (прострочений, зіпсований) людині не розрізнити.
   * Акаунт із поштою запрошення вже є (409, зокрема запрошення вже прийняли) — «увійдіть». Інша невдача (сервер не відповів, помилка сервера, відповідь не за контрактом) —
   * про токен нічого не каже, тож «не вдалося перевірити» з кнопкою повтору (`retry`).
   */
  private async load(token: string | undefined): Promise<void> {
    const id = ++this.loadId;
    this.view.set('loading');

    const healthy = await this.connection.probeApi(this.connection.pageServerUrl);
    if (id !== this.loadId) {
      return;
    }
    if (!healthy) {
      this.view.set('no-server');
      return;
    }

    const details = token ? await this.invitations.validate(this.connection.pageServerUrl, token) : 'invalid';
    if (id !== this.loadId) {
      return;
    }
    if (typeof details === 'string') {
      this.view.set(details === 'invalid' || details === 'exists' ? details : 'check-failed');
      return;
    }
    this.email.set(details.email);
    this.view.set('form');
  }

  /** «Спробувати ще раз» після невдалої перевірки: та сама перевірка заново. */
  protected retry(): void {
    void this.load(this.token());
  }

  protected onFieldChange(field: WritableSignal<string>, value: string | number | null | undefined): void {
    field.set(value == null ? '' : String(value));
  }

  /**
   * Перевіряє форму; якщо все гаразд — завершує реєстрацію
   * (`POST /api/v1/invitations/accept`), додає отриманий акаунт до збережених як
   * активний і веде на `REDIRECT_URL`. Помилка сервера лишає форму на місці, щоб
   * надіслати її ще раз.
   */
  protected async submit(): Promise<void> {
    if (this.saving()) {
      return;
    }
    this.submitted.set(true);
    if (this.lastNameError() || this.firstNameError() || this.passwordError() || this.passwordRepeatError()) {
      return;
    }

    this.saving.set(true);
    this.saveError.set(null);
    const server = this.connection.pageServerUrl;
    const result = await this.invitations.accept(server, this.token() ?? '', {
      firstName: this.firstName().trim(),
      lastName: this.lastName().trim(),
      patronymic: this.patronymic().trim() || null,
      password: this.password(),
      confirmPassword: this.passwordRepeat(),
    });
    this.saving.set(false);
    if (typeof result === 'string') {
      this.saveError.set(saveErrorKey(result));
      return;
    }

    if (!this.auth.adoptTokens(server, result, this.email())) {
      // Реєстрацію завершено, але браузер не дав записати акаунт: увійти можна буде пізніше.
      this.saveError.set('storageFull');
      return;
    }
    void this.router.navigateByUrl(REDIRECT_URL);
  }
}

/** Ключ помилки реєстрації (`join.saveError.<ключ>`) за відповіддю сервера. */
function saveErrorKey(result: InvitationFailure): string {
  switch (result) {
    case 'invalid':
      return 'invalid';
    case 'exists':
      return 'exists';
    case 'unreachable':
      return 'unreachable';
    default:
      return 'error';
  }
}
