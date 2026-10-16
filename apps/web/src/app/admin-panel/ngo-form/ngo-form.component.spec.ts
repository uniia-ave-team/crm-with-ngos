import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { AccountsService } from '../../core/accounts/accounts.service';
import { ConnectionService } from '../../core/connection/connection.service';
import { AdminPanelPageModule } from '../admin-panel.module';
import { AdminNgoFormComponent } from './ngo-form.component';

// Тексти сторінки живуть в uk.json — беремо їх звідти, а не дублюємо.
const text = uk.admin;

const UPDATE_NGO = 'Permissions.UpdateNgo';
const CREATE_IMAGES = 'Permissions.CreateLoginPageImages';
const DELETE_IMAGES = 'Permissions.DeleteLoginPageImages';
const VIEW_IMAGES = 'Permissions.ViewLoginPageImages';
const ALL_RIGHTS = [UPDATE_NGO, CREATE_IMAGES, DELETE_IMAGES];

describe('AdminNgoFormComponent', () => {
  let http: HttpTestingController;
  let accounts: AccountsService;
  let server: string;
  let createUrl: ReturnType<typeof vi.fn>;
  let revokeUrl: ReturnType<typeof vi.fn>;
  const originalCreate = (URL as unknown as Record<string, unknown>)['createObjectURL'];
  const originalRevoke = (URL as unknown as Record<string, unknown>)['revokeObjectURL'];

  beforeEach(async () => {
    createUrl = vi.fn(() => 'blob:http://localhost/logo-preview');
    revokeUrl = vi.fn();
    (URL as unknown as Record<string, unknown>)['createObjectURL'] = createUrl;
    (URL as unknown as Record<string, unknown>)['revokeObjectURL'] = revokeUrl;
    localStorage.clear();
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    await TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      imports: [AdminPanelPageModule],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    }).compileComponents();
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    http = TestBed.inject(HttpTestingController);
    const connection = TestBed.inject(ConnectionService);
    http.expectOne(connection.apiHealthUrl());
    server = connection.serverUrl();
    accounts = TestBed.inject(AccountsService);
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

  function render(ngoName: string | null = 'Org', ngoLogoUrl: string | null = null, permissions: string[] = ALL_RIGHTS) {
    accounts.add({ server, accessToken: 'a', refreshToken: 'r', identity: 'u1', ngoName, ngoLogoUrl, permissions });
    const fixture = TestBed.createComponent(AdminNgoFormComponent);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    return { fixture, element };
  }

  const input = (element: HTMLElement) => element.querySelector('#admin-ngo-name') as HTMLInputElement;
  const fileInput = (element: HTMLElement) => element.querySelector('input[type=file]') as HTMLInputElement;
  const type = (element: HTMLElement, value: string) => {
    input(element).value = value;
    input(element).dispatchEvent(new Event('input'));
  };
  const choose = (element: HTMLElement, file: File) => {
    Object.defineProperty(fileInput(element), 'files', { value: [file], configurable: true });
    fileInput(element).dispatchEvent(new Event('change'));
  };

  it('shows the title, the name and the logo block with texts from the language file', () => {
    const { element } = render();
    expect(element.querySelector('.settings-section__title')?.textContent).toContain(text.title);
    expect(element.querySelector('label[for=admin-ngo-name]')?.textContent?.replace('*', '').trim()).toBe(text.name);
    expect(element.querySelector('#admin-logo-label')?.textContent?.trim()).toBe(text.logo);
    expect(element.querySelector('.settings-avatar__choose')?.textContent?.trim()).toBe(text.chooseFile);
    expect(element.querySelector('.settings-avatar__name')?.textContent?.trim()).toBe(text.noFile);
    expect(element.textContent).toContain(text.logoHint);
    expect(element.querySelector('.settings-submit')?.textContent?.trim()).toBe(text.save);
  });

  it('has the gallery of the login page images inside the form, above the only save button', () => {
    const { element } = render();
    const form = element.querySelector('form')!;
    const gallery = form.querySelector('app-admin-images-gallery');
    expect(gallery).not.toBeNull();

    const saves = Array.from(element.querySelectorAll<HTMLButtonElement>('button')).filter(
      (button) => button.textContent?.trim() === text.save,
    );
    expect(saves).toHaveLength(1);
    expect(saves[0].type).toBe('submit');
    expect(gallery!.compareDocumentPosition(saves[0]) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  describe('what is shown depends on the rights', () => {
    const name = (element: HTMLElement) => element.querySelector('#admin-ngo-name');
    const logo = (element: HTMLElement) => element.querySelector('.settings-avatar');
    const gallery = (element: HTMLElement) => element.querySelector('app-admin-images-gallery');
    const save = (element: HTMLElement) => element.querySelector('.settings-submit');

    it('shows the name, the logo and the gallery with all the rights', () => {
      const { element } = render('Org', null, ALL_RIGHTS);
      expect(name(element)).not.toBeNull();
      expect(logo(element)).not.toBeNull();
      expect(gallery(element)).not.toBeNull();
    });

    it('shows the name and the logo only with UpdateNgo', () => {
      const withRight = render('Org', null, [UPDATE_NGO]).element;
      expect(name(withRight)).not.toBeNull();
      expect(logo(withRight)).not.toBeNull();
    });

    it.each([[CREATE_IMAGES], [DELETE_IMAGES], [CREATE_IMAGES, DELETE_IMAGES]])(
      'hides the name and the logo without UpdateNgo (rights: %s)',
      (...rights) => {
        const { element } = render('Org', null, rights);
        expect(name(element)).toBeNull();
        expect(logo(element)).toBeNull();
        expect(element.querySelector('label[for=admin-ngo-name]')).toBeNull();
      },
    );

    it('hides the gallery with UpdateNgo alone', () => {
      const { element } = render('Org', null, [UPDATE_NGO]);
      expect(gallery(element)).toBeNull();
    });

    it.each([[CREATE_IMAGES], [DELETE_IMAGES], [CREATE_IMAGES, DELETE_IMAGES]])(
      'shows the gallery with CreateLoginPageImages or DeleteLoginPageImages (rights: %s)',
      (...rights) => {
        const { element } = render('Org', null, rights);
        expect(gallery(element)).not.toBeNull();
      },
    );

    it('keeps the title and the single save button whatever is shown', () => {
      const { element } = render('Org', null, [CREATE_IMAGES]);
      expect(element.querySelector('.settings-section__title')?.textContent).toContain(text.title);
      expect(save(element)).not.toBeNull();
    });

    it('follows the rights when they change', () => {
      const { fixture, element } = render('Org', null, [UPDATE_NGO]);
      expect(gallery(element)).toBeNull();

      accounts.update(accounts.activeId()!, { permissions: ALL_RIGHTS });
      fixture.detectChanges();
      expect(gallery(element)).not.toBeNull();

      accounts.update(accounts.activeId()!, { permissions: [DELETE_IMAGES] });
      fixture.detectChanges();
      expect(name(element)).toBeNull();
      expect(gallery(element)).not.toBeNull();
    });
  });

  it('marks the name as required', () => {
    const { element } = render();
    expect(input(element).required).toBe(true);
    expect(element.querySelector('label[for=admin-ngo-name] .settings-field__required')?.textContent).toBe('*');
  });

  it('starts with the name and the logo of the organization from the account record', () => {
    const { element } = render('Org', 'https://x/logo.png');
    expect(input(element).value).toBe('Org');
    expect((element.querySelector('.logo-image') as HTMLImageElement).src).toBe('https://x/logo.png');
  });

  it('shows only an empty dashed block, not the letter avatar, when the organization has no logo', () => {
    const { element } = render('Org', null);
    expect(element.querySelector('.logo-image')).toBeNull();
    const empty = element.querySelector('.logo-empty') as HTMLElement;
    expect(empty).not.toBeNull();
    expect(empty.textContent?.trim()).toBe('');
    expect(empty.getAttribute('aria-hidden')).toBe('true');
  });

  it('takes the dashed block away when there is a logo', () => {
    const { element } = render('Org', 'https://x/logo.png');
    expect(element.querySelector('.logo-empty')).toBeNull();
    expect(element.querySelector('.logo-image')).not.toBeNull();
  });

  it('never shows letters instead of the logo: a logo that cannot be loaded gives the dashed block', () => {
    const { fixture, element } = render('Org', 'https://x/broken.png');
    expect(element.querySelector('.logo-image')).not.toBeNull();

    element.querySelector('.logo-image')!.dispatchEvent(new Event('error'));
    fixture.detectChanges();

    expect(element.querySelector('.logo-image')).toBeNull();
    expect(element.querySelector('.logo-empty')).not.toBeNull();
    // У мініатюрі (перший елемент блока) літер нема.
    expect(element.querySelector('.settings-avatar')?.firstElementChild?.textContent?.trim()).toBe('');
  });

  const markers = () =>
    TestBed.inject(TranslateService).setTranslation('uk', {
      common: { required: 'T-required' },
      admin: {
        title: 'T-title',
        name: 'T-name',
        logo: 'T-logo',
        chooseFile: 'T-choose',
        noFile: 'T-nofile',
        logoHint: 'T-hint',
        logoSaveHint: 'T-savehint',
        save: 'T-save',
        saving: 'T-saving',
        result: {
          name: 'T-r-name',
          logo: 'T-r-logo',
          images: 'T-r-images',
          ngoFailed: { invalid: 'T-n-invalid', unauthorized: 'T-n-unauthorized', forbidden: 'T-n-forbidden', unreachable: 'T-n-unreachable', error: 'T-n-error' },
          imagesFailed: { invalid: 'T-i-invalid', unauthorized: 'T-i-unauthorized', forbidden: 'T-i-forbidden', unreachable: 'T-i-unreachable', error: 'T-i-error' },
        },
        images: {
          title: 'T-gallery',
          empty: 'T-empty',
          loading: 'T-loading',
          alt: 'T-alt {{number}}',
          add: 'T-add',
          remove: 'T-remove',
          error: { type: 'T-typeerror', load: 'T-loaderror' },
        },
        error: { nameRequired: 'T-namerequired', logoType: 'T-logotype', logoSize: 'T-logosize' },
      },
    });

  const listUrl = () => `${server}/api/v1/loginpageimages`;

  it('takes every text of the page from the language file, none is hard-coded', async () => {
    markers();
    const { fixture, element } = render('', 'https://x/logo.png', [...ALL_RIGHTS, VIEW_IMAGES]);
    (await vi.waitFor(() => http.expectOne((r) => r.url === listUrl()))).flush({
      items: [{ id: 'i1', url: '/files/1.jpg' }],
      hasNextPage: false,
    });
    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(element.querySelector('.gallery__item')).not.toBeNull();
    });

    // Помилки з'являються після спроби збереження й невдалого вибору файлу.
    (element.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit', { cancelable: true }));
    fixture.detectChanges();
    choose(element, new File(['x'], 'doc.pdf', { type: 'application/pdf' }));
    fixture.detectChanges();

    const shown = [
      element.textContent ?? '',
      ...Array.from(element.querySelectorAll('[aria-label], [title], [alt]')).flatMap((e) => [
        e.getAttribute('aria-label') ?? '',
        e.getAttribute('title') ?? '',
        e.getAttribute('alt') ?? '',
      ]),
    ].join('\n');

    for (const marker of [
      'T-title', 'T-name', 'T-logo', 'T-choose', 'T-nofile', 'T-hint', 'T-savehint', 'T-save', 'T-gallery',
      'T-alt 1', 'T-add', 'T-remove', 'T-namerequired', 'T-logotype',
    ]) {
      expect(shown, marker).toContain(marker);
    }
    // Із мовного файлу, а не з коду: жодного з українських текстів нема.
    for (const original of [uk.admin.title, uk.admin.save, uk.admin.images.title, uk.admin.images.add, uk.admin.chooseFile]) {
      expect(shown).not.toContain(original);
    }
  });

  it('takes the lines of the save result from the language file as well', async () => {
    markers();
    const { fixture, element } = render('Org', '/files/logo.png', [...ALL_RIGHTS, VIEW_IMAGES]);
    (await vi.waitFor(() => http.expectOne((r) => r.url === listUrl() && r.method === 'GET'))).flush({
      items: [{ id: 'i1', url: '/files/1.jpg' }],
      hasNextPage: false,
    });
    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(element.querySelector('.gallery__item')).not.toBeNull();
    });
    type(element, 'Renamed');
    element.querySelector<HTMLButtonElement>('.gallery__remove')!.click();
    fixture.detectChanges();

    (element.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit', { cancelable: true }));
    (await vi.waitFor(() => http.expectOne({ method: 'PUT', url: `${server}/api/v1/ngo` }))).flush(null, {
      status: 204,
      statusText: 'No Content',
    });
    (await vi.waitFor(() => http.expectOne({ method: 'DELETE', url: `${listUrl()}/i1` }))).flush(
      {},
      { status: 403, statusText: 'Forbidden' },
    );
    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(element.querySelector('.settings-result')).not.toBeNull();
    });

    const shown = element.querySelector('.settings-result')!.textContent ?? '';
    for (const marker of ['T-r-name', 'T-r-logo', 'T-i-forbidden']) {
      expect(shown, marker).toContain(marker);
    }
    expect(shown).not.toContain(uk.admin.result.name);
    expect(shown).not.toContain(uk.admin.result.imagesFailed.forbidden);
  });

  it('takes the texts about no images and a failed load from the language file as well', async () => {
    markers();
    const empty = render('Org', null, [VIEW_IMAGES, DELETE_IMAGES]);
    (await vi.waitFor(() => http.expectOne((r) => r.url === listUrl()))).flush({ items: [], hasNextPage: false });
    await vi.waitFor(() => {
      empty.fixture.detectChanges();
      expect(empty.element.textContent).toContain('T-empty');
    });
    expect(empty.element.textContent).not.toContain(uk.admin.images.empty);
  });

  it('replaces the dashed block with the preview when a file is chosen', () => {
    const { fixture, element } = render('Org', null);
    expect(element.querySelector('.logo-empty')).not.toBeNull();

    choose(element, new File([new Uint8Array(5)], 'new.png', { type: 'image/png' }));
    fixture.detectChanges();

    expect(element.querySelector('.logo-empty')).toBeNull();
    expect((element.querySelector('.logo-image') as HTMLImageElement).src).toBe('blob:http://localhost/logo-preview');
  });

  it('shows the chosen file on the thumbnail instead of the saved logo', () => {
    const { fixture, element } = render('Org', 'https://x/logo.png');
    choose(element, new File([new Uint8Array(5)], 'new.png', { type: 'image/png' }));
    fixture.detectChanges();
    expect((element.querySelector('.logo-image') as HTMLImageElement).src).toBe('blob:http://localhost/logo-preview');
  });

  it('follows the saved name until it is edited, and then keeps what was typed', () => {
    const { fixture, element } = render('Org');
    const id = accounts.activeId()!;

    accounts.update(id, { ngoName: 'Org B' });
    fixture.detectChanges();
    expect(input(element).value).toBe('Org B');

    type(element, 'Typed');
    accounts.update(id, { ngoName: 'Org C' });
    fixture.detectChanges();
    expect(input(element).value).toBe('Typed');
  });

  describe('saving (PUT /api/v1/ngo)', () => {
    const ngoUrl = () => `${server}/api/v1/ngo`;
    const submitForm = (fixture: { detectChanges(): void }, element: HTMLElement) => {
      (element.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit', { cancelable: true }));
      fixture.detectChanges();
    };
    const block = (element: HTMLElement) => element.querySelector('.settings-result');
    /** Рядки результату: що змінено й що не вдалося. */
    const lines = (element: HTMLElement) =>
      Array.from(element.querySelectorAll('.settings-result__line')).map((line) => line.textContent?.trim());
    const noNgoRequests = () => http.expectNone(ngoUrl());

    it('keeps the existing logo link of the system in a hidden input of the form', () => {
      const { element } = render('Org', '/files/logo.png');
      const hidden = element.querySelector('form input[type=hidden]') as HTMLInputElement;
      expect(hidden).not.toBeNull();
      expect(hidden.value).toBe('/files/logo.png');
    });

    it('keeps the hidden input empty when the organization has no logo, and follows the saved link', () => {
      const { fixture, element } = render('Org', null);
      const hidden = () => element.querySelector('form input[type=hidden]') as HTMLInputElement;
      expect(hidden().value).toBe('');

      accounts.update(accounts.activeId()!, { ngoLogoUrl: '/files/new.png' });
      fixture.detectChanges();
      expect(hidden().value).toBe('/files/new.png');
    });

    it('puts the name from the form and the received logo link (still null: the upload is not made yet)', async () => {
      const { fixture, element } = render('Org', '/files/logo.png');
      type(element, '  New name ');

      submitForm(fixture, element);
      const request = await vi.waitFor(() => http.expectOne(ngoUrl()));
      expect(request.request.method).toBe('PUT');
      expect(request.request.body).toEqual({ name: 'New name', logoUrl: null });
      request.flush(null, { status: 204, statusText: 'No Content' });
    });

    it('sends nothing and shows no block when neither the name nor the logo was changed', async () => {
      const { fixture, element } = render('Org', null);
      submitForm(fixture, element);
      await Promise.resolve();
      noNgoRequests();
      expect(block(element)).toBeNull();
    });

    it('lists the name and the logo separately: only the logo when only the logo changed', async () => {
      // Логотип в системі є, а отримане посилання поки завжди null — тож логотип змінюється, назва ні.
      const { fixture, element } = render('Org', '/files/logo.png');
      submitForm(fixture, element);
      (await vi.waitFor(() => http.expectOne(ngoUrl()))).flush(null, { status: 204, statusText: 'No Content' });
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(block(element)).not.toBeNull();
      });
      expect(lines(element)).toEqual([text.result.logo]);
    });

    it('lists both lines when the name and the logo changed', async () => {
      const { fixture, element } = render('Org', '/files/logo.png');
      type(element, 'Renamed');
      submitForm(fixture, element);
      (await vi.waitFor(() => http.expectOne(ngoUrl()))).flush(null, { status: 204, statusText: 'No Content' });
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(block(element)).not.toBeNull();
      });
      expect(lines(element)).toEqual([text.result.name, text.result.logo]);
      expect(block(element)?.classList).toContain('settings-result--success');
    });

    it('shows a green block with what was changed after a successful update, and writes the name to the account', async () => {
      const { fixture, element } = render('Org');
      type(element, 'Renamed');
      submitForm(fixture, element);
      (await vi.waitFor(() => http.expectOne(ngoUrl()))).flush(null, { status: 204, statusText: 'No Content' });
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(block(element)).not.toBeNull();
      });

      // Змінилася лише назва — і про логотип нічого не сказано.
      expect(lines(element)).toEqual([text.result.name]);
      expect(block(element)?.classList).toContain('settings-result--success');
      expect(block(element)?.getAttribute('role')).toBe('status');
      expect(accounts.active()?.ngoName).toBe('Renamed');
      // Блок — під заголовком й над формою.
      const title = element.querySelector('.settings-section__title')!;
      const form = element.querySelector('form')!;
      expect(title.compareDocumentPosition(block(element)!) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
      expect(block(element)!.compareDocumentPosition(form) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    });

    it.each([
      [400, 'invalid'],
      [401, 'unauthorized'],
      [403, 'forbidden'],
      [500, 'error'],
    ] as const)('shows a red block for HTTP %i and keeps what was typed', async (status, key) => {
      const { fixture, element } = render('Org');
      type(element, 'Typed');
      submitForm(fixture, element);
      (await vi.waitFor(() => http.expectOne(ngoUrl()))).flush({}, { status, statusText: 'x' });
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(block(element)).not.toBeNull();
      });

      expect(lines(element)).toEqual([text.result.ngoFailed[key]]);
      expect(block(element)?.classList).not.toContain('settings-result--success');
      expect(block(element)?.getAttribute('role')).toBe('alert');
      expect(input(element).value).toBe('Typed');
      expect(accounts.active()?.ngoName).toBe('Org');
    });

    it('says that the server does not answer', async () => {
      const { fixture, element } = render('Org');
      type(element, 'Other name');
      submitForm(fixture, element);
      (await vi.waitFor(() => http.expectOne(ngoUrl()))).error(new ProgressEvent('error'));
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(block(element)).not.toBeNull();
      });
      expect(lines(element)).toEqual([text.result.ngoFailed.unreachable]);
    });

    it('sends nothing and shows the error under the field when the name is empty', async () => {
      const { fixture, element } = render('Org');
      type(element, '   ');
      submitForm(fixture, element);
      await Promise.resolve();
      noNgoRequests();
      expect(element.querySelector('.settings-field__error')?.textContent?.trim()).toBe(text.error.nameRequired);
      expect(block(element)).toBeNull();
    });

    it('sends nothing without the right UpdateNgo', async () => {
      const { fixture, element } = render('Org', null, [CREATE_IMAGES, DELETE_IMAGES]);
      submitForm(fixture, element);
      await Promise.resolve();
      noNgoRequests();
      expect(block(element)).toBeNull();
    });

    it('blocks the button and shows the saving text while the request runs, and does not send twice', async () => {
      const { fixture, element } = render('Org');
      type(element, 'Other name');
      submitForm(fixture, element);
      const request = await vi.waitFor(() => http.expectOne(ngoUrl()));
      fixture.detectChanges();

      const button = element.querySelector('.settings-submit') as HTMLButtonElement;
      expect(button.disabled).toBe(true);
      expect(button.textContent?.trim()).toBe(text.saving);

      submitForm(fixture, element);
      await Promise.resolve();
      noNgoRequests();

      request.flush(null, { status: 204, statusText: 'No Content' });
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(button.disabled).toBe(false);
      });
      expect(button.textContent?.trim()).toBe(text.save);
    });

    describe('together with the gallery', () => {
      const imagesUrl = () => `${server}/api/v1/loginpageimages`;
      const noContent = { status: 204, statusText: 'No Content' };

      /** Відкриває форму з правами; сервер віддає `count` зображень сторінки входу. */
      async function openWithImages(permissions: string[], count = 2) {
        const opened = render('Org', null, permissions);
        const first = await vi.waitFor(() => http.expectOne((r) => r.url === imagesUrl() && r.method === 'GET'));
        first.flush({
          items: Array.from({ length: count }, (_, i) => ({ id: `id-${i + 1}`, url: `/files/${i + 1}.jpg` })),
          hasNextPage: false,
        });
        await vi.waitFor(() => {
          opened.fixture.detectChanges();
          expect(opened.element.querySelectorAll('.gallery__item')).toHaveLength(count);
        });
        return opened;
      }
      const removeFirst = (fixture: { detectChanges(): void }, element: HTMLElement) => {
        element.querySelector<HTMLButtonElement>('.gallery__remove')!.click();
        fixture.detectChanges();
      };
      const waitResult = async (fixture: { detectChanges(): void }, element: HTMLElement) => {
        await vi.waitFor(() => {
          fixture.detectChanges();
          expect(block(element)).not.toBeNull();
        });
      };

      it('sends the name and the deleted images with the one save button, then shows what was changed in a green block', async () => {
        const { fixture, element } = await openWithImages([...ALL_RIGHTS, VIEW_IMAGES]);
        removeFirst(fixture, element);
        type(element, 'Renamed');

        submitForm(fixture, element);
        (await vi.waitFor(() => http.expectOne({ method: 'PUT', url: ngoUrl() }))).flush(null, noContent);
        (await vi.waitFor(() => http.expectOne({ method: 'DELETE', url: `${imagesUrl()}/id-1` }))).flush(null, noContent);
        // Після видалення список із сервера отримується заново.
        (await vi.waitFor(() => http.expectOne((r) => r.method === 'GET' && r.url === imagesUrl()))).flush({
          items: [{ id: 'id-2', url: '/files/2.jpg' }],
          hasNextPage: false,
        });
        await waitResult(fixture, element);

        expect(block(element)?.classList).toContain('settings-result--success');
        expect(lines(element)).toEqual([text.result.name, text.result.images]);
      });

      it('sends the added file with the one save button', async () => {
        const { fixture, element } = await openWithImages([...ALL_RIGHTS, VIEW_IMAGES], 1);
        type(element, 'Renamed');
        const file = new File([new Uint8Array(5)], 'new.png', { type: 'image/png' });
        const galleryInput = element.querySelector('app-admin-images-gallery input[type=file]') as HTMLInputElement;
        Object.defineProperty(galleryInput, 'files', { value: [file], configurable: true });
        galleryInput.dispatchEvent(new Event('change'));
        fixture.detectChanges();

        submitForm(fixture, element);
        (await vi.waitFor(() => http.expectOne({ method: 'PUT', url: ngoUrl() }))).flush(null, noContent);
        const upload = await vi.waitFor(() => http.expectOne({ method: 'POST', url: imagesUrl() }));
        expect(((upload.request.body as FormData).get('file') as File).name).toBe('new.png');
        upload.flush('new-id', { status: 201, statusText: 'Created' });
        (await vi.waitFor(() => http.expectOne((r) => r.method === 'GET' && r.url === imagesUrl()))).flush({
          items: [],
          hasNextPage: false,
        });
        await waitResult(fixture, element);
        expect(block(element)?.classList).toContain('settings-result--success');
        expect(lines(element)).toEqual([text.result.name, text.result.images]);
      });

      it('shows what was saved and what was not, in a red block, when the name is saved but a deletion fails', async () => {
        const { fixture, element } = await openWithImages([...ALL_RIGHTS, VIEW_IMAGES]);
        removeFirst(fixture, element);
        type(element, 'Renamed');

        submitForm(fixture, element);
        (await vi.waitFor(() => http.expectOne({ method: 'PUT', url: ngoUrl() }))).flush(null, noContent);
        (await vi.waitFor(() => http.expectOne({ method: 'DELETE', url: `${imagesUrl()}/id-1` }))).flush(
          {},
          { status: 403, statusText: 'Forbidden' },
        );
        await waitResult(fixture, element);

        expect(block(element)?.classList).not.toContain('settings-result--success');
        expect(block(element)?.getAttribute('role')).toBe('alert');
        // Частково збережено: назва змінена, зображення — ні.
        expect(lines(element)).toEqual([text.result.name, text.result.imagesFailed.forbidden]);
      });

      it('sends only the image requests (no PUT) for a user who cannot update the organization', async () => {
        const { fixture, element } = await openWithImages([VIEW_IMAGES, DELETE_IMAGES]);
        removeFirst(fixture, element);

        submitForm(fixture, element);
        (await vi.waitFor(() => http.expectOne({ method: 'DELETE', url: `${imagesUrl()}/id-1` }))).flush(null, noContent);
        (await vi.waitFor(() => http.expectOne((r) => r.method === 'GET' && r.url === imagesUrl()))).flush({
          items: [{ id: 'id-2', url: '/files/2.jpg' }],
          hasNextPage: false,
        });
        await waitResult(fixture, element);

        noNgoRequests();
        expect(block(element)?.classList).toContain('settings-result--success');
        expect(lines(element)).toEqual([text.result.images]);
      });

      it('sends nothing and shows no block for a gallery-only user who changed nothing', async () => {
        const { fixture, element } = await openWithImages([VIEW_IMAGES, DELETE_IMAGES]);
        submitForm(fixture, element);
        await Promise.resolve();
        http.expectNone((r) => r.method !== 'GET');
        expect(block(element)).toBeNull();
      });

      it('saves the gallery even when the name is empty: the name is only about UpdateNgo and sends no PUT', async () => {
        const { fixture, element } = await openWithImages([...ALL_RIGHTS, VIEW_IMAGES]);
        removeFirst(fixture, element);
        type(element, '  ');

        submitForm(fixture, element);
        (await vi.waitFor(() => http.expectOne({ method: 'DELETE', url: `${imagesUrl()}/id-1` }))).flush(null, noContent);
        (await vi.waitFor(() => http.expectOne((r) => r.method === 'GET' && r.url === imagesUrl()))).flush({
          items: [{ id: 'id-2', url: '/files/2.jpg' }],
          hasNextPage: false,
        });
        await waitResult(fixture, element);

        noNgoRequests();
        // Помилка назви лишається під полем, а блок говорить про те, що збережено.
        expect(element.querySelector('.settings-field__error')?.textContent?.trim()).toBe(text.error.nameRequired);
        expect(block(element)?.classList).toContain('settings-result--success');
        expect(lines(element)).toEqual([text.result.images]);
      });

      it('sends nothing when the name is empty and there are no changes in the gallery', async () => {
        const { fixture, element } = await openWithImages([...ALL_RIGHTS, VIEW_IMAGES]);
        type(element, '  ');
        submitForm(fixture, element);
        await Promise.resolve();
        http.expectNone((r) => r.method !== 'GET');
        expect(element.querySelector('.settings-field__error')?.textContent?.trim()).toBe(text.error.nameRequired);
        expect(block(element)).toBeNull();
      });

      it('lists only the part that failed when the images fail and the name was not changed', async () => {
        const { fixture, element } = await openWithImages([...ALL_RIGHTS, VIEW_IMAGES]);
        removeFirst(fixture, element);

        submitForm(fixture, element);
        // Назву не змінювали — PUT не йде, лише видалення.
        (await vi.waitFor(() => http.expectOne({ method: 'DELETE', url: `${imagesUrl()}/id-1` }))).flush(
          {},
          { status: 500, statusText: 'x' },
        );
        await waitResult(fixture, element);

        noNgoRequests();
        expect(lines(element)).toEqual([text.result.imagesFailed.error]);
        expect(block(element)?.classList).not.toContain('settings-result--success');
      });

      it('saves the name with the right UpdateNgo whatever rights for the images the user has', async () => {
        const { fixture, element } = render('Org', null, [UPDATE_NGO]);
        type(element, 'Only name');
        submitForm(fixture, element);
        const request = await vi.waitFor(() => http.expectOne({ method: 'PUT', url: ngoUrl() }));
        expect(request.request.body).toEqual({ name: 'Only name', logoUrl: null });
        request.flush(null, noContent);
        http.expectNone((r) => r.url === imagesUrl());
      });
    });

    it('hides the previous result as soon as the name is edited or a file is chosen', async () => {
      const { fixture, element } = render('Org');
      type(element, 'Other name');
      submitForm(fixture, element);
      (await vi.waitFor(() => http.expectOne(ngoUrl()))).flush({}, { status: 500, statusText: 'x' });
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(block(element)).not.toBeNull();
      });

      type(element, 'Changed');
      fixture.detectChanges();
      expect(block(element)).toBeNull();
    });
  });

  it('asks for the name under the field, and sends no request', () => {
    const { fixture, element } = render('');
    expect(element.querySelector('.settings-field__error')).toBeNull();

    (element.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit', { cancelable: true }));
    fixture.detectChanges();
    expect(element.querySelector('.settings-field__error')?.textContent?.trim()).toBe(text.error.nameRequired);

    type(element, 'Org');
    fixture.detectChanges();
    expect(element.querySelector('.settings-field__error')).toBeNull();
    http.expectNone(() => true);
  });

  it('does not submit the form natively', () => {
    const { element } = render();
    const event = new Event('submit', { cancelable: true });
    (element.querySelector('form') as HTMLFormElement).dispatchEvent(event);
    expect(event.defaultPrevented).toBe(true);
  });

  describe('logo', () => {
    const png = (size = 10) => new File([new Uint8Array(size)], 'logo.png', { type: 'image/png' });

    it('only offers the image types the server accepts', () => {
      const { element } = render();
      expect(fileInput(element).accept).toBe('image/png,image/jpeg,image/webp,image/svg+xml');
    });

    it('shows the chosen file name and previews it, with the reminder to save', () => {
      const { fixture, element } = render();
      const hint = () => element.querySelectorAll('.settings-avatar__hint')[1];
      expect(hint().classList).toContain('settings-avatar__hint--hidden');

      choose(element, png());
      fixture.detectChanges();

      expect(createUrl).toHaveBeenCalledTimes(1);
      expect(element.querySelector('.settings-avatar__name')?.textContent?.trim()).toBe('logo.png');
      expect(hint().classList).not.toContain('settings-avatar__hint--hidden');
      expect(hint().textContent?.trim()).toBe(text.logoSaveHint);
    });

    it('releases the previous preview when another file is chosen, and the last one on destroy', () => {
      const { fixture, element } = render();
      choose(element, png());
      choose(element, png(20));
      expect(revokeUrl).toHaveBeenCalledTimes(1);

      fixture.destroy();
      expect(revokeUrl).toHaveBeenCalledTimes(2);
    });

    it('rejects a file of another type, with the error under the logo', () => {
      const { fixture, element } = render();
      choose(element, new File(['x'], 'doc.pdf', { type: 'application/pdf' }));
      fixture.detectChanges();

      expect(createUrl).not.toHaveBeenCalled();
      expect(element.querySelector('.settings-avatar__name')?.textContent?.trim()).toBe(text.noFile);
      expect(element.querySelector('.settings-avatar__upload .settings-field__error')?.textContent?.trim()).toBe(text.error.logoType);
    });

    it('rejects a file that is larger than 2 MB', () => {
      const { fixture, element } = render();
      choose(element, png(2 * 1024 * 1024 + 1));
      fixture.detectChanges();

      expect(createUrl).not.toHaveBeenCalled();
      expect(element.querySelector('.settings-avatar__upload .settings-field__error')?.textContent?.trim()).toBe(text.error.logoSize);
    });

    it('clears the error when a good file is chosen afterwards', () => {
      const { fixture, element } = render();
      choose(element, new File(['x'], 'doc.pdf', { type: 'application/pdf' }));
      fixture.detectChanges();
      choose(element, png());
      fixture.detectChanges();
      expect(element.querySelector('.settings-avatar__upload .settings-field__error')).toBeNull();
    });
  });

  it('gives the controls a tabindex (Safari does not tab to the fields without it)', () => {
    const { element } = render();
    for (const control of Array.from(element.querySelectorAll('input:not([type=file]):not([type=hidden]), button'))) {
      expect(control.getAttribute('tabindex')).toBe('0');
    }
  });
});
