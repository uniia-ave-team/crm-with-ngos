import { HttpErrorResponse } from '@angular/common/http';
import { TimeoutError } from 'rxjs';

import { toRequestFailure } from './request-failure';

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
