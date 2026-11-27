import { Component, computed, inject } from '@angular/core';
import { TranslatePipe } from '@ngx-translate/core';

import { ConnectionService } from '../../connection/connection.service';
import { DemoSessionService, formatCountdown } from '../../demo/demo-session.service';

/**
 * Тонкий банер демо-режиму над оболонкою застосунку (усі сторінки після входу): пояснює,
 * що це демо, що зміни бачить лише сам відвідувач, попереджає, що розробка ще триває (можливі
 * помилки, деякі сервіси ще недоступні), і показує зворотний відлік до скидання
 * демо-даних (`DemoSessionService`: щосекунди, за `demo_exp` токена активного акаунта; коли
 * лишилось 5 хвилин чи менше, банер виділено). Якщо токен не каже, коли дані зникнуть, —
 * замість лічильника текст із тривалістю сеансу. Закрити банер не можна. Поза демо-режимом
 * (`ConnectionService.demo` — `null`) нічого не малює.
 */
@Component({
  selector: 'app-demo-banner',
  templateUrl: './demo-banner.component.html',
  styleUrls: ['./demo-banner.component.scss'],
  imports: [TranslatePipe],
})
export class DemoBannerComponent {
  private readonly session = inject(DemoSessionService);

  protected readonly demo = inject(ConnectionService).demo;

  /** Час до скидання демо-даних (`59:12`, `1:00:00`) або `null`, якщо відліку нема. */
  protected readonly timeLeft = computed(() => {
    const seconds = this.session.secondsLeft();
    return seconds === null ? null : formatCountdown(seconds);
  });

  protected readonly ending = this.session.ending;
}
