import { Component, ElementRef, Injector, afterNextRender, computed, inject, input, linkedSignal, output, signal, viewChild } from '@angular/core';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';

import {
  CUSTOM_FIELD_NAME_MAX_LENGTH,
  CUSTOM_TABLE_DESCRIPTION_MAX_LENGTH,
  CUSTOM_TABLE_MAX_FIELDS,
  CUSTOM_TABLE_NAME_MAX_LENGTH,
} from '../../../core/custom-tables/custom-tables.service';
import { TableTemplate, TableTemplateUpdate } from '../../../core/custom-tables/table-templates.service';
import { ActionBarComponent } from '../../../shared/action-bar/action-bar.component';
import { ActionBarContent, ActionBarItem } from '../../../shared/action-bar/action-bar.service';
import { cellTextColor } from '../../../shared/color/cell-colors';
import { ColorPickerComponent } from '../../../shared/color-picker/color-picker.component';
import { PopoverComponent } from '../../../shared/popover/popover.component';
import { RichTextDirective, RichTextFormat, formatSelection } from '../../../shared/rich-text/rich-text.directive';
import { joinBackground, richTextToPlain, sanitizeRichText, splitBackground } from '../../../shared/rich-text/rich-text';

/** Стовпчик у формі: сталий локальний ключ `uid` (курсор не злітає) і назва (`id` у полів шаблону нема). */
interface EditableField {
  readonly uid: string;
  readonly name: string;
}

/**
 * Дії з вибраним стовпчиком (підпис — `knowledgeBase.table.cellActions.<id>`). «Напівжирного» нема:
 * заголовки й так напівжирні.
 */
type ColumnAction = 'background' | Exclude<RichTextFormat, 'bold'> | 'addColumnRight' | 'addColumnLeft' | 'deleteColumn';

const ICONS: Readonly<Record<ColumnAction, string>> = {
  background: 'color-fill',
  italic: 'format-italic',
  underline: 'format-underline',
  strikethrough: 'format-strikethrough',
  addColumnRight: 'column-add-right',
  addColumnLeft: 'column-add-left',
  deleteColumn: 'column-remove',
};

/**
 * Форма шаблону (у вікні «Редагувати» менеджера шаблонів): «Назва» (обовʼязкова), «Опис» і
 * «Стовпчики» — рядок заголовків, як у таблиці. Назву стовпчика змінюють у самому заголовку
 * (редагований блок із форматуванням виділеного, `appRichText`); порожня після виходу — «Новий
 * стовпчик». Над рядком — вбудована смуга дій (`app-action-bar`) для вибраного стовпчика, як у
 * таблиці: колір тла (кружок, вікно вибору кольору), курсив, підкреслення, закреслення
 * («Напівжирного» нема: заголовки й так напівжирні); «Додати стовпчик справа», «… зліва», «Видалити стовпчик»
 * (останній — ні: сервер вимагає хоча б одне поле). Структура шаблону замінюється цілком (`id` у
 * полів шаблону нема). Зміни йдуть на сервер лише з «Зберегти»
 * (`submitted`), тож видалення стовпчика не перепитується: вікно можна закрити без збереження.
 */
@Component({
  selector: 'app-kb-template-form',
  templateUrl: './kb-template-form.component.html',
  styleUrls: ['../../../settings/settings-section.scss', './kb-template-form.component.scss'],
  imports: [ActionBarComponent, ColorPickerComponent, PopoverComponent, RichTextDirective, TranslatePipe],
})
export class KbTemplateFormComponent {
  /** Шаблон, з яким форма відкривається. */
  readonly template = input.required<TableTemplate>();
  /** Запит іде: кнопка вимкнена. */
  readonly busy = input(false);
  /** Помилка попередньої спроби (ключ мовного файлу) або `null`. */
  readonly error = input<string | null>(null);

  readonly submitted = output<TableTemplateUpdate>();

