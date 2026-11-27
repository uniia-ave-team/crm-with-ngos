import { Component, signal, viewChild } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { UserProfile } from '../../core/users/user-profile';
import { ProfileFormComponent, ProfileFormValue } from './profile-form.component';

// Повну поведінку полів (перевірки, аватар, займенники) перевіряють тести «Даних профілю», що
// показують цю форму; тут — лише те, що форма дає сторінкам.

@Component({
  template: `<app-profile-form idPrefix="test" [profile]="profile()" (changed)="changes = changes + 1" (save)="saves.push($event)" />`,
  imports: [ProfileFormComponent],
})
class HostComponent {
  readonly profile = signal<UserProfile | null>({ id: 'u1', email: 'a@b.org', firstName: 'Перший', lastName: 'Колега', roles: [], pronounCategory: 3 });
  readonly form = viewChild.required(ProfileFormComponent);
  readonly saves: ProfileFormValue[] = [];
  changes = 0;
}

describe('ProfileFormComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })] });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
  });

  function render() {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const field = (name: string) => element.querySelector(`#test-${name}`) as HTMLInputElement;
    const type = (name: string, value: string) => {
      field(name).value = value;
      field(name).dispatchEvent(new Event('input'));
      fixture.detectChanges();
    };
    const submit = () => {
      (element.querySelector('button[type=submit]') as HTMLButtonElement).click();
      fixture.detectChanges();
    };
    return { fixture, host: fixture.componentInstance, element, field, type, submit };
  }

  it('starts the ids of the fields with idPrefix, and the labels point to them', () => {
    const { element, field } = render();
    expect(field('last-name').value).toBe('Колега');
    expect(element.querySelector('label[for=test-last-name]')).not.toBeNull();
  });

  it('gives the body of the request and no avatar on save, empty optional fields as null', () => {
    const { host, type, submit } = render();
    type('phone', '  ');
    submit();
    expect(host.saves).toEqual([
      {
        profile: {
          firstName: 'Перший',
          lastName: 'Колега',
          patronymic: null,
          phoneNumber: null,
          country: null,
          emergencyContact: null,
          preferredLanguage: null,
          pronouns: uk.settings.personal.pronounThey,
          pronounCategory: 3,
        },
        avatar: null,
      },
    ]);
  });

  it('tells about every change and every press of "Save", and gives nothing while a required field is empty', () => {
    const { host, type, submit } = render();
    type('first-name', ' ');
    submit();
    expect(host.changes).toBe(2);
    expect(host.saves).toEqual([]);
  });

  it('keeps the typed text over a new profile, and follows the profile again after markSaved', () => {
    const { fixture, host, field, type } = render();
    type('last-name', 'Інше');
    host.profile.update((profile) => ({ ...profile!, lastName: 'Нове' }));
    fixture.detectChanges();
    expect(field('last-name').value).toBe('Інше');

    host.form().markSaved();
    fixture.detectChanges();
    expect(field('last-name').value).toBe('Нове');
  });
});
