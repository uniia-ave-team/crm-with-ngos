import { Component, Signal, computed, inject, input } from '@angular/core';

import { LanguageService } from '../../core/i18n/language.service';
import { mobileLogoBase } from '../../core/i18n/mobile-logo';

/**
 * Прелоадер на всю сторінку: оверлей 100vw × 100vh із заливкою меню, а в центрі
 * мобільне лого, під ним спінер і текст «Будь ласка, зачекайте...» (з мовного
 * файлу). Показується, поки `visible` = `true`.
 *
 *   <app-preloader [visible]="busy()" />
 */
@Component({
  selector: 'app-preloader',
  templateUrl: './preloader.component.html',
  styleUrls: ['./preloader.component.scss'],
  standalone: false,
})
export class PreloaderComponent {
  readonly visible = input(false);

  private readonly languageService = inject(LanguageService);

  /** Базова назва файлу мобільного лого за мовою (`.svg` / `_dark.svg`). */
  protected readonly logoBase: Signal<string> = computed(() => mobileLogoBase(this.languageService.language()));
}
