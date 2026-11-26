import { Component, inject } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { KnowledgeBaseService } from '../../core/knowledge-base/knowledge-base.service';
import { KbContentsComponent } from '../kb-contents/kb-contents.component';

/**
 * Вид «Посібник» (`/knowledge-base`): зміст бази знань сіткою (`app-kb-contents`). Поки зміст
 * завантажується — «Завантажуємо зміст…», невдача — повідомлення з «Спробувати ще раз».
 */
@Component({
  selector: 'app-kb-guide',
  templateUrl: './kb-guide.component.html',
  styleUrls: ['../kb-view.scss'],
  imports: [KbContentsComponent, TranslatePipe],
})
export class KbGuideComponent {
  private readonly knowledgeBase = inject(KnowledgeBaseService);
  protected readonly state = this.knowledgeBase.state;

  protected retry(): void {
    void this.knowledgeBase.load();
  }
}
