import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, input } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { DashboardChart } from '../core/api/admin.api';
import { OwnerDashboardDto } from '../core/api/setup.api';
import { SessionStore } from '../core/session/session.store';
import { APP_ICONS } from '../icons';
import { ApexChartComponent } from '../shared/apex-chart.component';
import { meWith } from './industry/industry.fixtures.spec';
import { OwnerPage } from './owner.page';

/** The chart library is not what these specs are about; a stand-in records what it was given. */
@Component({ selector: 'erp-apex-chart', standalone: true, template: `<div data-testid="chart-stub">{{ chart().categories.join(',') }}</div>` })
class ChartStub { readonly chart = input.required<DashboardChart>(); }

const OWNER: OwnerDashboardDto = {
  from: '2026-01-01', to: '2026-06-30', currency: 'AED',
  sections: [
    { key: 'revenue', title: 'Revenue and expenses', cards: [{ key: 'revenue', label: 'Revenue', value: 12500, format: 'money', link: '/finance/reports' }, { key: 'expenses', label: 'Expenses', value: 4000, format: 'money' }],
      chart: { categories: ['Jan', 'Feb'], series: [{ name: 'Revenue', values: [5000, 7500] }, { name: 'Expenses', values: [1000, 3000] }] } },
    { key: 'gross-profit', title: 'Profit', cards: [{ key: 'gross-margin', label: 'Gross margin', value: 68, format: 'percent' }, { key: 'orders', label: 'Open jobs', value: 14, format: 'number', caption: 'waiting' }] },
    { key: 'receivables', title: 'Receivables', cards: [{ key: 'receivables', label: 'Owed by customers', value: 900, format: 'money' }],
      table: { columns: ['Customer', 'Balance'], rows: [['Gulf Traders', '900.00']], columnFormats: ['text', 'money'] }, note: 'Credit notes are netted off.' },
    { key: 'branch', title: 'Branch comparison', cards: [] },
  ],
};

describe('Owner dashboard page', () => {
  let fixture: ComponentFixture<OwnerPage>;
  let http: HttpTestingController;
  const root = () => fixture.nativeElement as HTMLElement;
  const el = (id: string) => root().querySelector(`[data-testid="${id}"]`) as HTMLElement | null;

  beforeEach(async () => {
    localStorage.clear();
    TestBed.overrideComponent(OwnerPage, { remove: { imports: [ApexChartComponent] }, add: { imports: [ChartStub] } });
    await TestBed.configureTestingModule({
      imports: [OwnerPage],
      providers: [provideNoopAnimations(), provideHttpClient(), provideHttpClientTesting(), provideRouter([]), provideNzIcons(APP_ICONS)],
    }).compileComponents();
    TestBed.inject(SessionStore).me.set(meWith(['finance.reports.read']));
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(OwnerPage);
    fixture.detectChanges();
  });
  afterEach(() => fixture.destroy());

  const load = (body: OwnerDashboardDto = OWNER) => {
    const req = http.expectOne((r) => r.url === '/api/v1/dashboard/owner');
    req.flush(body);
    fixture.detectChanges();
    return req;
  };

  it('asks for the chosen period and entity, and shows the sections in the order the server gives', () => {
    const req = load();
    expect(req.request.params.get('from')).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    expect(req.request.params.get('to')).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    const sections = Array.from(root().querySelectorAll('section')).map((s) => s.getAttribute('data-testid'));
    expect(sections).toEqual(['owner-section-revenue', 'owner-section-gross-profit', 'owner-section-receivables', 'owner-section-branch']);
    expect(root().textContent).toContain('Revenue and expenses');
  });

  it('shows cards by format, with the workspace currency and their links', () => {
    load();
    const revenue = el('owner-card-revenue')!;
    expect(revenue.textContent).toContain('Revenue');
    expect(revenue.textContent).toContain('AED');
    expect(revenue.tagName).toBe('A');
    expect(revenue.getAttribute('href')).toBe('/finance/reports');
    expect(el('owner-card-gross-margin')?.textContent).toContain('%');
    expect(el('owner-card-orders')?.textContent).toContain('waiting');
  });

  it('shows a table with column formats, a chart and the section note', () => {
    load();
    const table = el('owner-table-receivables')!;
    expect(table.textContent).toContain('Gulf Traders');
    expect(table.textContent).toContain('900.00');
    expect(table.querySelector('th.r')?.textContent).toContain('Balance');
    expect(el('owner-chart-revenue')?.textContent).toContain('Jan,Feb');
    expect(el('owner-note-receivables')?.textContent).toContain('Credit notes are netted off.');
  });

  it('says so when a section has no data', () => {
    load();
    expect(el('owner-empty-branch')).not.toBeNull();
    expect(el('owner-empty-revenue')).toBeNull();
  });

  it('loads again for another period', () => {
    load();
    fixture.componentInstance.setRange({ preset: 'custom', from: '2026-04-01', to: '2026-06-30' });
    const again = http.expectOne((r) => r.url === '/api/v1/dashboard/owner');
    expect(again.request.params.get('from')).toBe('2026-04-01');
    expect(again.request.params.get('to')).toBe('2026-06-30');
    again.flush({ ...OWNER, from: '2026-04-01', sections: [] });
  });

  it('offers a retry when the figures do not load', () => {
    http.expectOne((r) => r.url === '/api/v1/dashboard/owner').flush({}, { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();
    expect(el('state-panel')).not.toBeNull();
    (el('state-panel-action') as HTMLButtonElement).click();
    http.expectOne((r) => r.url === '/api/v1/dashboard/owner').flush(OWNER);
  });
});
