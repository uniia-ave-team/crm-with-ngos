import { Component, computed, input, model } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { AvatarComponent } from '../../../shared/avatar/avatar.component';
import { UserSummary } from '../../users/users.service';

/** Найдовша назва ролі в проєкті, яку прийме сервер (`ProjectValidationConstants.MaxRoleTitleLength`). */
export const PROJECT_ROLE_TITLE_MAX_LENGTH = 150;

/**
 * Вибраний для додавання колега (у полі додавання учасників замість пошуку): аватар і ПІБ — як у
 * рядках учасників, а під ПІБ поле назви ролі в проєкті (`roleTitle`, необовʼязкове, до 150 символів;
 * при появі отримує курсор).
 */
@Component({
  selector: 'app-member-choice',
  templateUrl: './member-choice.component.html',
  styleUrls: ['./member-choice.component.scss'],
  imports: [AvatarComponent, TranslatePipe],
})
export class MemberChoiceComponent {
  readonly user = input.required<UserSummary>();
  /** Назва організації: бере участь у кольорі аватарки (як у колег). */
  readonly ngoName = input('');
  /** Назва ролі колеги в проєкті. */
  readonly roleTitle = model('');

  protected readonly maxLength = PROJECT_ROLE_TITLE_MAX_LENGTH;
  /** ПІБ (нема — пошта). */
  protected readonly name = computed(() => {
    const user = this.user();
    return (
      [user.lastName, user.firstName, user.patronymic]
        .map((part) => part?.trim())
        .filter(Boolean)
        .join(' ') || user.email
    );
  });

  protected onRole(event: Event): void {
    this.roleTitle.set((event.target as HTMLInputElement).value);
  }
}
