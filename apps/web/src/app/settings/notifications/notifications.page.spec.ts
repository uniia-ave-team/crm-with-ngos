import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { SettingsPageModule } from '../settings.module';
import { NotificationsPage } from './notifications.page';

// Тексти сторінки живуть в uk.json — беремо їх звідти, а не дублюємо.
const text = uk.settings.notifications;

describe('NotificationsPage', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      imports: [SettingsPageModule],
      providers: [provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    }).compileComponents();
    TestBed.inject(TranslateService).setTranslation('uk', uk);
  });

  function render() {
    const fixture = TestBed.createComponent(NotificationsPage);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows the title, the four lists and the button from the language file', () => {
    const element = render();
    expect(element.querySelector('.settings-section__title')?.textContent).toContain(text.title);
    expect(Array.from(element.querySelectorAll('.settings-field__label')).map((l) => l.textContent?.trim())).toEqual([
      text.organization,
      text.tasksStart,
      text.deadlines,
      text.calendar,
    ]);
    expect(element.querySelectorAll('select')).toHaveLength(4);
    expect(element.querySelector('.settings-submit')?.textContent?.trim()).toBe(text.submit);
  });

  it('starts with the values of the design: a week, a month, 5 days and a day before', () => {
    const element = render();
    const selected = Array.from(element.querySelectorAll('select')).map((s) => s.selectedOptions[0].textContent?.trim());
    expect(selected).toEqual([text.when.week, text.when.month, text.when.fiveDays, text.when.dayBefore]);
  });

  it('offers the same options in every list, from the language file', () => {
    const element = render();
    for (const select of Array.from(element.querySelectorAll('select'))) {
      expect(Array.from(select.options).map((o) => o.textContent?.trim())).toEqual([
        text.when.none,
        text.when.dayBefore,
        text.when.fiveDays,
        text.when.week,
        text.when.month,
      ]);
    }
  });

  it('links every label to its list and gives the controls a tabindex', () => {
    const element = render();
    for (const label of Array.from(element.querySelectorAll('label'))) {
      expect(element.querySelector(`#${label.getAttribute('for')}`)).not.toBeNull();
    }
    for (const control of Array.from(element.querySelectorAll('select, button'))) {
      expect(control.getAttribute('tabindex')).toBe('0');
    }
  });
});
