import { Injectable, Signal, inject, signal } from '@angular/core';
import { ActivatedRoute, NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs/operators';

/**
 * Сторінки, які мають другу (контекстну) панель бічного меню.
 * Прив'язується до маршруту через `data: { layoutSection: ... }`
 * у app-routing.module.ts.
 */
export type LayoutSection = 'knowledge-base' | 'planner';

const COLLAPSE_STORAGE_KEY = 'yavir.secondaryPanelCollapsed';

/**
 * Перші сегменти шляху сторінок без оболонки — дублює `data: { layout:
 * 'none' }` з app-routing.module.ts. Потрібне лише як підстраховка на
 * найперше синхронне читання (див. readHideShell): на ньому Router іноді
 * ще не встиг домапити дочірній маршрут (особливо для lazy-модуля), і
 * ActivatedRoute.snapshot.data порожній — без цієї підстраховки на
 * /login на мить блимнула б оболонка, перш ніж реакція на NavigationEnd
 * прибрала б її.
 */
// Порожній рядок — корінь '/', який завжди редіректить на 'login' (див.
// app-routing.module.ts) — на цю ж мить синхронного читання він ще не встиг
// розгорнутись, тож підстраховка мусить трактувати корінь так само.
const NO_SHELL_PATHS = new Set(['login', '']);

/**
 * Стан бічного меню: яку контекстну панель показувати (за поточним
 * маршрутом) і чи згорнута вона на десктопі. Сервіс — синглтон на весь
 * застосунок, тому підписка на router.events у конструкторі жодного разу
 * не відписується — вона живе, поки живе застосунок.
 */
@Injectable({ providedIn: 'root' })
export class LayoutService {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  private readonly sectionSignal = signal<LayoutSection | null>(this.readSection());
  readonly section: Signal<LayoutSection | null> = this.sectionSignal.asReadonly();

  private readonly collapsedSignal = signal<boolean>(this.readCollapsed());
  readonly collapsed: Signal<boolean> = this.collapsedSignal.asReadonly();

  // Сторінки без бічного меню взагалі (наразі — тільки /login).
  // Прив'язується через `data: { layout: 'none' }` у app-routing.module.ts.
  private readonly hideShellSignal = signal<boolean>(this.readHideShell());
  readonly hideShell: Signal<boolean> = this.hideShellSignal.asReadonly();

  constructor() {
    this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe(() => {
        this.sectionSignal.set(this.readSection());
        this.hideShellSignal.set(this.readHideShell());
      });
  }

  toggleCollapsed(): void {
    this.setCollapsed(!this.collapsedSignal());
  }

  setCollapsed(collapsed: boolean): void {
    this.collapsedSignal.set(collapsed);
    try {
      localStorage.setItem(COLLAPSE_STORAGE_KEY, collapsed ? '1' : '0');
    } catch {
      // Сховище може бути недоступне (приватний режим тощо) — просто не зберігаємо.
    }
  }

  private readSection(): LayoutSection | null {
    let route: ActivatedRoute | null = this.route.root;
    let section: LayoutSection | null = null;
    while (route) {
      const value = route.snapshot.data['layoutSection'];
      if (value === 'knowledge-base' || value === 'planner') {
        section = value;
      }
      route = route.firstChild;
    }
    return section;
  }

  private readHideShell(): boolean {
    let route: ActivatedRoute | null = this.route.root;
    let hide = false;
    let matchedChild = false;
    while (route) {
      if (route.firstChild) {
        matchedChild = true;
      }
      const value = route.snapshot.data['layout'];
      if (value === 'none') {
        hide = true;
      }
      route = route.firstChild;
    }
    if (matchedChild) {
      return hide;
    }
    // ActivatedRoute ще не домапив жодної дитини (найперше синхронне
    // читання, до першого NavigationEnd) — читаємо шлях напряму з адреси.
    const firstSegment = location.pathname.split('/').filter(Boolean)[0] ?? '';
    return NO_SHELL_PATHS.has(firstSegment);
  }

  private readCollapsed(): boolean {
    try {
      return localStorage.getItem(COLLAPSE_STORAGE_KEY) === '1';
    } catch {
      return false;
    }
  }
}
