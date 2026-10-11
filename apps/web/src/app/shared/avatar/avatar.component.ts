import { Component, Signal, computed, inject, input, signal } from '@angular/core';

import { ConnectionService } from '../../core/connection/connection.service';
import { avatarColor } from './avatar-color';

/**
 * Аватар користувача: зображення (`avatarUrl`) або, якщо його нема, літери
 * (перші літери імені й прізвища) на кольорі, що рахується від імені,
 * прізвища й назви організації (`avatarColor`). Нема даних про користувача — стандартний синій
 * без літер.
 *
 * Розмір задає той, хто використовує: CSS-змінна `--avatar-size` (за
 * замовчуванням 40px) і за потреби `--avatar-font-size` (за замовчуванням
 * 35% розміру). Форма — squircle, як у меню.
 *
 *   <app-avatar [firstName]="user.firstName" [lastName]="user.lastName" [avatarUrl]="user.avatarUrl" [ngoName]="ngoName" />
 */
@Component({
  selector: 'app-avatar',
  templateUrl: './avatar.component.html',
  styleUrls: ['./avatar.component.scss'],
  standalone: false,
})
export class AvatarComponent {
  readonly firstName = input<string | null | undefined>('');
  readonly lastName = input<string | null | undefined>('');
  readonly avatarUrl = input<string | null | undefined>(null);
  /** Назва організації: бере участь у розрахунку кольору (див. `avatarColor`). */
  readonly ngoName = input<string | null | undefined>('');
  /**
   * Застарілий акаунт (сесію завершено): літери на сірій заливці, а зображення
   * (якщо є) затемнене напівпрозорим сірим шаром.
   */
  readonly outdated = input(false);

  private readonly connection = inject(ConnectionService);

  /** Адреса зображення, яке не вдалося завантажити (для неї повертаємось до літер). */
  private readonly failedUrl = signal<string | null>(null);

  /** Літери в аватарі: перші літери імені й прізвища у верхньому регістрі. */
  protected readonly initials: Signal<string> = computed(
    () => firstLetter(this.firstName()) + firstLetter(this.lastName()),
  );

  /** Колір тла з літерами (`null` — стандартний). */
  protected readonly background: Signal<string | null> = computed(() =>
    avatarColor(this.firstName() ?? '', this.lastName() ?? '', this.ngoName() ?? ''),
  );

  /** Зображення аватара, якщо воно є: тоді літер і кольору нема взагалі. Відносну адресу рахуємо від адреси сервера. */
  protected readonly image: Signal<string | null> = computed(() => {
    const url = this.avatarUrl()?.trim();
    if (!url) {
      return null;
    }
    let resolved: string;
    try {
      resolved = new URL(url, `${this.connection.serverUrl()}/`).toString();
    } catch {
      return null;
    }
    return resolved === this.failedUrl() ? null : resolved;
  });

  protected onImageError(): void {
    this.failedUrl.set(this.image());
  }
}

/** Перша літера (у верхньому регістрі) слова; порожній рядок, якщо слова нема. */
function firstLetter(name: string | null | undefined): string {
  const [letter] = Array.from((name ?? '').trim());
  return letter ? letter.toLocaleUpperCase() : '';
}
