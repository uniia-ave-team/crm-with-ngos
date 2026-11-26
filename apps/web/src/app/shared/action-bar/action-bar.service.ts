import { Injectable, Signal, signal } from '@angular/core';

/** Кнопка смуги дій: дія (`id`), ключ підпису (підказка й назва для екранних читачів), іконка. */
export interface ActionBarItem<T extends string = string> {
  readonly id: T;
  readonly labelKey: string;
  readonly icon: string;
  /** Кнопку видно, але натиснути не можна (нема прав чи дія ще не готова). */
  readonly disabled?: boolean;
  /**
   * Кнопка-кружок кольору замість іконки: колір (`#rrggbb`) чи `null` — пунктирне порожнє коло (кольору
   * нема). Без цього поля — іконка.
   */
  readonly swatch?: string | null;
}

/** Що показує смуга: назва для екранних читачів, групи кнопок і що робити з вибраною дією. */
export interface ActionBarContent<T extends string = string> {
  readonly labelKey: string;
  /** Групи кнопок (рознесено по всій ширині смуги). */
  readonly groups: readonly (readonly ActionBarItem<T>[])[];
  /** Натиснули кнопку: дія й сама кнопка (до неї можна привʼязати спливаюче вікно). */
  readonly choose: (action: T, button: HTMLElement) => void;
}

/**
 * Зміст смуги дій знизу сторінки (`app-action-bar`): вид показує свої кнопки (`show`), коли щось
 * вибрано (наприклад, клітинку таблиці), і ховає (`hide`), коли вибір знято. Смуга одна на
 * сторінку, зміст має власника (`owner` — зазвичай сам вид): `hide` чужого власника нічого не
 * ховає (інший вид уже показав своє).
 */
@Injectable({ providedIn: 'root' })
export class ActionBarService {
  private readonly contentSignal = signal<ActionBarContent | null>(null);
  readonly content: Signal<ActionBarContent | null> = this.contentSignal.asReadonly();
  private owner: object | null = null;

  show<T extends string>(owner: object, content: ActionBarContent<T>): void {
    this.owner = owner;
    this.contentSignal.set(content as unknown as ActionBarContent);
  }

  hide(owner: object): void {
    if (this.owner === owner) {
      this.owner = null;
      this.contentSignal.set(null);
    }
  }
}
