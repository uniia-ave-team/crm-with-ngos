import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideTranslateService } from '@ngx-translate/core';

import { SearchHeaderComponent } from '../search-header/search-header.component';
import { ActionButtonComponent, ActionButtonTone } from './action-button.component';

@Component({
  template: `
    <app-action-button class="alone" icon="add-outline" label="Дія" [tone]="tone()" (click)="clicks = clicks + 1" />
    <app-action-button class="icon-only" icon="share-outline" label="Експорт" [iconOnly]="true" />
    <app-search-header title="Заголовок">
      <app-action-button class="in-header" icon="people" label="Дія" />
    </app-search-header>
  `,
  imports: [ActionButtonComponent, SearchHeaderComponent],
})
class HostComponent {
  readonly tone = signal<ActionButtonTone>('neutral');
  clicks = 0;
}

describe('ActionButtonComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })] });
  });

  function render() {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const alone = () => element.querySelector('app-action-button.alone') as HTMLElement;
    const inHeader = () => element.querySelector('app-action-button.in-header') as HTMLElement;
    return { fixture, host: fixture.componentInstance, element, alone, inHeader };
  }

  it('is a button with the icon and the text, the text also in aria-label and a tabindex (for Safari)', () => {
    const button = render().alone().querySelector('button')!;
    expect(button.getAttribute('type')).toBe('button');
    expect(button.getAttribute('tabindex')).toBe('0');
    expect(button.getAttribute('aria-label')).toBe('Дія');
    expect(button.textContent?.trim()).toBe('Дія');
    expect((button.querySelector('ion-icon') as unknown as { name: string }).name).toBe('add-outline');
  });

  it.each<ActionButtonTone>(['blue', 'green', 'neutral'])('takes the tone %s', (tone) => {
    const { fixture, host, alone } = render();
    host.tone.set(tone);
    fixture.detectChanges();
    expect(alone().querySelector('button')?.classList).toContain(`action-button--${tone}`);
  });

  it('is always the icon only with iconOnly, the text in the hint; without it there is no hint', () => {
    const { element } = render();
    const iconOnly = element.querySelector('.icon-only')!;
    expect(iconOnly.classList).toContain('action-button--compact');
    expect(iconOnly.querySelector('button')?.getAttribute('title')).toBe('Експорт');
    expect(iconOnly.querySelector('button')?.getAttribute('aria-label')).toBe('Експорт');
    const alone = element.querySelector('.alone')!;
    expect(alone.classList).not.toContain('action-button--compact');
    expect(alone.querySelector('button')?.hasAttribute('title')).toBe(false);
  });

  it('passes the click to the page', () => {
    const { host, alone } = render();
    alone().querySelector('button')!.click();
    expect(host.clicks).toBe(1);
  });

  it('is pressed to the icon only in the search header while the search is open', () => {
    const { fixture, element, alone, inHeader } = render();
    expect(alone().classList).not.toContain('action-button--in-header');
    expect(inHeader().classList).toContain('action-button--in-header');
    expect(inHeader().classList).not.toContain('action-button--compact');

    element.querySelector<HTMLButtonElement>('.search-header__toggle')!.click();
    fixture.detectChanges();
    expect(inHeader().classList).toContain('action-button--compact');
    expect(alone().classList).not.toContain('action-button--compact');
  });
});
