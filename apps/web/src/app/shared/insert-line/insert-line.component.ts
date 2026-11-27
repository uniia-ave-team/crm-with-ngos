import { Component, input, output } from '@angular/core';
import { IonIcon } from '@ionic/angular';

/**
 * «+» між рядками списку (на майбутнє; зараз ніде не використовується): тонка смуга нульової
 * висоти посеред проміжку між рядками, при наведенні на яку (чи фокусі з клавіатури) зʼявляються
 * лінія й кнопка «+» (подія `insert`). На пристроях без наведення її не видно.
 *
 *   <ul class="list">            <!-- flex-колонка з проміжком 8px -->
 *     <li>…</li>
 *     <li><app-insert-line [label]="…" (insert)="add(1)" /></li>
 *     <li>…</li>
 *   </ul>
 *
 * Сам елемент займає 0px, а відʼємні поля (`--insert-line-gap` / 2, за замовчуванням 4px) забирають
 * другий проміжок, який він додає в список; тож відступи між рядками не змінюються. Колір лінії й
 * кнопки — `--insert-line-accent` (за замовчуванням зелений кнопок «Додати»). Лінія займає всю
 * ширину батька: щоб «+» був частиною вкладеного блоку (папки), ставте його всередину цього блоку.
 */
@Component({
  selector: 'app-insert-line',
  templateUrl: './insert-line.component.html',
  styleUrls: ['./insert-line.component.scss'],
  imports: [IonIcon],
})
export class InsertLineComponent {
  /** Назва кнопки для допоміжних технологій. */
  readonly label = input.required<string>();
  readonly insert = output<void>();
}
