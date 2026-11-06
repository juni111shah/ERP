import { CartLine, cartTotals, changeDue, fromHeldOrder, lineAmounts, parseScan, quickCash, remainingDue, repriceHeld, returnAmount, round2, splitRefund, tendersSettle, toOrderLine, toPayments } from './pos-cart';
import { PosCatalogItemDto, PosOrderDto } from '../../core/api/pos.api';

function line(over: Partial<CartLine> = {}): CartLine {
  return { key: 'i', itemId: 'i', itemCode: 'A', description: 'A', quantity: 1, unitPrice: 100, listPrice: 100, discountPercent: 0, taxRate: 0, onHand: null, ...over };
}

describe('POS cart', () => {
  describe('line amounts', () => {
    it('takes the discount before tax', () => {
      expect(lineAmounts(line({ quantity: 2, discountPercent: 10, taxRate: 5 }))).toEqual({ gross: 200, discount: 20, net: 180, tax: 9, total: 189 });
    });

    it('rounds each step to cents as the server does', () => {
      const a = lineAmounts(line({ quantity: 3, unitPrice: 3.33, discountPercent: 10, taxRate: 5 }));
      expect(a.gross).toBe(9.99);
      expect(a.discount).toBe(1);
      expect(a.net).toBe(8.99);
      expect(a.tax).toBe(0.45);
      expect(a.total).toBe(9.44);
    });
  });

  describe('rounding, cent for cent with the server', () => {
    it('rounds halves up where binary floats fall just short', () => {
      expect(round2(1.005)).toBe(1.01);
      expect(round2(14.5 * 0.15)).toBe(2.18);
      expect(round2(80.3 * 0.05)).toBe(4.02);
      expect(round2(0.1 * 0.05)).toBe(0.01);
    });

    it('rounds halves away from zero when negative, and has no negative zero', () => {
      expect(round2(-2.675)).toBe(-2.68);
      expect(Object.is(round2(-0.001), 0)).toBeTrue();
    });

    it('charges what the server charges on the amounts that used to differ', () => {
      expect(lineAmounts(line({ unitPrice: 14.5, taxRate: 15 })).total).toBe(16.68);
      expect(lineAmounts(line({ unitPrice: 80.3, taxRate: 5 })).total).toBe(84.32);
    });
  });

  describe('return amounts', () => {
    const sold = (quantity: number, unitPrice: number, discountPercent: number, taxRate: number, returnedQuantity = 0, returnedAmount = 0) => ({
      quantity, unitPrice, discountPercent, taxRate, returnedQuantity, returnedAmount,
      lineTotal: lineAmounts({ quantity, unitPrice, discountPercent, taxRate }).total,
    });

    it('shares a rounded line out so the units add up to what was paid', () => {
      // The same figures the server test asserts (PosTillTests).
      expect(returnAmount(sold(3, 3.33, 10, 5), 1)).toBe(3.15);
      expect(returnAmount(sold(3, 3.33, 10, 5, 1, 3.15), 1)).toBe(3.14);
      expect(returnAmount(sold(3, 3.33, 10, 5, 2, 6.29), 1)).toBe(3.15);
    });

    it('stays right on very small prices', () => {
      expect(returnAmount(sold(4, 0.1, 0, 5), 1)).toBe(0.11);
      expect(returnAmount(sold(4, 0.1, 0, 5, 1, 0.11), 1)).toBe(0.1);
      expect(returnAmount(sold(4, 0.1, 0, 5, 2, 0.21), 1)).toBe(0.11);
      expect(returnAmount(sold(4, 0.1, 0, 5, 3, 0.32), 1)).toBe(0.1);
    });

    it('asks for nothing when nothing is chosen', () => {
      expect(returnAmount(sold(2, 10, 0, 0), 0)).toBe(0);
    });
  });

  describe('splitting a refund', () => {
    it('is one refund when the sale was fully paid', () => {
      expect(splitRefund(20, 'cash', 20, 0, 'cash', null)).toEqual([{ method: 'cash', amount: 20, reference: null }]);
    });

    it('sends what the money cannot take back to the account', () => {
      expect(splitRefund(100, 'cash', 60, 40, 'cash', null)).toEqual([{ method: 'cash', amount: 60, reference: null }, { method: 'account', amount: 40, reference: null }]);
    });

    it('sends what the account cannot take back as money', () => {
      expect(splitRefund(100, 'account', 60, 40, 'card', 'R1')).toEqual([{ method: 'card', amount: 60, reference: 'R1' }, { method: 'account', amount: 40, reference: null }]);
    });

    it('is all on the account when that covers it', () => {
      expect(splitRefund(30, 'account', 60, 40, 'cash', null)).toEqual([{ method: 'account', amount: 30, reference: null }]);
    });
  });

  it('adds lines up without floating point dust', () => {
    const t = cartTotals([line({ unitPrice: 0.1, key: 'a' }), line({ unitPrice: 0.2, key: 'b' })]);
    expect(t.total).toBe(0.3);
    expect(t.units).toBe(2);
  });

  describe('what is sent', () => {
    it('sends no price for a catalogue item at its list price', () => {
      expect(toOrderLine(line({ quantity: 2 }))).toEqual({ itemId: 'i', quantity: 2, discountPercent: 0 });
    });

    it('sends the price when it was overridden', () => {
      expect(toOrderLine(line({ unitPrice: 80 })).unitPrice).toBe(80);
    });

    it('sends everything for a typed-in item', () => {
      const sent = toOrderLine(line({ itemId: null, key: 'open-1', itemCode: 'MISC', description: 'Gift wrap', unitPrice: 5, listPrice: null, taxRate: 5 }));
      expect(sent).toEqual({ itemId: null, itemCode: 'MISC', description: 'Gift wrap', quantity: 1, unitPrice: 5, discountPercent: 0, taxRate: 5 });
    });
  });

  describe('a held ticket coming back', () => {
    const held = (unitPrice: number) => fromHeldOrder({ lines: [{ id: 'l', itemId: 'x', itemCode: 'X', description: 'X', quantity: 2, unitPrice, discountPercent: 5, taxRate: 5, lineTotal: 0, returnedQuantity: 0 }] } as unknown as PosOrderDto)[0];
    const item = (price: number | null) => ({ id: 'x', price, taxRate: 5, onHand: 7 }) as PosCatalogItemDto;

    it('invents no override at the catalogue price', () => {
      const line = repriceHeld(held(9), item(9), false);
      expect(line.quantity).toBe(2);
      expect(line.onHand).toBe(7);
      expect(toOrderLine(line).unitPrice).toBeUndefined();
    });

    it('keeps a price set by hand for someone who may set prices', () => {
      const line = repriceHeld(held(80), item(100), true);
      expect(line.unitPrice).toBe(80);
      expect(toOrderLine(line).unitPrice).toBe(80);
    });

    it('shows the catalogue price to someone who may not', () => {
      const line = repriceHeld(held(80), item(100), false);
      expect(line.unitPrice).toBe(100);
      expect(toOrderLine(line).unitPrice).toBeUndefined();
    });

    it('sends the held price when the item could not be looked up', () => {
      expect(toOrderLine(repriceHeld(held(80), null, false)).unitPrice).toBe(80);
    });
  });

  describe('scan entry', () => {
    it('reads a plain code as one', () => {
      expect(parseScan(' ABC-1 ')).toEqual({ quantity: 1, code: 'ABC-1' });
    });

    it('reads a quantity prefix', () => {
      expect(parseScan('0.12345*ABC').quantity).toBe(0.1235);
      expect(parseScan('3*ABC')).toEqual({ quantity: 3, code: 'ABC' });
      expect(parseScan('1.5 x ABC')).toEqual({ quantity: 1.5, code: 'ABC' });
    });

    it('leaves a code that only looks like one alone', () => {
      expect(parseScan('0*ABC')).toEqual({ quantity: 1, code: '0*ABC' });
      expect(parseScan('X100')).toEqual({ quantity: 1, code: 'X100' });
    });
  });

  it('offers the next round notes above what is due', () => {
    expect(quickCash(23.5)).toEqual([25, 30, 50, 100]);
    expect(quickCash(50)).toEqual([100, 500, 1000]);
    expect(quickCash(0)).toEqual([]);
  });

  describe('tenders', () => {
    it('counts what is still due', () => {
      expect(remainingDue(100, [{ method: 'card', amount: 60 }])).toBe(40);
      expect(remainingDue(100, [{ method: 'cash', amount: 120 }])).toBe(0);
    });

    it('gives change from cash only', () => {
      expect(changeDue(100, [{ method: 'cash', amount: 120 }])).toBe(20);
      expect(changeDue(100, [{ method: 'card', amount: 120 }])).toBe(0);
      expect(changeDue(100, [{ method: 'card', amount: 90 }, { method: 'cash', amount: 50 }])).toBe(40);
    });

    it('settles only when covered and any excess is cash', () => {
      expect(tendersSettle(100, [])).toBeFalse();
      expect(tendersSettle(100, [{ method: 'cash', amount: 99 }])).toBeFalse();
      expect(tendersSettle(100, [{ method: 'cash', amount: 100 }])).toBeTrue();
      expect(tendersSettle(100, [{ method: 'cash', amount: 500 }])).toBeTrue();
      expect(tendersSettle(100, [{ method: 'card', amount: 120 }])).toBeFalse();
    });

    it('sends amounts that add up to the total, with the change on the cash', () => {
      const payments = toPayments(100, [{ method: 'card', amount: 60, reference: 'A1' }, { method: 'cash', amount: 50 }]);
      expect(payments).toEqual([
        { method: 'card', amount: 60, tenderedAmount: 60, changeAmount: 0, reference: 'A1' },
        { method: 'cash', amount: 40, tenderedAmount: 50, changeAmount: 10, reference: null },
      ]);
      expect(payments.reduce((s, p) => s + p.amount, 0)).toBe(100);
    });
  });
});
