import { CustomTableField } from '../../core/custom-tables/custom-tables.service';
import { mergeFields } from './merge-fields';

describe('mergeFields', () => {
  const field = (id: string, name: string, order = 0): CustomTableField => ({ id, name, key: id, isRequired: false, order });
  const names = (fields: readonly CustomTableField[]) => fields.map((candidate) => `${candidate.name}:${candidate.id}`);

  const base = [field('a', 'Назва', 0), field('b', 'Сума', 1)];

  it('keeps a column somebody else added meanwhile when only the description (no columns) changed here', () => {
    const fresh = [...base, field('c', 'Email', 2)];
    expect(names(mergeFields(base, base, fresh))).toEqual(['Назва:a', 'Сума:b', 'Email:c']);
  });

  it('puts the own renames, deletions and new columns onto the fresh structure, keeping the changes of others', () => {
    const local = [field('new:1', 'Перша'), field('a', 'Назва фонду'), field('new:2', 'Після назви')];
    // Тим часом інший: перейменував «Сума» (яку тут видалили) і додав «Email».
    const fresh = [field('a', 'Назва', 0), field('b', 'Сума, грн', 1), field('c', 'Email', 2)];
    const fields = mergeFields(base, local, fresh);
    expect(names(fields)).toEqual(['Перша:new:1', 'Назва фонду:a', 'Після назви:new:2', 'Email:c']);
    expect(fields.map((candidate) => candidate.order)).toEqual([0, 1, 2, 3]);
  });

  it('keeps the rename of somebody else when it was not renamed here, and skips a column somebody else deleted', () => {
    const local = [field('a', 'Назва'), field('b', 'Сума'), field('new:1', 'Нова')];
    const fresh = [field('a', 'Назва організації')];
    expect(names(mergeFields(base, local, fresh))).toEqual(['Назва організації:a', 'Нова:new:1']);
  });

  it('follows the server positions even when the fields come out of order', () => {
    const fresh = [field('b', 'Сума', 1), field('c', 'Email', 2), field('a', 'Назва', 0)];
    expect(names(mergeFields(base, base, fresh))).toEqual(['Назва:a', 'Сума:b', 'Email:c']);
  });
});
