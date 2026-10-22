import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { AccountsService } from '../../core/accounts/accounts.service';
import { ConnectionService } from '../../core/connection/connection.service';
import { CurrentUserService } from '../../core/users/current-user.service';
import { SettingsPageModule } from '../settings.module';
import { LanguageService } from '../../core/i18n/language.service';
import { LANGUAGES } from '../../core/i18n/languages.generated';
import { PersonalDataPage } from './personal-data.page';

// Тексти сторінки живуть в uk.json — беремо їх звідти, а не дублюємо.

/** Обирає радіокнопку займенників за порядком: 0 — «Вона/її», … 3 — «Інше». */
const choosePronoun = (element: HTMLElement, index: number) => {
  const radio = element.querySelectorAll('.settings-checks input[type=radio]')[index] as HTMLInputElement;
  radio.checked = true;
  radio.dispatchEvent(new Event('change'));
};
const chosenPronoun = (element: HTMLElement) =>
  Array.from(element.querySelectorAll('.settings-checks input[type=radio]')).findIndex((r) => (r as HTMLInputElement).checked);

describe('PersonalDataPage', () => {
  let http: HttpTestingController;
  let server: string;

  // У jsdom нема URL.createObjectURL/revokeObjectURL: ставимо заглушки для кожного тесту.
  const originalUrlMethods = {
    createObjectURL: (URL as unknown as Record<string, unknown>)['createObjectURL'],
    revokeObjectURL: (URL as unknown as Record<string, unknown>)['revokeObjectURL'],
  };
  let createUrl: ReturnType<typeof vi.fn>;
  let revokeUrl: ReturnType<typeof vi.fn>;

  beforeEach(async () => {
    let counter = 0;
    createUrl = vi.fn(() => `blob:http://localhost/preview-${++counter}`);
    revokeUrl = vi.fn();
    (URL as unknown as Record<string, unknown>)['createObjectURL'] = createUrl;
    (URL as unknown as Record<string, unknown>)['revokeObjectURL'] = revokeUrl;
    localStorage.clear();
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    await TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      imports: [SettingsPageModule],
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
    // Повертаємо URL у початковий стан (у jsdom цих методів нема).
    for (const name of ['createObjectURL', 'revokeObjectURL'] as const) {
      if (originalUrlMethods[name] === undefined) {
        delete (URL as unknown as Record<string, unknown>)[name];
      } else {
        (URL as unknown as Record<string, unknown>)[name] = originalUrlMethods[name];
      }
    }
  });

  /** Підтверджує вхід профілем (після цього сторінка бачить користувача). */
  async function signIn(profile: Record<string, unknown>, ngoName = 'Org'): Promise<void> {
    TestBed.inject(AccountsService).add({ server, accessToken: 'a', refreshToken: 'r', identity: 'u1', ngoName });
    const result = TestBed.inject(CurrentUserService).ensure();
    http.expectOne(`${server}/api/v1/users/me`).flush({ id: 'u1', roles: [], customFields: [], ...profile });
    await result;
  }

  function render() {
    const fixture = TestBed.createComponent(PersonalDataPage);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    return { fixture, element };
  }

  describe('required and optional fields', () => {
    const labelOf = (element: HTMLElement, id: string) => element.querySelector(`label[for=${id}]`) as HTMLElement;
    const hasMark = (el: HTMLElement) => !!el.querySelector('.settings-field__required');

    it.each(['settings-last-name', 'settings-first-name'])('marks %s as required: asterisk and the required attribute', (id) => {
      const { element } = render();
      expect(hasMark(labelOf(element, id))).toBe(true);
      expect((element.querySelector(`#${id}`) as HTMLInputElement).required).toBe(true);
    });

    it.each([
      'settings-patronymic',
      'settings-position',
      'settings-phone',
      'settings-emergency-contact',
      'settings-country',
    ])('leaves %s optional: no asterisk and no required attribute', (id) => {
      const { element } = render();
      expect(hasMark(labelOf(element, id))).toBe(false);
      expect((element.querySelector(`#${id}`) as HTMLInputElement).required).toBe(false);
    });

    it('marks the pronouns as required with a labelled asterisk that screen readers announce', () => {
      const { element } = render();
      const mark = element.querySelector('.settings-fieldset legend .settings-field__required') as HTMLElement;
      expect(mark).not.toBeNull();
      expect(mark.getAttribute('role')).toBe('img');
      expect(mark.getAttribute('aria-label')).toBe(uk.common.required);
    });

    it('leaves the avatar optional: no asterisk next to its label', () => {
      const { element } = render();
      expect(element.querySelector('.settings-avatar .settings-field__required')).toBeNull();
    });

    it('hides the asterisks of text fields from screen readers (the required attribute says it) and gives them a tooltip', () => {
      const { element } = render();
      const mark = labelOf(element, 'settings-last-name').querySelector('.settings-field__required') as HTMLElement;
      expect(mark.getAttribute('aria-hidden')).toBe('true');
      expect(mark.getAttribute('title')).toBe(uk.common.required);
    });
  });

  describe('opening the tab', () => {
    const detailsUrl = (id: string) => `${server}/api/v1/users/${id}`;

    it('asks for the detailed profile of the current user', async () => {
      await signIn({ id: 'user-7', firstName: 'First', lastName: 'Last', email: 'a@b.org' });
      render();
      const request = http.expectOne(detailsUrl('user-7'));
      expect(request.request.method).toBe('GET');
      request.flush({ id: 'user-7', firstName: 'First', lastName: 'Last', email: 'a@b.org', roles: [], customFields: [] });
    });

    it('fills the form and the saved account from the answer', async () => {
      await signIn({ id: 'user-7', firstName: 'Old First', lastName: 'Old Last', email: 'old@b.org' });
      const { fixture, element } = render();
      http.expectOne(detailsUrl('user-7')).flush({
        id: 'user-7',
        email: 'new@b.org',
        firstName: 'First',
        lastName: 'Last',
        patronymic: 'Middle',
        internalPosition: 'Position',
        phoneNumber: '+380123456789',
        country: 'Country',
        emergencyContact: 'Contact',
        avatarUrl: null,
        roles: [],
        customFields: [],
      });
      await vi.waitFor(() => expect(TestBed.inject(CurrentUserService).user()?.lastName).toBe('Last'));
      fixture.detectChanges();

      const value = (id: string) => (element.querySelector(`#${id}`) as HTMLInputElement).value;
      expect(value('settings-last-name')).toBe('Last');
      expect(value('settings-first-name')).toBe('First');
      expect(value('settings-patronymic')).toBe('Middle');
      expect(value('settings-position')).toBe('Position');
      expect(value('settings-phone')).toBe('+380123456789');
      expect(value('settings-emergency-contact')).toBe('Contact');
      expect(value('settings-country')).toBe('Country');

      const account = TestBed.inject(AccountsService).active()!;
      expect(account).toMatchObject({ firstName: 'First', lastName: 'Last', email: 'new@b.org' });
      expect(JSON.parse(localStorage.getItem(`yavir.account.${account.id}`)!)).toMatchObject({
        firstName: 'First',
        lastName: 'Last',
      });
    });

    it('keeps the data from /users/me when the detailed profile is not available', async () => {
      await signIn({ id: 'user-7', firstName: 'First', lastName: 'Last', email: 'a@b.org' });
      const { fixture, element } = render();
      http.expectOne(detailsUrl('user-7')).flush({}, { status: 403, statusText: 'Forbidden' });
      await new Promise((resolve) => setTimeout(resolve));
      fixture.detectChanges();
      expect((element.querySelector('#settings-first-name') as HTMLInputElement).value).toBe('First');
    });

    it('sends nothing when nobody is signed in', () => {
      render();
      http.expectNone(() => true);
    });
  });

  it('shows the title, the labels and the button from the language file', () => {
    const { element } = render();
    expect(element.querySelector('.settings-section__title')?.textContent).toContain(uk.settings.personal.title);
    // Зірочка обовʼязкового поля — не частина тексту підпису.
    const labels = Array.from(element.querySelectorAll('.settings-field__label')).map((l) =>
      l.textContent?.replaceAll('*', '').trim(),
    );
    expect(labels).toEqual([
      uk.settings.personal.lastName,
      uk.settings.personal.firstName,
      uk.settings.personal.patronymic,
      uk.settings.personal.newAvatar,
      uk.settings.personal.position,
      uk.settings.personal.pronouns,
      uk.settings.personal.phone,
      uk.settings.personal.emergencyContact,
      uk.settings.personal.country,
      uk.settings.personal.preferredLanguage,
    ]);
    expect(element.querySelector('.settings-submit')?.textContent?.trim()).toBe(uk.settings.personal.save);
    expect(element.textContent).toContain(uk.settings.personal.avatarHint);
    expect(Array.from(element.querySelectorAll('.settings-check span')).map((s) => s.textContent?.trim())).toEqual([
      uk.settings.personal.pronounShe,
      uk.settings.personal.pronounHe,
      uk.settings.personal.pronounThey,
      uk.settings.personal.pronounOther,
    ]);
  });

  it('prefills the fields from the profile: last name, first name, patronymic and the phone', async () => {
    await signIn({
      firstName: 'First',
      lastName: 'Last',
      patronymic: 'Middle',
      phoneNumber: '+380123456789',
    });
    const { element } = render();
    const value = (id: string) => (element.querySelector(`#${id}`) as HTMLInputElement).value;
    expect(value('settings-last-name')).toBe('Last');
    expect(value('settings-first-name')).toBe('First');
    expect(value('settings-patronymic')).toBe('Middle');
    expect(value('settings-phone')).toBe('+380123456789');
  });

  it('prefills the emergency contact and the country, and puts them near the end, before the language and the save button', async () => {
    await signIn({ firstName: 'First', lastName: 'Last', emergencyContact: '+380501112233, Contact', country: 'Country' });
    const { element } = render();
    const value = (id: string) => (element.querySelector(`#${id}`) as HTMLInputElement).value;
    expect(value('settings-emergency-contact')).toBe('+380501112233, Contact');
    expect(value('settings-country')).toBe('Country');

    const fields = Array.from(element.querySelectorAll('form > *')).map(
      (child) => child.querySelector('input, select')?.id || child.tagName.toLowerCase(),
    );
    // Далі йде лише мова інтерфейсу, а за нею кнопка.
    expect(fields.slice(-4)).toEqual(['settings-emergency-contact', 'settings-country', 'settings-language', 'button']);
  });

  describe('saving', () => {
    const putUrl = (id = 'user-7') => `${server}/api/v1/users/${id}`;
    const profile = {
      id: 'user-7',
      firstName: 'First',
      lastName: 'Last',
      patronymic: 'Middle',
      internalPosition: 'Position',
      phoneNumber: '+380123456789',
      country: 'Country',
      emergencyContact: 'Contact',
      email: 'a@b.org',
      preferredLanguage: 'uk',
      pronouns: uk.settings.personal.pronounShe,
      avatarUrl: '/files/old.png',
    };

    const setText = (element: HTMLElement, id: string, value: string) => {
      const input = element.querySelector(`#${id}`) as HTMLInputElement;
      input.value = value;
      input.dispatchEvent(new Event('input'));
    };
    const errors = (element: HTMLElement) => Array.from(element.querySelectorAll('.settings-field__error')).map((e) => e.textContent?.trim());
    const result = (element: HTMLElement) => element.querySelector('.settings-result');
    const save = (fixture: { detectChanges(): void }, element: HTMLElement) => {
      (element.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit', { cancelable: true }));
      fixture.detectChanges();
    };
    /** Профіль завантажено; повертає сторінку, поля якої вже заповнено. */
    async function open(extra: Record<string, unknown> = {}) {
      await signIn({ ...profile, ...extra });
      const page = render();
      await vi.waitFor(() => expect((page.element.querySelector('#settings-last-name') as HTMLInputElement).value).not.toBe(''));
      return page;
    }

    describe('prefilling the pronouns from the profile', () => {
      it.each([
        [uk.settings.personal.pronounShe, 0],
        [uk.settings.personal.pronounHe, 1],
        [uk.settings.personal.pronounThey, 2],
      ])('turns "%s" into its radio button', async (pronouns, index) => {
        const { fixture, element } = await open({ pronouns });
        fixture.detectChanges();
        expect(chosenPronoun(element)).toBe(index);
        expect((element.querySelector('.settings-input--small') as HTMLInputElement).value).toBe('');
      });

      it('puts anything else into "Other" with that text', async () => {
        const { fixture, element } = await open({ pronouns: 'ze/zir' });
        fixture.detectChanges();
        expect(chosenPronoun(element)).toBe(3);
        expect((element.querySelector('.settings-input--small') as HTMLInputElement).value).toBe('ze/zir');
      });

      it('puts several saved values (from the time of checkboxes) into "Other" as one text', async () => {
        const { fixture, element } = await open({ pronouns: `${uk.settings.personal.pronounShe}, ${uk.settings.personal.pronounThey}` });
        fixture.detectChanges();
        expect(chosenPronoun(element)).toBe(3);
        expect((element.querySelector('.settings-input--small') as HTMLInputElement).value).toBe(`${uk.settings.personal.pronounShe}, ${uk.settings.personal.pronounThey}`);
      });

      it('leaves nothing chosen when the profile has no pronouns', async () => {
        const { fixture, element } = await open({ pronouns: null });
        fixture.detectChanges();
        expect(chosenPronoun(element)).toBe(-1);
      });
    });

    describe('validation', () => {
      it('shows errors under the empty required fields and sends nothing', async () => {
        const { fixture, element } = await open({ pronouns: null });
        setText(element, 'settings-last-name', '');
        setText(element, 'settings-first-name', '  ');
        save(fixture, element);

        expect(errors(element)).toEqual([uk.settings.personal.error.required, uk.settings.personal.error.required, uk.settings.personal.error.pronounsRequired]);
        // Помилка стоїть під своїм полем.
        expect(element.querySelector('#settings-last-name')!.parentElement!.querySelector('.settings-field__error')).not.toBeNull();
        expect(element.querySelector('#settings-first-name')!.parentElement!.querySelector('.settings-field__error')).not.toBeNull();
        expect(element.querySelector('.settings-fieldset .settings-field__error')?.textContent).toContain(uk.settings.personal.error.pronounsRequired);
        http.expectNone({ method: 'PUT', url: putUrl() });
        expect(result(element)).toBeNull();
      });

      it('does not complain about empty optional fields', async () => {
        const { fixture, element } = await open();
        for (const id of ['settings-patronymic', 'settings-position', 'settings-phone', 'settings-emergency-contact', 'settings-country']) {
          setText(element, id, '');
        }
        save(fixture, element);
        expect(errors(element)).toEqual([]);
        http.expectOne({ method: 'PUT', url: putUrl() });
      });

      it.each(['abc', '12345', 'tel 0501112233', '+38050-111-22-33-44-55-66-77'])(
        'rejects the phone %s under its field and sends nothing',
        async (phone) => {
          const { fixture, element } = await open();
          setText(element, 'settings-phone', phone);
          save(fixture, element);

          expect(errors(element)).toEqual([uk.settings.personal.error.phoneInvalid]);
          expect(element.querySelector('#settings-phone')!.parentElement!.querySelector('.settings-field__error')).not.toBeNull();
          http.expectNone({ method: 'PUT', url: putUrl() });
        },
      );

      it.each(['+380501112233', '+38 (050) 111-22-33', '050 111 22 33'])('accepts the phone %s', async (phone) => {
        const { fixture, element } = await open();
        setText(element, 'settings-phone', phone);
        save(fixture, element);

        expect(errors(element)).toEqual([]);
        const request = http.expectOne({ method: 'PUT', url: putUrl() });
        expect((request.request.body as { phoneNumber: string }).phoneNumber).toBe(phone);
        request.flush(null, { status: 204, statusText: 'No Content' });
      });

      it('does not check an empty phone: it is optional', async () => {
        const { fixture, element } = await open();
        setText(element, 'settings-phone', '   ');
        save(fixture, element);
        expect(errors(element)).toEqual([]);
        const request = http.expectOne({ method: 'PUT', url: putUrl() });
        expect((request.request.body as { phoneNumber: unknown }).phoneNumber).toBeNull();
        request.flush(null, { status: 204, statusText: 'No Content' });
      });

      it('asks for the own pronouns when "Other" is chosen and left empty', async () => {
        const { fixture, element } = await open({ pronouns: null });
        choosePronoun(element, 3);
        save(fixture, element);
        expect(errors(element)).toEqual([uk.settings.personal.error.pronounOtherRequired]);
        http.expectNone({ method: 'PUT', url: putUrl() });
      });

      it('updates the errors while typing after the first attempt', async () => {
        const { fixture, element } = await open();
        setText(element, 'settings-last-name', '');
        save(fixture, element);
        expect(errors(element)).toEqual([uk.settings.personal.error.required]);

        setText(element, 'settings-last-name', 'Last');
        fixture.detectChanges();
        expect(errors(element)).toEqual([]);
      });
    });

    describe('the request', () => {
      it('puts the profile to /users/{id}: the typed values, the current language and a null avatar', async () => {
        const { fixture, element } = await open();
        setText(element, 'settings-last-name', ' Other ');
        setText(element, 'settings-position', 'Position B');
        save(fixture, element);

        const request = http.expectOne({ method: 'PUT', url: putUrl() });
        expect(request.request.method).toBe('PUT');
        expect(request.request.body).toEqual({
          firstName: 'First',
          lastName: 'Other',
          patronymic: 'Middle',
          internalPosition: 'Position B',
          phoneNumber: '+380123456789',
          country: 'Country',
          emergencyContact: 'Contact',
          preferredLanguage: 'uk',
          pronouns: uk.settings.personal.pronounShe,
          pronounCategory: 2,
          avatarUrl: null,
        });
        request.flush(null, { status: 204, statusText: 'No Content' });
      });

      it('sends null for every empty optional field, including whitespace only', async () => {
        const { fixture, element } = await open();
        setText(element, 'settings-patronymic', '');
        setText(element, 'settings-position', '   ');
        setText(element, 'settings-phone', '');
        setText(element, 'settings-emergency-contact', '');
        setText(element, 'settings-country', '');
        save(fixture, element);

        const request = http.expectOne({ method: 'PUT', url: putUrl() });
        expect(request.request.body).toMatchObject({
          patronymic: null,
          internalPosition: null,
          phoneNumber: null,
          emergencyContact: null,
          country: null,
        });
        request.flush(null, { status: 204, statusText: 'No Content' });
      });

      it('does not send the email: the API has no such field in this request', async () => {
        const { fixture, element } = await open();
        save(fixture, element);
        const request = http.expectOne({ method: 'PUT', url: putUrl() });
        expect(Object.keys(request.request.body as object)).not.toContain('email');
        request.flush(null, { status: 204, statusText: 'No Content' });
      });

      it('sends a null avatar in the PUT even when a photo has been chosen: the image goes in its own request', async () => {
        const { fixture, element } = await open();
        const input = element.querySelector('.settings-avatar__input') as HTMLInputElement;
        Object.defineProperty(input, 'files', { value: [new File(['x'], 'photo.png', { type: 'image/png' })], configurable: true });
        input.dispatchEvent(new Event('change'));
        save(fixture, element);

        const request = http.expectOne({ method: 'PUT', url: putUrl() });
        expect((request.request.body as { avatarUrl: unknown }).avatarUrl).toBeNull();
        request.flush(null, { status: 204, statusText: 'No Content' });
        await vi.waitFor(() => http.expectOne({ method: 'POST', url: `${putUrl()}/avatar` }).flush(null));
      });

      it.each([
        ['"She"', 0, '', uk.settings.personal.pronounShe, 2],
        ['"He"', 1, '', uk.settings.personal.pronounHe, 1],
        ['"They"', 2, '', uk.settings.personal.pronounThey, 3],
        ['"Other" with a text', 3, ' ze/zir ', 'ze/zir', null],
      ])('maps %s to the pronouns text and the category', async (_name, index, other, text, category) => {
        const { fixture, element } = await open({ pronouns: null });
        choosePronoun(element, index);
        if (other) {
          const small = element.querySelector('.settings-input--small') as HTMLInputElement;
          small.value = other;
          small.dispatchEvent(new Event('input'));
        }
        save(fixture, element);

        const request = http.expectOne({ method: 'PUT', url: putUrl() });
        expect(request.request.body).toMatchObject({ pronouns: text, pronounCategory: category });
        request.flush(null, { status: 204, statusText: 'No Content' });
      });

      it('disables the button while the request is running and does not send twice', async () => {
        const { fixture, element } = await open();
        save(fixture, element);
        const button = element.querySelector('.settings-submit') as HTMLButtonElement;
        expect(button.disabled).toBe(true);
        expect(button.textContent?.trim()).toBe(uk.settings.personal.saving);

        save(fixture, element);
        http.expectOne({ method: 'PUT', url: putUrl() }).flush(null, { status: 204, statusText: 'No Content' });
        await vi.waitFor(() => expect(result(element)).not.toBeNull());
      });
    });

    describe('the avatar (POST /api/v1/users/{id}/avatar)', () => {
      const avatarUrl = () => `${putUrl()}/avatar`;
      const choose = (element: HTMLElement, file: File) => {
        const input = element.querySelector('.settings-avatar__input') as HTMLInputElement;
        Object.defineProperty(input, 'files', { value: [file], configurable: true });
        input.dispatchEvent(new Event('change'));
      };
      const png = () => new File(['image'], 'photo.png', { type: 'image/png' });
      const putOk = async () => {
        const request = await vi.waitFor(() => http.expectOne({ method: 'PUT', url: putUrl() }));
        request.flush(null, { status: 204, statusText: 'No Content' });
      };

      it('sends the chosen image as multipart/form-data (field "file") after the PUT, then reads the profile again', async () => {
        const { fixture, element } = await open();
        // Профіль, який сторінка запросила при відкритті (другий такий запит піде після завантаження аватара).
        http.expectOne({ method: 'GET', url: putUrl() }).flush({ ...profile, roles: [], customFields: [] });
        choose(element, png());
        save(fixture, element);
        http.expectNone({ method: 'POST', url: avatarUrl() });
        await putOk();

        const upload = await vi.waitFor(() => http.expectOne({ method: 'POST', url: avatarUrl() }));
        const form = upload.request.body as FormData;
        expect(form).toBeInstanceOf(FormData);
        expect((form.get('file') as File).name).toBe('photo.png');
        upload.flush(null);

        const profileRequest = await vi.waitFor(() => http.expectOne({ method: 'GET', url: putUrl() }));
        profileRequest.flush({ ...profile, avatarUrl: '/files/new.png', roles: [], customFields: [] });
        await vi.waitFor(() => expect(TestBed.inject(CurrentUserService).user()?.avatarUrl).toBe('/files/new.png'));
        await vi.waitFor(() => expect(result(element) ?? (fixture.detectChanges(), result(element))).not.toBeNull());
        expect(element.querySelector('.settings-avatar__name')?.textContent?.trim()).toBe(uk.settings.personal.noFile);
        expect(result(element)?.textContent?.trim()).toBe(uk.settings.personal.result.success);
      });

      it('sends no image request when no file is chosen', async () => {
        const { fixture, element } = await open();
        save(fixture, element);
        await putOk();
        await vi.waitFor(() => expect(result(element) ?? (fixture.detectChanges(), result(element))).not.toBeNull());
        http.expectNone({ method: 'POST', url: avatarUrl() });
      });

      it('does not send the image when the PUT failed', async () => {
        const { fixture, element } = await open();
        choose(element, png());
        save(fixture, element);
        http.expectOne({ method: 'PUT', url: putUrl() }).flush({}, { status: 400, statusText: 'Bad Request' });
        await vi.waitFor(() => expect(result(element) ?? (fixture.detectChanges(), result(element))).not.toBeNull());
        http.expectNone({ method: 'POST', url: avatarUrl() });
      });

      it.each([
        [400, uk.settings.personal.result.avatar.invalid],
        [401, uk.settings.personal.result.avatar.unauthorized],
        [403, uk.settings.personal.result.avatar.forbidden],
        [500, uk.settings.personal.result.avatar.error],
      ])('shows a red block for HTTP %i of the image request and keeps the file for another try', async (status, text) => {
        const { fixture, element } = await open();
        choose(element, png());
        save(fixture, element);
        await putOk();
        const upload = await vi.waitFor(() => http.expectOne({ method: 'POST', url: avatarUrl() }));
        upload.flush({}, { status, statusText: 'x' });
        await vi.waitFor(() => expect(result(element) ?? (fixture.detectChanges(), result(element))).not.toBeNull());

        const block = result(element)!;
        expect(block.textContent?.trim()).toBe(text);
        expect(block.classList).not.toContain('settings-result--success');
        expect(block.getAttribute('role')).toBe('alert');
        expect(element.querySelector('.settings-avatar__name')?.textContent?.trim()).toBe('photo.png');
        // Дані профілю при цьому збережено.
        expect(TestBed.inject(CurrentUserService).user()?.firstName).toBe('First');
      });

      it('says that the server does not answer for the image request', async () => {
        const { fixture, element } = await open();
        choose(element, png());
        save(fixture, element);
        await putOk();
        (await vi.waitFor(() => http.expectOne({ method: 'POST', url: avatarUrl() }))).error(new ProgressEvent('error'));
        await vi.waitFor(() => expect(result(element) ?? (fixture.detectChanges(), result(element))).not.toBeNull());
        expect(result(element)?.textContent?.trim()).toBe(uk.settings.personal.result.avatar.unreachable);
      });
    });

    describe('the interface language', () => {
      const select = (element: HTMLElement) => element.querySelector('#settings-language') as HTMLSelectElement;
      const options = (element: HTMLElement) =>
        Array.from(select(element).options).map((o) => [o.value, o.textContent?.trim()]);

      it('lists the languages of the translation files (value is the code, text is the name) after an empty option', async () => {
        const { element } = await open();
        expect(options(element)).toEqual([
          ['', uk.settings.personal.languageNone],
          ...LANGUAGES.map((language) => [language.code, language.name]),
        ]);
        // Список складається зі згенерованого переліку файлів, а не прописаний у формі.
        expect(LANGUAGES.length).toBeGreaterThanOrEqual(4);
      });

      it('preselects the language saved in the profile', async () => {
        const { fixture, element } = await open({ preferredLanguage: 'en' });
        fixture.detectChanges();
        expect(select(element).value).toBe('en');
      });

      it('keeps a saved code that has no translation file, showing the code itself', async () => {
        const { fixture, element } = await open({ preferredLanguage: 'pl' });
        fixture.detectChanges();
        expect(options(element).at(-1)).toEqual(['pl', 'pl']);
        expect(select(element).value).toBe('pl');
      });

      it('switches the interface to the language right after it was saved', async () => {
        const { fixture, element } = await open({ preferredLanguage: 'uk' });
        const language = TestBed.inject(LanguageService);
        expect(language.language()).toBe('uk');

        select(element).value = 'en';
        select(element).dispatchEvent(new Event('change'));
        save(fixture, element);
        http.expectOne({ method: 'PUT', url: putUrl() }).flush(null, { status: 204, statusText: 'No Content' });
        await vi.waitFor(() => expect(language.language()).toBe('en'));
      });

      it('switches the interface to the language of the detailed profile when the tab is opened', async () => {
        await signIn({ ...profile, preferredLanguage: 'uk' });
        const language = TestBed.inject(LanguageService);
        render();
        expect(language.language()).toBe('uk');

        http.expectOne({ method: 'GET', url: putUrl() }).flush({ ...profile, preferredLanguage: 'be', roles: [], customFields: [] });
        await vi.waitFor(() => expect(language.language()).toBe('be'));
      });

      it('sends the chosen code, and null when nothing is chosen', async () => {
        const { fixture, element } = await open({ preferredLanguage: 'uk' });
        select(element).value = 'en';
        select(element).dispatchEvent(new Event('change'));
        save(fixture, element);
        const first = http.expectOne({ method: 'PUT', url: putUrl() });
        expect((first.request.body as { preferredLanguage: unknown }).preferredLanguage).toBe('en');
        first.flush(null, { status: 204, statusText: 'No Content' });
        await vi.waitFor(() => expect(TestBed.inject(CurrentUserService).user()?.preferredLanguage).toBe('en'));

        select(element).value = '';
        select(element).dispatchEvent(new Event('change'));
        fixture.detectChanges();
        save(fixture, element);
        const second = http.expectOne({ method: 'PUT', url: putUrl() });
        expect((second.request.body as { preferredLanguage: unknown }).preferredLanguage).toBeNull();
        second.flush(null, { status: 204, statusText: 'No Content' });
      });
    });

    describe('scrolling to the result', () => {
      /** Кладе сторінку всередину `ion-content`, як у справжньому застосунку. */
      function insideContent(element: HTMLElement) {
        const content = document.createElement('ion-content') as unknown as HTMLElement & { scrollToTop: ReturnType<typeof vi.fn> };
        content.scrollToTop = vi.fn();
        document.body.appendChild(content);
        content.appendChild(element);
        return content;
      }

      afterEach(() => document.querySelectorAll('ion-content').forEach((el) => el.remove()));

      it('scrolls the content to the top when the server has answered with success', async () => {
        const { fixture, element } = await open();
        const content = insideContent(element);
        save(fixture, element);
        expect(content.scrollToTop).not.toHaveBeenCalled(); // відповіді ще нема

        http.expectOne({ method: 'PUT', url: putUrl() }).flush(null, { status: 204, statusText: 'No Content' });
        await vi.waitFor(() => expect(content.scrollToTop).toHaveBeenCalledTimes(1));
      });

      it('scrolls to the top when the server has answered with an error, too', async () => {
        const { fixture, element } = await open();
        const content = insideContent(element);
        save(fixture, element);
        http.expectOne({ method: 'PUT', url: putUrl() }).flush({}, { status: 400, statusText: 'Bad Request' });
        await vi.waitFor(() => expect(content.scrollToTop).toHaveBeenCalledTimes(1));
      });

      it('does not scroll when the form is rejected before the request', async () => {
        const { fixture, element } = await open();
        const content = insideContent(element);
        setText(element, 'settings-last-name', '');
        save(fixture, element);
        expect(content.scrollToTop).not.toHaveBeenCalled();
      });
    });

    describe('the result', () => {
      it('shows a green block above the form and applies the saved data to the profile and the account', async () => {
        const { fixture, element } = await open();
        setText(element, 'settings-first-name', 'New First');
        setText(element, 'settings-patronymic', '');
        save(fixture, element);
        http.expectOne({ method: 'PUT', url: putUrl() }).flush(null, { status: 204, statusText: 'No Content' });
        await vi.waitFor(() => expect(TestBed.inject(CurrentUserService).user()?.firstName).toBe('New First'));
        fixture.detectChanges();

        const block = result(element)!;
        expect(block.textContent?.trim()).toBe(uk.settings.personal.result.success);
        expect(block.classList).toContain('settings-result--success');
        expect(block.getAttribute('role')).toBe('status');
        const title = element.querySelector('.settings-section__title')!;
        const form = element.querySelector('form')!;
        expect(title.compareDocumentPosition(block) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
        expect(block.compareDocumentPosition(form) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();

        expect(TestBed.inject(CurrentUserService).user()).toMatchObject({ firstName: 'New First', patronymic: null });
        expect(TestBed.inject(AccountsService).active()).toMatchObject({ firstName: 'New First' });
      });

      it.each([
        [400, uk.settings.personal.result.invalid],
        [403, uk.settings.personal.result.forbidden],
        [404, uk.settings.personal.result.error],
        [500, uk.settings.personal.result.error],
      ])('shows a red block for HTTP %i and keeps what was typed', async (status, text) => {
        const { fixture, element } = await open();
        setText(element, 'settings-first-name', 'New First');
        save(fixture, element);
        http.expectOne({ method: 'PUT', url: putUrl() }).flush({}, { status, statusText: 'x' });
        await vi.waitFor(() => expect(result(element) ?? (fixture.detectChanges(), result(element))).not.toBeNull());

        const block = result(element)!;
        expect(block.textContent?.trim()).toBe(text);
        expect(block.classList).not.toContain('settings-result--success');
        expect(block.getAttribute('role')).toBe('alert');
        expect((element.querySelector('#settings-first-name') as HTMLInputElement).value).toBe('New First');
        expect(TestBed.inject(CurrentUserService).user()?.firstName).toBe('First');
      });

      it('says that the server does not answer', async () => {
        const { fixture, element } = await open();
        save(fixture, element);
        http.expectOne({ method: 'PUT', url: putUrl() }).error(new ProgressEvent('error'));
        await vi.waitFor(() => expect(result(element) ?? (fixture.detectChanges(), result(element))).not.toBeNull());
        expect(result(element)?.textContent?.trim()).toBe(uk.settings.personal.result.unreachable);
      });

      it('hides the result as soon as a field is edited', async () => {
        const { fixture, element } = await open();
        save(fixture, element);
        http.expectOne({ method: 'PUT', url: putUrl() }).flush({}, { status: 400, statusText: 'Bad Request' });
        await vi.waitFor(() => expect(result(element) ?? (fixture.detectChanges(), result(element))).not.toBeNull());

        setText(element, 'settings-country', 'Country B');
        fixture.detectChanges();
        expect(result(element)).toBeNull();
      });
    });
  });

  describe('the "Other" pronouns category', () => {
    const otherRadio = (element: HTMLElement) =>
      element.querySelector('.settings-check-other input[type=radio]') as HTMLInputElement;
    const otherInput = (element: HTMLElement) => element.querySelector('.settings-check-other .settings-input--small') as HTMLInputElement | null;

    const hidden = (element: HTMLElement) => otherInput(element)!.classList.contains('settings-input--hidden');

    it('is the last category, and its input is in the layout from the start but hidden until it is chosen', () => {
      const { element } = render();
      const categories = Array.from(element.querySelectorAll('.settings-checks input[type=radio]'));
      expect(categories).toHaveLength(4);
      expect(categories[3]).toBe(otherRadio(element));
      // Поле в розмітці (займає місце), але приховане: блок має повну ширину одразу.
      expect(otherInput(element)).not.toBeNull();
      expect(hidden(element)).toBe(true);
    });

    it('shows the small input next to it once it is chosen, and hides it again when it is cleared', () => {
      const { fixture, element } = render();

      choosePronoun(element, 3);
      fixture.detectChanges();
      const input = otherInput(element)!;
      expect(hidden(element)).toBe(false);
      expect(input.getAttribute('tabindex')).toBe('0');
      expect(input.getAttribute('aria-label')).toBe(uk.settings.personal.pronounOtherInput);
      expect(input.placeholder).toBe(uk.settings.personal.pronounOtherInput);
      expect(input.classList).toContain('settings-input--small');

      // Обрали інший варіант — поле знову приховане.
      choosePronoun(element, 0);
      fixture.detectChanges();
      expect(otherInput(element)).not.toBeNull();
      expect(hidden(element)).toBe(true);
    });

    it('does not show the input for the other options', () => {
      const { fixture, element } = render();
      choosePronoun(element, 1);
      fixture.detectChanges();
      expect(hidden(element)).toBe(true);
    });

    it('keeps what was typed when another option is chosen and "Other" is chosen again', () => {
      const { fixture, element } = render();
      choosePronoun(element, 3);
      fixture.detectChanges();
      const input = otherInput(element)!;
      input.value = 'ze/zir';
      input.dispatchEvent(new Event('input'));

      choosePronoun(element, 2);
      fixture.detectChanges();
      choosePronoun(element, 3);
      fixture.detectChanges();
      expect(otherInput(element)!.value).toBe('ze/zir');
    });

    it('is a real radio group: one name, one choice at a time', () => {
      const { element } = render();
      const radios = Array.from(element.querySelectorAll('.settings-checks input[type=radio]')) as HTMLInputElement[];
      expect(new Set(radios.map((r) => r.name))).toEqual(new Set(['pronouns']));
      expect(element.querySelector('.settings-checks')?.getAttribute('role')).toBe('radiogroup');
      expect(element.querySelector('.settings-checks input[type=checkbox]')).toBeNull();
    });
  });

  it('leaves the new fields empty when the profile has no such data', async () => {
    await signIn({ firstName: 'First', lastName: 'Last', country: null });
    const { element } = render();
    expect((element.querySelector('#settings-emergency-contact') as HTMLInputElement).value).toBe('');
    expect((element.querySelector('#settings-country') as HTMLInputElement).value).toBe('');
  });

  it('prefills the position from the profile and puts it right before the pronouns', async () => {
    await signIn({ firstName: 'First', lastName: 'Last', internalPosition: 'Position B' });
    const { element } = render();
    expect((element.querySelector('#settings-position') as HTMLInputElement).value).toBe('Position B');

    const blocks = Array.from(element.querySelectorAll('form > *')).map((child) => child.querySelector('input')?.id || child.tagName.toLowerCase());
    const position = blocks.indexOf('settings-position');
    expect(position).toBeGreaterThan(-1);
    // Наступний блок — займенники (fieldset), а не поле вводу.
    expect(blocks[position + 1]).toBe('fieldset');
  });

  it('leaves the position empty when the profile has none', async () => {
    await signIn({ firstName: 'First', lastName: 'Last', internalPosition: null });
    const { element } = render();
    expect((element.querySelector('#settings-position') as HTMLInputElement).value).toBe('');
  });

  it('leaves the patronymic empty when the profile has none', async () => {
    await signIn({ firstName: 'First', lastName: 'Last', patronymic: null, email: 'a@b.org' });
    const { element } = render();
    expect((element.querySelector('#settings-patronymic') as HTMLInputElement).value).toBe('');
    expect((element.querySelector('#settings-first-name') as HTMLInputElement).value).toBe('First');
  });

  it('puts the three name fields in one block, in the order last name, first name, patronymic, with the name autocomplete hints', () => {
    const { element } = render();
    const ids = Array.from(element.querySelectorAll('input.settings-input')).slice(0, 3).map((i) => [i.id, i.getAttribute('autocomplete')]);
    expect(ids).toEqual([
      ['settings-last-name', 'family-name'],
      ['settings-first-name', 'given-name'],
      ['settings-patronymic', 'additional-name'],
    ]);
  });

  it('draws the same avatar as the side menu: the name, the surname and the NGO name of the account', async () => {
    await signIn({ firstName: 'First', lastName: 'Last', email: 'a@b.org' }, 'Org');
    const { fixture, element } = render();
    const avatar = element.querySelector('app-avatar') as HTMLElement;
    expect(avatar).not.toBeNull();
    // Ті самі входи, що й у бічному меню (primary-nav): імʼя, прізвище, зображення й назва ГО.
    const instance = fixture.debugElement.query((el) => el.nativeElement === avatar).componentInstance as {
      firstName(): string;
      lastName(): string;
      ngoName(): string;
    };
    expect([instance.firstName(), instance.lastName(), instance.ngoName()]).toEqual(['First', 'Last', 'Org']);
  });

  it('shows "no file" until a file is chosen, then its name', () => {
    const { fixture, element } = render();
    const name = () => element.querySelector('.settings-avatar__name')?.textContent?.trim();
    expect(name()).toBe(uk.settings.personal.noFile);

    const input = element.querySelector('.settings-avatar__input') as HTMLInputElement;
    const file = new File(['x'], 'photo.png', { type: 'image/png' });
    Object.defineProperty(input, 'files', { value: [file], configurable: true });
    input.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(name()).toBe('photo.png');
  });

  describe('avatar preview', () => {
    // Заглушки `URL.createObjectURL`/`revokeObjectURL` ставить загальний beforeEach.

    const choose = (element: HTMLElement, file: File | null) => {
      const input = element.querySelector('.settings-avatar__input') as HTMLInputElement;
      Object.defineProperty(input, 'files', { value: file ? [file] : [], configurable: true });
      input.dispatchEvent(new Event('change'));
    };
    const image = (element: HTMLElement) => element.querySelector('app-avatar img') as HTMLImageElement | null;
    const png = (name = 'photo.png') => new File(['x'], name, { type: 'image/png' });

    it('shows the chosen image instead of the letters', async () => {
      await signIn({ firstName: 'First', lastName: 'Last', email: 'a@b.org', avatarUrl: null });
      const { fixture, element } = render();
      expect(image(element)).toBeNull();

      choose(element, png());
      fixture.detectChanges();

      expect(createUrl).toHaveBeenCalledTimes(1);
      expect(image(element)?.getAttribute('src')).toBe('blob:http://localhost/preview-1');
      expect(element.querySelector('.settings-avatar__name')?.textContent?.trim()).toBe('photo.png');
    });

    describe('the note about saving', () => {
      const note = (element: HTMLElement) => {
        const hints = Array.from(element.querySelectorAll('.settings-avatar__hint'));
        // Примітка іде одразу після підказки про розмір файлу.
        expect(hints).toHaveLength(2);
        expect(hints[0].textContent).toContain(uk.settings.personal.avatarHint);
        return hints[1] as HTMLElement;
      };

      it('is in the layout under the size hint from the start, but hidden until an image is chosen', () => {
        const { element } = render();
        expect(note(element).textContent?.trim()).toBe(uk.settings.personal.avatarSaveHint);
        expect(note(element).classList).toContain('settings-avatar__hint--hidden');
      });

      it('appears once an image is chosen and hides again when the choice is cleared', () => {
        const { fixture, element } = render();
        choose(element, png());
        fixture.detectChanges();
        expect(note(element).classList).not.toContain('settings-avatar__hint--hidden');

        choose(element, null);
        fixture.detectChanges();
        expect(note(element).classList).toContain('settings-avatar__hint--hidden');
      });

      it('does not appear for a file that is not an image', () => {
        const { fixture, element } = render();
        choose(element, new File(['x'], 'notes.txt', { type: 'text/plain' }));
        fixture.detectChanges();
        expect(note(element).classList).toContain('settings-avatar__hint--hidden');
      });
    });

    it('shows the chosen image over the avatar from the profile', async () => {
      await signIn({ firstName: 'First', lastName: 'Last', email: 'a@b.org', avatarUrl: 'http://x/old.png' });
      const { fixture, element } = render();
      expect(image(element)?.getAttribute('src')).toBe('http://x/old.png');

      choose(element, png());
      fixture.detectChanges();
      expect(image(element)?.getAttribute('src')).toBe('blob:http://localhost/preview-1');
    });

    it('replaces the preview with the next choice and releases the previous one', async () => {
      await signIn({ firstName: 'First', lastName: 'Last', email: 'a@b.org' });
      const { fixture, element } = render();
      choose(element, png('one.png'));
      choose(element, png('two.png'));
      fixture.detectChanges();

      expect(revokeUrl).toHaveBeenCalledWith('blob:http://localhost/preview-1');
      expect(image(element)?.getAttribute('src')).toBe('blob:http://localhost/preview-2');
    });

    it('goes back to the profile avatar when the choice is cleared', async () => {
      await signIn({ firstName: 'First', lastName: 'Last', email: 'a@b.org', avatarUrl: null });
      const { fixture, element } = render();
      choose(element, png());
      fixture.detectChanges();
      expect(image(element)).not.toBeNull();

      choose(element, null);
      fixture.detectChanges();
      expect(image(element)).toBeNull();
      expect(element.querySelector('.settings-avatar__name')?.textContent?.trim()).toBe(uk.settings.personal.noFile);
      expect(revokeUrl).toHaveBeenCalled();
    });

    it('ignores a file that is not an image', async () => {
      await signIn({ firstName: 'First', lastName: 'Last', email: 'a@b.org', avatarUrl: null });
      const { fixture, element } = render();
      choose(element, new File(['x'], 'notes.txt', { type: 'text/plain' }));
      fixture.detectChanges();

      expect(createUrl).not.toHaveBeenCalled();
      expect(image(element)).toBeNull();
      expect(element.querySelector('.settings-avatar__name')?.textContent?.trim()).toBe(uk.settings.personal.noFile);
    });

    it('keeps the chosen image when the profile is refreshed from the server', async () => {
      await signIn({ id: 'user-7', firstName: 'First', lastName: 'Last', email: 'a@b.org', avatarUrl: null });
      const { fixture, element } = render();
      choose(element, png());
      fixture.detectChanges();

      http.expectOne(`${server}/api/v1/users/user-7`).flush({
        id: 'user-7',
        email: 'a@b.org',
        firstName: 'First',
        lastName: 'Last',
        avatarUrl: 'http://x/from-server.png',
        roles: [],
        customFields: [],
      });
      await vi.waitFor(() => expect(TestBed.inject(CurrentUserService).user()?.avatarUrl).toBe('http://x/from-server.png'));
      fixture.detectChanges();
      expect(image(element)?.getAttribute('src')).toBe('blob:http://localhost/preview-1');
    });

    it('releases the image when the tab is closed', async () => {
      await signIn({ firstName: 'First', lastName: 'Last', email: 'a@b.org' });
      const { fixture, element } = render();
      choose(element, png());
      fixture.detectChanges();

      fixture.destroy();
      expect(revokeUrl).toHaveBeenCalledWith('blob:http://localhost/preview-1');
    });
  });

  it('opens the file dialog from the "Choose file" button and accepts images only', () => {
    const { element } = render();
    const input = element.querySelector('.settings-avatar__input') as HTMLInputElement;
    const click = vi.spyOn(input, 'click').mockImplementation(() => undefined);
    (element.querySelector('.settings-avatar__choose') as HTMLButtonElement).click();
    expect(click).toHaveBeenCalled();
    expect(input.accept).toBe('image/png,image/webp,image/jpeg,image/gif');
  });

  describe('avatar file types', () => {
    const choose = (element: HTMLElement, file: File) => {
      const input = element.querySelector('.settings-avatar__input') as HTMLInputElement;
      Object.defineProperty(input, 'files', { value: [file], configurable: true });
      input.dispatchEvent(new Event('change'));
    };
    const error = (element: HTMLElement) => element.querySelector('.settings-avatar__upload .settings-field__error');

    it('lists the types in the hint, from the language file', () => {
      const { element } = render();
      expect(element.textContent).toContain(uk.settings.personal.avatarHint);
    });

    it.each([
      ['photo.png', 'image/png'],
      ['photo.webp', 'image/webp'],
      ['photo.jpeg', 'image/jpeg'],
      ['photo.jpg', 'image/jpeg'],
      ['photo.gif', 'image/gif'],
    ])('accepts %s', (name, type) => {
      const { fixture, element } = render();
      choose(element, new File(['x'], name, { type }));
      fixture.detectChanges();

      expect(element.querySelector('.settings-avatar__name')?.textContent?.trim()).toBe(name);
      expect(error(element)).toBeNull();
    });

    it.each([
      ['photo.svg', 'image/svg+xml'],
      ['photo.bmp', 'image/bmp'],
      ['notes.txt', 'text/plain'],
    ])('rejects %s, with the error from the language file, and keeps "no file"', (name, type) => {
      const { fixture, element } = render();
      choose(element, new File(['x'], name, { type }));
      fixture.detectChanges();

      expect(element.querySelector('.settings-avatar__name')?.textContent?.trim()).toBe(uk.settings.personal.noFile);
      expect(error(element)?.textContent?.trim()).toBe(uk.settings.personal.error.avatarType);
    });

    it('clears the error when an allowed file is chosen afterwards', () => {
      const { fixture, element } = render();
      choose(element, new File(['x'], 'photo.svg', { type: 'image/svg+xml' }));
      fixture.detectChanges();
      expect(error(element)).not.toBeNull();

      choose(element, new File(['x'], 'photo.png', { type: 'image/png' }));
      fixture.detectChanges();
      expect(error(element)).toBeNull();
    });
  });

  it('does not send anything to the server on save: it is only the look so far', () => {
    const { element } = render();
    const form = element.querySelector('form') as HTMLFormElement;
    const event = new Event('submit', { cancelable: true });
    form.dispatchEvent(event);
    expect(event.defaultPrevented).toBe(true);
    http.expectNone(() => true);
  });

  it('gives every control a tabindex (Safari does not tab to the fields without it)', () => {
    const { element } = render();
    const controls = Array.from(element.querySelectorAll('input:not([type=file]), select, button'));
    expect(controls.length).toBeGreaterThan(0);
    for (const control of controls) {
      expect(control.getAttribute('tabindex')).toBe('0');
    }
  });
});
