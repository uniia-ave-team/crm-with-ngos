import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';

import { LayoutService } from './layout.service';

@Component({ template: '', standalone: false })
class EmptyPage {}

const COLLAPSE_KEY = 'yavir.secondaryPanelCollapsed';

describe('LayoutService', () => {
  /** Створює сервіс, коли в адресному рядку вже стоїть `path` (як при відкритті сторінки). */
  function createAt(path: string): LayoutService {
    window.history.replaceState({}, '', path);
    TestBed.configureTestingModule({
      declarations: [EmptyPage],
      providers: [
        provideRouter([
          { path: 'login', component: EmptyPage, data: { layout: 'none' } },
          { path: 'planner', component: EmptyPage, data: { layoutSection: 'planner' } },
          { path: 'colleagues', component: EmptyPage },
          {
            path: 'settings',
            component: EmptyPage,
            data: { layoutSection: 'settings' },
            children: [{ path: 'password', component: EmptyPage }],
          },
          { path: 'odd', component: EmptyPage, data: { layoutSection: 'unknown-section' } },
        ]),
      ],
    });
    return TestBed.inject(LayoutService);
  }

  beforeEach(() => localStorage.clear());

  afterEach(() => {
    window.history.replaceState({}, '', '/');
    localStorage.clear();
    vi.restoreAllMocks();
  });

  describe('hideShell at the very first read, before the router has matched anything', () => {
    it.each([
      ['/login', true],
      ['/login?error=no-connection', true],
      ['/join', true],
      ['/relogin', true],
      ['/', true],
      ['/planner', false],
      ['/settings/password', false],
      ['/loginx', false],
    ])('for %s is %s (taken from the address itself)', (path, hidden) => {
      expect(createAt(path).hideShell()).toBe(hidden);
    });
  });

  describe('after a navigation', () => {
    it('hides the shell on a route with layout: none and shows it on the others', async () => {
      const service = createAt('/login');
      const router = TestBed.inject(Router);

      await router.navigateByUrl('/planner');
      expect(service.hideShell()).toBe(false);
      await router.navigateByUrl('/login');
      expect(service.hideShell()).toBe(true);
      await router.navigateByUrl('/colleagues');
      expect(service.hideShell()).toBe(false);
    });

    it('takes the section of the second panel from the route, and has none on a route without it', async () => {
      const service = createAt('/login');
      const router = TestBed.inject(Router);
      expect(service.section()).toBeNull();

      await router.navigateByUrl('/planner');
      expect(service.section()).toBe('planner');
      await router.navigateByUrl('/colleagues');
      expect(service.section()).toBeNull();
    });

    it('keeps the section of the parent route on its child pages', async () => {
      const service = createAt('/login');
      await TestBed.inject(Router).navigateByUrl('/settings/password');
      expect(service.section()).toBe('settings');
    });

    it('ignores a section that it does not know', async () => {
      const service = createAt('/login');
      await TestBed.inject(Router).navigateByUrl('/odd');
      expect(service.section()).toBeNull();
    });
  });

  describe('the collapsed state of the second panel', () => {
    it('is expanded by default, and collapsed when it was saved so', () => {
      expect(createAt('/planner').collapsed()).toBe(false);
      TestBed.resetTestingModule();
      localStorage.setItem(COLLAPSE_KEY, '1');
      expect(createAt('/planner').collapsed()).toBe(true);
    });

    it('toggles and remembers the state in localStorage', () => {
      const service = createAt('/planner');
      service.toggleCollapsed();
      expect(service.collapsed()).toBe(true);
      expect(localStorage.getItem(COLLAPSE_KEY)).toBe('1');

      service.toggleCollapsed();
      expect(service.collapsed()).toBe(false);
      expect(localStorage.getItem(COLLAPSE_KEY)).toBe('0');
    });

    it('still works when localStorage cannot be read or written', () => {
      vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
        throw new Error('blocked');
      });
      vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
        throw new Error('blocked');
      });
      const service = createAt('/planner');
      expect(service.collapsed()).toBe(false);
      service.setCollapsed(true);
      expect(service.collapsed()).toBe(true);
    });
  });
});
