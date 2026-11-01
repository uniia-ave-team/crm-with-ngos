import { Component, computed, input } from '@angular/core';

import { roleColor } from './role-color';

/**
 * Плашка ролі (тег): колір рахується за назвою (`roleColor`, див. `role-color.ts`).
 *
 *   <app-role-pill name="Куратор" />            — активна: заливка з білим текстом
 *   <app-role-pill name="Куратор" [active]="false" />  — неактивна: рамка й текст на тлі сторінки
 *
 * Необовʼязкова іконка (`icon`, назва Ionicons) стоїть перед назвою:
 *
 *   <app-role-pill name="Куратор" icon="ellipse-outline" />
 *
 * Контраст із білим ≥ 7:1 для заливки й для рамки та тексту у світлій темі;
 * у темній темі рамка й текст неактивної плашки беруть світліший колір того
 * самого відтінку (контраст ≥ 7:1 із тлом темної теми). Назва без літер — нейтральний
 * колір меню.
 */
@Component({
  selector: 'app-role-pill',
  templateUrl: './role-pill.component.html',
  styleUrls: ['./role-pill.component.scss'],
  standalone: false,
})
export class RolePillComponent {
  readonly name = input.required<string>();
  /** Активна (заливка) чи ні (рамка). */
  readonly active = input(true);
  /** Назва іконки Ionicons перед назвою; `null` — без іконки. */
  readonly icon = input<string | null>(null);

  protected readonly color = computed(() => roleColor(this.name()));
}
