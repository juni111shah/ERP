import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:erp_mobile/core/api_client.dart';
import 'package:erp_mobile/core/key_value_store.dart';
import 'package:erp_mobile/core/me.dart';
import 'package:erp_mobile/core/session.dart';
import 'package:erp_mobile/features/ops/pos_cart.dart';
import 'package:erp_mobile/features/ops/pos_screens.dart';
import 'package:erp_mobile/features/receivables/party_screens.dart';
import 'package:erp_mobile/kit/field.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/fake_adapter.dart';

const _admin = Me(fullName: 'A', email: 'a@x', baseCurrency: 'QAR', countryCode: 'QA', permissions: {'*'}, legalEntities: [], modules: {});

const _cashier = Me(fullName: 'C', email: 'c@x', baseCurrency: 'QAR', countryCode: 'QA', permissions: {'pos.orders.create', 'pos.orders.read', 'pos.sessions.read', 'pos.sessions.manage'}, legalEntities: [], modules: {});

const _session = {'id': 's1', 'registerId': 'r1', 'legalEntityId': 'l1', 'registerName': 'Front counter', 'cashierName': 'A', 'openingCash': 100, 'totalSales': 0, 'totalReturns': 0, 'expectedCash': 100, 'status': 'open'};

const _pen = {'id': 'pen', 'sku': 'PEN', 'barcode': '111', 'name': 'Pen', 'uom': 'ea', 'type': 'product', 'price': 14.5, 'taxRate': 15, 'onHand': 9};

Widget host(FakeAdapter adapter, Widget child, {Me me = _admin}) {
  final store = MemoryKeyValueStore();
  return ProviderScope(
    key: UniqueKey(),
    overrides: [
      storeProvider.overrideWithValue(store),
      meOrEmptyProvider.overrideWithValue(me),
      apiProvider.overrideWith((ref) => ApiClient(store: store, baseUrl: 'http://api.test', dio: Dio()..httpClientAdapter = adapter)),
    ],
    child: MaterialApp(home: child),
  );
}

Map<String, dynamic> _body(RequestOptions o) => o.data is String ? jsonDecode(o.data as String) as Map<String, dynamic> : o.data as Map<String, dynamic>;

/// A scripted till. Anything not named answers 404, so a wrong address fails the test.
FakeAdapter till({List<Map<String, dynamic>> held = const [], Map<String, dynamic>? order, ResponseBody Function(RequestOptions)? onPost}) => FakeAdapter((o) {
      final path = o.path;
      if (o.method == 'GET') {
        if (path.endsWith('/terminal')) {
          return envelope({
            'registers': [
              {'id': 'r1', 'legalEntityId': 'l1', 'code': 'R1', 'name': 'Front counter', 'openSessionId': 's1'},
            ],
            'settings': {'maxDiscountPercent': 50, 'cashierDiscountPercent': 10, 'allowPartialPayment': false, 'allowNegativeDrawer': false, 'allowNegativeStock': true, 'enforceFloatAtOpen': true},
          });
        }
        if (path.endsWith('/terminal/session')) return envelope(_session);
        if (path.endsWith('/terminal/held')) return envelope(held);
        if (path.endsWith('/terminal/catalog')) {
          final q = o.queryParameters;
          final hit = q['code'] == 'PEN' || q['code'] == '111' || '${q['search'] ?? ''}'.toLowerCase().contains('pen') || (q['code'] == null && q['search'] == null);
          return envelope({
            'items': hit ? [_pen] : [],
            'total': hit ? 1 : 0,
          });
        }
        if (path.endsWith('/terminal/customers')) {
          return envelope([
            {'id': 'c1', 'code': 'C-1', 'name': 'Acme Trading', 'phone': '555'},
          ]);
        }
        if (path.contains('/orders/by-number/') && order != null) return envelope(order);
      }
      if (o.method == 'DELETE') return envelope(null, status: 200);
      if (o.method == 'POST' && onPost != null) return onPost(o);
      return problem(404, 'No such address: ${o.method} $path');
    });

Future<void> scan(WidgetTester t, String code) async {
  await t.enterText(find.byKey(const ValueKey('pos-scan')), code);
  await t.testTextInput.receiveAction(TextInputAction.search);
  await t.pumpAndSettle();
}

