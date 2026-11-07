import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { of } from 'rxjs';
import { SessionStore } from '../../core/session/session.store';
import { APP_ICONS } from '../../icons';
import { IndustryDetailPage } from './industry-detail.page';
import { IndustryListPage } from './industry-list.page';
import { PACK, RECORD, meWith } from './industry.fixtures.spec';

function routeFor(params: Record<string, string>) {
  const paramMap = convertToParamMap(params);
  return { provide: ActivatedRoute, useValue: { snapshot: { paramMap }, paramMap: of(paramMap) } };
}

describe('Generic record list', () => {
  let fixture: ComponentFixture<IndustryListPage>;
  let http: HttpTestingController;
  const text = () => (fixture.nativeElement as HTMLElement).textContent ?? '';
  const el = (id: string) => (fixture.nativeElement as HTMLElement).querySelector(`[data-testid="${id}"]`) as HTMLElement | null;
  const open = (permissions: string[]) => {
    TestBed.inject(SessionStore).me.set(meWith(permissions));
    fixture = TestBed.createComponent(IndustryListPage);
    fixture.detectChanges();
    http.match('/api/v1/m/logistics/definitions').forEach((r) => r.flush(PACK));
    const list = http.expectOne((r) => r.url === '/api/v1/m/logistics/records/shipments');
    list.flush({ items: [RECORD], total: 1, page: 1, pageSize: 25, totalPages: 1, hasNext: false, hasPrevious: false });
    fixture.detectChanges();
    return list;
  };

  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [IndustryListPage],
      providers: [provideNoopAnimations(), provideHttpClient(), provideHttpClientTesting(), provideRouter([]), provideNzIcons(APP_ICONS), routeFor({ module: 'logistics', type: 'shipments' })],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => { fixture?.destroy(); document.querySelectorAll('.cdk-overlay-container').forEach((e) => (e.innerHTML = '')); });

  it('builds the columns from the definition: number, name, party, parent, listed fields, status, date and amount', () => {
    open(['logistics.shipments.read']);
    const headers = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('th')).map((th) => th.textContent?.trim() ?? '');
    for (const h of ['Number', 'Consignment', 'Customer', 'Vehicle', 'Origin', 'Weight', 'Fragile', 'Mode', 'Status', 'Date']) expect(headers.join('|')).withContext(h).toContain(h);
    expect(headers.join('|')).toContain('Amount (AED)');
    expect(headers.join('|')).not.toContain('Handling notes');
    expect(text()).toContain('SHP-000001');
    expect(text()).toContain('Gulf Traders');
    expect(text()).toContain('Doha');
    expect(el('industry-title')?.textContent).toContain('Shipments');
    expect(text()).toContain('Draft');
  });

  it('shows the Restricted note on a sensitive type', () => {
    open(['logistics.shipments.read']);
    expect(el('industry-restricted')).not.toBeNull();
  });

  it('shows New only to someone who may manage that type', () => {
    open(['logistics.shipments.read']);
    expect(el('industry-new')).toBeNull();
    fixture.destroy();
    open(['logistics.shipments.read', 'logistics.shipments.manage']);
    expect(el('industry-new')).not.toBeNull();
  });

  it('filters the list by status', () => {
    open(['logistics.shipments.read']);
    const page = fixture.componentInstance;
    page.filterStatus('delivered');
    const again = http.expectOne((r) => r.url === '/api/v1/m/logistics/records/shipments');
    expect(again.request.params.getAll('filter')).toEqual(['status:eq:delivered']);
    again.flush({ items: [], total: 0, page: 1, pageSize: 25, totalPages: 0, hasNext: false, hasPrevious: false });
  });
});

describe('Generic record detail', () => {
  let fixture: ComponentFixture<IndustryDetailPage>;
  let http: HttpTestingController;
  const root = () => fixture.nativeElement as HTMLElement;
  const button = (label: string) => Array.from(root().querySelectorAll('button')).find((b) => b.textContent?.trim().endsWith(label)) as HTMLButtonElement | undefined;
  const open = (permissions: string[], record = RECORD) => {
    TestBed.inject(SessionStore).me.set(meWith(permissions));
    fixture = TestBed.createComponent(IndustryDetailPage);
    fixture.detectChanges();
    http.match('/api/v1/m/logistics/definitions').forEach((r) => r.flush(PACK));
    http.expectOne('/api/v1/m/logistics/records/shipments/r1').flush(record);
    fixture.detectChanges();
  };
  const flushParties = () => http.match((r) => r.url.startsWith('/api/v1/')).filter((r) => !r.cancelled).forEach((r: TestRequest) => r.flush({ items: [], total: 0 }));

  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [IndustryDetailPage],
      providers: [provideNoopAnimations(), provideHttpClient(), provideHttpClientTesting(), provideRouter([]), provideNzIcons(APP_ICONS), routeFor({ module: 'logistics', type: 'shipments', id: 'r1' })],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => { fixture?.destroy(); document.querySelectorAll('.cdk-overlay-container').forEach((e) => (e.innerHTML = '')); });

  it('shows the fields, the lines, the history and the Restricted note', () => {
    open(['logistics.shipments.read']);
    flushParties();
    const text = root().textContent ?? '';
    expect(text).toContain('SHP-000001');
    expect(text).toContain('Doha');
    expect(text).toContain('Road freight');
    expect(root().querySelector('[data-testid="industry-history"]')?.textContent).toContain('Draft');
    expect(root().querySelector('[data-testid="industry-restricted"]')).not.toBeNull();
  });

  it('offers the next steps the server allows, only to someone who may manage the type', () => {
    open(['logistics.shipments.read']);
    flushParties();
    expect(button('Mark picked up')).toBeUndefined();
    fixture.destroy();

    open(['logistics.shipments.read', 'logistics.shipments.manage']);
    flushParties();
    expect(button('Mark picked up')).toBeDefined();
    expect(button('Cancel shipment')).toBeDefined();
    // Not delivered yet, so it cannot be invoiced.
    expect(button('Invoice')).toBeUndefined();
  });

  it('offers Invoice once the record is in a billable status', () => {
    open(['logistics.shipments.read', 'logistics.shipments.manage'], { ...RECORD, status: 'delivered', nextSteps: [] });
    flushParties();
    expect(button('Invoice')).toBeDefined();
    expect(button('Raise bill')).toBeUndefined();
    expect(button('Issue materials')).toBeUndefined();
  });

  it('moves the record when a step is taken, and shows the server\'s message when it refuses', () => {
    open(['logistics.shipments.read', 'logistics.shipments.manage']);
    flushParties();
    button('Mark picked up')!.click();
    const first = http.expectOne('/api/v1/m/logistics/records/shipments/r1/transition');
    expect(first.request.body).toEqual({ to: 'picked', reason: null });
    first.flush({ ...RECORD, status: 'picked', nextSteps: [] });
    fixture.detectChanges();
    expect(root().textContent).toContain('Picked up');
    expect(root().querySelector('[data-testid="industry-action-error"]')).toBeNull();

    fixture.destroy();
    open(['logistics.shipments.read', 'logistics.shipments.manage']);
    flushParties();
    button('Mark picked up')!.click();
    http.expectOne('/api/v1/m/logistics/records/shipments/r1/transition')
      .flush({ status: 409, title: 'Conflict', detail: "A record in status 'draft' cannot move to 'picked'." }, { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();
    expect(root().querySelector('[data-testid="industry-action-error"]')?.textContent).toContain("cannot move to 'picked'");
  });
});
