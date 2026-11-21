import { CLOSE_TIMEOUT_MS, ClosingWaiters } from './closing-waiters';

describe('ClosingWaiters', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  /** Чи виконався проміс (після того, як мікрозадачі прокрутились). */
  async function isDone(promise: Promise<void>): Promise<boolean> {
    let done = false;
    void promise.then(() => (done = true));
    await vi.advanceTimersByTimeAsync(0);
    return done;
  }

  it('resolves every wait when the window has closed', async () => {
    const waiters = new ClosingWaiters();
    const first = waiters.wait();
    const second = waiters.wait();
    expect(await isDone(first)).toBe(false);

    waiters.settle();
    expect(await isDone(first)).toBe(true);
    expect(await isDone(second)).toBe(true);
  });

  it('resolves a wait anyway after the timeout, when the window does not report that it has closed', async () => {
    const wait = new ClosingWaiters().wait();
    await vi.advanceTimersByTimeAsync(CLOSE_TIMEOUT_MS - 1);
    expect(await isDone(wait)).toBe(false);
    await vi.advanceTimersByTimeAsync(1);
    expect(await isDone(wait)).toBe(true);
  });

  it('does not touch the waits that come after a settle: they wait for the next closing', async () => {
    const waiters = new ClosingWaiters();
    waiters.settle();
    const later = waiters.wait();
    expect(await isDone(later)).toBe(false);
    waiters.settle();
    expect(await isDone(later)).toBe(true);
  });
});
