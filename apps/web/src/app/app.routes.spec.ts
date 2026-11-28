import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';

import { routes } from './app.routes';

describe('app routes', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideRouter(routes)] }));

  /** Маршрут за шляхом. */
  const route = (path: string) => routes.find((candidate) => candidate.path === path)!;

  /** Скільки стражів стоїть на маршруті. */
  const guards = (path: string) => {
    const configured = TestBed.inject(Router).config.find((r) => r.path === path);
    return configured?.canActivate?.length ?? 0;
  };

  it('puts the guard of the rights after the login and connection guards on the pages that depend on rights', () => {
    // authGuard, connectionGuard і страж прав.
    expect(guards('admin-panel')).toBe(3);
    expect(guards('colleagues')).toBe(3);
    expect(guards('knowledge-base')).toBe(3);
    expect(guards('planner')).toBe(3);
    expect(guards('planner/projects/:id')).toBe(3);
  });

  it('keeps only the login and connection guards on the pages that do not depend on rights', () => {
    for (const path of ['start', 'calendar', 'settings']) {
      expect(guards(path), path).toBe(2);
    }
  });

  it('has no guards on the login page, on the pages of the invitation and of the new password and on the page of the repeated login', () => {
    expect(guards('login')).toBe(0);
    expect(guards('join')).toBe(0);
    expect(guards('reset-password')).toBe(0);
    expect(guards('relogin')).toBe(0);
  });

  it('runs the guards on every page change on every page for the one who signed in', () => {
    for (const path of ['calendar', 'planner', 'knowledge-base', 'colleagues', 'admin-panel', 'settings']) {
      expect(route(path).runGuardsAndResolvers).toBe('always');
    }
  });

  it('sends an unknown address to /login, as the last route', async () => {
    const last = routes.at(-1)!;
    expect(last).toEqual({ path: '**', redirectTo: 'login' });
    expect(routes.filter((candidate) => candidate.path === '**')).toHaveLength(1);

    // Сторінку входу тут не вантажимо: досить того, що роутер доходить до неї без помилки.
    const router = TestBed.inject(Router);
    router.resetConfig(router.config.map((candidate) => (candidate.path === 'login' ? { path: 'login', children: [] } : candidate)));
    await router.navigateByUrl('/no-such-page');
    expect(router.url).toBe('/login');
  });
});
