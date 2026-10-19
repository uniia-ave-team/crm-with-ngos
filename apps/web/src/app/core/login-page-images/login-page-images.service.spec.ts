import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { ConnectionService } from '../connection/connection.service';
import { LoginPageImagesService } from './login-page-images.service';

describe('LoginPageImagesService', () => {
  let http: HttpTestingController;
  let service: LoginPageImagesService;
  let server: string;
  const listUrl = () => `${server}/api/v1/loginpageimages`;
  const body = (items: unknown, hasNextPage = false) => ({ items, totalCount: 0, pageNumber: 1, pageSize: 20, hasNextPage });

  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const connection = TestBed.inject(ConnectionService);
    http.expectOne(connection.apiHealthUrl());
    server = connection.serverUrl();
    service = TestBed.inject(LoginPageImagesService);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it('asks for exactly the page that was requested, with its size', async () => {
    const result = service.page(3, 20);
    const request = http.expectOne((r) => r.url === listUrl());
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('PageNumber')).toBe('3');
    expect(request.request.params.get('PageSize')).toBe('20');
    request.flush(body([]));
    await result;
  });

  it('does not ask for any other page by itself', async () => {
    const result = service.page(1, 20);
    http.expectOne((r) => r.url === listUrl()).flush(body([{ id: 'a', url: '/a.jpg' }], true));
    await result;
    http.expectNone((r) => r.url === listUrl());
  });

  it('returns the images with absolute links and whether there is a next page', async () => {
    const result = service.page(1, 20);
    http
      .expectOne((r) => r.url === listUrl())
      .flush(body([{ id: 'a', url: '/files/a.jpg' }, { id: 'b', url: 'https://cdn.example.org/b.jpg' }], true));
    expect(await result).toEqual({
      images: [
        { id: 'a', url: `${server}/files/a.jpg` },
        { id: 'b', url: 'https://cdn.example.org/b.jpg' },
      ],
      hasNextPage: true,
    });
  });

  it('says there is no next page when the server does not say so', async () => {
    const result = service.page(1, 20);
    http.expectOne((r) => r.url === listUrl()).flush({ items: [] });
    expect(await result).toEqual({ images: [], hasNextPage: false });
  });

  it('skips the items that have no id or no link', async () => {
    const result = service.page(1, 20);
    http
      .expectOne((r) => r.url === listUrl())
      .flush(body([{ id: 'a', url: '/a.jpg' }, { id: 'b' }, { url: '/c.jpg' }, { id: 'd', url: '  ' }, null, 7]));
    expect(await result).toEqual({ images: [{ id: 'a', url: `${server}/a.jpg` }], hasNextPage: false });
  });

  it('treats an answer without a list as an error', async () => {
    const result = service.page(1, 20);
    http.expectOne((r) => r.url === listUrl()).flush({ items: 'nope' });
    expect(await result).toBe('error');
  });

  it.each([
    [400, 'invalid'],
    [401, 'unauthorized'],
    [403, 'forbidden'],
    [500, 'error'],
  ])('returns the failure for HTTP %i (%s)', async (status, expected) => {
    const result = service.page(1, 20);
    http.expectOne((r) => r.url === listUrl()).flush({}, { status, statusText: 'x' });
    expect(await result).toBe(expected);
  });

  it('reports an unreachable server', async () => {
    const result = service.page(1, 20);
    http.expectOne((r) => r.url === listUrl()).error(new ProgressEvent('error'));
    expect(await result).toBe('unreachable');
  });

  describe('random', () => {
    const randomUrl = () => `${listUrl()}/random`;

    it('asks for the random image and returns it with an absolute link', async () => {
      const result = service.random();
      const request = http.expectOne(randomUrl());
      expect(request.request.method).toBe('GET');
      request.flush({ id: 'abc', url: '/files/login/abc.jpg' });
      expect(await result).toEqual({ id: 'abc', url: `${server}/files/login/abc.jpg` });
    });

    it('keeps a link that is already absolute', async () => {
      const result = service.random();
      http.expectOne(randomUrl()).flush({ id: 'abc', url: 'https://cdn.example.org/abc.jpg' });
      expect(await result).toEqual({ id: 'abc', url: 'https://cdn.example.org/abc.jpg' });
    });

    it('returns null when there are no images (404)', async () => {
      const result = service.random();
      http.expectOne(randomUrl()).flush({}, { status: 404, statusText: 'Not Found' });
      expect(await result).toBeNull();
    });

    it.each([[{}], [{ id: 'abc' }], [{ id: 'abc', url: '  ' }], [{ url: '/a.jpg' }], [null]])(
      'returns null for an answer without an image (%j)',
      async (body) => {
        const result = service.random();
        http.expectOne(randomUrl()).flush(body);
        expect(await result).toBeNull();
      },
    );

    it.each([500, 503, 403])('returns null when the server fails with HTTP %i (the page keeps its own image)', async (status) => {
      const result = service.random();
      http.expectOne(randomUrl()).flush({}, { status, statusText: 'x' });
      expect(await result).toBeNull();
    });

    it('returns null when the server does not answer', async () => {
      const result = service.random();
      http.expectOne(randomUrl()).error(new ProgressEvent('error'));
      expect(await result).toBeNull();
    });
  });

  describe('delete', () => {
    it('sends DELETE for the id (escaped) and treats 204 as deleted', async () => {
      const result = service.delete('123e4567-e89b-12d3-a456-426614174000');
      const request = http.expectOne(`${listUrl()}/123e4567-e89b-12d3-a456-426614174000`);
      expect(request.request.method).toBe('DELETE');
      request.flush(null, { status: 204, statusText: 'No Content' });
      expect(await result).toBe('deleted');
    });

    it('keeps the id as one path segment', async () => {
      const result = service.delete('a/b');
      http.expectOne(`${listUrl()}/a%2Fb`).flush(null, { status: 204, statusText: 'No Content' });
      expect(await result).toBe('deleted');
    });

    it('treats 404 (it is not there any more) as deleted', async () => {
      const result = service.delete('x');
      http.expectOne(`${listUrl()}/x`).flush({}, { status: 404, statusText: 'Not Found' });
      expect(await result).toBe('deleted');
    });

    it.each([
      [400, 'invalid'],
      [401, 'unauthorized'],
      [403, 'forbidden'],
      [500, 'error'],
    ])('returns the failure for HTTP %i (%s)', async (status, expected) => {
      const result = service.delete('x');
      http.expectOne(`${listUrl()}/x`).flush({}, { status, statusText: 'x' });
      expect(await result).toBe(expected);
    });

    it('reports an unreachable server', async () => {
      const result = service.delete('x');
      http.expectOne(`${listUrl()}/x`).error(new ProgressEvent('error'));
      expect(await result).toBe('unreachable');
    });
  });

  describe('upload', () => {
    const file = () => new File([new Uint8Array(5)], 'photo.png', { type: 'image/png' });

    it('posts the file as multipart/form-data in the field "file" and returns the id of the created image', async () => {
      const source = file();
      const result = service.upload(source);
      const request = http.expectOne(listUrl());
      expect(request.request.method).toBe('POST');
      expect(request.request.body).toBeInstanceOf(FormData);
      const sent = (request.request.body as FormData).get('file') as File;
      expect(sent).toBeInstanceOf(File);
      expect(sent.name).toBe('photo.png');
      request.flush('123e4567-e89b-12d3-a456-426614174000', { status: 201, statusText: 'Created' });
      expect(await result).toEqual({ id: '123e4567-e89b-12d3-a456-426614174000' });
    });

    it('does not set the content type by itself (the browser adds the boundary)', async () => {
      const result = service.upload(file());
      const request = http.expectOne(listUrl());
      expect(request.request.headers.has('Content-Type')).toBe(false);
      request.flush('id', { status: 201, statusText: 'Created' });
      await result;
    });

    it.each([
      [400, 'invalid'],
      [401, 'unauthorized'],
      [403, 'forbidden'],
      [500, 'error'],
    ])('returns the failure for HTTP %i (%s)', async (status, expected) => {
      const result = service.upload(file());
      http.expectOne(listUrl()).flush({}, { status, statusText: 'x' });
      expect(await result).toBe(expected);
    });

    it('reports an unreachable server', async () => {
      const result = service.upload(file());
      http.expectOne(listUrl()).error(new ProgressEvent('error'));
      expect(await result).toBe('unreachable');
    });
  });
});
