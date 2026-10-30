import { TestBed, ComponentFixture, fakeAsync, tick } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Observable, of, throwError } from 'rxjs';
import { DashboardApi, DashboardDto, DashboardPeriod } from '../core/api/admin.api';
import { DashboardPage, RANGE_KEY, presetRange } from './dashboard.page';

/** The period control: one dropdown (+ Refresh); the picker only for Custom; every change reloads the figures for that period. */
describe('Overview range control', () => {
  let fixture: ComponentFixture<DashboardPage>;
  let calls: DashboardPeriod[];
  let fail: boolean;
  const dto = (p: DashboardPeriod): DashboardDto => ({
    asOf: p.to!, from: p.from, to: p.to, alerts: [],
    kpis: [{ key: 'cash', label: 'Cash and bank', value: 10, format: 'money', caption: 'all bank and cash accounts, at 30 Oct 2026', order: 60,
      captionKey: 'kpi.cash.caption', captionArgs: { date: '2026-10-30' } }],
    charts: [{ key: 'billed-collected', title: 'Billed vs collected', kind: 'bar', categories: ['Nov 25'], order: 10, granularity: 'month',
      buckets: [{ from: '2025-10-31', to: '2025-11-30' }], series: [{ name: 'Billed', values: [5] }, { name: 'Collected', values: [0] }] }],
  });
  const el = (id: string) => fixture.nativeElement.querySelector(`[data-testid="${id}"]`) as HTMLElement | null;

  beforeEach(async () => {
    localStorage.removeItem(RANGE_KEY);
    calls = []; fail = false;
    await TestBed.configureTestingModule({
      imports: [DashboardPage],
      providers: [provideRouter([]), provideNoopAnimations(), provideHttpClient(), provideHttpClientTesting(),
        { provide: DashboardApi, useValue: { get: (p: DashboardPeriod): Observable<DashboardDto> => { calls.push(p); return fail ? throwError(() => new Error('x')) : of(dto(p)); } } }],
    }).compileComponents();
  });
  const create = () => { fixture = TestBed.createComponent(DashboardPage); return fixture; };
  afterEach(() => localStorage.removeItem(RANGE_KEY));

  it('shows the dropdown and Refresh, no Narrow/Widen buttons and no picker by default, and loads "Last year"', fakeAsync(() => {
    create();
    fixture.detectChanges(); tick(); fixture.detectChanges();
    expect(el('dashboard-range-preset')).not.toBeNull();
    expect(el('dashboard-range-refresh')).not.toBeNull();
    expect(el('dashboard-range-narrow')).toBeNull();
    expect(el('dashboard-range-widen')).toBeNull();
    expect(el('dashboard-range-picker')).toBeNull();
    expect(fixture.componentInstance.selectValue()).toBe('1y');
    const y = presetRange('1y');
    expect(calls.at(-1)).toEqual({ from: y.from, to: y.to });
    // the static "All Legal Entities • as of …" text is gone
    expect(fixture.nativeElement.textContent).not.toContain('All Legal Entities');
    expect(fixture.nativeElement.querySelector('.scope-pill')).toBeNull();
    // the cash caption is translated client-side from its key, the date as an argument
    expect(fixture.componentInstance.data()!.kpis[0].caption).toContain('all bank and cash accounts, at');
    expect(fixture.componentInstance.data()!.kpis[0].caption).toContain('Oct');
  }));

  it('Custom shows the picker without reloading; a preset hides it and reloads that period', fakeAsync(() => {
    create();
    fixture.detectChanges(); tick(); fixture.detectChanges();
    const page = fixture.componentInstance;
    const before = calls.length;
    page.setPreset('custom'); fixture.detectChanges(); tick(); fixture.detectChanges();
    expect(el('dashboard-range-picker')).not.toBeNull();
    expect(calls.length).toBe(before);
    page.setCustomRange([new Date(2025, 0, 1), new Date(2025, 5, 30)]); fixture.detectChanges(); tick();
    expect(calls.at(-1)).toEqual({ from: '2025-01-01', to: '2025-06-30' });
    expect(el('dashboard-range-picker')).not.toBeNull();
    expect(JSON.parse(localStorage.getItem(RANGE_KEY)!)).toEqual({ preset: 'custom', from: '2025-01-01', to: '2025-06-30' });
    page.setPreset('3m'); fixture.detectChanges(); tick(); fixture.detectChanges();
    expect(el('dashboard-range-picker')).toBeNull();
    expect(calls.at(-1)).toEqual({ from: presetRange('3m').from, to: presetRange('3m').to });
    expect(TestBed.inject(Router).url).toContain('range=3m');
  }));

  it('rejects a custom period longer than 5 years, keeps the current one and says why', fakeAsync(() => {
    create();
    fixture.detectChanges(); tick();
    const page = fixture.componentInstance;
    page.setPreset('custom'); tick();
    const before = calls.length;
    page.setCustomRange([new Date(2015, 0, 1), new Date(2025, 0, 1)]); fixture.detectChanges(); tick();
    expect(calls.length).toBe(before);
    expect(page.range().preset).toBe('1y');
    expect(el('dashboard-range-error')).not.toBeNull();
  }));

  it('restores a stored custom period with the picker visible', fakeAsync(() => {
    localStorage.setItem(RANGE_KEY, JSON.stringify({ preset: 'custom', from: '2025-03-01', to: '2025-06-30' }));
    create();
    fixture.detectChanges(); tick(); fixture.detectChanges();
    expect(fixture.componentInstance.selectValue()).toBe('custom');
    expect(TestBed.inject(Router).url).toContain('from=2025-03-01'); // written to the URL on first load
    expect(el('dashboard-range-picker')).not.toBeNull();
    expect(calls.at(-1)).toEqual({ from: '2025-03-01', to: '2025-06-30' });
  }));

  it('shows an error with Retry instead of an endless skeleton when the load fails', fakeAsync(() => {
    fail = true;
    create();
    fixture.detectChanges(); tick(); fixture.detectChanges();
    expect(el('dashboard-error')).not.toBeNull();
    expect(el('dashboard-loading')).toBeNull();
    fail = false;
    el('dashboard-retry')!.click(); fixture.detectChanges(); tick(); fixture.detectChanges();
    expect(el('dashboard-error')).toBeNull();
    expect(fixture.componentInstance.data()).not.toBeNull();
  }));
});