  private readonly translate = inject(TranslateService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly injector = inject(Injector);
  private readonly colorPopover = viewChild<PopoverComponent>('colorPopover');

  protected readonly nameMaxLength = CUSTOM_TABLE_NAME_MAX_LENGTH;
  /** Скільки тексту з форматуванням уміщається в заголовок (ліміт сервера мінус тег кольору тла). */
  protected readonly headMaxLength = CUSTOM_FIELD_NAME_MAX_LENGTH - '<div style="background-color: #000000"></div>'.length;
  protected readonly descriptionMaxLength = CUSTOM_TABLE_DESCRIPTION_MAX_LENGTH;

  protected readonly title = linkedSignal(() => this.template().name);
  protected readonly description = linkedSignal(() => this.template().description ?? '');
  protected readonly fields = linkedSignal<readonly EditableField[]>(() =>
    this.template().fields.map((field, index) => ({ uid: `field-${index}`, name: field.name })),
  );
  /** Вибраний стовпчик (`uid`) і його заголовок; `null` — нічого. */
  protected readonly selected = signal<{ readonly uid: string; readonly element: HTMLElement } | null>(null);
  /** Стовпчик, для якого відкрито вибір кольору (фокус тоді йде у вікно вибору, а вибір стовпчика зникає). */
  private colorTarget: { readonly uid: string; readonly element: HTMLElement } | null = null;
  private readonly submittedOnce = signal(false);
  private newCount = 0;

  protected readonly titleError = computed(() => (this.submittedOnce() && !this.title().trim() ? 'common.required' : null));

  /** Смуга дій для вибраного стовпчика; нічого не вибрано — усе вимкнене. */
  protected readonly toolbar = computed<ActionBarContent>(() => {
    const selected = this.selected();
    const field = selected ? this.fields().find((candidate) => candidate.uid === selected.uid) : undefined;
    const none = !field;
    const item = (id: ColumnAction, disabled: boolean, swatch?: string | null): ActionBarItem<ColumnAction> => ({
      id,
      icon: ICONS[id],
      labelKey: `knowledgeBase.table.cellActions.${id}`,
      disabled,
      ...(swatch !== undefined ? { swatch } : {}),
    });
    return {
      labelKey: 'knowledgeBase.templates.columnActions',
      groups: [
        [
          item('background', none, field ? splitBackground(field.name).background : null),
          item('italic', none),
          item('underline', none),
          item('strikethrough', none),
        ],
        [
          // Більше 50 полів сервер не прийме; останнього не видалити — потрібне хоча б одне.
          item('addColumnRight', none || this.fields().length >= CUSTOM_TABLE_MAX_FIELDS),
          item('addColumnLeft', none || this.fields().length >= CUSTOM_TABLE_MAX_FIELDS),
          item('deleteColumn', none || this.fields().length <= 1),
        ],
      ],
      // Кнопки — лише з `ColumnAction` (вище), тож назва дії — одна з них.
      choose: (action, button) => this.onAction(action as ColumnAction, button),
    };
  });

  /** Назва стовпчика з форматуванням (без тегу кольору тла), очищена — для заголовка. */
  protected htmlOf(name: string): string {
    return sanitizeRichText(splitBackground(name).html);
  }

  /** Кольори заголовка: тло з тегу в назві, текст того самого відтінку з контрастом 7:1. */
  protected colorsOf(name: string): { readonly background: string; readonly color: string } | null {
    const background = splitBackground(name).background;
    return background ? { background, color: cellTextColor(background) } : null;
  }

  /** Колір тла стовпчика, для якого відкрито вибір кольору. */
  protected readonly pickerColor = signal<string | null>(null);

  protected onText(target: typeof this.title, event: Event): void {
    target.set((event.target as HTMLInputElement | HTMLTextAreaElement).value);
  }

  /** Змінили назву стовпчика (з форматуванням); колір тла лишається. */
  protected onHeadInput(field: EditableField, html: string): void {
    this.update(field.uid, (current) => joinBackground(splitBackground(current.name).background, html));
  }

  protected onHeadFocus(field: EditableField, event: FocusEvent): void {
    this.selected.set({ uid: field.uid, element: event.target as HTMLElement });
  }

  /** Вийшли із заголовка: порожня назва — «Новий стовпчик»; вибір лишається, якщо фокус у смузі дій чи виборі кольору. */
  protected onHeadBlur(field: EditableField, event: FocusEvent): void {
    const current = this.fields().find((candidate) => candidate.uid === field.uid);
    if (current && !richTextToPlain(current.name).trim()) {
      this.update(field.uid, (value) => joinBackground(splitBackground(value.name).background, this.newColumnName()));
    }
    const next = event.relatedTarget as HTMLElement | null;
    if (!next?.closest('app-action-bar, ion-popover')) {
      this.selected.set(null);
    }
  }

  /** Вибрали колір тла (чи «Без кольору») для стовпчика, для якого відкрито вибір кольору. */
  protected onColorPick(color: string | null): void {
    void this.colorPopover()?.close();
    const target = this.colorTarget;
    this.colorTarget = null;
    if (target) {
      this.update(target.uid, (field) => joinBackground(color, splitBackground(field.name).html));
      target.element.focus();
    }
  }

  protected submit(): void {
    this.submittedOnce.set(true);
    if (this.titleError() || this.busy()) {
      return;
    }
    this.submitted.emit({
      name: this.title().trim(),
      description: this.description().trim() || null,
      // Порожньої назви сервер не прийме.
      fields: this.fields().map((field) => ({ name: richTextToPlain(field.name).trim() ? field.name : this.newColumnName() })),
    });
  }

  private onAction(action: ColumnAction, button: HTMLElement): void {
    const selected = this.selected();
    if (!selected) {
      return;
    }
    switch (action) {
      case 'background': {
        this.colorTarget = selected;
        const field = this.fields().find((candidate) => candidate.uid === selected.uid);
        this.pickerColor.set(field ? splitBackground(field.name).background : null);
        this.colorPopover()?.open(button);
        return;
      }
      case 'addColumnRight':
      case 'addColumnLeft':
        this.addColumn(selected.uid, action === 'addColumnRight' ? 1 : 0);
        return;
      case 'deleteColumn':
        this.fields.update((fields) => fields.filter((field) => field.uid !== selected.uid));
        this.selected.set(null);
        return;
      default:
        // Курсор лишився в заголовку (кнопки смуги не забирають фокус): форматується виділене.
        formatSelection(action);
    }
  }

  /** Новий стовпчик справа (`offset` 1) чи зліва (0) від `uid`; курсор — у його заголовок. */
  private addColumn(uid: string, offset: number): void {
    const fields = this.fields();
    const index = fields.findIndex((field) => field.uid === uid) + offset;
    const added: EditableField = { uid: `new-${++this.newCount}`, name: this.newColumnName() };
    this.fields.set([...fields.slice(0, index), added, ...fields.slice(index)]);
    afterNextRender(() => this.host.querySelectorAll<HTMLElement>('.kb-template-form__head')[index]?.focus(), { injector: this.injector });
  }

  private update(uid: string, name: (field: EditableField) => string): void {
    this.fields.update((fields) => fields.map((field) => (field.uid === uid ? { ...field, name: name(field) } : field)));
  }

  private newColumnName(): string {
    return this.translate.instant('knowledgeBase.table.newColumn') as string;
  }
}
