import 'package:erp_mobile/features/ops/pos_cart.dart';
import 'package:flutter_test/flutter_test.dart';

/// The same cases as frontend/src/app/pages/pos/pos-cart.spec.ts, so the web and the app cannot drift apart unnoticed.
CartLine line({String? itemId = 'i', String key = 'i', String code = 'A', String description = 'A', double quantity = 1, double unitPrice = 100, double? listPrice = 100, double discountPercent = 0, double taxRate = 0}) =>
    CartLine(key: key, itemId: itemId, itemCode: code, description: description, quantity: quantity, unitPrice: unitPrice, listPrice: listPrice, discountPercent: discountPercent, taxRate: taxRate);

void main() {
  group('line amounts', () {
    test('takes the discount before tax', () {
      final a = amountsOf(line(quantity: 2, discountPercent: 10, taxRate: 5));
      expect([a.gross, a.discount, a.net, a.tax, a.total], [200, 20, 180, 9, 189]);
    });

    test('rounds each step to cents as the server does', () {
      final a = amountsOf(line(quantity: 3, unitPrice: 3.33, discountPercent: 10, taxRate: 5));
      expect([a.gross, a.discount, a.net, a.tax, a.total], [9.99, 1, 8.99, 0.45, 9.44]);
    });
  });

  group('rounding, cent for cent with the server', () {
    test('rounds halves up where binary floats fall just short', () {
      expect(round2(1.005), 1.01);
      expect(round2(14.5 * 0.15), 2.18);
      expect(round2(80.3 * 0.05), 4.02);
      expect(round2(0.1 * 0.05), 0.01);
    });

    test('rounds halves away from zero when negative, and has no negative zero', () {
      expect(round2(-2.675), -2.68);
      expect(round2(-0.001).isNegative, isFalse);
    });

    test('charges what the server charges on the amounts that used to differ', () {
      expect(amountsOf(line(unitPrice: 14.5, taxRate: 15)).total, 16.68);
      expect(amountsOf(line(unitPrice: 80.3, taxRate: 5)).total, 84.32);
    });

    test('keeps quantities to four decimals', () {
      expect(roundQty(0.1 + 0.2), 0.3);
      expect(roundQty(0.12345), 0.1235);
    });
  });

  group('return amounts', () {
    Map<String, dynamic> sold(double quantity, double unitPrice, double discountPercent, double taxRate, [double returnedQuantity = 0, double returnedAmount = 0]) => {
          'quantity': quantity,
          'unitPrice': unitPrice,
          'discountPercent': discountPercent,
          'taxRate': taxRate,
          'returnedQuantity': returnedQuantity,
          'returnedAmount': returnedAmount,
          'lineTotal': lineAmounts(quantity: quantity, unitPrice: unitPrice, discountPercent: discountPercent, taxRate: taxRate).total,
        };

    test('shares a rounded line out so the units add up to what was paid', () {
      // The same figures the server test asserts (PosTillTests).
      expect(returnAmount(sold(3, 3.33, 10, 5), 1), 3.15);
      expect(returnAmount(sold(3, 3.33, 10, 5, 1, 3.15), 1), 3.14);
      expect(returnAmount(sold(3, 3.33, 10, 5, 2, 6.29), 1), 3.15);
    });

    test('stays right on very small prices', () {
      expect(returnAmount(sold(4, 0.1, 0, 5), 1), 0.11);
      expect(returnAmount(sold(4, 0.1, 0, 5, 1, 0.11), 1), 0.1);
      expect(returnAmount(sold(4, 0.1, 0, 5, 2, 0.21), 1), 0.11);
      expect(returnAmount(sold(4, 0.1, 0, 5, 3, 0.32), 1), 0.1);
    });

    test('asks for nothing when nothing is chosen', () {
      expect(returnAmount(sold(2, 10, 0, 0), 0), 0);
    });

    test('reads the whole numbers JSON gives for round amounts', () {
      expect(returnAmount({'quantity': 2, 'unitPrice': 10, 'discountPercent': 0, 'taxRate': 0, 'lineTotal': 20, 'returnedQuantity': 0, 'returnedAmount': 0}, 1), 10);
    });
  });

  group('splitting a refund', () {
    test('is one refund when the sale was fully paid', () {
      expect(splitRefund(20, 'cash', 20, 0, 'cash', null), [
        {'method': 'cash', 'amount': 20, 'reference': null},
      ]);
    });

    test('sends what the money cannot take back to the account', () {
      expect(splitRefund(100, 'cash', 60, 40, 'cash', null), [
        {'method': 'cash', 'amount': 60, 'reference': null},
        {'method': 'account', 'amount': 40, 'reference': null},
      ]);
    });

    test('sends what the account cannot take back as money', () {
      expect(splitRefund(100, 'account', 60, 40, 'card', 'R1'), [
        {'method': 'card', 'amount': 60, 'reference': 'R1'},
        {'method': 'account', 'amount': 40, 'reference': null},
      ]);
    });

    test('is all on the account when that covers it', () {
      expect(splitRefund(30, 'account', 60, 40, 'cash', null), [
        {'method': 'account', 'amount': 30, 'reference': null},
      ]);
    });
  });

  group('how the money can go back', () {
    Map<String, dynamic> order(List<String> paid, {String? invoiceId}) => {
          'invoiceId': invoiceId,
          'payments': [
            for (final m in paid) {'method': m},
          ],
        };

    test('offers cash, card and how the sale was paid, never a gift card', () {
      expect(refundMethods(order(['other', 'giftCard'])), ['cash', 'card', 'other']);
    });

    test('offers the account only when the sale made an invoice', () {
      expect(refundMethods(order(['account'])), isNot(contains('account')));
      expect(refundMethods(order(['account'], invoiceId: 'inv')), contains('account'));
    });

    test('starts on the way the sale was paid', () {
      expect(defaultRefundMethod(order(['card', 'cash'])), 'card');
      expect(defaultRefundMethod(order(['giftCard'])), 'cash');
      expect(defaultRefundMethod(order(['cash', 'account'], invoiceId: 'inv')), 'account');
      expect(moneyRefundMethod(order(['account', 'card'], invoiceId: 'inv')), 'card');
      expect(moneyRefundMethod(order(['account'], invoiceId: 'inv')), 'cash');
    });
  });

  test('adds lines up without floating point dust', () {
    final t = cartTotals([line(unitPrice: 0.1, key: 'a'), line(unitPrice: 0.2, key: 'b')]);
    expect(t.total, 0.3);
    expect(t.units, 2);
  });

  group('what is sent', () {
    test('sends no price for a catalogue item at its list price', () {
      expect(toOrderLine(line(quantity: 2)), {'itemId': 'i', 'quantity': 2, 'discountPercent': 0});
    });

    test('sends the price when it was overridden', () {
      expect(toOrderLine(line(unitPrice: 80))['unitPrice'], 80);
    });

    test('sends everything for a typed-in item', () {
      final sent = toOrderLine(line(itemId: null, key: 'open-1', code: 'MISC', description: 'Gift wrap', unitPrice: 5, listPrice: null, taxRate: 5));
      expect(sent, {'itemId': null, 'itemCode': 'MISC', 'description': 'Gift wrap', 'quantity': 1, 'unitPrice': 5, 'discountPercent': 0, 'taxRate': 5});
    });
  });

  group('a held ticket coming back', () {
    CartLine held(double unitPrice) => fromHeldOrder({
          'lines': [
            {'id': 'l', 'itemId': 'x', 'itemCode': 'X', 'description': 'X', 'quantity': 2, 'unitPrice': unitPrice, 'discountPercent': 5, 'taxRate': 5, 'lineTotal': 0, 'returnedQuantity': 0},
          ],
        }).single;
    Map<String, dynamic> item(num? price) => {'id': 'x', 'price': price, 'taxRate': 5, 'onHand': 7};

    test('invents no override at the catalogue price', () {
      final l = repriceHeld(held(9), item(9), canOverridePrice: false);
      expect(l.quantity, 2);
      expect(l.onHand, 7);
      expect(toOrderLine(l).containsKey('unitPrice'), isFalse);
    });

    test('keeps a price set by hand for someone who may set prices', () {
      final l = repriceHeld(held(80), item(100), canOverridePrice: true);
      expect(l.unitPrice, 80);
      expect(toOrderLine(l)['unitPrice'], 80);
    });

    test('shows the catalogue price to someone who may not', () {
      final l = repriceHeld(held(80), item(100), canOverridePrice: false);
      expect(l.unitPrice, 100);
      expect(toOrderLine(l).containsKey('unitPrice'), isFalse);
    });

    test('sends the held price when the item could not be looked up', () {
      expect(toOrderLine(repriceHeld(held(80), null, canOverridePrice: false))['unitPrice'], 80);
    });

    test('keeps the discount it was held with', () {
      expect(toOrderLine(repriceHeld(held(9), item(9), canOverridePrice: false))['discountPercent'], 5);
    });
  });

  group('scan entry', () {
    test('reads a plain code as one', () {
      final s = parseScan(' ABC-1 ');
      expect([s.quantity, s.code], [1, 'ABC-1']);
    });

    test('reads a quantity prefix', () {
      expect(parseScan('0.12345*ABC').quantity, 0.1235);
      final three = parseScan('3*ABC');
      expect([three.quantity, three.code], [3, 'ABC']);
      final decimal = parseScan('1.5 x ABC');
      expect([decimal.quantity, decimal.code], [1.5, 'ABC']);
    });

    test('leaves a code that only looks like one alone', () {
      final zero = parseScan('0*ABC');
      expect([zero.quantity, zero.code], [1, '0*ABC']);
      final x = parseScan('X100');
      expect([x.quantity, x.code], [1, 'X100']);
    });
  });

  test('offers the next round notes above what is due', () {
    expect(quickCash(23.5), [25, 30, 50, 100]);
    expect(quickCash(50), [100, 500, 1000]);
    expect(quickCash(0), isEmpty);
  });

  group('tenders', () {
    test('counts what is still due', () {
      expect(remainingDue(100, const [Tender('card', 60)]), 40);
      expect(remainingDue(100, const [Tender('cash', 120)]), 0);
    });

    test('gives change from cash only', () {
      expect(changeDue(100, const [Tender('cash', 120)]), 20);
      expect(changeDue(100, const [Tender('card', 120)]), 0);
      expect(changeDue(100, const [Tender('card', 90), Tender('cash', 50)]), 40);
    });

    test('settles only when covered and any excess is cash', () {
      expect(tendersSettle(100, const []), isFalse);
      expect(tendersSettle(100, const [Tender('cash', 99)]), isFalse);
      expect(tendersSettle(100, const [Tender('cash', 100)]), isTrue);
      expect(tendersSettle(100, const [Tender('cash', 500)]), isTrue);
      expect(tendersSettle(100, const [Tender('card', 120)]), isFalse);
    });

    test('sends amounts that add up to the total, with the change on the cash', () {
      final payments = toPayments(100, const [Tender('card', 60, 'A1'), Tender('cash', 50)]);
      expect(payments, [
        {'method': 'card', 'amount': 60, 'tenderedAmount': 60, 'changeAmount': 0, 'reference': 'A1'},
        {'method': 'cash', 'amount': 40, 'tenderedAmount': 50, 'changeAmount': 10, 'reference': null},
      ]);
      expect(payments.fold<num>(0, (s, p) => s + (p['amount'] as num)), 100);
    });
  });

  group('discount limit', () {
    const settings = {'maxDiscountPercent': 50, 'cashierDiscountPercent': 10};

    test('holds a cashier to the cashier limit', () {
      expect(discountLimit(settings, canOverride: false), 10);
    });

    test('lets someone with the override go to the maximum', () {
      expect(discountLimit(settings, canOverride: true), 50);
    });

    test('never lets the cashier limit exceed the maximum', () {
      expect(discountLimit(const {'maxDiscountPercent': 5, 'cashierDiscountPercent': 10}, canOverride: false), 5);
    });

    test('is the maximum when no cashier limit is set', () {
      expect(discountLimit(const {'maxDiscountPercent': 20, 'cashierDiscountPercent': null}, canOverride: false), 20);
    });
  });
}
