import { Component, ElementRef, computed, effect, inject, input, signal, untracked, viewChild } from '@angular/core';
import { IonIcon } from '@ionic/angular';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

import { AccountsService } from '../../core/accounts/accounts.service';
import { PERMISSIONS } from '../../core/auth/permissions';
import { LanguageService } from '../../core/i18n/language.service';
import { ProjectsService } from '../../core/projects/projects.service';
import { TaskAccessService } from '../../core/tasks/task-access.service';
import { ProjectTask, TASK_COMMENT_MAX_LENGTH, TaskComment, TasksService } from '../../core/tasks/tasks.service';
import { CurrentUserService } from '../../core/users/current-user.service';
import { ActionListComponent, ActionListItem } from '../../shared/action-list/action-list.component';
import { PopoverComponent } from '../../shared/popover/popover.component';
import { roleColor } from '../../shared/role-pill/role-color';
import { SidePanelComponent } from '../../shared/side-panel/side-panel.component';
import { formatDay } from '../planner-board/task-card/task-card.component';
import { TaskDialogComponent } from '../planner-board/task-dialog.component';
import { TaskMenuComponent } from '../planner-board/task-menu.component';
import { TaskAssigneesComponent } from '../task-assignees/task-assignees.component';
import { TaskCommentComponent } from '../task-comment/task-comment.component';
import { TaskStatusComponent } from '../task-status/task-status.component';
import { relativeTime } from './relative-time';

/** Скільки коментарів за запит (наступні — при прокрутці). */
export const TASK_COMMENTS_PAGE_SIZE = 20;

/** Дії зі своїм коментарем. */
type CommentAction = 'edit' | 'delete';

const COMMENT_ITEMS: readonly ActionListItem<CommentAction>[] = [
  { id: 'edit', labelKey: 'planner.details.commentActions.edit', icon: 'create-outline' },
  { id: 'delete', labelKey: 'planner.details.commentActions.delete', icon: 'trash-outline' },
];

/** Коментарі: завантажуються, є (і чи є ще сторінки) чи не вдалося. */
interface CommentsState {
  readonly status: 'loading' | 'ready' | 'failed';
  readonly comments: readonly TaskComment[];
  readonly hasMore: boolean;
}

/**
 * Бічне вікно завдання (`app-side-panel`; відкривається `open(task)` — клік по картці чи рядку,
 * «Детальніше»). У шапці, на рівні хрестика, — тег проєкту (колір — як у кружка проєкту в бічному
 * меню, за назвою) і три крапки — те саме меню, що й на картці. Далі назва, тег стану
 * (`app-task-status`, з `UpdateTask` — підвищення), період, пріоритет, опис (якщо є), задіяні колеги,
 * теги й коментарі: `TasksService.comments` від давніших до новіших, по `TASK_COMMENTS_PAGE_SIZE`,
 * наступні — коли список докрутили до кінця (автор — «Прізвище Імʼя», текст — тонким шрифтом); під
 * ними — поле нового коментаря (Enter чи кнопка — `addComment`, до 5000 символів). У своїх
 * коментарях при наведенні — три крапки: «Редагувати» (текст стає полем, а замість трьох крапок —
 * завжди видима кнопка «Надіслати»; після збереження коментар знову звичайний) і «Видалити» (без
 * перепитування). Відкриття позначає коментарі прочитаними (`markCommentsRead`).
 * Після змін завдань (`TasksService.version`) завдання перечитується; його вже нема — вікно
 * закривається.
 */
@Component({
  selector: 'app-task-details',
  templateUrl: './task-details.component.html',
  styleUrls: ['../task-labels.scss', './task-details.component.scss'],
  imports: [ActionListComponent, IonIcon, PopoverComponent, SidePanelComponent, TaskCommentComponent, TaskAssigneesComponent, TaskDialogComponent, TaskMenuComponent, TaskStatusComponent, TranslatePipe],
})
export class TaskDetailsComponent {
  /** Проєкт, чиї завдання відкриваються. */
  readonly projectId = input.required<string>();

  private readonly tasks = inject(TasksService);
  private readonly projects = inject(ProjectsService);
  private readonly currentUser = inject(CurrentUserService);
  private readonly accounts = inject(AccountsService);
  private readonly translate = inject(TranslateService);
  private readonly language = inject(LanguageService).language;
  private readonly panel = viewChild.required(SidePanelComponent);
  private readonly sentinel = viewChild<ElementRef<HTMLElement>>('sentinel');

