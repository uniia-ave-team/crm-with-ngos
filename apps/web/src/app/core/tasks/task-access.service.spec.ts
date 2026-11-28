import { Signal, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { ProjectsService } from '../projects/projects.service';
import { CurrentUserService } from '../users/current-user.service';
import { TaskAccessService } from './task-access.service';

describe('TaskAccessService', () => {
  function access(permissions: string[], visibility: 0 | 1 | 2 = 0) {
    (TestBed.inject(CurrentUserService) as { permissions: Signal<readonly string[]> }).permissions = signal(permissions);
    vi.spyOn(TestBed.inject(ProjectsService), 'state').mockReturnValue({ status: 'ready', projects: [{ id: 'p1', title: 'Явір', visibility }] });
    return TestBed.inject(TaskAccessService);
  }

  afterEach(() => vi.restoreAllMocks());

  it('lets an editor or a manager change and delete with the rights, never a viewer', () => {
    const service = access(['Permissions.UpdateTask', 'Permissions.DeleteTask']);
    expect(service.canEdit('p1')).toBe(false);
    service.setClaim('p1', 1);
    expect([service.canEdit('p1'), service.canDelete('p1')]).toEqual([true, true]);
    service.setClaim('p1', 2);
    expect(service.canEdit('p1')).toBe(true);
    service.setClaim('p1', 0);
    expect([service.canEdit('p1'), service.canDelete('p1')]).toEqual([false, false]);
    // Рівень доступу іншого проєкту не діє.
    service.setClaim('p2', 2);
    expect(service.canEdit('p1')).toBe(false);
  });

  it('asks each right on its own', () => {
    const service = access(['Permissions.UpdateTask']);
    service.setClaim('p1', 1);
    expect([service.canEdit('p1'), service.canDelete('p1')]).toEqual([true, false]);
  });

  it('lets a non-member change only in a public project', () => {
    const service = access(['Permissions.UpdateTask'], 2);
    service.setClaim('p1', null);
    expect(service.canEdit('p1')).toBe(true);
    TestBed.resetTestingModule();
    const other = access(['Permissions.UpdateTask'], 1);
    other.setClaim('p1', null);
    expect(other.canEdit('p1')).toBe(false);
  });
});
