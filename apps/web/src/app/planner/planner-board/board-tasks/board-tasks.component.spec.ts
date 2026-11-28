import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../../assets/i18n/uk.json';
import { ProjectTask, TasksService } from '../../../core/tasks/tasks.service';
import { BOARD_TASKS_PAGE_SIZE, BoardTasksComponent } from './board-tasks.component';

describe('BoardTasksComponent', () => {
  const task = (id: string): ProjectTask => ({
    id,
    topicId: 't1',
    title: 'Завдання ' + id,
    description: null,
    status: 0,
    priority: 1,
    startDate: '2026-03-20',
    endDate: null,
    assignees: [],
    tags: [],
    unreadComments: 0,
  });

  async function render() {
    TestBed.configureTestingModule({
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
      providers: [provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    const page = vi
      .spyOn(TestBed.inject(TasksService), 'page')
      .mockResolvedValueOnce({ items: [task('a'), task('b')], hasNextPage: true })
      .mockResolvedValueOnce({ items: [task('c')], hasNextPage: false });
    const fixture = TestBed.createComponent(BoardTasksComponent);
    fixture.componentRef.setInput('projectId', 'p1');
    fixture.componentRef.setInput('topicId', 't1');
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const titles = () => Array.from(element.querySelectorAll('.task-card__title')).map((title) => title.textContent?.trim());
    return { fixture, element, page, titles };
  }

  afterEach(() => vi.restoreAllMocks());

  it('asks all the pages of the column one after another', async () => {
    const { fixture, page, titles } = await render();
    await vi.waitFor(() => expect(page).toHaveBeenCalledWith('p1', 2, BOARD_TASKS_PAGE_SIZE, { topicId: 't1' }));
    expect(page).toHaveBeenCalledWith('p1', 1, BOARD_TASKS_PAGE_SIZE, { topicId: 't1' });
    await fixture.whenStable();
    fixture.detectChanges();
    expect(titles()).toEqual(['Завдання a', 'Завдання b', 'Завдання c']);
    expect(page).toHaveBeenCalledTimes(2);
  });
});
