import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { SecondaryPanelComponent } from '../../core/layout/secondary-panel/secondary-panel.component';
import { KNOWLEDGE_BASE_SAMPLE_URL, KNOWLEDGE_BASE_SOURCE, KnowledgeBaseService } from '../../core/knowledge-base/knowledge-base.service';
import { KbGuideComponent } from './kb-guide.component';

describe('KbGuideComponent', () => {
  let http: HttpTestingController;
  // Як відповідь сервера: без головної (її додає клієнт).
  const entries = [
    { id: 'f1', type: 'folder', title: 'Розділ', items: [{ id: 'p1', type: 'page', title: 'Документ' }] },
    { id: 't1', type: 'customtable', title: 'Таблиця' },
  ];

  beforeEach(() => {
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]), provideTranslateService({ lang: 'uk', fallbackLang: 'uk' }),
        // Зразковий зміст: тут перевіряється вигляд папок і сторінок (у таблиць їх нема).
        { provide: KNOWLEDGE_BASE_SOURCE, useValue: 'sample' },
      ],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => vi.restoreAllMocks());

  function render() {
    // Зміст підвантажує сторінка «Бази знань» (оболонка видів); тут — так само.
    void TestBed.inject(KnowledgeBaseService).load();
    const fixture = TestBed.createComponent(KbGuideComponent);
    fixture.detectChanges();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  }

  async function settle(fixture: { detectChanges(): void; whenStable(): Promise<unknown> }) {
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('loads the contents and opens on the guide built from it', async () => {
    const { fixture, element } = render();
    expect(element.querySelector('.kb-view__message')?.textContent?.trim()).toBe(uk.knowledgeBase.loading);

    http.expectOne(KNOWLEDGE_BASE_SAMPLE_URL).flush(entries);
    await settle(fixture);
    expect(element.querySelector('.kb-contents__title')?.textContent?.trim()).toBe(uk.knowledgeBase.guide);
    expect(Array.from(element.querySelectorAll('.kb-contents__grid h2, .kb-contents__item')).map((e) => e.textContent?.trim())).toEqual([
      'Розділ',
      'Документ',
      'Таблиця',
    ]);
  });

  it('says when the contents could not be loaded, and asks again on "Try again"', async () => {
    const { fixture, element } = render();
    http.expectOne(KNOWLEDGE_BASE_SAMPLE_URL).flush({}, { status: 500, statusText: 'x' });
    await settle(fixture);
    expect(element.querySelector('.kb-view__message--error')?.textContent).toContain(uk.knowledgeBase.error);

    (element.querySelector('.kb-view__retry') as HTMLButtonElement).click();
    http.expectOne(KNOWLEDGE_BASE_SAMPLE_URL).flush(entries);
    await settle(fixture);
    expect(element.querySelector('app-kb-contents')).not.toBeNull();
  });

  it('has the same groups and pages as the tree in the side menu, in the same order (one load for both)', async () => {
    const { fixture, element } = render();
    const panel = TestBed.createComponent(SecondaryPanelComponent);
    panel.componentRef.setInput('section', 'knowledge-base');
    panel.detectChanges();
    http.expectOne(KNOWLEDGE_BASE_SAMPLE_URL).flush(entries);
    await settle(fixture);
    await settle(panel);

    const contents = Array.from(element.querySelectorAll('.kb-contents__grid h2, .kb-contents__item')).map((e) => e.textContent?.trim());
    const tree = Array.from((panel.nativeElement as HTMLElement).querySelectorAll('.tree__group-label, .tree__row--item')).map((e) =>
      e.textContent?.trim(),
    );
    expect(contents).toEqual(tree);
  });
});