ResponseBody _sold(RequestOptions o) => envelope({
      'id': 'o1',
      'number': 'POS-1',
      'totalAmount': 16.68,
      'payments': _body(o)['payments'],
    }, status: 201);

void main() {
  group('terminal', () {
    testWidgets('starts from the till endpoints, which ask only for the right to sell', (t) async {
      final adapter = till(held: const [
        {'id': 'h1', 'customerName': 'Walk-in Customer', 'cashierName': 'A', 'orderDate': '2026-11-06', 'totalAmount': 10, 'lines': []},
      ]);
      await t.pumpWidget(host(adapter, const PosTerminalScreen(), me: _cashier));
      await t.pumpAndSettle();
      final paths = adapter.requests.map((o) => o.path).toList();
      expect(paths, everyElement(contains('/terminal')));
      expect(find.text('Held sales (1)'), findsOneWidget);
      expect(find.byKey(const ValueKey('pos-new-sale')), findsOneWidget);
    });

    testWidgets('offers a return only to someone who may give refunds', (t) async {
      await t.pumpWidget(host(till(), const PosTerminalScreen(), me: _cashier));
      await t.pumpAndSettle();
      expect(find.byKey(const ValueKey('pos-terminal-return')), findsNothing);
      await t.pumpWidget(host(till(), const PosTerminalScreen()));
      await t.pumpAndSettle();
      expect(find.byKey(const ValueKey('pos-terminal-return')), findsOneWidget);
    });
  });

  group('ticket', () {
    testWidgets('a scan goes on the ticket and is charged what the server charges', (t) async {
      late Map<String, dynamic> sent;
      late RequestOptions post;
      final adapter = till(onPost: (o) {
        sent = _body(o);
        post = o;
        return _sold(o);
      });
      await t.pumpWidget(host(adapter, const PosSaleScreen(session: _session)));
      await t.pumpAndSettle();
      await scan(t, '111');
      expect(find.text('Pen'), findsOneWidget);
      // 14.50 + 15% is 16.68 only with the rounding the server uses.
      expect(find.textContaining('16.68'), findsWidgets);

      await t.tap(find.byKey(const ValueKey('pos-pay')));
      await t.pumpAndSettle();
      await t.tap(find.byKey(const ValueKey('pos-confirm-payment')));
      await t.pumpAndSettle();

      expect((sent['lines'] as List).single, {'itemId': 'pen', 'quantity': 1.0, 'discountPercent': 0.0});
      expect((sent['payments'] as List).single['amount'], 16.68);
      expect(sent['sessionId'], 's1');
      expect(sent['parkedOrderId'], isNull);
      expect(post.headers['Idempotency-Key'], isNotNull);
      expect(find.text('Sale POS-1'), findsOneWidget);
    });

    testWidgets('a repeat scan and a quantity prefix add up on one line', (t) async {
      final adapter = till();
      await t.pumpWidget(host(adapter, const PosSaleScreen(session: _session)));
      await t.pumpAndSettle();
      await scan(t, '3*PEN');
      expect(find.text('3'), findsOneWidget);
      final before = adapter.requests.length;
      await scan(t, 'pen');
      expect(find.text('4'), findsOneWidget);
      expect(adapter.requests.length, before, reason: 'a code the ticket has seen needs no round trip');
    });

    testWidgets('an unknown code says so and stays in the box', (t) async {
      await t.pumpWidget(host(till(), const PosSaleScreen(session: _session), me: _cashier));
      await t.pumpAndSettle();
      await scan(t, 'NOPE');
      expect(find.text('No product matches "NOPE".'), findsOneWidget);
      expect(find.widgetWithText(TextField, 'NOPE'), findsOneWidget);
    });

    testWidgets('holding sends the ticket with no money, and a named customer by id', (t) async {
      late Map<String, dynamic> parked;
      final adapter = till(onPost: (o) {
        parked = _body(o);
        return o.path.endsWith('/orders/park') ? envelope({'id': 'h1'}, status: 201) : problem(404, 'wrong address');
      });
      await t.pumpWidget(host(adapter, const PosSaleScreen(session: _session)));
      await t.pumpAndSettle();
      await scan(t, 'PEN');
      await t.tap(find.byKey(const ValueKey('pos-customer')));
      await t.pumpAndSettle();
      await t.tap(find.byKey(const ValueKey('pos-customer-find')));
      await t.pumpAndSettle();
      await t.tap(find.text('Acme Trading'));
      await t.pumpAndSettle();
      await t.tap(find.byKey(const ValueKey('pos-hold')));
      await t.pumpAndSettle();
      expect(parked['customerPartyId'], 'c1');
      expect(parked['customerName'], isNull);
      expect(parked.containsKey('payments'), isFalse);
      expect(find.text('Sale held.'), findsOneWidget);
      expect(find.text('Empty ticket'), findsOneWidget);
    });

    const held = {
      'id': 'h1',
      'customerName': 'Acme Trading',
      'customerPartyId': 'c1',
      'lines': [
        {'id': 'l1', 'itemId': 'pen', 'itemCode': 'PEN', 'description': 'Pen', 'quantity': 2, 'unitPrice': 12, 'discountPercent': 0, 'taxRate': 15, 'lineTotal': 27.6},
      ],
    };

    testWidgets('a resumed sale is at today\'s price, and paying it names the held copy', (t) async {
      late Map<String, dynamic> sent;
      final adapter = till(onPost: (o) {
        sent = _body(o);
        return _sold(o);
      });
      await t.pumpWidget(host(adapter, const PosSaleScreen(session: _session, held: held), me: _cashier));
      await t.pumpAndSettle();
      expect(find.text('Acme Trading'), findsOneWidget);
      expect(find.text('Prices changed since this sale was held. The ticket shows the current prices.'), findsOneWidget);
      await t.tap(find.byKey(const ValueKey('pos-pay')));
      await t.pumpAndSettle();
      await t.tap(find.byKey(const ValueKey('pos-tender-card')));
      await t.pumpAndSettle();
      await t.tap(find.byKey(const ValueKey('pos-confirm-payment')));
      await t.pumpAndSettle();
      expect(sent['parkedOrderId'], 'h1');
      expect(sent['customerPartyId'], 'c1');
      final line = (sent['lines'] as List).single as Map;
      expect(line.containsKey('unitPrice'), isFalse, reason: 'a cashier cannot override: the server would refuse the lower held price');
      expect((sent['payments'] as List).single['amount'], 33.35); // 2 x 14.50 = 29.00, + 15% = 33.35
    });

    testWidgets('a resumed sale held again replaces its old copy', (t) async {
      final adapter = till(onPost: (o) => envelope({'id': 'h2'}, status: 201));
      await t.pumpWidget(host(adapter, const PosSaleScreen(session: _session, held: held)));
      await t.pumpAndSettle();
      await t.tap(find.byKey(const ValueKey('pos-hold')));
      await t.pumpAndSettle();
      expect(adapter.requests.where((o) => o.method == 'DELETE').single.path, endsWith('/orders/h1/park'));
    });

    testWidgets('the held list resumes or discards', (t) async {
      final adapter = till(held: const [held]);
      Map<String, dynamic>? resumed;
      await t.pumpWidget(host(adapter, Builder(builder: (context) => Scaffold(body: TextButton(onPressed: () async => resumed = await Navigator.of(context).push<Map<String, dynamic>>(MaterialPageRoute(builder: (_) => const PosHeldSalesScreen(registerId: 'r1'))), child: const Text('open'))))));
      await t.tap(find.text('open'));
      await t.pumpAndSettle();
      expect(find.text('2 × Pen'), findsOneWidget);
      await t.tap(find.text('Discard'));
      await t.pumpAndSettle();
      await t.tap(find.text('Discard').last);
      await t.pumpAndSettle();
      expect(adapter.requests.where((o) => o.method == 'DELETE').single.path, endsWith('/orders/h1/park'));
      await t.tap(find.text('Resume'));
      await t.pumpAndSettle();
      expect(resumed?['id'], 'h1');
    });

    testWidgets('a line discount above the cashier limit is refused before it reaches the server', (t) async {
      await t.pumpWidget(host(till(), const PosSaleScreen(session: _session, settings: {'maxDiscountPercent': 50, 'cashierDiscountPercent': 10}), me: _cashier));
      await t.pumpAndSettle();
      await scan(t, 'PEN');
      await t.tap(find.text('Pen'));
      await t.pumpAndSettle();
      expect(find.text('Your limit is 10%.'), findsOneWidget);
      await t.enterText(find.byKey(const ValueKey('pos-edit-discount')), '20');
      await t.tap(find.text('Apply'));
      await t.pumpAndSettle();
      expect(find.text('Above your limit of 10%'), findsOneWidget);
      await t.enterText(find.byKey(const ValueKey('pos-edit-discount')), '10');
      await t.tap(find.text('Apply'));
      await t.pumpAndSettle();
      expect(find.textContaining('10% off'), findsOneWidget);
    });

    testWidgets('leaving a ticket with items asks first', (t) async {
      await t.pumpWidget(host(till(), Builder(builder: (context) => Scaffold(body: TextButton(onPressed: () => Navigator.of(context).push(MaterialPageRoute<void>(builder: (_) => const PosSaleScreen(session: _session))), child: const Text('open'))))));
      await t.tap(find.text('open'));
      await t.pumpAndSettle();
      await scan(t, 'PEN');
      await t.pageBack();
      await t.pumpAndSettle();
      expect(find.textContaining('Leave this ticket?'), findsOneWidget);
    });
  });

  group('payment', () {
    Future<Map<String, dynamic>> pay(WidgetTester t, Future<void> Function() steps, {Me me = _admin}) async {
      var sent = <String, dynamic>{};
      await t.pumpWidget(host(till(onPost: (o) {
        sent = _body(o);
        return _sold(o);
      }), const PosSaleScreen(session: _session), me: me));
      await t.pumpAndSettle();
      await scan(t, 'PEN');
      await t.tap(find.byKey(const ValueKey('pos-pay')));
      await t.pumpAndSettle();
      await steps();
      return sent;
    }

    String due(WidgetTester t) => t.widget<Text>(find.byKey(const ValueKey('pos-due'))).data!;

    testWidgets('cash above what is due shows the change and sends what was applied', (t) async {
      final sent = await pay(t, () async {
        await t.enterText(find.byKey(const ValueKey('pos-amount')), '20');
        await t.pump();
        expect(find.text('Change to give: 3.32'), findsOneWidget);
        await t.tap(find.byKey(const ValueKey('pos-confirm-payment')));
        await t.pumpAndSettle();
      });
      expect((sent['payments'] as List).single, {'method': 'cash', 'amount': 16.68, 'tenderedAmount': 20, 'changeAmount': 3.32, 'reference': null});
    });

    testWidgets('a part payment keeps the tender and asks for the rest', (t) async {
      final sent = await pay(t, () async {
        await t.tap(find.byKey(const ValueKey('pos-tender-card')));
        await t.pump();
        await t.enterText(find.byKey(const ValueKey('pos-amount')), '10');
        await t.pump();
        await t.tap(find.byKey(const ValueKey('pos-confirm-payment')));
        await t.pumpAndSettle();
        expect(due(t), '6.68');
        await t.tap(find.byKey(const ValueKey('pos-tender-cash')));
        await t.pump();
        await t.tap(find.byKey(const ValueKey('pos-confirm-payment')));
        await t.pumpAndSettle();
      });
      final payments = sent['payments'] as List;
      expect(payments.map((p) => p['method']), ['card', 'cash']);
      expect(payments.map((p) => p['amount']), [10, 6.68]);
    });

    testWidgets('a card cannot be more than what is due', (t) async {
      await pay(t, () async {
        await t.tap(find.byKey(const ValueKey('pos-tender-card')));
        await t.pump();
        await t.enterText(find.byKey(const ValueKey('pos-amount')), '50');
        await t.pump();
        expect(find.text('Only cash can be more than what is due.'), findsOneWidget);
        expect(t.widget<ButtonStyleButton>(find.byKey(const ValueKey('pos-confirm-payment'))).onPressed, isNull);
      });
    });

    testWidgets('on account needs the permission, and a customer on the ticket', (t) async {
      await pay(t, () async {
        expect(find.byKey(const ValueKey('pos-tender-account')), findsNothing);
      }, me: _cashier);

      await pay(t, () async {
        await t.tap(find.byKey(const ValueKey('pos-tender-account')));
        await t.pump();
        expect(find.text('Choose a customer on the ticket first.'), findsOneWidget);
        expect(t.widget<ButtonStyleButton>(find.byKey(const ValueKey('pos-confirm-payment'))).onPressed, isNull);
      });
    });

    testWidgets('a refused payment stays on screen with the reason, and retries with the same key', (t) async {
      final keys = <Object?>[];
      var attempts = 0;
      await t.pumpWidget(host(till(onPost: (o) {
        keys.add(o.headers['Idempotency-Key']);
        attempts++;
        return attempts == 1 ? problem(409, 'Only 1 of Pen in stock.') : _sold(o);
      }), const PosSaleScreen(session: _session)));
      await t.pumpAndSettle();
      await scan(t, 'PEN');
      await t.tap(find.byKey(const ValueKey('pos-pay')));
      await t.pumpAndSettle();
      await t.tap(find.byKey(const ValueKey('pos-confirm-payment')));
      await t.pumpAndSettle();
      expect(find.text('Only 1 of Pen in stock.'), findsOneWidget);
      await t.tap(find.byKey(const ValueKey('pos-confirm-payment')));
      await t.pumpAndSettle();
      expect(keys, hasLength(2));
      expect(keys.first, keys.last);
      expect(find.text('Sale POS-1'), findsOneWidget);
    });
  });

  group('return', () {
    Map<String, dynamic> order({Object? invoiceId, num moneyLeft = 9.44, num onAccountLeft = 0, List<Map<String, dynamic>>? payments, String status = 'completed'}) => {
          'id': 'o1',
          'number': 'POS-9',
          'status': status,
          'orderDate': '2026-11-05T10:00:00Z',
          'customerName': 'Acme Trading',
          'totalAmount': 9.44,
          'returnedTotal': 0,
          'invoiceId': invoiceId,
          'moneyLeftToRefund': moneyLeft,
          'onAccountLeft': onAccountLeft,
          'payments': payments ??
              [
                {'method': 'card', 'amount': 9.44},
              ],
          'lines': [
            {'id': 'l1', 'itemId': 'x', 'itemCode': 'X', 'description': 'Book', 'quantity': 3, 'unitPrice': 3.33, 'discountPercent': 10, 'taxRate': 5, 'lineTotal': 9.44, 'returnedQuantity': 0, 'returnedAmount': 0},
          ],
        };

    ResponseBody done(RequestOptions o) => envelope({'id': 'rt1', 'number': 'POSR-1', 'totalAmount': 3.15, 'refunds': _body(o)['refunds']}, status: 201);

    testWidgets('finds the receipt and refunds exactly the share the server works out', (t) async {
      late Map<String, dynamic> sent;
      late RequestOptions post;
      await t.pumpWidget(host(till(order: order(), onPost: (o) {
        sent = _body(o);
        post = o;
        return done(o);
      }), const PosReturnScreen(session: {'id': 's1', 'registerName': 'Front counter'})));
      await t.pumpAndSettle();
      await t.enterText(find.byKey(const ValueKey('pos-return-number')), 'POS-9');
      await t.tap(find.byKey(const ValueKey('pos-return-find')));
      await t.pumpAndSettle();
      expect(find.textContaining('3 of 3 can be returned'), findsOneWidget);
      // Nothing chosen yet, and no reason.
      expect(t.widget<ButtonStyleButton>(find.byKey(const ValueKey('pos-return-confirm'))).onPressed, isNull);
      await t.tap(find.byTooltip('One more Book'));
      await t.pump();
      await t.enterText(find.byKey(const ValueKey('pos-return-reason')), 'Wrong size');
      await t.pump();
      expect(find.text('Refund 3.15'), findsOneWidget);
      await t.tap(find.byKey(const ValueKey('pos-return-confirm')));
      await t.pumpAndSettle();
      expect(sent['orderId'], 'o1');
      expect(sent['sessionId'], 's1');
      expect(sent['reason'], 'Wrong size');
      expect(sent['lines'], [
        {'orderLineId': 'l1', 'quantity': 1, 'restock': true},
      ]);
      expect(sent['refunds'], [
        {'method': 'card', 'amount': 3.15, 'reference': null},
      ]);
      expect(post.headers['Idempotency-Key'], isNotNull);
      expect(find.text('Refund POSR-1'), findsOneWidget);
    });

    testWidgets('a sale part on account goes back the same way', (t) async {
      late Map<String, dynamic> sent;
      await t.pumpWidget(host(till(
        order: order(invoiceId: 'inv', moneyLeft: 5, onAccountLeft: 4.44, payments: [
          {'method': 'cash', 'amount': 5},
          {'method': 'account', 'amount': 4.44},
        ]),
        onPost: (o) {
          sent = _body(o);
          return done(o);
        },
      ), const PosReturnScreen(session: {'id': 's1'}, initialNumber: 'POS-9')));
      await t.pumpAndSettle();
      for (var i = 0; i < 3; i++) {
        await t.tap(find.byTooltip('One more Book'));
        await t.pump();
      }
      await t.enterText(find.byKey(const ValueKey('pos-return-reason')), 'Damaged');
      await t.pump();
      await t.scrollUntilVisible(find.byKey(const ValueKey('pos-return-split')), 200, scrollable: find.byType(Scrollable).first);
      expect(find.textContaining('Cash 5.00 and On account 4.44'), findsOneWidget);
      await t.tap(find.byKey(const ValueKey('pos-return-confirm')));
      await t.pumpAndSettle();
      final refunds = sent['refunds'] as List;
      expect(refunds, [
        {'method': 'cash', 'amount': 5, 'reference': null},
        {'method': 'account', 'amount': 4.44, 'reference': null},
      ]);
    });

    testWidgets('the quantity stops at what can still be returned', (t) async {
      final partly = order();
      (partly['lines'] as List).first
        ..['returnedQuantity'] = 2
        ..['returnedAmount'] = 6.29;
      await t.pumpWidget(host(till(order: partly), const PosReturnScreen(session: {'id': 's1'}, initialNumber: 'POS-9')));
      await t.pumpAndSettle();
      await t.tap(find.byTooltip('One more Book'));
      await t.pump();
      expect(t.widget<IconButton>(find.widgetWithIcon(IconButton, Icons.add_circle_outline)).onPressed, isNull);
      expect(find.text('Refund 3.15'), findsOneWidget);
    });

    testWidgets('a voided sale cannot be returned, and a missing receipt says why', (t) async {
      await t.pumpWidget(host(till(order: order(status: 'voided')), const PosReturnScreen(session: {'id': 's1'}, initialNumber: 'POS-9')));
      await t.pumpAndSettle();
      expect(find.text('Only a completed sale can be returned.'), findsOneWidget);
      await t.pumpWidget(host(FakeAdapter((_) => problem(404, 'No sale with receipt number X.')), const PosReturnScreen(session: {'id': 's1'}, initialNumber: 'X')));
      await t.pumpAndSettle();
      expect(find.text('No sale with receipt number X.'), findsOneWidget);
    });

    testWidgets('an order offers Return only when something can come back, to whoever may', (t) async {
      final sale = order();
      FakeAdapter one(Map<String, dynamic> o) => FakeAdapter((_) => envelope(o));
      await t.pumpWidget(host(one(sale), const OrderDetailScreen(id: 'o1')));
      await t.pumpAndSettle();
      expect(find.byKey(const ValueKey('pos-order-return')), findsOneWidget);
      await t.pumpWidget(host(one(sale), const OrderDetailScreen(id: 'o1'), me: _cashier));
      await t.pumpAndSettle();
      expect(find.byKey(const ValueKey('pos-order-return')), findsNothing);
      final all = order();
      (all['lines'] as List).first['returnedQuantity'] = 3;
      await t.pumpWidget(host(one(all), const OrderDetailScreen(id: 'o1')));
      await t.pumpAndSettle();
      expect(find.byKey(const ValueKey('pos-order-return')), findsNothing);
    });

    testWidgets('a sale part returned can no longer be voided', (t) async {
      await t.pumpWidget(host(FakeAdapter((_) => envelope({...order(), 'returnedTotal': 3.15})), const OrderDetailScreen(id: 'o1')));
      await t.pumpAndSettle();
      expect(find.text('Void'), findsNothing);
      expect(find.text('Returned so far'), findsOneWidget);
    });

    testWidgets('returns are listed and open', (t) async {
      final adapter = FakeAdapter((o) => o.path.endsWith('/returns/rt1')
          ? envelope({'id': 'rt1', 'number': 'POSR-1', 'customerName': 'Acme', 'originalNumber': 'POS-9', 'cashierName': 'A', 'returnDate': '2026-11-05', 'subTotal': 3, 'taxTotal': 0.15, 'totalAmount': 3.15, 'reason': 'Wrong size', 'creditNoteNumber': 'CN-1', 'lines': [{'description': 'Book', 'quantity': 1, 'unitPrice': 3.33, 'lineTotal': 3.15, 'restock': true}], 'refunds': [{'method': 'card', 'amount': 3.15}]})
          : ResponseBody.fromString(jsonEncode({'success': true, 'statusCode': 200, 'data': [{'id': 'rt1', 'number': 'POSR-1', 'customerName': 'Acme', 'originalNumber': 'POS-9', 'returnDate': '2026-11-05', 'reason': 'Wrong size', 'totalAmount': 3.15}], 'meta': {'total': 1, 'page': 1, 'pageSize': 25}}), 200, headers: {Headers.contentTypeHeader: ['application/json']}));
      await t.pumpWidget(host(adapter, const ReturnsScreen()));
      await t.pumpAndSettle();
      expect(find.text('POSR-1'), findsOneWidget);
      await t.tap(find.text('POSR-1'));
      await t.pumpAndSettle();
      expect(find.text('CN-1'), findsOneWidget);
      expect(find.text('back on shelf', findRichText: false), findsNothing);
      expect(find.textContaining('back on shelf'), findsOneWidget);
    });
  });

  group('shift report', () {
    Map<String, dynamic> report(String status) => {
          'session': {
            ..._session,
            'status': status,
            'openedAt': '2026-11-05T08:00:00Z',
            'closedAt': status == 'closed' ? '2026-11-05T17:00:00Z' : null,
            'totalPayIn': 0,
            'totalPayOut': 20,
            'actualCash': 95,
            'cashDifference': -5,
            'drawerTransactions': [
              {'id': 'd0', 'type': 'floatIn', 'amount': 100, 'reason': 'Opening cash float'},
              {'id': 'd1', 'type': 'payOut', 'amount': 20, 'reason': 'Milk'},
            ],
          },
          'grossSales': 110,
          'discounts': 10,
          'netSales': 100,
          'tax': 5,
          'salesTotal': 105,
          'returns': 5,
          'voids': 0,
          'netTakings': 100,
          'saleCount': 2,
          'voidCount': 0,
          'returnCount': 1,
          'itemsSold': 4,
          'averageTicket': 52.5,
          'cashSales': 25,
          'cashRefunds': 5,
          'heldCount': 1,
          'tenders': [
            {'method': 'cash', 'sales': 25, 'refunds': 5, 'net': 20, 'count': 1},
            {'method': 'card', 'sales': 80, 'refunds': 0, 'net': 80, 'count': 1},
          ],
          'topItems': [
            {'itemCode': 'PEN', 'description': 'Pen', 'quantity': 4, 'revenue': 105},
          ],
        };

    testWidgets('an open shift is an X report with its cash actions', (t) async {
      final adapter = FakeAdapter((o) => envelope(report('open')));
      await t.pumpWidget(host(adapter, const SessionDetailScreen(row: _session)));
      await t.pumpAndSettle();
      expect(adapter.requests.first.path, endsWith('/sessions/s1/report'));
      expect(find.text('X report'), findsOneWidget);
      expect(find.text('Net takings'), findsOneWidget);
      expect(find.text('100.00'), findsWidgets);
      expect(find.text('Cash in/out'), findsOneWidget);
      expect(find.text('Close shift'), findsOneWidget);
      await t.scrollUntilVisible(find.textContaining('Milk'), 200, scrollable: find.byType(Scrollable).first);
      expect(find.textContaining('Opening cash float'), findsNothing);
    });

    testWidgets('a closed shift is a Z report that shows what was short, and nothing to do', (t) async {
      await t.pumpWidget(host(FakeAdapter((o) => envelope(report('closed'))), const SessionDetailScreen(row: _session)));
      await t.pumpAndSettle();
      expect(find.text('Z report'), findsOneWidget);
      expect(find.text('Close shift'), findsNothing);
      await t.scrollUntilVisible(find.text('Short'), 200, scrollable: find.byType(Scrollable).first);
      expect(find.text('-5.00'), findsWidgets);
    });

    test('closing warns about what stays on hold', () {
      expect(closeShiftForm(const {'id': 's', 'expectedCash': 10}, heldCount: 2).intro, contains('2 held sales stay on hold'));
      expect(closeShiftForm(const {'id': 's', 'expectedCash': 10}).intro, isNot(contains('on hold')));
    });

    test('opening asks for a counted float while the setting is on', () {
      final enforced = openShiftForm(const {'id': 'r', 'name': 'R'});
      expect(FormEngine.validate(enforced.fields, FormEngine.start(enforced.fields)).keys, contains('openingCash'));
      final relaxed = openShiftForm(const {'id': 'r', 'name': 'R'}, enforceFloat: false);
      expect(FormEngine.validate(relaxed.fields, FormEngine.start(relaxed.fields)), isEmpty);
    });
  });

  group('forms', () {
    late FormSpecHolder holder;

    Future<void> register(WidgetTester t, Map<String, dynamic>? existing) async {
      holder = FormSpecHolder();
      await t.pumpWidget(host(FakeAdapter((_) => envelope({})), Consumer(builder: (context, ref, _) {
        holder.fields = registerForm(ref, existing: existing).fields;
        holder.initial = registerForm(ref, existing: existing).initial;
        return const SizedBox();
      })));
    }

    testWidgets('a register can choose its warehouse and every account', (t) async {
      await register(t, null);
      expect(holder.fields.map((f) => f.key), containsAll(['warehouseId', 'defaultCashAccountId', 'cardAccountId', 'varianceAccountId', 'expenseAccountId']));
    });

    testWidgets('editing a register keeps what is set, and shows it as chosen', (t) async {
      await register(t, {'id': 'r', 'code': 'R', 'name': 'R', 'isActive': true, 'warehouseId': 'w', 'cardAccountId': 'c'});
      expect(holder.initial['__label_warehouseId'], 'Selected warehouse');
      expect(holder.initial['__label_cardAccountId'], 'Selected account');
      expect(holder.initial.containsKey('__label_varianceAccountId'), isFalse);
    });

    test('updating a register keeps its custom fields and clears the accounts it must clear', () {
      final body = registerUpdateBody(const {'customFields': '{"cf_a":1}', 'varianceAccountId': 'v'}, const {'name': 'R', 'cardAccountId': 'c'});
      expect(body['customFields'], '{"cf_a":1}');
      expect(body['cardAccountId'], 'c');
      // A left-out account keeps its value on the server, so one the form cleared goes as the empty id.
      expect(body['varianceAccountId'], '00000000-0000-0000-0000-000000000000');
      expect(body['expenseAccountId'], '00000000-0000-0000-0000-000000000000');
      // These two are replaced outright, so a cleared one goes as null.
      expect(body['warehouseId'], isNull);
      expect(body['defaultCashAccountId'], isNull);
    });

    test('an item carries its barcode, and editing keeps it', () {
      final spec = itemForm(const {'id': 'i', 'sku': 'S', 'name': 'N', 'type': 'product', 'uom': 'ea', 'barcode': '123', 'isActive': true});
      final body = FormEngine.collect(spec.fields, FormEngine.start(spec.fields, spec.initial));
      expect(body['barcode'], '123');
    });
  });

  test('toOrderLine is what the sale and the hold both send', () {
    const l = CartLine(key: 'k', itemId: 'i', itemCode: 'A', description: 'A', quantity: 1, unitPrice: 2, listPrice: 2);
    expect((holdBody(const {'id': 's'}, lines: const [l])['lines'] as List).single, toOrderLine(l));
  });
}

class FormSpecHolder {
  List<Field> fields = const [];
  Map<String, dynamic> initial = const {};
}
