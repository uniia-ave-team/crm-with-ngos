import { HttpErrorResponse, HttpEvent, HttpHandlerFn, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable, catchError, defer, from, switchMap, throwError } from 'rxjs';

import { ConnectionService } from '../connection/connection.service';
import { AuthService } from './auth.service';

/**
 * Запити до API, яким токени не потрібні (і які не мають спричиняти оновлення
 * токенів чи вихід): вхід, обмін токенів, статус системи та створення
 * користувача (реєстрація першого адміна).
 */
const ANONYMOUS_REQUESTS: ReadonlyArray<{ method?: string; path: string }> = [
  { path: '/api/v1/auth/login' },
  { path: '/api/v1/auth/refresh-token' },
  { path: '/api/v1/system/status' },
  { method: 'POST', path: '/api/v1/users' },
];

/**
 * Додає до запитів до API заголовок `Authorization: Bearer <accessToken>` і
 * сам піклується про токени:
 * - якщо access-токен уже прострочений (за `exp`), спершу оновлює токени;
 * - якщо сервер відповів 401, оновлює токени й повторює запит один раз.
 * Якщо оновити токени не вдалося — вихід із системи (токени забуто,
 * перехід на сторінку входу).
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  // Сервіси інжектимо лише для запитів до /api: перевірка /health йде з
  // конструктора ConnectionService, і тягти його (через AuthService) сюди ще до
  // кінця конструювання — циклічна залежність.
  if (!isApiPath(req.url)) {
    return next(req);
  }
  const auth = inject(AuthService);
  const connection = inject(ConnectionService);

  if (!needsAuth(req, connection.serverUrl())) {
    return next(req);
  }

  return defer(() => from(currentToken(auth))).pipe(
    switchMap((token) =>
      send(req, next, token).pipe(
        catchError((error: unknown) => {
          if (!(error instanceof HttpErrorResponse) || error.status !== 401 || !token) {
            return throwError(() => error);
          }
          return from(recoverFromUnauthorized(auth, token)).pipe(
            switchMap((newToken) => (newToken ? send(req, next, newToken) : throwError(() => error))),
          );
        }),
      ),
    ),
  );
};

function isApiPath(url: string): boolean {
  try {
    return new URL(url, location.origin).pathname.startsWith('/api/');
  } catch {
    return false;
  }
}

function needsAuth(req: HttpRequest<unknown>, serverUrl: string): boolean {
  if (!req.url.startsWith(`${serverUrl}/api/`)) {
    return false;
  }
  const path = req.url.slice(serverUrl.length).split('?')[0];
  return !ANONYMOUS_REQUESTS.some((anonymous) => anonymous.path === path && (!anonymous.method || anonymous.method === req.method));
}

function send(req: HttpRequest<unknown>, next: HttpHandlerFn, token: string | null): Observable<HttpEvent<unknown>> {
  return next(token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req);
}

/** Токен для запиту: за потреби спершу оновлюємо прострочений; не вдалося — вихід. */
async function currentToken(auth: AuthService): Promise<string | null> {
  if (auth.hasTokens() && auth.accessTokenExpired() && (await auth.refresh()) !== 'ok') {
    auth.signOut();
    return null;
  }
  return auth.accessToken();
}

/**
 * Сервер відхилив токен `usedToken`. Якщо інший запит уже встиг оновити токени —
 * беремо новий; інакше оновлюємо самі. Не вдалося — вихід і `null`.
 */
async function recoverFromUnauthorized(auth: AuthService, usedToken: string): Promise<string | null> {
  const latest = auth.accessToken();
  if (latest && latest !== usedToken) {
    return latest;
  }
  if ((await auth.refresh()) === 'ok') {
    return auth.accessToken();
  }
  auth.signOut();
  return null;
}
