import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { PosOrderDto, PosOrdersApi } from '@core/api/pos.api';
import { SessionStore } from '@core/session/session.store';
import { APP_ICONS } from '../../icons';
import { meWith } from '@features/industry/industry.fixtures.spec';
import { AppliedPromo, cartSignature, CartLine, cartTotals } from './pos-cart';
import { PosReturnDialogComponent } from './components/pos-return-dialog/pos-return-dialog.component';

const line = (over: Partial<CartLine> = {}): CartLine => ({ key: 'i', itemId: 'i', itemCode: 'A', description: 'A', quantity: 1, unitPrice: 100, listPrice: 100, discountPercent: 0, taxRate: 0, onHand: null, ...over });

describe('POS promo code', () => {
  it('asks the server for a quote with the code and the ticket lines', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const http = TestBed.inject(HttpTestingController);
    TestBed.inject(PosOrdersApi).quote({ legalEntityId: 'le1', customerPartyId: null, lines: [{ itemId: 'i', quantity: 2 }], promoCode: 'SAVE10' }).subscribe();
    const call = http.expectOne((r) => r.url.endsWith('/m/pos/orders/quote'));
    expect(call.request.method).toBe('POST');
    expect(call.request.body).toEqual({ legalEntityId: 'le1', customerPartyId: null, lines: [{ itemId: 'i', quantity: 2 }], promoCode: 'SAVE10' });
  });

  it('puts the quoted share on each line so the payment total equals the server total', () => {
    const lines = [line({ key: 'a', quantity: 2, unitPrice: 50, taxRate: 10 }), line({ key: 'b', unitPrice: 30, taxRate: 10 })];
    const quote: AppliedPromo = { code: 'SAVE10', name: 'Ten off', discount: 13, sig: cartSignature(lines), lines: [{ unitPrice: 50, discountPercent: 0, taxRate: 10, promoDiscount: 10 }, { unitPrice: 30, discountPercent: 0, taxRate: 10, promoDiscount: 3 }] };
    const priced = lines.map((l, i) => ({ ...l, ...quote.lines[i] }));
    // Net is gross less the code, tax on that net: (100-10 + 30-3) = 117 net, 11.70 tax.
    expect(cartTotals(priced)).toEqual(jasmine.objectContaining({ gross: 130, promo: 13, net: 117, tax: 11.7, total: 128.7 }));
    expect(cartTotals(lines).total).toBe(143);
  });

  it('notices when the ticket changed after the quote', () => {
    const lines = [line({ quantity: 1 })];
    const sig = cartSignature(lines);
    expect(cartSignature([line({ quantity: 2 })])).not.toBe(sig);
    expect(cartSignature([line({ unitPrice: 90 })])).not.toBe(sig);
    expect(cartSignature([line()])).toBe(sig);
  });
});

describe('POS return refunds by original tender', () => {
  const order = (): PosOrderDto => ({
    id: 'o1', number: 'POS-1', status: 'completed', invoiceId: null, moneyLeftToRefund: 150, onAccountLeft: 0, lines: [], returnedTotal: 0,
    payments: [{ id: 'p1', method: 'card', amount: 100, tenderedAmount: 100, changeAmount: 0, paymentDate: '2026-07-01' }, { id: 'p2', method: 'cash', amount: 50, tenderedAmount: 60, changeAmount: 10, paymentDate: '2026-07-01' }],
  } as unknown as PosOrderDto);

  function open(permissions: string[]) {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideNoopAnimations(), provideNzIcons(APP_ICONS)] });
    TestBed.inject(SessionStore).me.set(meWith(permissions, ['pos']));
    const f = TestBed.createComponent(PosReturnDialogComponent);
    f.detectChanges();
    f.componentInstance.order.set(order());
    return f.componentInstance;
  }

  it('offers only the ways the sale was paid and shows what each took', () => {
    const c = open(['pos.orders.create']);
    expect(c.methods().sort()).toEqual(['card', 'cash']);
    expect(c.tenders()).toEqual([{ method: 'card', amount: 100 }, { method: 'cash', amount: 50 }]);
  });

  it('adds cash and card for someone with the refund override', () => {
    const c = open(['pos.orders.refund-override']);
    c.order.set({ ...order(), payments: [order().payments[0]] });
    expect(c.methods().sort()).toEqual(['card', 'cash']);
  });

  it('warns when a refund to one way is more than it took, unless the person may override', () => {
    const c = open(['pos.orders.create', 'pos.orders.refund']);
    c.method.set('cash');
    c.rows.set([{ line: { quantity: 1, unitPrice: 80, discountPercent: 0, taxRate: 0, lineTotal: 80, returnedQuantity: 0, returnedAmount: 0 } as never, left: 1, quantity: 1, restock: true }]);
    expect(c.total()).toBe(80);
    expect(c.overTender()).toBe(50);
    expect(c.canSubmit()).toBeFalse();
    c.method.set('card');
    expect(c.overTender()).toBeNull();
  });
});
