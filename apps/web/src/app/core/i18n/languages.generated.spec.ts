import { LANGUAGES } from './languages.generated';

describe('LANGUAGES (generated from src/assets/i18n/*.json)', () => {
  it('has the application languages that have a translation file', () => {
    const codes = LANGUAGES.map((language) => language.code);
    for (const code of ['uk', 'en', 'crh', 'be']) {
      expect(codes).toContain(code);
    }
  });

  it('gives every language a non-empty name (the code when the file has none)', () => {
    for (const language of LANGUAGES) {
      expect(language.name.trim().length).toBeGreaterThan(0);
    }
  });

  it('is sorted by code and has no duplicates', () => {
    const codes = LANGUAGES.map((language) => language.code);
    expect(codes).toEqual([...codes].sort((a, b) => a.localeCompare(b)));
    expect(new Set(codes).size).toBe(codes.length);
  });

  it('uses only codes that fit the server limit of the preferred language (10 characters)', () => {
    for (const language of LANGUAGES) {
      expect(language.code.length).toBeLessThanOrEqual(10);
    }
  });
});
