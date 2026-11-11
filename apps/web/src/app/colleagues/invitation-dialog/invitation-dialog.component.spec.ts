import { Component, CUSTOM_ELEMENTS_SCHEMA, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { ColleaguesPageModule } from '../colleagues.module';
import { InvitationShare, InvitationStatus } from './invitation-dialog.component';

@Component({
  template: `<app-invitation-dialog [email]="email()" [token]="token()" [expiresAt]="expiresAt()" [ngoName]="ngo()" [status]="status()" (copyLink)="copied.push($event)" (shareLink)="shared.push($event)" (dismiss)="dismissed = dismissed + 1" />`,
  imports: [ColleaguesPageModule],
  schemas: [CUSTOM_ELEMENTS_SCHEMA],
})
class HostComponent {
  readonly email = signal('a@b.org');
  readonly token = signal('eyJ.header.payload.signature-of-a-long-token');
  readonly expiresAt = signal('2026-11-20T10:00:00Z');
  readonly ngo = signal('Організація');
  readonly status = signal<InvitationStatus>('idle');
  readonly copied: string[] = [];
  readonly shared: InvitationShare[] = [];
  dismissed = 0;
}

describe('InvitationDialogComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })] });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
  });

  const setShare = (value: unknown) => Object.defineProperty(navigator, 'share', { value, configurable: true, writable: true });
  afterEach(() => Reflect.deleteProperty(navigator, 'share'));

  /** `withShare`: чи вміє «браузер» поширювати (кнопка «Поділитися» є лише тоді). */
  function render(withShare = true) {
    setShare(withShare ? () => Promise.resolve() : undefined);
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const q = <T extends HTMLElement = HTMLElement>(css: string) => element.querySelector(css) as T;
    return { fixture, host: fixture.componentInstance, element, q };
  }
  const origin = () => window.location.origin;

  it('has the title and the call to share the link with a colleague, from the language file', () => {
    const { q } = render();
    expect(q('.invitation__title').textContent?.trim()).toBe(uk.colleagues.invitation.title);
    expect(q('.invitation__hint').textContent?.trim()).toBe(uk.colleagues.invitation.hint);
    expect(uk.colleagues.invitation.hint).toContain('Поділіться посиланням з колегою');
    expect(uk.colleagues.invitation.hint).not.toContain('токен');
  });

  describe('the link', () => {
    it('is the current domain, /join and the token as the parameter', () => {
      const { q } = render();
      expect(q('.invitation__link').textContent?.trim()).toBe(`${origin()}/join?token=eyJ.header.payload.signature-of-a-long-token`);
    });

    it('has the token encoded for the address', () => {
      const { fixture, host, q } = render();
      host.token.set('a b+c/d=e&f?g');
      fixture.detectChanges();
      expect(q('.invitation__link').textContent?.trim()).toBe(`${origin()}/join?token=${encodeURIComponent('a b+c/d=e&f?g')}`);
    });

    it('follows the token when it changes', () => {
      const { fixture, host, q } = render();
      host.token.set('another');
      fixture.detectChanges();
      expect(q('.invitation__link').textContent?.trim()).toBe(`${origin()}/join?token=another`);
    });

    it('is one line that scrolls sideways (not wrapped), is read by screen readers as a read-only text box, and can be focused by the keyboard', () => {
      const { q } = render();
      const link = q('.invitation__link');
      const style = getComputedStyle(link);
      expect(style.whiteSpace).toBe('nowrap');
      expect(style.overflowX).toBe('auto');
      expect(link.getAttribute('role')).toBe('textbox');
      expect(link.getAttribute('aria-readonly')).toBe('true');
      expect(link.getAttribute('tabindex')).toBe('0');
      expect(link.getAttribute('aria-label')).toBe(uk.colleagues.invitation.link);
    });

    it('is selected whole by one click', () => {
      const { q } = render();
      const style = getComputedStyle(q('.invitation__link')) as CSSStyleDeclaration & { webkitUserSelect?: string };
      expect([style.userSelect, style.webkitUserSelect]).toContain('all');
    });
  });

  describe('the buttons at the end of the link: copy first, then share', () => {
    it('are two, in the same row after the link: the copy button first, the share button the last', () => {
      const { q } = render();
      const row = q('.invitation__link-row');
      expect(Array.from(row.children)).toEqual([q('.invitation__link'), q('.invitation__copy'), q('.invitation__share')]);
    });

    it('have the icons copy-outline and share-social, a label, a title and a tabindex', () => {
      const { q } = render();
      const copy = q<HTMLButtonElement>('.invitation__copy');
      const share = q<HTMLButtonElement>('.invitation__share');
      expect((copy.querySelector('ion-icon') as unknown as { name: string }).name).toBe('copy-outline');
      expect((share.querySelector('ion-icon') as unknown as { name: string }).name).toBe('share-social');
      for (const [button, label] of [[copy, uk.colleagues.invitation.copy], [share, uk.colleagues.invitation.share]] as const) {
        expect(button.getAttribute('aria-label')).toBe(label);
        expect(button.getAttribute('title')).toBe(label);
        expect(button.getAttribute('tabindex')).toBe('0');
        expect(button.type).toBe('button');
      }
    });

    it('gives the whole link (not only the token) when the copy button is pressed', () => {
      const { host, q } = render();
      q<HTMLButtonElement>('.invitation__copy').click();
      expect(host.copied).toEqual([`${origin()}/join?token=eyJ.header.payload.signature-of-a-long-token`]);
      expect(host.shared).toEqual([]);
    });

    it('has no share button when the browser cannot share, and the copy button is then the last one', () => {
      const { q } = render(false);
      expect(q('.invitation__share')).toBeNull();
      expect(q('.invitation__link-row').lastElementChild).toBe(q('.invitation__copy'));
    });
  });

  describe('what is shared by the share button (the signature from the language file and the link)', () => {
    it('is the title, the signature with the organization and the date, and the link', () => {
      const { host, q } = render();
      q<HTMLButtonElement>('.invitation__share').click();
      expect(host.copied).toEqual([]);
      expect(host.shared).toHaveLength(1);
      const [shared] = host.shared;
      expect(shared.url).toBe(`${origin()}/join?token=eyJ.header.payload.signature-of-a-long-token`);
      expect(shared.title).toBe(uk.colleagues.invitation.shareTitle);
      expect(shared.text.startsWith('Вас запрошено приєднатися до системи Явір, яку використовує Організація. Ви можете зареєструватися за посиланням нижче до ')).toBe(true);
      expect(shared.text.endsWith('.')).toBe(true);
      expect(shared.text).toContain('2026');
      expect(shared.text).not.toContain('{{');
    });

    it('has the date of the invitation in the same words as it is shown in the dialog', () => {
      const { host, q } = render();
      const shown = q('.invitation__details dd:last-of-type').textContent?.trim() ?? '';
      q<HTMLButtonElement>('.invitation__share').click();
      expect(shown).not.toBe('');
      expect(host.shared[0].text).toContain(`до ${shown}.`);
    });

    it('takes every sentence from the language file (the introduction with the name of the organization, then the end with the date)', () => {
      expect(uk.colleagues.invitation.shareIntro).toBe('Вас запрошено приєднатися до системи Явір, яку використовує {{ngo}}.');
      expect(uk.colleagues.invitation.shareOutro).toBe('Ви можете зареєструватися за посиланням нижче до {{date}}.');
    });

    it('follows the name of the organization', () => {
      const { fixture, host, q } = render();
      host.ngo.set('Інша організація');
      fixture.detectChanges();
      q<HTMLButtonElement>('.invitation__share').click();
      expect(host.shared[0].text).toContain('яку використовує Інша організація.');
    });

    it('leaves out the organization when it is not known, and the date when the server gave none', () => {
      const { fixture, host, q } = render();
      host.ngo.set('  ');
      host.expiresAt.set('');
      fixture.detectChanges();
      q<HTMLButtonElement>('.invitation__share').click();
      expect(host.shared[0].text).toBe(`${uk.colleagues.invitation.shareIntroNoNgo} ${uk.colleagues.invitation.shareOutroNoDate}`);
    });

    it('shares the link encoded for the address', () => {
      const { fixture, host, q } = render();
      host.token.set('a b+c');
      fixture.detectChanges();
      q<HTMLButtonElement>('.invitation__share').click();
      expect(host.shared[0].url).toBe(`${origin()}/join?token=${encodeURIComponent('a b+c')}`);
    });
  });

  describe('the look of the row of the link and of the copy button', () => {
    // Значення з var() у jsdom не рахуються, тож правила стилів компонента читаємо як текст.
    const css = () => Array.from(document.querySelectorAll('style')).map((style) => style.textContent ?? '').join('\n');
    // Тіла всіх правил, у списку селекторів яких є `selector` (у тому числі правила `:last-child` тощо).
    const rule = (selector: string) =>
      css()
        .split('}')
        .flatMap((chunk) => {
          const [selectors, body] = chunk.split('{');
          return body !== undefined && selectors.includes(selector) ? [body] : [];
        })
        .join(';');

    it('has the row without the padding except 12px on the left', () => {
      render();
      expect(rule('.invitation__link-row')).toMatch(/padding:\s*0(px)? 0(px)? 0(px)? 12px/);
    });

    it('has the buttons on the background of the active items with a line on the left, not rounded, and the last of them rounded on the right', () => {
      render();
      const buttons = rule('.invitation__copy');
      expect(buttons).toContain('background: var(--nav-active-bg)');
      expect(buttons).toContain('border-left: 1px solid var(--nav-border)');
      expect(buttons).toContain('border-radius: 0;');
      expect(buttons).toContain('border-radius: 0 10px 10px 0');
    });

    it('has the share button styled like the copy button', () => {
      render();
      expect(rule('.invitation__share')).toContain('background: var(--nav-active-bg)');
    });
  });

  describe('the cross', () => {
    it('stands in the row of the title, after it, with the icon close-outline, a label and a tabindex', () => {
      const { q } = render();
      const header = q('.invitation__header');
      const cross = q<HTMLButtonElement>('.invitation__close');
      expect(header.firstElementChild).toBe(q('.invitation__title'));
      expect(header.lastElementChild).toBe(cross);
      expect((cross.querySelector('ion-icon') as unknown as { name: string }).name).toBe('close-outline');
      expect(cross.getAttribute('aria-label')).toBe(uk.colleagues.invitation.close);
      expect(cross.getAttribute('tabindex')).toBe('0');
    });

    it('tells that the dialog should be closed, and it is the only way to say so (no button "Close" any more)', () => {
      const { host, element, q } = render();
      q<HTMLButtonElement>('.invitation__close').click();
      expect(host.dismissed).toBe(1);
      expect(Array.from(element.querySelectorAll('button')).map((b) => b.textContent?.trim()).filter(Boolean)).toEqual([]);
    });
  });

  describe('the order of the blocks', () => {
    it('is the link first, then the email and the date', () => {
      const { q } = render();
      const link = q('.invitation__link-row');
      const details = q('.invitation__details');
      expect(link.compareDocumentPosition(details) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    });

    it('has the title, then the hint, then the link, then the details', () => {
      const { element } = render();
      const order = ['.invitation__header', '.invitation__hint', '.invitation__link-row', '.invitation__status', '.invitation__details'].map((css) =>
        Array.from(element.querySelector('.invitation')!.children).indexOf(element.querySelector(css) as Element),
      );
      expect(order).toEqual([0, 1, 2, 3, 4]);
    });
  });

  it('shows the email the invitation was made for', () => {
    const { fixture, host, q } = render();
    expect(q('.invitation__details').textContent).toContain(uk.colleagues.invitation.forEmail);
    expect(q('.invitation__details').textContent).toContain('a@b.org');
    host.email.set('other@b.org');
    fixture.detectChanges();
    expect(q('.invitation__details').textContent).toContain('other@b.org');
  });

  describe('the date until which the invitation is valid', () => {
    it('is shown in words with the year, for the language of the interface', () => {
      const { q } = render();
      const text = q('.invitation__details').textContent ?? '';
      expect(text).toContain(uk.colleagues.invitation.expires);
      expect(text).toContain('2026');
    });

    it.each(['', 'not a date'])('is not shown when the server gave "%s"', (value) => {
      const { fixture, host, q } = render();
      host.expiresAt.set(value);
      fixture.detectChanges();
      expect(q('.invitation__details').textContent).not.toContain(uk.colleagues.invitation.expires);
    });
  });

  describe('the status of the copying', () => {
    it('is empty before the copying, and read as a status', () => {
      const { q } = render();
      expect(q('.invitation__status').textContent?.trim()).toBe('');
      expect(q('.invitation__status').getAttribute('role')).toBe('status');
    });

    it('says that the link is copied', () => {
      const { fixture, host, q } = render();
      host.status.set('copied');
      fixture.detectChanges();
      expect(q('.invitation__status').textContent?.trim()).toBe(uk.colleagues.invitation.copied);
      expect(q('.invitation__status').classList).not.toContain('invitation__status--error');
    });

    it('says that the sharing failed, in the color of the errors', () => {
      const { fixture, host, q } = render();
      host.status.set('shareFailed');
      fixture.detectChanges();
      expect(q('.invitation__status').textContent?.trim()).toBe(uk.colleagues.invitation.shareFailed);
      expect(q('.invitation__status').classList).toContain('invitation__status--error');
    });

    it('says that the copying failed, in the color of the errors', () => {
      const { fixture, host, q } = render();
      host.status.set('failed');
      fixture.detectChanges();
      expect(q('.invitation__status').textContent?.trim()).toBe(uk.colleagues.invitation.copyFailed);
      expect(q('.invitation__status').classList).toContain('invitation__status--error');
    });
  });
});
