import { HttpErrorResponse } from '@angular/common/http';
import { TimeoutError } from 'rxjs';

import { toRequestFailure, toUploadFailure } from './request-failure';
import uk from '../../../assets/i18n/uk.json';

describe('toRequestFailure', () => {
  it.each([
    [400, 'invalid'],
    [401, 'unauthorized'],
    [403, 'forbidden'],
    [404, 'error'],
    [409, 'error'],
    [500, 'error'],
    [503, 'error'],
  ])('reads HTTP %i as %s', (status, failure) => {
    expect(toRequestFailure(new HttpErrorResponse({ status }))).toBe(failure);
  });

  it('reads a response that never came (status 0: network, CORS) as unreachable', () => {
    expect(toRequestFailure(new HttpErrorResponse({ status: 0 }))).toBe('unreachable');
  });

  it('reads a request that ran out of time as unreachable', () => {
    expect(toRequestFailure(new TimeoutError())).toBe('unreachable');
  });

  it.each([
    ['an ordinary error', new Error('boom')],
    ['a thrown text', 'boom'],
    ['nothing', undefined],
  ])('reads %s, which is neither an HTTP answer nor a timeout, as error: the server is not to blame', (_name, error) => {
    expect(toRequestFailure(error)).toBe('error');
  });
});

describe('toUploadFailure', () => {
  it('reads HTTP 413 as quotaExceeded: the storage of the server is full', () => {
    expect(toUploadFailure(new HttpErrorResponse({ status: 413 }))).toBe('quotaExceeded');
  });

  it.each([400, 401, 403, 500, 0])('reads HTTP %i as an ordinary request would', (status) => {
    expect(toUploadFailure(new HttpErrorResponse({ status }))).toBe(toRequestFailure(new HttpErrorResponse({ status })));
  });

  it('has a text for the full storage in every place that shows the failure of an upload', () => {
    const groups = [
      uk.settings.personal.result.avatar,
      uk.admin.result.logoFailed,
      uk.admin.result.imagesFailed,
      uk.login.ngo.logoUploadError,
    ] as Record<string, string>[];
    for (const group of groups) {
      expect(group['quotaExceeded']).toBeTruthy();
    }
  });
});
