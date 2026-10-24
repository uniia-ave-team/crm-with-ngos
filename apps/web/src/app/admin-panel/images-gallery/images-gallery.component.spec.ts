import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { AccountsService } from '../../core/accounts/accounts.service';
import { ConnectionService } from '../../core/connection/connection.service';
import { AdminPanelPageModule } from '../admin-panel.module';
import { AdminImagesGalleryComponent } from './images-gallery.component';

// Тексти живуть в uk.json — беремо їх звідти, а не дублюємо.
const text = uk.admin.images;

const VIEW_IMAGES = 'Permissions.ViewLoginPageImages';
const CREATE_IMAGES = 'Permissions.CreateLoginPageImages';
const DELETE_IMAGES = 'Permissions.DeleteLoginPageImages';

describe('AdminImagesGalleryComponent', () => {
  let http: HttpTestingController;
  let server: string;
  let createUrl: ReturnType<typeof vi.fn>;
  let revokeUrl: ReturnType<typeof vi.fn>;
  const originalCreate = (URL as unknown as Record<string, unknown>)['createObjectURL'];
  const originalRevoke = (URL as unknown as Record<string, unknown>)['revokeObjectURL'];

  beforeEach(async () => {
    let counter = 0;
    createUrl = vi.fn(() => `blob:http://localhost/image-${++counter}`);
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

  const listUrl = () => `${server}/api/v1/loginpageimages`;
  const serverImages = (count: number) =>
    Array.from({ length: count }, (_, index) => ({ id: `id-${index + 1}`, url: `/files/login/${index + 1}.jpg` }));
  const page = (items: unknown[], hasNextPage = false) => ({ items, totalCount: items.length, pageNumber: 1, pageSize: 20, hasNextPage });
  const pageRequest = (number: number) => http.expectOne((r) => r.url === listUrl() && r.params.get('PageNumber') === String(number));
  const state = (fixture: { componentInstance: unknown }) =>
    fixture.componentInstance as unknown as { loading(): boolean; firstLoaded(): boolean; loadFailed(): boolean };
  /** Три списки галереї (див. опис компонента). */
  const lists = (fixture: { componentInstance: unknown }) => {
    const c = fixture.componentInstance as unknown as {
      systemImages(): { id: string; url: string }[];
      deletedImages(): { id: string; url: string }[];
      addedImages(): { key: number; file: File; url: string }[];
    };
    return { system: c.systemImages(), deleted: c.deletedImages(), added: c.addedImages() };
  };
  const itemUrl = (index: number) => `${server}/files/login/${index}.jpg`;

  /**
   * Відкриває галерею з правами `permissions`. Якщо є право перегляду — сервер
   * віддає `count` зображень (або `failure` — помилку).
   */
  async function render(
    permissions: string[] = [VIEW_IMAGES, CREATE_IMAGES, DELETE_IMAGES],
    options: { count?: number; failure?: number } = {},
  ) {
    TestBed.inject(AccountsService).add({ server, accessToken: 'a', refreshToken: 'r', identity: 'u1', permissions });
    const fixture = TestBed.createComponent(AdminImagesGalleryComponent);
    fixture.detectChanges();
    if (permissions.includes(VIEW_IMAGES)) {
      const request = await vi.waitFor(() => pageRequest(1));
      if (options.failure) {
        request.flush({}, { status: options.failure, statusText: 'x' });
      } else {
        request.flush(page(serverImages(options.count ?? 10)));
      }
      // Чекаємо, поки компонент опрацює відповідь.
      await vi.waitFor(() => expect(state(fixture).loading()).toBe(false));
    }
    fixture.detectChanges();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  }

  const items = (element: HTMLElement) => Array.from(element.querySelectorAll('.gallery__item'));
  const fileInput = (element: HTMLElement) => element.querySelector('input[type=file]') as HTMLInputElement;
  const choose = (element: HTMLElement, file: File) => {
    Object.defineProperty(fileInput(element), 'files', { value: [file], configurable: true });
    fileInput(element).dispatchEvent(new Event('change'));
  };

  describe('getting the images from the server', () => {
    it('asks for the images once, with the paging parameters, and shows what the server returned', async () => {
      const { element } = await render([VIEW_IMAGES, CREATE_IMAGES, DELETE_IMAGES], { count: 3 });
      expect(items(element)).toHaveLength(3);
      const sources = Array.from(element.querySelectorAll('.gallery__image')).map((i) => i.getAttribute('src'));
      // Відносні посилання рахуються від адреси сервера.
      expect(sources).toEqual([`${server}/files/login/1.jpg`, `${server}/files/login/2.jpg`, `${server}/files/login/3.jpg`]);
      http.expectNone((r) => r.url === listUrl());
    });

    it('asks for the first page of 20 only', async () => {
      TestBed.inject(AccountsService).add({ server, accessToken: 'a', refreshToken: 'r', identity: 'u1', permissions: [VIEW_IMAGES] });
      const fixture = TestBed.createComponent(AdminImagesGalleryComponent);
      fixture.detectChanges();

      const first = await vi.waitFor(() => pageRequest(1));
      expect(first.request.method).toBe('GET');
      expect(first.request.params.get('PageSize')).toBe('20');
      first.flush(page(serverImages(20), true));
      await vi.waitFor(() => expect(state(fixture).loading()).toBe(false));

      // Далі — лише коли користувач докрутить до кінця: самі сторінки не вантажимо.
      http.expectNone((r) => r.url === listUrl());
    });

    it('does not ask the server without the right ViewLoginPageImages', async () => {
      const { element } = await render([CREATE_IMAGES, DELETE_IMAGES]);
      http.expectNone((r) => r.url === listUrl());
      expect(items(element)).toHaveLength(0);
      // Додавати можна — плитка є.
      expect(element.querySelector('.gallery__add')).not.toBeNull();
    });

    it('asks as soon as the right arrives, when it came later than the page', async () => {
      const accounts = TestBed.inject(AccountsService);
      const { fixture } = await render([CREATE_IMAGES]);
      http.expectNone((r) => r.url === listUrl());

      accounts.update(accounts.activeId()!, { permissions: [VIEW_IMAGES, CREATE_IMAGES] });
      fixture.detectChanges();
      (await vi.waitFor(() => http.expectOne((r) => r.url === listUrl()))).flush(page(serverImages(2)));
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(fixture.nativeElement.querySelectorAll('.gallery__item')).toHaveLength(2);
      });
    });

    it('shows an error (not the "no images" text) when the images could not be received', async () => {
      const { element } = await render([VIEW_IMAGES, DELETE_IMAGES], { failure: 500 });
      expect(element.querySelector('.settings-field__error')?.textContent?.trim()).toBe(text.error.load);
      expect(element.querySelector('.gallery__empty')).toBeNull();
      expect(items(element)).toHaveLength(0);
    });
  });

  describe('loading more images while scrolling', () => {
    /** У jsdom розкладки нема: розміри рядка задаємо самі й повідомляємо про прокрутку. */
    function scrollRow(element: HTMLElement, left: number, scrollWidth: number, clientWidth = 800) {
      const track = element.querySelector('.gallery__track') as HTMLElement;
      Object.defineProperty(track, 'scrollWidth', { value: scrollWidth, configurable: true });
      Object.defineProperty(track, 'clientWidth', { value: clientWidth, configurable: true });
      track.scrollLeft = left;
      track.dispatchEvent(new Event('scroll'));
    }
    const spinner = (element: HTMLElement) => element.querySelector('.gallery__loading');

    /** Першу сторінку (20) віддає сервер із ознакою наступної. */
    async function openWithNextPage(permissions = [VIEW_IMAGES, CREATE_IMAGES, DELETE_IMAGES]) {
      TestBed.inject(AccountsService).add({ server, accessToken: 'a', refreshToken: 'r', identity: 'u1', permissions });
      const fixture = TestBed.createComponent(AdminImagesGalleryComponent);
      fixture.detectChanges();
      (await vi.waitFor(() => pageRequest(1))).flush(page(serverImages(20), true));
      await vi.waitFor(() => expect(state(fixture).loading()).toBe(false));
      fixture.detectChanges();
      return { fixture, element: fixture.nativeElement as HTMLElement };
    }

    it('does not ask for the next page while the end of the row is far away', async () => {
      const { fixture, element } = await openWithNextPage();
      scrollRow(element, 0, 3500);
      fixture.detectChanges();
      scrollRow(element, 1000, 3500);
      http.expectNone((r) => r.url === listUrl());
      expect(spinner(element)).toBeNull();
    });

    it('asks for the next page (and only it) when the scroll comes near the end, with a preloader meanwhile', async () => {
      const { fixture, element } = await openWithNextPage();
      scrollRow(element, 2500, 3500);
      fixture.detectChanges();

      const request = await vi.waitFor(() => pageRequest(2));
      expect(request.request.params.get('PageSize')).toBe('20');
      expect(spinner(element)).not.toBeNull();
      expect(spinner(element)?.getAttribute('aria-label')).toBe(text.loading);

      // Поки сторінка йде, нові прокрутки другого запиту не роблять.
      scrollRow(element, 2600, 3500);
      http.expectNone((r) => r.url === listUrl() && r.params.get('PageNumber') === '2');

      request.flush(page([{ id: 'x1', url: '/files/login/21.jpg' }], false));
      await vi.waitFor(() => expect(state(fixture).loading()).toBe(false));
      fixture.detectChanges();
      expect(spinner(element)).toBeNull();
      expect(items(element)).toHaveLength(21);
    });

    it('puts the received images after the existing ones and before the files chosen on the page', async () => {
      const { fixture, element } = await openWithNextPage();
      choose(element, new File([new Uint8Array(5)], 'mine.png', { type: 'image/png' }));
      fixture.detectChanges();

      scrollRow(element, 2500, 3500);
      (await vi.waitFor(() => pageRequest(2))).flush(page([{ id: 'x1', url: '/files/login/21.jpg' }], false));
      await vi.waitFor(() => expect(state(fixture).loading()).toBe(false));
      fixture.detectChanges();

      const sources = Array.from(element.querySelectorAll('.gallery__image')).map((i) => i.getAttribute('src'));
      expect(sources).toHaveLength(22);
      expect(sources[20]).toBe(`${server}/files/login/21.jpg`);
      expect(sources[21]).toBe('blob:http://localhost/image-1');
    });

    it('stops asking when the server says there is no next page', async () => {
      const { fixture, element } = await openWithNextPage();
      scrollRow(element, 2500, 3500);
      (await vi.waitFor(() => pageRequest(2))).flush(page(serverImages(2), false));
      await vi.waitFor(() => expect(state(fixture).loading()).toBe(false));
      fixture.detectChanges();

      scrollRow(element, 3000, 4000);
      http.expectNone((r) => r.url === listUrl());
    });

    it('takes the following pages by itself while the row does not fill the window', async () => {
      // Ширина вікна 800, рядок 500: прокручувати нічого, тож наступну сторінку треба добрати самому.
      const proto = HTMLElement.prototype;
      const widths = Object.getOwnPropertyDescriptor(proto, 'clientWidth');
      const full = Object.getOwnPropertyDescriptor(proto, 'scrollWidth');
      Object.defineProperty(proto, 'clientWidth', { value: 800, configurable: true });
      Object.defineProperty(proto, 'scrollWidth', { value: 500, configurable: true });
      try {
        TestBed.inject(AccountsService).add({ server, accessToken: 'a', refreshToken: 'r', identity: 'u1', permissions: [VIEW_IMAGES] });
        const fixture = TestBed.createComponent(AdminImagesGalleryComponent);
        fixture.detectChanges();
        (await vi.waitFor(() => pageRequest(1))).flush(page(serverImages(3), true));
        fixture.detectChanges();
        // Без жодної прокрутки просить другу.
        (await vi.waitFor(() => pageRequest(2))).flush(page(serverImages(2), false));
        await vi.waitFor(() => expect(state(fixture).loading()).toBe(false));
      } finally {
        if (widths) Object.defineProperty(proto, 'clientWidth', widths);
        else delete (proto as unknown as Record<string, unknown>)['clientWidth'];
        if (full) Object.defineProperty(proto, 'scrollWidth', full);
        else delete (proto as unknown as Record<string, unknown>)['scrollWidth'];
      }
    });

    it('shows an error and does not retry by itself when a page fails, but retries when the user scrolls to the end again', async () => {
      const { fixture, element } = await openWithNextPage();
      scrollRow(element, 2500, 3500);
      (await vi.waitFor(() => pageRequest(2))).flush({}, { status: 500, statusText: 'x' });
      await vi.waitFor(() => expect(state(fixture).loadFailed()).toBe(true));
      fixture.detectChanges();

      expect(element.querySelector('.settings-field__error')?.textContent?.trim()).toBe(text.error.load);
      expect(spinner(element)).toBeNull();
      // Помилка не знищила те, що вже є.
      expect(items(element)).toHaveLength(20);
      http.expectNone((r) => r.url === listUrl());

      scrollRow(element, 2600, 3500);
      (await vi.waitFor(() => pageRequest(2))).flush(page([{ id: 'id-21', url: '/files/login/21.jpg' }], false));
      await vi.waitFor(() => expect(state(fixture).loading()).toBe(false));
      fixture.detectChanges();
      expect(element.querySelector('.settings-field__error')).toBeNull();
      expect(items(element)).toHaveLength(21);
    });

    it('does not ask for pages of a user who has no right to view them', async () => {
      const { fixture, element } = await render([CREATE_IMAGES]);
      scrollRow(element, 2500, 3500);
      fixture.detectChanges();
      http.expectNone((r) => r.url === listUrl());
      expect(spinner(element)).toBeNull();
    });
  });

  describe('the add tile and the small plus button', () => {
    const tile = (element: HTMLElement) => element.querySelector('.gallery__add') as HTMLElement;
    const quick = (element: HTMLElement) => element.querySelector('.gallery__quick-add');
    /** Рядок завширшки `clientWidth`; плитка додавання стоїть у кінці, на `tileLeft`. */
    function layout(element: HTMLElement, scrollLeft: number, tileLeft = 3300, clientWidth = 800) {
      const track = element.querySelector('.gallery__track') as HTMLElement;
      Object.defineProperty(track, 'scrollWidth', { value: tileLeft + 160, configurable: true });
      Object.defineProperty(track, 'clientWidth', { value: clientWidth, configurable: true });
      Object.defineProperty(tile(element), 'offsetLeft', { value: tileLeft, configurable: true });
      Object.defineProperty(tile(element), 'offsetWidth', { value: 160, configurable: true });
      track.scrollLeft = scrollLeft;
      track.dispatchEvent(new Event('scroll'));
    }

    it('puts the add tile at the end of the row', async () => {
      const { element } = await render();
      const track = element.querySelector('.gallery__track') as HTMLElement;
      const last = Array.from(track.children).filter((c) => c.tagName !== 'INPUT').pop()!;
      expect(last.classList.contains('gallery__add')).toBe(true);
    });

    it('has no small plus button when the row has no layout to judge by', async () => {
      const { element } = await render();
      expect(quick(element)).toBeNull();
    });

    it('shows the small plus button at the right edge while the add tile at the end is out of view', async () => {
      const { fixture, element } = await render();
      layout(element, 0);
      fixture.detectChanges();
      const small = quick(element) as HTMLButtonElement;
      expect(small).not.toBeNull();
      expect(small.getAttribute('aria-label')).toBe(text.add);
      expect(small.title).toBe(text.add);
      expect(small.querySelector('ion-icon')?.getAttribute('name')).toBe('add-outline');

      layout(element, 1500);
      fixture.detectChanges();
      expect(quick(element)).not.toBeNull();
    });

    it('takes the small button away when the add tile comes into view', async () => {
      const { fixture, element } = await render();
      layout(element, 0);
      fixture.detectChanges();
      expect(quick(element)).not.toBeNull();

      // Рядок докручено до кінця: плитка (3300…3460) у вікні 2700…3500.
      layout(element, 2700);
      fixture.detectChanges();
      expect(quick(element)).toBeNull();
    });

    it('has no small button when the whole row fits and the add tile is in view', async () => {
      const { fixture, element } = await render();
      layout(element, 0, 600, 800);
      fixture.detectChanges();
      expect(quick(element)).toBeNull();
    });

    it('opens the same file dialog as the add tile', async () => {
      const { fixture, element } = await render();
      layout(element, 0);
      fixture.detectChanges();
      const click = vi.spyOn(fileInput(element), 'click');
      (quick(element) as HTMLButtonElement).click();
      expect(click).toHaveBeenCalledTimes(1);
    });

    it('never shows the small button without the right to add', async () => {
      const { fixture, element } = await render([VIEW_IMAGES, DELETE_IMAGES]);
      const track = element.querySelector('.gallery__track') as HTMLElement;
      Object.defineProperty(track, 'scrollWidth', { value: 4000, configurable: true });
      Object.defineProperty(track, 'clientWidth', { value: 800, configurable: true });
      track.scrollLeft = 900;
      track.dispatchEvent(new Event('scroll'));
      fixture.detectChanges();
      expect(quick(element)).toBeNull();
    });
  });

  describe('when there are no images', () => {
    it('shows the "no images" block instead of the add tile when the user cannot add', async () => {
      const { element } = await render([VIEW_IMAGES, DELETE_IMAGES], { count: 0 });
      const empty = element.querySelector('.gallery__empty') as HTMLElement;
      expect(empty).not.toBeNull();
      expect(empty.textContent?.trim()).toBe(text.empty);
      expect(element.querySelector('.gallery__add')).toBeNull();
      expect(items(element)).toHaveLength(0);
    });

    it('shows just the add tile (no text block) when the user can add', async () => {
      const { element } = await render([VIEW_IMAGES, CREATE_IMAGES], { count: 0 });
      expect(element.querySelector('.gallery__empty')).toBeNull();
      expect(element.querySelector('.gallery__add')).not.toBeNull();
    });

    it('does not show the "no images" block while there are images', async () => {
      const { element } = await render([VIEW_IMAGES, DELETE_IMAGES], { count: 2 });
      expect(element.querySelector('.gallery__empty')).toBeNull();
    });

    it('does not show it before the answer of the server either', async () => {
      TestBed.inject(AccountsService).add({ server, accessToken: 'a', refreshToken: 'r', identity: 'u1', permissions: [VIEW_IMAGES] });
      const fixture = TestBed.createComponent(AdminImagesGalleryComponent);
      fixture.detectChanges();
      await vi.waitFor(() => http.expectOne((r) => r.url === listUrl()));
      fixture.detectChanges();
      expect((fixture.nativeElement as HTMLElement).querySelector('.gallery__empty')).toBeNull();
    });

    it('shows the "no images" block once the last image is removed by a user who cannot add', async () => {
      const { fixture, element } = await render([VIEW_IMAGES, DELETE_IMAGES], { count: 1 });
      element.querySelector<HTMLButtonElement>('.gallery__remove')!.click();
      fixture.detectChanges();
      expect(element.querySelector('.gallery__empty')?.textContent?.trim()).toBe(text.empty);
    });
  });

  describe('buttons depend on the rights', () => {
    const removeButtons = (element: HTMLElement) => element.querySelectorAll('.gallery__remove');
    const addButton = (element: HTMLElement) => element.querySelector('.gallery__add');

    it('shows the remove buttons and the add tile with both rights', async () => {
      const { element } = await render([VIEW_IMAGES, CREATE_IMAGES, DELETE_IMAGES]);
      expect(removeButtons(element)).toHaveLength(10);
      expect(addButton(element)).not.toBeNull();
    });

    it('shows the remove buttons but no add tile with DeleteLoginPageImages alone', async () => {
      const { element } = await render([VIEW_IMAGES, DELETE_IMAGES]);
      expect(items(element)).toHaveLength(10);
      expect(removeButtons(element)).toHaveLength(10);
      expect(addButton(element)).toBeNull();
      expect(element.querySelector('input[type=file]')).toBeNull();
    });

    it('shows the add tile but no remove buttons with CreateLoginPageImages alone', async () => {
      const { element } = await render([VIEW_IMAGES, CREATE_IMAGES]);
      expect(items(element)).toHaveLength(10);
      expect(removeButtons(element)).toHaveLength(0);
      expect(addButton(element)).not.toBeNull();
    });

    it('shows neither button without both rights, but still the images', async () => {
      const { element } = await render([VIEW_IMAGES]);
      expect(items(element)).toHaveLength(10);
      expect(removeButtons(element)).toHaveLength(0);
      expect(addButton(element)).toBeNull();
    });
  });

  it('shows the title and the images in a row', async () => {
    const { element } = await render();
    expect(element.querySelector('.gallery__title')?.textContent).toContain(text.title);
    expect(items(element)).toHaveLength(10);
  });

  it('gives every image a numbered alt text from the language file', async () => {
    const { element } = await render();
    const alts = Array.from(element.querySelectorAll('.gallery__image')).map((i) => i.getAttribute('alt'));
    expect(alts[0]).toBe(text.alt.replace('{{number}}', '1'));
    expect(alts[9]).toBe(text.alt.replace('{{number}}', '10'));
  });

  it('keeps the images and the add tile in one scrollable track, with the add tile last', async () => {
    const { element } = await render();
    const track = element.querySelector('.gallery__track') as HTMLElement;
    const children = Array.from(track.children).filter((c) => c.tagName !== 'INPUT');
    expect(children).toHaveLength(11);
    // Зображення, а плитка додавання — останньою.
    expect(children.slice(0, 10).every((c) => c.classList.contains('gallery__item'))).toBe(true);
    expect(children[10].classList.contains('gallery__add')).toBe(true);
  });

  it('gives every image a remove button with a trash icon and a label from the language file', async () => {
    const { element } = await render();
    const buttons = Array.from(element.querySelectorAll<HTMLButtonElement>('.gallery__remove'));
    expect(buttons).toHaveLength(10);
    for (const button of buttons) {
      expect(button.getAttribute('aria-label')).toBe(text.remove);
      expect(button.title).toBe(text.remove);
      expect(button.querySelector('ion-icon')?.getAttribute('name')).toBe('trash-outline');
    }
  });

  it('removes exactly the image whose button was pressed (in the page only, nothing is sent)', async () => {
    const { fixture, element } = await render();
    const buttons = () => Array.from(element.querySelectorAll<HTMLButtonElement>('.gallery__remove'));

    buttons()[3].click();
    fixture.detectChanges();
    expect(items(element)).toHaveLength(9);
    const sources = Array.from(element.querySelectorAll('.gallery__image')).map((i) => i.getAttribute('src'));
    expect(sources).not.toContain(`${server}/files/login/4.jpg`);

    for (let i = 0; i < 9; i++) {
      buttons()[0].click();
      fixture.detectChanges();
    }
    expect(items(element)).toHaveLength(0);
    // Кнопка додавання лишається, навіть коли зображень нема.
    expect(element.querySelector('.gallery__add')).not.toBeNull();
    http.expectNone(() => true);
  });

  it('has an add button with a plus icon and a label, which opens the file dialog', async () => {
    const { element } = await render();
    const add = element.querySelector<HTMLButtonElement>('.gallery__add')!;
    expect(add.getAttribute('aria-label')).toBe(text.add);
    expect(add.querySelector('ion-icon')?.getAttribute('name')).toBe('add-outline');

    const click = vi.spyOn(fileInput(element), 'click');
    add.click();
    expect(click).toHaveBeenCalledTimes(1);
  });

  it('only offers the image types the server accepts', async () => {
    const { element } = await render();
    expect(fileInput(element).accept).toBe('image/png,image/webp,image/jpeg,image/gif');
  });

  it('appends a chosen image to the end of the images', async () => {
    const { fixture, element } = await render();
    choose(element, new File([new Uint8Array(5)], 'new.png', { type: 'image/png' }));
    fixture.detectChanges();

    expect(items(element)).toHaveLength(11);
    const last = items(element)[10].querySelector('.gallery__image')!;
    expect(last.getAttribute('src')).toBe('blob:http://localhost/image-1');
    // Плитка додавання лишається останньою.
    const track = element.querySelector('.gallery__track')!;
    expect(Array.from(track.children).filter((c) => c.tagName !== 'INPUT').pop()?.classList.contains('gallery__add')).toBe(true);
  });

  it('rejects a file that is not an accepted image, with the error from the language file', async () => {
    const { fixture, element } = await render();
    choose(element, new File(['x'], 'doc.pdf', { type: 'application/pdf' }));
    fixture.detectChanges();

    expect(items(element)).toHaveLength(10);
    expect(createUrl).not.toHaveBeenCalled();
    expect(element.querySelector('.settings-field__error')?.textContent?.trim()).toBe(text.error.type);

    choose(element, new File([new Uint8Array(5)], 'ok.png', { type: 'image/png' }));
    fixture.detectChanges();
    expect(element.querySelector('.settings-field__error')).toBeNull();
  });

  it('releases the link of an added image when it is removed and the rest when the page is destroyed', async () => {
    const { fixture, element } = await render();
    choose(element, new File([new Uint8Array(5)], 'a.png', { type: 'image/png' }));
    choose(element, new File([new Uint8Array(5)], 'b.png', { type: 'image/png' }));
    fixture.detectChanges();

    // Зображення з сервера посилань не створюють, тож їх не звільняємо.
    element.querySelectorAll<HTMLButtonElement>('.gallery__remove')[0].click();
    fixture.detectChanges();
    expect(revokeUrl).not.toHaveBeenCalled();

    // Після видалення першого зображення додані стоять під номерами 10 і 11 (індекси 9 і 10).
    element.querySelectorAll<HTMLButtonElement>('.gallery__remove')[9].click();
    fixture.detectChanges();
    expect(revokeUrl).toHaveBeenCalledTimes(1);
    expect(revokeUrl).toHaveBeenCalledWith('blob:http://localhost/image-1');

    fixture.destroy();
    expect(revokeUrl).toHaveBeenCalledTimes(2);
  });

  describe('three lists: system images, deleted ones and added ones', () => {
    const png = (name = 'new.png') => new File([new Uint8Array(5)], name, { type: 'image/png' });
    const removeButton = (element: HTMLElement, index: number) =>
      element.querySelectorAll<HTMLButtonElement>('.gallery__remove')[index];

    it('starts with all the images in the first list, and nothing deleted or added', async () => {
      const { fixture } = await render([VIEW_IMAGES, CREATE_IMAGES, DELETE_IMAGES], { count: 3 });
      const { system, deleted, added } = lists(fixture);
      expect(system.map((i) => i.id)).toEqual(['id-1', 'id-2', 'id-3']);
      expect(deleted).toEqual([]);
      expect(added).toEqual([]);
    });

    it('moves a deleted system image from the first list to the second one, and sends nothing yet', async () => {
      const { fixture, element } = await render([VIEW_IMAGES, CREATE_IMAGES, DELETE_IMAGES], { count: 3 });
      removeButton(element, 1).click();
      fixture.detectChanges();

      const { system, deleted, added } = lists(fixture);
      expect(system.map((i) => i.id)).toEqual(['id-1', 'id-3']);
      expect(deleted.map((i) => i.id)).toEqual(['id-2']);
      expect(deleted[0].url).toBe(itemUrl(2));
      expect(added).toEqual([]);
      expect(items(element)).toHaveLength(2);
      http.expectNone(() => true);
    });

    it('puts an added file into the third list (not into the others), and sends nothing yet', async () => {
      const { fixture, element } = await render([VIEW_IMAGES, CREATE_IMAGES, DELETE_IMAGES], { count: 2 });
      const file = png();
      choose(element, file);
      fixture.detectChanges();

      const { system, deleted, added } = lists(fixture);
      expect(system).toHaveLength(2);
      expect(deleted).toEqual([]);
      expect(added).toHaveLength(1);
      expect(added[0].file).toBe(file);
      expect(items(element)).toHaveLength(3);
      http.expectNone(() => true);
    });

    it('just drops a deleted added image from the third list: it does not go to the second one', async () => {
      const { fixture, element } = await render([VIEW_IMAGES, CREATE_IMAGES, DELETE_IMAGES], { count: 2 });
      choose(element, png());
      fixture.detectChanges();
      expect(lists(fixture).added).toHaveLength(1);

      removeButton(element, 2).click();
      fixture.detectChanges();

      const { system, deleted, added } = lists(fixture);
      expect(added).toEqual([]);
      expect(deleted).toEqual([]);
      expect(system).toHaveLength(2);
      expect(revokeUrl).toHaveBeenCalledWith('blob:http://localhost/image-1');
    });

    it('does not show the deleted images any more, even when more pages are received', async () => {
      TestBed.inject(AccountsService).add({
        server,
        accessToken: 'a',
        refreshToken: 'r',
        identity: 'u1',
        permissions: [VIEW_IMAGES, DELETE_IMAGES],
      });
      const fixture = TestBed.createComponent(AdminImagesGalleryComponent);
      fixture.detectChanges();
      (await vi.waitFor(() => pageRequest(1))).flush(page(serverImages(2), true));
      await vi.waitFor(() => expect(state(fixture).loading()).toBe(false));
      fixture.detectChanges();
      const element = fixture.nativeElement as HTMLElement;

      removeButton(element, 0).click();
      fixture.detectChanges();

      // Сервер (він ще не знає про видалення) повторює видалене на наступній сторінці.
      const track = element.querySelector('.gallery__track') as HTMLElement;
      Object.defineProperty(track, 'scrollWidth', { value: 3500, configurable: true });
      Object.defineProperty(track, 'clientWidth', { value: 800, configurable: true });
      track.scrollLeft = 2500;
      track.dispatchEvent(new Event('scroll'));
      (await vi.waitFor(() => pageRequest(2))).flush(page([{ id: 'id-1', url: '/files/login/1.jpg' }, { id: 'id-9', url: '/files/login/9.jpg' }]));
      await vi.waitFor(() => expect(state(fixture).loading()).toBe(false));

      expect(lists(fixture).system.map((i) => i.id)).toEqual(['id-2', 'id-9']);
    });

    describe('save()', () => {
      const delUrl = (id: string) => `${listUrl()}/${id}`;
      const noContent = { status: 204, statusText: 'No Content' };
      const save = (fixture: { componentInstance: unknown }) =>
        (fixture.componentInstance as unknown as { save(): Promise<string> }).save();
      /** Відповідає на перезавантаження списку після збереження. */
      async function answerReload(count: number) {
        (await vi.waitFor(() => pageRequest(1))).flush(page(serverImages(count)));
      }

      it('has nothing to do (and sends nothing) when the user has changed nothing', async () => {
        const { fixture } = await render([VIEW_IMAGES, CREATE_IMAGES, DELETE_IMAGES], { count: 2 });
        expect(await save(fixture)).toBe('nothing');
        http.expectNone(() => true);
      });

      it('sends DELETE for every image of the second list, then POST for every file of the third one', async () => {
        const { fixture, element } = await render([VIEW_IMAGES, CREATE_IMAGES, DELETE_IMAGES], { count: 3 });
        removeButton(element, 0).click();
        fixture.detectChanges();
        removeButton(element, 0).click();
        fixture.detectChanges();
        choose(element, png('a.png'));
        choose(element, png('b.png'));
        fixture.detectChanges();

        const done = save(fixture);
        const order: string[] = [];
        (await vi.waitFor(() => http.expectOne(delUrl('id-1')))).flush(null, noContent);
        order.push('delete id-1');
        (await vi.waitFor(() => http.expectOne(delUrl('id-2')))).flush(null, noContent);
        order.push('delete id-2');
        for (const name of ['a.png', 'b.png']) {
          const request = await vi.waitFor(() => http.expectOne((r) => r.method === 'POST' && r.url === listUrl()));
          expect(((request.request.body as FormData).get('file') as File).name).toBe(name);
          request.flush('new-id', { status: 201, statusText: 'Created' });
          order.push(`upload ${name}`);
        }
        await answerReload(2);

        expect(await done).toBe('saved');
        expect(order).toEqual(['delete id-1', 'delete id-2', 'upload a.png', 'upload b.png']);
      });

      it('empties the second and the third lists and gets the images from the server again after a successful save', async () => {
        const { fixture, element } = await render([VIEW_IMAGES, CREATE_IMAGES, DELETE_IMAGES], { count: 2 });
        removeButton(element, 0).click();
        choose(element, png());
        fixture.detectChanges();

        const done = save(fixture);
        (await vi.waitFor(() => http.expectOne(delUrl('id-1')))).flush(null, noContent);
        (await vi.waitFor(() => http.expectOne((r) => r.method === 'POST' && r.url === listUrl()))).flush('x', {
          status: 201,
          statusText: 'Created',
        });
        await answerReload(2);
        await done;
        await vi.waitFor(() => expect(state(fixture).loading()).toBe(false));

        const { system, deleted, added } = lists(fixture);
        expect(deleted).toEqual([]);
        expect(added).toEqual([]);
        expect(system.map((i) => i.id)).toEqual(['id-1', 'id-2']);
        expect(revokeUrl).toHaveBeenCalledWith('blob:http://localhost/image-1');
      });

      it('treats an image that is already gone from the server (404) as deleted', async () => {
        const { fixture, element } = await render([VIEW_IMAGES, DELETE_IMAGES], { count: 2 });
        removeButton(element, 0).click();
        fixture.detectChanges();

        const done = save(fixture);
        (await vi.waitFor(() => http.expectOne(delUrl('id-1')))).flush({}, { status: 404, statusText: 'Not Found' });
        await answerReload(1);
        expect(await done).toBe('saved');
        expect(lists(fixture).deleted).toEqual([]);
      });

      it('stops deleting at the first failed deletion, reports it and keeps the unsent ones in the lists', async () => {
        const { fixture, element } = await render([VIEW_IMAGES, DELETE_IMAGES], { count: 3 });
        removeButton(element, 0).click();
        fixture.detectChanges();
        removeButton(element, 0).click();
        fixture.detectChanges();
        removeButton(element, 0).click();
        fixture.detectChanges();

        const done = save(fixture);
        (await vi.waitFor(() => http.expectOne(delUrl('id-1')))).flush(null, noContent);
        (await vi.waitFor(() => http.expectOne(delUrl('id-2')))).flush({}, { status: 403, statusText: 'Forbidden' });
        // Перший видалився — список отримується заново; другий і третій лишаються.
        await answerReload(2);

        expect(await done).toBe('forbidden');
        http.expectNone(delUrl('id-3'));
        expect(lists(fixture).deleted.map((i) => i.id)).toEqual(['id-2', 'id-3']);
      });

      it('uploads the added files even when a deletion failed: they depend only on CreateLoginPageImages', async () => {
        const { fixture, element } = await render([VIEW_IMAGES, CREATE_IMAGES, DELETE_IMAGES], { count: 2 });
        removeButton(element, 0).click();
        fixture.detectChanges();
        choose(element, png('a.png'));
        fixture.detectChanges();

        const done = save(fixture);
        (await vi.waitFor(() => http.expectOne(delUrl('id-1')))).flush({}, { status: 500, statusText: 'x' });
        const upload = await vi.waitFor(() => http.expectOne((r) => r.method === 'POST' && r.url === listUrl()));
        upload.flush('new-id', { status: 201, statusText: 'Created' });
        await answerReload(2);

        // Помилка видалення — перша невдача — і є результатом; завантаження все одно зроблено.
        expect(await done).toBe('error');
        expect(lists(fixture).added).toEqual([]);
        expect(lists(fixture).deleted.map((i) => i.id)).toEqual(['id-1']);
      });

      it('deletes even when an upload failed: deletions depend only on DeleteLoginPageImages', async () => {
        const { fixture, element } = await render([VIEW_IMAGES, CREATE_IMAGES, DELETE_IMAGES], { count: 2 });
        removeButton(element, 0).click();
        fixture.detectChanges();
        choose(element, png('a.png'));
        fixture.detectChanges();

        const done = save(fixture);
        (await vi.waitFor(() => http.expectOne(delUrl('id-1')))).flush(null, noContent);
        (await vi.waitFor(() => http.expectOne((r) => r.method === 'POST' && r.url === listUrl()))).flush(
          {},
          { status: 400, statusText: 'Bad Request' },
        );
        await answerReload(1);

        expect(await done).toBe('invalid');
        expect(lists(fixture).deleted).toEqual([]);
        expect(lists(fixture).added).toHaveLength(1);
      });

      it('sends only the deletions for a user who can delete but not add', async () => {
        const { fixture, element } = await render([VIEW_IMAGES, DELETE_IMAGES], { count: 1 });
        removeButton(element, 0).click();
        fixture.detectChanges();

        const done = save(fixture);
        (await vi.waitFor(() => http.expectOne(delUrl('id-1')))).flush(null, noContent);
        await answerReload(0);
        expect(await done).toBe('saved');
        http.expectNone((r) => r.method === 'POST');
      });

      it('sends only the uploads for a user who can add but not delete', async () => {
        const { fixture, element } = await render([VIEW_IMAGES, CREATE_IMAGES], { count: 2 });
        choose(element, png('a.png'));
        fixture.detectChanges();

        const done = save(fixture);
        (await vi.waitFor(() => http.expectOne((r) => r.method === 'POST' && r.url === listUrl()))).flush('x', {
          status: 201,
          statusText: 'Created',
        });
        await answerReload(3);
        expect(await done).toBe('saved');
        http.expectNone((r) => r.method === 'DELETE');
      });

      it('does not repeat what was done when the user saves again after a failure', async () => {
        const { fixture, element } = await render([VIEW_IMAGES, DELETE_IMAGES], { count: 3 });
        removeButton(element, 0).click();
        fixture.detectChanges();
        removeButton(element, 0).click();
        fixture.detectChanges();

        const first = save(fixture);
        (await vi.waitFor(() => http.expectOne(delUrl('id-1')))).flush(null, noContent);
        (await vi.waitFor(() => http.expectOne(delUrl('id-2')))).flush({}, { status: 500, statusText: 'x' });
        await answerReload(2);
        expect(await first).toBe('error');

        const second = save(fixture);
        http.expectNone(delUrl('id-1'));
        (await vi.waitFor(() => http.expectOne(delUrl('id-2')))).flush(null, noContent);
        await answerReload(1);
        expect(await second).toBe('saved');
      });

      it('reports a failure of an upload', async () => {
        const { fixture, element } = await render([VIEW_IMAGES, CREATE_IMAGES], { count: 1 });
        choose(element, png());
        fixture.detectChanges();

        const done = save(fixture);
        (await vi.waitFor(() => http.expectOne((r) => r.method === 'POST' && r.url === listUrl()))).flush(
          {},
          { status: 400, statusText: 'Bad Request' },
        );
        expect(await done).toBe('invalid');
        expect(lists(fixture).added).toHaveLength(1);
        // Нічого не вдалося — список із сервера не перезавантажуємо.
        http.expectNone((r) => r.method === 'GET');
      });

      it('does not send what the user has no right to send', async () => {
        // Право видаляти є, додавати нема: у третьому списку нічого й не буває, а видалення йдуть.
        const { fixture, element } = await render([VIEW_IMAGES, DELETE_IMAGES], { count: 1 });
        removeButton(element, 0).click();
        fixture.detectChanges();
        const done = save(fixture);
        (await vi.waitFor(() => http.expectOne(delUrl('id-1')))).flush(null, noContent);
        await answerReload(0);
        expect(await done).toBe('saved');
      });

      it('does not accept new additions or deletions while it is saving', async () => {
        const { fixture, element } = await render([VIEW_IMAGES, CREATE_IMAGES, DELETE_IMAGES], { count: 2 });
        removeButton(element, 0).click();
        fixture.detectChanges();

        const done = save(fixture);
        const request = await vi.waitFor(() => http.expectOne(delUrl('id-1')));
        removeButton(element, 0).click();
        choose(element, png());
        fixture.detectChanges();
        expect(lists(fixture).deleted.map((i) => i.id)).toEqual(['id-1']);
        expect(lists(fixture).added).toEqual([]);

        request.flush(null, noContent);
        await answerReload(1);
        await done;
      });
    });
  });

  describe('fade at the edges of the scroll', () => {
    /** У jsdom розкладки нема: розміри прокрутки задаємо самі. */
    function scrollTo(element: HTMLElement, left: number, scrollWidth = 1000, clientWidth = 400) {
      const track = element.querySelector('.gallery__track') as HTMLElement;
      Object.defineProperty(track, 'scrollWidth', { value: scrollWidth, configurable: true });
      Object.defineProperty(track, 'clientWidth', { value: clientWidth, configurable: true });
      track.scrollLeft = left;
      track.dispatchEvent(new Event('scroll'));
    }
    const visible = (element: HTMLElement) =>
      ['start', 'end'].map((side) => element.querySelector(`.gallery__fade--${side}`)!.classList.contains('gallery__fade--visible'));

    it('shows no fade while nothing is hidden by the scroll', async () => {
      const { fixture, element } = await render();
      scrollTo(element, 0, 400, 400);
      fixture.detectChanges();
      expect(visible(element)).toEqual([false, false]);
    });

    it('fades only the end at the start of the row', async () => {
      const { fixture, element } = await render();
      scrollTo(element, 0);
      fixture.detectChanges();
      expect(visible(element)).toEqual([false, true]);
    });

    it('fades both edges in the middle of the row', async () => {
      const { fixture, element } = await render();
      scrollTo(element, 300);
      fixture.detectChanges();
      expect(visible(element)).toEqual([true, true]);
    });

    it('fades only the start when the row is scrolled to the end', async () => {
      const { fixture, element } = await render();
      scrollTo(element, 600);
      fixture.detectChanges();
      expect(visible(element)).toEqual([true, false]);
    });

    it('does not cover the images for the mouse and the screen readers', async () => {
      const { element } = await render();
      for (const fade of Array.from(element.querySelectorAll('.gallery__fade'))) {
        expect(fade.getAttribute('aria-hidden')).toBe('true');
      }
    });
  });

  it('gives the buttons a tabindex (Safari does not tab to them without it)', async () => {
    const { element } = await render();
    for (const button of Array.from(element.querySelectorAll('button'))) {
      expect(button.getAttribute('tabindex')).toBe('0');
    }
  });
});
