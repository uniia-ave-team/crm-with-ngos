import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslatePipe, TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { ListTextPipe } from './list-text.pipe';

@Component({
  template: `<p class="or">{{ items | listText: 'or' }}</p>
    <p class="and">{{ items | listText: 'and' }}</p>
    <p class="inside">{{ 'text' | translate: { types: items | listText: 'or' } }}</p>`,
  imports: [ListTextPipe, TranslatePipe],
})
class HostComponent {
  items: readonly string[] = ['PNG', 'WebP', 'SVG'];
}

describe('ListTextPipe', () => {
  function render() {
    TestBed.configureTestingModule({ imports: [HostComponent], providers: [provideTranslateService()] });
    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('uk', { common: { list: uk.common.list }, text: 'Формати: {{types}}.' });
    translate.setTranslation('xx', { common: { list: { separator: ' / ', and: ' & ', or: ' | ' } }, text: 'Formats: {{types}}.' });
    translate.use('uk');
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const text = (selector: string) => (fixture.nativeElement as HTMLElement).querySelector(selector)?.textContent?.trim();
    return { fixture, translate, text };
  }

  it('joins the items with the separator and the joiners of the language file', () => {
    const { text } = render();
    const { separator, and, or } = uk.common.list;
    expect(text('.or')).toBe(`PNG${separator}WebP${or}SVG`);
    expect(text('.and')).toBe(`PNG${separator}WebP${and}SVG`);
  });

  it('can be put into a text of the language file as a parameter', () => {
    const { text } = render();
    expect(text('.inside')).toBe(`Формати: PNG${uk.common.list.separator}WebP${uk.common.list.or}SVG.`);
  });

  it('gives the list in the new language after the language is changed, the text around it too', () => {
    const { fixture, translate, text } = render();
    translate.use('xx');
    fixture.detectChanges();
    expect(text('.or')).toBe('PNG / WebP | SVG');
    expect(text('.inside')).toBe('Formats: PNG / WebP | SVG.');
  });
});
