import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, PendingTasks, Signal, computed, effect, inject, signal, untracked } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';

import {
  AssignProjectMemberCommand,
  CreateProjectCommand,
  CreateTagCommand,
  CreateTopicCommand,
  PagedResultOfTopicDto,
  PagedResultOfProjectSummaryDto,
  ProjectDto,
  ProjectMemberDto,
  ProjectSummaryDto,
  UpdateProjectCommand,
  UpdateProjectMemberCommand,
  UpdateTopicCommand,
} from '../../api/generated/models';
import { Gone, isGone } from '../custom-tables/gone';
import { AccountsService } from '../accounts/accounts.service';
import { AuthService } from '../auth/auth.service';
import { PERMISSIONS } from '../auth/permissions';
import { ConnectionService } from '../connection/connection.service';
import { readPagedItems } from '../custom-tables/paged';
import { Untrusted } from '../http/api-contract';
import { RequestFailure, toRequestFailure } from '../http/request-failure';
import { REQUEST_TIMEOUT_MS } from '../http/timeouts';
import { CurrentUserService } from '../users/current-user.service';

const API_PROJECTS_PATH = '/api/v1/projects';
/** Скільки проєктів за запит (більше сервер не віддає). */
const PROJECTS_PAGE_SIZE = 100;
/** Найдовша назва проєкту, яку прийме сервер (`ProjectValidationConstants.MaxTitleLength`). */
export const PROJECT_TITLE_MAX_LENGTH = 255;
/** Найдовший опис проєкту (`ProjectValidationConstants.MaxDescriptionLength`). */
export const PROJECT_DESCRIPTION_MAX_LENGTH = 2000;

/**
 * Доступність проєкту (`ProjectVisibility` сервера, числами): приватний (бачать і редагують лише
 * учасники з відповідним доступом), публічний перегляд (бачать усі в організації, редагують
 * учасники) і публічний (бачать і редагують усі в організації).
 */
export const PROJECT_VISIBILITIES = [0, 1, 2] as const;
export type ProjectVisibility = (typeof PROJECT_VISIBILITIES)[number];

/** Іконка доступності: приватний — замок, публічний перегляд — око, публічний — глобус. */
export const PROJECT_VISIBILITY_ICONS: Readonly<Record<ProjectVisibility, string>> = {
  0: 'lock-closed-outline',
  1: 'eye-outline',
  2: 'globe-outline',
};

/** Дані проєкту для форми (новий чи змінений): дати — `YYYY-MM-DD` (поле дати), кінця може не бути. */
export interface NewProject {
  readonly title: string;
  readonly description: string | null;
  readonly visibility: ProjectVisibility;
  readonly startDate: string;
  readonly endDate: string | null;
}

/** Адреса планера проєкту (його `id` приходить у сторінку входом `id`). */
export function projectLink(id: string): string {
  return `/planner/projects/${encodeURIComponent(id)}`;
}

/**
 * Дія з проєктом у вікні дій: редагувати, доступи, видалити (меню трьох крапок) і нова колонка
 * дошки («Створити» на «Дошці»).
 */
export type ProjectAction = 'edit' | 'access' | 'delete' | 'topic';

/**
 * Найдовша назва колонки дошки. Сервер приймає до 255 (`ProjectValidationConstants.MaxTopicTitleLength`),
 * але в заголовок колонки (два рядки) більше не вміщається.
 */
export const TOPIC_TITLE_MAX_LENGTH = 32;

/** Найдовша назва тегу (`ProjectValidationConstants.MaxTagTitleLength`). */
export const TAG_TITLE_MAX_LENGTH = 100;

/** Тег проєкту: `id` і назва. */
export interface ProjectTag {
  readonly id: string;
  readonly title: string;
}

/** Назва тегу для порівняння: сервер вважає однаковими назви, що різняться лише регістром і пробілами по краях. */
export const tagKey = (title: string) => title.trim().toLocaleLowerCase();

