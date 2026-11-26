import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { IonButtons, IonContent, IonHeader, IonMenuButton, IonTitle, IonToolbar } from '@ionic/angular';
import { TranslatePipe } from '@ngx-translate/core';

import { KnowledgeBaseService } from '../core/knowledge-base/knowledge-base.service';
import { ActionBarComponent } from '../shared/action-bar/action-bar.component';
import { ActionBarService } from '../shared/action-bar/action-bar.service';

/**
 * Вкладка «База знань» (`/knowledge-base`). Дерево розділу — у другій панелі; основний блок —
 * один із видів за адресою (`knowledge-base.routes.ts`): «Посібник», таблиця, сторінка чи менеджер
 * шаблонів. Сторінка лише підвантажує зміст (`KnowledgeBaseService`, спільний з деревом) і
 * дає відступи; що показати, вирішує вид у `router-outlet`.
 *
 * Шапка з кнопкою меню й назвою вкладки лише на телефоні й планшеті (до 992px), де другої
 * панелі не видно; на десктопі заголовок дає сам вид. Знизу змісту — смуга дій (`app-action-bar`,
 * `slot="fixed"`), яку показує вид через `ActionBarService`.
 */
@Component({
  selector: 'app-knowledge-base',
  templateUrl: './knowledge-base.page.html',
  styleUrls: ['./knowledge-base.page.scss'],
  imports: [ActionBarComponent, IonButtons, IonContent, IonHeader, IonMenuButton, IonTitle, IonToolbar, RouterOutlet, TranslatePipe],
})
export class KnowledgeBasePage {
  protected readonly actionBar = inject(ActionBarService);

  constructor() {
    void inject(KnowledgeBaseService).load();
  }
}
