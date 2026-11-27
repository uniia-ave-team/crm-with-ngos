import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { SortBarComponent, SortBarValue } from './sort-bar.component';

@Component({
  template: `<app-sort-bar [options]="options" [value]="value()" (sortChange)="changes.push($event); value.set($event)" />`,
  imports: [SortBarComponent],
})
class HostComponent {
  readonly options = [
    { id: 'name', labelKey: 'colleagues.columns.name' },
    { id: 'email', labelKey: 'colleagues.columns.email' },
  ];
  readonly value = signal<SortBarValue>({ id: 'name', direction: 'asc' });
  readonly changes: SortBarValue[] = [];
}

describe('SortBarComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })] });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
  });

  function render() {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    return {
      fixture,
      host: fixture.componentInstance,
      element,
      select: () => element.querySelector('select')!,
      direction: () => element.querySelector<HTMLButtonElement>('.sort-bar__direction')!,
    };
  }

  it('has the label, the fields from the language file with the current one chosen, and the direction button', () => {
    const { element, select, direction } = render();
    expect(element.querySelector('label')?.textContent?.trim()).toBe(uk.common.sort.by);
    expect(Array.from(select().options).map((option) => option.textContent?.trim())).toEqual([uk.colleagues.columns.name, uk.colleagues.columns.email]);
    expect(select().value).toBe('name');
    expect(direction().getAttribute('aria-label')).toBe(uk.common.sort.asc);
    expect(direction().getAttribute('tabindex')).toBe('0');
    expect((direction().querySelector('ion-icon') as unknown as { name: string }).name).toBe('caret-down-outline');
  });

  it('gives the new field with the same direction, and turns the direction by a click', () => {
    const { fixture, host, select, direction } = render();
    select().value = 'email';
    select().dispatchEvent(new Event('change'));
    fixture.detectChanges();
    direction().click();
    fixture.detectChanges();
    expect(host.changes).toEqual([
      { id: 'email', direction: 'asc' },
      { id: 'email', direction: 'desc' },
    ]);
    expect(direction().getAttribute('aria-label')).toBe(uk.common.sort.desc);
    expect((direction().querySelector('ion-icon') as unknown as { name: string }).name).toBe('caret-up-outline');
  });
});
