import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, Signal, inject, signal } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';

import { CreateTaskCommand, CreateTaskCommentCommand, UpdateTaskCommentCommand, TagDto, TaskAssigneeDto, TaskCommentDto, TaskSummaryDto, UpdateTaskCommand } from '../../api/generated/models';
import { ConnectionService } from '../connection/connection.service';
import { Gone, isGone } from '../custom-tables/gone';
import { readPagedItems } from '../custom-tables/paged';
import { Untrusted } from '../http/api-contract';
import { RequestFailure, toRequestFailure } from '../http/request-failure';
import { REQUEST_TIMEOUT_MS } from '../http/timeouts';
import { ProjectTag, dayOf, utcDay } from '../projects/projects.service';


/** Найдовша назва завдання (`TaskValidationConstants.MaxTitleLength`). */
export const TASK_TITLE_MAX_LENGTH = 255;
/** Найдовший коментар (`TaskValidationConstants.MaxCommentContentLength`). */
export const TASK_COMMENT_MAX_LENGTH = 5000;
/** Найдовший опис завдання (`TaskValidationConstants.MaxDescriptionLength`). */
export const TASK_DESCRIPTION_MAX_LENGTH = 10000;

/**
 * Стан завдання (`ProjectTaskStatus`): 0 — до виконання, 1 — в роботі, 2 — заблоковано, 3 — виконано
 * (чекає на перевірку), 4 — завершено (сервер сам ставить дату кінця, якщо її нема).
 */
export const TASK_STATUSES = [0, 1, 2, 3, 4] as const;
export type TaskStatus = (typeof TASK_STATUSES)[number];

/** Пріоритет завдання (`TaskPriority`): 0 — низький, 1 — середній, 2 — високий, 3 — критичний. */
export const TASK_PRIORITIES = [0, 1, 2, 3] as const;
export type TaskPriority = (typeof TASK_PRIORITIES)[number];

/** Завдання з форми; дні — `YYYY-MM-DD`, без кінця — `null`; теги — назвами (нові ще не мають `id`). */
export interface NewTask {
  readonly title: string;
  readonly description: string | null;
  readonly status: TaskStatus;
  readonly priority: TaskPriority;
  readonly assigneeIds: readonly string[];
  readonly tags: readonly string[];
  readonly startDate: string;
  readonly endDate: string | null;
}

/** Що запитати в списку завдань: лише свої, сортування (`OrderBy` — поле DTO, `null` — порядок сервера) і пошук. */
export interface TasksQuery {
  readonly mine?: boolean;
  /** Лише завдання колонки (`TopicId`). */
  readonly topicId?: string;
  readonly orderBy?: string | null;
  readonly sortOrder?: 'asc' | 'desc';
  readonly search?: string | null;
}

/** Коментар до завдання: автор (видаленого — без імені) і час (`createdAt` — ISO з сервера). */
export interface TaskComment {
  readonly id: string;
  readonly userId: string | null;
  readonly firstName: string;
  readonly lastName: string;
  readonly avatarUrl: string | null;
  readonly content: string;
  readonly createdAt: string;
}

/** Виконавець завдання. */
export interface TaskAssignee {
  readonly id: string;
  readonly firstName: string;
  readonly lastName: string;
  readonly patronymic: string | null;
  readonly avatarUrl: string | null;
}

/** Завдання на дошці; дні — `YYYY-MM-DD` за UTC, без кінця — `null`. */
export interface ProjectTask {
  readonly id: string;
  readonly topicId: string;
  readonly title: string;
  readonly description: string | null;
  readonly status: TaskStatus;
  readonly priority: TaskPriority;
  readonly startDate: string;
  readonly endDate: string | null;
  readonly assignees: readonly TaskAssignee[];
  readonly tags: readonly ProjectTag[];
  /** Непрочитаних коментарів (усіх сервер у списку не віддає). */
  readonly unreadComments: number;
}

/** Завдання проєктів (дошка планера). */
@Injectable({ providedIn: 'root' })
export class TasksService {
  private readonly http = inject(HttpClient);
  private readonly connection = inject(ConnectionService);

  private readonly versionSignal = signal(0);
  /** Лічильник змін завдань: хто показує завдання, перепитує їх. */
  readonly version: Signal<number> = this.versionSignal.asReadonly();

