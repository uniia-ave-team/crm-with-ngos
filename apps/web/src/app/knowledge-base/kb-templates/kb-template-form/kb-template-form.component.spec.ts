import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideIonicAngular } from '@ionic/angular';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../../assets/i18n/uk.json';
import { TableTemplate, TableTemplateUpdate } from '../../../core/custom-tables/table-templates.service';
import { ActionBarContent } from '../../../shared/action-bar/action-bar.service';
import { KbTemplateFormComponent } from './kb-template-form.component';

describe('KbTemplateFormComponent', () => {
  const template: TableTemplate = {
    id: 'tpl2',
    name: 'Своя',
    description: 'Опис',
    isSystemPreset: false,
    fields: [
      { name: 'Назва', order: 0 },
      { name: 'Терміни', order: 1 },
    ],
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideIonicAngular(), provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
  });

  afterEach(() => vi.restoreAllMocks());

  function render() {
    const fixture = TestBed.createComponent(KbTemplateFormComponent);
    fixture.componentRef.setInput('template', template);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const component = fixture.componentInstance as unknown as {
      toolbar(): ActionBarContent;
      onColorPick(color: string | null): void;
    };
    const heads = () => Array.from(element.querySelectorAll<HTMLElement>('.kb-template-form__head'));
    const select = (index: number) => {
      heads()[index].dispatchEvent(new FocusEvent('focus'));
      fixture.detectChanges();
    };
    const item = (id: string) => component.toolbar().groups.flat().find((candidate) => candidate.id === id)!;
    const choose = (id: string) => {
      component.toolbar().choose(id, document.createElement('button'));
      fixture.detectChanges();
    };
    const submitted: TableTemplateUpdate[] = [];
    fixture.componentInstance.submitted.subscribe((value) => submitted.push(value));
    const submit = () => {
      element.querySelector('form')!.dispatchEvent(new Event('submit'));
      fixture.detectChanges();
    };
    return { fixture, element, component, heads, select, item, choose, submitted, submit };
  }

  it('shows the name, the description and the columns as a header row, with the column actions off until one is chosen', () => {
    const { element, heads, item, component } = render();
    expect(element.querySelector<HTMLInputElement>('#kb-template-title')?.value).toBe('Своя');
    expect(element.querySelector<HTMLTextAreaElement>('#kb-template-description')?.value).toBe('Опис');
    expect(heads().map((head) => head.innerHTML)).toEqual(['Назва', 'Терміни']);
    expect(heads()[0].getAttribute('contenteditable')).toBe('true');
    // «Напівжирного» нема: заголовки й так напівжирні.
    expect(component.toolbar().groups.flat().map((candidate) => candidate.id)).toEqual([
      'background',
      'italic',
      'underline',
      'strikethrough',
      'addColumnRight',
      'addColumnLeft',
      'deleteColumn',
    ]);
    for (const id of ['background', 'italic', 'underline', 'strikethrough', 'addColumnRight', 'addColumnLeft', 'deleteColumn']) {
      expect(item(id).disabled, id).toBe(true);
      expect(uk.knowledgeBase.table.cellActions[id as keyof typeof uk.knowledgeBase.table.cellActions], id).toBeTruthy();
    }
  });

  it('renames a column in its header with formatting, adds columns on both sides, colours one and deletes one, sending all on «Save»', () => {
    const { heads, select, item, choose, component, submitted, submit, fixture } = render();
    select(0);
    expect(item('italic').disabled).toBe(false);
    heads()[0].innerHTML = '<b>Назва</b> фонду';
    heads()[0].dispatchEvent(new Event('input'));
    // Новий стовпчик одразу вибраний (курсор у його заголовку): «зліва» — від нього.
    choose('addColumnRight');
    choose('addColumnLeft');
    expect(heads().map((head) => head.textContent)).toEqual(['Назва фонду', uk.knowledgeBase.table.newColumn, uk.knowledgeBase.table.newColumn, 'Терміни']);

    choose('background');
    component.onColorPick('#fde2e4');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('th')[1].style.background).toBe('rgb(253, 226, 228)');

    select(3);
    choose('deleteColumn');
    submit();
    expect(submitted).toEqual([
      {
        name: 'Своя',
        description: 'Опис',
        fields: [
          { name: '<b>Назва</b> фонду' },
          { name: `<div style="background-color: #fde2e4">${uk.knowledgeBase.table.newColumn}</div>` },
          { name: uk.knowledgeBase.table.newColumn },
        ],
      },
    ]);
  });

  it('gives an emptied column name back as «New column» and does not delete the only column', () => {
    const { heads, select, item, choose, fixture } = render();
    select(1);
    choose('deleteColumn');
    select(0);
    expect(item('deleteColumn').disabled).toBe(true);
    heads()[0].innerHTML = '';
    heads()[0].dispatchEvent(new Event('input'));
    heads()[0].dispatchEvent(new FocusEvent('blur'));
    fixture.detectChanges();
    expect(heads()[0].textContent).toBe(uk.knowledgeBase.table.newColumn);
  });

  it('cleans the column names from the server before they get into the headers', () => {
    const fixture = TestBed.createComponent(KbTemplateFormComponent);
    fixture.componentRef.setInput('template', { ...template, fields: [{ name: '<img src="x" onerror="alert(1)"><i style="color:red">Назва</i>', order: 0 }] });
    fixture.detectChanges();
    const head = (fixture.nativeElement as HTMLElement).querySelector('.kb-template-form__head')!;
    expect(head.innerHTML).toBe('<i>Назва</i>');
  });

  it('asks for the name before sending', () => {
    const { element, submitted, submit, fixture } = render();
    const title = element.querySelector<HTMLInputElement>('#kb-template-title')!;
    title.value = '  ';
    title.dispatchEvent(new Event('input'));
    submit();
    fixture.detectChanges();
    expect(submitted).toEqual([]);
    expect(element.querySelector('#kb-template-title-error')?.textContent?.trim()).toBe(uk.common.required);
  });
});
