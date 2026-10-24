import {
  Component,
  DestroyRef,
  ElementRef,
  afterNextRender,
  afterRenderEffect,
  computed,
  effect,
  inject,
  signal,
  untracked,
  viewChild,
} from '@angular/core';

import { PERMISSIONS } from '../../core/auth/permissions';
import { RequestFailure } from '../../core/http/request-failure';
import { IMAGE_TYPES } from '../../core/images/image-types';
import { LoginPageImagesService } from '../../core/login-page-images/login-page-images.service';
import { CurrentUserService } from '../../core/users/current-user.service';

/** Зображення, що є в системі: `id` на сервері й посилання на файл. */
interface SystemImage {
  readonly id: string;
  readonly url: string;
}

/** Зображення, яке користувач додав на цій сторінці й ще не завантажив на сервер. */
interface AddedImage {
  readonly key: number;
  readonly file: File;
  /** `blob:`-посилання на файл для превʼю (його треба звільняти). */
  readonly url: string;
}

/** Плитка рядка: зображення з системи або додане користувачем. */
interface Tile {
  readonly key: string;
  readonly url: string;
  readonly system: SystemImage | null;
  readonly added: AddedImage | null;
}

/**
 * Чим закінчилося збереження галереї: `saved` — щось змінено на сервері, `nothing` —
 * змін не було (або нема прав їх надсилати), інакше — причина першої невдачі.
 */
export type GallerySaveResult = 'saved' | 'nothing' | RequestFailure;


/** Скільки зображень просимо в сервера за один запит. */
const PAGE_SIZE = 20;

/** Наступну сторінку просимо, коли до кінця рядка лишилося менше стільки пікселів (≈ дві з половиною плитки). */
const LOAD_AHEAD_PX = 400;

/**
 * Галерея зображень сторінки входу на сторінці «Адмін» (частина форми
 * організації, її кнопка «Зберегти» спільна): зображення 8:10 заввишки 200px в
 * один рядок із прокруткою по X; при наведенні на зображення зʼявляється кнопка
 * видалення.
 *
 * Зображень може бути тисячі, тому вони підвантажуються частинами: перша
 * сторінка при відкритті (якщо є право `Permissions.ViewLoginPageImages`), а
 * наступні — коли прокрутка наближається до кінця рядка (в кінці рядка тоді
 * крутиться прелоадер). Якщо рядок ще не заповнює вікно, сторінки добираються
 * самі.
 *
 * Плитка додавання стоїть останньою в рядку (лише з
 * `Permissions.CreateLoginPageImages`); поки її не видно (рядок довгий і до
 * кінця далеко), на правому краї рядка зʼявляється маленька кнопка «+». Кнопки
 * видалення — лише з `Permissions.DeleteLoginPageImages`. Немає зображень і немає
 * права додавати — на місці плитки блок «Наразі відсутні зображення…».
 *
 * Зміни користувача лежать у трьох списках і на сервер йдуть лише за кнопкою
 * «Зберегти» (`save()`; її натискає форма):
 *
 * 1. `systemImages` — зображення з системи (отримані з сервера);
 * 2. `deletedImages` — зображення з системи, які користувач видалив (переносяться
 *    сюди з першого списку); за ними йдуть `DELETE /api/v1/loginpageimages/{id}`;
 * 3. `addedImages` — файли, додані користувачем; за ними йдуть
 *    `POST /api/v1/loginpageimages`. Видалення доданого файлу просто прибирає
 *    його з третього списку.
 *
 * Після збереження список із сервера отримується заново (позиції сторінок
 * зсунулися, а нові зображення мають зʼявитися з їхніми посиланнями).
 */
@Component({
  selector: 'app-admin-images-gallery',
  templateUrl: './images-gallery.component.html',
  styleUrls: ['../../settings/settings-section.scss', './images-gallery.component.scss'],
  standalone: false,
})
export class AdminImagesGalleryComponent {
  protected readonly accept = IMAGE_TYPES.join(',');

  private readonly permissions = inject(CurrentUserService).permissions;
  private readonly service = inject(LoginPageImagesService);
  private readonly canView = computed(() => this.permissions().includes(PERMISSIONS.viewLoginPageImages));
  protected readonly canRemove = computed(() => this.permissions().includes(PERMISSIONS.deleteLoginPageImages));
  protected readonly canAdd = computed(() => this.permissions().includes(PERMISSIONS.createLoginPageImages));

  /** 1. Зображення з системи. */
  private readonly systemImages = signal<readonly SystemImage[]>([]);
  /** 2. Зображення з системи, видалені користувачем (за ними підуть запити на видалення). */
  private readonly deletedImages = signal<readonly SystemImage[]>([]);
  /** 3. Зображення, додані користувачем (за ними підуть запити на завантаження). */
  private readonly addedImages = signal<readonly AddedImage[]>([]);
  private nextKey = 1;

