import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { statusTone } from '../../core/format/display';
import { SessionStore } from '../../core/session/session.store';
import { APP_ICONS } from '../../icons';
import { meWith } from '../industry/industry.fixtures.spec';
import { RequirementsPage } from './requirements.page';
import { WorkOrderDetailPage } from './work-order-detail.page';

const page = (items: unknown[]) => ({ items, total: items.length, page: 1, pageSize: 25, totalPages: 1, hasNext: false, hasPrevious: false });
const ORDER = (status: string, extra: Record<string, unknown> = {}) => ({
  id: 'w1', legalEntityId: 'le1', workOrderNumber: 'WO-1', bomCode: 'B1', bomId: 'b1', producedItemId: 'i9', producedItemCode: 'T', producedItemName: 'Table', plannedQuantity: 10, completedQuantity: 0, scrappedQuantity: 0, uom: 'pc',
  warehouseId: 'wh1', startDate: '2026-06-01', dueDate: '2026-06-30', status, estimatedTotalCost: 500, actualTotalCost: 0, createdAt: '2026-06-01T00:00:00Z', materialCost: 120, conversionCost: 30, scrapCost: 0,
  operations: [{ id: 'op1', workOrderId: 'w1', sequenceOrder: 1, operationName: 'Cut', workCenterName: 'Saw', plannedHours: 4, actualHours: 1, hourlyRate: 20, status: 'inProgress' }],
  materialIssues: [{ id: 'm1', itemId: 'i1', itemCode: 'WOOD', itemName: 'Wood', quantity: 5, unitCost: 24, value: 120, issueDate: '2026-06-02', backflushed: false, notes: null, reversedAt: null }], ...extra,
});

function setup(permissions: string[], params: Record<string, string> = { id: 'w1' }) {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), provideNoopAnimations(), provideRouter([]), provideNzIcons(APP_ICONS),
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap(params), queryParamMap: convertToParamMap({}) } } }],
  });
  TestBed.inject(SessionStore).me.set(meWith(permissions, ['manufacturing']));
  return TestBed.inject(HttpTestingController);
}
const load = (http: HttpTestingController, status: string, extra: Record<string, unknown> = {}) => {
  http.expectOne((r) => r.url.endsWith('/m/manufacturing/workorders/w1')).flush(ORDER(status, extra));
  http.match((r) => r.url.endsWith('/m/manufacturing/boms/b1')).forEach((r) => r.flush({ id: 'b1', components: [{ id: 'c1', componentItemId: 'i1', componentItemCode: 'WOOD', componentItemName: 'Wood', quantityRequired: 1 }] }));
};

describe('work order status', () => {
  it('reads Reversed as a status of its own', () => {
    expect(statusTone('reversed')).toBe('orange');
  });
});

