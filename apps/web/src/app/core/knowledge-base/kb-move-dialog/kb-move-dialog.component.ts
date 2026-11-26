import { Component, computed, input, linkedSignal, output, signal } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { IonIcon } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

/** Папка у списках вікна «Перемістити». */
export interface KbMoveFolder {
  readonly id: string;
  readonly title: string;
}

/** Значення опції «Без папки» у списках (корінь змісту). */
const NO_FOLDER = '';

/**
 * Зміст вікна «Перемістити» (ставиться в `app-modal` під `app-dialog-header`): два списки —
 * «З папки» й «До папки» — з усіма папками й опцією «Без папки». «З папки» — де матеріал зараз
 * (лише показує, змінити не можна); «До папки» спершу — перша інша. `move` віддає папку
 * призначення (`null` — без папки).
 */
@Component({
  selector: 'app-kb-move-dialog',
  templateUrl: './kb-move-dialog.component.html',
  styleUrls: ['../../../settings/settings-section.scss', './kb-move-dialog.component.scss'],
  imports: [IonIcon, NgTemplateOutlet, TranslatePipe],
})
export class KbMoveDialogComponent {
  /** Усі папки змісту. */
  readonly folders = input.required<readonly KbMoveFolder[]>();
  /** Папка, де матеріал зараз; `null` — без папки. */
  readonly from = input.required<string | null>();

  readonly move = output<string | null>();

  protected readonly noFolder = NO_FOLDER;
  protected readonly fromValue = computed(() => this.from() ?? NO_FOLDER);

  /** Вибрана папка призначення: спершу перша, відмінна від поточної. */
  protected readonly toValue = linkedSignal(() => {
    const from = this.fromValue();
    const options = [NO_FOLDER, ...this.folders().map((folder) => folder.id)];
    return options.find((id) => id !== from) ?? from;
  });

  private readonly submitted = signal(false);

  protected readonly error = computed<string | null>(() =>
    this.submitted() && this.toValue() === this.fromValue() ? 'knowledgeBase.dialogs.move.same' : null,
  );

  protected onChange(event: Event): void {
    this.toValue.set((event.target as HTMLSelectElement).value);
  }

  protected submit(): void {
    this.submitted.set(true);
    if (!this.error()) {
      const to = this.toValue();
      this.move.emit(to === NO_FOLDER ? null : to);
    }
  }
}
