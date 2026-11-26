import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { ConnectionService } from '../connection/connection.service';
import { TableTemplatesService } from './table-templates.service';

describe('TableTemplatesService', () => {
  beforeEach(() => {
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
  });

  afterEach(() => vi.restoreAllMocks());

  it('sends the columns in their order as text fields that are not required, and gives the new id', async () => {
    const service = TestBed.inject(TableTemplatesService);
    const creating = service.create({
      name: 'Донори',
      description: null,
      fields: [
        { name: '<b>Назва</b>', key: 'name' },
        { name: 'Сума', key: 'amount' },
      ],
    });
    const request = TestBed.inject(HttpTestingController).expectOne(`${TestBed.inject(ConnectionService).serverUrl()}/api/v1/tabletemplates`);
    expect(request.request.method).toBe('POST');
    // Тип і обовʼязковість сервер ще вимагає: усе — рядки, необовʼязкові.
    expect(request.request.body).toEqual({
      name: 'Донори',
      description: null,
      fields: [
        { name: '<b>Назва</b>', key: 'name', dataType: 0, isRequired: false, order: 0 },
        { name: 'Сума', key: 'amount', dataType: 0, isRequired: false, order: 1 },
      ],
    });
    request.flush('tpl9');
    expect(await creating).toEqual({ id: 'tpl9' });
  });

  it('gives the reason when the server did not create the template', async () => {
    const creating = TestBed.inject(TableTemplatesService).create({ name: 'Донори', description: null, fields: [{ name: 'Назва', key: 'name' }] });
    TestBed.inject(HttpTestingController)
      .expectOne(`${TestBed.inject(ConnectionService).serverUrl()}/api/v1/tabletemplates`)
      .flush(null, { status: 400, statusText: 'Bad Request' });
    expect(await creating).toBe('invalid');
  });

  it('saves a template with all its columns (a new one with no id) as text fields, and deletes one', async () => {
    const service = TestBed.inject(TableTemplatesService);
    const http = TestBed.inject(HttpTestingController);
    const url = `${TestBed.inject(ConnectionService).serverUrl()}/api/v1/tabletemplates/tpl2`;
    const updating = service.update('tpl2', {
      name: 'Своя',
      description: null,
      fields: [
        { id: 'c1', name: 'Назва', key: 'name' },
        { id: null, name: 'Новий стовпчик', key: 'column_2' },
      ],
    });
    const put = http.expectOne(url);
    expect(put.request.method).toBe('PUT');
    expect(put.request.body).toEqual({
      name: 'Своя',
      description: null,
      fields: [
        { id: 'c1', name: 'Назва', key: 'name', dataType: 0, isRequired: false, order: 0 },
        { id: null, name: 'Новий стовпчик', key: 'column_2', dataType: 0, isRequired: false, order: 1 },
      ],
    });
    put.flush(null, { status: 204, statusText: 'No Content' });
    expect(await updating).toBe('done');

    const removing = service.remove('tpl2');
    const del = http.expectOne(url);
    expect(del.request.method).toBe('DELETE');
    del.flush(null, { status: 400, statusText: 'Bad Request' });
    expect(await removing).toBe('invalid');
  });
});
