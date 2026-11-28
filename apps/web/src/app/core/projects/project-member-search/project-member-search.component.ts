import { Component, Injector, afterNextRender, computed, inject, input, output, signal, viewChild } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { IonIcon } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

import { AccountsService } from '../../accounts/accounts.service';
import { UserSummary } from '../../users/users.service';
import { Colleague, ColleagueSearchComponent } from '../colleague-search/colleague-search.component';
import { MemberChoiceComponent } from '../member-choice/member-choice.component';
import { PROJECT_CLAIMS, ProjectClaim } from '../projects.service';

/**
 * Поле додавання колеги до проєкту (згори вікна доступів, як у Figma): пошук колеги
 * (`app-colleague-search`, з сервера, без тих, хто вже учасник, — `memberIds`), у полі праворуч
 * тег-вибір рівня доступу нового учасника (спершу «Редактор»), після поля — «Додати» (лише коли
 * колегу вибрано; віддає його, рівень і назву ролі — `add`). Вибір колеги (лише одного) замінює пошук
 * на нього самого — аватар, ПІБ і поле назви ролі (`app-member-choice`); хрестик біля «Додати»
 * скидає вибір і повертає пошук. Вибір рівня — окремий блок у правому верхньому куті поля.
 */
@Component({
  selector: 'app-project-member-search',
  templateUrl: './project-member-search.component.html',
  styleUrls: ['./project-member-search.component.scss'],
  imports: [ColleagueSearchComponent, IonIcon, MemberChoiceComponent, NgTemplateOutlet, TranslatePipe],
})
export class ProjectMemberSearchComponent {
  /** `id` користувачів, які вже учасники проєкту: у списку їх нема. */
  readonly memberIds = input<readonly string[]>([]);
  /**
   * Де закінчується відкритий список колег — px від верху цього поля (0 — список закритий). Список
   * лежить поверх усього нижче, тож вікно з прокруткою має бути не нижчим за цю межу.
   */
  readonly overlayBottom = output<number>();
  /** «Додати»: вибраний колега, рівень доступу й назва ролі в проєкті (порожня — `null`). */
  readonly add = output<{ readonly user: UserSummary; readonly claim: ProjectClaim; readonly roleTitle: string | null }>();

  private readonly accounts = inject(AccountsService);
  private readonly injector = inject(Injector);
  private readonly search = viewChild(ColleagueSearchComponent);

  protected readonly claims = PROJECT_CLAIMS;
  /** Рівень доступу нового учасника (спершу — редактор). */
  protected readonly newClaim = signal<ProjectClaim>(1);
  /** Вибраний колега (замість пошуку — він із полем назви ролі); `null` — пошук. */
  protected readonly chosen = signal<UserSummary | null>(null);
  /** Назва ролі вибраного колеги в проєкті. */
  protected readonly roleTitle = signal('');
  /** Назва організації: бере участь у кольорі аватарки (як у колег). */
  protected readonly ngoName = computed(() => this.accounts.active()?.ngoName ?? '');

  protected onNewClaim(event: Event): void {
    this.newClaim.set(Number((event.target as HTMLSelectElement).value) as ProjectClaim);
  }

  /** Вибрали колегу: замість пошуку — він із полем назви ролі (курсор — у ньому). */
  protected choose(colleague: Colleague): void {
    // Пошук тут — на сервері, тож колега — `UserSummary`.
    this.chosen.set(colleague as UserSummary);
    this.roleTitle.set('');
    this.overlayBottom.emit(0);
  }

  /** Хрестик біля «Додати»: вибір скидається, повертається поле пошуку (з курсором). */
  protected reset(): void {
    this.chosen.set(null);
    this.roleTitle.set('');
    afterNextRender(() => this.search()?.focus(), { injector: this.injector });
  }

  /** «Додати»: віддає вибраного колегу з рівнем і назвою ролі (запит робить вікно доступів). */
  protected submit(): void {
    const user = this.chosen();
    if (user) {
      this.add.emit({ user, claim: this.newClaim(), roleTitle: this.roleTitle().trim() || null });
    }
  }
}
