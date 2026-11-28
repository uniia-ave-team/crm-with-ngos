import { Component, ElementRef, computed, effect, inject, input, model, output, signal, viewChild } from '@angular/core';
import { IonIcon } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

import { TAG_TITLE_MAX_LENGTH, tagKey } from '../../projects/projects.service';

/** Лічильник полів: у кожного списку свій `id`. */
let nextId = 0;

/**
 * Поле тегів (як у YouTube): вибрані теги — плашками в самому полі (з хрестиком), за ними — текст.
 * Кома чи Enter перетворює набране на тег (вставлений текст із комами — на кілька), Backspace у
 * порожньому полі прибирає останній. Під полем — підказки з наявних тегів (`suggestions`: ті, що
 * містять набране, без уже вибраних; видно щонайбільше 4, далі прокрутка), прикріплені до поля, як
 * список колег; клік додає тег. Назва — до 100 символів; та сама назва (без регістру) двічі не
 * додається. Без `allowNew` тег може бути лише з наявних: набране, якого нема серед них, тегом не
 * стає. Значення — назви (`tags`).
 */
@Component({
  selector: 'app-tag-input',
  templateUrl: './tag-input.component.html',
  styleUrls: ['./tag-input.component.scss'],
  imports: [IonIcon, TranslatePipe],
})
export class TagInputComponent {
  /** Вибрані теги (назви). */
  readonly tags = model<readonly string[]>([]);
  /** Наявні теги проєкту (назви) — для підказок. */
  readonly suggestions = input<readonly string[]>([]);
  /** Чи можна створювати нові теги (інакше — лише з наявних). */
  readonly allowNew = input(true);
  /** `id` поля (для підпису `<label for>` у формі). */
  readonly inputId = input<string | null>(null);
  /** Де закінчується відкритий список підказок — px від верху цього поля (0 — список закритий). */
  readonly overlayBottom = output<number>();

  protected readonly maxLength = TAG_TITLE_MAX_LENGTH;
  protected readonly text = signal('');
  protected readonly focused = signal(false);
  protected readonly listId = `tag-input-${++nextId}`;

  /** Підказки: наявні теги з набраним, без вибраних. */
  protected readonly matches = computed(() => {
    const chosen = new Set(this.tags().map(tagKey));
    const text = tagKey(this.text());
    return this.suggestions().filter((title) => !chosen.has(tagKey(title)) && (!text || tagKey(title).includes(text)));
  });
  protected readonly open = computed(() => this.focused() && this.matches().length > 0);

  private readonly input = viewChild.required<ElementRef<HTMLInputElement>>('textInput');
  private readonly dropdown = viewChild<ElementRef<HTMLElement>>('dropdown');
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;

  constructor() {
    // Висота відкритого списку змінюється з набраним — міряємо, де він закінчується.
    effect((onCleanup) => {
      const dropdown = this.dropdown()?.nativeElement;
      if (!dropdown) {
        this.overlayBottom.emit(0);
        return;
      }
      const measure = () => this.overlayBottom.emit(dropdown.getBoundingClientRect().bottom - this.host.getBoundingClientRect().top);
      measure();
      if (typeof ResizeObserver === 'function') {
        const observer = new ResizeObserver(measure);
        observer.observe(dropdown);
        onCleanup(() => observer.disconnect());
      }
    });
  }

  /** Новий текст: усе до останньої коми — теги, решта лишається в полі. */
  protected onInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    const parts = value.split(',');
    const rest = parts.pop() ?? '';
    parts.forEach((part) => this.add(part));
    this.text.set(rest);
    // Поле показує лише залишок (після коми).
    (event.target as HTMLInputElement).value = rest;
  }

  /** Enter — набране стає тегом (форма не відправляється); Backspace у порожньому полі — без останнього тегу. */
  protected onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter') {
      event.preventDefault();
      this.commit();
    } else if (event.key === 'Backspace' && !this.text() && this.tags().length) {
      this.tags.update((tags) => tags.slice(0, -1));
    }
  }

  /** Вийшли з поля: набране теж стає тегом. */
  protected onBlur(): void {
    this.focused.set(false);
    this.commit();
  }

  protected pick(title: string): void {
    this.add(title);
    this.text.set('');
    this.input().nativeElement.value = '';
  }

  protected remove(title: string): void {
    this.tags.update((tags) => tags.filter((tag) => tag !== title));
    this.input().nativeElement.focus();
  }

  private commit(): void {
    if (this.text().trim()) {
      this.add(this.text());
      this.text.set('');
      this.input().nativeElement.value = '';
    }
  }

  /** Додає тег, якщо його ще нема; без `allowNew` — лише наявний (з його написанням). */
  private add(raw: string): void {
    const title = raw.trim().slice(0, TAG_TITLE_MAX_LENGTH);
    if (!title || this.tags().some((tag) => tagKey(tag) === tagKey(title))) {
      return;
    }
    const known = this.suggestions().find((suggestion) => tagKey(suggestion) === tagKey(title));
    if (known || this.allowNew()) {
      this.tags.update((tags) => [...tags, known ?? title]);
    }
  }
}
