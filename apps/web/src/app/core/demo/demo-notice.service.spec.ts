import { TestBed } from '@angular/core/testing';
import { ToastController } from '@ionic/angular/lazy';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { DemoNoticeService } from './demo-notice.service';

describe('DemoNoticeService', () => {
  let service: DemoNoticeService;
  let create: ReturnType<typeof vi.fn>;
  /** Закриває сповіщення, показані досі (як це зробив би таймер `duration`). */
  let dismissAll: () => void;

  beforeEach(() => {
    const dismissals: Array<() => void> = [];
    dismissAll = () => dismissals.splice(0).forEach((dismiss) => dismiss());
    create = vi.fn(async () => ({
      present: vi.fn().mockResolvedValue(undefined),
      onDidDismiss: () => new Promise<void>((resolve) => dismissals.push(resolve)),
    }));
    TestBed.configureTestingModule({
      providers: [
        provideTranslateService({ lang: 'uk', fallbackLang: 'uk' }),
        { provide: ToastController, useValue: { create } },
      ],
    });
    // Тексти живуть в uk.json — беремо їх звідти, а не дублюємо.
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    service = TestBed.inject(DemoNoticeService);
  });

  it.each([
    ['restricted', uk.demo.toast.restricted],
    ['quotaExceeded', uk.demo.toast.quotaExceeded],
  ] as const)('shows the "%s" notice as a toast with the text from the language file', async (notice, text) => {
    void service.show(notice);
    await vi.waitFor(() => expect(create).toHaveBeenCalledTimes(1));

    expect(create.mock.calls[0][0]).toMatchObject({ message: text, position: 'top' });
    // Сповіщення зникає саме.
    expect(create.mock.calls[0][0].duration).toBeGreaterThan(0);
    dismissAll();
  });

  it('does not stack the same notice while it is on the screen, and shows it again afterwards', async () => {
    const first = service.show('restricted');
    await service.show('restricted');
    await vi.waitFor(() => expect(create).toHaveBeenCalledTimes(1));

    // Інше сповіщення тим часом показується.
    void service.show('quotaExceeded');
    await vi.waitFor(() => expect(create).toHaveBeenCalledTimes(2));

    dismissAll();
    await first;
    void service.show('restricted');
    await vi.waitFor(() => expect(create).toHaveBeenCalledTimes(3));
    dismissAll();
  });
});
