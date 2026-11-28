import { startUrl } from './start-url';

describe('startUrl', () => {
  it.each([
    [['Permissions.ViewProject', 'Permissions.ViewCustomTable', 'Permissions.ViewUser'], '/planner'],
    [['Permissions.ViewCustomTable', 'Permissions.ViewUser'], '/knowledge-base'],
    [['Permissions.ViewUser', 'Permissions.UpdateNgo'], '/colleagues'],
    [['Permissions.UpdateNgo', 'Permissions.ViewTask'], null],
    [[], null],
  ])('takes the first page by the rights: Planner, Knowledge base, Colleagues, and no further (%j)', (granted, url) => {
    expect(startUrl(granted)).toBe(url);
  });
});
