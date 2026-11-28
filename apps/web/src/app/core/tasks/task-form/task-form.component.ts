import { Component, ElementRef, computed, inject, input, linkedSignal, output, signal, viewChild } from '@angular/core';
import { IonIcon } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

import { AvatarComponent } from '../../../shared/avatar/avatar.component';
import { AccountsService } from '../../accounts/accounts.service';
import { Colleague, ColleagueSearchComponent, ColleagueSource, colleagueName } from '../../projects/colleague-search/colleague-search.component';
import { DateRangeComponent, isDateRangeValid, today } from '../../projects/date-range/date-range.component';
import { TagInputComponent } from '../tag-input/tag-input.component';
import {
  NewTask,
  TASK_DESCRIPTION_MAX_LENGTH,
  TASK_PRIORITIES,
  TASK_STATUSES,
  TASK_TITLE_MAX_LENGTH,
  TaskPriority,
  TaskStatus,
} from '../tasks.service';

/** Дані, з якими форма відкривається (вікно «Редагувати»): завдання й виконавці з іменами. */
export type TaskFormInitial = Omit<NewTask, 'assigneeIds'> & { readonly assignees: readonly Colleague[] };

/**
 * Форма нового завдання (у вікні під `app-dialog-header`, як форма проєкту): «Назва» (обовʼязкова,
 * до 255 символів), «Опис» (до 10000), «Статус» (спершу «До виконання») і «Пріоритет» (спершу
 * «Середній») поруч, «Виконавці» — пошук колеги (`app-colleague-search`, колеги — `colleagues`; вибір
 * кількох: список не закривається, вибрані в ньому позначені й знімаються хрестиком), вибрані — ще й
 * під полем (аватар, ПІБ, хрестик «Прибрати»), «Теги» (`app-tag-input`: кома формує тег, підказки —
 * наявні теги проєкту `tagSuggestions`; без `allowNewTags` — лише з них) і дати (`app-date-range`: початок
 * обовʼязковий, спершу — сьогодні). Помилки полів — після першої спроби; без них форма віддає
 * значення (`submitted`). У вікні «Редагувати» поля заповнені даними завдання (`initial`), а кнопка —
 * «Зберегти» (`submitLabelKey`).
 */
@Component({
  selector: 'app-task-form',
  templateUrl: './task-form.component.html',
  styleUrls: ['../../../settings/settings-section.scss', './task-form.component.scss'],
  imports: [AvatarComponent, ColleagueSearchComponent, DateRangeComponent, IonIcon, TagInputComponent, TranslatePipe],
  // Відкритий список колег лежить поверх усього нижче: форма не нижча за нього (інакше вікно обріже список).
  host: { '[style.min-height.px]': 'overlayHeight()' },
})
export class TaskFormComponent {
  /** Звідки колеги для «Виконавців» (див. `ColleagueSource`). */
  readonly colleagues = input<ColleagueSource>(null);
  /** Дані, з якими форма відкривається (`null` — нове завдання). */
  readonly initial = input<TaskFormInitial | null>(null);
  /** Ключ підпису кнопки. */
  readonly submitLabelKey = input('planner.tasks.add.submit');
  /** Наявні теги проєкту (назви) — підказки поля тегів. */
  readonly tagSuggestions = input<readonly string[]>([]);
  /** Чи можна створювати нові теги (право `UpdateProject`); інакше — лише з наявних. */
  readonly allowNewTags = input(true);
  /** Запит іде: кнопка вимкнена. */
  readonly busy = input(false);
  /** Помилка попередньої спроби (ключ мовного файлу) або `null`. */
  readonly error = input<string | null>(null);

  readonly submitted = output<NewTask>();

  private readonly accounts = inject(AccountsService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly colleagueField = viewChild.required(ColleagueSearchComponent, { read: ElementRef });
  private readonly tagField = viewChild.required(TagInputComponent, { read: ElementRef });

  protected readonly titleMaxLength = TASK_TITLE_MAX_LENGTH;
  protected readonly descriptionMaxLength = TASK_DESCRIPTION_MAX_LENGTH;
  protected readonly statuses = TASK_STATUSES;
  protected readonly priorities = TASK_PRIORITIES;

  protected readonly title = linkedSignal(() => this.initial()?.title ?? '');
  protected readonly description = linkedSignal(() => this.initial()?.description ?? '');
  protected readonly status = linkedSignal<TaskStatus>(() => this.initial()?.status ?? 0);
  protected readonly priority = linkedSignal<TaskPriority>(() => this.initial()?.priority ?? 1);
  protected readonly assignees = linkedSignal<readonly Colleague[]>(() => this.initial()?.assignees ?? []);
  protected readonly assigneeIds = computed(() => this.assignees().map((colleague) => colleague.id));
  protected readonly tags = linkedSignal<readonly string[]>(() => this.initial()?.tags ?? []);
  protected readonly startDate = linkedSignal(() => this.initial()?.startDate ?? today());
  protected readonly endDate = linkedSignal(() => this.initial()?.endDate ?? '');
  protected readonly submittedOnce = signal(false);
  /** Мінімальна висота форми, поки відкритий список колег (`null` — без обмеження). */
  protected readonly overlayHeight = signal<number | null>(null);
  /** Назва організації: бере участь у кольорі аватарки (як у колег). */
  protected readonly ngoName = computed(() => this.accounts.active()?.ngoName ?? '');
  protected readonly nameOf = colleagueName;

  protected readonly titleError = computed(() => (this.submittedOnce() && !this.title().trim() ? 'common.required' : null));

  protected onText(target: typeof this.title, event: Event): void {
    target.set((event.target as HTMLInputElement | HTMLTextAreaElement).value);
  }

  protected onStatus(event: Event): void {
    this.status.set(Number((event.target as HTMLSelectElement).value) as TaskStatus);
  }

  protected onPriority(event: Event): void {
    this.priority.set(Number((event.target as HTMLSelectElement).value) as TaskPriority);
  }

  /** Де закінчується список під полем (від верху поля: колеги чи підказки тегів) — форма має сягати щонайменше туди. */
  protected onOverlay(bottom: number, field: 'colleagues' | 'tags'): void {
    const element = (field === 'colleagues' ? this.colleagueField() : this.tagField()).nativeElement as HTMLElement;
    const top = element.getBoundingClientRect().top - this.host.getBoundingClientRect().top;
    this.overlayHeight.set(bottom ? top + bottom + 8 : null);
  }

  protected addAssignee(colleague: Colleague): void {
    this.assignees.update((list) => [...list, colleague]);
  }

  protected removeAssignee(colleague: Colleague): void {
    this.assignees.update((list) => list.filter((candidate) => candidate.id !== colleague.id));
  }

  protected submit(): void {
    this.submittedOnce.set(true);
    if (this.busy() || this.titleError() || !isDateRangeValid(this.startDate(), this.endDate())) {
      return;
    }
    this.submitted.emit({
      title: this.title().trim(),
      description: this.description().trim() || null,
      status: this.status(),
      priority: this.priority(),
      assigneeIds: this.assigneeIds(),
      tags: this.tags(),
      startDate: this.startDate(),
      endDate: this.endDate() || null,
    });
  }
}
