import { CustomRecord } from '../../core/custom-tables/custom-records.service';
import { LocalRecord, mergeRecords } from './merge-records';

describe('mergeRecords', () => {
  const local = (key: string, data: Record<string, unknown>, serverId: string | null = key): LocalRecord => ({ key, serverId, data });
  const server = (id: string, data: Record<string, unknown>): CustomRecord => ({ id, data });
  const none = { dirtyKeys: new Set<string>(), focused: null };

  it('keeps the same objects when nothing changed', () => {
    const records = [local('r1', { name: 'А' }), local('r2', { name: 'Б' })];
    expect(mergeRecords(records, [server('r1', { name: 'А' }), server('r2', { name: 'Б' })], none)).toBe(records);
  });

  it('changes only the changed values of the changed record', () => {
    const first = local('r1', { name: 'А', terms: 'січень' });
    const second = local('r2', { name: 'Б', terms: null });
    const merged = mergeRecords([first, second], [server('r1', { name: 'А', terms: 'лютий' }), server('r2', { name: 'Б', terms: null })], none);
    expect(merged[0]).not.toBe(first);
    expect(merged[0].data).toEqual({ name: 'А', terms: 'лютий' });
    expect(merged[1]).toBe(second);
  });

  it('adds new records and drops the deleted ones, in the order of the server', () => {
    const merged = mergeRecords([local('r1', { name: 'А' }), local('r2', { name: 'Б' })], [server('r3', { name: 'В' }), server('r1', { name: 'А' })], none);
    expect(merged.map((record) => record.key)).toEqual(['r3', 'r1']);
  });

  it('leaves the rows with unsaved changes and the cell with the cursor as they are', () => {
    const dirty = local('r1', { name: 'Моє' });
    const focused = local('r2', { name: 'Пишу', terms: 'січень' });
    const merged = mergeRecords([dirty, focused], [server('r1', { name: 'Чуже' }), server('r2', { name: 'Чуже', terms: 'лютий' })], {
      dirtyKeys: new Set(['r1']),
      focused: { key: 'r2', field: 'name' },
    });
    expect(merged[0]).toBe(dirty);
    expect(merged[1].data).toEqual({ name: 'Пишу', terms: 'лютий' });
  });

  it('keeps the new row not yet created on the server at the end, and a dirty row deleted elsewhere', () => {
    const unsaved = local('kb-table-empty-row', { name: 'Новий' }, null);
    const dirtyDeleted = local('r2', { name: 'Змінений' });
    const merged = mergeRecords([local('r1', { name: 'А' }), dirtyDeleted, unsaved], [server('r1', { name: 'А' })], {
      dirtyKeys: new Set(['r2']),
      focused: null,
    });
    expect(merged.map((record) => record.key)).toEqual(['r1', 'r2', 'kb-table-empty-row']);
  });
});
