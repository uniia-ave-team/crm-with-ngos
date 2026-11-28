import { Component, computed, input, linkedSignal, output, signal } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { TOPIC_TITLE_MAX_LENGTH } from '../projects.service';

/**
 * Форма колонки дошки (під `app-dialog-header`, як форма проєкту): «Назва колонки» (обовʼязкова, до
 * `TOPIC_TITLE_MAX_LENGTH` символів) і кнопка. Нова колонка (вікно дій проєкту) — порожня з
 * «Створити»; «Редагувати» з меню колонки — з її назвою (`initial`) і «Зберегти» (`submitLabelKey`).
 * Порожня назва — помилка під полем після спроби; інакше назва без пробілів по краях — `submitted`.
 */
@Component({
  selector: 'app-topic-form',
  templateUrl: './topic-form.component.html',
  styleUrls: ['../../../settings/settings-section.scss', './topic-form.component.scss'],
  imports: [TranslatePipe],
})
export class TopicFormComponent {
  /** Запит іде: кнопка вимкнена. */
  readonly busy = input(false);
  /** Помилка попередньої спроби (ключ мовного файлу) або `null`. */
  readonly error = input<string | null>(null);
  /** Назва, з якою форма відкривається. */
  readonly initial = input('');
  /** Ключ підпису кнопки. */
  readonly submitLabelKey = input('planner.dialogs.topic.submit');

  readonly submitted = output<string>();

  protected readonly maxLength = TOPIC_TITLE_MAX_LENGTH;
  protected readonly title = linkedSignal(() => this.initial());
  private readonly submittedOnce = signal(false);

  protected readonly titleError = computed(() => (this.submittedOnce() && !this.title().trim() ? 'common.required' : null));

  protected onTitle(event: Event): void {
    this.title.set((event.target as HTMLInputElement).value);
  }

  protected submit(): void {
    this.submittedOnce.set(true);
    if (!this.busy() && !this.titleError()) {
      this.submitted.emit(this.title().trim());
    }
  }
}
