import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { TaskComment } from '../../core/tasks/tasks.service';
import { TaskCommentComponent } from './task-comment.component';

describe('TaskCommentComponent', () => {
  const comment: TaskComment = { id: 'c1', userId: 'me', firstName: 'Імʼя', lastName: 'Прізвище', avatarUrl: null, content: 'Готово', createdAt: '2026-03-20T10:00:00Z' };

  function render(own: boolean, editing = false) {
    TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    const fixture = TestBed.createComponent(TaskCommentComponent);
    fixture.componentRef.setInput('comment', comment);
    fixture.componentRef.setInput('author', 'Прізвище Імʼя');
    fixture.componentRef.setInput('own', own);
    fixture.componentRef.setInput('editing', editing);
    fixture.detectChanges();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  }

  it('has the three dots only on an own comment', () => {
    expect(render(false).element.querySelector('.comment__menu')).toBeNull();
    TestBed.resetTestingModule();
    const { fixture, element } = render(true);
    const menus: Event[] = [];
    fixture.componentInstance.menu.subscribe((event) => menus.push(event));
    const button = element.querySelector<HTMLButtonElement>('.comment__menu')!;
    expect(button.getAttribute('aria-label')).toBe(uk.planner.details.commentActions.label);
    button.click();
    expect(menus).toHaveLength(1);
  });

  it('turns the text into a field with the text and the dots into «Send» while editing', () => {
    const { fixture, element } = render(true, true);
    const saved: string[] = [];
    fixture.componentInstance.save.subscribe((value) => saved.push(value));
    expect(element.querySelector('.comment__text')).toBeNull();
    expect(element.querySelector('.comment__menu')).toBeNull();
    const input = element.querySelector<HTMLInputElement>('.comment__input')!;
    expect(input.value).toBe('Готово');
    input.value = 'Готово, перевірено';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    element.querySelector<HTMLButtonElement>('.comment__send')!.click();
    expect(saved).toEqual(['Готово, перевірено']);
  });
});
