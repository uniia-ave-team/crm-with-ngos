import { CUSTOM_ELEMENTS_SCHEMA, Signal, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideIonicAngular } from '@ionic/angular';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../../assets/i18n/uk.json';
import { DialogHeaderComponent } from '../../../shared/dialog-header/dialog-header.component';
import { ModalComponent } from '../../../shared/modal/modal.component';
import { ProjectsService } from '../projects.service';
import { ProjectAddDialogComponent } from './project-add-dialog.component';

describe('ProjectAddDialogComponent', () => {
  function setUp(canCreate = true) {
    TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideIonicAngular(), provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    const service = TestBed.inject(ProjectsService);
    (service as { canCreate: Signal<boolean> }).canCreate = signal(canCreate);
    const create = vi.spyOn(service, 'create').mockResolvedValue({ id: 'p1' });
    const fixture = TestBed.createComponent(ProjectAddDialogComponent);
    fixture.detectChanges();
    const modal = fixture.debugElement.query(By.directive(ModalComponent)).componentInstance as ModalComponent;
    const openModal = vi.spyOn(modal, 'open');
    const closeModal = vi.spyOn(modal, 'close').mockResolvedValue();
    /** Відкриває вікно й чекає, поки зʼявиться форма. */
    const open = async () => {
      fixture.componentInstance.open();
      // Вікно відкривається з анімацією Ionic; запас часу — на повільний повний прогін.
      await vi.waitFor(
        () => {
          fixture.detectChanges();
          expect(document.querySelector('#project-title')).not.toBeNull();
        },
        { timeout: 3000 },
      );
      return document.querySelector('app-modal ion-modal, ion-modal') as HTMLElement;
    };
    const field = <T extends HTMLElement>(id: string) => document.querySelector<T>(`#${id}`)!;
    const type = (id: string, value: string) => {
      const input = field<HTMLInputElement>(id);
      input.value = value;
      input.dispatchEvent(new Event(input.tagName === 'SELECT' ? 'change' : 'input'));
      fixture.detectChanges();
    };
    const submit = async () => {
      document.querySelector<HTMLFormElement>('form.settings-form')!.dispatchEvent(new Event('submit', { cancelable: true }));
      await fixture.whenStable();
      fixture.detectChanges();
    };
    const text = () => document.querySelector('form.settings-form')!.textContent ?? '';
    return { fixture, create, openModal, closeModal, open, field, type, submit, text };
  }

  afterEach(() => {
    vi.restoreAllMocks();
    document.querySelectorAll('ion-modal').forEach((modal) => modal.remove());
  });

  it('asks the name, the description, the visibility (private at first) and the dates (today at first, no end)', async () => {
    const { open, field, fixture } = setUp();
    await open();
    const header = fixture.debugElement.query(By.directive(DialogHeaderComponent)).componentInstance as DialogHeaderComponent;
    expect(header.title()).toBe(uk.planner.dialogs.add.title);
    const options = Array.from(field<HTMLSelectElement>('project-visibility').options);
    expect(options.map((option) => [option.value, option.textContent?.trim()])).toEqual([
      ['0', uk.planner.visibility['0']],
      ['1', uk.planner.visibility['1']],
      ['2', uk.planner.visibility['2']],
    ]);
    expect(field<HTMLSelectElement>('project-visibility').value).toBe('0');
    expect(field('project-visibility-hint').textContent?.trim()).toBe(uk.planner.visibilityHint['0']);
    expect(field<HTMLInputElement>('project-start').value).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    expect(field<HTMLInputElement>('project-end').value).toBe('');
    expect(field<HTMLInputElement>('project-title').getAttribute('maxlength')).toBe('255');
    expect(field<HTMLTextAreaElement>('project-description').getAttribute('maxlength')).toBe('2000');
  });

  it('does not send without a name or with the end before the start', async () => {
    const { open, type, submit, text, create } = setUp();
    await open();
    await submit();
    expect(text()).toContain(uk.common.required);
    type('project-title', 'Явір');
    type('project-start', '2026-03-20');
    type('project-end', '2026-03-19');
    await submit();
    expect(text()).toContain(uk.planner.dialogs.add.endBeforeStart);
    expect(create).not.toHaveBeenCalled();
  });

  it('creates the project with the chosen values and closes', async () => {
    const { open, type, submit, create, closeModal, field } = setUp();
    await open();
    type('project-title', '  Явір  ');
    type('project-description', ' ');
    type('project-visibility', '2');
    expect(field('project-visibility-hint').textContent?.trim()).toBe(uk.planner.visibilityHint['2']);
    // Іконка перед вибором — за вибраною доступністю.
    expect((document.querySelector('.project-form__visibility-icon') as unknown as { name: string }).name).toBe('globe-outline');
    type('project-start', '2026-03-20');
    type('project-end', '2026-03-25');
    await submit();
    expect(create).toHaveBeenCalledWith({ title: 'Явір', description: null, visibility: 2, startDate: '2026-03-20', endDate: '2026-03-25' });
    expect(closeModal).toHaveBeenCalled();
  });

  it('clears the end date by the cross at the end of the field, shown only while there is a date', async () => {
    const { open, type, submit, create, field, fixture } = setUp();
    await open();
    expect(document.querySelector('.date-range__clear')).toBeNull();
    type('project-title', 'Явір');
    type('project-end', '2030-01-01');
    const cross = document.querySelector<HTMLButtonElement>('.date-range__clear')!;
    expect(cross.getAttribute('aria-label')).toBe(uk.planner.dialogs.add.clearEndDate);
    cross.click();
    fixture.detectChanges();
    expect(field<HTMLInputElement>('project-end').value).toBe('');
    expect(document.activeElement).toBe(field('project-end'));
    expect(document.querySelector('.date-range__clear')).toBeNull();
    await submit();
    expect(create).toHaveBeenCalledWith(expect.objectContaining({ endDate: null }));
  });

  it('keeps the window with the reason when the server did not create the project', async () => {
    const { open, type, submit, create, closeModal, text } = setUp();
    create.mockResolvedValue('invalid');
    await open();
    type('project-title', 'Явір');
    await submit();
    expect(text()).toContain(uk.planner.dialogs.add.failure.invalid);
    expect(closeModal).not.toHaveBeenCalled();
  });

  it('does not open without the right to create projects', () => {
    const { fixture, openModal } = setUp(false);
    fixture.componentInstance.open();
    expect(openModal).not.toHaveBeenCalled();
  });
});
