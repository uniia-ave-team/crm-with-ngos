import { Directive, ElementRef, effect, inject, input, untracked } from '@angular/core';

import { ProtectedImageService } from '../../core/http/protected-image.service';

/**
 * `src` для зображення, яке лежить за авторизацією API (логотип ГО, аватар):
 *
 *   <img [appProtectedSrc]="logoUrl" alt="" />
 *
 * Посилання, що потребує токена, завантажується через `ProtectedImageService`
 * (звичайний `<img src>` дав би 401 і порожнє місце), інші кладуться в `src`
 * одразу. Поки файл завантажується, `src` порожній. Не вдалося завантажити —
 * елемент отримує подію `error`, як і при битому посиланні, тож наявні
 * `(error)`-обробники (повернення до літер чи до порожнього блока) працюють.
 */
@Directive({
  selector: 'img[appProtectedSrc]',
})
export class ProtectedSrcDirective {
  readonly appProtectedSrc = input<string | null | undefined>(null);

  private readonly image: HTMLImageElement = inject<ElementRef<HTMLImageElement>>(ElementRef).nativeElement;
  private readonly images = inject(ProtectedImageService);
  /** Адреса, для якої йде (чи йшло) завантаження: запізніла відповідь на стару адресу ігнорується. */
  private current: string | null = null;

  constructor() {
    effect(() => {
      const url = this.appProtectedSrc()?.trim() || null;
      untracked(() => void this.apply(url));
    });
  }

  private async apply(url: string | null): Promise<void> {
    this.current = url;
    if (!url) {
      this.image.removeAttribute('src');
      return;
    }
    // Авторизація не потрібна — кладемо в `src` одразу.
    if (!this.images.needsAuth(url)) {
      this.image.setAttribute('src', url);
      return;
    }
    // Поки файл завантажується, старого зображення не показуємо.
    this.image.removeAttribute('src');
    const resolved = await this.images.resolve(url);
    if (this.current !== url) {
      return;
    }
    if (resolved) {
      this.image.setAttribute('src', resolved);
    } else {
      this.image.removeAttribute('src');
      this.image.dispatchEvent(new Event('error'));
    }
  }
}
