import { TestBed } from '@angular/core/testing';

import { DrawableImage, ThumbnailFit, ThumbnailService } from './thumbnail.service';

/** Сервіс, у якому зображення не декодується браузером, а задається тестом. */
class TestThumbnailService extends ThumbnailService {
  image: DrawableImage | Error = { width: 200, height: 100, source: {} as CanvasImageSource };
  readonly requested: string[] = [];

  protected override async load(src: string): Promise<DrawableImage> {
    this.requested.push(src);
    if (this.image instanceof Error) {
      throw this.image;
    }
    return this.image;
  }
}

describe('ThumbnailService', () => {
  let service: TestThumbnailService;
  let draw: ReturnType<typeof vi.fn>;
  let toDataUrl: ReturnType<typeof vi.fn>;
  let canvas: { width: number; height: number; getContext: ReturnType<typeof vi.fn>; toDataURL: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    draw = vi.fn();
    toDataUrl = vi.fn(() => 'data:image/webp;base64,AAAA');
    canvas = { width: 0, height: 0, getContext: vi.fn(() => ({ drawImage: draw })), toDataURL: toDataUrl };
    const create = document.createElement.bind(document);
    vi.spyOn(document, 'createElement').mockImplementation(((tag: string) => (tag === 'canvas' ? canvas : create(tag))) as typeof document.createElement);
    TestBed.configureTestingModule({ providers: [{ provide: ThumbnailService, useClass: TestThumbnailService }] });
    service = TestBed.inject(ThumbnailService) as TestThumbnailService;
  });

  afterEach(() => vi.restoreAllMocks());

  it('gives a data link of the image in WebP, drawn on a square of the given size', async () => {
    const result = await service.create('blob:http://localhost/1', 96, 'cover');

    expect(result).toBe('data:image/webp;base64,AAAA');
    expect(service.requested).toEqual(['blob:http://localhost/1']);
    expect(canvas.width).toBe(96);
    expect(canvas.height).toBe(96);
    expect(toDataUrl).toHaveBeenCalledWith('image/webp', expect.any(Number));
  });

  describe.each<[ThumbnailFit, [number, number, number, number]]>([
    // Зображення 200×100 у квадраті 96: cover — висота 96, ширина 192, обрізання по центру;
    // contain — ширина 96, висота 48, поля по центру.
    ['cover', [-48, 0, 192, 96]],
    ['contain', [0, 24, 96, 48]],
  ])('the fit "%s"', (fit, [x, y, width, height]) => {
    it('draws the image in the right place and size', async () => {
      await service.create('blob:x', 96, fit);
      expect(draw).toHaveBeenCalledWith(expect.anything(), x, y, width, height);
    });
  });

  it('draws a tall image by cover so that it fills the square (cut at the top and the bottom)', async () => {
    service.image = { width: 100, height: 300, source: {} as CanvasImageSource };
    await service.create('blob:x', 96, 'cover');
    expect(draw).toHaveBeenCalledWith(expect.anything(), 0, -96, 96, 288);
  });

  it('is null when the image cannot be read', async () => {
    service.image = new Error('broken');
    expect(await service.create('blob:x', 96, 'cover')).toBeNull();
  });

  it('is null when there is no drawing context', async () => {
    canvas.getContext.mockReturnValue(null);
    expect(await service.create('blob:x', 96, 'cover')).toBeNull();
  });

  it('is null for an image with no size', async () => {
    service.image = { width: 0, height: 0, source: {} as CanvasImageSource };
    expect(await service.create('blob:x', 96, 'cover')).toBeNull();
  });

  it('is null when the browser gives something that is not an image link', async () => {
    toDataUrl.mockReturnValue('data:,');
    expect(await service.create('blob:x', 96, 'cover')).toBeNull();
  });
});