  /**
   * Одна сторінка `GET /api/v1/projects/{id}/tasks` (до 100 за запит; наступні — при прокрутці):
   * `query.mine` — лише свої (`Filter=mine`; сервер тоді спершу ставить ті, де більше нових
   * коментарів, далі — за найближчою датою), `topicId` — лише колонки, `orderBy`/`sortOrder` —
   * сортування, `search` — `SearchTerm`. Без сортування — порядок сервера (за датою початку). Завдання не за контрактом
   * пропускається; проєкту вже нема — `gone`.
   */
  async page(
    projectId: string,
    page: number,
    pageSize: number,
    query: TasksQuery = {},
  ): Promise<{ readonly items: readonly ProjectTask[]; readonly hasNextPage: boolean } | RequestFailure | Gone> {
    let params = new HttpParams().set('PageNumber', page).set('PageSize', pageSize);
    if (query.mine) {
      params = params.set('Filter', 'mine');
    }
    if (query.topicId) {
      params = params.set('TopicId', query.topicId);
    }
    if (query.orderBy) {
      params = params.set('OrderBy', query.orderBy).set('SortOrder', query.sortOrder ?? 'asc');
    }
    if (query.search?.trim()) {
      params = params.set('SearchTerm', query.search.trim());
    }
    try {
      const body = await firstValueFrom(this.http.get<unknown>(this.url(projectId), { params }).pipe(timeout(REQUEST_TIMEOUT_MS)));
      return readPagedItems(body, page, toTask);
    } catch (error) {
      if (isGone(error)) {
        return 'gone';
      }
      const failure = toRequestFailure(error);
      console.warn(`[TasksService] Не вдалося завантажити завдання (${failure})`, error);
      return failure;
    }
  }

  /**
   * `POST /api/v1/projects/{id}/tasks` (право `CreateTask` і «Редагування» в проєкті): завдання в
   * колонці `topicId` з тегами `tagIds` (назви вже перетворено на `id` — `ProjectsService.tagIds`);
   * дні — початок дня за UTC, як у проєктів. Посилань і метаданих форма ще не має. Виконавця, якого сервер не приймає (вимкнений акаунт, не учасник приватного проєкту), —
   * 400 (`invalid`); проєкту, колонки чи колеги вже нема — `gone` (404). Повертає `id` нового завдання.
   */
  async create(projectId: string, topicId: string, task: NewTask, tagIds: readonly string[]): Promise<{ readonly id: string } | RequestFailure | Gone> {
    const body = {
      topicId,
      title: task.title,
      description: task.description,
      status: task.status,
      priority: task.priority,
      assigneeIds: [...task.assigneeIds],
      tagIds: [...tagIds],
      startDate: utcDay(task.startDate),
      endDate: task.endDate ? utcDay(task.endDate) : null,
    } satisfies CreateTaskCommand;
    try {
      const id = await firstValueFrom(
        this.http.post<unknown>(this.url(projectId), body).pipe(timeout(REQUEST_TIMEOUT_MS)),
      );
      this.versionSignal.update((version) => version + 1);
      return { id: typeof id === 'string' ? id : '' };
    } catch (error) {
      if (isGone(error)) {
        return 'gone';
      }
      const failure = toRequestFailure(error);
      console.warn(`[TasksService] Не вдалося створити завдання (${failure})`, error);
      return failure;
    }
  }

  /**
   * `PUT /api/v1/projects/{id}/tasks/{taskId}` (право `UpdateTask` і «Редагування» в проєкті): назва,
   * опис, стан, пріоритет, виконавці, теги (`tagIds`: сервер лишає в завданні рівно їх — нові
   * додає, зайві прибирає) й дати (як у `create`), колонка та сама. Посилання й метадані — `null`:
   * сервер їх тоді не чіпає (форма їх ще не має). Завдання, проєкту, колонки чи
   * колеги вже нема — `gone` (404); виконавця, якого сервер не приймає, — `invalid` (400).
   */
  async update(projectId: string, task: ProjectTask, changes: NewTask, tagIds: readonly string[]): Promise<'done' | RequestFailure | Gone> {
    const body = {
      topicId: task.topicId,
      title: changes.title,
      description: changes.description,
      status: changes.status,
      priority: changes.priority,
      assigneeIds: [...changes.assigneeIds],
      tagIds: [...tagIds],
      externalLinks: null,
      metadata: null,
      startDate: utcDay(changes.startDate),
      endDate: changes.endDate ? utcDay(changes.endDate) : null,
    } satisfies UpdateTaskCommand;
    try {
      await firstValueFrom(this.http.put(`${this.url(projectId)}/${encodeURIComponent(task.id)}`, body).pipe(timeout(REQUEST_TIMEOUT_MS)));
      this.versionSignal.update((version) => version + 1);
      return 'done';
    } catch (error) {
      if (isGone(error)) {
        this.versionSignal.update((version) => version + 1);
        return 'gone';
      }
      const failure = toRequestFailure(error);
      console.warn(`[TasksService] Не вдалося змінити завдання (${failure})`, error);
      return failure;
    }
  }

