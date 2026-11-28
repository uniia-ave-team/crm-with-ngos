import { Component, computed, input, linkedSignal, output, signal } from '@angular/core';
import { IonIcon } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

import {
  NewProject,
  PROJECT_DESCRIPTION_MAX_LENGTH,
  PROJECT_TITLE_MAX_LENGTH,
  PROJECT_VISIBILITIES,
  PROJECT_VISIBILITY_ICONS,
  ProjectVisibility,
} from '../projects.service';
import { DateRangeComponent, isDateRangeValid, today } from '../date-range/date-range.component';

/**
 * Форма проєкту (у `app-modal` під `app-dialog-header`, як форма «Бази знань» `app-kb-item-form`):
 * «Назва» (обовʼязкова, до 255 символів), «Опис» (до 2000), «Доступність» (вибір із трьох значень
 * `ProjectVisibility`, спершу — «Приватний»; перед вибором — іконка вибраного: замок, око, глобус;
 * під полем — що означає вибране) і дати (`app-date-range`: початок обовʼязковий, спершу — сьогодні;
 * кінець необовʼязковий, не раніше за початок). Помилки полів — після
 * першої спроби; без них форма віддає значення (`submitted`). У вікні «Редагувати» поля заповнені
 * даними проєкту (`initial`), а кнопка — «Зберегти» (`submitLabelKey`).
 */
@Component({
  selector: 'app-project-form',
  templateUrl: './project-form.component.html',
  styleUrls: ['../../../settings/settings-section.scss', './project-form.component.scss'],
  imports: [DateRangeComponent, IonIcon, TranslatePipe],
})
export class ProjectFormComponent {
  /** Дані, з якими форма відкривається (`null` — новий проєкт: порожня, початок — сьогодні). */
  readonly initial = input<NewProject | null>(null);
  /** Ключ підпису кнопки. */
  readonly submitLabelKey = input('planner.dialogs.add.submit');
  /** Запит іде: кнопка вимкнена. */
  readonly busy = input(false);
  /** Помилка попередньої спроби (ключ мовного файлу) або `null`. */
  readonly error = input<string | null>(null);

  readonly submitted = output<NewProject>();

  protected readonly titleMaxLength = PROJECT_TITLE_MAX_LENGTH;
  protected readonly descriptionMaxLength = PROJECT_DESCRIPTION_MAX_LENGTH;
  protected readonly visibilities = PROJECT_VISIBILITIES;
  protected readonly visibilityIcons = PROJECT_VISIBILITY_ICONS;

  protected readonly title = linkedSignal(() => this.initial()?.title ?? '');
  protected readonly description = linkedSignal(() => this.initial()?.description ?? '');
  protected readonly visibility = linkedSignal<ProjectVisibility>(() => this.initial()?.visibility ?? 0);
  protected readonly startDate = linkedSignal(() => this.initial()?.startDate ?? today());
  protected readonly endDate = linkedSignal(() => this.initial()?.endDate ?? '');
  protected readonly submittedOnce = signal(false);

  protected readonly titleError = computed(() => (this.submittedOnce() && !this.title().trim() ? 'common.required' : null));

  protected onText(target: typeof this.title, event: Event): void {
    target.set((event.target as HTMLInputElement | HTMLTextAreaElement).value);
  }

  protected onVisibility(event: Event): void {
    this.visibility.set(Number((event.target as HTMLSelectElement).value) as ProjectVisibility);
  }

  protected submit(): void {
    this.submittedOnce.set(true);
    if (this.busy() || this.titleError() || !isDateRangeValid(this.startDate(), this.endDate())) {
      return;
    }
    this.submitted.emit({
      title: this.title().trim(),
      description: this.description().trim() || null,
      visibility: this.visibility(),
      startDate: this.startDate(),
      endDate: this.endDate() || null,
    });
  }
}
