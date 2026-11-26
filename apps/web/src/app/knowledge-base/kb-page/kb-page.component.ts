import { Component, input } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { injectKnowledgeBaseEntry } from '../kb-entry';

/**
 * Вид «сторінка» (`/knowledge-base/pages/:id`, `id` — вхід із адреси) — запланований: заголовок —
 * назва сторінки зі змісту, під ним заглушка. TODO: зміст сторінки (документ), коли на сервері
 * зʼявляться сторінки.
 */
@Component({
  selector: 'app-kb-page',
  templateUrl: './kb-page.component.html',
  styleUrls: ['../kb-view.scss'],
  imports: [TranslatePipe],
})
export class KbPageComponent {
  /** `id` сторінки з адреси. */
  readonly id = input.required<string>();

  protected readonly entry = injectKnowledgeBaseEntry(this.id);
}
