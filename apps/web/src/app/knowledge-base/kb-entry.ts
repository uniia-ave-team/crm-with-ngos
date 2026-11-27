import { Signal, computed, effect, inject } from '@angular/core';
import { Router } from '@angular/router';

import { KNOWLEDGE_BASE_URL, KnowledgeBaseEntry, findKnowledgeBaseEntry } from '../core/knowledge-base/knowledge-base';
import { KnowledgeBaseService } from '../core/knowledge-base/knowledge-base.service';

/**
 * Матеріал змісту, відкритий у виді (таблиця, сторінка), за `id` з адреси. Назву вид бере звідси,
 * тож після «Редагувати» вона оновлюється одразу. Коли зміст завантажено, а матеріалу в ньому нема
 * (видалили чи адреса застаріла), — перехід на «Посібник». Викликається в конструкторі виду.
 */
export function injectKnowledgeBaseEntry(id: Signal<string>): Signal<KnowledgeBaseEntry | null> {
  const knowledgeBase = inject(KnowledgeBaseService);
  const router = inject(Router);
  const entry = computed(() => findKnowledgeBaseEntry(knowledgeBase.state().entries, id()));
  effect(() => {
    if (knowledgeBase.state().status === 'ready' && !entry()) {
      void router.navigateByUrl(KNOWLEDGE_BASE_URL);
    }
  });
  return entry;
}
