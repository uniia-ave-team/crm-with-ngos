import { Injectable, inject } from '@angular/core';
import { ToastController } from '@ionic/angular/lazy';
import { TranslateService } from '@ngx-translate/core';

/**
 * Сповіщення демо-режиму (ключ перекладу — `demo.toast.<назва>`):
 * - `restricted` — дію в демо вимкнено (`DEMO_MODE_RESTRICTED`);
 * - `quotaExceeded` — вичерпано ліміт демо (`DEMO_QUOTA_EXCEEDED`).
 */
export type DemoNotice = 'restricted' | 'quotaExceeded';

const TOAST_DURATION_MS = 5000;

/**
 * Показує сповіщення демо-режиму спливним повідомленням (`ion-toast`). Викликає його
 * `authInterceptor` за `errorCode` відповіді сервера, тож сторінкам про демо знати не треба.
 */
@Injectable({ providedIn: 'root' })
export class DemoNoticeService {
  private readonly toasts = inject(ToastController);
  private readonly translate = inject(TranslateService);

  /** Сповіщення, що зараз на екрані: те саме вдруге не показуємо (кілька запитів відхилено разом). */
  private readonly showing = new Set<DemoNotice>();

  async show(notice: DemoNotice): Promise<void> {
    if (this.showing.has(notice)) {
      return;
    }
    this.showing.add(notice);
    try {
      const toast = await this.toasts.create({
        message: this.translate.instant(`demo.toast.${notice}`),
        duration: TOAST_DURATION_MS,
        position: 'top',
      });
      await toast.present();
      await toast.onDidDismiss();
    } finally {
      this.showing.delete(notice);
    }
  }
}
