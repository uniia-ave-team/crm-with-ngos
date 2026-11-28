import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../../assets/i18n/uk.json';
import { ProjectTask } from '../../../core/tasks/tasks.service';
import { TaskCardComponent, formatDay } from './task-card.component';

describe('TaskCardComponent', () => {
  const task: ProjectTask = {
    id: 'a',
    topicId: 't1',
    title: 'Афіша',
    description: null,
    status: 2,
    priority: 3,
    startDate: '2026-03-20',
    endDate: '2026-03-25',
    assignees: ['1', '2', '3'].map((id) => ({ id, firstName: 'Ім' + id, lastName: 'Пр' + id, patronymic: null, avatarUrl: null })),
    tags: [], unreadComments: 2,
  };

  function render(value: ProjectTask) {
    TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    const fixture = TestBed.createComponent(TaskCardComponent);
    fixture.componentRef.setInput('task', value);
    fixture.componentRef.setInput('projectId', 'p1');
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const text = (selector: string) => element.querySelector(selector)?.textContent?.replace(/\s+/g, ' ').trim();
    const day = (value: string) => formatDay(value);
    return { fixture, element, text, day };
  }

  it('writes a day as DD.MM.YYYY', () => {
    expect(formatDay('2026-03-05')).toBe('05.03.2026');
  });

  it('shows the title, the dates, the status, the priority, the new comments and the assignees', () => {
    const { element, text, day } = render(task);
    expect(text('.task-card__title')).toBe('Афіша');
    expect(text('.task-card__dates')).toBe(`${day('2026-03-20')} - ${day('2026-03-25')}`);
    expect(text('.task-status')).toBe(uk.planner.taskStatus['2']);
    expect(element.querySelector('.task-status')!.getAttribute('data-status')).toBe('2');
    expect(text('.task-priority')).toBe(uk.planner.taskPriorityLabel['3']);
    expect(text('.task-card__comments')).toBe(uk.planner.task.unreadComments.replace('{{count}}', '2'));
    expect(element.querySelector('.task-card__comments')!.classList).toContain('task-card__comments--unread');
    expect(element.querySelectorAll('app-task-assignees app-avatar')).toHaveLength(2);
  });

  it('says «Comments» without new ones, and «from» without the end', () => {
    const { element, text, day } = render({ ...task, endDate: null, tags: [], unreadComments: 0, assignees: [] });
    expect(text('.task-card__dates')).toBe(uk.planner.task.from.replace('{{start}}', day('2026-03-20')));
    expect(text('.task-card__comments')).toBe(uk.planner.task.comments);
    expect(element.querySelector('.task-card__comments')!.classList).not.toContain('task-card__comments--unread');
    expect(element.querySelector('app-task-assignees')).not.toBeNull();
  });

  it('gives the three dots with the task without opening it, and opens it by a click or Enter on the card', () => {
    const { fixture, element } = render(task);
    const menus: ProjectTask[] = [];
    let opened = 0;
    fixture.componentInstance.menu.subscribe((value) => menus.push(value.task));
    fixture.componentInstance.open.subscribe(() => opened++);
    const button = element.querySelector<HTMLButtonElement>('.task-card__menu')!;
    expect(button.getAttribute('aria-label')).toBe(uk.planner.task.actions.replace('{{title}}', 'Афіша'));
    button.click();
    expect(menus).toEqual([task]);
    expect(opened).toBe(0);
    const card = element.querySelector<HTMLElement>('.task-card')!;
    card.click();
    card.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    expect(opened).toBe(2);
  });
});