  /**
   * `DELETE /api/v1/projects/{id}/tasks/{taskId}` (право `DeleteTask` і «Редагування» в проєкті):
   * завдання разом із вкладеннями. Його вже нема (404) — теж `done`. Успіх — списки перечитуються.
   */
  async remove(projectId: string, taskId: string): Promise<'done' | RequestFailure> {
    try {
      await firstValueFrom(this.http.delete(`${this.url(projectId)}/${encodeURIComponent(taskId)}`).pipe(timeout(REQUEST_TIMEOUT_MS)));
    } catch (error) {
      if (!isGone(error)) {
        const failure = toRequestFailure(error);
        console.warn(`[TasksService] Не вдалося видалити завдання (${failure})`, error);
        return failure;
      }
    }
    this.versionSignal.update((version) => version + 1);
    return 'done';
  }

  /** `GET /api/v1/projects/{id}/tasks/{taskId}`: свіже завдання (бічне вікно); нема — `gone`. */
  async get(projectId: string, taskId: string): Promise<ProjectTask | RequestFailure | Gone> {
    try {
      const body = await firstValueFrom(this.http.get<unknown>(`${this.url(projectId)}/${encodeURIComponent(taskId)}`).pipe(timeout(REQUEST_TIMEOUT_MS)));
      return toTask(body as Untrusted<TaskSummaryDto>)[0] ?? 'error';
    } catch (error) {
      return this.failure(error, 'завантажити завдання');
    }
  }

  /**
   * Сторінка коментарів `GET …/tasks/{taskId}/comments` (право `ViewTask`), від давніших до новіших;
   * наступні — при прокрутці. Коментар не за контрактом пропускається.
   */
  async comments(
    projectId: string,
    taskId: string,
    page: number,
    pageSize: number,
  ): Promise<{ readonly items: readonly TaskComment[]; readonly hasNextPage: boolean } | RequestFailure | Gone> {
    const params = new HttpParams().set('PageNumber', page).set('PageSize', pageSize);
    try {
      const body = await firstValueFrom(this.http.get<unknown>(this.commentsUrl(projectId, taskId), { params }).pipe(timeout(REQUEST_TIMEOUT_MS)));
      return readPagedItems(body, page, toComment);
    } catch (error) {
      return this.failure(error, 'завантажити коментарі');
    }
  }

  /**
   * `POST …/tasks/{taskId}/comments/read`: усі коментарі завдання прочитані користувачем. Успіх —
   * списки завдань перечитуються (зникає червоний кружок нових коментарів).
   */
  async markCommentsRead(projectId: string, taskId: string): Promise<'done' | RequestFailure | Gone> {
    try {
      await firstValueFrom(this.http.post(`${this.commentsUrl(projectId, taskId)}/read`, null).pipe(timeout(REQUEST_TIMEOUT_MS)));
      this.versionSignal.update((version) => version + 1);
      return 'done';
    } catch (error) {
      return this.failure(error, 'позначити коментарі прочитаними');
    }
  }

  /** `POST …/tasks/{taskId}/comments` (право `ViewTask`): новий коментар; повертає його `id`. */
  async addComment(projectId: string, taskId: string, content: string): Promise<{ readonly id: string } | RequestFailure | Gone> {
    try {
      const body = { content } satisfies CreateTaskCommentCommand;
      const id = await firstValueFrom(this.http.post<unknown>(this.commentsUrl(projectId, taskId), body).pipe(timeout(REQUEST_TIMEOUT_MS)));
      return { id: typeof id === 'string' ? id : '' };
    } catch (error) {
      return this.failure(error, 'надіслати коментар');
    }
  }

  /** `PUT …/comments/{commentId}`: новий текст свого коментаря. */
  async updateComment(projectId: string, taskId: string, commentId: string, content: string): Promise<'done' | RequestFailure | Gone> {
    try {
      const body = { content } satisfies UpdateTaskCommentCommand;
      await firstValueFrom(
        this.http.put(`${this.commentsUrl(projectId, taskId)}/${encodeURIComponent(commentId)}`, body).pipe(timeout(REQUEST_TIMEOUT_MS)),
      );
      return 'done';
    } catch (error) {
      return this.failure(error, 'змінити коментар');
    }
  }

