import { NgModule } from '@angular/core';
import { Routes, RouterModule } from '@angular/router';

import { NotificationsPage } from './notifications/notifications.page';
import { PasswordPage } from './password/password.page';
import { PersonalDataPage } from './personal-data/personal-data.page';
import { SettingsPage } from './settings.page';

// Вкладки живуть усередині сторінки налаштувань (її router-outlet), але мають
// власні адреси, на які можна вести звичайними посиланнями. Сама `/settings`
// відкриває першу вкладку.
const routes: Routes = [
  {
    path: '',
    component: SettingsPage,
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'personal-data' },
      { path: 'personal-data', component: PersonalDataPage },
      { path: 'password', component: PasswordPage },
      { path: 'notifications', component: NotificationsPage },
    ],
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class SettingsPageRoutingModule {}
