import { TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import { LanguageService } from './language.service';
import { LANGUAGES } from './languages.generated';

const STORAGE_KEY = 'yavir.language';

describe('LanguageService', () => {
  /**
   * Створює сервіс так, ніби застосунок відкрили на пристрої з мовою `device`: `stored` — мова,
   * запамʼятована для нових вкладок, `tab` — мова, яку вже має ця вкладка.
   */
  function create(options: { stored?: string; tab?: string; device?: string } = {}) {
    if (options.stored !== undefined) {
      localStorage.setItem(STORAGE_KEY, options.stored);
    }
    if (options.tab !== undefined) {
      sessionStorage.setItem(STORAGE_KEY, options.tab);
    }
    vi.spyOn(navigator, 'language', 'get').mockReturnValue(options.device ?? 'uk-UA');
    TestBed.configureTestingModule({ providers: [provideTranslateService()] });
    const translate = TestBed.inject(TranslateService);
    const use = vi.spyOn(translate, 'use');
    const service = TestBed.inject(LanguageService);
    return { service, translate, use };
  }

  /** Те, що бачить ця вкладка, коли мову змінили в іншій. */
  function changeInAnotherTab(key: string, newValue: string | null, oldValue: string | null = 'uk'): void {
    window.dispatchEvent(new StorageEvent('storage', { key, newValue, oldValue }));
  }

  beforeEach(() => localStorage.clear());

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
    document.documentElement.lang = '';
  });

  describe('the language at the start', () => {
    it('is the saved one, whatever the language of the device', () => {
      const { service, use } = create({ stored: 'crh', device: 'en-US' });
      expect(service.language()).toBe('crh');
      expect(use).toHaveBeenCalledWith('crh');
    });

    it('is the language of the device (without the region) when nothing is saved, and it is saved at once', () => {
      const { service, use } = create({ device: 'be-BY' });
      expect(service.language()).toBe('be');
      expect(use).toHaveBeenCalledWith('be');
      expect(localStorage.getItem(STORAGE_KEY)).toBe('be');
    });

    it.each([['fr-FR'], ['pl'], ['']])('is English when the language of the device is %j, which has no translation file', (device) => {
      const { service } = create({ device });
      expect(service.language()).toBe('en');
      expect(localStorage.getItem(STORAGE_KEY)).toBe('en');
    });

    it('ignores a saved language that has no translation file and takes the one of the device', () => {
      const { service } = create({ stored: 'pl', device: 'uk-UA' });
      expect(service.language()).toBe('uk');
      expect(localStorage.getItem(STORAGE_KEY)).toBe('uk');
    });

    it('sets Ukrainian as the fallback for the strings a language has not', () => {
      const { translate } = create({ stored: 'en' });
      expect(translate.getFallbackLang()).toBe('uk');
    });

    it('still works when localStorage cannot be read or written', () => {
      vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
        throw new Error('denied');
      });
      vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
        throw new Error('denied');
      });
      const { service } = create({ device: 'be-BY' });
      expect(service.language()).toBe('be');
      expect(() => service.applyPreferred('uk')).not.toThrow();
      expect(service.language()).toBe('uk');
    });
  });

  describe('the lang attribute of the page (what the browser and a screen reader take the language from)', () => {
    it('is the language at the start and follows every change', () => {
      const { service } = create({ stored: 'uk' });
      expect(document.documentElement.lang).toBe('uk');

      service.applyPreferred('en');
      expect(document.documentElement.lang).toBe('en');
    });
  });

  describe('the language of a tab (every tab has its own: the accounts in them may differ)', () => {
    it('is the one this tab already has, whatever is remembered for new tabs', () => {
      const { service } = create({ tab: 'crh', stored: 'en' });
      expect(service.language()).toBe('crh');
    });

    it('starts a new tab with the remembered language and keeps it as its own', () => {
      const { service } = create({ stored: 'en' });
      expect(service.language()).toBe('en');
      expect(sessionStorage.getItem(STORAGE_KEY)).toBe('en');
    });

    it('does not follow a change of the language in another tab', () => {
      const { service, use } = create({ stored: 'uk' });
      use.mockClear();
      localStorage.setItem(STORAGE_KEY, 'en');
      changeInAnotherTab(STORAGE_KEY, 'en');
      expect(service.language()).toBe('uk');
      expect(use).not.toHaveBeenCalled();
    });

    it('remembers its language for new tabs only while the tab is seen, and again when the user comes back to it', () => {
      const { service } = create({ stored: 'uk' });
      const state = vi.spyOn(document, 'visibilityState', 'get').mockReturnValue('hidden');

      service.applyPreferred('en');
      expect(sessionStorage.getItem(STORAGE_KEY)).toBe('en');
      expect(localStorage.getItem(STORAGE_KEY)).toBe('uk');

      state.mockReturnValue('visible');
      document.dispatchEvent(new Event('visibilitychange'));
      expect(localStorage.getItem(STORAGE_KEY)).toBe('en');
    });
  });

  describe('applyPreferred (the language of the profile)', () => {
    it('switches the interface to a supported language, applies it and remembers it', () => {
      const { service, use } = create({ stored: 'uk' });
      expect(service.applyPreferred('en')).toBe(true);
      expect(service.language()).toBe('en');
      expect(use).toHaveBeenLastCalledWith('en');
      expect(sessionStorage.getItem(STORAGE_KEY)).toBe('en');
      expect(localStorage.getItem(STORAGE_KEY)).toBe('en');
    });

    it('does nothing when the language is already in use', () => {
      const { service, use } = create({ stored: 'uk' });
      use.mockClear();
      expect(service.applyPreferred('uk')).toBe(false);
      expect(use).not.toHaveBeenCalled();
    });

    it.each([[null], [undefined], [''], ['   '], ['pl'], ['xx-YY']])('ignores %j: nothing to switch to', (code) => {
      const { service } = create({ stored: 'uk' });
      expect(service.applyPreferred(code)).toBe(false);
      expect(service.language()).toBe('uk');
      expect(localStorage.getItem(STORAGE_KEY)).toBe('uk');
    });

    it('takes the language with surrounding spaces', () => {
      const { service } = create({ stored: 'uk' });
      expect(service.applyPreferred(' crh ')).toBe(true);
      expect(service.language()).toBe('crh');
    });

    it('takes every language that has a translation file (the generated list), and only those', () => {
      const { service } = create({ stored: 'uk' });
      for (const { code } of LANGUAGES) {
        service.applyPreferred(code);
        expect(service.language()).toBe(code);
      }
    });
  });
});