/** Запит на вікно дії з проєктом. */
export interface ProjectActionRequest {
  readonly action: ProjectAction;
  readonly project: ProjectSummary;
}

/**
 * Рівень доступу учасника в проєкті (`ProjectClaim` сервера, числами): 0 — переглядач (бачить і
 * коментує), 1 — редактор (ще й змінює задачі, теми, теги), 2 — керівник (ще й доступність,
 * учасники, видалення проєкту).
 */
export const PROJECT_CLAIMS = [0, 1, 2] as const;
export type ProjectClaim = (typeof PROJECT_CLAIMS)[number];

/** Колонка (тема) дошки проєкту: `id`, назва й позиція. */
export interface ProjectTopic {
  readonly id: string;
  readonly title: string;
  readonly order: number;
}

/** Учасник проєкту: користувач, його роль у проєкті (назва), рівень доступу й чи активний акаунт. */
export interface ProjectMember {
  readonly userId: string;
  readonly firstName: string;
  readonly lastName: string;
  readonly patronymic: string | null;
  readonly avatarUrl: string | null;
  readonly roleTitle: string | null;
  readonly claim: ProjectClaim;
  readonly isActive: boolean;
}

/** Проєкт у списку: `id`, назва й доступність (невідома — `null`). */
export interface ProjectSummary {
  readonly id: string;
  readonly title: string;
  readonly visibility?: ProjectVisibility | null;
}

/** Стан списку проєктів: завантажується, є (`projects`) чи не вдалося. */
export interface ProjectsState {
  readonly status: 'loading' | 'ready' | 'failed';
  readonly projects: readonly ProjectSummary[];
}

/**
 * Проєкти «Планера» (`GET /api/v1/projects`, право `ViewProject`): ті, що сервер показує
 * користувачеві (публічні й ті, де він учасник), усі сторінки по 100, за назвою А–Я. Список
 * підвантажується при першому зверненні (`load`) і далі спільний; повтор після невдачі — знову
 * `load`. Інший акаунт — список попередньої організації забувається (як зміст «Бази знань»).
 * Чи можна створити проєкт — `canCreate` (право `CreateProject`), створює — `create`.
 */
@Injectable({ providedIn: 'root' })
export class ProjectsService {
  private readonly http = inject(HttpClient);
  private readonly connection = inject(ConnectionService);
  private readonly currentUser = inject(CurrentUserService);
  private readonly auth = inject(AuthService);
  private readonly accounts = inject(AccountsService);
  /** Завантаження — задача застосунку: `whenStable` (тести, пререндер) його дочікується. */
  private readonly pendingTasks = inject(PendingTasks);

  private readonly stateSignal = signal<ProjectsState>({ status: 'loading', projects: [] });
  readonly state: Signal<ProjectsState> = this.stateSignal.asReadonly();

  /** Чи можна бачити проєкти (`ViewProject`): без нього список не запитується. */
  readonly canView: Signal<boolean> = computed(() => this.currentUser.permissions().includes(PERMISSIONS.viewProject));
  /**
   * Чи доступна дія з проєктами за правами ролі: «Редагувати» — з `UpdateProject`, «Видалити» — з
   * `DeleteProject`; доступи є завжди (без `UpdateProject` — лише переглянути, `canEditAccess`).
   * Доступ у самому проєкті (учасник із «Редагуванням» чи «Керуванням», публічний проєкт) сервер
   * перевіряє окремо.
   */
  can(action: ProjectAction): boolean {
    const granted = this.currentUser.permissions();
    switch (action) {
      case 'edit':
        return granted.includes(PERMISSIONS.updateProject);
      case 'delete':
        return granted.includes(PERMISSIONS.deleteProject);
      case 'topic':
        return granted.includes(PERMISSIONS.updateProject);
      case 'access':
        return true;
    }
  }

