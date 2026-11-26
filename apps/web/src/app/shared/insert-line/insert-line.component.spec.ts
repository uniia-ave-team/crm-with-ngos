import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { InsertLineComponent } from './insert-line.component';

@Component({
  template: `<app-insert-line label="Додати" (insert)="inserts = inserts + 1" />`,
  imports: [InsertLineComponent],
})
class HostComponent {
  inserts = 0;
}

describe('InsertLineComponent', () => {
  it('is a «+» button named by its label that tells about a press, taking no height itself', () => {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const button = element.querySelector<HTMLButtonElement>('.insert-line__button')!;
    expect(button.getAttribute('aria-label')).toBe('Додати');
    expect(button.getAttribute('tabindex')).toBe('0');
    expect((button.querySelector('ion-icon') as unknown as { name: string }).name).toBe('add-outline');
    expect(getComputedStyle(element.querySelector('app-insert-line')!).height).toBe('0px');

    button.click();
    expect(fixture.componentInstance.inserts).toBe(1);
  });
});
