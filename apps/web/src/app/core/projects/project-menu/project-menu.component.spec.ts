import { CUSTOM_ELEMENTS_SCHEMA, Signal, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideIonicAngular } from '@ionic/angular';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../../assets/i18n/uk.json';
import { ActionListComponent } from '../../../shared/action-list/action-list.component';
import { PopoverComponent } from '../../../shared/popover/popover.component';
import { CurrentUserService } from '../../users/current-user.service';
import { ProjectsService } from '../projects.service';
import { ProjectMenuComponent } from './project-menu.component';

describe('ProjectMenuComponent', () => {
  const project = { id: 'p1', title: 'Явір' };

  function setUp(permissions: string[]) {
    TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideIonicAngular(), provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    (
      TestBed.inject(CurrentUserService) as {
        permissions: Signal<readonly string[]>;
      }
    ).permissions = signal(permissions);
    const fixture = TestBed.createComponent(ProjectMenuComponent);
    fixture.detectChanges();
    const popover = fixture.debugElement.query(By.directive(PopoverComponent)).componentInstance as PopoverComponent;
    vi.spyOn(popover, 'open').mockImplementation(() => undefined);
    vi.spyOn(popover, 'close').mockResolvedValue();
    const list = () => fixture.debugElement.query(By.directive(ActionListComponent))?.componentInstance as ActionListComponent | undefined;
    fixture.componentInstance.open(document.createElement('button'), project);
    fixture.detectChanges();
    return { fixture, list };
  }

  afterEach(() => vi.restoreAllMocks());

  it('offers «Edit» and «Edit access» with Permissions.UpdateProject and «Delete» with Permissions.DeleteProject', () => {
    expect(
      setUp(['Permissions.UpdateProject', 'Permissions.DeleteProject'])
        .list()!
        .items()
        .map((item) => item.labelKey),
    ).toEqual(['planner.actions.edit', 'planner.actions.access', 'planner.actions.delete']);
  });

  it('always offers the access: «View access» without Permissions.UpdateProject', () => {
    expect(
      setUp(['Permissions.DeleteProject'])
        .list()!
        .items()
        .map((item) => item.labelKey),
    ).toEqual(['planner.actions.viewAccess', 'planner.actions.delete']);
    TestBed.resetTestingModule();
    expect(
      setUp([])
        .list()!
        .items()
        .map((item) => item.labelKey),
    ).toEqual(['planner.actions.viewAccess']);
  });

  it('asks for the window of the chosen action for its project', async () => {
    const { fixture, list } = setUp(['Permissions.UpdateProject']);
    const request = vi.spyOn(TestBed.inject(ProjectsService), 'requestAction');
    list()!.choose.emit('access');
    await fixture.whenStable();
    expect(request).toHaveBeenCalledWith('access', project);
  });
});
