import { Component, inject } from '@angular/core';

import { ConnectionService } from '../../connection/connection.service';

/**
 * Тонкий банер демо-режиму над оболонкою застосунку (усі сторінки після входу): пояснює,
 * що це демо, що зміни бачить лише сам відвідувач і коли вони скидаються. Закрити його не
 * можна. Поза демо-режимом (`ConnectionService.demo` — `null`) нічого не малює.
 */
@Component({
  selector: 'app-demo-banner',
  templateUrl: './demo-banner.component.html',
  styleUrls: ['./demo-banner.component.scss'],
  standalone: false,
})
export class DemoBannerComponent {
  protected readonly demo = inject(ConnectionService).demo;
}
