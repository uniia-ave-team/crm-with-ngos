import { Component, DestroyRef, ElementRef, computed, effect, inject, input, output, signal, viewChild } from '@angular/core';
import { IonIcon } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

import { AvatarComponent } from '../../../shared/avatar/avatar.component';
import { AccountsService } from '../../accounts/accounts.service';
import { UserSummary, UsersService } from '../../users/users.service';

/** Пауза після останнього символу в пошуку, перш ніж запитати колег. */
export const COLLEAGUE_SEARCH_DELAY_MS = 300;
/** Скільки колег показує випадаючий список (пошук на сервері). */
const SUGGESTIONS_PAGE_SIZE = 20;

/** Лічильник полів: у кожного списку свій `id`. */
let nextId = 0;

/** Колега у пошуку: те, що треба для рядка списку (користувач із `UsersService` чи учасник проєкту). */
export type Colleague = Pick<UserSummary, 'id' | 'firstName' | 'lastName' | 'patronymic' | 'avatarUrl' | 'isActive'> & {
  readonly email?: string | null;
};

/** ПІБ колеги: прізвище, імʼя, по батькові (нема — пошта, нема й її — порожньо). */
export function colleagueName(colleague: Colleague): string {
  return (
    [colleague.lastName, colleague.firstName, colleague.patronymic]
      .map((part) => part?.trim())
      .filter(Boolean)
      .join(' ') ||
    colleague.email ||
    ''
  );
}

/** Звідки колеги: `null` — пошук на сервері; список (чи ще вантажиться, чи не вдалося) — пошук у ньому. */
export type ColleagueSource = readonly Colleague[] | 'loading' | 'failed' | null;

/** Стан випадаючого списку колег: шукаємо, є чи не вдалося. */
type SuggestionsState = { readonly status: 'loading' } | { readonly status: 'ready'; readonly colleagues: readonly Colleague[] } | { readonly status: 'failed' };

/**
 * Поле пошуку колеги (вікно доступів проєкту, форма завдання): фокус у полі відкриває під ним список
 * колег (аватар і ПІБ) — прикріплений до поля, на всю його ширину, поверх усього нижче; поки він
 * відкритий, у поля нема нижніх заокруглень. Колеги — з сервера (`source` = `null`: `UsersService.list`,
 * перша сторінка за ПІБ, з `SearchTerm` за текстом поля, після паузи `COLLEAGUE_SEARCH_DELAY_MS`) чи
 * з готового списку (`source`, наприклад учасники проєкту: пошук за ПІБ і поштою тут же). У списку
 * нема вимкнених акаунтів і тих, кого вказано в `excludeIds`; видно щонайбільше 4, далі прокрутка.
 * Вибір віддає колегу (`choose`), очищує поле й закриває список. Вибір кількох (`multiple`, форма
 * завдання) список не закриває, а вибрані (`selectedIds`) лишаються в ньому: ПІБ зелений, на аватарці
 * галочка, після імені — хрестик, що знімає вибір (`deselect`). Вихід із поля чи Esc закриває список.
 * Вміст компонента (наприклад, вибір рівня доступу) стоїть у полі праворуч.
 */
@Component({
  selector: 'app-colleague-search',
  templateUrl: './colleague-search.component.html',
  styleUrls: ['./colleague-search.component.scss'],
  imports: [AvatarComponent, IonIcon, TranslatePipe],
})
export class ColleagueSearchComponent {
  /** Звідки колеги (див. `ColleagueSource`). */
  readonly source = input<ColleagueSource>(null);
  /** `id` колег, яких у списку не показувати (уже учасники, уже вибрані). */
  readonly excludeIds = input<readonly string[]>([]);
  /** Вибір кількох колег: список після вибору не закривається, вибрані — в ньому з позначкою. */
  readonly multiple = input(false);
  /** `id` вибраних колег (для `multiple`). */
  readonly selectedIds = input<readonly string[]>([]);
  /** `id` поля (для підпису `<label for>` у формі); `null` — без нього. */
  readonly inputId = input<string | null>(null);
  /** Ключ підказки в порожньому полі. */
  readonly placeholderKey = input('planner.access.searchPlaceholder');
  /** Ключ назви поля для читачів екрана (`null` — поле підписане `<label>` форми). */
  readonly labelKey = input<string | null>('planner.access.searchLabel');
  /**
   * Де закінчується відкритий список колег — px від верху цього поля (0 — список закритий). Список
   * лежить поверх усього нижче, тож вікно з прокруткою має бути не нижчим за цю межу.
   */
  readonly overlayBottom = output<number>();
  readonly choose = output<Colleague>();
  /** Хрестик після імені вибраного колеги (для `multiple`): зняти вибір. */
  readonly deselect = output<Colleague>();

