import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { TaskAssignee } from '../../core/tasks/tasks.service';
import { TaskAssigneesComponent } from './task-assignees.component';

describe('TaskAssigneesComponent', () => {
  const assignee = (id: string): TaskAssignee => ({ id, firstName: 'Ім' + id, lastName: 'Пр' + id, patronymic: null, avatarUrl: null });

  function render(assignees: readonly TaskAssignee[]) {
    TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    const fixture = TestBed.createComponent(TaskAssigneesComponent);
    fixture.componentRef.setInput('assignees', assignees);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows two avatars and «...» for the rest, named after all of them', () => {
    const element = render([assignee('1'), assignee('2'), assignee('3')]);
    expect(element.querySelectorAll('app-avatar')).toHaveLength(2);
    expect(element.querySelector('.task-assignees__item--more')?.textContent?.trim()).toBe(uk.planner.members.moreMark);
    expect(element.querySelector('.task-assignees')!.getAttribute('aria-label')).toBe(`${uk.planner.task.assignees}: Пр1 Ім1, Пр2 Ім2, Пр3 Ім3`);
  });

  it('shows an empty dashed avatar without assignees', () => {
    const element = render([]);
    expect(element.querySelector('app-avatar')).toBeNull();
    const empty = element.querySelector('.task-assignees__item--more')!;
    expect(empty.textContent?.trim()).toBe('');
    expect(element.querySelector('.task-assignees')!.getAttribute('aria-label')).toBe(uk.planner.task.noAssignees);
  });
});