  /** Що показує рядок: зображення з системи (крім видалених), потім додані. */
  protected readonly tiles = computed<readonly Tile[]>(() => [
    ...this.systemImages().map((image) => ({ key: `s-${image.id}`, url: image.url, system: image, added: null })),
    ...this.addedImages().map((image) => ({ key: `a-${image.key}`, url: image.url, system: null, added: image })),
  ]);

  /** Йде збереження змін на сервер: додавання й видалення в цей час не приймаються. */
  protected readonly saving = signal(false);

  /** Номер наступної сторінки, яку попросимо в сервера. */
  private nextPage = 1;
  /** Сервер сказав, що за отриманими є ще сторінки (до першої відповіді вважаємо, що є). */
  private readonly hasMore = signal(true);
  /** Іде запит сторінки: в кінці рядка крутиться прелоадер. */
  protected readonly loading = signal(false);
  /** Перша сторінка отримана (до того «порожньо» ще не відомо). */
  private readonly firstLoaded = signal(false);
  /** Останній запит не вдався: автоматично більше не просимо, поки користувач знову не прокрутить до кінця. */
  protected readonly loadFailed = signal(false);

  /** Список отримано, зображень нема, а додавати користувач не може: замість плитки — текст. */
  protected readonly showEmpty = computed(
    () => this.firstLoaded() && !this.loading() && this.tiles().length === 0 && !this.canAdd(),
  );

  /** Остання спроба додати файл не вдалася (файл не зображення потрібного типу). */
  protected readonly typeError = signal(false);

  private readonly track = viewChild<ElementRef<HTMLElement>>('track');
  private readonly fileInput = viewChild<ElementRef<HTMLInputElement>>('imageFile');
  /** Ліворуч є приховані прокруткою зображення: на краю показуємо градієнт. */
  protected readonly fadeStart = signal(false);
  /** Праворуч є приховані прокруткою зображення: на краю показуємо градієнт. */
  protected readonly fadeEnd = signal(false);
  /** Плитки додавання (вона в кінці рядка) не видно: показуємо маленьку кнопку «+». */
  private readonly addTileOutOfView = signal(false);
  protected readonly showQuickAdd = computed(() => this.canAdd() && this.addTileOutOfView());

  constructor() {
    const destroyRef = inject(DestroyRef);

    // Посилання на додані файли живуть, поки відкрита сторінка.
    destroyRef.onDestroy(() => {
      for (const image of this.addedImages()) {
        URL.revokeObjectURL(image.url);
      }
    });

    // Право перегляду є (воно може прийти трохи пізніше за сторінку) — просимо
    // першу сторінку.
    effect(() => {
      if (this.canView() && untracked(() => this.nextPage === 1 && !this.loading() && !this.firstLoaded())) {
        untracked(() => void this.loadNextPage());
      }
    });

    // Краї прокрутки перераховуємо, коли міняється список зображень, і добираємо
    // сторінки, якщо рядок ще не заповнює вікно.
    afterRenderEffect(() => {
      this.tiles();
      this.loading();
      this.updateScrollState();
      if (this.nearEnd()) {
        void this.loadNextPage();
      }
    });
    afterNextRender(() => {
      const element = this.track()?.nativeElement;
      if (element && typeof ResizeObserver !== 'undefined') {
        const observer = new ResizeObserver(() => this.updateScrollState());
        observer.observe(element);
        destroyRef.onDestroy(() => observer.disconnect());
      }
    });
  }

  /** Прокрутка рядка: градієнти, маленька «+» і, коли кінець близько, наступна сторінка. */
  protected onScroll(): void {
    this.updateScrollState();
    if (this.nearEnd()) {
      // Після невдачі повторюємо лише коли користувач сам знову дійшов до кінця.
      this.loadFailed.set(false);
      void this.loadNextPage();
    }
  }

  private updateScrollState(): void {
    const element = this.track()?.nativeElement;
    if (!element) {
      return;
    }
    const maxScroll = element.scrollWidth - element.clientWidth;
    // Піксель запасу: дробові розміри не мають залишати градієнт на самому краю.
    this.fadeStart.set(element.scrollLeft > 1);
    this.fadeEnd.set(maxScroll - element.scrollLeft > 1);
    // Плитка додавання стоїть останньою: її не видно, коли вона цілком за правим
    // краєм видимої частини рядка (або цілком за лівим). Без розкладки — невідомо.
    const tile = element.querySelector<HTMLElement>('.gallery__add');
    const visibleFrom = element.scrollLeft;
    const visibleTo = element.scrollLeft + element.clientWidth;
    this.addTileOutOfView.set(
      !!tile &&
        element.clientWidth > 0 &&
        (tile.offsetLeft >= visibleTo || tile.offsetLeft + tile.offsetWidth <= visibleFrom),
    );
  }