describe('work order detail', () => {
  it('issues listed material, returns some, and records hours for an operation', () => {
    const http = setup(['manufacturing.workorders.read', 'manufacturing.workorders.manage']);
    const f = TestBed.createComponent(WorkOrderDetailPage);
    f.detectChanges();
    load(http, 'inProgress');
    f.detectChanges();
    const root = f.nativeElement as HTMLElement;
    expect(root.textContent).toContain('WOOD · Wood');
    expect(Array.from(root.querySelectorAll('button')).map((b) => b.textContent?.trim()).some((t) => t?.endsWith('Reverse'))).toBeFalse();

    f.componentInstance.issue.show();
    f.componentInstance.issue.submit({ lines: [{ itemId: 'i1', quantity: 3 }, { itemId: '', quantity: 0 }], date: '2026-06-03', notes: '' });
    const issue = http.expectOne((r) => r.url.endsWith('/w1/issue'));
    expect(issue.request.body).toEqual({ lines: [{ itemId: 'i1', quantity: 3 }], date: '2026-06-03', notes: null });
    issue.flush(ORDER('inProgress'));
    load(http, 'inProgress');

    f.componentInstance.giveBack.show();
    f.componentInstance.giveBack.submit({ lines: [{ itemId: 'i1', quantity: 1 }] });
    const back = http.expectOne((r) => r.url.endsWith('/w1/return-materials'));
    expect(back.request.body.lines).toEqual([{ itemId: 'i1', quantity: 1 }]);
    back.flush(ORDER('inProgress'));
    load(http, 'inProgress');

    f.componentInstance.setHours('op1', 3.5);
    f.componentInstance.saveHours(ORDER('inProgress').operations[0] as never);
    const hours = http.expectOne((r) => r.url.endsWith('/w1/operations/op1/hours'));
    expect(hours.request.method).toBe('PUT');
    expect(hours.request.body).toEqual({ actualHours: 3.5, status: null });
    hours.flush(ORDER('inProgress'));
    load(http, 'inProgress');
  });

  it('completes with a completion date and the hours entered', () => {
    const http = setup(['manufacturing.workorders.read', 'manufacturing.workorders.manage']);
    const f = TestBed.createComponent(WorkOrderDetailPage);
    f.detectChanges();
    load(http, 'inProgress');
    f.componentInstance.setHours('op1', 4);
    f.componentInstance.complete.show();
    f.componentInstance.complete.submit({ completedQuantity: 9, scrappedQuantity: 1, completionDate: '2026-06-20', notes: '' });
    const call = http.expectOne((r) => r.url.endsWith('/w1/complete'));
    expect(call.request.body.completionDate).toBe('2026-06-20');
    expect(call.request.body.operationHours).toEqual([{ operationId: 'op1', actualHours: 4 }]);
    expect(call.request.body.completedQuantity).toBe(9);
    call.flush(ORDER('completed'));
    load(http, 'completed');
  });

  it('offers Reverse on a completed order to someone who may, and shows the reversal note and no actions once reversed', () => {
    const http = setup(['manufacturing.workorders.read', 'manufacturing.workorders.reverse']);
    const f = TestBed.createComponent(WorkOrderDetailPage);
    f.detectChanges();
    load(http, 'completed', { completedQuantity: 10 });
    f.detectChanges();
    const labels = () => Array.from((f.nativeElement as HTMLElement).querySelectorAll('button')).map((b) => b.textContent?.trim() ?? '');
    expect(labels().some((t) => t.endsWith('Reverse'))).toBeTrue();
    f.componentInstance.order.set(ORDER('reversed', { reversedAt: '2026-07-01T00:00:00Z', reversalReason: 'Wrong count' }) as never);
    f.detectChanges();
    expect(labels().some((t) => t.endsWith('Reverse'))).toBeFalse();
    expect((f.nativeElement as HTMLElement).querySelector('[data-testid="wo-reversed"]')?.textContent).toContain('Wrong count');
  });
});

describe('material requirements', () => {
  it('asks with a what-if item only when item and quantity are both given', () => {
    const http = setup(['manufacturing.workorders.read']);
    const f = TestBed.createComponent(RequirementsPage);
    f.detectChanges();
    http.match((r) => r.url.endsWith('/m/inventory/warehouses')).forEach((r) => r.flush(page([])));
    http.match((r) => r.url.endsWith('/items')).forEach((r) => r.flush(page([])));
    const first = http.expectOne((r) => r.url.endsWith('/m/manufacturing/requirements'));
    expect(first.request.params.has('itemId')).toBeFalse();
    first.flush({ legalEntityId: 'le1', openWorkOrders: 1, openPurchaseNetted: false, rows: [{ itemId: 'i1', itemCode: 'WOOD', itemName: 'Wood', uom: 'm', level: 1, isManufactured: false, grossRequired: 50, onHand: 20, onOrder: 0, inProduction: 0, netRequired: 30 }], shortages: [{ itemId: 'i1' }] });
    f.detectChanges();
    expect((f.nativeElement as HTMLElement).querySelector('[data-testid="req-summary"]')?.textContent).toContain('1 items short');
    f.componentInstance.itemId.set('i9');
    f.componentInstance.run(); // quantity missing: still no what-if
    expect(http.expectOne((r) => r.url.endsWith('/m/manufacturing/requirements')).request.params.has('itemId')).toBeFalse();
    f.componentInstance.quantity.set(5);
    f.componentInstance.run();
    const withItem = http.expectOne((r) => r.url.endsWith('/m/manufacturing/requirements'));
    expect(withItem.request.params.get('itemId')).toBe('i9');
    expect(withItem.request.params.get('quantity')).toBe('5');
  });
});
