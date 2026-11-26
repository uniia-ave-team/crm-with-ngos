import { NgTemplateOutlet } from '@angular/common';
import { Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { IonIcon } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

import {
  KNOWLEDGE_BASE_PAGE_TYPES,
  KnowledgeBaseEntry,
  KnowledgeBaseFolder,
  KnowledgeBasePage,
  isKnowledgeBaseFolder,
  knowledgeBaseLink,
} from '../../core/knowledge-base/knowledge-base';
import { KbItemMenuComponent } from '../../core/knowledge-base/kb-item-menu/kb-item-menu.component';
import { KnowledgeBaseService } from '../../core/knowledge-base/knowledge-base.service';

/** Блок сітки «Посібника»: група сторінок (розділ) чи всі матеріали без папки одним стовпчиком. */
type ContentsBlock = { readonly group: KnowledgeBaseFolder } | { readonly pages: readonly KnowledgeBasePage[] };

/**
 * «Посібник» — вид «зміст» основного блоку «Бази знань» (поряд будуть «таблиця» й «сторінка»):
 * заголовок і вітальний текст із мовного файлу (`knowledgeBase.guide`, `knowledgeBase.intro`) і
 * сітка блоків, побудована з того самого змісту, що й дерево бокового меню (`entries`). Група —
 * розділ: назва з лінією під нею і список сторінок; усі матеріали без папки — один блок-стовпчик
 * (на місці першого з них), а не окремий блок кожен.
 * Головна сторінка — це сам «Посібник», тож у сітці її нема. Сторінка — іконка за типом
 * (сторінка — `document-outline`, таблиця — `grid-outline`) і назва — посилання на вид
 * матеріалу (таблиця — `/knowledge-base/tables/{id}`, сторінка — `/knowledge-base/pages/{id}`).
 *
 * При наведенні (чи фокусі з клавіатури) рядок сторінки чи заголовок групи виділяється підложкою,
 * а біля назви зʼявляються три крапки — меню дій (`app-kb-item-menu`). Місце під них є завжди,
 * тож назва переноситься однаково з наведенням і без: блок не ширшає й не стрибає. На телефоні й
 * планшеті (до 992px) три крапки видно завжди й вони більші, а «+» папки там нема: це пункт
 * «Додати в папку» в меню. На пристроях без наведення три крапки видно завжди. Розміри
 * шрифтів — як на сторінці «Адмін»: заголовок 24px, решта 15px.
 *
 * Назва папки (групи) виглядає наведеною завжди: підложка, обрис і видимі «+» (додати сторінку в
 * папку — `KnowledgeBaseService.requestInsert` з її `id` і назвою) та три крапки.
 *
 * Сітка — колонки щонайменше 280px, скільки вміщується: на телефоні одна, на широкому екрані
 * три й більше; блоки йдуть рядками, а висота рядка — за найвищим блоком у ньому.
 */
@Component({
  selector: 'app-kb-contents',
  templateUrl: './kb-contents.component.html',
  styleUrls: ['./kb-contents.component.scss'],
  imports: [IonIcon, KbItemMenuComponent, NgTemplateOutlet, RouterLink, TranslatePipe],
})
export class KbContentsComponent {
  readonly entries = input.required<readonly KnowledgeBaseEntry[]>();

  protected readonly knowledgeBase = inject(KnowledgeBaseService);

  protected readonly types = KNOWLEDGE_BASE_PAGE_TYPES;
  /** Адреса виду таблиці чи сторінки. */
  protected readonly link = knowledgeBaseLink;

  protected readonly blocks = computed<readonly ContentsBlock[]>(() => {
    const loose = this.entries().filter((entry): entry is KnowledgeBasePage => !isKnowledgeBaseFolder(entry) && entry.type !== 'home');
    let looseShown = false;
    return this.entries().flatMap((entry): ContentsBlock[] => {
      if (isKnowledgeBaseFolder(entry)) {
        return [{ group: entry }];
      }
      if (entry.type === 'home' || looseShown) {
        return [];
      }
      looseShown = true;
      return [{ pages: loose }];
    });
  });

  /** «+» папки: додати сторінку в неї (див. `KnowledgeBaseService.requestInsert`). */
  protected addToFolder(group: KnowledgeBaseFolder): void {
    this.knowledgeBase.requestInsert({ folder: { id: group.id, title: group.title } });
  }
}
