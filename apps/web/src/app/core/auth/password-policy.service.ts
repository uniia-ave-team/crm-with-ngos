import { HttpClient } from '@angular/common/http';
import { Injectable, Signal, inject, signal } from '@angular/core';
import { firstValueFrom, timeout } from 'rxjs';

import { IdentityOptionsDto } from '../../api/generated/models';
import { DEFAULT_PASSWORD_POLICY, PasswordPolicy } from '../../shared/password-fields/password-rules';
import { Untrusted } from '../http/api-contract';
import { toRequestFailure } from '../http/request-failure';
import { REQUEST_TIMEOUT_MS } from '../http/timeouts';

const API_IDENTITY_OPTIONS_PATH = '/api/v1/system/identity-options';

/** Що відомо про умови одного сервера. */
interface PolicyEntry {
  readonly policy: ReturnType<typeof signal<PasswordPolicy>>;
  /** `loading` — запит іде; `loaded` — умови сервера отримано; `failed` — не вдалося, спитаємо знову. */
  status: 'loading' | 'loaded' | 'failed';
}

/**
 * Умови до пароля на кожному сервері (`GET <сервер>/api/v1/system/identity-options`, без
 * авторизації). Їх показують і перевіряють форми нового пароля: реєстрація адміністратора на
 * `/login`, реєстрація за запрошенням (`/join`) і зміна пароля в налаштуваннях.
 */
@Injectable({ providedIn: 'root' })
export class PasswordPolicyService {
  private readonly http = inject(HttpClient);
  private readonly entries = new Map<string, PolicyEntry>();

  /**
   * Умови сервера `server` (origin) сигналом. Поки відповіді нема — і якщо отримати її не
   * вдалося — це `DEFAULT_PASSWORD_POLICY`; коли сервер відповість, сигнал зміниться. Запит
   * один на сервер; невдалий повториться при наступному виклику для цього сервера.
   */
  policy(server: string): Signal<PasswordPolicy> {
    let entry = this.entries.get(server);
    if (!entry) {
      entry = { policy: signal(DEFAULT_PASSWORD_POLICY), status: 'failed' };
      this.entries.set(server, entry);
    }
    if (entry.status === 'failed') {
      void this.load(server, entry);
    }
    return entry.policy.asReadonly();
  }

  private async load(server: string, entry: PolicyEntry): Promise<void> {
    entry.status = 'loading';
    const url = `${server}${API_IDENTITY_OPTIONS_PATH}`;
    try {
      const body = await firstValueFrom(this.http.get<Untrusted<IdentityOptionsDto>>(url).pipe(timeout(REQUEST_TIMEOUT_MS)));
      const policy = toPasswordPolicy(body);
      if (!policy) {
        console.warn('[PasswordPolicyService] Умови до пароля не відповідають контракту', body);
        entry.status = 'failed';
        return;
      }
      entry.policy.set(policy);
      entry.status = 'loaded';
    } catch (error) {
      console.warn(`[PasswordPolicyService] Не вдалося отримати умови до пароля (${toRequestFailure(error)})`, error);
      entry.status = 'failed';
    }
  }
}

/**
 * Умови з відповіді сервера; `null` — відповідь не за контрактом (довжина не ціле число від 1,
 * хоч одна вимога не булева). Довжину контракт дозволяє й рядком із числом.
 */
function toPasswordPolicy(body: Untrusted<IdentityOptionsDto>): PasswordPolicy | null {
  const { requiredLength, requireLowercase, requireUppercase, requireDigit, requireNonAlphanumeric } = body ?? {};
  const minLength = typeof requiredLength === 'string' && requiredLength.trim() ? Number(requiredLength) : requiredLength;
  if (typeof minLength !== 'number' || !Number.isInteger(minLength) || minLength < 1) {
    return null;
  }
  const flags = [requireLowercase, requireUppercase, requireDigit, requireNonAlphanumeric];
  if (!flags.every((flag) => typeof flag === 'boolean')) {
    return null;
  }
  return {
    minLength,
    requireLowercase: requireLowercase as boolean,
    requireUppercase: requireUppercase as boolean,
    requireDigit: requireDigit as boolean,
    requireSpecial: requireNonAlphanumeric as boolean,
  };
}
