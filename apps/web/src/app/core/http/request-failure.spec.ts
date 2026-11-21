import { HttpErrorResponse } from '@angular/common/http';
import { TimeoutError } from 'rxjs';

import { DEMO_SESSION_EXPIRED, readBlobErrorCode, readErrorCode, toRequestFailure } from './request-failure';

describe('toRequestFailure', () => {
  it.each([
    [400, 'invalid'],
    [401, 'unauthorized'],
    [403, 'forbidden'],
    [404, 'error'],
    [409, 'error'],
    [429, 'error'],
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

describe('readErrorCode (errorCode of the problem JSON)', () => {
  const failure = (status: number, error: unknown = null) => new HttpErrorResponse({ status, error });

  it('reads errorCode from the problem JSON of the response', () => {
    expect(readErrorCode(failure(401, { title: 'x', status: 401, errorCode: DEMO_SESSION_EXPIRED }))).toBe(DEMO_SESSION_EXPIRED);
  });

  it('reads it from a body that came as JSON text', () => {
    expect(readErrorCode(failure(403, JSON.stringify({ errorCode: 'DEMO_MODE_RESTRICTED' })))).toBe('DEMO_MODE_RESTRICTED');
  });

  it.each([
    ['a body without the field', { title: 'x' }],
    ['a code that is not a string', { errorCode: 42 }],
    ['text that is not JSON', '<html></html>'],
    ['no body', null],
  ])('gives null for %s', (_name, body) => {
    expect(readErrorCode(failure(403, body))).toBeNull();
  });

  it('gives null for something that is not an HTTP response', () => {
    expect(readErrorCode(new Error('boom'))).toBeNull();
  });
});

describe('readBlobErrorCode', () => {
  it('reads errorCode from a Blob body (a request for an image)', async () => {
    const body = new Blob([JSON.stringify({ errorCode: DEMO_SESSION_EXPIRED })], { type: 'application/problem+json' });
    expect(await readBlobErrorCode(body)).toBe(DEMO_SESSION_EXPIRED);
  });

  it('gives null for a Blob that is not JSON', async () => {
    expect(await readBlobErrorCode(new Blob(['\u0089PNG']))).toBeNull();
  });
});
