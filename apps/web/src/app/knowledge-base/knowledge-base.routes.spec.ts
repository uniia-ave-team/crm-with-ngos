import { KbGuideComponent } from './kb-guide/kb-guide.component';
import { KbPageComponent } from './kb-page/kb-page.component';
import { KbTableComponent } from './kb-table/kb-table.component';
import { KbTemplatesComponent } from './kb-templates/kb-templates.component';
import { KNOWLEDGE_BASE_ROUTES } from './knowledge-base.routes';
import { KnowledgeBasePage } from './knowledge-base.page';

describe('knowledge base routes', () => {
  const [shell] = KNOWLEDGE_BASE_ROUTES;
  const child = (path: string) => shell.children!.find((route) => route.path === path)!;

  it('shows every view inside the page of the knowledge base, the guide by default', () => {
    expect(shell.component).toBe(KnowledgeBasePage);
    expect(child('')).toMatchObject({ pathMatch: 'full', component: KbGuideComponent });
    expect(child('tables/:id').component).toBe(KbTableComponent);
    expect(child('pages/:id').component).toBe(KbPageComponent);
    expect(child('templates').component).toBe(KbTemplatesComponent);
  });

  it('lets into the template manager only with the rights for templates, and sends unknown addresses to the guide', () => {
    expect(child('templates').canActivate).toHaveLength(1);
    expect(child('tables/:id').canActivate).toBeUndefined();
    expect(child('**')).toEqual({ path: '**', redirectTo: '' });
  });
});
