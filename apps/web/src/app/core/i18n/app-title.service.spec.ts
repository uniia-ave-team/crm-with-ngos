import { Signal, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Title } from '@angular/platform-browser';
import { Router, provideRouter } from '@angular/router';
import { TranslateService, provideTranslateService } from '@ngx-translate/core';

import uk from '../../../assets/i18n/uk.json';
import { AccountsService } from '../accounts/accounts.service';
import { AppTitleService, isBarePage } from './app-title.service';

describe('AppTitleService', () => {
  async function start(url: string, ngoName: string | null) {
    TestBed.configureTestingModule({
      providers: [provideRouter([{ path: '**', children: [] }]), provideTranslateService({ lang: 'uk', fallbackLang: 'uk' })],
    });
    TestBed.inject(TranslateService).setTranslation('uk', uk);
    TestBed.inject(TranslateService).use('uk');
    (TestBed.inject(AccountsService) as unknown as { active: Signal<{ ngoName: string | null } | null> }).active = signal({ ngoName });
    const service = TestBed.inject(AppTitleService);
    await TestBed.inject(Router).navigateByUrl(url);
    TestBed.tick();
    return { service, title: () => TestBed.inject(Title).getTitle() };
  }

  it('is «NGO — Yavir» on the pages of an account', async () => {
    const { title } = await start('/planner', 'Громада');
    expect(title()).toBe(uk.common.pageTitle.replace('{{ngo}}', 'Громада').replace('{{app}}', uk.common.appName));
  });

  it('is only the name of the app on the pages without an account, and without the name of the NGO', async () => {
    for (const url of ['/login', '/relogin', '/join?token=x', '/reset-password#token=x']) {
      TestBed.resetTestingModule();
      expect((await start(url, 'Громада')).title()).toBe(uk.common.appName);
    }
    TestBed.resetTestingModule();
    expect((await start('/planner', null)).title()).toBe(uk.common.appName);
  });

  it('tells the bare pages by the path only', () => {
    expect(isBarePage('/login')).toBe(true);
    expect(isBarePage('/reset-password?token=x')).toBe(true);
    expect(isBarePage('/loginx')).toBe(false);
    expect(isBarePage('/planner')).toBe(false);
  });
});
