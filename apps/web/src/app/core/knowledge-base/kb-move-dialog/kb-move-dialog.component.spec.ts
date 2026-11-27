import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideIonicAngular } from '@ionic/angular';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../../assets/i18n/uk.json';
import { KbMoveDialogComponent } from './kb-move-dialog.component';

@Component({
  imports: [KbMoveDialogComponent],
  template: `<app-kb-move-dialog [folders]="folders" [from]="from()" (move)="moved.push($event)" />`,
})
class HostComponent {
  readonly folders = [
    { id: 'f1', title: 'Розділ' },
    { id: 'f2', title: 'Інший розділ' },
  ];
  readonly from = signal<string | null>('f1');
  readonly moved: (string | null)[] = [];
}

describe('KbMoveDialogComponent', () => {
  function render(from: string | null = 'f1') {
    TestBed.configureTestingModule({ providers: [provideIonicAngular(), provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })] });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    const fixture = TestBed.createComponent(HostComponent);
    fixture.componentInstance.from.set(from);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const fromSelect = element.querySelector('#kb-move-from') as HTMLSelectElement;
    const toSelect = element.querySelector('#kb-move-to') as HTMLSelectElement;
    const choose = (value: string) => {
      toSelect.value = value;
      toSelect.dispatchEvent(new Event('change'));
      fixture.detectChanges();
    };
    const submit = () => {
      element.querySelector('form')?.dispatchEvent(new Event('submit'));
      fixture.detectChanges();
    };
    const error = () => element.querySelector('.settings-field__error')?.textContent?.trim() ?? null;
    return { host: fixture.componentInstance, fromSelect, toSelect, choose, submit, error };
  }

  const texts = (select: HTMLSelectElement) => Array.from(select.options).map((option) => option.textContent?.trim());

  it('offers «No folder» and all the folders in both lists, the current folder fixed in «From»', () => {
    const { fromSelect, toSelect } = render();
    const options = [uk.knowledgeBase.dialogs.move.noFolder, 'Розділ', 'Інший розділ'];
    expect(texts(fromSelect)).toEqual(options);
    expect(texts(toSelect)).toEqual(options);
    expect(fromSelect.disabled).toBe(true);
    expect(fromSelect.value).toBe('f1');
  });

  it.each([
    ['in a folder', 'f1', ''],
    ['without a folder', null, 'f1'],
  ])('starts «To» with the first other choice for a material %s', (_name, from, to) => {
    const { fromSelect, toSelect } = render(from);
    expect(fromSelect.value).toBe(from ?? '');
    expect(toSelect.value).toBe(to);
  });

  it('moves to the chosen folder or out of any folder', () => {
    const { host, choose, submit } = render();
    choose('f2');
    submit();
    choose('');
    submit();
    expect(host.moved).toEqual(['f2', null]);
  });

  it('does not move to the folder the material is already in', () => {
    const { host, choose, submit, error } = render();
    choose('f1');
    submit();
    expect(error()).toBe(uk.knowledgeBase.dialogs.move.same);
    expect(host.moved).toEqual([]);
  });
});
