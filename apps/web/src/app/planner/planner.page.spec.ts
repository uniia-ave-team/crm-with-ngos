import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { CUSTOM_ELEMENTS_SCHEMA, Signal, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { Router, provideRouter } from '@angular/router';
import { provideIonicAngular } from '@ionic/angular';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../assets/i18n/uk.json';
import { ProjectMember, ProjectsService, ProjectsState } from '../core/projects/projects.service';
import { CurrentUserService } from '../core/users/current-user.service';
import { TaskDetailsComponent } from './task-details/task-details.component';
import { PlannerPage } from './planner.page';

describe('PlannerPage', () => {
  const member = (id: string): ProjectMember => ({
    userId: id,
    firstName: 'Ім' + id,
    lastName: 'Пр' + id,
    patronymic: null,
    avatarUrl: null,
    roleTitle: null,
    claim: 0,
    isActive: true,
  });

  async function render(
    options: { id?: string; view?: string; members?: readonly ProjectMember[]; permissions?: string[]; projects?: ProjectsState['projects'] } = {},
  ) {
    TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [
        provideIonicAngular(),
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslateService({ lang: 'uk', fallbackLang: 'uk' }),
      ],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    (TestBed.inject(CurrentUserService) as { permissions: Signal<readonly string[]> }).permissions = signal(options.permissions ?? []);
    const service = TestBed.inject(ProjectsService);
    (service as unknown as { state: () => ProjectsState }).state = signal<ProjectsState>({
      status: 'ready',
      projects: options.projects ?? [
        { id: 'p1', title: 'Явір' },
        { id: 'p2', title: 'Унія' },
      ],
    });
    vi.spyOn(service, 'load').mockResolvedValue();
    const members = vi.spyOn(service, 'members').mockResolvedValue(options.members ?? []);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const fixture = TestBed.createComponent(PlannerPage);
    fixture.componentRef.setInput('id', options.id);
    fixture.componentRef.setInput('view', options.view);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const tabs = () => Array.from(element.querySelectorAll<HTMLButtonElement>('.planner__title .planner__tab'));
    return { fixture, element, members, tabs, navigate };
  }

  afterEach(() => vi.restoreAllMocks());

  it('opens the first project of the list at once (without a record in the history) when none is chosen', async () => {
    const { navigate, tabs } = await render();
    expect(navigate).toHaveBeenCalledWith('/planner/projects/p1', { replaceUrl: true });
    expect(tabs()).toHaveLength(0);
  });

  it('shows only the empty state, without a title, when there are no projects', async () => {
    const { element, navigate } = await render({ projects: [] });
    expect(navigate).not.toHaveBeenCalled();
    expect(element.querySelector('app-empty-state.planner__empty .empty-state__text')?.textContent?.trim()).toBe('Тут поки порожньо');
    expect(element.querySelector('ion-title')).toBeNull();
  });

  it('does not leave a chosen project', async () => {
    const { navigate, element } = await render({ id: 'p2' });
    expect(navigate).not.toHaveBeenCalled();
    expect(element.querySelector('app-empty-state')).toBeNull();
  });

  it('gives a project three tabs in place of the title, the board chosen at first, and the members of that project', async () => {
    const { tabs, members } = await render({ id: 'p1' });
    expect(tabs().map((tab) => tab.textContent?.trim())).toEqual([uk.planner.tabs.my, uk.planner.tabs.board, uk.planner.tabs.about]);
    expect(tabs().map((tab) => tab.getAttribute('aria-selected'))).toEqual(['false', 'true', 'false']);
    // «Про проєкт» запланована: у розмітці, але прихована.
    expect(tabs().map((tab) => tab.classList.contains('planned'))).toEqual([false, false, true]);
    expect(tabs()[1].classList).toContain('planner__tab--active');
    expect(members).toHaveBeenCalledWith('p1');
  });

  it('repeats the tabs in the bottom panel for phones, and names the project in the header there', async () => {
    const { element } = await render({ id: 'p1', view: 'my' });
    const bottom = Array.from(element.querySelectorAll<HTMLButtonElement>('ion-footer.planner__footer .planner__tab'));
    expect(bottom.map((tab) => [tab.textContent?.trim(), tab.getAttribute('aria-selected'), tab.classList.contains('planned')])).toEqual([
      [uk.planner.tabs.my, 'true', false],
      [uk.planner.tabs.board, 'false', false],
      [uk.planner.tabs.about, 'false', true],
    ]);
    expect(element.querySelector('.planner__project-title')?.textContent?.trim()).toBe('Явір');
  });

  it('chooses a tab by putting it into the address (without a record in the history)', async () => {
    const { tabs } = await render({ id: 'p1', view: 'my' });
    expect(tabs()[0].getAttribute('aria-selected')).toBe('true');
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    tabs()[1].click();
    expect(navigate).toHaveBeenCalledWith([], { queryParams: { view: 'board' }, queryParamsHandling: 'merge', replaceUrl: true });
  });

  it('shows only the active members', async () => {
    const { element } = await render({ id: 'p1', members: [member('1'), { ...member('x'), isActive: false }, member('2')] });
    const avatars = element.querySelector('app-member-avatars')!;
    expect(avatars.querySelectorAll('app-avatar')).toHaveLength(2);
  });

  it('gives «Create» only on the board and with Permissions.UpdateProject; it asks for the window «New column»', async () => {
    expect((await render({ id: 'p1' })).element.querySelector('app-action-button')).toBeNull();
    TestBed.resetTestingModule();
    expect((await render({ id: 'p1', view: 'my', permissions: ['Permissions.UpdateProject'] })).element.querySelector('app-action-button[icon="add-outline"]')).toBeNull();
    TestBed.resetTestingModule();
    const { element } = await render({ id: 'p1', permissions: ['Permissions.UpdateProject'] });
    const button = element.querySelector<HTMLElement>('app-action-button')!;
    expect(button.getAttribute('label') ?? button.textContent).toContain(uk.planner.createTask);
    const request = vi.spyOn(TestBed.inject(ProjectsService), 'requestAction');
    button.click();
    expect(request).toHaveBeenCalledWith('topic', { id: 'p1', title: 'Явір' });
  });

  it('has the sort button (for a phone) only on «My tasks»', async () => {
    expect((await render({ id: 'p1', view: 'board' })).element.querySelector('.planner__sort-toggle')).toBeNull();
    TestBed.resetTestingModule();
    expect((await render({ id: 'p1', view: 'my' })).element.querySelector('.planner__sort-toggle')).not.toBeNull();
  });

  it('has the search on «My tasks» and on the board', async () => {
    expect((await render({ id: 'p1', view: 'board' })).element.querySelector('.search-header__toggle')).not.toBeNull();
    TestBed.resetTestingModule();
    expect((await render({ id: 'p1', view: 'my' })).element.querySelector('.search-header__toggle')).not.toBeNull();
  });

  it('shows the board for the planned «About project» in the address', async () => {
    const { tabs } = await render({ id: 'p1', view: 'about' });
    expect(tabs().map((tab) => tab.getAttribute('aria-selected'))).toEqual(['false', 'true', 'false']);
  });

  it('asks for the members again after a change of the access', async () => {
    const { fixture, members } = await render({ id: 'p1', members: [member('1')] });
    expect(members).toHaveBeenCalledTimes(1);
    (TestBed.inject(ProjectsService) as unknown as { membersVersionSignal: { update(fn: (v: number) => number): void } }).membersVersionSignal.update((v) => v + 1);
    fixture.detectChanges();
    await fixture.whenStable();
    expect(members).toHaveBeenCalledTimes(2);
  });

  it('opens the access of the project by a click on the members', async () => {
    const { element } = await render({ id: 'p1', members: [member('1')] });
    const request = vi.spyOn(TestBed.inject(ProjectsService), 'requestAction');
    element.querySelector<HTMLButtonElement>('app-member-avatars button')!.click();
    expect(request).toHaveBeenCalledWith('access', { id: 'p1', title: 'Явір' });
    expect(element.querySelector('app-member-avatars button')!.getAttribute('aria-label')).toContain(uk.planner.actions.viewAccess);
  });

  it('opens the side window for a task opened on the board', async () => {
    const { fixture } = await render({ id: 'p1' });
    const details = fixture.debugElement.query(By.directive(TaskDetailsComponent)).componentInstance as TaskDetailsComponent;
    const open = vi.spyOn(details, 'open').mockImplementation(() => undefined);
    const task = { id: 'a', topicId: 't1', title: 'Афіша', description: null, status: 0, priority: 1, startDate: '2026-03-20', endDate: null, assignees: [], tags: [], unreadComments: 0 } as const;
    (fixture.debugElement.query(By.css('app-planner-board')).componentInstance as { openTask: { emit(value: unknown): void } }).openTask.emit(task);
    fixture.detectChanges();
    expect(open).toHaveBeenCalledWith(task);
  });
});
