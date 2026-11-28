import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../../assets/i18n/uk.json';
import { UserSummary, UsersService } from '../../users/users.service';
import { COLLEAGUE_SEARCH_DELAY_MS } from '../colleague-search/colleague-search.component';
import { ProjectMemberSearchComponent } from './project-member-search.component';

describe('ProjectMemberSearchComponent', () => {
  const user = (id: string, overrides: Partial<UserSummary> = {}): UserSummary => ({
    id,
    email: `${id}@b.org`,
    firstName: 'Імʼя',
    lastName: 'Прізвище',
    patronymic: null,
    phoneNumber: null,
    country: null,
    emergencyContact: null,
    pronouns: null,
    pronounCategory: null,
    avatarUrl: null,
    roles: [],
    isActive: true,
    ...overrides,
  });

  function render(memberIds: string[] = []) {
    TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    const list = vi
      .spyOn(TestBed.inject(UsersService), 'list')
      .mockResolvedValue({
        items: [user('u1', { patronymic: 'Іванович' }), user('u2'), user('member'), user('off', { isActive: false })],
        hasNextPage: false,
      });
    const fixture = TestBed.createComponent(ProjectMemberSearchComponent);
    fixture.componentRef.setInput('memberIds', memberIds);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const input = () => element.querySelector<HTMLInputElement>('.colleague-search__input')!;
    const settle = async () => {
      await fixture.whenStable();
      fixture.detectChanges();
    };
    const options = () => Array.from(element.querySelectorAll<HTMLButtonElement>('.colleague-search__option'));
    return { fixture, element, input, settle, options, list };
  }

  afterEach(() => vi.restoreAllMocks());

  it('is the search, the level of the new member inside (an editor at first) and the disabled «Add»', () => {
    const { element, input } = render();
    expect(input().placeholder).toBe(uk.planner.access.searchPlaceholder);
    const select = element.querySelector<HTMLSelectElement>('.member-search__tag select')!;
    expect(select.value).toBe('1');
    expect(select.getAttribute('aria-label')).toBe(uk.planner.access.newClaim);
    const button = element.querySelector<HTMLButtonElement>('.member-search__add-button')!;
    expect(button.textContent?.trim()).toBe(uk.planner.access.add);
    // Нікого не вибрано — «Додати» вимкнена.
    expect(button.disabled).toBe(true);
  });

  it('opens the colleagues on focus (by name, without a search term): the avatar and the full name, without the members and the disabled', async () => {
    const { input, settle, options, list, element } = render(['member']);
    input().dispatchEvent(new Event('focus'));
    await settle();
    expect(list).toHaveBeenCalledWith(1, 20, { orderBy: 'LastName', sortOrder: 'asc' }, null);
    expect(input().getAttribute('aria-expanded')).toBe('true');
    expect(options().map((option) => option.querySelector('.colleague-search__name')?.textContent?.trim())).toEqual([
      'Прізвище Імʼя Іванович',
      'Прізвище Імʼя',
    ]);
    expect(options()[0].querySelector('app-avatar')).not.toBeNull();
    expect(element.querySelector('.colleague-search__list')?.getAttribute('role')).toBe('listbox');
  });

  it('looks for the typed text after a pause, with the list attached to the field', async () => {
    const { input, settle, list, fixture, element } = render();
    input().value = 'Прізв';
    input().dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(list).not.toHaveBeenCalled();
    await vi.waitFor(() => expect(list).toHaveBeenCalledWith(1, 20, { orderBy: 'LastName', sortOrder: 'asc' }, 'Прізв'), {
      timeout: COLLEAGUE_SEARCH_DELAY_MS * 5,
    });
    await settle();
    // Список прикріплений до поля: поле без нижніх заокруглень, поки він відкритий.
    expect(element.querySelector('.colleague-search__field')!.classList).toContain('colleague-search__field--open');
  });

  it('shows the chosen colleague in place of the search (avatar, full name, the role title under it) and lets no other be chosen', async () => {
    const { input, settle, options, fixture, element } = render(['member']);
    input().dispatchEvent(new Event('focus'));
    await settle();
    options()[0].click();
    fixture.detectChanges();
    const choice = element.querySelector('app-member-choice')!;
    expect(choice.querySelector('.choice__name')?.textContent?.trim()).toBe('Прізвище Імʼя Іванович');
    expect(choice.querySelector('app-avatar')).not.toBeNull();
    const role = choice.querySelector<HTMLInputElement>('.choice__role')!;
    expect(role.placeholder).toBe(uk.planner.access.rolePlaceholder);
    expect(role.getAttribute('maxlength')).toBe('150');
    // Пошуку й списку вже нема — іншого не вибрати; вибір рівня лишається.
    expect(element.querySelector('.colleague-search__input')).toBeNull();
    expect(element.querySelector('.colleague-search__list')).toBeNull();
    expect(element.querySelector('.member-search__chosen > .member-search__tag select')).not.toBeNull();
  });

  it('brings the search back by the cross next to «Add»', async () => {
    const { input, settle, options, fixture, element } = render(['member']);
    expect(element.querySelector('.member-search__reset')).toBeNull();
    input().dispatchEvent(new Event('focus'));
    await settle();
    options()[0].click();
    fixture.detectChanges();
    const reset = element.querySelector<HTMLButtonElement>('.member-search__reset')!;
    expect(reset.getAttribute('aria-label')).toBe(uk.planner.access.resetChoice);
    reset.click();
    fixture.detectChanges();
    expect(element.querySelector('app-member-choice')).toBeNull();
    expect(input()).not.toBeNull();
    expect(element.querySelector<HTMLButtonElement>('.member-search__add-button')!.disabled).toBe(true);
  });

  it('says nobody was found, and closes the list on Escape', async () => {
    const { input, settle, element, list, fixture } = render(['u1', 'u2', 'member']);
    input().dispatchEvent(new Event('focus'));
    await settle();
    expect(list).toHaveBeenCalled();
    expect(element.querySelector('.colleague-search__message')?.textContent?.trim()).toBe(uk.planner.access.suggestions.empty);
    input().dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
    fixture.detectChanges();
    expect(element.querySelector('.colleague-search__list')).toBeNull();
  });

  it('gives the chosen colleague with the level and the role title by «Add» (an empty title is null)', async () => {
    const { input, settle, options, fixture, element } = render(['member']);
    const added: unknown[] = [];
    fixture.componentInstance.add.subscribe((value) => added.push(value));
    input().dispatchEvent(new Event('focus'));
    await settle();
    options()[0].click();
    fixture.detectChanges();
    const select = element.querySelector<HTMLSelectElement>('.member-search__tag select')!;
    select.value = '2';
    select.dispatchEvent(new Event('change'));
    const role = element.querySelector<HTMLInputElement>('.choice__role')!;
    role.value = '  Дизайнер ';
    role.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    const button = element.querySelector<HTMLButtonElement>('.member-search__add-button')!;
    expect(button.disabled).toBe(false);
    button.click();
    role.value = ' ';
    role.dispatchEvent(new Event('input'));
    button.click();
    expect(added).toEqual([
      { user: expect.objectContaining({ id: 'u1' }), claim: 2, roleTitle: 'Дизайнер' },
      { user: expect.objectContaining({ id: 'u1' }), claim: 2, roleTitle: null },
    ]);
  });
});
