import { Component, ElementRef, computed, inject, signal, viewChild } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { AccountsService } from '../../core/accounts/accounts.service';
import { CurrentUserService } from '../../core/users/current-user.service';
import { UsersService } from '../../core/users/users.service';
import { ProfileFormComponent, ProfileFormValue } from '../../shared/profile-form/profile-form.component';

/**
 * Що показати в блоці над формою за результатом останнього збереження. `avatar.*` —
 * дані профілю збережено, а аватар завантажити не вдалося.
 */
type SaveResult =
  | 'success'
  | 'invalid'
  | 'forbidden'
  | 'unreachable'
  | 'error'
  | 'avatar.invalid'
  | 'avatar.unauthorized'
  | 'avatar.forbidden'
  | 'avatar.unreachable'
  | 'avatar.error'
  | 'avatar.quotaExceeded';

/**
 * Вкладка «Дані профілю» (`/settings/personal-data`): спільна форма профілю
 * (`app-profile-form`), заповнена з профілю поточного користувача (`GET /users/me`, його
 * перепитує `authGuard` при кожній зміні сторінки). «Зберегти» шле `PUT /users/me`. Жодних прав
 * для цього не потрібно.
 *
 * Аватар у `PUT` не передається (посилання призначає сервер): якщо обрано файл,
 * після збереження даних іде додатковий запит `POST /users/me/avatar` із самим
 * зображенням; не обрано — цього запиту нема.
 */
@Component({
  selector: 'app-settings-personal-data',
  templateUrl: './personal-data.page.html',
  styleUrls: ['../settings-section.scss'],
  imports: [ProfileFormComponent, TranslatePipe],
})
export class PersonalDataPage {
  private readonly accounts = inject(AccountsService);
  private readonly currentUser = inject(CurrentUserService);
  private readonly users = inject(UsersService);
  private readonly host: ElementRef<HTMLElement> = inject(ElementRef);
  private readonly form = viewChild.required(ProfileFormComponent);

  /** Поточний користувач (профіль із `GET /api/v1/users/me`); `null`, доки не завантажено. */
  protected readonly user = this.currentUser.user;

  protected readonly saving = signal(false);
  /** Результат останнього збереження (блок над формою); `null` — ще не було. */
  protected readonly result = signal<SaveResult | null>(null);

  /** Ключ тексту блока над формою. */
  protected readonly resultKey = computed(() => {
    const result = this.result();
    return result ? `settings.personal.result.${result}` : null;
  });

  /** Назва ГО активного акаунта: входить у колір аватара, як і в боковому меню. */
  protected readonly ngoName = computed(() => this.accounts.active()?.ngoName ?? '');

  /**
   * «Зберегти» (поля форма вже перевірила): `PUT /users/me`. Якщо після цього обрано файл
   * аватара — додатковий запит `POST /users/me/avatar` із самим зображенням (файл не обрано —
   * запиту нема); невдача завантаження показується окремим червоним блоком, а файл лишається
   * обраним для повторної спроби.
   */
  protected async onSave({ profile: body, avatar }: ProfileFormValue): Promise<void> {
    if (this.saving()) {
      return;
    }
    this.result.set(null);
    this.saving.set(true);
    const outcome = await this.users.updateProfile(body);

    if (outcome === 'updated') {
      // Те, що збережено, одразу видно в меню й знімку акаунта, без нового запиту.
      this.currentUser.applyLocal({
        firstName: body.firstName,
        lastName: body.lastName,
        patronymic: body.patronymic,
        phoneNumber: body.phoneNumber,
        country: body.country,
        emergencyContact: body.emergencyContact,
        preferredLanguage: body.preferredLanguage,
        pronouns: body.pronouns,
        pronounCategory: body.pronounCategory,
      });
      // Збережене — тепер і є профіль: далі поля знову йдуть за ним.
      this.form().markSaved();
      // Результат показуємо після обох запитів: успіх — лише якщо й аватар (за наявності файлу) завантажено.
      this.result.set((await this.uploadAvatar(avatar)) ?? 'success');
      this.saving.set(false);
      this.scrollToTop();
      return;
    }
    this.saving.set(false);
    this.result.set(
      outcome === 'invalid' || outcome === 'forbidden' || outcome === 'unreachable' ? outcome : 'error',
    );
    this.scrollToTop();
  }

  /**
   * Другий запит збереження: обраний файл аватара (`POST /users/me/avatar`).
   * Успіх — профіль читається заново (`GET /users/me`: нове посилання на аватар
   * призначив сервер), обраний файл і його превʼю забуваються. Повертає результат невдачі
   * (червоний блок із причиною) або `null`, якщо завантажено чи файлу не було.
   */
  private async uploadAvatar(file: File | null): Promise<SaveResult | null> {
    if (!file) {
      return null;
    }
    const outcome = await this.users.uploadAvatar(file);
    if (outcome === 'uploaded') {
      this.form().clearAvatar();
      await this.currentUser.reload();
      return null;
    }
    return `avatar.${outcome}`;
  }

  /**
   * Результат показується над формою, а кнопка внизу: після відповіді прокручуємо
   * вміст сторінки (його `ion-content`) нагору, щоб блок було видно.
   */
  private scrollToTop(): void {
    const content = this.host.nativeElement.closest('ion-content') as HTMLIonContentElement | null;
    void content?.scrollToTop?.(300);
  }
}
