import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { of } from 'rxjs';
import { SessionStore } from '@core/session/session.store';
import { APP_ICONS } from '../../icons';
import { meWith } from '@features/industry/industry.fixtures.spec';
import { AssetDetailPage } from './pages/asset-detail/asset-detail.page';
import { AssetRegisterPage } from './pages/asset-register/asset-register.page';
import { AssetReportsPage } from './pages/asset-reports/asset-reports.page';
import { previousMonth } from './asset-periods';
import { DepreciationPage } from './pages/depreciation/depreciation.page';

const page = (items: unknown[]) => ({ items, total: items.length, page: 1, pageSize: 25, totalPages: 1, hasNext: false, hasPrevious: false });
const CATEGORY = { id: 'c1', code: 'VEH', name: 'Vehicles', lifeMonths: 60, method: 'straightLine', decliningRatePercent: null, assetAccountId: 'a1', accumulatedDepreciationAccountId: 'a2', depreciationExpenseAccountId: 'a3', disposalGainLossAccountId: null, isActive: true };
const ASSET = {
  id: 'as1', legalEntityId: 'le1', number: 'FA-00001', name: 'Delivery van', description: null, categoryId: 'c1', acquisitionDate: '2026-01-10', cost: 12000, salvageValue: 2000, method: 'straightLine', lifeMonths: 60,
  decliningRatePercent: null, businessUnitId: null, custodian: 'Ann', status: 'draft', accumulatedDepreciation: 0, netBookValue: 12000, capitalisedOn: null, capitalisationSource: null, capitalisationJournalId: null,
  sourceBillId: null, sourceBillLineId: null, disposedOn: null, disposalProceeds: null, disposalGainLoss: null, disposalJournalId: null,
};

