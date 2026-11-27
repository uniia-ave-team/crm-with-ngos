import { TestBed } from '@angular/core/testing';
import { provideIonicAngular } from '@ionic/angular';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { DialogHeaderComponent } from './dialog-header.component';

describe('DialogHeaderComponent', () => {
  it('shows the title and a cross that asks to close', () => {
    TestBed.configureTestingModule({ providers: [provideIonicAngular(), provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })] });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    const fixture = TestBed.createComponent(DialogHeaderComponent);
    fixture.componentRef.setInput('title', 'Перейменувати');
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const dismiss = vi.fn();
    fixture.componentInstance.dismiss.subscribe(dismiss);

    expect(element.querySelector('h2')?.textContent?.trim()).toBe('Перейменувати');
    const close = element.querySelector('button') as HTMLButtonElement;
    expect(close.getAttribute('aria-label')).toBe(uk.common.close);
    close.click();
    expect(dismiss).toHaveBeenCalledOnce();
  });
});
