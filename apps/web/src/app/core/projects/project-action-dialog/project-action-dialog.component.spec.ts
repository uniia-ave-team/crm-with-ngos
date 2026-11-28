import { CUSTOM_ELEMENTS_SCHEMA, Signal, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { Router, provideRouter } from '@angular/router';
import { provideIonicAngular } from '@ionic/angular';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../../assets/i18n/uk.json';
import { ConfirmDialogComponent } from '../../../shared/confirm-dialog/confirm-dialog.component';
import { ModalComponent } from '../../../shared/modal/modal.component';
import { CurrentUserService } from '../../users/current-user.service';
import { ProjectFormComponent } from '../project-form/project-form.component';
import { TopicFormComponent } from '../topic-form/topic-form.component';
import { ProjectsService } from '../projects.service';
import { ProjectActionDialogComponent } from './project-action-dialog.component';

describe('ProjectActionDialogComponent', () => {
  const project = { id: 'p1', title: 'Явір' };
  const details = {
    title: 'Явір',
    description: null,
    visibility: 0 as const,
    startDate: '2026-03-20',
    endDate: null,
  };

  /** Вікно за запитом дії (з правами `permissions`); відкривається насправді (з анімацією Ionic). */
  async function choose(action: 'edit' | 'delete' | 'access' | 'topic', permissions = ['Permissions.UpdateProject', 'Permissions.DeleteProject']) {
    TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideIonicAngular(), provideRouter([]), provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    (
      TestBed.inject(CurrentUserService) as {
        permissions: Signal<readonly string[]>;
      }
    ).permissions = signal(permissions);
    const service = TestBed.inject(ProjectsService);
    const get = vi.spyOn(service, 'get').mockResolvedValue(details);
    const update = vi.spyOn(service, 'update').mockResolvedValue('done');
    const remove = vi.spyOn(service, 'remove').mockResolvedValue('done');
    const createTopic = vi.spyOn(service, 'createTopic').mockResolvedValue('done');
    const fixture = TestBed.createComponent(ProjectActionDialogComponent);
    fixture.detectChanges();
    const modal = fixture.debugElement.query(By.directive(ModalComponent)).componentInstance as ModalComponent;
    const closeModal = vi.spyOn(modal, 'close').mockResolvedValue();
    service.requestAction(action, project);
    fixture.detectChanges();
    const child = <T>(type: new (...args: never[]) => T) => fixture.debugElement.query(By.directive(type))?.componentInstance as T | undefined;
    return { fixture, get, update, remove, createTopic, closeModal, child, modal, service };
  }

  // Вікно, що ще відкривається, не прибирати: Ionic доробить відкриття вже без нього («framework delegate is missing»).
  afterEach(async () => {
    await new Promise((resolve) => setTimeout(resolve, 50));
    document.querySelectorAll('ion-modal').forEach((modal) => modal.remove());
  });

  it('«Edit» loads the project into the same form as «New project» and saves the changes by update', async () => {
    const { fixture, get, update, closeModal, child } = await choose('edit');
    const form = await vi.waitFor(
      () => {
        fixture.detectChanges();
        const found = child(ProjectFormComponent);
        expect(found).toBeDefined();
        return found!;
      },
      { timeout: 3000 },
    );
    expect(get).toHaveBeenCalledWith('p1');
    expect(form.initial()).toEqual(details);
    expect(form.submitLabelKey()).toBe('planner.dialogs.edit.save');

    // Нічого не змінили — запиту нема, вікно закривається.
    form.submitted.emit(details);
    expect(update).not.toHaveBeenCalled();
    expect(closeModal).toHaveBeenCalledTimes(1);

    form.submitted.emit({ ...details, title: 'Явір 2', visibility: 2 });
    await vi.waitFor(() =>
      expect(update).toHaveBeenCalledWith('p1', {
        ...details,
        title: 'Явір 2',
        visibility: 2,
      }),
    );
    expect(closeModal).toHaveBeenCalledTimes(2);
  });

  it('«Delete» asks with the name and the loss of everything in the project, deletes and leaves its planner when it is open', async () => {
    const { fixture, remove, closeModal, child } = await choose('delete');
    vi.spyOn(TestBed.inject(Router), 'url', 'get').mockReturnValue('/planner/projects/p1');
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
    const confirm = await vi.waitFor(
      () => {
        fixture.detectChanges();
        const found = child(ConfirmDialogComponent);
        expect(found).toBeDefined();
        return found!;
      },
      { timeout: 3000 },
    );
    expect(confirm.message()).toBe(uk.planner.dialogs.delete.replace('{{title}}', 'Явір'));
    confirm.confirm.emit();
    await vi.waitFor(() => expect(remove).toHaveBeenCalledWith('p1'));
    await vi.waitFor(() => expect(navigate).toHaveBeenCalledWith('/planner'));
    expect(closeModal).toHaveBeenCalled();
  });

  it('names the access window «View access» without Permissions.UpdateProject, and takes the request', async () => {
    const { fixture, modal, service } = await choose('access', []);
    fixture.detectChanges();
    expect(modal.label()).toBe(uk.planner.actions.viewAccess);
    expect(service.actionRequest()).toBeNull();
  });

  it('does not open «Edit» without Permissions.UpdateProject', async () => {
    const { get } = await choose('edit', []);
    expect(get).not.toHaveBeenCalled();
  });

  it('«New column» asks the title and creates the column, closing the window; a taken title keeps it with the reason', async () => {
    const { fixture, createTopic, closeModal, child, modal } = await choose('topic', ['Permissions.UpdateProject']);
    const form = await vi.waitFor(
      () => {
        fixture.detectChanges();
        const found = child(TopicFormComponent);
        expect(found).toBeDefined();
        return found!;
      },
      { timeout: 3000 },
    );
    expect(modal.label()).toBe(uk.planner.actions.topic);
    // Довша назва не вміщається в заголовок колонки (два рядки).
    expect(document.getElementById('topic-title')?.getAttribute('maxlength')).toBe('32');
    form.submitted.emit('До виконання');
    await vi.waitFor(() => expect(createTopic).toHaveBeenCalledWith('p1', 'До виконання'));
    await vi.waitFor(() => expect(closeModal).toHaveBeenCalled());

    createTopic.mockResolvedValue('exists');
    closeModal.mockClear();
    form.submitted.emit('До виконання');
    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(form.error()).toBe('planner.dialogs.failure.exists');
    });
    expect(closeModal).not.toHaveBeenCalled();
  });

  it('does not open «New column» without Permissions.UpdateProject', async () => {
    const { fixture, child } = await choose('topic', []);
    fixture.detectChanges();
    expect(child(TopicFormComponent)).toBeUndefined();
  });
});
