import { Component, effect, inject, input, signal, untracked } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { IonIcon, MenuController } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

import { KNOWLEDGE_BASE_PAGE_TYPES, KnowledgeBaseFolder, KnowledgeBasePage, isKnowledgeBaseFolder, knowledgeBaseLink } from '../../knowledge-base/knowledge-base';
import { KbItemMenuComponent } from '../../knowledge-base/kb-item-menu/kb-item-menu.component';
import { KnowledgeBaseService } from '../../knowledge-base/knowledge-base.service';
import { LayoutSection } from '../layout.service';

interface MenuItem {
  /** Ключ перекладу напису. */
  readonly labelKey: string;
  readonly icon: string;
  /** Адреса, на яку веде пункт. */
  readonly url: string;
  /** Функція запланована, але ще не готова: пункт у розмітці, проте прихований класом `planned`. */
  readonly planned?: boolean;
}

/**
 * Вміст другої (контекстної) панелі — дерево розділу. Показується лише на
 * сторінках, для яких `LayoutService` знайшов розділ (`LayoutSection`) за поточним
 * маршрутом: knowledge-base, planner, settings.
 *
 * Дерево «Бази знань» — зі змісту, який підвантажує `KnowledgeBaseService` (той самий, з якого
 * сторінка будує «Посібник»): головна сторінка — посилання на `/knowledge-base` («Головна» з
 * мовного файлу), групи — папка з назвою розділу й сторінки в ньому, окремі сторінки — пункти з іконкою
 * за типом. Сторінок і таблиць ще нема, тож пункти, крім «Головна», поки не є посиланнями. При
 * наведенні (чи фокусі з клавіатури) розділ і сторінка виділяються підложкою, а біля назви
 * зʼявляються три крапки (меню дій ще нема); назва обрізається трикрапкою, тож панель не ширшає.
 * Клік по назві групи згортає її, повторний — розгортає (стан за `id` папки запамʼятовується в
 * `localStorage`, `COLLAPSED_FOLDERS_KEY`, і переживає перезавантаження); при наведенні на групу її папка
 * змінюється на стрілку (розгорнута — вниз, згорнута — вправо). У плашці папки при наведенні
 * перед трьома крапками є ще «+»: додати сторінку в цю папку (`KnowledgeBaseService.requestInsert`
 * з її `id` і назвою). Три крапки відкривають меню дій (`app-kb-item-menu`). На телефоні й планшеті
 * (до 992px) у рядку лише три крапки — завжди видимі й більші, а «Додати в папку» — у їхньому меню.
 * Між групами більший відступ, ніж між пунктами.
 *
 * Меню «Мої налаштування» — пункти з мовних файлів, кожен веде на власну
 * адресу всередині сторінки налаштувань (`/settings/<вкладка>`).
 */
@Component({
  selector: 'app-secondary-panel',
  templateUrl: './secondary-panel.component.html',
  styleUrls: ['./secondary-panel.component.scss'],
  imports: [IonIcon, KbItemMenuComponent, NgTemplateOutlet, RouterLink, RouterLinkActive, TranslatePipe],
})
export class SecondaryPanelComponent {
  readonly section = input.required<LayoutSection>();

  protected readonly settingsItems: readonly MenuItem[] = [
    { labelKey: 'settings.menu.personal', icon: 'person-outline', url: '/settings/personal-data' },
    { labelKey: 'settings.menu.password', icon: 'key-outline', url: '/settings/password' },
    { labelKey: 'settings.menu.notifications', icon: 'notifications-outline', url: '/settings/notifications', planned: true },
  ];

  protected readonly knowledgeBaseService = inject(KnowledgeBaseService);
  private readonly menu = inject(MenuController);
  /** Зміст «Бази знань» (той самий, з якого сторінка будує «Посібник»). */
  protected readonly knowledgeBase = this.knowledgeBaseService.state;
  /** Адреса виду таблиці чи сторінки. */
  protected readonly link = knowledgeBaseLink;

  /**
   * Перехід із дерева бази знань: на телефоні бокове меню — шторка поверх виду, тож її закриваємо
   * (на десктопі меню — частина розмітки, і `close` нічого не робить).
   */
  protected closeMenu(): void {
    void this.menu.close('secondary');
  }

  /** Іконка матеріалу за типом (шаблон рядка не знає типу контексту). */
  protected iconOf(page: KnowledgeBasePage): string {
    return KNOWLEDGE_BASE_PAGE_TYPES[page.type].icon;
  }
  protected readonly isGroup = isKnowledgeBaseFolder;

  /** Згорнуті папки дерева бази знань (`id`); запамʼятовуються в `localStorage`. */
  private readonly collapsedGroups = signal<ReadonlySet<string>>(readCollapsedFolders());

  constructor() {
    // Зміст бази знань підвантажується, щойно панель показує її дерево.
    effect(() => {
      if (this.section() === 'knowledge-base') {
        untracked(() => void this.knowledgeBaseService.load());
      }
    });
  }

  protected isCollapsed(id: string): boolean {
    return this.collapsedGroups().has(id);
  }

  /** Клік по назві папки: розгорнута згортається, згорнута розгортається; стан запамʼятовується. */
  protected toggleGroup(id: string): void {
    const collapsed = new Set(this.collapsedGroups());
    if (!collapsed.delete(id)) {
      collapsed.add(id);
    }
    this.collapsedGroups.set(collapsed);
    writeCollapsedFolders(collapsed);
  }

  /** «+» у плашці папки: додати сторінку в цю папку (див. `KnowledgeBaseService.requestInsert`). */
  protected addToFolder(group: KnowledgeBaseFolder): void {
    this.knowledgeBaseService.requestInsert({ folder: { id: group.id, title: group.title } });
  }
}

/** Ключ `localStorage` зі згорнутими папками бази знань (масив їхніх `id`). */
export const COLLAPSED_FOLDERS_KEY = 'yavir.knowledgeBase.collapsedFolders';

/** Згорнуті папки зі сховища; сховище недоступне чи запис зіпсований — жодної. */
function readCollapsedFolders(): ReadonlySet<string> {
  try {
    const value: unknown = JSON.parse(localStorage.getItem(COLLAPSED_FOLDERS_KEY) ?? '[]');
    return new Set(Array.isArray(value) ? value.filter((id): id is string => typeof id === 'string') : []);
  } catch {
    return new Set();
  }
}

/** Записує згорнуті папки; сховище недоступне чи переповнене — стан живе до перезавантаження. */
function writeCollapsedFolders(ids: ReadonlySet<string>): void {
  try {
    if (ids.size === 0) {
      localStorage.removeItem(COLLAPSED_FOLDERS_KEY);
    } else {
      localStorage.setItem(COLLAPSED_FOLDERS_KEY, JSON.stringify([...ids]));
    }
  } catch {
    // Приватний режим чи переповнене сховище: згортання однаково працює до перезавантаження.
  }
}
