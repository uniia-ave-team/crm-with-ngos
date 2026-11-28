import { CUSTOM_ELEMENTS_SCHEMA, Signal, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideIonicAngular } from '@ionic/angular';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { ProjectTask, TasksService } from '../../core/tasks/tasks.service';
import { TaskAccessService } from '../../core/tasks/task-access.service';
import { CurrentUserService } from '../../core/users/current-user.service';
import { DataTableComponent } from '../../shared/data-table/data-table.component';
import { SortBarComponent } from '../../shared/sort-bar/sort-bar.component';
import { TaskMenuComponent } from '../planner-board/task-menu.component';
import { MY_TASKS_PAGE_SIZE, MyTasksComponent } from './my-tasks.component';

describe('MyTasksComponent', () => {
  const task = (id: string, overrides: Partial<ProjectTask> = {}): ProjectTask => ({
    id,
    topicId: 't1',
    title: 'Завдання ' + id,
    description: null,
    status: 1,
    priority: 3,
    startDate: '2026-03-20',
    endDate: '2026-03-25',
    assignees: [],
    tags: [{ id: 'g1', title: 'Дизайн' }],
    unreadComments: 0,
    ...overrides,
  });

  async function render(pages: { items: ProjectTask[]; hasNextPage: boolean }[] = [{ items: [task('a'), task('b', { unreadComments: 2 })], hasNextPage: false }], permissions: string[] = []) {
    TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideIonicAngular(), provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    (TestBed.inject(CurrentUserService) as { permissions: Signal<readonly string[]> }).permissions = signal(permissions);
    TestBed.inject(TaskAccessService).setClaim('p1', 1);
    const page = vi.spyOn(TestBed.inject(TasksService), 'page');
    pages.forEach((value) => page.mockResolvedValueOnce(value));
    page.mockResolvedValue({ items: [], hasNextPage: false });
    const fixture = TestBed.createComponent(MyTasksComponent);
    fixture.componentRef.setInput('projectId', 'p1');
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    return { fixture, element, page };
  }

  afterEach(() => vi.restoreAllMocks());

  it('asks the own tasks of the project and shows them in the table with their columns', async () => {
    const { element, page } = await render();
    expect(page).toHaveBeenCalledWith('p1', 1, MY_TASKS_PAGE_SIZE, { mine: true, orderBy: null, sortOrder: undefined, search: '' });
    const headers = Array.from(element.querySelectorAll('app-data-table th')).map((th) => th.textContent?.trim());
    expect(headers.slice(0, 6)).toEqual(Object.values(uk.planner.myTasks.columns));
    const rows = Array.from(element.querySelectorAll('app-data-table tbody tr'));
    expect(rows).toHaveLength(2);
    expect(rows[0].querySelector('.my-tasks__title-text')?.textContent?.trim()).toBe('Завдання a');
    expect(rows[0].querySelector('.task-status')?.textContent?.trim()).toBe(uk.planner.taskStatus['1']);
    expect(rows[0].querySelector('.my-tasks__priority')?.textContent?.trim()).toBe(uk.planner.taskPriority['3']);
    expect(rows[0].querySelector('.task-tag')?.textContent?.trim()).toBe('Дизайн');
    expect(rows[0].querySelector('.my-tasks__comments--unread')).toBeNull();
    expect(rows[1].querySelector('.my-tasks__comments--unread')).not.toBeNull();
    // Ті самі завдання — картками для телефону.
    expect(element.querySelectorAll('.my-tasks__cards app-task-card')).toHaveLength(2);
  });

  it('sorts on the server by a header, and by the sort row on a phone when it is open', async () => {
    const { fixture, element, page } = await render();
    expect(element.querySelector('app-sort-bar')).toBeNull();
    const table = fixture.debugElement.query(By.directive(DataTableComponent)).componentInstance as DataTableComponent;
    const columns = (table as unknown as { columns(): { id: string; orderBy?: string }[] }).columns();
    table.sortChange.emit({ column: columns.find((column) => column.id === 'priority')! as never, direction: 'desc' });
    await fixture.whenStable();
    expect(page).toHaveBeenLastCalledWith('p1', 1, MY_TASKS_PAGE_SIZE, { mine: true, orderBy: 'Priority', sortOrder: 'desc', search: '' });

    fixture.componentRef.setInput('sortOpen', true);
    fixture.detectChanges();
    const bar = fixture.debugElement.query(By.directive(SortBarComponent)).componentInstance as SortBarComponent;
    expect(bar.options().map((option) => option.id)).toEqual(['title', 'status', 'dates', 'priority']);
    bar.sortChange.emit({ id: 'dates', direction: 'asc' });
    await fixture.whenStable();
    expect(page).toHaveBeenLastCalledWith('p1', 1, MY_TASKS_PAGE_SIZE, { mine: true, orderBy: 'StartDate', sortOrder: 'asc', search: '' });
  });

  it('asks again by the search of the header', async () => {
    const { fixture, page } = await render();
    fixture.componentRef.setInput('search', 'афіша');
    fixture.detectChanges();
    await fixture.whenStable();
    expect(page).toHaveBeenLastCalledWith('p1', 1, MY_TASKS_PAGE_SIZE, { mine: true, orderBy: null, sortOrder: undefined, search: 'афіша' });
  });

  it('opens a task by a click on its row or card, and gives the menu of a row with «Edit» by Permissions.UpdateTask', async () => {
    const { fixture, element } = await render(undefined, ['Permissions.UpdateTask']);
    const opened: ProjectTask[] = [];
    fixture.componentInstance.openTask.subscribe((value) => opened.push(value));
    element.querySelector<HTMLElement>('app-data-table tbody tr')!.click();
    element.querySelector<HTMLElement>('.my-tasks__cards .task-card')!.click();
    expect(opened.map((value) => value.id)).toEqual(['a', 'a']);
    const menu = fixture.debugElement.query(By.directive(TaskMenuComponent)).componentInstance as TaskMenuComponent;
    expect(menu.canEdit()).toBe(true);
    const open = vi.spyOn(menu, 'open').mockImplementation(() => undefined);
    element.querySelector<HTMLButtonElement>('app-data-table .data-table__menu')!.click();
    expect(open).toHaveBeenCalledWith(expect.any(HTMLElement), expect.objectContaining({ id: 'a' }));
  });

  it('shows the empty state without tasks, «nothing found» with a search, and the reason of a failure', async () => {
    const first = await render([{ items: [], hasNextPage: false }]);
    expect(first.element.querySelector('app-empty-state')).not.toBeNull();
    first.fixture.componentRef.setInput('search', 'х');
    first.fixture.detectChanges();
    await first.fixture.whenStable();
    first.fixture.detectChanges();
    expect(first.element.querySelector('.my-tasks__message')?.textContent?.trim()).toBe(uk.planner.myTasks.notFound);
    TestBed.resetTestingModule();
    const { page, element, fixture } = await render([]);
    page.mockReset();
    page.mockResolvedValue('unreachable');
    fixture.componentInstance['reload']();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(element.querySelector('.my-tasks__message--error')?.textContent).toContain(uk.planner.myTasks.error);
  });
});
