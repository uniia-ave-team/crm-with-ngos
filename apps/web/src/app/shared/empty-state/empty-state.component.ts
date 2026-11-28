import { Component } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

/**
 * Порожній стан: ілюстрація (`assets/empty_light.svg`, у темній темі — `empty_dark.svg`, за
 * `prefers-color-scheme`) і під нею «Тут поки порожньо» (`common.emptyState`), посередині. Ілюстрація
 * лише прикрашає (`alt=""`), текст читають екранні читачі.
 *
 *   <app-empty-state />
 */
@Component({
  selector: 'app-empty-state',
  templateUrl: './empty-state.component.html',
  styleUrls: ['./empty-state.component.scss'],
  imports: [TranslatePipe],
})
export class EmptyStateComponent {}