  protected readonly task = signal<ProjectTask | null>(null);
  protected readonly comments = signal<CommentsState>({ status: 'loading', comments: [], hasMore: false });
  protected readonly draft = signal('');
  protected readonly sending = signal(false);
  protected readonly sendError = signal<string | null>(null);
  protected readonly commentMaxLength = TASK_COMMENT_MAX_LENGTH;
  protected readonly commentItems = COMMENT_ITEMS;
  private readonly commentMenu = viewChild.required<PopoverComponent>('commentMenu');
  /** Коментар, чиє меню відкрите. */
  private menuComment: TaskComment | null = null;
  /** Свій коментар, що редагується. */
  protected readonly editing = signal<{ readonly id: string } | null>(null);
  /** Запит зі своїм коментарем іде (`id`). */
  protected readonly commentBusy = signal<string | null>(null);
  /** Помилка дії зі своїм коментарем (ключ мовного файлу). */
  protected readonly commentError = signal<string | null>(null);
  protected readonly selfId = computed(() => this.currentUser.user()?.id ?? null);

  private readonly taskAccess = inject(TaskAccessService);
  /** Зміна (меню, підвищення стану) й видалення — право ролі й доступ у проєкті (не переглядач). */
  protected readonly canEdit = computed(() => this.taskAccess.canEdit(this.projectId()));
  protected readonly canDelete = computed(() => this.taskAccess.canDelete(this.projectId()));
  protected readonly ngoName = computed(() => this.accounts.active()?.ngoName ?? '');
  /** Назва й колір проєкту (тег у шапці). */
  protected readonly project = computed(() => {
    const id = this.projectId();
    const title = this.projects.state().projects.find((project) => project.id === id)?.title ?? '';
    return { title, color: title ? roleColor(title) : null };
  });
  protected readonly period = computed(() => {
    const task = this.task();
    if (!task) {
      return null;
    }
    const start = formatDay(task.startDate);
    return task.endDate ? { key: 'planner.task.dates', start, end: formatDay(task.endDate) } : { key: 'planner.task.from', start, end: '' };
  });

  private nextPage = 1;
  private loadingComments = false;
  private generation = 0;

  constructor() {
    // Завдання змінилося (стан, редагування) — перечитуємо відкрите.
    effect(() => {
      this.tasks.version();
      untracked(() => void this.refresh());
    });
    // Кінець списку коментарів видно — наступна сторінка.
    effect((onCleanup) => {
      const sentinel = this.sentinel()?.nativeElement;
      if (!sentinel || typeof IntersectionObserver !== 'function') {
        return;
      }
      const observer = new IntersectionObserver((entries) => {
        if (entries.some((entry) => entry.isIntersecting) && this.comments().hasMore) {
          void this.loadComments();
        }
      });
      observer.observe(sentinel);
      onCleanup(() => observer.disconnect());
    });
  }

  /** Відкриває вікно завдання: коментарі з першої сторінки, усі — прочитані. */
  open(task: ProjectTask): void {
    this.task.set(task);
    this.draft.set('');
    this.editing.set(null);
    this.commentError.set(null);
    this.sendError.set(null);
    this.generation++;
    this.loadingComments = false;
    this.nextPage = 1;
    this.comments.set({ status: 'loading', comments: [], hasMore: false });
    this.panel().open();
    void this.loadComments();
    if (task.unreadComments) {
      void this.tasks.markCommentsRead(this.projectId(), task.id);
    }
  }

  protected onClosed(): void {
    this.task.set(null);
    this.generation++;
  }

  protected close(): void {
    this.panel().close();
  }

  /** Автор коментаря: «Прізвище Імʼя» (видалений акаунт — «Колега»). */
  protected authorOf(comment: TaskComment): string {
    return `${comment.lastName.trim()} ${comment.firstName.trim()}`.trim() || (this.translate.instant('planner.details.unknownAuthor') as string);
  }

  protected openCommentMenu(event: Event, comment: TaskComment): void {
    this.menuComment = comment;
    this.commentMenu().open(event);
  }