  /** До кінця рядка лишилося менше `LOAD_AHEAD_PX` (без розкладки — наприклад, у тестах — невідомо, тож ні). */
  private nearEnd(): boolean {
    const element = this.track()?.nativeElement;
    if (!element || element.clientWidth === 0) {
      return false;
    }
    return element.scrollWidth - (element.scrollLeft + element.clientWidth) < LOAD_AHEAD_PX;
  }

  /** Просить у сервера наступну сторінку (одну; повторні виклики, поки вона йде, ігноруються). */
  private async loadNextPage(): Promise<void> {
    if (!this.canView() || this.loading() || !this.hasMore() || this.loadFailed()) {
      return;
    }
    this.loading.set(true);
    const result = await this.service.page(this.nextPage, PAGE_SIZE);
    this.loading.set(false);
    if (typeof result === 'string') {
      this.loadFailed.set(true);
      return;
    }
    this.nextPage++;
    this.hasMore.set(result.hasNextPage);
    this.firstLoaded.set(true);
    // Те, що користувач уже видалив (воно ще є на сервері), і повтори не показуємо.
    const known = new Set([...this.systemImages(), ...this.deletedImages()].map((image) => image.id));
    const received = result.images.filter((image) => !known.has(image.id));
    this.systemImages.set([...this.systemImages(), ...received]);
  }

  /** Починає отримання списку з першої сторінки (після збереження). */
  private async reload(): Promise<void> {
    this.systemImages.set([]);
    this.nextPage = 1;
    this.hasMore.set(true);
    this.firstLoaded.set(false);
    this.loadFailed.set(false);
    if (this.canView()) {
      await this.loadNextPage();
    }
  }

  protected openFileDialog(): void {
    this.fileInput()?.nativeElement.click();
  }

  /**
   * Видалення: зображення з системи переходить із першого списку в другий (воно
   * зникне на сервері, коли користувач збереже); додане користувачем просто
   * прибирається з третього.
   */
  protected remove(tile: Tile): void {
    if (this.saving()) {
      return;
    }
    if (tile.system) {
      const image = tile.system;
      this.systemImages.set(this.systemImages().filter((current) => current.id !== image.id));
      this.deletedImages.set([...this.deletedImages(), image]);
    } else if (tile.added) {
      const image = tile.added;
      URL.revokeObjectURL(image.url);
      this.addedImages.set(this.addedImages().filter((current) => current.key !== image.key));
    }
  }

  protected onFileChosen(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    input.value = '';
    this.typeError.set(false);
    if (!file || this.saving()) {
      return;
    }
    // Фільтр діалогу можна обійти: тип перевіряємо самі.
    if (!IMAGE_TYPES.includes(file.type)) {
      this.typeError.set(true);
      return;
    }
    // Додане користувачем — у третій список.
    this.addedImages.set([...this.addedImages(), { key: this.nextKey++, file, url: URL.createObjectURL(file) }]);
  }

  /**
   * Надсилає зміни на сервер: видалення за другим списком (зважає лише на право
   * `DeleteLoginPageImages`) і завантаження за третім (лише `CreateLoginPageImages`).
   * Це незалежні частини: невдача однієї не зупиняє іншу, а в межах частини на
   * першій невдачі зупиняємось. Кожен успішний запит прибирає свій запис зі
   * списку, тож повторне збереження не повторює зроблене. Якщо щось змінилося на
   * сервері — список із нього отримується заново. Повертає першу невдачу.
   */
  async save(): Promise<GallerySaveResult> {
    if (this.saving()) {
      return 'nothing';
    }
    const toDelete = this.canRemove() ? this.deletedImages() : [];
    const toUpload = this.canAdd() ? this.addedImages() : [];
    if (toDelete.length === 0 && toUpload.length === 0) {
      return 'nothing';
    }

    this.saving.set(true);
    this.typeError.set(false);
    let changed = false;
    let failure: RequestFailure | null = null;

    // Видалення (другий список): до першої невдачі.
    for (const image of toDelete) {
      const result = await this.service.delete(image.id);
      if (result !== 'deleted') {
        failure ??= result;
        break;
      }
      changed = true;
      this.deletedImages.set(this.deletedImages().filter((current) => current.id !== image.id));
    }

    // Завантаження (третій список): не залежить від того, чим скінчились видалення.
    for (const image of toUpload) {
      const result = await this.service.upload(image.file);
      if (typeof result === 'string') {
        failure ??= result;
        break;
      }
      changed = true;
      URL.revokeObjectURL(image.url);
      this.addedImages.set(this.addedImages().filter((current) => current.key !== image.key));
    }

    this.saving.set(false);
    if (changed) {
      await this.reload();
    }
    return failure ?? 'saved';
  }
}
