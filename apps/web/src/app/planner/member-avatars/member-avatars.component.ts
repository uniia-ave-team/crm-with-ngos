import { Component, computed, inject, input, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { AccountsService } from '../../core/accounts/accounts.service';
import { ProjectMember } from '../../core/projects/projects.service';
import { CurrentUserService } from '../../core/users/current-user.service';
import { AvatarComponent } from '../../shared/avatar/avatar.component';

/** Скільки аватарок учасників видно; решта — третьою аватаркою з «...». */
const SHOWN_MEMBERS = 2;

/**
 * Учасники проєкту в шапці планера — кнопкою: дві аватарки внахлест (друга лежить на першій; якщо
 * серед учасників є сам користувач — він завжди першою аватаркою, решта — у порядку списку), а
 * якщо учасників більше — третя з «...» і пунктирною обвідкою (підказка — скільки ще). У справжніх
 * аватарок білий контур. Назва кнопки — `label` (дія) й імена всіх учасників; клік — `activate`.
 */
@Component({
  selector: 'app-member-avatars',
  templateUrl: './member-avatars.component.html',
  styleUrls: ['./member-avatars.component.scss'],
  imports: [AvatarComponent, TranslatePipe],
})
export class MemberAvatarsComponent {
  readonly members = input.required<readonly ProjectMember[]>();
  /** Що робить клік (перша частина назви кнопки), наприклад «Переглянути доступи». */
  readonly label = input('');
  readonly activate = output<void>();

  private readonly accounts = inject(AccountsService);
  private readonly currentUser = inject(CurrentUserService);

  /** Учасники з самим користувачем на початку (якщо він серед них). */
  private readonly ordered = computed(() => {
    const selfId = this.currentUser.user()?.id;
    const self = this.members().find((member) => member.userId === selfId);
    return self ? [self, ...this.members().filter((member) => member !== self)] : this.members();
  });
  /** Видимі аватарки: перші `SHOWN_MEMBERS` учасників. */
  protected readonly shown = computed(() => this.ordered().slice(0, SHOWN_MEMBERS));
  /** Скільки учасників не показано (тоді — аватарка з «...»). */
  protected readonly hidden = computed(() => Math.max(this.members().length - SHOWN_MEMBERS, 0));
  /** Імена всіх учасників — у підказці й назві кнопки. */
  protected readonly names = computed(() =>
    this.members()
      .map((member) => `${member.lastName} ${member.firstName}`.trim())
      .join(', '),
  );
  /** Назва організації: бере участь у кольорі аватарки (як у колег). */
  protected readonly ngoName = computed(() => this.accounts.active()?.ngoName ?? '');
}
