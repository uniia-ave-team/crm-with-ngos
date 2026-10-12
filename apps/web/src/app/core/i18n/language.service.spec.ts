import { TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import { LanguageService } from './language.service';

describe('LanguageService.applyPreferred', () => {
  let service: LanguageService;
  let translate: TranslateService;

  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem('yavir.language', 'uk');
    TestBed.configureTestingModule({ providers: [provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })] });
    translate = TestBed.inject(TranslateService);
    service = TestBed.inject(LanguageService);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it('switches the interface to a supported language, applies it and remembers it', () => {
    const use = vi.spyOn(translate, 'use');
    expect(service.applyPreferred('en')).toBe(true);
    expect(service.language()).toBe('en');
    expect(use).toHaveBeenCalledWith('en');
    expect(localStorage.getItem('yavir.language')).toBe('en');
  });

  it('does nothing when the language is already in use', () => {
    const use = vi.spyOn(translate, 'use');
    expect(service.applyPreferred('uk')).toBe(false);
    expect(use).not.toHaveBeenCalled();
  });

  it.each([[null], [undefined], [''], ['   '], ['pl'], ['xx-YY']])('ignores %j: nothing to switch to', (code) => {
    expect(service.applyPreferred(code)).toBe(false);
    expect(service.language()).toBe('uk');
    expect(localStorage.getItem('yavir.language')).toBe('uk');
  });

  it('takes the language with surrounding spaces', () => {
    expect(service.applyPreferred(' crh ')).toBe(true);
    expect(service.language()).toBe('crh');
  });
});
