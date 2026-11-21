import { Component, input } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { IonIcon } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

import { LayoutSection } from '../layout.service';

interface TreeLink {
  readonly label: string;
  readonly icon: string;
}

interface MenuItem {
  /** Ключ перекладу напису. */
  readonly labelKey: string;
  readonly icon: string;
  /** Адреса, на яку веде пункт. */
  readonly url: string;
  /** Функція запланована, але ще не готова: пункт у розмітці, проте прихований класом `planned`. */
  readonly planned?: boolean;
}

interface TreeGroup {
  readonly label: string;
  readonly icon: string;
  readonly items: readonly TreeLink[];
}

/**
 * Вміст другої (контекстної) панелі — дерево розділу. Показується лише на
 * сторінках, для яких `LayoutService` знайшов розділ (`LayoutSection`) за поточним
 * маршрутом: knowledge-base, planner, settings.
 *
 * Дерево «Бази знань» — статичний макет для дизайну; бекенду документів ще
 * немає (див. apps/web/README.md, розділ «Ще не зроблено»), тому пункти,
 * крім «Головна», поки не є посиланнями.
 *
 * Меню «Мої налаштування» — пункти з мовних файлів, кожен веде на власну
 * адресу всередині сторінки налаштувань (`/settings/<вкладка>`).
 */
@Component({
  selector: 'app-secondary-panel',
  templateUrl: './secondary-panel.component.html',
  styleUrls: ['./secondary-panel.component.scss'],
  imports: [IonIcon, RouterLink, RouterLinkActive, TranslatePipe],
})
export class SecondaryPanelComponent {
  readonly section = input.required<LayoutSection>();

  protected readonly settingsItems: readonly MenuItem[] = [
    { labelKey: 'settings.menu.personal', icon: 'person-outline', url: '/settings/personal-data' },
    { labelKey: 'settings.menu.password', icon: 'key-outline', url: '/settings/password' },
    { labelKey: 'settings.menu.notifications', icon: 'notifications-outline', url: '/settings/notifications', planned: true },
  ];

  protected readonly knowledgeBaseGroups: readonly TreeGroup[] = [
    {
      label: 'Політики',
      icon: 'document-outline',
      items: [
        { label: 'Про членство', icon: 'document-outline' },
        { label: 'Про запобігання конфлікту інтересів', icon: 'document-outline' },
        { label: 'Про дистанційну роботу', icon: 'document-outline' },
        { label: 'Про порядок закупівель', icon: 'document-outline' },
      ],
    },
    {
      label: 'Меморандуми',
      icon: 'document-outline',
      items: [
        { label: 'Про співпрацю', icon: 'document-outline' },
        { label: 'Про співпрацю', icon: 'document-outline' },
      ],
    },
    {
      label: 'Можливості',
      icon: 'document-outline',
      items: [
        { label: 'Ресурси', icon: 'document-outline' },
        { label: 'Підписки', icon: 'document-outline' },
      ],
    },
  ];
}
