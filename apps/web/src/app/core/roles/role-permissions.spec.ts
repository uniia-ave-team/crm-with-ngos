import { PERMISSIONS, Permission } from '../auth/permissions';
import { ALWAYS_GRANTED_PERMISSIONS, UNUSED_PERMISSIONS, PERMISSION_GROUPS, PermissionGroup, canGrantGroup, claimsOfGroups, diffClaims } from './role-permissions';

describe('role-permissions', () => {
  const group = (...rights: string[]): PermissionGroup => ({ id: 'g', permissions: rights.map((right) => `Permissions.${right}` as Permission) });

  describe('canGrantGroup (a group can be given only with ALL of its permissions)', () => {
    it('is true when the user has the only permission of the group', () => {
      expect(canGrantGroup(group('ViewUser'), ['Permissions.ViewUser'])).toBe(true);
    });

    it('is false when the user has not the only permission of the group', () => {
      expect(canGrantGroup(group('ViewUser'), ['Permissions.UpdateUser'])).toBe(false);
      expect(canGrantGroup(group('ViewUser'), [])).toBe(false);
    });

    it('is true for a merged group when the user has every permission of it, and more', () => {
      const merged = group('CreateUser', 'UpdateUser');
      expect(canGrantGroup(merged, ['Permissions.CreateUser', 'Permissions.UpdateUser'])).toBe(true);
      expect(canGrantGroup(merged, ['Permissions.DeleteUser', 'Permissions.UpdateUser', 'Permissions.CreateUser'])).toBe(true);
    });

    it('is false for a merged group when even one permission is missing', () => {
      const merged = group('CreateUser', 'UpdateUser');
      expect(canGrantGroup(merged, ['Permissions.CreateUser'])).toBe(false);
      expect(canGrantGroup(merged, ['Permissions.UpdateUser'])).toBe(false);
    });

    it('does not take a permission without the prefix or with another case for the same one', () => {
      expect(canGrantGroup(group('ViewUser'), ['ViewUser'])).toBe(false);
      expect(canGrantGroup(group('ViewUser'), ['permissions.viewuser'])).toBe(false);
    });
  });

  it('has thirteen groups with unique ids and no permission in two groups', () => {
    expect(PERMISSION_GROUPS).toHaveLength(13);
    expect(new Set(PERMISSION_GROUPS.map((candidate) => candidate.id)).size).toBe(13);
    const all = PERMISSION_GROUPS.flatMap((candidate) => candidate.permissions);
    expect(new Set(all).size).toBe(all.length);
  });

  it('leaves no permission of the server out: each one is in a group, is given to every role, is CreateNgo of the first user or is not used yet', () => {
    const placed = [
      ...PERMISSION_GROUPS.flatMap((candidate) => candidate.permissions),
      ...ALWAYS_GRANTED_PERMISSIONS,
      PERMISSIONS.createNgo,
      ...UNUSED_PERMISSIONS,
    ];
    expect([...placed].sort()).toEqual(Object.values(PERMISSIONS).sort());
  });

  describe('the permissions without settings (they are given to every role)', () => {
    it('are only the viewing of the table templates (not the creating of an organization nor the removed ViewNgo)', () => {
      expect(ALWAYS_GRANTED_PERMISSIONS).toEqual(['Permissions.ViewTableTemplate']);
    });

    it('are not among the permissions that can be chosen', () => {
      const chosen = PERMISSION_GROUPS.flatMap((candidate) => candidate.permissions);
      for (const right of ALWAYS_GRANTED_PERMISSIONS) {
        expect(chosen).not.toContain(right);
      }
    });
  });

  describe('claimsOfGroups', () => {
    it('gives only the permissions without settings when nothing is chosen', () => {
      expect(claimsOfGroups(new Set())).toEqual(ALWAYS_GRANTED_PERMISSIONS);
    });

    it('gives all the permissions of the chosen groups in the order of the groups, then the permissions without settings', () => {
      // Вибір у зворотному порядку: порядок результату — за порядком груп.
      const claims = claimsOfGroups(new Set(['editUsers', 'viewUsers']));
      expect(claims).toEqual(['Permissions.ViewUser', 'Permissions.CreateUser', 'Permissions.UpdateUser', ...ALWAYS_GRANTED_PERMISSIONS]);
    });

    it('takes every permission of a merged group', () => {
      // «Керування таблицями» — підпункт, тож діє лише разом із «Переглядом таблиць» (одне право).
      const claims = claimsOfGroups(new Set(['viewTables', 'manageTables']));
      expect(claims).toContain('Permissions.CreateCustomTable');
      expect(claims).toContain('Permissions.DeleteCustomRecord');
      expect(claims).toHaveLength(1 + 7 + ALWAYS_GRANTED_PERMISSIONS.length);
    });

    it('gives a sub-item only together with its item ("Managing roles" needs "Viewing colleagues")', () => {
      expect(claimsOfGroups(new Set(['manageRoles']))).toEqual(ALWAYS_GRANTED_PERMISSIONS);
      expect(claimsOfGroups(new Set(['editUsers']))).toEqual(ALWAYS_GRANTED_PERMISSIONS);
      expect(claimsOfGroups(new Set(['manageRoles', 'viewUsers']))).toEqual([
        'Permissions.ViewUser',
        'Permissions.ManageRole',
        'Permissions.AssignRoleToUser',
        ...ALWAYS_GRANTED_PERMISSIONS,
      ]);
    });

    it('ignores an unknown id and has no repeats', () => {
      const claims = claimsOfGroups(new Set(['nonexistent', 'viewUsers', 'viewUsers']));
      expect(claims).toEqual(['Permissions.ViewUser', ...ALWAYS_GRANTED_PERMISSIONS]);
      expect(new Set(claims).size).toBe(claims.length);
    });
  });

  describe('diffClaims (what to add to the role and what to take away from it)', () => {
    const ALL = PERMISSION_GROUPS.flatMap((candidate) => candidate.permissions);
    const P = (...rights: string[]) => rights.map((right) => `Permissions.${right}`);

    it('has nothing to change when the form is left as it was (apart from the permissions without settings)', () => {
      const original = P('ViewUser', 'CreateUser', 'UpdateUser', ...ALWAYS_GRANTED_PERMISSIONS.map((right) => right.replace('Permissions.', '')));
      expect(diffClaims(original, P('ViewUser', 'CreateUser', 'UpdateUser'), ALL)).toEqual({ add: [], remove: [] });
    });

    it('adds the permissions of a group that was switched on, and only those the role has not', () => {
      const original = [...P('CanResetPassword'), ...ALWAYS_GRANTED_PERMISSIONS];
      expect(diffClaims(original, P('CanResetPassword', 'ViewUser'), ALL)).toEqual({ add: P('ViewUser'), remove: [] });
    });

    it('takes away the permissions of a group that was switched off', () => {
      const original = [...P('ViewUser', 'CanResetPassword'), ...ALWAYS_GRANTED_PERMISSIONS];
      expect(diffClaims(original, P('CanResetPassword'), ALL)).toEqual({ add: [], remove: P('ViewUser') });
    });

    it('fixes a partial group: switched off takes its leftover away, switched on adds the missing permissions', () => {
      const original = [...P('CreateUser'), ...ALWAYS_GRANTED_PERMISSIONS];
      expect(diffClaims(original, [], ALL)).toEqual({ add: [], remove: P('CreateUser') });
      expect(diffClaims(original, P('CreateUser', 'UpdateUser'), ALL)).toEqual({ add: P('UpdateUser'), remove: [] });
    });

    it('does not touch a group the user cannot give (he has not all of its permissions), whichever way it is', () => {
      const original = [...P('CreateUser'), ...ALWAYS_GRANTED_PERMISSIONS];
      // У користувача лише одне з двох прав групи: група для нього недоступна.
      expect(diffClaims(original, [], P('CreateUser'))).toEqual({ add: [], remove: [] });
      expect(diffClaims([...ALWAYS_GRANTED_PERMISSIONS], P('CreateUser', 'UpdateUser'), P('CreateUser'))).toEqual({ add: [], remove: [] });
    });

    it('leaves the permissions that are in no group as they are', () => {
      const original = [...P('CreateNgo', 'Unknown'), ...ALWAYS_GRANTED_PERMISSIONS];
      expect(diffClaims(original, [], ALL)).toEqual({ add: [], remove: [] });
    });

    it('adds the permissions without settings when the role has not them, once', () => {
      expect(diffClaims([], [], ALL)).toEqual({ add: [...ALWAYS_GRANTED_PERMISSIONS], remove: [] });
      expect(diffClaims([...ALWAYS_GRANTED_PERMISSIONS], [], ALL).add).toEqual([]);
    });

    it('lists the added permissions in the order of the groups, the permissions without settings last', () => {
      const change = diffClaims([], P('CanResetPassword', 'ViewUser'), ALL);
      expect(change.add).toEqual([...P('ViewUser', 'CanResetPassword'), ...ALWAYS_GRANTED_PERMISSIONS]);
    });
  });
});
