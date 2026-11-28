import { CUSTOM_ELEMENTS_SCHEMA, Signal, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideIonicAngular } from '@ionic/angular';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { ProjectMember, ProjectsService } from '../../core/projects/projects.service';
import { TaskFormComponent } from '../../core/tasks/task-form/task-form.component';
import { NewTask, ProjectTask, TasksService } from '../../core/tasks/tasks.service';
import { TaskAccessService } from '../../core/tasks/task-access.service';
import { CurrentUserService } from '../../core/users/current-user.service';
import { DialogHeaderComponent } from '../../shared/dialog-header/dialog-header.component';
import { ModalComponent } from '../../shared/modal/modal.component';
import { TaskDialogComponent } from './task-dialog.component';

describe('TaskDialogComponent', () => {
  const topic = { id: 't1', title: 'Розробка', order: 0 };
  const member: ProjectMember = {
    userId: 'u1',
    firstName: 'Імʼя',
    lastName: 'Прізвище',
    patronymic: null,
    avatarUrl: null,
    roleTitle: null,
    claim: 1,
    isActive: true,
  };
  const task: NewTask = { title: 'Афіша', description: null, status: 0, priority: 1, assigneeIds: [], tags: [], startDate: '2026-12-01', endDate: null };

  /** Вікно для проєкту з доступністю `visibility` і правами `permissions`; відкривається насправді (з анімацією Ionic). */
  const existing: ProjectTask = {
    id: 'k1',
    topicId: 't1',
    title: 'Афіша',
    description: null,
    status: 0,
    priority: 1,
    startDate: '2026-12-01',
    endDate: null,
    assignees: [{ id: 'u1', firstName: 'Імʼя', lastName: 'Прізвище', patronymic: null, avatarUrl: null }],
    tags: [], unreadComments: 0,
  };

  async function open(visibility: 0 | 1 | 2, permissions = ['Permissions.ViewUser'], edit = false) {
    TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideIonicAngular(), provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    (TestBed.inject(CurrentUserService) as { permissions: Signal<readonly string[]> }).permissions = signal(permissions);
    TestBed.inject(TaskAccessService).setClaim('p1', 1);
    const projects = TestBed.inject(ProjectsService);
    vi.spyOn(projects, 'state').mockReturnValue({ status: 'ready', projects: [{ id: 'p1', title: 'Явір', visibility }] });
    const members = vi.spyOn(projects, 'members').mockResolvedValue([member]);
    vi.spyOn(projects, 'tags').mockResolvedValue([{ id: 'g1', title: 'Дизайн' }]);
    const tagIds = vi.spyOn(projects, 'tagIds').mockResolvedValue(['g1']);
    const create = vi.spyOn(TestBed.inject(TasksService), 'create').mockResolvedValue({ id: 'task-1' });
    const update = vi.spyOn(TestBed.inject(TasksService), 'update').mockResolvedValue('done');
    const fixture = TestBed.createComponent(TaskDialogComponent);
    fixture.detectChanges();
    const closeModal = vi.spyOn(fixture.debugElement.query(By.directive(ModalComponent)).componentInstance as ModalComponent, 'close').mockResolvedValue();
    if (edit) {
      fixture.componentInstance.edit('p1', existing);
    } else {
      fixture.componentInstance.create('p1', topic);
    }
    const child = <T>(type: new (...args: never[]) => T) =>
      vi.waitFor(
        () => {
          fixture.detectChanges();
          const found = fixture.debugElement.query(By.directive(type))?.componentInstance as T | undefined;
          expect(found).toBeDefined();
          return found!;
        },
        { timeout: 3000 },
      );
    return { fixture, members, create, update, closeModal, child, tagIds };
  }

  // Вікно, що ще відкривається, не прибирати: Ionic доробить відкриття вже без нього.
  afterEach(async () => {
    await new Promise((resolve) => setTimeout(resolve, 50));
    document.querySelectorAll('ion-modal').forEach((modal) => modal.remove());
    vi.restoreAllMocks();
  });

  it('is named after the column and looks for the assignees among all the colleagues in a public project', async () => {
    const { members, child } = await open(2);
    expect((await child(DialogHeaderComponent)).title()).toBe(uk.planner.tasks.add.title.replace('{{title}}', 'Розробка'));
    expect((await child(TaskFormComponent)).colleagues()).toBeNull();
    expect(members).not.toHaveBeenCalled();
  });

  it('looks for the assignees among the members in a private project, and without the right to see the colleagues', async () => {
    const expected = [expect.objectContaining({ id: 'u1', lastName: 'Прізвище' })];
    const first = await open(0);
    await vi.waitFor(async () => expect((await first.child(TaskFormComponent)).colleagues()).toEqual(expected));
    expect(first.members).toHaveBeenCalledWith('p1');
    TestBed.resetTestingModule();
    const second = await open(2, []);
    await vi.waitFor(async () => expect((await second.child(TaskFormComponent)).colleagues()).toEqual(expected));
  });

  it('creates the task in the column and closes; a failure keeps the window with the reason', async () => {
    const { fixture, create, closeModal, child } = await open(2);
    const form = await child(TaskFormComponent);
    create.mockResolvedValueOnce('invalid');
    form.submitted.emit(task);
    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(form.error()).toBe('planner.tasks.failure.invalid');
    });
    expect(closeModal).not.toHaveBeenCalled();
    form.submitted.emit(task);
    await vi.waitFor(() => expect(closeModal).toHaveBeenCalled());
    expect(create).toHaveBeenLastCalledWith('p1', 't1', task, ['g1']);
  });

  it('«Edit» fills the form with the task and «Save», sending the changes and closing; nothing changed — no request', async () => {
    const { update, closeModal, child } = await open(2, ['Permissions.ViewUser', 'Permissions.UpdateTask'], true);
    expect((await child(DialogHeaderComponent)).title()).toBe(uk.planner.tasks.edit.title);
    const form = await child(TaskFormComponent);
    expect(form.submitLabelKey()).toBe('planner.dialogs.edit.save');
    expect(form.initial()).toEqual({
      title: 'Афіша',
      description: null,
      status: 0,
      priority: 1,
      startDate: '2026-12-01',
      endDate: null,
      assignees: [expect.objectContaining({ id: 'u1', isActive: true })],
      tags: [],
    });
    form.submitted.emit({ ...task, assigneeIds: ['u1'] });
    expect(update).not.toHaveBeenCalled();
    expect(closeModal).toHaveBeenCalled();
    closeModal.mockClear();
    const changed = { ...task, title: 'Звіт', assigneeIds: ['u1'] };
    form.submitted.emit(changed);
    await vi.waitFor(() => expect(closeModal).toHaveBeenCalled());
    expect(update).toHaveBeenCalledWith('p1', existing, changed, ['g1']);
  });

  it('opens no «Edit» without the right or for a viewer of the project', async () => {
    const { fixture } = await open(2, ['Permissions.ViewUser'], true);
    expect(fixture.componentInstance['dialog']()).toBeNull();
    TestBed.resetTestingModule();
    const viewer = await open(2, ['Permissions.UpdateTask'], false);
    TestBed.inject(TaskAccessService).setClaim('p1', 0);
    viewer.fixture.componentInstance.edit('p1', existing);
    viewer.fixture.detectChanges();
    expect(viewer.fixture.componentInstance['dialog']()).toEqual(expect.objectContaining({ mode: 'create' }));
  });

  it('suggests the tags of the project, allows new ones only with Permissions.UpdateProject, and turns the titles into ids before sending', async () => {
    const { tagIds, create, child } = await open(2, ['Permissions.ViewUser']);
    const form = await child(TaskFormComponent);
    await vi.waitFor(() => expect(form.tagSuggestions()).toEqual(['Дизайн']));
    expect(form.allowNewTags()).toBe(false);
    tagIds.mockResolvedValueOnce('forbidden');
    form.submitted.emit({ ...task, tags: ['Дизайн', 'Новий'] });
    await vi.waitFor(() => expect(form.error()).toBe('planner.tasks.failure.forbidden'));
    expect(tagIds).toHaveBeenCalledWith('p1', ['Дизайн', 'Новий']);
    expect(create).not.toHaveBeenCalled();
    TestBed.resetTestingModule();
    const second = await open(2, ['Permissions.UpdateProject']);
    expect((await second.child(TaskFormComponent)).allowNewTags()).toBe(true);
  });
});
