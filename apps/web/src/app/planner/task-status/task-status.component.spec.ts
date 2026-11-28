import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { ProjectTask, TasksService } from '../../core/tasks/tasks.service';
import { TaskStatusComponent } from './task-status.component';

describe('TaskStatusComponent', () => {
  const task: ProjectTask = {
    id: 'a',
    topicId: 't1',
    title: 'Афіша',
    description: 'Макет',
    status: 0,
    priority: 2,
    startDate: '2026-03-20',
    endDate: null,
    assignees: [{ id: 'u1', firstName: 'Імʼя', lastName: 'Прізвище', patronymic: null, avatarUrl: null }],
    tags: [{ id: 'g1', title: 'Дизайн' }],
    unreadComments: 0,
  };

  function render(value: ProjectTask, canPromote = true) {
    TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    const update = vi.spyOn(TestBed.inject(TasksService), 'update');
    const fixture = TestBed.createComponent(TaskStatusComponent);
    fixture.componentRef.setInput('task', value);
    fixture.componentRef.setInput('projectId', 'p1');
    fixture.componentRef.setInput('canPromote', canPromote);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const tag = () => element.querySelector<HTMLElement>('.task-status')!;
    const icon = () => (tag().querySelector('ion-icon') as unknown as { name: string } | null)?.name ?? null;
    const fire = (name: string) => {
      tag().dispatchEvent(new Event(name));
      fixture.detectChanges();
    };
    return { fixture, tag, icon, fire, update };
  }

  afterEach(() => vi.restoreAllMocks());

  it('shows the next status in blue with an arrow up on hover, and the current one again after it', () => {
    const { tag, icon, fire } = render(task);
    expect(tag().textContent?.trim()).toBe(uk.planner.taskStatus['0']);
    fire('mouseenter');
    expect(tag().textContent?.trim()).toBe(uk.planner.taskStatus['1']);
    expect(tag().classList).toContain('task-status--raised');
    expect(icon()).toBe('arrow-up-outline');
    fire('mouseleave');
    expect(tag().textContent?.trim()).toBe(uk.planner.taskStatus['0']);
    expect(icon()).toBeNull();
  });

  it('raises «In progress» to «Done» by a click with the rest of the task as it is, keeping the raised tag with a cloud until the task comes back', async () => {
    const { fixture, tag, icon, fire, update } = render({ ...task, status: 1 });
    let finish: (value: 'done') => void = () => undefined;
    update.mockReturnValue(new Promise((resolve) => (finish = resolve)));
    fire('mouseenter');
    expect(tag().classList).toContain('task-status--raised');
    const click = new MouseEvent('click', { bubbles: true });
    const outer = vi.fn();
    fixture.nativeElement.addEventListener('click', outer);
    tag().dispatchEvent(click);
    fixture.detectChanges();
    expect(update).toHaveBeenCalledWith(
      'p1',
      expect.objectContaining({ id: 'a' }),
      { title: 'Афіша', description: 'Макет', status: 3, priority: 2, assigneeIds: ['u1'], tags: ['Дизайн'], startDate: '2026-03-20', endDate: null },
      ['g1'],
    );
    // Клік не відкриває саме завдання.
    expect(outer).not.toHaveBeenCalled();
    expect(tag().textContent?.trim()).toBe(uk.planner.taskStatus['3']);
    expect(icon()).toBe('cloud-sync-outline');
    // Поки чекаємо — уже кольори нового стану, не синя підказка.
    expect(tag().classList).not.toContain('task-status--raised');
    expect(tag().getAttribute('data-status')).toBe('3');
    finish('done');
    await fixture.whenStable();
    fixture.componentRef.setInput('task', { ...task, status: 3 });
    fixture.detectChanges();
    expect(tag().textContent?.trim()).toBe(uk.planner.taskStatus['3']);
    expect(icon()).toBeNull();
  });

  it('shows no next status after a click while the pointer stays, until it leaves and comes back', async () => {
    const { fixture, tag, fire, update } = render(task);
    update.mockResolvedValue('done');
    fire('mouseenter');
    tag().click();
    await fixture.whenStable();
    fixture.componentRef.setInput('task', { ...task, status: 1 });
    fixture.detectChanges();
    // Курсор ще на тегу: «В процесі» у своїх кольорах, без підказки «Готово».
    expect(tag().textContent?.trim()).toBe(uk.planner.taskStatus['1']);
    expect(tag().classList).not.toContain('task-status--raised');
    fire('mouseleave');
    fire('mouseenter');
    expect(tag().textContent?.trim()).toBe(uk.planner.taskStatus['3']);
  });

  it('turns back on a failure', async () => {
    const { fixture, tag, update } = render(task);
    update.mockResolvedValue('error');
    tag().click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(tag().textContent?.trim()).toBe(uk.planner.taskStatus['0']);
  });

  it('is a plain tag without the right, and for the statuses with nothing next', () => {
    expect(render(task, false).tag().tagName).toBe('SPAN');
    TestBed.resetTestingModule();
    expect(render({ ...task, status: 2 }).tag().tagName).toBe('SPAN');
    TestBed.resetTestingModule();
    expect(render({ ...task, status: 3 }).tag().tagName).toBe('SPAN');
  });
});
