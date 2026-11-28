import { Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { IonIcon, IonSpinner } from '@ionic/angular';
import { TranslateService } from '@ngx-translate/core';
import { TranslatePipe } from '@ngx-translate/core';

import { AvatarComponent } from '../../../shared/avatar/avatar.component';
import { AccountsService } from '../../accounts/accounts.service';
import { PERMISSIONS } from '../../auth/permissions';
import { CurrentUserService } from '../../users/current-user.service';
import { UserSummary } from '../../users/users.service';
import { ProjectMemberSearchComponent } from '../project-member-search/project-member-search.component';
import { PROJECT_CLAIMS, PROJECT_VISIBILITY_ICONS, ProjectClaim, ProjectMember, ProjectVisibility, ProjectsService } from '../projects.service';

/** Стан списку учасників: завантажуємо, є чи не вдалося (ключ помилки). */
type MembersState =
  | { readonly status: 'loading' }
  | { readonly status: 'ready'; readonly members: readonly ProjectMember[] }
  | { readonly status: 'failed'; readonly errorKey: string };

/**
 * Доступи проєкту (зміст вікна «Редагувати доступи» / «Переглянути доступи»): учасники
 * (`ProjectsService.members`) — аватар, ПІБ, під ним назва ролі в проєкті (як займенники в таблиці
 * колег; нема — «—»), а збоку тегом рівень доступу (`planner.claims.*`: Переглядач, Редактор,
 * Керівник). Вимкнені акаунти — приглушені, з позначкою. З правом змінювати (`editable`, тобто
 * `UpdateProject`) тег у чужих рядках — вибір рівня (виглядає як тег зі стрілкою вниз;
 * `updateMember`), а за ним — кнопка «Прибрати з проєкту» (`removeMember`); свій рядок —
 * завжди простий тег. Невдача зміни — причина над списком, а вибір повертається.
 *
 * Лейаут — як вікно «Поділитися» у Figma: згори (з `ViewUser` і правом змінювати) поле пошуку колеги;
 * під ним підпис «У кого є доступ до проєкту», рядок доступності проєкту (іконка й назва; з правом
 * редагувати проєкт — зі стрілкою, що веде на «Редагувати», `editProject`) і учасники в блоці, що
 * прокручується. Поле додавання — `app-project-member-search`; його «Додати» — `addMember` (колесо
 * завантаження на час запитів, далі зелений текст про успіх під полем чи червоний блок невдач).
 */
@Component({
  selector: 'app-project-access',
  // Відкритий список колег лежить поверх усього нижче: вікно не нижче за його нижній край (16px —
  // відступ над полем), інакше прокрутка вікна обрізала б список. Короткий список вікно не змінює.
  host: { '[style.min-height.px]': 'overlayBottom() ? overlayBottom() + 16 : null' },
  templateUrl: './project-access.component.html',
  styleUrls: ['./project-access.component.scss'],
  imports: [AvatarComponent, IonIcon, IonSpinner, ProjectMemberSearchComponent, TranslatePipe],
})
export class ProjectAccessComponent {
  /** `id` проєкту. */
  readonly projectId = input.required<string>();
  /** Чи можна змінювати доступи (`UpdateProject`). */
  readonly editable = input(false);
  /** Доступність проєкту (рядок над учасниками); невідома — `null`, рядка нема. */
  readonly visibility = input<ProjectVisibility | null>(null);
  /** Чи можна змінити доступність тут же (стрілка в рядку веде на «Редагувати»). */
  readonly canEditProject = input(false);
  /** Натиснули рядок доступності: перейти до «Редагувати». */
  readonly editProject = output<void>();

  protected readonly visibilityIcons = PROJECT_VISIBILITY_ICONS;

  private readonly projects = inject(ProjectsService);
  private readonly currentUser = inject(CurrentUserService);
  private readonly translate = inject(TranslateService);
  private readonly accounts = inject(AccountsService);

  protected readonly claims = PROJECT_CLAIMS;
  protected readonly state = signal<MembersState>({ status: 'loading' });
  /** Учасник, зміна якого йде (його вибір і кнопка вимкнені). */
  protected readonly busyUserId = signal<string | null>(null);
  /** Невдача останньої зміни (ключ мовного файлу) або `null`. */
  protected readonly error = signal<string | null>(null);
  /** `id` поточного користувача: свій рядок не змінюється. */
  protected readonly selfId = computed(() => this.currentUser.user()?.id ?? null);
  /**
   * Поле додавання колеги (згори, як у Figma): лише з правом бачити колег (`ViewUser`) і змінювати
   * доступи (`editable`). Поки лише вигляд: пошуку й додавання ще нема.
   */
  protected readonly canAdd = computed(() => this.editable() && this.currentUser.permissions().includes(PERMISSIONS.viewUser));
  /** Назва організації: бере участь у кольорі аватарки (як у колег). */
  protected readonly ngoName = computed(() => this.accounts.active()?.ngoName ?? '');

  constructor() {
    effect(() => {
      const id = this.projectId();
      untracked(() => void this.load(id));
    });
  }

  /** Додаємо колег: замість вмісту вікна — колесо завантаження. */
  protected readonly adding = signal(false);
  /** Кого щойно додано (зелений текст під полем) і невдачі (червоний блок) останнього «Додати». */
  protected readonly addResult = signal<{ readonly added: string; readonly failures: readonly string[] } | null>(null);
  /** Поле додавання створюється заново після «Додати» (порожнє, без тегів). */
  protected readonly searchGeneration = signal(0);

  /** Де закінчується відкритий список колег поля додавання (px від верху поля; 0 — закритий). */
  protected readonly overlayBottom = signal(0);

  /**
   * «Додати»: вибраного колегу з рівнем і назвою ролі (`addMember`); поки запит іде — колесо
   * завантаження замість вікна. Далі учасники заново й поле порожнє: успіх — зеленим під полем,
   * невдача — червоним блоком із причиною.
   */
  protected async addMember(request: { readonly user: UserSummary; readonly claim: ProjectClaim; readonly roleTitle: string | null }): Promise<void> {
    this.adding.set(true);
    this.addResult.set(null);
    this.error.set(null);
    const { user } = request;
    const name = this.nameOf({ lastName: user.lastName, firstName: user.firstName, patronymic: user.patronymic }) || user.email;
    const result = await this.projects.addMember(this.projectId(), user.id, request.claim, request.roleTitle);
    const failures =
      result === 'done'
        ? []
        : [this.translate.instant('planner.access.addFailed', { name, reason: this.translate.instant(`planner.access.addFailure.${result}`) }) as string];
    await this.load(this.projectId());
    this.searchGeneration.update((generation) => generation + 1);
    this.addResult.set({ added: result === 'done' ? name : '', failures });
    this.adding.set(false);
  }

  /** `id` учасників: у списку колег поля додавання їх нема. */
  protected readonly memberIds = computed(() => {
    const state = this.state();
    return state.status === 'ready' ? state.members.map((member) => member.userId) : [];
  });

  /** ПІБ учасника (для доданого колеги — прізвище й імʼя): прізвище, імʼя, по батькові. */
  protected nameOf(member: Pick<ProjectMember, 'lastName' | 'firstName' | 'patronymic'>): string {
    return [member.lastName, member.firstName, member.patronymic]
      .map((part) => part?.trim())
      .filter(Boolean)
      .join(' ');
  }

  /** Чи можна змінювати цей рядок: є право й це не сам користувач. */
  protected canChange(member: ProjectMember): boolean {
    return this.editable() && member.userId !== this.selfId();
  }

  protected async changeClaim(member: ProjectMember, event: Event): Promise<void> {
    const select = event.target as HTMLSelectElement;
    const claim = Number(select.value) as ProjectClaim;
    const result = await this.run(member, () => this.projects.updateMember(this.projectId(), member, claim));
    if (result) {
      this.replace(member.userId, (current) => ({ ...current, claim }));
    } else {
      // Сервер не прийняв — у виборі знову попередній рівень.
      select.value = String(member.claim);
    }
  }

  protected async remove(member: ProjectMember): Promise<void> {
    if (await this.run(member, () => this.projects.removeMember(this.projectId(), member.userId))) {
      const state = this.state();
      if (state.status === 'ready') {
        this.state.set({ status: 'ready', members: state.members.filter((candidate) => candidate.userId !== member.userId) });
      }
    }
  }

  /** Виконує зміну учасника; успіх — `true`, невдача — причина над списком і `false`. */
  private async run(member: ProjectMember, request: () => Promise<string>): Promise<boolean> {
    this.busyUserId.set(member.userId);
    this.error.set(null);
    const result = await request();
    this.busyUserId.set(null);
    if (result === 'done') {
      return true;
    }
    this.error.set(`planner.access.failure.${result}`);
    return false;
  }

  private replace(userId: string, change: (member: ProjectMember) => ProjectMember): void {
    const state = this.state();
    if (state.status === 'ready') {
      this.state.set({ status: 'ready', members: state.members.map((member) => (member.userId === userId ? change(member) : member)) });
    }
  }

  private async load(id: string): Promise<void> {
    this.state.set({ status: 'loading' });
    this.error.set(null);
    const members = await this.projects.members(id);
    if (id !== this.projectId()) {
      return;
    }
    this.state.set(typeof members === 'string' ? { status: 'failed', errorKey: `planner.dialogs.failure.${members}` } : { status: 'ready', members });
  }
}
