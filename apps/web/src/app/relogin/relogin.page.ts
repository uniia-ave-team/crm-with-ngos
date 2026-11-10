import { Component } from '@angular/core';

/**
 * Сторінка повторного входу (`/relogin`). Поки порожня. Без стражів і без
 * бічного меню, як `/login` і `/join`: відкривається за будь-якого стану сесії.
 */
@Component({
  selector: 'app-relogin',
  templateUrl: './relogin.page.html',
  standalone: false,
})
export class ReloginPage {}
