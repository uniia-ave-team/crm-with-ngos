import { Component } from '@angular/core';

interface NotificationField {
  readonly id: string;
  /** Ключ перекладу підпису. */
  readonly labelKey: string;
  /** За скільки повідомляти (значення початково обраного пункту). */
  readonly initial: string;
}

interface NotificationOption {
  readonly value: string;
  readonly labelKey: string;
}

/**
 * Вкладка «Сповіщення» (`/settings/notifications`). Поки лише вигляд форми:
 * початкові значення взято з макета, збереження запитів на сервер не робить.
 */
@Component({
  selector: 'app-settings-notifications',
  templateUrl: './notifications.page.html',
  styleUrls: ['../settings-section.scss'],
  standalone: false,
})
export class NotificationsPage {
  protected readonly fields: readonly NotificationField[] = [
    { id: 'notify-organization', labelKey: 'settings.notifications.organization', initial: 'week' },
    { id: 'notify-tasks-start', labelKey: 'settings.notifications.tasksStart', initial: 'month' },
    { id: 'notify-deadlines', labelKey: 'settings.notifications.deadlines', initial: 'fiveDays' },
    { id: 'notify-calendar', labelKey: 'settings.notifications.calendar', initial: 'dayBefore' },
  ];

  protected readonly options: readonly NotificationOption[] = [
    { value: 'none', labelKey: 'settings.notifications.when.none' },
    { value: 'dayBefore', labelKey: 'settings.notifications.when.dayBefore' },
    { value: 'fiveDays', labelKey: 'settings.notifications.when.fiveDays' },
    { value: 'week', labelKey: 'settings.notifications.when.week' },
    { value: 'month', labelKey: 'settings.notifications.when.month' },
  ];
}
