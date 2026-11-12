import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { SessionStore } from '@core/session/session.store';
import { APP_ICONS } from '../../icons';
import { meWith } from '@features/industry/industry.fixtures.spec';
import { PurchaseOrderDetailPage } from '@features/purchasing/pages/purchase-order-detail/purchase-order-detail.page';
import { ReturnsPage } from './pages/returns/returns.page';

const page = (items: unknown[]) => ({ items, total: items.length, page: 1, pageSize: 25, totalPages: 1, hasNext: false, hasPrevious: false });

function setup(permissions: string[], kind: 'sales' | 'purchase', query: Record<string, string> = {}, params: Record<string, string> = {}) {
  // Row actions go behind a menu without a hover pointer, as in a headless browser; these specs are about which controls exist.
  spyOn(window, 'matchMedia').and.returnValue({ matches: true, addEventListener() {}, removeEventListener() {} } as unknown as MediaQueryList);
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), provideNoopAnimations(), provideRouter([]), provideNzIcons(APP_ICONS),
      { provide: ActivatedRoute, useValue: { snapshot: { data: { kind }, queryParamMap: convertToParamMap(query), paramMap: convertToParamMap(params) }, paramMap: { subscribe: (f: (p: unknown) => void) => f(convertToParamMap(params)) } } }],
  });
  TestBed.inject(SessionStore).me.set(meWith(permissions, ['sales', 'purchasing']));
  return TestBed.inject(HttpTestingController);
}

describe('sales returns', () => {
  it('lists returns and shows Post on a draft and Void on a posted one', () => {
    const http = setup(['sales.returns.read', 'sales.returns.manage'], 'sales');
    const f = TestBed.createComponent(ReturnsPage);
    f.detectChanges();
    const row = (id: string, status: string) => ({ id, number: 'SR-' + id, legalEntityId: 'le1', salesOrderId: 'o1', orderNumber: 'SO-1', invoiceId: null, invoiceNumber: null, customerPartyId: 'c1', customerName: 'Gulf Traders', returnDate: '2026-07-01', status, creditNoteId: null, creditNoteNumber: null, totalAmount: 50, lineCount: 1, createdAt: '2026-07-01T00:00:00Z' });
    http.expectOne((r) => r.url.endsWith('/m/sales/returns')).flush(page([row('1', 'draft'), row('2', 'posted')]));
    f.detectChanges();
    const root = f.nativeElement as HTMLElement;
    // Wide screens show actions on hover; under the test the kebab menu hides them, so check what the component would render by opening the menus.
    root.querySelectorAll<HTMLElement>('[data-testid="row-actions-menu"]').forEach((b) => b.click());
    f.detectChanges();
    const overlay = document.body.textContent ?? '';
    expect(overlay).toContain('Post');
    expect(overlay).toContain('Void');
    document.querySelectorAll('.cdk-overlay-container').forEach((e) => (e.innerHTML = ''));
  });

  it('starts from an order, offers only what was delivered and not returned, and sends restock per line', () => {
    const http = setup(['sales.returns.read', 'sales.returns.manage'], 'sales', { order: 'o1' });
    const f = TestBed.createComponent(ReturnsPage);
    f.detectChanges();
    http.expectOne((r) => r.url.endsWith('/m/sales/returns')).flush(page([]));
    http.expectOne((r) => r.url.endsWith('/m/sales/orders/o1')).flush({
      id: 'o1', number: 'SO-1', lines: [
        { id: 'l1', description: 'Chair', quantity: 10, deliveredQuantity: 8, returnedQuantity: 3, unitOfMeasure: 'pc' },
        { id: 'l2', description: 'Desk', quantity: 2, deliveredQuantity: 0, returnedQuantity: 0, unitOfMeasure: 'pc' },
      ],
    });
    f.detectChanges();
    expect(f.componentInstance.source()?.lines).toEqual([{ key: 'l1', label: 'Chair', max: 5, unit: 'pc' }]);
    expect(f.componentInstance.formOpen()).toBeTrue();
    f.componentInstance.create({ returnDate: '2026-07-02', reason: 'Damaged', notes: null, warehouseId: null, lines: [{ key: 'l1', quantity: 2, restock: false }] });
    const post = http.expectOne((r) => r.url.endsWith('/m/sales/returns') && r.method === 'POST');
    expect(post.request.body).toEqual({ salesOrderId: 'o1', invoiceId: null, returnDate: '2026-07-02', reason: 'Damaged', notes: null, lines: [{ orderLineId: 'l1', invoiceLineId: null, quantity: 2, restock: false }] });
    post.flush({ id: 'r1', number: 'SR-1' });
    http.expectOne((r) => r.url.endsWith('/m/sales/returns') && r.method === 'GET').flush(page([]));
  });
});

