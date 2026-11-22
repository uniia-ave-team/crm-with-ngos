import { joinList } from './list-text';

describe('joinList', () => {
  it('puts the separator between the items and the last joiner before the last one', () => {
    expect(joinList(['A', 'B', 'C', 'D'], ', ', ' or ')).toBe('A, B, C or D');
  });

  it('joins two items with the last joiner only', () => {
    expect(joinList(['A', 'B'], ', ', ' and ')).toBe('A and B');
  });

  it('leaves a single item as it is and gives nothing for an empty list', () => {
    expect(joinList(['A'], ', ', ' or ')).toBe('A');
    expect(joinList([], ', ', ' or ')).toBe('');
  });

  it('takes the separator and the joiner as they are given, for a language with other signs', () => {
    expect(joinList(['A', 'B', 'C'], '、', '、')).toBe('A、B、C');
  });
});
