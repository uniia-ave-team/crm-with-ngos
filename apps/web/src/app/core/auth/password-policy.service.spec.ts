import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { DEFAULT_PASSWORD_POLICY } from '../../shared/password-fields/password-rules';
import { PasswordPolicyService } from './password-policy.service';

describe('PasswordPolicyService', () => {
  const server = 'https://first.example:5065';
  const other = 'https://second.example:5065';
  const url = (origin: string) => `${origin}/api/v1/system/identity-options`;
  const options = { requiredLength: 12, requireDigit: false, requireNonAlphanumeric: true, requireUppercase: true, requireLowercase: false, requireUniqueEmail: true };
  let http: HttpTestingController;
  let service: PasswordPolicyService;

  beforeEach(() => {
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    service = TestBed.inject(PasswordPolicyService);
  });

  afterEach(() => vi.restoreAllMocks());

  it('gives the default policy at once and the one of the server when it answers', async () => {
    const policy = service.policy(server);
    expect(policy()).toEqual(DEFAULT_PASSWORD_POLICY);

    http.expectOne(url(server)).flush(options);
    await vi.waitFor(() =>
      expect(policy()).toEqual({ minLength: 12, requireLowercase: false, requireUppercase: true, requireDigit: false, requireSpecial: true }),
    );
  });

  it('asks every server once and keeps their policies apart', async () => {
    const first = service.policy(server);
    const second = service.policy(other);
    expect(service.policy(server)()).toBe(first());
    http.expectOne(url(server)).flush(options);
    http.expectOne(url(other)).flush({ ...options, requiredLength: 6 });
    await vi.waitFor(() => expect([first().minLength, second().minLength]).toEqual([12, 6]));

    service.policy(server);
    http.expectNone(url(server));
  });

  it('takes the length given as a string with a number, as the contract allows', async () => {
    const policy = service.policy(server);
    http.expectOne(url(server)).flush({ ...options, requiredLength: '10' });
    await vi.waitFor(() => expect(policy().minLength).toBe(10));
  });

  it.each([
    ['a length that is not a number', { ...options, requiredLength: 'ten' }],
    ['a length below one', { ...options, requiredLength: 0 }],
    ['a fractional length', { ...options, requiredLength: 7.5 }],
    ['a requirement that is not a boolean', { ...options, requireDigit: 'yes' }],
    ['nothing', null],
  ])('keeps the default policy for an answer with %s, and asks again next time', async (_name, body) => {
    const policy = service.policy(server);
    http.expectOne(url(server)).flush(body);
    await Promise.resolve();
    await vi.waitFor(() => expect(console.warn).toHaveBeenCalled());
    expect(policy()).toEqual(DEFAULT_PASSWORD_POLICY);

    service.policy(server);
    http.expectOne(url(server));
  });

  it('keeps the default policy when the server does not answer, and asks again next time', async () => {
    const policy = service.policy(server);
    http.expectOne(url(server)).error(new ProgressEvent('error'));
    await vi.waitFor(() => expect(console.warn).toHaveBeenCalled());
    expect(policy()).toEqual(DEFAULT_PASSWORD_POLICY);

    expect(service.policy(server)).toBeDefined();
    http.expectOne(url(server)).flush(options);
    await vi.waitFor(() => expect(policy().minLength).toBe(12));
  });
});
