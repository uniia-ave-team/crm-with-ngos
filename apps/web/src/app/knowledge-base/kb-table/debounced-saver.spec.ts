import { DebouncedSaver, SAVE_DELAY_MS } from './debounced-saver';

describe('DebouncedSaver', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it('waits two seconds after the last change, saving once, and is «saving» from the first change till the answer', async () => {
    expect(SAVE_DELAY_MS).toBe(2000);
    let finish!: (ok: boolean) => void;
    const save = vi.fn(() => new Promise<boolean>((resolve) => (finish = resolve)));
    const saver = new DebouncedSaver(save);
    expect(saver.state()).toBe('saved');

    saver.changed();
    expect(saver.state()).toBe('saving');
    await vi.advanceTimersByTimeAsync(1500);
    saver.changed();
    await vi.advanceTimersByTimeAsync(1500);
    expect(save).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(500);
    expect(save).toHaveBeenCalledTimes(1);
    expect(saver.state()).toBe('saving');

    finish(true);
    await vi.runAllTimersAsync();
    expect(saver.state()).toBe('saved');
  });

  it('saves one after another, and stays «saving» while a newer change waits', async () => {
    const resolvers: ((ok: boolean) => void)[] = [];
    const save = vi.fn(() => new Promise<boolean>((resolve) => resolvers.push(resolve)));
    const saver = new DebouncedSaver(save);
    saver.changed();
    await vi.advanceTimersByTimeAsync(2000);
    saver.changed();
    await vi.advanceTimersByTimeAsync(2000);
    // Друге збереження чекає, поки скінчиться перше.
    expect(save).toHaveBeenCalledTimes(1);
    resolvers[0](true);
    await vi.advanceTimersByTimeAsync(0);
    expect(save).toHaveBeenCalledTimes(2);
    expect(saver.state()).toBe('saving');
    resolvers[1](true);
    await vi.advanceTimersByTimeAsync(0);
    expect(saver.state()).toBe('saved');
  });

  it('says it failed, and tries again at once on retry', async () => {
    const save = vi.fn().mockResolvedValueOnce(false).mockResolvedValueOnce(true);
    const saver = new DebouncedSaver(save);
    saver.changed();
    await vi.advanceTimersByTimeAsync(2000);
    expect(saver.state()).toBe('failed');
    saver.retry();
    expect(saver.state()).toBe('saving');
    await vi.advanceTimersByTimeAsync(0);
    expect(save).toHaveBeenCalledTimes(2);
    expect(saver.state()).toBe('saved');
  });

  it('saves at once on flush and does nothing without changes', async () => {
    const save = vi.fn().mockResolvedValue(true);
    const saver = new DebouncedSaver(save);
    await saver.flush();
    expect(save).not.toHaveBeenCalled();
    saver.changed();
    await saver.flush();
    expect(save).toHaveBeenCalledTimes(1);
    await vi.advanceTimersByTimeAsync(5000);
    expect(save).toHaveBeenCalledTimes(1);
  });
});