  /** Чи можна змінювати доступи (`UpdateProject`): інакше — «Переглянути доступи». */
  readonly canEditAccess: Signal<boolean> = computed(() => this.currentUser.permissions().includes(PERMISSIONS.updateProject));

  private readonly actionRequestSignal = signal<ProjectActionRequest | null>(null);
  /**
   * Остання запитана дія з проєктом (три крапки в боковому меню, аватарки учасників у шапці
   * планера): вікно дії (`app-project-action-dialog`) бере її звідси й відкривається.
   */
  readonly actionRequest: Signal<ProjectActionRequest | null> = this.actionRequestSignal.asReadonly();

  /** Просить відкрити вікно дії `action` для проєкту `project`. */
  requestAction(action: ProjectAction, project: ProjectSummary): void {
    this.actionRequestSignal.set({ action, project });
  }

  /** Вікно дії взяло запит у роботу (наступний запит відкриє його знову). */
  clearActionRequest(): void {
    this.actionRequestSignal.set(null);
  }

  /** Чи можна створити проєкт (`CreateProject`): від цього залежить «+». */
  readonly canCreate: Signal<boolean> = computed(() => this.currentUser.permissions().includes(PERMISSIONS.createProject));

  /** Для кого завантажено список: акаунт (`AuthService.sessionId`) і сервер. */
  private loadedFor: string | null = null;
  private request: Promise<void> | null = null;
  /** Номер останнього запиту списку: відповідь на давніший (їх може йти два) нічого не змінює. */
  private fetchId = 0;

  constructor() {
    // Інший акаунт (вихід, вхід, перемикання) — список попередньої організації забуваємо;
    // тимчасове перемикання (пошук акаунта) його не скидає.
    effect(() => {
      const owner = this.owner();
      const switching = this.accounts.switching();
      untracked(() => {
        if (!switching && this.loadedFor !== null && this.loadedFor !== owner) {
          this.loadedFor = null;
          this.request = null;
          this.stateSignal.set({ status: 'loading', projects: [] });
        }
      });
    });
  }

  /** Підвантажує список, якщо його ще нема (чи минулий раз не вдалося); запит, що йде, не дублює. */
  load(): Promise<void> {
    if (this.stateSignal().status === 'ready' && this.loadedFor === this.owner()) {
      return Promise.resolve();
    }
    return this.startFetch();
  }