describe('Fixed asset screens', () => {
  let http: HttpTestingController;
  const setup = (permissions: string[], route: Record<string, string> = {}) => {
    // Row actions go behind a menu without a hover pointer, as in a headless browser; these specs are about which controls exist.
    spyOn(window, 'matchMedia').and.returnValue({ matches: true, addEventListener() {}, removeEventListener() {} } as unknown as MediaQueryList);
    const paramMap = convertToParamMap(route);
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideNoopAnimations(), provideRouter([]), provideNzIcons(APP_ICONS), { provide: ActivatedRoute, useValue: { snapshot: { paramMap }, paramMap: of(paramMap) } }],
    });
    TestBed.inject(SessionStore).me.set(meWith(permissions, ['assets']));
    http = TestBed.inject(HttpTestingController);
  };
  const el = (f: ComponentFixture<unknown>, id: string) => (f.nativeElement as HTMLElement).querySelector(`[data-testid="${id}"]`) as HTMLElement | null;

  it('lists the register and offers New only to someone who may manage it', () => {
    setup(['assets.register.read']);
    const f = TestBed.createComponent(AssetRegisterPage);
    f.detectChanges();
    http.match((r) => r.url.endsWith('/m/assets/categories')).forEach((r) => r.flush(page([CATEGORY])));
    http.expectOne((r) => r.url.endsWith('/m/assets/register')).flush(page([ASSET]));
    f.detectChanges();
    expect((f.nativeElement as HTMLElement).textContent).toContain('FA-00001');
    expect((f.nativeElement as HTMLElement).textContent).toContain('Delivery van');
    expect(el(f, 'asset-new')).toBeNull();
  });

  it('shows New to a manager', () => {
    setup(['assets.register.read', 'assets.register.manage']);
    const f = TestBed.createComponent(AssetRegisterPage);
    f.detectChanges();
    http.match((r) => r.url.endsWith('/m/assets/categories')).forEach((r) => r.flush(page([CATEGORY])));
    http.expectOne((r) => r.url.endsWith('/m/assets/register')).flush(page([]));
    f.detectChanges();
    expect(el(f, 'asset-new')).not.toBeNull();
  });

  it('capitalises a draft asset from a clearing account', () => {
    setup(['assets.register.read', 'assets.register.capitalise', 'assets.reports.read'], { id: 'as1' });
    const f = TestBed.createComponent(AssetDetailPage);
    f.detectChanges();
    http.match((r) => r.url.endsWith('/m/assets/categories')).forEach((r) => r.flush(page([CATEGORY])));
    http.expectOne((r) => r.url.endsWith('/m/assets/register/as1')).flush(ASSET);
    http.expectOne((r) => r.url.endsWith('/m/assets/register/as1/schedule')).flush({ assetId: 'as1', number: 'FA-00001', name: 'Delivery van', cost: 12000, salvageValue: 2000, method: 'straightLine', lifeMonths: 60, status: 'draft', accumulatedDepreciation: 0, rows: [] });
    f.detectChanges();
    const root = f.nativeElement as HTMLElement;
    expect(root.textContent).toContain('FA-00001');
    expect(root.textContent).toContain('There is nothing to depreciate yet');
    const buttons = Array.from(root.querySelectorAll('button')).map((b) => b.textContent?.trim());
    expect(buttons.some((t) => t?.endsWith('Capitalise'))).toBeTrue();
    expect(buttons.some((t) => t?.endsWith('Dispose'))).toBeFalse();
    expect(buttons.some((t) => t?.endsWith('Edit'))).toBeFalse();

    const page$ = f.componentInstance as AssetDetailPage;
    page$.capitalise.show(ASSET as never);
    page$.capitalise.submit({ source: 'clearing', accountId: 'a9', date: '2026-02-01' });
    const call = http.expectOne((r) => r.url.endsWith('/m/assets/register/as1/capitalise'));
    expect(call.request.method).toBe('POST');
    expect(call.request.body).toEqual({ source: 'clearing', accountId: 'a9', date: '2026-02-01' });
    call.flush({ ...ASSET, status: 'capitalised' });
    http.expectOne((r) => r.url.endsWith('/m/assets/register/as1')).flush({ ...ASSET, status: 'capitalised', capitalisedOn: '2026-02-01' });
    http.match((r) => r.url.endsWith('/schedule')).forEach((r) => r.flush({ assetId: 'as1', number: 'FA-00001', name: 'x', cost: 1, salvageValue: 0, method: 'straightLine', lifeMonths: 1, status: 'capitalised', accumulatedDepreciation: 0, rows: [] }));
    f.detectChanges();
    expect(Array.from(root.querySelectorAll('button')).some((b) => b.textContent?.trim().endsWith('Dispose'))).toBe(false); // no dispose permission
  });

  it('runs depreciation for a legal entity and period', () => {
    setup(['assets.depreciation.read', 'assets.depreciation.run']);
    const f = TestBed.createComponent(DepreciationPage);
    f.detectChanges();
    http.expectOne((r) => r.url.endsWith('/m/assets/depreciation-runs')).flush(page([]));
    f.detectChanges();
    expect(el(f, 'depreciation-run')).not.toBeNull();
    f.componentInstance.runForm.show();
    f.componentInstance.runForm.submit({ legalEntityId: 'le1', year: 2026, month: 3 });
    const call = http.expectOne((r) => r.url.endsWith('/m/assets/depreciation-runs') && r.method === 'POST');
    expect(call.request.body).toEqual({ legalEntityId: 'le1', year: 2026, month: 3 });
    call.flush({ id: 'r1', legalEntityId: 'le1', year: 2026, month: 3, periodEnd: '2026-03-31', journalEntryId: 'j1', total: 166.67, assetCount: 1, runAt: '2026-04-01T00:00:00Z', runBy: null, entries: null, alreadyRun: false });
    http.expectOne((r) => r.url.endsWith('/m/assets/depreciation-runs') && r.method === 'GET').flush(page([]));
  });

  it('names the previous month for the run form', () => {
    expect(previousMonth(new Date(2026, 0, 15))).toEqual({ year: 2025, month: 12 });
    expect(previousMonth(new Date(2026, 5, 1))).toEqual({ year: 2026, month: 5 });
  });

  it('asks for the register report as of a date', () => {
    setup(['assets.reports.read']);
    const f = TestBed.createComponent(AssetReportsPage);
    f.detectChanges();
    http.match((r) => r.url.endsWith('/m/assets/categories')).forEach((r) => r.flush(page([CATEGORY])));
    const call = http.expectOne((r) => r.url.endsWith('/m/assets/reports/register'));
    expect(call.request.params.get('asOf')).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    call.flush({ asOf: '2026-03-31', rows: [{ assetId: 'as1', legalEntityId: 'le1', number: 'FA-00001', name: 'Delivery van', categoryId: 'c1', categoryName: 'Vehicles', businessUnitId: null, custodian: null, acquisitionDate: '2026-01-10', capitalisedOn: '2026-01-10', cost: 12000, salvageValue: 2000, accumulatedDepreciation: 500, netBookValue: 11500 }], totalCost: 12000, totalAccumulatedDepreciation: 500, totalNetBookValue: 11500 });
    f.detectChanges();
    expect((f.nativeElement as HTMLElement).textContent).toContain('11,500.00');
  });
});
