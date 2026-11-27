import { CustomTableField } from '../../core/custom-tables/custom-tables.service';
import { mergeFields } from './merge-fields';

describe('mergeFields', () => {
  const field = (id: string, name: string, key = id): CustomTableField => ({ id, name, key, dataType: 0, isRequired: false, order: 0 });
  const names = (fields: readonly CustomTableField[]) => fields.map((candidate) => `${candidate.name}:${candidate.key}`);

  const base = [field('a', 'Назва'), field('b', 'Сума')];

  it('keeps a column somebody else added meanwhile when only the description (no columns) changed here', () => {
    const fresh = [...base, field('c', 'Email')];
    expect(names(mergeFields(base, base, fresh).fields)).toEqual(['Назва:a', 'Сума:b', 'Email:c']);
  });

  it('puts the own renames, deletions and new columns onto the fresh structure, keeping the changes of others', () => {
    const local = [field('new:x', 'Перша', 'column_3'), field('a', 'Назва фонду'), field('new:y', 'Після назви', 'column_4')];
    // Тим часом інший: перейменував «Сума» (яку тут видалили) і додав «Email».
    const fresh = [field('a', 'Назва'), field('b', 'Сума, грн'), field('c', 'Email')];
    const { fields, keyChanges } = mergeFields(base, local, fresh);
    expect(names(fields)).toEqual(['Перша:column_3', 'Назва фонду:a', 'Після назви:column_4', 'Email:c']);
    expect(fields.map((candidate) => candidate.order)).toEqual([0, 1, 2, 3]);
    expect(keyChanges.size).toBe(0);
  });

  it('keeps the rename of somebody else when it was not renamed here, and skips a column somebody else deleted', () => {
    const local = [field('a', 'Назва'), field('b', 'Сума'), field('new:x', 'Нова', 'column_3')];
    const fresh = [field('a', 'Назва організації')];
    expect(names(mergeFields(base, local, fresh).fields)).toEqual(['Назва організації:a', 'Нова:column_3']);
  });

  it('gives a new column another key when somebody else took that key meanwhile', () => {
    const local = [...base, field('new:x', 'Нова', 'column_3')];
    const fresh = [...base, field('c', 'Email', 'column_3')];
    const { fields, keyChanges } = mergeFields(base, local, fresh);
    // Нова — одразу після свого сусіда зліва («Сума»), чужа «Email» — за нею.
    expect(names(fields)).toEqual(['Назва:a', 'Сума:b', 'Нова:column_4', 'Email:column_3']);
    expect([...keyChanges]).toEqual([['column_3', 'column_4']]);
  });
});
