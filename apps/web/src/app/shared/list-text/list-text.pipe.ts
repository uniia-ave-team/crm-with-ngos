import { Pipe, PipeTransform, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

import { joinList } from './list-text';

/** Чим зʼєднати останній елемент переліку: «та» (`and`) чи «або» (`or`). */
export type ListKind = 'and' | 'or';

/**
 * Перелік із масиву мовою інтерфейсу: `['PNG', 'WebP', 'SVG'] | listText: 'or'` дає
 * «PNG, WebP або SVG». Роздільник і сполучники беруться з мовного файлу (`common.list.*`), тож
 * перелік завжди тією самою мовою, що й текст навколо, а мова з іншим розділовим знаком чи
 * напрямом письма задає свої.
 *
 * Результат підставляють у текст параметром:
 * `'admin.logoHint' | translate: { types: names | listText: 'or' }`.
 *
 * Канал не чистий (`pure: false`), як і `translate`: після зміни мови він має дати новий
 * перелік, хоча масив той самий.
 */
@Pipe({ name: 'listText', pure: false })
export class ListTextPipe implements PipeTransform {
  private readonly translate = inject(TranslateService);

  transform(items: readonly string[], kind: ListKind): string {
    const text = (key: string) => this.translate.instant(`common.list.${key}`) as string;
    return joinList(items, text('separator'), text(kind));
  }
}
