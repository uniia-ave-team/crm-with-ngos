import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideIonicAngular } from '@ionic/angular';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../../assets/i18n/uk.json';
import { TableTemplateOption } from '../../custom-tables/table-templates.service';
import { KnowledgeBaseDetails } from '../knowledge-base';
import { KbItemFormComponent, KbItemFormValue } from './kb-item-form.component';

@Component({
  imports: [KbItemFormComponent],
  template: `<app-kb-item-form submitLabelKey="knowledgeBase.dialogs.add.submit" [initial]="initial()" [templates]="templates()" (submitted)="values.push($event)" />`,
})
class HostComponent {
  readonly initial = signal<KnowledgeBaseDetails | null>(null);
  readonly templates = signal<readonly TableTemplateOption[] | null>(null);
  readonly values: KbItemFormValue[] = [];
}

describe('KbItemFormComponent', () => {
  function render(setup: { initial?: KnowledgeBaseDetails; templates?: readonly TableTemplateOption[] } = {}) {
    TestBed.configureTestingModule({ providers: [provideIonicAngular(), provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })] });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    const fixture = TestBed.createComponent(HostComponent);
    fixture.componentInstance.initial.set(setup.initial ?? null);
    fixture.componentInstance.templates.set(setup.templates ?? null);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const field = <T extends HTMLElement>(id: string) => element.querySelector(`#${id}`) as T | null;
    const setValue = (id: string, value: string, event = 'input') => {
      const control = field<HTMLInputElement>(id)!;
      control.value = value;
      control.dispatchEvent(new Event(event));
      fixture.detectChanges();
    };
    const submit = () => {
      element.querySelector('form')?.dispatchEvent(new Event('submit'));
      fixture.detectChanges();
    };
    const labels = () => Array.from(element.querySelectorAll('label')).map((label) => label.textContent?.replace('*', '').trim());
    return { host: fixture.componentInstance, element, field, setValue, submit, labels };
  }

  const form = uk.knowledgeBase.dialogs.form;

  it('asks for the type (a table, not changeable yet), the template, the name and the description when adding', () => {
    const { field, labels } = render({ templates: [{ id: 'tpl1', name: 'База донорів' }] });
    expect(labels()).toEqual([form.type, form.template, form.title, form.description]);
    const type = field<HTMLSelectElement>('kb-item-type')!;
    expect(type.disabled).toBe(true);
    expect(type.value).toBe('customtable');
    expect(Array.from(type.options).map((option) => option.textContent?.trim())).toEqual(Object.values(uk.knowledgeBase.types));
    const template = field<HTMLSelectElement>('kb-item-template')!;
    expect(Array.from(template.options).map((option) => option.textContent?.trim())).toEqual([form.noTemplate, 'База донорів']);
    expect(template.value).toBe('');
  });

  it('gives the name, the description, the type and the chosen template without spaces at the edges', () => {
    const { host, setValue, submit } = render({ templates: [{ id: 'tpl1', name: 'База донорів' }] });
    setValue('kb-item-template', 'tpl1', 'change');
    setValue('kb-item-title', '  Донори ');
    setValue('kb-item-description', '   ');
    submit();
    expect(host.values).toEqual([{ title: 'Донори', description: null, type: 'customtable', templateId: 'tpl1' }]);
  });

  it('does not give an empty name', () => {
    const { host, element, setValue, submit } = render({ templates: [] });
    setValue('kb-item-title', '   ');
    submit();
    expect(element.querySelector('.settings-field__error')?.textContent?.trim()).toBe(uk.common.required);
    expect(host.values).toEqual([]);
  });

  it('opens with the name and the description of the material and without the type and the template when editing', () => {
    const { field, labels } = render({ initial: { title: 'Таблиця', description: 'Опис' } });
    expect(labels()).toEqual([form.title, form.description]);
    expect(field<HTMLInputElement>('kb-item-title')!.value).toBe('Таблиця');
    expect(field<HTMLTextAreaElement>('kb-item-description')!.value).toBe('Опис');
  });
});
