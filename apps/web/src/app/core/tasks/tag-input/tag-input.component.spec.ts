import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../../assets/i18n/uk.json';
import { TagInputComponent } from './tag-input.component';

describe('TagInputComponent', () => {
  function render(suggestions: string[] = ['Дизайн', 'Друк', 'SMM'], allowNew = true) {
    TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    const fixture = TestBed.createComponent(TagInputComponent);
    fixture.componentRef.setInput('suggestions', suggestions);
    fixture.componentRef.setInput('allowNew', allowNew);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const input = element.querySelector<HTMLInputElement>('.tag-input__input')!;
    const type = (text: string) => {
      input.value = text;
      input.dispatchEvent(new Event('input'));
      fixture.detectChanges();
    };
    const key = (name: string) => {
      input.dispatchEvent(new KeyboardEvent('keydown', { key: name }));
      fixture.detectChanges();
    };
    const tags = () => fixture.componentInstance.tags();
    const options = () => Array.from(element.querySelectorAll<HTMLElement>('.tag-input__option')).map((option) => option.textContent?.trim());
    input.dispatchEvent(new Event('focus'));
    fixture.detectChanges();
    return { fixture, element, input, type, key, tags, options };
  }

  it('turns the text before a comma into a tag, also for a pasted list, keeping the rest in the field', () => {
    const { type, tags, input } = render();
    type('Афіша,');
    expect(tags()).toEqual(['Афіша']);
    expect(input.value).toBe('');
    type('Звіт, Друк ,Ще');
    expect(tags()).toEqual(['Афіша', 'Звіт', 'Друк']);
    expect(input.value).toBe('Ще');
  });

  it('adds a tag once whatever the case, taking the spelling of an existing one', () => {
    const { type, tags } = render();
    type('дизайн,ДИЗАЙН,');
    expect(tags()).toEqual(['Дизайн']);
  });

  it('makes a tag by Enter, removes the last by Backspace in an empty field and by its cross', () => {
    const { fixture, element, type, key, tags } = render();
    type('Афіша');
    key('Enter');
    type('Звіт,');
    expect(tags()).toEqual(['Афіша', 'Звіт']);
    key('Backspace');
    expect(tags()).toEqual(['Афіша']);
    const cross = element.querySelector<HTMLButtonElement>('.tag-input__remove')!;
    expect(cross.getAttribute('aria-label')).toBe(uk.planner.tasks.tags.remove.replace('{{title}}', 'Афіша'));
    cross.click();
    fixture.detectChanges();
    expect(tags()).toEqual([]);
  });

  it('suggests the existing tags with the text, without the chosen ones, and adds one by a click', () => {
    const { fixture, element, type, tags, options } = render();
    expect(options()).toEqual(['Дизайн', 'Друк', 'SMM']);
    type('др');
    expect(options()).toEqual(['Друк']);
    element.querySelector<HTMLElement>('.tag-input__option')!.click();
    fixture.detectChanges();
    expect(tags()).toEqual(['Друк']);
    expect(options()).toEqual(['Дизайн', 'SMM']);
  });

  it('takes only the existing tags when new ones are not allowed', () => {
    const { type, tags } = render(['Дизайн'], false);
    type('Новий,дизайн,');
    expect(tags()).toEqual(['Дизайн']);
  });
});
