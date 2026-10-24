import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { ConnectionService } from '../../core/connection/connection.service';
import { ProtectedSrcModule } from './protected-src.module';

@Component({
  template: `<img [appProtectedSrc]="url()" alt="" (error)="errors = errors + 1" />`,
  imports: [ProtectedSrcModule],
})
class HostComponent {
  readonly url = signal<string | null>(null);
  errors = 0;
}

describe('ProtectedSrcDirective', () => {
  let http: HttpTestingController;
  let server: string;
  let counter: number;
  const originalCreate = (URL as unknown as Record<string, unknown>)['createObjectURL'];
  const originalRevoke = (URL as unknown as Record<string, unknown>)['revokeObjectURL'];

  beforeEach(() => {
    counter = 0;
    (URL as unknown as Record<string, unknown>)['createObjectURL'] = vi.fn(() => `blob:http://localhost/${++counter}`);
    (URL as unknown as Record<string, unknown>)['revokeObjectURL'] = vi.fn();
    localStorage.clear();
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const connection = TestBed.inject(ConnectionService);
    http.expectOne(connection.apiHealthUrl());
    server = connection.serverUrl();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
    for (const [name, original] of [
      ['createObjectURL', originalCreate],
      ['revokeObjectURL', originalRevoke],
    ] as const) {
      if (original === undefined) {
        delete (URL as unknown as Record<string, unknown>)[name];
      } else {
        (URL as unknown as Record<string, unknown>)[name] = original;
      }
    }
  });

  const logo = (version = 'v1') => `${server}/api/v1/ngo/logo?v=${version}`;

  function render(url: string | null) {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.componentInstance.url.set(url);
    fixture.detectChanges();
    const image = fixture.nativeElement.querySelector('img') as HTMLImageElement;
    return { fixture, image, host: fixture.componentInstance };
  }

  it('has no src without an address', () => {
    const { image } = render(null);
    expect(image.hasAttribute('src')).toBe(false);
  });

  it('puts a link that needs no authorization into src at once', () => {
    const { image } = render('blob:http://localhost/preview');
    expect(image.getAttribute('src')).toBe('blob:http://localhost/preview');
    http.expectNone(() => true);
  });

  it('loads a protected file with the token (not with a bare <img>) and puts the blob link into src', async () => {
    const { fixture, image } = render(logo());
    // Поки файл завантажується, src порожній (голий запит <img> дав би 401).
    expect(image.hasAttribute('src')).toBe(false);

    http.expectOne(logo()).flush(new Blob(['x'], { type: 'image/png' }));
    await vi.waitFor(() => expect(image.getAttribute('src')).toBe('blob:http://localhost/1'));
    fixture.detectChanges();
    expect(image.getAttribute('src')).not.toContain('/api/');
  });

  it('gives the error event when the file cannot be loaded, so the (error) handlers work', async () => {
    const { host, image } = render(logo());
    http.expectOne(logo()).flush(new Blob([]), { status: 401, statusText: 'Unauthorized' });
    await vi.waitFor(() => expect(host.errors).toBe(1));
    expect(image.hasAttribute('src')).toBe(false);
  });

  it('follows the address: a new one is loaded, the old answer that comes late is ignored', async () => {
    const { fixture, image, host } = render(logo('v1'));
    const first = http.expectOne(logo('v1'));

    host.url.set(logo('v2'));
    fixture.detectChanges();
    const second = http.expectOne(logo('v2'));
    second.flush(new Blob(['y']));
    await vi.waitFor(() => expect(image.getAttribute('src')).toBe('blob:http://localhost/1'));

    // Відповідь на першу адресу приходить запізно — src не міняється.
    first.flush(new Blob(['x']));
    await Promise.resolve();
    await Promise.resolve();
    expect(image.getAttribute('src')).toBe('blob:http://localhost/1');
  });

  it('removes src when the address is taken away', async () => {
    const { fixture, image, host } = render('blob:http://localhost/preview');
    expect(image.getAttribute('src')).toBe('blob:http://localhost/preview');
    host.url.set(null);
    fixture.detectChanges();
    expect(image.hasAttribute('src')).toBe(false);
  });
});
