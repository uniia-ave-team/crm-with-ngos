import { Injectable, inject, signal } from '@angular/core';

import { PERMISSIONS } from '../auth/permissions';
import { ProjectClaim, ProjectsService } from '../projects/projects.service';
import { CurrentUserService } from '../users/current-user.service';

/**
 * Хто може змінювати й видаляти завдання проєкту: потрібне право ролі (`UpdateTask` чи
 * `DeleteTask`) і доступ у проєкті — учасник із «Редагуванням» чи «Керуванням»; переглядач — ні.
 * Не учасник — лише в публічному проєкті (сервер дозволяє там редагування всім). Рівень доступу
 * користувача в проєкті (`setClaim`) дає планер, коли завантажить учасників; поки його нема — ні.
 */
@Injectable({ providedIn: 'root' })
export class TaskAccessService {
  private readonly currentUser = inject(CurrentUserService);
  private readonly projects = inject(ProjectsService);

  /** Рівень доступу користувача в проєкті (`null` — не учасник). */
  private readonly claim = signal<{ readonly projectId: string; readonly claim: ProjectClaim | null } | null>(null);

  /** Рівень доступу користувача в проєкті `projectId` (`null` — не учасник). */
  setClaim(projectId: string, claim: ProjectClaim | null): void {
    this.claim.set({ projectId, claim });
  }

  /** Чи можна змінювати завдання проєкту (`UpdateTask` і доступ). */
  canEdit(projectId: string): boolean {
    return this.currentUser.permissions().includes(PERMISSIONS.updateTask) && this.canChange(projectId);
  }

  /** Чи можна видаляти завдання проєкту (`DeleteTask` і доступ). */
  canDelete(projectId: string): boolean {
    return this.currentUser.permissions().includes(PERMISSIONS.deleteTask) && this.canChange(projectId);
  }

  private canChange(projectId: string): boolean {
    const known = this.claim();
    if (known?.projectId !== projectId) {
      return false;
    }
    if (known.claim !== null) {
      return known.claim >= 1;
    }
    return this.projects.state().projects.find((project) => project.id === projectId)?.visibility === 2;
  }
}
