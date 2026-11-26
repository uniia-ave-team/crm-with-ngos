import { Component, computed, input, linkedSignal, output, signal } from '@angular/core';
import { IonIcon } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

import { CUSTOM_TABLE_DESCRIPTION_MAX_LENGTH, CUSTOM_TABLE_NAME_MAX_LENGTH } from '../../custom-tables/custom-tables.service';
import { TableTemplateOption } from '../../custom-tables/table-templates.service';
import { KNOWLEDGE_BASE_ITEM_TYPES, KnowledgeBaseDetails, KnowledgeBaseItemType } from '../knowledge-base';

/** Що віддає форма: назва й опис, а у вікні «Додати» — ще тип і шаблон таблиці. */
export interface KbItemFormValue extends KnowledgeBaseDetails {
  readonly type: KnowledgeBaseItemType;
  /** Шаблон «за основу» (лише таблиця); `null` — без шаблону. */
  readonly templateId: string | null;
}

/** Значення опції «Без шаблону». */
const NO_TEMPLATE = '';

/**
 * Форма матеріалу чи папки «Бази знань» (у `app-modal` під `app-dialog-header`): «Назва»
 * (обовʼязкова), «Опис» і кнопка. У вікні «Додати» (`templates` не `null`) над ними ще «Тип» —
 * видимий, але поки вимкнений (вибрана таблиця: сторінок і папок сервер ще не вміє) — і для
 * таблиці «Шаблон»: «Без шаблону» й шаблони з сервера. У вікні «Редагувати» — назва й опис
 * матеріалу (`initial`).
 */
@Component({
  selector: 'app-kb-item-form',
  templateUrl: './kb-item-form.component.html',
  styleUrls: ['../../../settings/settings-section.scss', './kb-item-form.component.scss'],
  imports: [IonIcon, TranslatePipe],
})
export class KbItemFormComponent {
  /** Назва й опис, з якими форма відкривається (`null` — порожня). */
  readonly initial = input<KnowledgeBaseDetails | null>(null);
  /** Шаблони таблиць для вибору (вікно «Додати»); `null` — без полів «Тип» і «Шаблон». */
  readonly templates = input<readonly TableTemplateOption[] | null>(null);
  /** Ключ підпису кнопки. */
  readonly submitLabelKey = input.required<string>();
  /** Запит іде: кнопка вимкнена. */
  readonly busy = input(false);
  /** Помилка попередньої спроби (ключ мовного файлу) або `null`. */
  readonly error = input<string | null>(null);

  readonly submitted = output<KbItemFormValue>();

  protected readonly types = KNOWLEDGE_BASE_ITEM_TYPES;
  protected readonly noTemplate = NO_TEMPLATE;
  protected readonly nameMaxLength = CUSTOM_TABLE_NAME_MAX_LENGTH;
  protected readonly descriptionMaxLength = CUSTOM_TABLE_DESCRIPTION_MAX_LENGTH;

  protected readonly title = linkedSignal(() => this.initial()?.title ?? '');
  protected readonly description = linkedSignal(() => this.initial()?.description ?? '');
  /** Тип нового матеріалу; поки лише таблиця (див. `KnowledgeBaseService.create`). */
  protected readonly type = signal<KnowledgeBaseItemType>('customtable');
  protected readonly templateId = linkedSignal({ source: this.templates, computation: () => NO_TEMPLATE });
  private readonly submittedOnce = signal(false);

  protected readonly titleError = computed(() => (this.submittedOnce() && !this.title().trim() ? 'common.required' : null));

  protected onText(target: typeof this.title, event: Event): void {
    target.set((event.target as HTMLInputElement | HTMLTextAreaElement).value);
  }

  protected onTemplate(event: Event): void {
    this.templateId.set((event.target as HTMLSelectElement).value);
  }

  protected submit(): void {
    this.submittedOnce.set(true);
    if (this.titleError() || this.busy()) {
      return;
    }
    const type = this.type();
    this.submitted.emit({
      title: this.title().trim(),
      description: this.description().trim() || null,
      type,
      templateId: type === 'customtable' && this.templateId() !== NO_TEMPLATE ? this.templateId() : null,
    });
  }
}
