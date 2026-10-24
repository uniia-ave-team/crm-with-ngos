import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';

import { AppRoutingModule } from './app-routing.module';

describe('AppRoutingModule', () => {
  beforeEach(() => TestBed.configureTestingModule({ imports: [AppRoutingModule] }));

  /** Скільки стражів стоїть на маршруті. */
  const guards = (path: string) => {
    const route = TestBed.inject(Router).config.find((r) => r.path === path);
    return route?.canActivate?.length ?? 0;
  };

  it('puts the guard of the rights after the login and connection guards on the pages that depend on rights', () => {
    // authGuard, connectionGuard і страж прав.
    expect(guards('admin-panel')).toBe(3);
    expect(guards('colleagues')).toBe(3);
  });

  it('keeps only the login and connection guards on the pages that do not depend on rights', () => {
    for (const path of ['calendar', 'planner', 'knowledge-base', 'settings']) {
      expect(guards(path), path).toBe(2);
    }
  });

  it('has no guards on the login page', () => {
    expect(guards('login')).toBe(0);
  });
});
