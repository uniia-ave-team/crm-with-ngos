import { HttpErrorResponse, HttpEvent, HttpHandlerFn, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { Injector, inject } from '@angular/core';
import { Observable, catchError, defer, from, switchMap, throwError } from 'rxjs';

import { ConnectionService } from '../connection/connection.service';
import { DemoNoticeService } from '../demo/demo-notice.service';
import {
  DEMO_MODE_RESTRICTED,
  DEMO_QUOTA_EXCEEDED,
  DEMO_SESSION_EXPIRED,
  readBlobErrorCode,
  readErrorCode,
} from '../http/request-failure';
import { AuthService, RefreshResult } from './auth.service';

/**
 * Запити до API, яким токени не потрібні (і які не мають спричиняти оновлення
 * токенів чи вихід): вхід, вихід, обмін токенів, статус системи, умови до пароля,
 * створення користувача (реєстрація першого адміна) та запрошення — їх відкриває людина,
 * яка ще не має акаунта, а якщо в браузері є чужий активний акаунт, він тут ні до чого.
 */
const ANONYMOUS_REQUESTS: ReadonlyArray<{ method?: string; path: string }> = [
  // Вихід: заголовок збирає сам AuthService із токенів, узятих до очищення
  // сховища; перехоплювач не має ні додавати токен, ні оновлювати його.
  { path: '/api/v1/auth/logout' },
  { path: '/api/v1/auth/login' },
  { path: '/api/v1/auth/refresh-token' },
  { path: '/api/v1/system/status' },
  // Умови до пароля: публічні, їх показують і тим, хто ще не має акаунта.
  { method: 'GET', path: '/api/v1/system/identity-options' },
  // Випадкове зображення форми входу: публічне, на сторінці входу токена може й не бути.
  { method: 'GET', path: '/api/v1/loginpageimages/random' },
  { method: 'POST', path: '/api/v1/users' },
  { method: 'GET', path: '/api/v1/invitations/validate' },
  { method: 'POST', path: '/api/v1/invitations/accept' },
];

/**
 * Додає до запитів до API заголовок `Authorization: Bearer <accessToken>` і
 * сам піклується про токени:
 * - якщо access-токен уже прострочений (за `exp`), спершу оновлює токени;
 * - якщо сервер відповів 401, оновлює токени й повторює запит один раз.
 * Якщо оновити токени не вдалося — сесія акаунта завершується (`AuthService.expireSession`):
 * акаунт лишається в памʼяті з позначкою й перестає бути активним, а запит завершується
 * помилкою, не доходячи до сервера. На сторінку повторного входу веде `SessionService`.
 *
 * Запит належить акаунту, що був активним у мить, коли запит створили. Якщо, поки
 * оновлюються токени, активним стане інший акаунт, запит однаково піде з токеном свого
 * акаунта (він адресований його серверу), а невдача запишеться саме йому.
 *
 * Тут же, в одному місці, обробляються помилки демо-режиму (`errorCode` у тілі відповіді):
 * - 401 `DEMO_SESSION_EXPIRED` — копії демо-даних уже нема: токени не оновлюються, акаунт
 *   забувається (`AuthService.endDemoSession`; на сторінку входу з поясненням веде
 *   `SessionService`). Те саме, якщо так відповіло оновлення токенів перед запитом;
 * - 403 `DEMO_MODE_RESTRICTED` / `DEMO_QUOTA_EXCEEDED` — спливне повідомлення
 *   (`DemoNoticeService`), також для запитів без токена; сама помилка йде далі тому, хто робив
 *   запит.
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
  // Сервіс сповіщень береться лише тоді, коли він справді потрібен (помилка демо-режиму).
  const injector = inject(Injector);
  const notifyDemo = catchError<HttpEvent<unknown>, Observable<never>>((error: unknown) =>
    withErrorCode<never>(error, (code) => {
      if (code === DEMO_MODE_RESTRICTED) {
        void injector.get(DemoNoticeService).show('restricted');
      } else if (code === DEMO_QUOTA_EXCEEDED) {
        void injector.get(DemoNoticeService).show('quotaExceeded');
      }
      return throwError(() => error);
    }),
  );

  if (!needsAuth(req, connection.serverUrl())) {
    return next(req).pipe(notifyDemo);
  }
  const accountId = auth.sessionId();

  return defer(() => from(currentToken(auth, accountId, req.url))).pipe(
    switchMap((token) =>
      send(req, next, token).pipe(
        catchError((error: unknown) => {
          if (!(error instanceof HttpErrorResponse) || error.status !== 401 || !token) {
            return throwError(() => error);
          }
          return withErrorCode(error, (code) => {
            if (code === DEMO_SESSION_EXPIRED) {
              // Демо-сеанс завершився: оновлення токенів нічого не дасть.
              auth.endDemoSession(accountId);
              return throwError(() => error);
            }
            return from(recoverFromUnauthorized(auth, accountId, token)).pipe(
              switchMap((newToken) => (newToken ? send(req, next, newToken) : throwError(() => error))),
            );
          });
        }),
      ),
    ),
    notifyDemo,
  );
};

/**
 * Передає в `handle` код помилки (`errorCode`) невдалої відповіді. Тіло-`Blob` (запит
 * зображення) читається асинхронно, решта — одразу.
 */
function withErrorCode<T>(error: unknown, handle: (code: string | null) => Observable<T>): Observable<T> {
  const body = error instanceof HttpErrorResponse ? (error.error as unknown) : null;
  if (typeof Blob !== 'undefined' && body instanceof Blob) {
    return from(readBlobErrorCode(body)).pipe(switchMap(handle));
  }
  return handle(readErrorCode(error));
}

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

/**
 * Токен акаунта для запиту: прострочений спершу оновлюємо. Не вдалося — сесія акаунта
 * завершується, а запит не надсилається: без токена сервер однаково його відхилить. Той, хто
 * робив запит, отримує таку саму помилку, яку дав би сервер (див. `refreshFailure`).
 */
async function currentToken(auth: AuthService, accountId: string | null, url: string): Promise<string | null> {
  if (auth.accessTokenOf(accountId) && auth.accessTokenExpired(accountId)) {
    const refreshed = await auth.refresh(accountId);
    if (refreshed !== 'ok') {
      auth.expireSession(accountId, refreshed);
      throw refreshFailure(refreshed, url);
    }
  }
  return auth.accessTokenOf(accountId);
}

/**
 * Помилка запиту, якого не надіслали, бо не вдалося оновити токени: сервер не відповів —
 * «відповіді нема» (статус 0), інакше — 401, як відповів би сервер на запит без токена.
 */
function refreshFailure(refreshed: RefreshResult, url: string): HttpErrorResponse {
  return refreshed === 'unreachable'
    ? new HttpErrorResponse({ status: 0, statusText: 'Unknown Error', url })
    : new HttpErrorResponse({ status: 401, statusText: 'Unauthorized', url });
}

/**
 * Сервер відхилив токен `usedToken` акаунта. Якщо інший запит уже встиг оновити токени —
 * беремо новий; інакше оновлюємо самі. Не вдалося — сесія акаунта завершується, `null`.
 */
async function recoverFromUnauthorized(auth: AuthService, accountId: string | null, usedToken: string): Promise<string | null> {
  const latest = auth.accessTokenOf(accountId);
  if (latest && latest !== usedToken) {
    return latest;
  }
  const refreshed = await auth.refresh(accountId);
  if (refreshed === 'ok') {
    return auth.accessTokenOf(accountId);
  }
  auth.expireSession(accountId, refreshed);
  return null;
}