  private readonly users = inject(UsersService);
  private readonly accounts = inject(AccountsService);

  /** Текст пошуку. */
  protected readonly search = signal('');
  /** Випадаючий список під полем відкритий. */
  protected readonly open = signal(false);
  /** Відповідь сервера на останній пошук (для `source` = `null`). */
  private readonly found = signal<SuggestionsState>({ status: 'loading' });
  /** Назва організації: бере участь у кольорі аватарки (як у колег). */
  protected readonly ngoName = computed(() => this.accounts.active()?.ngoName ?? '');
  /** `id` списку (для `aria-controls`): у кожного поля свій. */
  protected readonly listId = `colleague-search-${++nextId}`;

  protected readonly suggestions = computed<SuggestionsState>(() => {
    const source = this.source();
    if (source === null) {
      return this.found();
    }
    if (source === 'loading' || source === 'failed') {
      return { status: source };
    }
    const text = this.search().trim().toLocaleLowerCase();
    return { status: 'ready', colleagues: text ? source.filter((colleague) => colleagueName(colleague).toLocaleLowerCase().includes(text)) : source };
  });

  /** Колеги в списку: без вимкнених акаунтів і `excludeIds`. */
  protected readonly candidates = computed(() => {
    const state = this.suggestions();
    if (state.status !== 'ready') {
      return [];
    }
    const excluded = new Set(this.excludeIds());
    return state.colleagues.filter((colleague) => colleague.isActive && !excluded.has(colleague.id));
  });

  protected readonly nameOf = colleagueName;
  /** Вибрані колеги (для `multiple`). */
  protected readonly selected = computed(() => new Set(this.multiple() ? this.selectedIds() : []));

  private readonly input = viewChild.required<ElementRef<HTMLInputElement>>('searchInput');
  private readonly dropdown = viewChild<ElementRef<HTMLElement>>('dropdown');
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private searchTimer: ReturnType<typeof setTimeout> | undefined;
  /** Номер запиту колег: відповідь на давніший нічого не змінює. */
  private searchId = 0;

  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.searchTimer));
    // Висота відкритого списку змінюється з кожною відповіддю — міряємо, де він закінчується.
    effect((onCleanup) => {
      const dropdown = this.dropdown()?.nativeElement;
      if (!dropdown) {
        this.overlayBottom.emit(0);
        return;
      }
      const measure = () => this.overlayBottom.emit(dropdown.getBoundingClientRect().bottom - this.host.getBoundingClientRect().top);
      measure();
      if (typeof ResizeObserver === 'function') {
        const observer = new ResizeObserver(measure);
        observer.observe(dropdown);
        onCleanup(() => observer.disconnect());
      }
    });
  }

  /** Ставить курсор у поле. */
  focus(): void {
    this.input().nativeElement.focus();
  }

  /** Фокус у полі: відкриває список колег за поточним текстом. */
  protected onFocus(): void {
    if (!this.open()) {
      this.open.set(true);
      void this.findColleagues(this.search());
    }
  }

  /** Новий текст: на сервері колег шукаємо після паузи (не на кожну літеру). */
  protected onInput(event: Event): void {
    const text = (event.target as HTMLInputElement).value;
    this.search.set(text);
    this.open.set(true);
    clearTimeout(this.searchTimer);
    if (this.source() === null) {
      this.searchTimer = setTimeout(() => void this.findColleagues(text), COLLEAGUE_SEARCH_DELAY_MS);
    }
  }

  /** Вийшли з поля (не в список) чи Esc: список закривається. */
  protected close(): void {
    clearTimeout(this.searchTimer);
    this.open.set(false);
  }

  protected pick(colleague: Colleague): void {
    if (this.selected().has(colleague.id)) {
      return;
    }
    // Кількох вибирають підряд: список лишається відкритим, з тим самим пошуком.
    if (!this.multiple()) {
      this.search.set('');
      this.close();
    }
    this.choose.emit(colleague);
  }

  protected unpick(colleague: Colleague, event: Event): void {
    event.stopPropagation();
    this.deselect.emit(colleague);
  }

  /** `GET /api/v1/users` (`UsersService.list`): перша сторінка колег за ПІБ, з `SearchTerm` за непорожнім текстом. */
  private async findColleagues(text: string): Promise<void> {
    if (this.source() !== null) {
      return;
    }
    const id = ++this.searchId;
    this.found.set({ status: 'loading' });
    const page = await this.users.list(1, SUGGESTIONS_PAGE_SIZE, { orderBy: 'LastName', sortOrder: 'asc' }, text.trim() || null);
    if (id === this.searchId) {
      this.found.set(typeof page === 'string' ? { status: 'failed' } : { status: 'ready', colleagues: page.items });
    }
  }
}
