import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { KNOWLEDGE_BASE_SAMPLE_URL, KNOWLEDGE_BASE_SOURCE, KnowledgeBaseService } from '../../core/knowledge-base/knowledge-base.service';
import { KbPageComponent } from './kb-page.component';

describe('KbPageComponent', () => {
  it('shows the name of the page from the contents and that pages do not open yet', async () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideTranslateService({ lang: 'uk', fallbackLang: 'uk' }),
        { provide: KNOWLEDGE_BASE_SOURCE, useValue: 'sample' },
      ],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    const loading = TestBed.inject(KnowledgeBaseService).load();
    TestBed.inject(HttpTestingController)
      .expectOne(KNOWLEDGE_BASE_SAMPLE_URL)
      .flush([{ id: 'f1', type: 'folder', title: 'Розділ', items: [{ id: 'p1', type: 'page', title: 'Документ' }] }]);
    await loading;

    const fixture = TestBed.createComponent(KbPageComponent);
    fixture.componentRef.setInput('id', 'p1');
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('h1')?.textContent?.trim()).toBe('Документ');
    expect(element.querySelector('.kb-view__message')?.textContent?.trim()).toBe(uk.knowledgeBase.page.planned);
  });
});