  /**
   * `POST /api/v1/projects` (право `CreateProject`; творець стає керівником проєкту): дати — початок
   * дня за UTC (`YYYY-MM-DDT00:00:00Z`), щоб обраний день не зсунувся. Успіх — список заново (місце
   * нового проєкту визначає сортування сервера); повертає `id` або причину невдачі.
   */
  async create(project: NewProject): Promise<{ readonly id: string } | RequestFailure> {
    const body = {
      title: project.title,
      description: project.description,
      visibility: project.visibility,
      startDate: utcDay(project.startDate),
      endDate: project.endDate ? utcDay(project.endDate) : null,
    } satisfies CreateProjectCommand;
    try {
      const id = await firstValueFrom(
        this.http.post<unknown>(`${this.connection.serverUrl()}${API_PROJECTS_PATH}`, body).pipe(timeout(REQUEST_TIMEOUT_MS)),
      );
      void this.startFetch(true);
      return { id: typeof id === 'string' ? id : '' };
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[ProjectsService] Не вдалося створити проєкт (${failure})`, error);
      return failure;
    }
  }

  /**
   * `GET /api/v1/projects/{id}`: дані проєкту для вікна «Редагувати» (дні — `YYYY-MM-DD` за UTC, як
   * їх зберігає `create`). Проєкту вже нема (404) — `gone`, і список оновлюється; відповідь не за
   * контрактом — `error`.
   */
  async get(id: string): Promise<NewProject | RequestFailure | Gone> {
    try {
      const body = await firstValueFrom(this.http.get<Untrusted<ProjectDto>>(this.url(id)).pipe(timeout(REQUEST_TIMEOUT_MS)));
      const project = toProjectDetails(body);
      if (!project) {
        console.warn('[ProjectsService] Проєкт прийшов не за контрактом', body);
        return 'error';
      }
      return project;
    } catch (error) {
      return this.failure(error, 'завантажити проєкт');
    }
  }

  /**
   * Колонки дошки: `GET /api/v1/projects/{id}` (право `ViewProject`) — сервер віддає проєкт разом з
   * колонками (`topics`), уже впорядкованими за позицією, тож окремі сторінки `…/topics` не потрібні.
   * Колонка не за контрактом пропускається; проєкту вже нема — `gone`.
   */
  async topics(projectId: string): Promise<readonly ProjectTopic[] | RequestFailure | Gone> {
    try {
      const body = await firstValueFrom(this.http.get<Untrusted<ProjectDto>>(this.url(projectId)).pipe(timeout(REQUEST_TIMEOUT_MS)));
      const topics = Array.isArray(body?.topics) ? body.topics : [];
      return topics.flatMap((topic): ProjectTopic[] => {
        const id = topic?.id;
        const title = topic?.title;
        const order = Number(topic?.order);
        return typeof id === 'string' && id && typeof title === 'string' && Number.isFinite(order) ? [{ id, title, order }] : [];
      });
    } catch (error) {
      return this.failure(error, 'завантажити колонки дошки');
    }
  }

  /**
   * `GET /api/v1/projects/{id}/members` (право `ViewProject`, проєкт видно користувачеві): учасники
   * проєкту в порядку сервера; запис не за контрактом пропускається. Проєкту вже нема (404) — `gone`.
   */
  async members(projectId: string): Promise<readonly ProjectMember[] | RequestFailure | Gone> {
    try {
      const body = await firstValueFrom(
        this.http.get<Untrusted<ProjectMemberDto>[]>(`${this.url(projectId)}/members`).pipe(timeout(REQUEST_TIMEOUT_MS)),
      );
      return Array.isArray(body) ? body.flatMap(toMember) : [];
    } catch (error) {
      return this.failure(error, 'завантажити учасників проєкту');
    }
  }

  /**
   * Теги проєкту: `GET /api/v1/projects/{id}` (право `ViewProject`) — сервер віддає проєкт разом з
   * усіма тегами, тож окремі сторінки `…/tags` не потрібні. Тег не за контрактом пропускається;
   * проєкту вже нема — `gone`.
   */
  async tags(projectId: string): Promise<readonly ProjectTag[] | RequestFailure | Gone> {
    try {
      const body = await firstValueFrom(this.http.get<Untrusted<ProjectDto>>(this.url(projectId)).pipe(timeout(REQUEST_TIMEOUT_MS)));
      const tags = Array.isArray(body?.tags) ? body.tags : [];
      return tags.flatMap((tag): ProjectTag[] => {
        const id = tag?.id;
        const title = tag?.title;
        return typeof id === 'string' && id && typeof title === 'string' && title.trim() ? [{ id, title }] : [];
      });
    } catch (error) {
      return this.failure(error, 'завантажити теги проєкту');
    }
  }

  /**
   * `id` тегів за назвами (для завдання): наявний тег (назви порівнюються без регістру) — його `id`,
   * нового нема — `POST /api/v1/projects/{id}/tags` (право `UpdateProject` і «Редагування» в
   * проєкті) і `id` створеного. Тег із такою назвою щойно створив хтось інший (409) — береться він.
   * Спершу — свіжі теги проєкту (`tags`). Невдача будь-якого запиту — її причина.
   */
  async tagIds(projectId: string, titles: readonly string[]): Promise<readonly string[] | RequestFailure | Gone> {
    if (!titles.length) {
      return [];
    }
    let known = await this.tags(projectId);
    if (typeof known === 'string') {
      return known;
    }
    const ids: string[] = [];
    for (const title of titles) {
      const find = (tags: readonly ProjectTag[]) => tags.find((tag) => tagKey(tag.title) === tagKey(title))?.id;
      let id = find(known);
      if (!id) {
        const created = await this.createTag(projectId, title.trim());
        if (created === 'exists') {
          const fresh = await this.tags(projectId);
          if (typeof fresh === 'string') {
            return fresh;
          }
          known = fresh;
          id = find(known);
        } else if (typeof created === 'string') {
          return created;
        } else {
          id = created.id;
        }
      }
      if (!id) {
        return 'error';
      }
      ids.push(id);
    }
    return [...new Set(ids)];
  }

  /** `POST /api/v1/projects/{id}/tags`: новий тег; тег із такою назвою вже є — `exists` (409). */
  private async createTag(projectId: string, title: string): Promise<{ readonly id: string } | 'exists' | RequestFailure | Gone> {
    try {
      const body = { title } satisfies CreateTagCommand;
      const id = await firstValueFrom(this.http.post<unknown>(`${this.url(projectId)}/tags`, body).pipe(timeout(REQUEST_TIMEOUT_MS)));
      return typeof id === 'string' && id ? { id } : 'error';
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 409) {
        return 'exists';
      }
      return this.failure(error, 'створити тег');
    }
  }

  private readonly membersVersionSignal = signal(0);
  /** Лічильник змін учасників (зміна рівня, видалення): хто показує учасників, перепитує їх. */
  readonly membersVersion: Signal<number> = this.membersVersionSignal.asReadonly();

  private readonly topicsVersionSignal = signal(0);
  /** Лічильник змін колонок дошки: хто показує колонки, перепитує їх. */
  readonly topicsVersion: Signal<number> = this.topicsVersionSignal.asReadonly();

  /**
   * Нова колонка (тема) дошки в кінці: спершу `GET /api/v1/projects/{id}/topics` — скільки колонок уже
   * є (порядок сервер вимагає неперервним з 0, тож нова — під номером їх кількості), тоді `POST
   * …/topics` з назвою й порядком (право `UpdateProject` і «Редагування» в проєкті). Колонка з такою
   * назвою вже є (чи хтось щойно зайняв той самий порядок) — `exists` (409); проєкту нема — `gone`.
   */
  async createTopic(projectId: string, title: string): Promise<'done' | 'exists' | RequestFailure | Gone> {
    try {
      const params = new HttpParams().set('PageNumber', 1).set('PageSize', 1);
      const page = await firstValueFrom(
        this.http.get<Untrusted<PagedResultOfTopicDto>>(`${this.url(projectId)}/topics`, { params }).pipe(timeout(REQUEST_TIMEOUT_MS)),
      );
      const order = Number(page?.totalCount);
      if (!Number.isInteger(order) || order < 0) {
        console.warn('[ProjectsService] Список колонок прийшов не за контрактом', page);
        return 'error';
      }
      const body = { title, order } satisfies CreateTopicCommand;
      await firstValueFrom(this.http.post(`${this.url(projectId)}/topics`, body).pipe(timeout(REQUEST_TIMEOUT_MS)));
      this.topicsVersionSignal.update((version) => version + 1);
      return 'done';
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 409) {
        return 'exists';
      }
      return this.failure(error, 'створити колонку');
    }
  }

  /**
   * `PUT /api/v1/projects/{id}/topics/{topicId}` (право `UpdateProject` і «Редагування» в проєкті):
   * нова назва колонки. Сервер разом із назвою бере й позицію, тож спершу свіжі колонки (`topics`) —
   * щоб надіслати позицію, яку колонка має зараз, і не пересунути її, якщо порядок тим часом
   * змінився. Колонка з такою назвою вже є — `exists` (409); самої колонки вже нема — `topicGone`
   * (дошка перепитує колонки); проєкту нема — `gone`.
   */
  async updateTopic(projectId: string, topicId: string, title: string): Promise<'done' | 'exists' | 'topicGone' | RequestFailure | Gone> {
    const topics = await this.topics(projectId);
    if (typeof topics === 'string') {
      return topics;
    }
    const current = topics.find((topic) => topic.id === topicId);
    if (!current) {
      this.topicsVersionSignal.update((version) => version + 1);
      return 'topicGone';
    }
    try {
      const body = { title, order: current.order } satisfies UpdateTopicCommand;
      await firstValueFrom(this.http.put(this.topicUrl(projectId, topicId), body).pipe(timeout(REQUEST_TIMEOUT_MS)));
      this.topicsVersionSignal.update((version) => version + 1);
      return 'done';
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 409) {
        return 'exists';
      }
      if (isGone(error)) {
        this.topicsVersionSignal.update((version) => version + 1);
        return 'topicGone';
      }
      return this.failure(error, 'змінити колонку');
    }
  }

  /**
   * `DELETE /api/v1/projects/{id}/topics/{topicId}` (право `UpdateProject` і «Редагування» в
   * проєкті); позиції решти колонок сервер стискає сам. Колонку, в якій є завдання, сервер не видаляє
   * (400 — `hasTasks`). Її вже нема (404) — теж `done`: результат той самий. Успіх — дошка
   * перепитує колонки.
   */
  async removeTopic(projectId: string, topicId: string): Promise<'done' | 'hasTasks' | RequestFailure> {
    try {
      await firstValueFrom(this.http.delete(this.topicUrl(projectId, topicId)).pipe(timeout(REQUEST_TIMEOUT_MS)));
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 400) {
        return 'hasTasks';
      }
      if (!isGone(error)) {
        const failure = toRequestFailure(error);
        console.warn(`[ProjectsService] Не вдалося видалити колонку (${failure})`, error);
        return failure;
      }
    }
    this.topicsVersionSignal.update((version) => version + 1);
    return 'done';
  }

  /**
   * `POST /api/v1/projects/{id}/members` (право `UpdateProject` і «Керування» в проєкті): додає
   * колегу з рівнем доступу `claim` і назвою ролі в проєкті `roleTitle` (порожня — `null`). Вимкнений акаунт сервер не додає (400 —
   * `invalid`); колега вже учасник — `exists` (409); проєкту чи колеги вже нема — `gone` (404).
   */
  async addMember(
    projectId: string,
    userId: string,
    claim: ProjectClaim,
    roleTitle: string | null = null,
  ): Promise<'done' | 'exists' | RequestFailure | Gone> {
    const body = { userId, roleTitle, claim } satisfies AssignProjectMemberCommand;
    try {
      await firstValueFrom(this.http.post(`${this.url(projectId)}/members`, body).pipe(timeout(REQUEST_TIMEOUT_MS)));
      this.membersVersionSignal.update((version) => version + 1);
      return 'done';
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 409) {
        return 'exists';
      }
      return this.failure(error, 'додати учасника до проєкту');
    }
  }

  /**
   * `PUT /api/v1/projects/{id}/members/{userId}` (право `UpdateProject` і «Керування» в проєкті):
   * роль у проєкті (назва лишається) з новим рівнем доступу. Понизити останнього активного
   * керівника сервер не дає (400 — `invalid`).
   */
  async updateMember(projectId: string, member: ProjectMember, claim: ProjectClaim): Promise<'done' | RequestFailure | Gone> {
    const body = { roleTitle: member.roleTitle, claim } satisfies UpdateProjectMemberCommand;
    try {
      await firstValueFrom(
        this.http.put(`${this.url(projectId)}/members/${encodeURIComponent(member.userId)}`, body).pipe(timeout(REQUEST_TIMEOUT_MS)),
      );
      this.membersVersionSignal.update((version) => version + 1);
      return 'done';
    } catch (error) {
      return this.failure(error, 'змінити доступ учасника');
    }
  }

  /**
   * `DELETE /api/v1/projects/{id}/members/{userId}` (право `UpdateProject` і «Керування» в проєкті).
   * Прибрати останнього активного керівника сервер не дає (400 — `invalid`).
   */
  async removeMember(projectId: string, userId: string): Promise<'done' | RequestFailure | Gone> {
    try {
      await firstValueFrom(this.http.delete(`${this.url(projectId)}/members/${encodeURIComponent(userId)}`).pipe(timeout(REQUEST_TIMEOUT_MS)));
      this.membersVersionSignal.update((version) => version + 1);
      return 'done';
    } catch (error) {
      return this.failure(error, 'прибрати учасника з проєкту');
    }
  }

  /**
   * `PUT /api/v1/projects/{id}` (право `UpdateProject`; змінити доступність може лише учасник із
   * «Керуванням»): назва, опис, доступність і дати (як у `create`). Успіх — список заново (назва могла
   * змінитися); проєкту вже нема (404) — `gone`.
   */
  async update(id: string, project: NewProject): Promise<'done' | RequestFailure | Gone> {
    const body = {
      title: project.title,
      description: project.description,
      visibility: project.visibility,
      startDate: utcDay(project.startDate),
      endDate: project.endDate ? utcDay(project.endDate) : null,
    } satisfies UpdateProjectCommand;
    try {
      await firstValueFrom(this.http.put(this.url(id), body).pipe(timeout(REQUEST_TIMEOUT_MS)));
      void this.startFetch(true);
      return 'done';
    } catch (error) {
      return this.failure(error, 'змінити проєкт');
    }
  }

  /**
   * `DELETE /api/v1/projects/{id}` (право `DeleteProject` і «Керування» в проєкті): проєкт разом з
   * усіма задачами, темами, тегами й учасниками. Його вже нема (404) — теж `done`: результат той
   * самий. Успіх — список заново.
   */
  async remove(id: string): Promise<'done' | RequestFailure> {
    try {
      await firstValueFrom(this.http.delete(this.url(id)).pipe(timeout(REQUEST_TIMEOUT_MS)));
    } catch (error) {
      if (!isGone(error)) {
        const failure = toRequestFailure(error);
        console.warn(`[ProjectsService] Не вдалося видалити проєкт (${failure})`, error);
        return failure;
      }
    }
    void this.startFetch(true);
    return 'done';
  }

  private url(id: string): string {
    return `${this.connection.serverUrl()}${API_PROJECTS_PATH}/${encodeURIComponent(id)}`;
  }

  private topicUrl(projectId: string, topicId: string): string {
    return `${this.url(projectId)}/topics/${encodeURIComponent(topicId)}`;
  }

  /** Причина невдачі запиту `what`; 404 — `gone` (і список заново: проєкту в ньому вже не має бути). */
  private failure(error: unknown, what: string): RequestFailure | Gone {
    if (isGone(error)) {
      void this.startFetch(true);
      return 'gone';
    }
    const failure = toRequestFailure(error);
    console.warn(`[ProjectsService] Не вдалося ${what} (${failure})`, error);
    return failure;
  }

  /** Запит списку; `again` — навіть якщо запит уже йде (його відповідь могла не встигнути за змінами). */
  private startFetch(again = false): Promise<void> {
    if (again) {
      this.request = null;
    }
    if (!this.request) {
      const done = this.pendingTasks.add();
      const request: Promise<void> = this.fetch().finally(() => {
        if (this.request === request) {
          this.request = null;
        }
        done();
      });
      this.request = request;
    }
    return this.request;
  }

  /** Чий список потрібен зараз: активний акаунт на своєму сервері. */
  private owner(): string {
    return `${this.auth.sessionId() ?? ''}|${this.connection.serverUrl()}`;
  }

  private async fetch(): Promise<void> {
    // Оновлення після створення не ховає наявний список, поки йде запит.
    if (this.stateSignal().status !== 'ready') {
      this.stateSignal.set({ status: 'loading', projects: [] });
    }
    const owner = this.owner();
    const id = ++this.fetchId;
    const projects = await this.listAll();
    // Поки йшов запит, акаунт змінився (це проєкти іншої організації) чи пішов новіший запит.
    if (owner !== this.owner() || id !== this.fetchId) {
      return;
    }
    this.loadedFor = owner;
    this.stateSignal.set(typeof projects === 'string' ? { status: 'failed', projects: [] } : { status: 'ready', projects });
  }

  private async listAll(): Promise<readonly ProjectSummary[] | RequestFailure> {
    const projects: ProjectSummary[] = [];
    try {
      for (let page = 1; ; page++) {
        const params = new HttpParams().set('OrderBy', 'Title').set('SortOrder', 'asc').set('PageNumber', page).set('PageSize', PROJECTS_PAGE_SIZE);
        const body = await firstValueFrom(
          this.http
            .get<Untrusted<PagedResultOfProjectSummaryDto>>(`${this.connection.serverUrl()}${API_PROJECTS_PATH}`, { params })
            .pipe(timeout(REQUEST_TIMEOUT_MS)),
        );
        const { items, hasNextPage } = readPagedItems(body, page, toProject);
        projects.push(...items);
        if (!hasNextPage) {
          return projects;
        }
      }
    } catch (error) {
      const failure = toRequestFailure(error);
      console.warn(`[ProjectsService] Не вдалося завантажити проєкти (${failure})`, error);
      return failure;
    }
  }
}

/** День `YYYY-MM-DD` як початок цього дня за UTC (формат дати й часу сервера). */
export function utcDay(day: string): string {
  return `${day}T00:00:00Z`;
}

/** День за UTC (`YYYY-MM-DD`) з дати й часу сервера; не дата — `null`. */
export function dayOf(value: unknown): string | null {
  if (typeof value !== 'string') {
    return null;
  }
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date.toISOString().slice(0, 10);
}

/** Дані проєкту для форми з відповіді `GET /projects/{id}`; не за контрактом — `null`. */
function toProjectDetails(value: Untrusted<ProjectDto>): NewProject | null {
  const title = value?.title;
  const visibility = PROJECT_VISIBILITIES.find((candidate) => candidate === value?.visibility);
  const startDate = dayOf(value?.startDate);
  const description = value?.description;
  if (typeof title !== 'string' || visibility === undefined || !startDate) {
    return null;
  }
  return {
    title,
    description: typeof description === 'string' ? description : null,
    visibility,
    startDate,
    endDate: dayOf(value?.endDate),
  };
}

function toMember(value: Untrusted<ProjectMemberDto>): ProjectMember[] {
  const userId = value?.userId;
  if (typeof userId !== 'string' || !userId) {
    return [];
  }
  const text = (field: unknown) => (typeof field === 'string' ? field : '');
  return [
    {
      userId,
      firstName: text(value?.firstName),
      lastName: text(value?.lastName),
      patronymic: text(value?.patronymic) || null,
      avatarUrl: text(value?.avatarUrl) || null,
      roleTitle: text(value?.roleTitle) || null,
      claim: PROJECT_CLAIMS.find((claim) => claim === value?.claim) ?? 0,
      isActive: value?.isActive !== false,
    },
  ];
}

function toProject(value: Untrusted<ProjectSummaryDto>): ProjectSummary[] {
  const id = value?.id;
  const title = value?.title;
  const visibility = PROJECT_VISIBILITIES.find((candidate) => candidate === value?.visibility) ?? null;
  return typeof id === 'string' && id && typeof title === 'string' && title.trim() ? [{ id, title, visibility }] : [];
}
