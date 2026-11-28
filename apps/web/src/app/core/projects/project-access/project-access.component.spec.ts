import { CUSTOM_ELEMENTS_SCHEMA, Signal, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../../assets/i18n/uk.json';
import { CurrentUser, CurrentUserService } from '../../users/current-user.service';
import { UserSummary } from '../../users/users.service';
import { ProjectMemberSearchComponent } from '../project-member-search/project-member-search.component';
import { ProjectMember, ProjectsService } from '../projects.service';
import { ProjectAccessComponent } from './project-access.component';

describe('ProjectAccessComponent', () => {
  const member = (userId: string, overrides: Partial<ProjectMember> = {}): ProjectMember => ({
    userId,
    firstName: 'Імʼя',
    lastName: 'Прізвище',
    patronymic: null,
    avatarUrl: null,
    roleTitle: null,
    claim: 0,
    isActive: true,
    ...overrides,
  });
  const members = [
    member('me', { claim: 2, roleTitle: 'Керівниця' }),
    member('u2', { claim: 1, roleTitle: 'Дизайнер', patronymic: 'По-батькові' }),
    member('u3', { isActive: false }),
  ];

  async function render(editable: boolean, permissions: string[] = [], extra: { visibility?: 0 | 1 | 2 | null; canEditProject?: boolean } = {}) {
    TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    (TestBed.inject(CurrentUserService) as { user: Signal<CurrentUser | null> }).user = signal({
      id: 'me',
      email: '',
      firstName: '',
      lastName: '',
      roles: [],
    } as unknown as CurrentUser);
    (TestBed.inject(CurrentUserService) as { permissions: Signal<readonly string[]> }).permissions = signal(permissions);
    const service = TestBed.inject(ProjectsService);
    const load = vi.spyOn(service, 'members').mockResolvedValue(members);
    const update = vi.spyOn(service, 'updateMember').mockResolvedValue('done');
    const remove = vi.spyOn(service, 'removeMember').mockResolvedValue('done');
    const fixture = TestBed.createComponent(ProjectAccessComponent);
    fixture.componentRef.setInput('projectId', 'p1');
    fixture.componentRef.setInput('editable', editable);
    fixture.componentRef.setInput('visibility', extra.visibility ?? null);
    fixture.componentRef.setInput('canEditProject', extra.canEditProject ?? false);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const rows = () => Array.from(element.querySelectorAll<HTMLElement>('.project-access__member'));
    return { fixture, element, rows, load, update, remove };
  }

  afterEach(() => vi.restoreAllMocks());

  it('lists the members: the avatar, the full name, the role title under it (or «—») and the level as a tag', async () => {
    const { rows, load } = await render(false);
    expect(load).toHaveBeenCalledWith('p1');
    expect(rows().map((row) => row.querySelector('.project-access__name')?.textContent?.trim())).toEqual([
      'Прізвище Імʼя',
      'Прізвище Імʼя По-батькові',
      'Прізвище Імʼя',
    ]);
    expect(rows().map((row) => row.querySelector('.project-access__role')?.textContent?.trim())).toEqual(['Керівниця', 'Дизайнер', uk.common.empty]);
    expect(rows().map((row) => row.querySelector('.project-access__tag')?.textContent?.trim())).toEqual([
      uk.planner.claims['2'],
      uk.planner.claims['1'],
      uk.planner.claims['0'],
    ]);
    expect(rows()[0].querySelector('app-avatar')).not.toBeNull();
    // Вимкнений акаунт — приглушений, з позначкою.
    expect(rows()[2].classList).toContain('project-access__member--disabled');
    expect(rows()[2].querySelector('.project-access__disabled-icon')?.getAttribute('aria-label')).toBe(uk.planner.access.disabled);
  });

  it('has no choice and no removing without the right to edit the access', async () => {
    const { element } = await render(false);
    expect(element.querySelector('select')).toBeNull();
    expect(element.querySelector('.project-access__remove')).toBeNull();
    expect(element.querySelector('.project-access__remove-spacer')).toBeNull();
  });

  it('turns the tags of the others (not the own one) into a choice of the level with a button to remove', async () => {
    const { rows } = await render(true);
    expect(rows()[0].querySelector('select')).toBeNull();
    expect(rows()[0].querySelector('.project-access__remove')).toBeNull();
    // Свій тег — у стовпчику з іншими: на місці смітника порожнє місце.
    expect(rows()[0].querySelector('.project-access__remove-spacer')).not.toBeNull();
    const select = rows()[1].querySelector('select')!;
    expect(Array.from(select.options).map((option) => [option.value, option.textContent?.trim()])).toEqual([
      ['0', uk.planner.claims['0']],
      ['1', uk.planner.claims['1']],
      ['2', uk.planner.claims['2']],
    ]);
    expect(select.value).toBe('1');
    expect(rows()[1].querySelector('.project-access__tag--select ion-icon')).not.toBeNull();
    expect(rows()[1].querySelector('.project-access__remove')).not.toBeNull();
  });

  it('changes the level of a member, and puts it back with the reason when the server says no', async () => {
    const { fixture, rows, update, element } = await render(true);
    const select = rows()[1].querySelector('select')!;
    select.value = '2';
    select.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    expect(update).toHaveBeenCalledWith('p1', members[1], 2);

    update.mockResolvedValue('invalid');
    const other = rows()[2].querySelector('select')!;
    other.value = '1';
    other.dispatchEvent(new Event('change'));
    await fixture.whenStable();
    fixture.detectChanges();
    expect(other.value).toBe('0');
    expect(element.querySelector('.project-access__message--error')?.textContent?.trim()).toBe(uk.planner.access.failure.invalid);
  });

  it('removes a member from the project and from the list', async () => {
    const { fixture, rows, remove } = await render(true);
    rows()[1].querySelector<HTMLButtonElement>('.project-access__remove')!.click();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(remove).toHaveBeenCalledWith('p1', 'u2');
    expect(rows()).toHaveLength(2);
  });

  describe('the field to add a colleague (as in Figma)', () => {
    it('is there on top with Permissions.ViewUser and the right to edit, knowing who is a member already', async () => {
      const { element, fixture } = await render(true, ['Permissions.ViewUser']);
      const search = fixture.debugElement.query(By.directive(ProjectMemberSearchComponent));
      expect(search).not.toBeNull();
      expect((search.componentInstance as ProjectMemberSearchComponent).memberIds()).toEqual(['me', 'u2', 'u3']);
      expect(element.querySelector('app-project-member-search')!.compareDocumentPosition(element.querySelector('.project-access__list')!) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    });

    it('is not there without Permissions.ViewUser or without the right to edit', async () => {
      expect((await render(true)).element.querySelector('app-project-member-search')).toBeNull();
      TestBed.resetTestingModule();
      expect((await render(false, ['Permissions.ViewUser'])).element.querySelector('app-project-member-search')).toBeNull();
    });
  });

  describe('as the «Share» window of Figma', () => {
    it('says who has access, above the visibility row and the list that scrolls', async () => {
      const { element } = await render(false, [], { visibility: 0 });
      expect(element.querySelector('.project-access__heading')?.textContent?.trim()).toBe(uk.planner.access.whoHasAccess);
      const row = element.querySelector('.project-access__visibility')!;
      expect(row.querySelector('.project-access__visibility-name')?.textContent?.trim()).toBe(uk.planner.visibility['0']);
      expect((row.querySelector('.project-access__visibility-icon') as unknown as { name: string }).name).toBe('lock-closed-outline');
      // Без права редагувати проєкт — без стрілки й не кнопка.
      expect(row.tagName).toBe('DIV');
      expect(row.querySelector('.project-access__chevron')).toBeNull();
      expect(getComputedStyle(element.querySelector('.project-access__list')!).overflowY).toBe('auto');
    });

    it.each([
      [1, 'eye-outline'],
      [2, 'globe-outline'],
    ] as const)('draws the visibility %s with %s', async (visibility, icon) => {
      const { element } = await render(false, [], { visibility });
      expect((element.querySelector('.project-access__visibility-icon') as unknown as { name: string }).name).toBe(icon);
    });

    it('leads to editing the project by the arrow of the visibility row when the project can be edited', async () => {
      const { element, fixture } = await render(true, [], { visibility: 2, canEditProject: true });
      const edits: unknown[] = [];
      fixture.componentInstance.editProject.subscribe(() => edits.push(true));
      const row = element.querySelector<HTMLButtonElement>('button.project-access__visibility')!;
      expect(row.querySelector('.project-access__chevron')).not.toBeNull();
      row.click();
      expect(edits).toHaveLength(1);
    });
  });

  describe('adding a colleague', () => {
    const colleague = (id: string, lastName: string) =>
      ({ id, email: `${id}@b.org`, firstName: 'Імʼя', lastName, patronymic: null, avatarUrl: null, roles: [], isActive: true }) as unknown as UserSummary;
    const search = (fixture: { debugElement: { query(predicate: unknown): { componentInstance: unknown } } }) =>
      fixture.debugElement.query(By.directive(ProjectMemberSearchComponent)).componentInstance as ProjectMemberSearchComponent;

    it('sends the colleague with the level and the role title, shows the wheel meanwhile, then the members anew and the success in green', async () => {
      const { fixture, element, load } = await render(true, ['Permissions.ViewUser']);
      let finish!: (value: 'done') => void;
      const add = vi.spyOn(TestBed.inject(ProjectsService), 'addMember').mockImplementationOnce(() => new Promise((resolve) => (finish = resolve)));
      const first = search(fixture);
      first.add.emit({ user: colleague('a', 'Перший'), claim: 0, roleTitle: 'Дизайнер' });
      fixture.detectChanges();
      expect(element.querySelector('.project-access__adding ion-spinner')).not.toBeNull();
      expect(element.querySelector('.project-access__list')).toBeNull();

      finish('done');
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(element.querySelector('.project-access__adding')).toBeNull();
      });
      expect(add).toHaveBeenCalledWith('p1', 'a', 0, 'Дизайнер');
      expect(load).toHaveBeenCalledTimes(2);
      expect(element.querySelector('.project-access__added')?.textContent?.trim()).toBe(uk.planner.access.added.replace('{{names}}', 'Перший Імʼя'));
      expect(element.querySelector('.project-access__failures')).toBeNull();
      // Поле — нове, порожнє.
      expect(search(fixture)).not.toBe(first);
    });

    it('shows a red block with the reason when the colleague was not added', async () => {
      const { fixture, element } = await render(true, ['Permissions.ViewUser']);
      vi.spyOn(TestBed.inject(ProjectsService), 'addMember').mockResolvedValueOnce('exists');
      search(fixture).add.emit({ user: colleague('a', 'Перший'), claim: 1, roleTitle: null });
      await vi.waitFor(() => {
        fixture.detectChanges();
        expect(element.querySelector('.project-access__failures')).not.toBeNull();
      });
      expect(element.querySelector('.project-access__failures')?.getAttribute('role')).toBe('alert');
      expect(element.querySelector('.project-access__failure')?.textContent?.trim()).toBe(
        uk.planner.access.addFailed.replace('{{name}}', 'Перший Імʼя').replace('{{reason}}', uk.planner.access.addFailure.exists),
      );
      expect(element.querySelector('.project-access__added')).toBeNull();
    });
  });
});
