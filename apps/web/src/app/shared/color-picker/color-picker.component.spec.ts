import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { ColorPickerComponent, PASTEL_COLORS } from './color-picker.component';

@Component({
  imports: [ColorPickerComponent],
  template: `<app-color-picker [value]="value()" (pick)="picked.push($event)" />`,
})
class HostComponent {
  readonly value = signal<string | null>(null);
  readonly picked: (string | null)[] = [];
}

describe('ColorPickerComponent', () => {
  function render(value: string | null = null) {
    TestBed.configureTestingModule({ providers: [provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })] });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    const fixture = TestBed.createComponent(HostComponent);
    fixture.componentInstance.value.set(value);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const swatches = () => Array.from(element.querySelectorAll<HTMLButtonElement>('.color-picker__swatch'));
    return { fixture, host: fixture.componentInstance, element, swatches };
  }

  it('offers «No colour» first, 10 pastel colours and a brighter red (two even rows of six), each named for screen readers and in the hint', () => {
    const { swatches } = render();
    expect(PASTEL_COLORS).toHaveLength(11);
    expect(PASTEL_COLORS.at(-1)).toEqual({ key: 'red', hex: '#ff6b6b' });
    expect(swatches()).toHaveLength(12);
    expect(swatches().map((swatch) => swatch.getAttribute('aria-label'))).toEqual([
      uk.colorPicker.none,
      ...PASTEL_COLORS.map((color) => uk.colorPicker.colors[color.key as keyof typeof uk.colorPicker.colors]),
    ]);
    expect(swatches().every((swatch) => swatch.getAttribute('title') === swatch.getAttribute('aria-label'))).toBe(true);
    expect(swatches()[0].classList).toContain('color-picker__swatch--none');
  });

  it('marks the current colour and gives the chosen one (or null for «No colour»)', () => {
    const { host, swatches } = render('#cdeaf5');
    const sky = swatches().find((swatch) => swatch.getAttribute('aria-pressed') === 'true')!;
    expect(sky.getAttribute('aria-label')).toBe(uk.colorPicker.colors.sky);
    swatches()[1].click();
    swatches()[0].click();
    expect(host.picked).toEqual([PASTEL_COLORS[0].hex, null]);
  });

  it('takes a HEX colour from the field, and says when it is not one', () => {
    const { fixture, host, element } = render();
    const input = element.querySelector<HTMLInputElement>('.color-picker__input')!;
    const submit = (value: string) => {
      input.value = value;
      input.dispatchEvent(new Event('input'));
      element.querySelector('form')!.dispatchEvent(new Event('submit'));
      fixture.detectChanges();
    };
    submit('зелений');
    expect(element.querySelector('.color-picker__error')?.textContent?.trim()).toBe(uk.colorPicker.invalid);
    expect(host.picked).toEqual([]);
    submit('A1B2C3');
    expect(host.picked).toEqual(['#a1b2c3']);
  });
});
