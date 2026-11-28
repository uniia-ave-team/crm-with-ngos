import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../../assets/i18n/uk.json';
import { UsersService } from '../../users/users.service';
import { Colleague, ColleagueSearchComponent, ColleagueSource } from './colleague-search.component';

describe('ColleagueSearchComponent with a ready list', () => {
  const colleague = (id: string, lastName: string, isActive = true): Colleague => ({
    id,
    firstName: 'Імʼя',
    lastName,
    patronymic: null,
    avatarUrl: null,
    isActive,
  });

  function render(source: ColleagueSource, excludeIds: string[] = []) {
    TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    const list = vi.spyOn(TestBed.inject(UsersService), 'list');
    const fixture = TestBed.createComponent(ColleagueSearchComponent);
    fixture.componentRef.setInput('source', source);
    fixture.componentRef.setInput('excludeIds', excludeIds);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const input = element.querySelector<HTMLInputElement>('.colleague-search__input')!;
    const names = () => Array.from(element.querySelectorAll('.colleague-search__name')).map((name) => name.textContent?.trim());
    const type = (text: string) => {
      input.value = text;
      input.dispatchEvent(new Event('input'));
      fixture.detectChanges();
    };
    input.dispatchEvent(new Event('focus'));
    fixture.detectChanges();
    return { fixture, element, input, names, type, list };
  }

  afterEach(() => vi.restoreAllMocks());

  it('looks among the given colleagues at once, without the server, the disabled and the excluded', () => {
    const { names, type, list } = render([colleague('a', 'Бондар'), colleague('b', 'Коваль'), colleague('c', 'Мельник'), colleague('off', 'Бойко', false)], ['c']);
    expect(names()).toEqual(['Бондар Імʼя', 'Коваль Імʼя']);
    type('ков');
    expect(names()).toEqual(['Коваль Імʼя']);
    expect(list).not.toHaveBeenCalled();
  });

  it('gives the chosen colleague, clears the field and closes the list', () => {
    const { fixture, element, input } = render([colleague('a', 'Бондар')]);
    const chosen: Colleague[] = [];
    fixture.componentInstance.choose.subscribe((value) => chosen.push(value));
    element.querySelector<HTMLButtonElement>('.colleague-search__option')!.click();
    fixture.detectChanges();
    expect(chosen.map((value) => value.id)).toEqual(['a']);
    expect(input.value).toBe('');
    expect(element.querySelector('.colleague-search__list')).toBeNull();
  });

  it('keeps the list open when choosing several, marking the chosen ones with a cross to unpick', () => {
    const { fixture, element, input } = render([colleague('a', 'Бондар'), colleague('b', 'Коваль')]);
    fixture.componentRef.setInput('multiple', true);
    const chosen: string[] = [];
    const unpicked: string[] = [];
    fixture.componentInstance.choose.subscribe((value) => chosen.push(value.id));
    fixture.componentInstance.deselect.subscribe((value) => unpicked.push(value.id));
    input.value = 'Бонд';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    element.querySelector<HTMLElement>('.colleague-search__option')!.click();
    fixture.componentRef.setInput('selectedIds', ['a']);
    fixture.detectChanges();
    expect(chosen).toEqual(['a']);
    // Список відкритий, пошук той самий, а вибраний — у ньому з позначкою.
    expect(input.value).toBe('Бонд');
    const option = element.querySelector<HTMLElement>('.colleague-search__option')!;
    expect(option.classList).toContain('colleague-search__option--selected');
    expect(option.getAttribute('aria-selected')).toBe('true');
    expect(option.querySelector('.colleague-search__check')).not.toBeNull();
    // Вибраного вдруге не вибрати, а хрестик знімає вибір.
    option.click();
    expect(chosen).toEqual(['a']);
    const cross = option.querySelector<HTMLButtonElement>('.colleague-search__unpick')!;
    expect(cross.getAttribute('aria-label')).toBe(uk.planner.access.suggestions.deselect.replace('{{name}}', 'Бондар Імʼя'));
    cross.click();
    expect(unpicked).toEqual(['a']);
    expect(chosen).toEqual(['a']);
  });

  it('says the list is loading or failed', () => {
    expect(render('loading').element.querySelector('.colleague-search__message')?.textContent?.trim()).toBe(uk.planner.access.suggestions.loading);
    TestBed.resetTestingModule();
    expect(render('failed').element.querySelector('.colleague-search__message')?.textContent?.trim()).toBe(uk.planner.access.suggestions.error);
  });
});