  protected async onCommentAction(action: CommentAction): Promise<void> {
    const comment = this.menuComment;
    await this.commentMenu().close();
    if (!comment) {
      return;
    }
    this.commentError.set(null);
    if (action === 'edit') {
      this.editing.set({ id: comment.id });
    } else {
      await this.removeComment(comment);
    }
  }

  /** «Надіслати» в редагуванні: новий текст; після збереження коментар знову звичайний. */
  protected async saveComment(comment: TaskComment, content: string): Promise<void> {
    const task = this.task();
    const text = content.trim();
    if (!task || !text || this.commentBusy()) {
      return;
    }
    if (text === comment.content) {
      this.editing.set(null);
      return;
    }
    this.commentBusy.set(comment.id);
    const result = await this.tasks.updateComment(this.projectId(), task.id, comment.id, text);
    this.commentBusy.set(null);
    if (result === 'done') {
      this.editing.set(null);
      this.replaceComments((comments) => comments.map((item) => (item.id === comment.id ? { ...item, content: text } : item)));
    } else {
      this.commentError.set(`planner.details.sendFailure.${result}`);
    }
  }

  /** «Видалити» свій коментар — без перепитування. */
  private async removeComment(comment: TaskComment): Promise<void> {
    const task = this.task();
    if (!task) {
      return;
    }
    this.commentBusy.set(comment.id);
    const result = await this.tasks.removeComment(this.projectId(), task.id, comment.id);
    this.commentBusy.set(null);
    if (result === 'done') {
      this.replaceComments((comments) => comments.filter((item) => item.id !== comment.id));
    } else {
      this.commentError.set(`planner.details.sendFailure.${result}`);
    }
  }

  private replaceComments(change: (comments: readonly TaskComment[]) => readonly TaskComment[]): void {
    this.comments.update((state) => ({ ...state, comments: change(state.comments) }));
  }

  protected timeOf(comment: TaskComment): string {
    return relativeTime(comment.createdAt, this.language(), this.translate.instant('planner.details.justNow') as string);
  }

  protected onDraft(event: Event): void {
    this.draft.set((event.target as HTMLInputElement).value);
  }

  /** Новий коментар; якщо всі сторінки вже показано — він одразу в кінці списку. */
  protected async send(): Promise<void> {
    const task = this.task();
    const content = this.draft().trim();
    if (!task || !content || this.sending()) {
      return;
    }
    this.sending.set(true);
    this.sendError.set(null);
    const result = await this.tasks.addComment(this.projectId(), task.id, content);
    this.sending.set(false);
    if (typeof result === 'string') {
      this.sendError.set(`planner.details.sendFailure.${result}`);
      return;
    }
    this.draft.set('');
    const state = this.comments();
    if (!state.hasMore && this.task()?.id === task.id) {
      const user = this.currentUser.user();
      this.comments.set({
        ...state,
        comments: [
          ...state.comments,
          {
            id: result.id || `local-${Date.now()}`,
            userId: user?.id ?? null,
            firstName: user?.firstName ?? '',
            lastName: user?.lastName ?? '',
            avatarUrl: user?.avatarUrl ?? null,
            content,
            createdAt: new Date().toISOString(),
          },
        ],
      });
    }
  }

  protected async loadComments(): Promise<void> {
    const task = this.task();
    if (!task || this.loadingComments) {
      return;
    }
    this.loadingComments = true;
    const generation = this.generation;
    const result = await this.tasks.comments(this.projectId(), task.id, this.nextPage, TASK_COMMENTS_PAGE_SIZE);
    if (generation !== this.generation) {
      return;
    }
    this.loadingComments = false;
    const before = this.comments();
    if (typeof result === 'string') {
      this.comments.set({ ...before, status: 'failed', hasMore: false });
      return;
    }
    this.nextPage++;
    const known = new Set(before.comments.map((comment) => comment.id));
    this.comments.set({ status: 'ready', comments: [...before.comments, ...result.items.filter((comment) => !known.has(comment.id))], hasMore: result.hasNextPage });
  }

  /** Перечитує відкрите завдання; його вже нема — вікно закривається. */
  private async refresh(): Promise<void> {
    const task = this.task();
    if (!task) {
      return;
    }
    const fresh = await this.tasks.get(this.projectId(), task.id);
    if (this.task()?.id !== task.id) {
      return;
    }
    if (fresh === 'gone') {
      this.close();
    } else if (typeof fresh !== 'string') {
      this.task.set(fresh);
    }
  }
}
