import { CUSTOM_ELEMENTS_SCHEMA, Signal, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { ProjectMember } from '../../core/projects/projects.service';
import { CurrentUser, CurrentUserService } from '../../core/users/current-user.service';
import { MemberAvatarsComponent } from './member-avatars.component';

describe('MemberAvatarsComponent', () => {
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

  function render(members: readonly ProjectMember[], selfId: string | null = null) {
    TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    (TestBed.inject(CurrentUserService) as { user: Signal<CurrentUser | null> }).user = signal(selfId ? ({ id: selfId } as CurrentUser) : null);
    const fixture = TestBed.createComponent(MemberAvatarsComponent);
    fixture.componentRef.setInput('members', members);
    fixture.componentRef.setInput('label', uk.planner.actions.viewAccess);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    return { fixture, element };
  }

  it('shows two avatars and, when there are more, a third with «...» telling how many more', () => {
    const { element } = render([member('1'), member('2'), member('3'), member('4')]);
    expect(element.querySelectorAll('app-avatar')).toHaveLength(2);
    const more = element.querySelector('.member-avatars__item--more')!;
    expect(more.textContent?.trim()).toBe(uk.planner.members.moreMark);
    expect(more.getAttribute('title')).toBe(uk.planner.members.more.replace('{{count}}', '2'));
    const button = element.querySelector('button')!;
    expect(button.getAttribute('aria-label')).toBe(`${uk.planner.actions.viewAccess}: Пр1 Ім1, Пр2 Ім2, Пр3 Ім3, Пр4 Ім4`);
  });

  it('has no third avatar with two members', () => {
    const { element } = render([member('1'), member('2')]);
    expect(element.querySelectorAll('app-avatar')).toHaveLength(2);
    expect(element.querySelector('.member-avatars__item--more')).toBeNull();
  });

  it('puts the user first when they are among the members', () => {
    const { fixture } = render([member('1'), member('2'), member('me')], 'me');
    const shown = (fixture.componentInstance as unknown as { shown(): readonly ProjectMember[] }).shown();
    expect(shown.map((item) => item.userId)).toEqual(['me', '1']);
  });

  it('tells a click', () => {
    const { fixture, element } = render([member('1')]);
    let clicks = 0;
    fixture.componentInstance.activate.subscribe(() => clicks++);
    element.querySelector('button')!.click();
    expect(clicks).toBe(1);
  });
});
