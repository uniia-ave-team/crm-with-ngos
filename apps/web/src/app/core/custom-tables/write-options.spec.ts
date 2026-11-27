import { writeRequestOptions } from './write-options';

describe('writeRequestOptions', () => {
  afterEach(() => delete (document as unknown as Record<string, unknown>)['visibilityState']);

  it('sends a change with keepalive only while the page is hidden (it may be closing)', () => {
    expect(writeRequestOptions()).toEqual({ keepalive: false });
    Object.defineProperty(document, 'visibilityState', { value: 'hidden', configurable: true });
    expect(writeRequestOptions()).toEqual({ keepalive: true });
  });
});
