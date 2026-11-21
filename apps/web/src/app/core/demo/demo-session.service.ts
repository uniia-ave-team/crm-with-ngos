import { DestroyRef, Injectable, Signal, computed, effect, inject, signal, untracked } from '@angular/core';

import { AuthService } from '../auth/auth.service';
import { readJwtDemoExpiry } from '../auth/jwt';

/** Коли до скидання демо-даних лишилось стільки секунд чи менше, лічильник виділяється. */
export const DEMO_ENDING_SECONDS = 5 * 60;

const TICK_MS = 1000;

/**
 * Час, що лишився, для лічильника: `мм:сс`, а від години — `г:мм:сс` (`59:12`, `1:00:00`).
 * Відʼємне чи нечислове значення — `00:00`.
 */
export function formatCountdown(totalSeconds: number): string {
  const seconds = Number.isFinite(totalSeconds) ? Math.max(0, Math.floor(totalSeconds)) : 0;
  const pad = (value: number) => String(value).padStart(2, '0');
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  const rest = pad(seconds % 60);
  return hours > 0 ? `${hours}:${pad(minutes)}:${rest}` : `${pad(minutes)}:${rest}`;
}

/**
 * Зворотний відлік демо-сеансу. Копія демо-даних відвідувача живе фіксований час від
 * створення, і момент її видалення сервер кладе в кожен access-токен (claim `demo_exp`, Unix-час
 * у секундах). Сервіс читає його з токена **активного акаунта** (після оновлення токенів чи
 * повторного входу — заново), раз на секунду оновлює `secondsLeft`, а коли час вийшов — сам
 * завершує демо-сеанс тим самим шляхом, що й відповідь сервера `DEMO_SESSION_EXPIRED`
 * (`AuthService.endDemoSession`: акаунт забувається, а `SessionService` веде на сторінку входу з
 * поясненням), не чекаючи запиту, який не вдасться.
 *
 * Створюється разом із застосунком (`app.config.ts`), тож сеанс завершується вчасно на будь-якій
 * сторінці, а не лише там, де є банер. Таймер працює, лише поки в токені є `demo_exp`, і
 * знімається разом із сервісом. Час береться з годинника пристрою: якщо він відстає, сеанс
 * завершить відповідь сервера.
 */
@Injectable({ providedIn: 'root' })
export class DemoSessionService {
  private readonly auth = inject(AuthService);

  private readonly now = signal(Date.now());

  /** Коли демо-дані буде видалено (мс від епохи) за токеном активного акаунта; `null` — у токені цього нема. */
  readonly expiresAt: Signal<number | null> = computed(() => {
    const seconds = readJwtDemoExpiry(this.auth.accessToken());
    return seconds === null ? null : seconds * 1000;
  });

  /** Скільки секунд лишилось до скидання демо-даних (не менше нуля); `null` — відліку нема. */
  readonly secondsLeft: Signal<number | null> = computed(() => {
    const expiresAt = this.expiresAt();
    return expiresAt === null ? null : Math.max(0, Math.ceil((expiresAt - this.now()) / 1000));
  });

  /** Лишилось `DEMO_ENDING_SECONDS` чи менше — лічильник варто виділити. */
  readonly ending: Signal<boolean> = computed(() => {
    const seconds = this.secondsLeft();
    return seconds !== null && seconds <= DEMO_ENDING_SECONDS;
  });

  private timer: ReturnType<typeof setInterval> | null = null;

  constructor() {
    // Активний акаунт чи його токен змінився — відлік іде вже за новим `demo_exp` (або зупиняється).
    effect(() => {
      const expiresAt = this.expiresAt();
      untracked(() => this.watch(expiresAt));
    });

    // У фонових вкладках таймери запізнюються: щойно вкладка знову видима — звіряємо час одразу.
    const onVisible = () => {
      if (document.visibilityState === 'visible') {
        this.tick();
      }
    };
    document.addEventListener('visibilitychange', onVisible);

    inject(DestroyRef).onDestroy(() => {
      this.clearTimer();
      document.removeEventListener('visibilitychange', onVisible);
    });
  }

  private watch(expiresAt: number | null): void {
    this.clearTimer();
    if (expiresAt === null) {
      return;
    }
    this.timer = setInterval(() => this.tick(), TICK_MS);
    this.tick();
  }

  /** Оновлює поточний час; час вийшов — завершує демо-сеанс активного акаунта. */
  private tick(): void {
    const now = Date.now();
    this.now.set(now);
    const expiresAt = this.expiresAt();
    if (expiresAt !== null && expiresAt <= now) {
      this.clearTimer();
      this.auth.endDemoSession(this.auth.sessionId());
    }
  }

  private clearTimer(): void {
    if (this.timer !== null) {
      clearInterval(this.timer);
      this.timer = null;
    }
  }
}
