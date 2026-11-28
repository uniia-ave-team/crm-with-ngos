import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../../assets/i18n/uk.json';
import { ColleagueSearchComponent } from '../../projects/colleague-search/colleague-search.component';
import { today } from '../../projects/date-range/date-range.component';
import { NewTask } from '../tasks.service';
import { TaskFormComponent } from './task-form.component';

describe('TaskFormComponent', () => {
  function render() {
    TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    const fixture = TestBed.createComponent(TaskFormComponent);
    fixture.componentRef.setInput('colleagues', []);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const submitted: NewTask[] = [];
    fixture.componentInstance.submitted.subscribe((task) => submitted.push(task));
    const field = <T extends HTMLElement>(selector: string) => element.querySelector<T>(selector)!;
    const set = (selector: string, value: string, event = 'input') => {
      const target = field<HTMLInputElement | HTMLSelectElement>(selector);
      target.value = value;
      target.dispatchEvent(new Event(event));
      fixture.detectChanges();
    };
    const submit = () => {
      field<HTMLButtonElement>('.settings-submit').click();
      fixture.detectChanges();
    };
    const search = () => fixture.debugElement.query(By.directive(ColleagueSearchComponent)).componentInstance as ColleagueSearchComponent;
    return { fixture, element, submitted, field, set, submit, search };
  }

  it('starts «To do», of medium priority, from today, and asks the title before sending', () => {
    const { element, field, submitted, submit } = render();
    expect(field<HTMLSelectElement>('#task-status').value).toBe('0');
    expect(field<HTMLSelectElement>('#task-priority').value).toBe('1');
    expect(field<HTMLInputElement>('#task-start').value).toBe(today());
    expect(field<HTMLInputElement>('#task-title').getAttribute('maxlength')).toBe('255');
    expect(field<HTMLTextAreaElement>('#task-description').getAttribute('maxlength')).toBe('10000');
    expect(field<HTMLButtonElement>('.settings-submit').textContent?.trim()).toBe(uk.planner.tasks.add.submit);
    submit();
    expect(submitted).toEqual([]);
    expect(element.querySelector('#task-title-error')?.textContent?.trim()).toBe(uk.common.required);
  });

  it('gives the task with the chosen assignees; a removed one is not sent', () => {
    const { fixture, element, set, submit, submitted, search } = render();
    set('#task-title', '  Афіша ');
    set('#task-description', ' ');
    set('#task-status', '1', 'change');
    set('#task-priority', '3', 'change');
    set('#task-end', '2099-01-01');
    const colleague = (id: string) => ({ id, firstName: 'Імʼя', lastName: id, patronymic: null, avatarUrl: null, isActive: true });
    search().choose.emit(colleague('Бондар'));
    search().choose.emit(colleague('Коваль'));
    fixture.detectChanges();
    expect(search().selectedIds()).toEqual(['Бондар', 'Коваль']);
    search().deselect.emit(colleague('Коваль'));
    search().choose.emit(colleague('Коваль'));
    fixture.detectChanges();
    const remove = element.querySelectorAll<HTMLButtonElement>('.task-form__remove');
    expect(remove[0].getAttribute('aria-label')).toBe(uk.planner.tasks.add.removeAssignee.replace('{{name}}', 'Бондар Імʼя'));
    remove[0].click();
    fixture.detectChanges();
    submit();
    expect(submitted).toEqual([
      { title: 'Афіша', description: null, status: 1, priority: 3, assigneeIds: ['Коваль'], tags: [], startDate: today(), endDate: '2099-01-01' },
    ]);
  });

  it('opens filled with the task to edit and the given label', () => {
    const { fixture, field, submit, submitted } = render();
    fixture.componentRef.setInput('initial', {
      title: 'Афіша',
      description: 'Макет',
      status: 2,
      priority: 3,
      startDate: '2026-12-01',
      endDate: '2026-12-05',
      assignees: [{ id: 'u1', firstName: 'Імʼя', lastName: 'Прізвище', patronymic: null, avatarUrl: null, isActive: true }],
      tags: ['Дизайн'],
    });
    fixture.componentRef.setInput('submitLabelKey', 'planner.dialogs.edit.save');
    fixture.detectChanges();
    expect(field<HTMLInputElement>('#task-title').value).toBe('Афіша');
    expect(field<HTMLSelectElement>('#task-status').value).toBe('2');
    expect(field<HTMLInputElement>('#task-end').value).toBe('2026-12-05');
    expect(field<HTMLButtonElement>('.settings-submit').textContent?.trim()).toBe(uk.planner.dialogs.edit.save);
    submit();
    expect(submitted).toEqual([
      { title: 'Афіша', description: 'Макет', status: 2, priority: 3, assigneeIds: ['u1'], tags: ['Дизайн'], startDate: '2026-12-01', endDate: '2026-12-05' },
    ]);
  });
});