  /** `DELETE …/comments/{commentId}`: свій коментар; його вже нема (404) — теж `done`. */
  async removeComment(projectId: string, taskId: string, commentId: string): Promise<'done' | RequestFailure> {
    try {
      await firstValueFrom(this.http.delete(`${this.commentsUrl(projectId, taskId)}/${encodeURIComponent(commentId)}`).pipe(timeout(REQUEST_TIMEOUT_MS)));
      return 'done';
    } catch (error) {
      const failure = this.failure(error, 'видалити коментар');
      return failure === 'gone' ? 'done' : failure;
    }
  }

  private commentsUrl(projectId: string, taskId: string): string {
    return `${this.url(projectId)}/${encodeURIComponent(taskId)}/comments`;
  }

  /** Причина невдачі запиту `what`; 404 — `gone`. */
  private failure(error: unknown, what: string): RequestFailure | Gone {
    if (isGone(error)) {
      return 'gone';
    }
    const failure = toRequestFailure(error);
    console.warn(`[TasksService] Не вдалося ${what} (${failure})`, error);
    return failure;
  }

  private url(projectId: string): string {
    return `${this.connection.serverUrl()}/api/v1/projects/${encodeURIComponent(projectId)}/tasks`;
  }
}

/** Виконавець із відповіді сервера; без `id` чи імені — пропускається. */
function toAssignee(value: Untrusted<TaskAssigneeDto>): TaskAssignee[] {
  const id = value?.id;
  const firstName = typeof value?.firstName === 'string' ? value.firstName : '';
  const lastName = typeof value?.lastName === 'string' ? value.lastName : '';
  if (typeof id !== 'string' || !id || !(firstName || lastName)) {
    return [];
  }
  return [
    {
      id,
      firstName,
      lastName,
      patronymic: typeof value?.patronymic === 'string' ? value.patronymic : null,
      avatarUrl: typeof value?.avatarUrl === 'string' ? value.avatarUrl : null,
    },
  ];
}

/** Коментар з відповіді сервера; без `id`, тексту чи часу — пропускається. */
function toComment(value: Untrusted<TaskCommentDto>): TaskComment[] {
  const id = value?.id;
  const content = value?.content;
  const createdAt = value?.createdAt;
  if (typeof id !== 'string' || !id || typeof content !== 'string' || typeof createdAt !== 'string' || Number.isNaN(new Date(createdAt).getTime())) {
    return [];
  }
  const text = (field: unknown) => (typeof field === 'string' ? field : '');
  return [
    {
      id,
      userId: typeof value?.userId === 'string' ? value.userId : null,
      firstName: text(value?.authorFirstName),
      lastName: text(value?.authorLastName),
      avatarUrl: typeof value?.authorAvatarUrl === 'string' ? value.authorAvatarUrl : null,
      content,
      createdAt,
    },
  ];
}

/** Тег завдання з відповіді сервера; без `id` чи назви — пропускається. */
function toTag(value: Untrusted<TagDto>): ProjectTag[] {
  const id = value?.id;
  const title = value?.title;
  return typeof id === 'string' && id && typeof title === 'string' && title.trim() ? [{ id, title }] : [];
}

/** Завдання з відповіді сервера; без `id`, колонки, назви чи дати початку, з невідомим станом чи пріоритетом — пропускається. */
function toTask(value: Untrusted<TaskSummaryDto>): ProjectTask[] {
  const id = value?.id;
  const topicId = value?.topicId;
  const title = value?.title;
  const status = TASK_STATUSES.find((candidate) => candidate === Number(value?.status));
  const priority = TASK_PRIORITIES.find((candidate) => candidate === Number(value?.priority));
  const startDate = dayOf(value?.startDate);
  if (typeof id !== 'string' || !id || typeof topicId !== 'string' || typeof title !== 'string' || status === undefined || priority === undefined || !startDate) {
    return [];
  }
  const unread = Number(value?.unreadCommentsCount);
  return [
    {
      id,
      topicId,
      title,
      description: typeof value?.description === 'string' && value.description.trim() ? value.description : null,
      status,
      priority,
      startDate,
      endDate: dayOf(value?.endDate),
      assignees: Array.isArray(value?.assignedUsers) ? value.assignedUsers.flatMap(toAssignee) : [],
      tags: Array.isArray(value?.tags) ? value.tags.flatMap(toTag) : [],
      unreadComments: Number.isInteger(unread) && unread > 0 ? unread : 0,
    },
  ];
}
