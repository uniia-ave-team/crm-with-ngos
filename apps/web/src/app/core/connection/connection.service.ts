import { Injectable, Signal, signal } from '@angular/core';

const SERVER_ADDRESS_STORAGE_KEY = 'yavir.serverAddress';

/**
 * Дані підключення до інсталяції (наразі — лише адреса сервера, введена на
 * сторінці входу). Синглтон на весь застосунок: адресу треба знати будь-якій
 * сторінці, що звертається до API, а не лише самій сторінці входу.
 *
 * Поки що сервіс лише зберігає адресу. Не робить: не валідує й не перевіряє
 * доступність сервера, не автовизначає його в мережі, не відповідає за сам
 * вхід (сесію/токен) — усе це прийде пізніше, коли з'явиться apps/web/src/app/api
 * (див. apps/web/README.md, розділ «Ще не зроблено»). Сторінка входу поки
 * що взагалі не викликає setServerAddress — поле вводу там суто візуальне.
 */
@Injectable({ providedIn: 'root' })
export class ConnectionService {
  private readonly serverAddressSignal = signal<string | null>(this.readServerAddress());
  readonly serverAddress: Signal<string | null> = this.serverAddressSignal.asReadonly();

  setServerAddress(address: string): void {
    const trimmed = address.trim();
    this.serverAddressSignal.set(trimmed || null);
    this.persistServerAddress(trimmed);
  }

  clearServerAddress(): void {
    this.serverAddressSignal.set(null);
    this.persistServerAddress('');
  }

  private persistServerAddress(value: string): void {
    try {
      if (value) {
        localStorage.setItem(SERVER_ADDRESS_STORAGE_KEY, value);
      } else {
        localStorage.removeItem(SERVER_ADDRESS_STORAGE_KEY);
      }
    } catch {
      // Сховище може бути недоступне (приватний режим тощо).
    }
  }

  private readServerAddress(): string | null {
    try {
      return localStorage.getItem(SERVER_ADDRESS_STORAGE_KEY);
    } catch {
      return null;
    }
  }
}
