import { CUSTOM_ELEMENTS_SCHEMA, Signal, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideIonicAngular } from '@ionic/angular';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { ProjectsService } from '../../core/projects/projects.service';
import { ProjectTask, TaskComment, TasksService } from '../../core/tasks/tasks.service';
import { CurrentUser, CurrentUserService } from '../../core/users/current-user.service';
import { roleColor } from '../../shared/role-pill/role-color';
import { TASK_COMMENTS_PAGE_SIZE, TaskDetailsComponent } from './task-details.component';

describe('TaskDetailsComponent', () => {
  const task: ProjectTask = {
    id: 'a',
    topicId: 't1',
    title: 'Афіша',
    description: 'Макет',
    status: 0,
    priority: 2,
    startDate: '2026-03-20',
    endDate: '2026-03-25',
    assignees: [],
    tags: [{ id: 'g1', title: 'Дизайн' }],
    unreadComments: 2,
  };
  const comment: TaskComment = {
    id: 'c1',
    userId: 'u2',
    firstName: 'Імʼя',
    lastName: 'Прізвище',
    avatarUrl: null,
    content: 'Готово',
    createdAt: '2026-03-20T10:00:00Z',
  };

  async function open(value: ProjectTask = task) {
    TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideIonicAngular(), provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    (TestBed.inject(CurrentUserService) as { user: Signal<CurrentUser | null> }).user = signal({ id: 'me', firstName: 'Я', lastName: 'Сам' } as CurrentUser);
    vi.spyOn(TestBed.inject(ProjectsService), 'state').mockReturnValue({ status: 'ready', projects: [{ id: 'p1', title: 'Явір', visibility: 0 }] });
    const tasks = TestBed.inject(TasksService);
    const comments = vi.spyOn(tasks, 'comments').mockResolvedValue({ items: [comment], hasNextPage: false });
    const markRead = vi.spyOn(tasks, 'markCommentsRead').mockResolvedValue('done');
    const addComment = vi.spyOn(tasks, 'addComment').mockResolvedValue({ id: 'c2' });
    const updateComment = vi.spyOn(tasks, 'updateComment').mockResolvedValue('done');
    const removeComment = vi.spyOn(tasks, 'removeComment').mockResolvedValue('done');
    const get = vi.spyOn(tasks, 'get').mockResolvedValue({ ...value, title: 'Афіша 2' });
    const fixture = TestBed.createComponent(TaskDetailsComponent);
    fixture.componentRef.setInput('projectId', 'p1');
    fixture.detectChanges();
    fixture.componentInstance.open(value);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const text = (selector: string) => element.querySelector(selector)?.textContent?.replace(/\s+/g, ' ').trim();
    return { fixture, element, text, comments, markRead, addComment, updateComment, removeComment, get, tasks };
  }

  afterEach(() => vi.restoreAllMocks());

  it('shows the project tag in its colour, the title, the facts, the tags and the comments, marking them read', async () => {
    const { element, text, comments, markRead } = await open();
    const project = element.querySelector<HTMLElement>('.task-details__project')!;
    expect(project.textContent?.trim()).toBe('Явір');
    expect(project.style.getPropertyValue('--project-base')).toBe(roleColor('Явір')!.base);
    expect(text('.task-details__title')).toBe('Афіша');
    expect(element.querySelector('app-task-status')).not.toBeNull();
    const labels = Array.from(element.querySelectorAll('.task-details__fact dt')).map((dt) => dt.textContent?.trim());
    expect(labels).toEqual([
      uk.planner.details.period,
      uk.planner.details.priority,
      uk.planner.details.description,
      uk.planner.details.assignees,
      uk.planner.details.tags,
    ]);
    expect(text('.task-details__priority')).toBe(uk.planner.taskPriority['2']);
    expect(text('.task-details__tags')).toBe('Дизайн');
    expect(comments).toHaveBeenCalledWith('p1', 'a', 1, TASK_COMMENTS_PAGE_SIZE);
    expect(text('.comment__author')).toBe('Прізвище Імʼя');
    expect(text('.comment__text')).toBe('Готово');
    expect(markRead).toHaveBeenCalledWith('p1', 'a');
  });

  it('does not mark read without new comments, and says there are none yet', async () => {
    const first = await open({ ...task, unreadComments: 0 });
    expect(first.markRead).not.toHaveBeenCalled();
    TestBed.resetTestingModule();
    const second = await open();
    second.comments.mockResolvedValue({ items: [], hasNextPage: false });
    second.fixture.componentInstance.open(task);
    await second.fixture.whenStable();
    second.fixture.detectChanges();
    expect(second.text('.task-details__note')).toBe(uk.planner.details.noComments);
  });

  it('sends a comment and puts it at the end when all are shown', async () => {
    const { fixture, element, addComment } = await open();
    const input = element.querySelector<HTMLInputElement>('.task-details__input')!;
    expect(input.getAttribute('maxlength')).toBe('5000');
    input.value = '  Дякую ';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    element.querySelector<HTMLButtonElement>('.task-details__send')!.click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(addComment).toHaveBeenCalledWith('p1', 'a', 'Дякую');
    expect(Array.from(element.querySelectorAll('.comment__text')).map((item) => item.textContent?.trim())).toEqual(['Готово', 'Дякую']);
    expect(Array.from(element.querySelectorAll('.comment__author')).map((item) => item.textContent?.trim())).toEqual(['Прізвище Імʼя', 'Сам Я']);
    expect(input.value).toBe('');
  });

  it('reads the task again after a change of the tasks', async () => {
    const { fixture, text, get, tasks } = await open();
    (tasks as unknown as { versionSignal: { update(fn: (v: number) => number): void } }).versionSignal.update((v) => v + 1);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(get).toHaveBeenCalledWith('p1', 'a');
    expect(text('.task-details__title')).toBe('Афіша 2');
  });

  it('edits an own comment in place and brings it back as usual, and deletes one without asking', async () => {
    const { fixture, element, updateComment, removeComment, comments } = await open();
    comments.mockResolvedValue({ items: [{ ...comment, userId: 'me' }], hasNextPage: false });
    fixture.componentInstance.open(task);
    await fixture.whenStable();
    fixture.detectChanges();
    const details = fixture.componentInstance as unknown as {
      openCommentMenu(event: Event, value: TaskComment): void;
      onCommentAction(action: 'edit' | 'delete'): Promise<void>;
      commentMenu(): { open(): void; close(): Promise<void> };
    };
    vi.spyOn(details.commentMenu(), 'open').mockImplementation(() => undefined);
    vi.spyOn(details.commentMenu(), 'close').mockResolvedValue();
    expect(element.querySelector('.comment__menu')).not.toBeNull();

    details.openCommentMenu(new Event('click'), { ...comment, userId: 'me' });
    await details.onCommentAction('edit');
    fixture.detectChanges();
    const input = element.querySelector<HTMLInputElement>('.comment__input')!;
    input.value = 'Готово, перевірено';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    element.querySelector<HTMLButtonElement>('.comment__send')!.click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(updateComment).toHaveBeenCalledWith('p1', 'a', 'c1', 'Готово, перевірено');
    expect(element.querySelector('.comment__input')).toBeNull();
    expect(element.querySelector('.comment__text')?.textContent?.trim()).toBe('Готово, перевірено');

    details.openCommentMenu(new Event('click'), { ...comment, userId: 'me' });
    await details.onCommentAction('delete');
    fixture.detectChanges();
    expect(removeComment).toHaveBeenCalledWith('p1', 'a', 'c1');
    expect(element.querySelector('app-task-comment')).toBeNull();
  });
});
