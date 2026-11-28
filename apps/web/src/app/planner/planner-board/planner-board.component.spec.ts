import { CUSTOM_ELEMENTS_SCHEMA, Signal, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { ProjectTopic, ProjectsService } from '../../core/projects/projects.service';
import { ProjectTask, TasksService } from '../../core/tasks/tasks.service';
import { TaskAccessService } from '../../core/tasks/task-access.service';
import { CurrentUserService } from '../../core/users/current-user.service';
import { TaskDialogComponent } from './task-dialog.component';
import { TaskMenuComponent } from './task-menu.component';
import { TopicMenuComponent } from './topic-menu.component';
import { By } from '@angular/platform-browser';
import { PlannerBoardComponent } from './planner-board.component';

describe('PlannerBoardComponent', () => {
  async function render(topics: readonly ProjectTopic[] | 'error', permissions: string[] = [], tasks: readonly ProjectTask[] = []) {
    TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    (TestBed.inject(CurrentUserService) as { permissions: Signal<readonly string[]> }).permissions = signal(permissions);
    TestBed.inject(TaskAccessService).setClaim('p1', 1);
    const load = vi.spyOn(TestBed.inject(ProjectsService), 'topics').mockResolvedValue(topics);
    const loadTasks = vi
      .spyOn(TestBed.inject(TasksService), 'page')
      .mockImplementation(async (_project, _page, _size, query) => ({ items: tasks.filter((task) => task.topicId === query?.topicId), hasNextPage: false }));
    const fixture = TestBed.createComponent(PlannerBoardComponent);
    fixture.componentRef.setInput('projectId', 'p1');
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return { fixture, element: fixture.nativeElement as HTMLElement, load, loadTasks };
  }

  afterEach(() => vi.restoreAllMocks());

  it('puts the columns of the project in one row, 385px each, the row scrolling sideways', async () => {
    const { element, load } = await render([
      { id: 't1', title: 'Розробка', order: 0 },
      { id: 't2', title: 'SMM', order: 1 },
    ]);
    expect(load).toHaveBeenCalledWith('p1');
    const columns = Array.from(element.querySelectorAll<HTMLElement>('.board__column'));
    expect(columns.map((column) => column.querySelector('.board__column-title')?.textContent?.trim())).toEqual(['Розробка', 'SMM']);
    expect(getComputedStyle(columns[0]).width).toBe('385px');
    expect(getComputedStyle(element.querySelector('.board__columns')!).overflowX).toBe('auto');
  });

  it('asks the tasks of each column by pages, in the order of the server, the list scrolling inside the column', async () => {
    const task = (id: string, topicId: string): ProjectTask => ({
      id,
      topicId,
      title: 'Завдання ' + id,
      status: 0,
      priority: 1,
      startDate: '2026-03-20',
      endDate: null,
      assignees: [],
      tags: [], unreadComments: 0,
      description: null,
    });
    const { element, loadTasks } = await render(
      [
        { id: 't1', title: 'Розробка', order: 0 },
        { id: 't2', title: 'SMM', order: 1 },
      ],
      [],
      [task('a', 't2'), task('b', 't1'), task('c', 't2')],
    );
    expect(loadTasks).toHaveBeenCalledWith('p1', 1, 100, { topicId: 't1' });
    expect(loadTasks).toHaveBeenCalledWith('p1', 1, 100, { topicId: 't2' });
    const columns = Array.from(element.querySelectorAll<HTMLElement>('.board__column'));
    const cards = (column: HTMLElement) => Array.from(column.querySelectorAll('app-task-card')).map((card) => card.querySelector('.task-card__title')?.textContent?.trim());
    expect(cards(columns[0])).toEqual(['Завдання b']);
    expect(cards(columns[1])).toEqual(['Завдання a', 'Завдання c']);
    expect(getComputedStyle(columns[1].querySelector('app-board-tasks')!).overflowY).toBe('auto');
  });

  it('opens a task by a click on its card or «Details», and edits it in the task window by «Edit» with Permissions.UpdateTask', async () => {
    const task: ProjectTask = {
      id: 'a',
      topicId: 't1',
      title: 'Афіша',
      description: null,
      status: 0,
      priority: 1,
      startDate: '2026-03-20',
      endDate: null,
      assignees: [],
      tags: [], unreadComments: 0,
    };
    const { fixture, element } = await render([{ id: 't1', title: 'Розробка', order: 0 }], ['Permissions.UpdateTask'], [task]);
    const opened: ProjectTask[] = [];
    fixture.componentInstance.openTask.subscribe((value) => opened.push(value));
    element.querySelector<HTMLElement>('.task-card')!.click();
    const menu = fixture.debugElement.query(By.directive(TaskMenuComponent)).componentInstance as TaskMenuComponent;
    expect(menu.canEdit()).toBe(true);
    menu.details.emit(task);
    expect(opened).toEqual([task, task]);
    const dialog = fixture.debugElement.query(By.directive(TaskDialogComponent)).componentInstance as TaskDialogComponent;
    const edit = vi.spyOn(dialog, 'edit').mockImplementation(() => undefined);
    menu.edit.emit(task);
    expect(edit).toHaveBeenCalledWith('p1', task);
  });

  it('asks the tasks again after a new task', async () => {
    const { fixture, loadTasks } = await render([{ id: 't1', title: 'Розробка', order: 0 }]);
    (TestBed.inject(TasksService) as unknown as { versionSignal: { update(fn: (v: number) => number): void } }).versionSignal.update((v) => v + 1);
    fixture.detectChanges();
    await fixture.whenStable();
    expect(loadTasks).toHaveBeenCalledTimes(2);
  });

  it('shows the empty state without columns', async () => {
    const { element } = await render([]);
    expect(element.querySelector('app-empty-state')).not.toBeNull();
  });

  it('asks again after a failure by «Try again», and again after a new column', async () => {
    const { element, load, fixture } = await render('error');
    expect(element.querySelector('.board__message--error')?.textContent).toContain(uk.planner.board.error);
    load.mockResolvedValue([{ id: 't1', title: 'Розробка', order: 0 }]);
    element.querySelector<HTMLButtonElement>('.board__retry')!.click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(element.querySelectorAll('.board__column')).toHaveLength(1);

    (TestBed.inject(ProjectsService) as unknown as { topicsVersionSignal: { update(fn: (v: number) => number): void } }).topicsVersionSignal.update((v) => v + 1);
    fixture.detectChanges();
    await fixture.whenStable();
    expect(load).toHaveBeenCalledTimes(3);
  });

  it('draws the gradient on the right only while there are columns beyond the edge', async () => {
    const { element, fixture } = await render([
      { id: 't1', title: 'Розробка', order: 0 },
      { id: 't2', title: 'SMM', order: 1 },
    ]);
    const row = element.querySelector<HTMLElement>('.board__columns')!;
    Object.defineProperty(row, 'clientWidth', { configurable: true, value: 400 });
    Object.defineProperty(row, 'scrollWidth', { configurable: true, value: 572 });
    row.scrollLeft = 0;
    row.dispatchEvent(new Event('scroll'));
    fixture.detectChanges();
    expect(element.classList).toContain('board--more-right');

    Object.defineProperty(row, 'scrollLeft', { configurable: true, value: 172 });
    row.dispatchEvent(new Event('scroll'));
    fixture.detectChanges();
    expect(element.classList).not.toContain('board--more-right');
  });

  describe('the head of a column', () => {
    const topics = [{ id: 't1', title: 'Розробка', order: 0 }];
    const buttons = (element: HTMLElement) => Array.from(element.querySelectorAll<HTMLButtonElement>('.board__column-head .board__column-button'));

    it('has «+» with Permissions.CreateTask and the three dots after it with Permissions.UpdateProject, named after the column', async () => {
      const { element } = await render(topics, ['Permissions.CreateTask', 'Permissions.UpdateProject']);
      expect(buttons(element).map((button) => button.getAttribute('aria-label'))).toEqual([
        uk.planner.board.addTask.replace('{{title}}', 'Розробка'),
        uk.planner.board.topicActions.replace('{{title}}', 'Розробка'),
      ]);
    });

    it('has no buttons without the rights', async () => {
      expect(buttons((await render(topics)).element)).toHaveLength(0);
      TestBed.resetTestingModule();
      expect(buttons((await render(topics, ['Permissions.UpdateProject'])).element).map((button) => button.getAttribute('aria-label'))).toEqual([
        uk.planner.board.topicActions.replace('{{title}}', 'Розробка'),
      ]);
    });

    it('opens the window of a new task in the column by «+»', async () => {
      const { element, fixture } = await render(topics, ['Permissions.CreateTask']);
      const dialog = fixture.debugElement.query(By.directive(TaskDialogComponent)).componentInstance as TaskDialogComponent;
      const open = vi.spyOn(dialog, 'create').mockImplementation(() => undefined);
      buttons(element)[0].click();
      expect(open).toHaveBeenCalledWith('p1', topics[0]);
    });

    it('opens the menu of the column with «Edit» and «Delete» by the three dots', async () => {
      const { element, fixture } = await render(topics, ['Permissions.UpdateProject']);
      const menu = fixture.debugElement.query(By.directive(TopicMenuComponent)).componentInstance as TopicMenuComponent;
      const open = vi.spyOn(menu, 'open').mockImplementation(() => undefined);
      buttons(element)[0].click();
      expect(open).toHaveBeenCalledWith(expect.any(Event), topics[0]);
      expect((menu as unknown as { items: { labelKey: string }[] }).items.map((item) => item.labelKey)).toEqual([
        'planner.topicActions.edit',
        'planner.topicActions.delete',
      ]);
    });
  });
});
