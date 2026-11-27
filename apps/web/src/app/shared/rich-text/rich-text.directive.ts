import { Directive, ElementRef, effect, inject, input, output } from '@angular/core';

import { sanitizeRichText } from './rich-text';

/** Команди форматування виділеного тексту. */
export type RichTextFormat = 'bold' | 'italic' | 'underline' | 'strikethrough';

/**
 * Редагований блок тексту з форматуванням (`contenteditable`): показує значення (`appRichText` —
 * HTML з `<b>`, `<i>`, `<u>`, `<s>`, `<br>`, див. `rich-text.ts`) і віддає змінене, вже очищене
 * (`richTextChange`). Значення ззовні теж очищується, перш ніж потрапити в блок: воно приходить із
 * сервера, і записати туди могли що завгодно (скрипт в атрибуті, стилі на всю сторінку). Значення ззовні записується в блок лише тоді, коли воно справді інше (інакше
 * курсор стрибав би на початок, поки людина пише). Enter — новий рядок (`<br>`), вставлений текст —
 * без форматування джерела.
 *
 *   <div contenteditable="true" role="textbox" [appRichText]="value" (richTextChange)="…"></div>
 */
@Directive({
  selector: '[appRichText]',
  host: {
    '(input)': 'emit()',
    '(beforeinput)': 'limit($event)',
    '(keydown.enter)': 'newLine($event)',
    '(paste)': 'pastePlain($event)',
  },
})
export class RichTextDirective {
  readonly appRichText = input<string>('');
  /**
   * Найдовше значення (HTML з форматуванням), яке прийме сервер; `null` — без обмеження. Введення й
   * вставлення понад нього не відбувається (вставлений текст обрізається), тож сервер не відповідає
   * незрозумілою відмовою.
   */
  readonly appRichTextMaxLength = input<number | null>(null);
  readonly richTextChange = output<string>();

  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;

  constructor() {
    effect(() => {
      const value = sanitizeRichText(this.appRichText() ?? '');
      if (sanitizeRichText(this.element.innerHTML) !== value) {
        this.element.innerHTML = value;
      }
    });
  }

  protected emit(): void {
    this.richTextChange.emit(sanitizeRichText(this.element.innerHTML));
  }

  /** Enter — перенос рядка (`<br>`), а не новий блок `<div>`. */
  protected newLine(event: Event): void {
    event.preventDefault();
    document.execCommand('insertLineBreak');
  }

  /** Вставлений текст — лише текст (форматування сторінки-джерела не потрібне), не довший за ліміт. */
  protected pastePlain(event: ClipboardEvent): void {
    const text = event.clipboardData?.getData('text/plain');
    if (text !== undefined) {
      event.preventDefault();
      const room = this.room();
      const fitting = room === null ? text : text.slice(0, Math.max(room, 0));
      if (fitting) {
        document.execCommand('insertText', false, fitting);
      }
    }
  }

  /** Введення понад ліміт (`appRichTextMaxLength`) не відбувається; видалення — завжди. */
  protected limit(event: InputEvent): void {
    if (!event.inputType.startsWith('insert') || event.inputType === 'insertFromPaste') {
      return;
    }
    const room = this.room();
    const adding = event.inputType === 'insertLineBreak' || event.inputType === 'insertParagraph' ? '<br>'.length : (event.data ?? ' ').length;
    if (room !== null && adding > room) {
      event.preventDefault();
    }
  }

  /** Скільки ще символів уміщається (`null` — без обмеження). */
  private room(): number | null {
    const max = this.appRichTextMaxLength();
    return max === null ? null : max - sanitizeRichText(this.element.innerHTML).length;
  }
}

/**
 * Форматує виділений текст у блоці з курсором: вмикає чи вимикає напівжирний, курсив, підкреслення
 * чи закреслення. `execCommand` застарілий, але іншого способу форматувати виділення в
 * `contenteditable` браузери не дають; зміна приходить подією `input` (тобто й у `richTextChange`).
 */
export function formatSelection(format: RichTextFormat): void {
  document.execCommand('styleWithCSS', false, 'false');
  document.execCommand(format === 'strikethrough' ? 'strikeThrough' : format);
}
