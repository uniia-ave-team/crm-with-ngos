import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { ConnectionService } from '../../core/connection/connection.service';
import { AvatarModule } from './avatar.module';

@Component({
  template: `<app-avatar [firstName]="firstName()" [lastName]="lastName()" [avatarUrl]="avatarUrl()" [ngoName]="ngoName()" [outdated]="outdated()" />`,
  imports: [AvatarModule],
})
class HostComponent {
  readonly firstName = signal<string | null | undefined>('');
  readonly lastName = signal<string | null | undefined>('');
  readonly avatarUrl = signal<string | null | undefined>(null);
  readonly ngoName = signal<string | null | undefined>('');
  readonly outdated = signal(false);
}

interface Values {
  firstName?: string | null;
  lastName?: string | null;
  avatarUrl?: string | null;
  ngoName?: string | null;
  outdated?: boolean;
}

describe('AvatarComponent', () => {
  let server: string;

  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({
      imports: [HostComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    const connection = TestBed.inject(ConnectionService);
    TestBed.inject(HttpTestingController).expectOne(connection.apiHealthUrl());
    server = connection.serverUrl();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  function render(values: Values = {}) {
    const fixture = TestBed.createComponent(HostComponent);
    if ('firstName' in values) fixture.componentInstance.firstName.set(values.firstName);
    if ('lastName' in values) fixture.componentInstance.lastName.set(values.lastName);
    if ('avatarUrl' in values) fixture.componentInstance.avatarUrl.set(values.avatarUrl);
    if ('ngoName' in values) fixture.componentInstance.ngoName.set(values.ngoName);
    if ('outdated' in values) fixture.componentInstance.outdated.set(values.outdated!);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const avatar = () => element.querySelector<HTMLElement>('.avatar')!;
    return { fixture, element, avatar, image: () => element.querySelector('img') };
  }

  it('shows the first letters of the first and last name in upper case', () => {
    const { avatar } = render({ firstName: '  єдиний', lastName: ' вишуканий' });
    expect(avatar().textContent?.trim()).toBe('ЄВ');
  });

  it('colours the background by the name (see avatar-color.ts)', () => {
    const { avatar } = render({ firstName: 'Єдиний', lastName: 'Вишуканий' });
    // #767a42
    expect(avatar().style.backgroundColor).toBe('rgb(118, 122, 66)');
  });

  it('takes the name of the organization into account in the colour', () => {
    const people = { firstName: 'Перший', lastName: 'Другий' };
    const without = render(people).avatar().style.backgroundColor;
    const withNgo = render({ ...people, ngoName: 'Організація' }).avatar().style.backgroundColor;
    // #cc29af
    expect(withNgo).toBe('rgb(204, 41, 175)');
    expect(withNgo).not.toBe(without);
    expect(render({ ...people, ngoName: null }).avatar().style.backgroundColor).toBe(without);
  });

  describe('outdated account', () => {
    it('drops the computed colour for the grey fill and keeps the letters', () => {
      const { avatar } = render({ firstName: 'Єдиний', lastName: 'Вишуканий', outdated: true });
      expect(avatar().classList).toContain('avatar--outdated');
      expect(avatar().classList).not.toContain('avatar--photo');
      expect(avatar().style.backgroundColor).toBe('');
      expect(avatar().textContent?.trim()).toBe('ЄВ');
    });

    it('lays a grey layer over the picture when there is one', () => {
      const { avatar, image } = render({ firstName: 'Єдиний', lastName: 'Вишуканий', avatarUrl: 'http://x/a.png', outdated: true });
      expect(image()).not.toBeNull();
      expect(avatar().classList).toContain('avatar--outdated');
      expect(avatar().classList).toContain('avatar--photo');
    });

    it('is not marked for a valid account', () => {
      const { avatar } = render({ firstName: 'Єдиний', lastName: 'Вишуканий', avatarUrl: 'http://x/a.png' });
      expect(avatar().classList).not.toContain('avatar--outdated');
    });
  });

  it('gives people with the same initials different colours', () => {
    const first = render({ firstName: 'Перший', lastName: 'Другий' }).avatar().style.backgroundColor;
    const second = render({ firstName: 'Пізній', lastName: 'Другий' }).avatar().style.backgroundColor;
    expect(first).not.toBe(second);
  });

  it('shows the default blue without letters until there is any data about the user', () => {
    const { avatar } = render();
    expect(avatar().textContent?.trim()).toBe('');
    expect(avatar().style.backgroundColor).toBe('');
  });

  it('copes with a missing last name', () => {
    const { avatar } = render({ firstName: 'Перший', lastName: undefined });
    expect(avatar().textContent?.trim()).toBe('П');
  });

  it('shows the picture instead of letters and the fill when avatarUrl is set', () => {
    const { avatar, image } = render({ firstName: 'Перший', lastName: 'Другий', avatarUrl: 'https://cdn.example.org/1.png' });
    expect(image()?.getAttribute('src')).toBe('https://cdn.example.org/1.png');
    expect(avatar().textContent?.trim()).toBe('');
    expect(avatar().style.backgroundColor).toBe('transparent');
  });

  it('resolves a relative avatarUrl against the server address', () => {
    const { image } = render({ avatarUrl: '/files/avatars/1.png' });
    expect(image()?.getAttribute('src')).toBe(`${server}/files/avatars/1.png`);
  });

  it('treats an empty avatarUrl as no picture', () => {
    const { avatar, image } = render({ firstName: 'Перший', lastName: 'Другий', avatarUrl: '   ' });
    expect(image()).toBeNull();
    expect(avatar().textContent?.trim()).toBe('ПД');
  });

  it('falls back to the letters and the colour when the picture cannot be loaded', () => {
    const { fixture, avatar, image } = render({ firstName: 'Перший', lastName: 'Другий', avatarUrl: 'https://cdn.example.org/missing.png' });
    image()!.dispatchEvent(new Event('error'));
    fixture.detectChanges();
    expect(image()).toBeNull();
    expect(avatar().textContent?.trim()).toBe('ПД');
    expect(avatar().style.backgroundColor).not.toBe('transparent');
    expect(avatar().style.backgroundColor).not.toBe('');
  });

  it('tries a new picture after the old one failed', () => {
    const { fixture, image } = render({ firstName: 'Перший', lastName: 'Другий', avatarUrl: 'https://cdn.example.org/missing.png' });
    image()!.dispatchEvent(new Event('error'));
    fixture.detectChanges();

    fixture.componentInstance.avatarUrl.set('https://cdn.example.org/new.png');
    fixture.detectChanges();
    expect(image()?.getAttribute('src')).toBe('https://cdn.example.org/new.png');
  });
});
