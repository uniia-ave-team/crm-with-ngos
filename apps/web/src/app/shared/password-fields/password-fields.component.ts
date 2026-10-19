import { Component, computed, input, output, viewChildren } from '@angular/core';
import { NgModel } from '@angular/forms';

import { PASSWORD_RULES } from './password-rules';

let nextId = 0;

/**
 * Поля нового пароля: пароль, умови до нього (які відмічаються під час
 * введення), повтор пароля й «паролі збігаються». Однакові на сторінці входу
 * (реєстрація адміністратора) і в налаштуваннях (зміна пароля).
 *
 * Компонент нічого не зберігає: значення приходять входами, зміни — подіями, а
 * помилки (ключі перекладу) підставляє батько після своєї перевірки. Чи
 * пароль достатньо складний, батько перевіряє через `isStrongPassword`.
 *
 *   <app-password-fields
 *     [password]="password()" [repeat]="repeat()"
 *     [passwordLabel]="'…' | translate" [repeatLabel]="'…' | translate"
 *     (passwordChange)="password.set($event)" (repeatChange)="repeat.set($event)"
 *   />
 *
 * Вигляд налаштовується CSS-змінними на самому елементі (усі необов'язкові):
 * `--password-fields-gap`, `--password-fields-radius`, `--password-fields-border`,
 * `--password-fields-background`, `--password-fields-accent`,
 * `--password-fields-min-height`, `--password-fields-label-font`.
 */
@Component({
  selector: 'app-password-fields',
  templateUrl: './password-fields.component.html',
  styleUrls: ['./password-fields.component.scss'],
  standalone: false,
})
export class PasswordFieldsComponent {
  readonly password = input('');
  readonly repeat = input('');
  /** Напис поля пароля: підказка в полі (або видимий напис над ним, див. `showLabels`) і `aria-label`. */
  readonly passwordLabel = input.required<string>();
  readonly repeatLabel = input.required<string>();
  /** Показувати написи над полями (замість підказок у полях). */
  readonly showLabels = input(false);
  /** Обовʼязкові поля: зірочка після напису (або після підказки) і `required` у полів. */
  readonly required = input(false);
  /** Помилки під полями — ключі перекладу (порожньо — помилки нема). */
  readonly passwordError = input<string | null>(null);
  readonly repeatError = input<string | null>(null);

  readonly passwordChange = output<string>();
  readonly repeatChange = output<string>();
  /** Enter у будь-якому з полів. */
  readonly submitted = output<void>();

  protected readonly idPrefix = `password-fields-${++nextId}`;

  /** Умови до пароля й чи виконано кожну (оновлюється під час введення). */
  protected readonly requirements = computed(() => {
    const password = this.password();
    return PASSWORD_RULES.map(({ key, icon, test }) => ({ key, icon, met: test(password) }));
  });

  /** Повтор пароля збігається з паролем (порожній повтор не рахується). */
  protected readonly matches = computed(() => !!this.repeat() && this.repeat() === this.password());

  private readonly models = viewChildren(NgModel);

  /**
   * Знімає з полів «торкнуто» й «змінено». Батько викликає це, коли сам очистив
   * значення (наприклад, після успішної зміни пароля): інакше порожнє
   * обовʼязкове поле, якого користувач уже торкався, підсвічується червоним
   * (Ionic малює `ion-touched` + `ion-invalid`), хоч помилки нема.
   */
  resetState(): void {
    for (const model of this.models()) {
      model.control.markAsUntouched();
      model.control.markAsPristine();
    }
  }

  protected onPassword(value: string | number | null | undefined): void {
    this.passwordChange.emit(value == null ? '' : String(value));
  }

  protected onRepeat(value: string | number | null | undefined): void {
    this.repeatChange.emit(value == null ? '' : String(value));
  }
}
