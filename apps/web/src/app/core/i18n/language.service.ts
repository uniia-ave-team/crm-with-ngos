import { Injectable, Signal, inject, signal } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

/** Мови, підтримувані інтерфейсом. */
export type AppLanguage = 'uk' | 'en' | 'crh' | 'be';

const SUPPORTED_LANGUAGES: readonly AppLanguage[] = ['uk', 'en', 'crh', 'be'];
const STORAGE_KEY = 'yavir.language';

/**
 * "Запасна" мова на випадок, якщо рядка немає у файлі обраної мови.
 * Українська — основна мова інтерфейсу: у ній є всі рядки, а в інших файлах
 * їх може ще не бути (наприклад, тексти сторінки входу).
 */
const FALLBACK_LANGUAGE = 'uk';

/**
 * Визначення й перемикання мови інтерфейсу. На відміну від
 * ave-keyboard-web-site (звідки запозичено сам підхід із ngx-translate),
 * тут немає ні шляхів виду /uk/..., ні читання Accept-Language на сервері
 * (застосунок не рендериться на сервері), ні спеціальної обробки
 * російської — лише визначення мови пристрою при першому відкритті.
 */
@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly translate = inject(TranslateService);

  readonly supportedLanguages = SUPPORTED_LANGUAGES;

  private readonly languageSignal = signal<AppLanguage>(this.detectLanguage());
  readonly language: Signal<AppLanguage> = this.languageSignal.asReadonly();

  constructor() {
    // Застосовуємо й зберігаємо результат детекції одразу — інакше при
    // першому відкритті (коли в localStorage ще нічого нема) вибір мови
    // ніде не фіксується, і кожен наступний виклик detectLanguage() читає
    // порожнє сховище й перевизначає мову з нуля.
    this.applyLanguage(this.languageSignal());
    this.persistLanguage(this.languageSignal());

    // Синхронізація між вкладками: подія `storage` спрацьовує лише в ІНШИХ
    // вкладках того самого origin (спричинена звідти зміна), не в тій, де
    // її зробили, — такий уже механізм браузера, а не наше обмеження.
    window.addEventListener('storage', (event) => {
      if (event.key !== STORAGE_KEY || event.newValue === event.oldValue) {
        return;
      }
      const value = event.newValue;
      if (value && this.isSupported(value)) {
        this.languageSignal.set(value);
        this.applyLanguage(value);
      }
    });
  }

  setLanguage(language: AppLanguage): void {
    this.languageSignal.set(language);
    this.applyLanguage(language);
    this.persistLanguage(language);
  }

  private applyLanguage(language: AppLanguage): void {
    // ngx-translate 18 повертає Observable з обох викликів і не виконує
    // запит, доки на нього не підписались.
    this.translate.setFallbackLang(FALLBACK_LANGUAGE).subscribe();
    this.translate.use(language).subscribe();
  }

  private persistLanguage(language: AppLanguage): void {
    try {
      localStorage.setItem(STORAGE_KEY, language);
    } catch {
      // Сховище може бути недоступне (приватний режим тощо).
    }
  }

  private detectLanguage(): AppLanguage {
    const stored = this.readStoredLanguage();
    if (stored) {
      return stored;
    }
    const browserLanguage = (navigator.language || 'en').split('-')[0].toLowerCase();
    return this.isSupported(browserLanguage) ? browserLanguage : 'en';
  }

  private readStoredLanguage(): AppLanguage | null {
    try {
      const value = localStorage.getItem(STORAGE_KEY);
      return value && this.isSupported(value) ? value : null;
    } catch {
      return null;
    }
  }

  private isSupported(value: string): value is AppLanguage {
    return (SUPPORTED_LANGUAGES as readonly string[]).includes(value);
  }
}