describe('purchase returns', () => {
  it('raises a return against a bill by bill line and a warehouse for an order', () => {
    const http = setup(['purchasing.returns.read', 'purchasing.returns.manage'], 'purchase', { bill: 'b1' });
    const f = TestBed.createComponent(ReturnsPage);
    f.detectChanges();
    http.match((r) => r.url.endsWith('/m/inventory/warehouses')).forEach((r) => r.flush(page([])));
    http.expectOne((r) => r.url.endsWith('/m/purchasing/returns')).flush(page([]));
    http.expectOne((r) => r.url.endsWith('/finance/bills/b1')).flush({ id: 'b1', number: 'BL-1', lines: [{ id: 'bl1', description: 'Cable', quantity: 4, price: 5 }] });
    expect(f.componentInstance.source()?.type).toBe('bill');
    f.componentInstance.create({ returnDate: '2026-07-02', reason: 'Faulty', notes: null, warehouseId: null, lines: [{ key: 'bl1', quantity: 1, restock: true }] });
    const post = http.expectOne((r) => r.url.endsWith('/m/purchasing/returns') && r.method === 'POST');
    expect(post.request.body.billId).toBe('b1');
    expect(post.request.body.lines).toEqual([{ orderLineId: null, billLineId: 'bl1', quantity: 1 }]);
  });
});

describe('purchase order convert to bill', () => {
  it('bills the chosen quantities, overrides only changed prices and sends allowUnreceived explicitly', () => {
    const http = setup(['purchasing.orders.read', 'purchasing.orders.manage'], 'purchase', {}, { id: 'po1' });
    const f = TestBed.createComponent(PurchaseOrderDetailPage);
    f.detectChanges();
    http.match((r) => r.url.endsWith('/m/inventory/warehouses')).forEach((r) => r.flush(page([])));
    http.expectOne((r) => r.url.endsWith('/m/purchasing/orders/po1')).flush({
      id: 'po1', number: 'PO-1', legalEntityId: 'le1', vendorPartyId: 'v1', vendorName: 'Acme', orderDate: '2026-06-01', currency: 'AED', status: 'partiallyReceived', subTotal: 0, taxTotal: 0, discountTotal: 0, totalAmount: 0, lineCount: 2, billIds: [],
      lines: [
        { id: 'l1', itemCode: 'A', description: 'Cable', quantity: 10, receivedQuantity: 6, billedQuantity: 2, unitOfMeasure: 'pc', unitPrice: 5, discountPercent: 0, taxRate: 0, lineTotal: 50 },
        { id: 'l2', itemCode: 'B', description: 'Plug', quantity: 4, receivedQuantity: 0, billedQuantity: 0, unitOfMeasure: 'pc', unitPrice: 2, discountPercent: 0, taxRate: 0, lineTotal: 8 },
      ],
    });
    f.detectChanges();
    const p = f.componentInstance;
    expect(p.canBill()).toBeTrue();
    p.openBillModal();
    // Received and not yet billed is the default: 6 - 2 = 4 of the cable, nothing of the plug.
    expect(p.billDraft().map((r) => r.quantity)).toEqual([4, 0]);
    p.setBill(0, 'price', 4.5);
    expect(p.billRequest()).toEqual({ lines: [{ lineId: 'l1', quantity: 4 }], allowUnreceived: false, unitPriceOverrides: { l1: 4.5 } });
    p.allowUnreceived.set(true);
    p.setBill(1, 'quantity', 4);
    expect(p.billRequest().allowUnreceived).toBeTrue();
    expect(p.billRequest().lines).toEqual([{ lineId: 'l1', quantity: 4 }, { lineId: 'l2', quantity: 4 }]);
    p.saveBill();
    const call = http.expectOne((r) => r.url.endsWith('/m/purchasing/orders/po1/convert-to-bill'));
    expect(call.request.body.allowUnreceived).toBeTrue();
    call.flush({});
  });
});
