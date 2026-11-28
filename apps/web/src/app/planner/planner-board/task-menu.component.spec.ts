import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideIonicAngular } from '@ionic/angular';
import { By } from '@angular/platform-browser';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { ProjectTask, TasksService } from '../../core/tasks/tasks.service';
import { ConfirmDialogComponent } from '../../shared/confirm-dialog/confirm-dialog.component';
import { ModalComponent } from '../../shared/modal/modal.component';
import { PopoverComponent } from '../../shared/popover/popover.component';
import { TaskMenuComponent } from './task-menu.component';

describe('TaskMenuComponent', () => {
  // Вікно, що ще відкривається, не прибирати: Ionic доробить відкриття вже без нього.
  afterEach(async () => {
    await new Promise((resolve) => setTimeout(resolve, 50));
    document.querySelectorAll('ion-modal, ion-popover').forEach((element) => element.remove());
    vi.restoreAllMocks();
  });

  it('offers «Details», «Edit» only when editing is allowed and «Delete» only when deleting is', () => {
    TestBed.configureTestingModule({ schemas: [CUSTOM_ELEMENTS_SCHEMA], providers: [provideIonicAngular(), provideTranslateService({ lang: 'uk' })] });
    const fixture = TestBed.createComponent(TaskMenuComponent);
    fixture.componentRef.setInput('projectId', 'p1');
    const labels = () => (fixture.componentInstance as unknown as { items(): { labelKey: string }[] }).items().map((item) => item.labelKey);
    expect(labels()).toEqual(['planner.taskActions.details']);
    fixture.componentRef.setInput('canEdit', true);
    expect(labels()).toEqual(['planner.taskActions.details', 'planner.taskActions.edit']);
    fixture.componentRef.setInput('canDelete', true);
    expect(labels()).toEqual(['planner.taskActions.details', 'planner.taskActions.edit', 'planner.taskActions.delete']);
  });

  it('«Delete» asks with the title in quotes and deletes the task; a failure keeps the window with the reason', async () => {
    TestBed.configureTestingModule({ schemas: [CUSTOM_ELEMENTS_SCHEMA], providers: [provideIonicAngular(), provideTranslateService({ lang: 'uk' })] });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    const remove = vi.spyOn(TestBed.inject(TasksService), 'remove').mockResolvedValueOnce('forbidden').mockResolvedValue('done');
    const fixture = TestBed.createComponent(TaskMenuComponent);
    fixture.componentRef.setInput('projectId', 'p1');
    fixture.componentRef.setInput('canDelete', true);
    fixture.detectChanges();
    const query = <T>(type: new (...args: never[]) => T) => fixture.debugElement.query(By.directive(type))?.componentInstance as T | undefined;
    vi.spyOn(query(PopoverComponent)!, 'open').mockImplementation(() => undefined);
    vi.spyOn(query(PopoverComponent)!, 'close').mockResolvedValue();
    const closeModal = vi.spyOn(query(ModalComponent)!, 'close').mockResolvedValue();
    const task = { id: 'a', title: 'Афіша' } as ProjectTask;
    fixture.componentInstance.open(document.createElement('button'), task);
    await (fixture.componentInstance as unknown as { onChoose(action: string): Promise<void> }).onChoose('delete');
    const confirm = await vi.waitFor(
      () => {
        fixture.detectChanges();
        const found = query(ConfirmDialogComponent);
        expect(found).toBeDefined();
        return found!;
      },
      { timeout: 3000 },
    );
    expect(confirm.message()).toBe(uk.planner.dialogs.deleteTask.replace('{{title}}', 'Афіша'));
    confirm.confirm.emit();
    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(confirm.error()).toBe(uk.planner.tasks.failure.forbidden);
    });
    expect(closeModal).not.toHaveBeenCalled();
    confirm.confirm.emit();
    await vi.waitFor(() => expect(closeModal).toHaveBeenCalled());
    expect(remove).toHaveBeenCalledWith('p1', 'a');
  });
});
