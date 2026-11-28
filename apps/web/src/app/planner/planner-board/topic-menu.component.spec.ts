import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideIonicAngular } from '@ionic/angular';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { ProjectsService } from '../../core/projects/projects.service';
import { TasksService } from '../../core/tasks/tasks.service';
import { TopicFormComponent } from '../../core/projects/topic-form/topic-form.component';
import { ConfirmDialogComponent } from '../../shared/confirm-dialog/confirm-dialog.component';
import { ModalComponent } from '../../shared/modal/modal.component';
import { PopoverComponent } from '../../shared/popover/popover.component';
import { TopicAction, TopicMenuComponent } from './topic-menu.component';

describe('TopicMenuComponent', () => {
  const topic = { id: 't1', title: 'Розробка', order: 0 };

  /** Меню колонки, у якому вибрали `action`; вікно відкривається насправді (з анімацією Ionic). */
  async function choose(action: TopicAction) {
    TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideIonicAngular(), provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    const service = TestBed.inject(ProjectsService);
    const updateTopic = vi.spyOn(service, 'updateTopic').mockResolvedValue('done');
    const removeTopic = vi.spyOn(service, 'removeTopic').mockResolvedValue('done');
    const tasksPage = vi.spyOn(TestBed.inject(TasksService), 'page').mockResolvedValue({ items: [], hasNextPage: false });
    const fixture = TestBed.createComponent(TopicMenuComponent);
    fixture.componentRef.setInput('projectId', 'p1');
    fixture.detectChanges();
    const query = <T>(type: new (...args: never[]) => T) => fixture.debugElement.query(By.directive(type))?.componentInstance as T | undefined;
    vi.spyOn(query(PopoverComponent)!, 'open').mockImplementation(() => undefined);
    vi.spyOn(query(PopoverComponent)!, 'close').mockResolvedValue();
    const closeModal = vi.spyOn(query(ModalComponent)!, 'close').mockResolvedValue();
    fixture.componentInstance.open(document.createElement('button'), topic);
    await (fixture.componentInstance as unknown as { onChoose(action: TopicAction): Promise<void> }).onChoose(action);
    const child = <T>(type: new (...args: never[]) => T) =>
      vi.waitFor(
        () => {
          fixture.detectChanges();
          const found = query(type);
          expect(found).toBeDefined();
          return found!;
        },
        { timeout: 3000 },
      );
    return { fixture, updateTopic, removeTopic, closeModal, child, tasksPage };
  }

  // Вікно, що ще відкривається, не прибирати: Ionic доробить відкриття вже без нього.
  afterEach(async () => {
    await new Promise((resolve) => setTimeout(resolve, 50));
    document.querySelectorAll('ion-modal, ion-popover').forEach((element) => element.remove());
    vi.restoreAllMocks();
  });

  it('«Edit» opens the column form with its title and «Save», sending the new title and closing the window', async () => {
    const { updateTopic, closeModal, child } = await choose('edit');
    const form = await child(TopicFormComponent);
    expect(form.initial()).toBe('Розробка');
    expect(form.submitLabelKey()).toBe('planner.dialogs.edit.save');
    form.submitted.emit('Дизайн');
    await vi.waitFor(() => expect(updateTopic).toHaveBeenCalledWith('p1', 't1', 'Дизайн'));
    await vi.waitFor(() => expect(closeModal).toHaveBeenCalled());
  });

  it('«Edit» closes without a request when the title is the same, and keeps the window with the reason on a taken title', async () => {
    const { fixture, updateTopic, closeModal, child } = await choose('edit');
    const form = await child(TopicFormComponent);
    form.submitted.emit('Розробка');
    expect(updateTopic).not.toHaveBeenCalled();
    expect(closeModal).toHaveBeenCalled();

    closeModal.mockClear();
    updateTopic.mockResolvedValue('exists');
    form.submitted.emit('SMM');
    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(form.error()).toBe('planner.dialogs.failure.exists');
    });
    expect(closeModal).not.toHaveBeenCalled();
  });

  it('«Delete» asks with the column named when it has no tasks, then deletes it; the server finding tasks says it cannot be deleted', async () => {
    const { fixture, removeTopic, closeModal, child, tasksPage } = await choose('delete');
    const confirm = await child(ConfirmDialogComponent);
    expect(tasksPage).toHaveBeenCalledWith('p1', 1, 1, { topicId: 't1' });
    expect(confirm.message()).toBe(uk.planner.dialogs.deleteTopic.replace('{{title}}', 'Розробка'));

    removeTopic.mockResolvedValueOnce('hasTasks');
    confirm.confirm.emit();
    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(document.querySelector('.topic-menu__message')?.textContent?.trim()).toBe(uk.planner.dialogs.failure.hasTasks.replace('{{title}}', 'Розробка'));
    });
    expect(closeModal).not.toHaveBeenCalled();
  });

  it('«Delete» of a column with tasks says it cannot be deleted, without asking', async () => {
    TestBed.resetTestingModule();
    const { fixture, removeTopic, tasksPage } = await choose('delete');
    tasksPage.mockResolvedValue({ items: [{ id: 'x' } as never], hasNextPage: false });
    const menu = fixture.componentInstance as unknown as { dialog(): { topic: unknown }; checkTasks(topic: unknown): Promise<void> };
    await menu.checkTasks(menu.dialog().topic);
    await vi.waitFor(() => {
      fixture.detectChanges();
      expect(document.querySelector('.topic-menu__message')?.textContent?.trim()).toBe(uk.planner.dialogs.failure.hasTasks.replace('{{title}}', 'Розробка'));
    });
    expect(document.querySelector('app-confirm-dialog')).toBeNull();
    expect(removeTopic).not.toHaveBeenCalled();
  });

  it('deletes a column without tasks after «Yes»', async () => {
    const { removeTopic, closeModal, child } = await choose('delete');
    const confirm = await child(ConfirmDialogComponent);
    confirm.confirm.emit();
    await vi.waitFor(() => expect(closeModal).toHaveBeenCalled());
    expect(removeTopic).toHaveBeenCalledWith('p1', 't1');
  });
});
