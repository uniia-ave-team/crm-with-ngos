import { Injectable, Signal, inject, signal } from '@angular/core';
import { ActivatedRoute, NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs/operators';

/** Розділи, які мають другу (контекстну) панель бічного меню. */
const LAYOUT_SECTIONS = ['knowledge-base', 'planner', 'settings'] as const;

/**
 * Розділ із другою панеллю. Привʼязується до маршруту через
 * `data: { layoutSection: ... }` у app.routes.ts.
 */
export type LayoutSection = (typeof LAYOUT_SECTIONS)[number];

const COLLAPSE_STORAGE_KEY = 'yavir.secondaryPanelCollapsed';

/**
 * Стан бічного меню: яку контекстну панель показувати (за поточним
 * маршрутом) і чи згорнута вона на десктопі. Сервіс існує в одному
 * екземплярі на весь застосунок (`providedIn: 'root'`), тому підписка на
 * router.events у конструкторі жодного разу не відписується — вона живе,
 * поки живе застосунок.
 */
@Injectable({ providedIn: 'root' })
export class LayoutService {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  private readonly sectionSignal = signal<LayoutSection | null>(this.readSection());
  readonly section: Signal<LayoutSection | null> = this.sectionSignal.asReadonly();

  private readonly collapsedSignal = signal<boolean>(this.readCollapsed());
  readonly collapsed: Signal<boolean> = this.collapsedSignal.asReadonly();

  // Сторінки без бічного меню взагалі: маршрути з `data: { layout: 'none' }` у app.routes.ts.
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
      const value: unknown = route.snapshot.data['layoutSection'];
      section = LAYOUT_SECTIONS.find((candidate) => candidate === value) ?? section;
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
    // Найперше синхронне читання, до першого NavigationEnd: Router ще не зіставив адресу з
    // маршрутом (сторінка довантажується), і `snapshot.data` порожній. Без цієї страховки на
    // /login на мить блимнула б оболонка. Тому беремо шлях з адреси й шукаємо його в
    // конфігурації маршрутів — тих самих, де стоїть `layout: 'none'`.
    const firstSegment = location.pathname.split('/').filter(Boolean)[0] ?? '';
    return this.routeHidesShell(firstSegment);
  }

  /** Чи маршрут першого рівня з таким шляхом позначено `layout: 'none'` у конфігурації маршрутів. */
  private routeHidesShell(path: string): boolean {
    const find = (candidate: string) => this.router.config.find((route) => route.path === candidate);
    let route = find(path);
    // Маршрут-переадресація (корінь `/` веде на `login`): дивимось на сторінку, куди він веде.
    if (typeof route?.redirectTo === 'string') {
      route = find(route.redirectTo.replace(/^\//, ''));
    }
    return route?.data?.['layout'] === 'none';
  }

  private readCollapsed(): boolean {
    try {
      return localStorage.getItem(COLLAPSE_STORAGE_KEY) === '1';
    } catch {
      return false;
    }
  }
}
