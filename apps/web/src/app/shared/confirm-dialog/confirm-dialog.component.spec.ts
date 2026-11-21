import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { ConfirmDialogComponent } from './confirm-dialog.component';

@Component({
  imports: [ConfirmDialogComponent],
  template: `
    <app-confirm-dialog
      message="Питання?"
      confirmLabel="Так"
      cancelLabel="Скасувати"
      [busy]="busy()"
      [error]="error()"
      (confirm)="confirmed = confirmed + 1"
      (dismiss)="cancelled = cancelled + 1"
    />
  `,
})
class HostComponent {
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  confirmed = 0;
  cancelled = 0;
}

describe('ConfirmDialogComponent', () => {
  function render() {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const confirmButton = () => element.querySelector('.confirm__button--confirm') as HTMLButtonElement;
    const cancelButton = () => element.querySelector('.confirm__button--cancel') as HTMLButtonElement;
    return { fixture, host: fixture.componentInstance, element, confirmButton, cancelButton };
  }

  it('shows the question and the two answers, confirming first', () => {
    const { element } = render();
    expect(element.querySelector('.confirm__message')?.textContent?.trim()).toBe('Питання?');
    expect(Array.from(element.querySelectorAll('button')).map((button) => button.textContent?.trim())).toEqual(['Так', 'Скасувати']);
    expect(element.querySelector('.confirm__error')).toBeNull();
  });

  it('tells which answer is chosen', () => {
    const { host, confirmButton, cancelButton } = render();
    confirmButton().click();
    cancelButton().click();
    cancelButton().click();
    expect([host.confirmed, host.cancelled]).toEqual([1, 2]);
  });

  it('turns off only the confirming button while the action goes, cancelling stays possible', () => {
    const { fixture, host, confirmButton, cancelButton } = render();
    host.busy.set(true);
    fixture.detectChanges();
    expect(confirmButton().disabled).toBe(true);
    expect(cancelButton().disabled).toBe(false);
    confirmButton().click();
    expect(host.confirmed).toBe(0);
  });

  it('shows the error as an alert and hides it once it is gone', () => {
    const { fixture, host, element } = render();
    host.error.set('Не вдалося.');
    fixture.detectChanges();
    const error = element.querySelector('.confirm__error');
    expect(error?.textContent?.trim()).toBe('Не вдалося.');
    expect(error?.getAttribute('role')).toBe('alert');
    host.error.set(null);
    fixture.detectChanges();
    expect(element.querySelector('.confirm__error')).toBeNull();
  });
});
