import { Component, input, linkedSignal, output } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { normalizeHex } from '../color/cell-colors';

/** Кольори на вибір: 10 пастельних і яскравіший червоний (ключ назви — `colorPicker.colors.<key>`). */
export const PASTEL_COLORS: readonly { readonly key: string; readonly hex: string }[] = [
  { key: 'pink', hex: '#fde2e4' },
  { key: 'rose', hex: '#fad2e1' },
  { key: 'peach', hex: '#ffe5b4' },
  { key: 'yellow', hex: '#fff1b6' },
  { key: 'lime', hex: '#e2f0cb' },
  { key: 'mint', hex: '#c7efcf' },
  { key: 'sky', hex: '#cdeaf5' },
  { key: 'periwinkle', hex: '#d4e4ff' },
  { key: 'lavender', hex: '#e4d8f5' },
  { key: 'sand', hex: '#ede6db' },
  // Яскравіший червоний — світлий рівно настільки, щоб текст на ньому мав контраст 7:1.
  { key: 'red', hex: '#ff6b6b' },
];

/**
 * Вибір кольору тла (зміст спливаючого вікна, `app-popover`): «Без кольору» (пунктирне порожнє коло),
 * 10 пастельних кольорів і яскравіший червоний — два рівні ряди по шість, під ними поле HEX тієї
 * самої ширини (`#rrggbb` чи `rrggbb`; Enter чи «Застосувати»), тож уся плашка — рівний
 * прямокутник. Вибраний колір позначено. `pick` — `#rrggbb` (малими) чи `null` (без кольору).
 */
@Component({
  selector: 'app-color-picker',
  templateUrl: './color-picker.component.html',
  styleUrls: ['./color-picker.component.scss'],
  imports: [TranslatePipe],
})
export class ColorPickerComponent {
  /** Поточний колір (`null` — нема). */
  readonly value = input<string | null>(null);
  readonly pick = output<string | null>();

  protected readonly colors = PASTEL_COLORS;
  protected readonly hex = linkedSignal(() => this.value() ?? '');
  protected readonly invalid = linkedSignal({ source: this.value, computation: () => false });

  protected onHexInput(event: Event): void {
    this.hex.set((event.target as HTMLInputElement).value);
    this.invalid.set(false);
  }

  protected applyHex(): void {
    const color = normalizeHex(this.hex());
    if (color) {
      this.pick.emit(color);
    } else {
      this.invalid.set(true);
    }
  }
}
