import { DOCUMENT } from '@angular/common';
import { DestroyRef, Injectable, Signal, inject, signal } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

import { LANGUAGES } from './languages.generated';

/**
 * Код мови інтерфейсу. Підтримуються мови, для яких є файл перекладів у
 * `src/assets/i18n` — їх список (`LANGUAGES`) генерується за цими файлами, тож нова мова
 * зʼявляється і у виборі в профілі, і тут, щойно додано її файл.
 */
export type AppLanguage = string;

const STORAGE_KEY = 'yavir.language';

/**
 * Запасна мова: з неї береться рядок, якого немає у файлі обраної мови. Українська — основна
 * мова інтерфейсу, у ній є всі рядки.
 */
const FALLBACK_LANGUAGE: AppLanguage = 'uk';

/** Мова при першому відкритті, коли мова пристрою не підтримується. */
const DEFAULT_LANGUAGE: AppLanguage = 'en';

/**
 * Визначення й перемикання мови інтерфейсу. Мова у кожної вкладки своя: у різних вкладках
 * можуть бути активні різні акаунти, а мову задає профіль (`applyPreferred`). Тому вона
 * зберігається в `sessionStorage` вкладки, а в `localStorage` лежить лише стартове значення
 * для нової вкладки — мова тієї, яку користувач бачив останньою. Вкладки одна одну не
 * перемикають.
 *
 * Мова на старті: збережена для цієї вкладки → стартова → мова пристрою → `DEFAULT_LANGUAGE`.
 * Обрана мова записується в `<html lang>` (за ним браузер і екранний диктор визначають мову
 * сторінки).
 */
@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly translate = inject(TranslateService);
  private readonly document = inject(DOCUMENT);

  private readonly languageSignal = signal<AppLanguage>(this.detectLanguage());
  readonly language: Signal<AppLanguage> = this.languageSignal.asReadonly();

  constructor() {
    // ngx-translate 18 повертає Observable з обох викликів (тут і в `applyLanguage`) і не
    // виконує запит, доки на нього не підписались.
    this.translate.setFallbackLang(FALLBACK_LANGUAGE).subscribe();

    // Застосовуємо й зберігаємо результат визначення одразу: інакше при першому відкритті
    // (коли у сховищі ще нічого нема) вибір мови ніде не фіксується.
    this.applyLanguage(this.languageSignal());
    this.persistLanguage(this.languageSignal());

    // Користувач повернувся до цієї вкладки — нова вкладка тепер має починати з її мови.
    const onVisibilityChange = (): void => this.rememberForNewTabs(this.languageSignal());
    this.document.addEventListener('visibilitychange', onVisibilityChange);
    // У застосунку сервіс живе, доки відкрита вкладка; слухача знімаємо для тестів, де сервіс
    // створюється заново.
    inject(DestroyRef).onDestroy(() => this.document.removeEventListener('visibilitychange', onVisibilityChange));
  }

  /**
   * Перемикає інтерфейс цієї вкладки на мову, яку користувач обрав у профілі
   * (`preferredLanguage`): застосовує її й запамʼятовує. Нічого не робить,
   * якщо мови нема, вона не підтримується (немає файлу перекладів) або вже діє. Повертає, чи
   * мову змінено.
   */
  applyPreferred(code: string | null | undefined): boolean {
    const language = code?.trim();
    if (!language || !isSupported(language) || language === this.languageSignal()) {
      return false;
    }
    this.languageSignal.set(language);
    this.applyLanguage(language);
    this.persistLanguage(language);
    return true;
  }

  private applyLanguage(language: AppLanguage): void {
    this.translate.use(language).subscribe();
    this.document.documentElement.lang = language;
  }

  /** Запамʼятовує мову за вкладкою і, якщо вкладку видно, як стартову для нових. */
  private persistLanguage(language: AppLanguage): void {
    try {
      sessionStorage.setItem(STORAGE_KEY, language);
    } catch {
      // Сховище може бути недоступне (приватний режим тощо).
    }
    this.rememberForNewTabs(language);
  }

  /**
   * Стартова мова нової вкладки — мова тієї, яку користувач бачив останньою; фонова вкладка
   * (наприклад, з іншим акаунтом) це значення не перезаписує.
   */
  private rememberForNewTabs(language: AppLanguage): void {
    if (this.document.visibilityState !== 'visible') {
      return;
    }
    try {
      localStorage.setItem(STORAGE_KEY, language);
    } catch {
      // Сховище може бути недоступне (приватний режим тощо).
    }
  }

  /** Мова цієї вкладки; без неї — стартова; без неї — мова пристрою; непідтримувана — `DEFAULT_LANGUAGE`. */
  private detectLanguage(): AppLanguage {
    const stored = this.readStoredLanguage(() => sessionStorage) ?? this.readStoredLanguage(() => localStorage);
    if (stored) {
      return stored;
    }
    // `uk-UA` → `uk`: файли перекладів названо кодом мови без регіону.
    const deviceLanguage = (navigator.language || '').split('-')[0].toLowerCase();
    if (isSupported(deviceLanguage)) {
      return deviceLanguage;
    }
    return isSupported(DEFAULT_LANGUAGE) ? DEFAULT_LANGUAGE : FALLBACK_LANGUAGE;
  }

  /** Підтримувана мова зі сховища; сховище передається функцією, бо сам доступ до нього може кинути помилку. */
  private readStoredLanguage(storage: () => Storage): AppLanguage | null {
    try {
      const value = storage().getItem(STORAGE_KEY);
      return value && isSupported(value) ? value : null;
    } catch {
      return null;
    }
  }
}

/** Чи є для мови файл перекладів (за згенерованим списком `LANGUAGES`). */
function isSupported(code: string): boolean {
  return LANGUAGES.some((language) => language.code === code);
}
