import { KNOWLEDGE_BASE_HOME, toKnowledgeBaseEntries, withHomePage } from './knowledge-base';

describe('toKnowledgeBaseEntries', () => {
  it('takes the folders with their items and the materials, in their order', () => {
    expect(
      toKnowledgeBaseEntries([
        { id: ' f1 ', type: 'folder', title: ' Розділ ', items: [{ id: 'p1', type: 'page', title: 'Документ' }] },
        { id: 't1', type: 'customtable', title: 'Таблиця' },
      ]),
    ).toEqual([
      { id: 'f1', type: 'folder', title: 'Розділ', items: [{ id: 'p1', type: 'page', title: 'Документ' }] },
      { id: 't1', type: 'customtable', title: 'Таблиця' },
    ]);
  });

  it.each([
    ['an unknown type (the server may add new ones)', { id: 'x', type: 'video', title: 'Х' }],
    ['a home page (only the client adds it)', { id: 'x', type: 'home', title: 'Х' }],
    ['a material without a title', { id: 'x', type: 'page', title: ' ' }],
    ['a material without an id', { type: 'page', title: 'Х' }],
    ['a folder without a title', { id: 'x', type: 'folder', title: '', items: [] }],
    ['a folder without an id', { type: 'folder', title: 'Розділ', items: [] }],
    ['not an object', 7],
  ])('drops %s', (_name, entry) => {
    expect(toKnowledgeBaseEntries([entry])).toEqual([]);
  });

  it('keeps only tables and pages in a folder, and an empty folder without items', () => {
    expect(
      toKnowledgeBaseEntries([
        {
          id: 'f1',
          type: 'folder',
          title: 'Розділ',
          items: [
            { id: 'f2', type: 'folder', title: 'Вкладена', items: [] },
            { id: 'p0', type: 'page' },
            { id: 'p1', type: 'page', title: 'Так' },
          ],
        },
        { id: 'f3', type: 'folder', title: 'Порожня' },
      ]),
    ).toEqual([
      { id: 'f1', type: 'folder', title: 'Розділ', items: [{ id: 'p1', type: 'page', title: 'Так' }] },
      { id: 'f3', type: 'folder', title: 'Порожня', items: [] },
    ]);
  });

  it('gives nothing for an answer that is not an array', () => {
    expect(toKnowledgeBaseEntries({ items: [] })).toEqual([]);
  });
});

describe('withHomePage', () => {
  it('puts the home page (the guide) first', () => {
    const table = { id: 't1', type: 'customtable', title: 'Таблиця' } as const;
    expect(withHomePage([table])).toEqual([KNOWLEDGE_BASE_HOME, table]);
    expect(KNOWLEDGE_BASE_HOME.type).toBe('home');
  });
});
