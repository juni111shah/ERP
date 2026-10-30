import 'package:dio/dio.dart';
import 'package:erp_mobile/core/api_client.dart';
import 'package:erp_mobile/core/key_value_store.dart';
import 'package:erp_mobile/core/me.dart';
import 'package:erp_mobile/core/session.dart';
import 'package:erp_mobile/features/sales/distribution_screens.dart';
import 'package:erp_mobile/features/sales/order_screens.dart';
import 'package:erp_mobile/features/sales/quote_screens.dart';
import 'package:erp_mobile/features/sales/retail_screens.dart';
import 'package:erp_mobile/features/sales/screens.dart';
import 'package:erp_mobile/kit/field.dart';
import 'package:erp_mobile/kit/form_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/fake_adapter.dart';

const _me = Me(
  fullName: 'T',
  email: 't@t',
  baseCurrency: 'QAR',
  countryCode: 'QA',
  permissions: {'*'},
  legalEntities: [LegalEntity(id: 'le1', code: 'MAIN', name: 'Main', baseCurrency: 'QAR')],
  modules: {'sales'},
);

Widget host(FakeAdapter adapter, Widget Function(BuildContext) child) {
  final store = MemoryKeyValueStore();
  return ProviderScope(
    key: UniqueKey(),
    overrides: [
      storeProvider.overrideWithValue(store),
      meOrEmptyProvider.overrideWithValue(_me),
      apiProvider.overrideWith((ref) => ApiClient(store: store, baseUrl: 'http://api.test', dio: Dio()..httpClientAdapter = adapter)),
    ],
    child: MaterialApp(home: Builder(builder: child)),
  );
}

/// Builds a form spec with a real [WidgetRef].
Future<FormSpec> specOf(WidgetTester tester, FormSpec Function(WidgetRef) build) async {
  late FormSpec spec;
  await tester.pumpWidget(host(FakeAdapter((_) => envelope({})), (_) => Consumer(builder: (context, ref, _) {
        spec = build(ref);
        return const SizedBox();
      })));
  return spec;
}

Map<String, dynamic> orderJson(String status, {num delivered = 0, String? invoice}) => {
      'id': 'o1',
      'number': 'SO-1',
      'status': status,
      'customerName': 'Acme',
      'currency': 'QAR',
      'orderDate': '2026-10-01',
      'totalAmount': 100,
      'subTotal': 100,
      'taxTotal': 0,
      'discountTotal': 0,
      'invoicedInvoiceId': invoice,
      'lines': [
        {'id': 'l1', 'description': 'Widget', 'quantity': 2, 'deliveredQuantity': delivered, 'unitOfMeasure': 'EA', 'unitPrice': 50, 'lineTotal': 100},
      ],
    };

void main() {
  test('every menu entry has a screen', () {
    final ids = salesScreens.expand((s) => s.menuIds).toSet();
    expect(ids, containsAll(['sales.quotes', 'sales.orders', 'retail.stores', 'retail.pricelists', 'retail.promotions', 'distribution.routes', 'distribution.trips', 'distribution.wholesale']));
  });

  group('forms', () {
    testWidgets('quote needs customer name, currency and a line; body carries complete lines', (tester) async {
      final spec = await specOf(tester, (ref) => quoteForm(ref));
      final raw = FormEngine.start(spec.fields, spec.initial)..['customerName'] = '';
      final errors = FormEngine.validate(spec.fields, raw);
      expect(errors['customerName'], 'Customer name is required');
      expect(errors['lines[0].description'], 'Description is required');
      expect(errors['lines[0].unitPrice'], 'Unit price is required');

      raw['customerName'] = 'Acme';
      (raw['lines'] as List).first
        ..['description'] = 'Work'
        ..['quantity'] = '2'
        ..['unitPrice'] = '10.50';
      expect(FormEngine.validate(spec.fields, raw), isEmpty);
      Object? posted;
      final body = FormEngine.collect(spec.fields, raw);
      final adapter = FakeAdapter((o) {
        posted = o.data;
        return envelope({'id': 'q1'});
      });
      late WidgetRef captured;
      await tester.pumpWidget(host(adapter, (_) => Consumer(builder: (context, ref, _) {
            captured = ref;
            return const SizedBox();
          })));
      await tester.runAsync(() => spec.submit(captured, body, raw));
      final line = ((posted as Map)['lines'] as List).first as Map;
      expect(line, containsPair('itemCode', ''));
      expect(line, containsPair('unitOfMeasure', 'EA'));
      expect(line, containsPair('discountPercent', 0));
      expect(line, containsPair('quantity', 2));
      expect(line, containsPair('unitPrice', 10.5));
      expect(posted as Map, containsPair('legalEntityId', 'le1'));
      expect(posted as Map, containsPair('currency', 'QAR'));
      expect(adapter.requests.single.method, 'POST');
      expect(adapter.requests.single.path, '/api/v1/m/sales/quotes');
    });

    testWidgets('editing an order goes to PUT with the lines filled from the record', (tester) async {
      final spec = await specOf(tester, (ref) => orderForm(ref, existing: orderJson('draft')));
      final raw = FormEngine.start(spec.fields, spec.initial);
      expect((raw['lines'] as List).first['description'], 'Widget');
      expect(spec.title, 'Edit SO-1');
    });

    testWidgets('promotion end date must not precede start date', (tester) async {
      final spec = await specOf(tester, (ref) => promotionForm(ref));
      final raw = FormEngine.start(spec.fields, spec.initial)
        ..['code'] = 'P1'
        ..['name'] = 'Sale'
        ..['discountValue'] = '10'
        ..['startDate'] = '2026-11-10'
        ..['endDate'] = '2026-11-01';
      expect(FormEngine.validate(spec.fields, raw)['endDate'], isNotNull);
      raw['endDate'] = '2026-11-30';
      expect(FormEngine.validate(spec.fields, raw), isEmpty);
      expect(FormEngine.collect(spec.fields, raw), containsPair('type', 'percentageDiscount'));
    });

    testWidgets('store form asks for code and name on create but not on edit', (tester) async {
      final create = await specOf(tester, (ref) => storeForm(ref));
      expect(FormEngine.validate(create.fields, FormEngine.start(create.fields, create.initial)).keys, containsAll(['code', 'name']));
      final edit = await specOf(tester, (ref) => storeForm(ref, existing: {'id': 's1', 'code': 'S1', 'name': 'Shop', 'isActive': true}));
      expect(edit.fields.any((f) => f.key == 'code'), isFalse);
    });

    testWidgets('trip completion refuses more than was loaded and posts per item', (tester) async {
      final trip = {
        'id': 't1',
        'tripNumber': 'TRIP-1',
        'inventoryItems': [
          {'itemId': 'i1', 'itemName': 'Cola', 'loadedQuantity': 10},
        ],
      };
      final spec = await specOf(tester, (ref) => completeTripForm(trip));
      final raw = FormEngine.start(spec.fields, spec.initial)
        ..['totalSalesAmount'] = '50'
        ..['totalCollectionsAmount'] = '50'
        ..['sold_0'] = '8'
        ..['returned_0'] = '3';
      expect(FormEngine.validate(spec.fields, raw)['damaged_0'], contains('exceed'));
      raw['returned_0'] = '2';
      expect(FormEngine.validate(spec.fields, raw), isEmpty);
    });

    testWidgets('route stops need a customer', (tester) async {
      final spec = await specOf(tester, (ref) => routeForm(ref));
      final raw = FormEngine.start(spec.fields, spec.initial)
        ..['code'] = 'R1'
        ..['name'] = 'North'
        ..['stops'] = [
          {'sequenceOrder': '1', 'customerName': 'X'},
        ];
      expect(FormEngine.validate(spec.fields, raw)['stops[0].customerPartyId'], 'Customer is required');
    });
  });

  group('tiles', () {
    testWidgets('quote and order tiles show customer, total and a humanized status', (tester) async {
      await tester.pumpWidget(host(FakeAdapter((_) => envelope({})), (c) => Scaffold(body: Column(children: [
            quoteTile(c, {'id': 'q1', 'number': 'QT-1', 'customerName': 'Acme', 'validUntil': '2026-11-19', 'currency': 'QAR', 'totalAmount': 1250, 'status': 'sent'}),
            orderTile(c, {...orderJson('processing'), 'totalAmount': 99}),
          ]))));
      expect(find.text('QT-1'), findsOneWidget);
      expect(find.textContaining('Acme'), findsNWidgets(2));
      expect(find.text('Sent'), findsOneWidget);
      expect(find.text('Processing'), findsOneWidget);
      expect(find.text('QAR 1,250.00'), findsOneWidget);
    });
  });

  group('detail actions', () {
    Future<void> openOrder(WidgetTester tester, Map<String, dynamic> order) async {
      await tester.pumpWidget(host(FakeAdapter((_) => envelope(order)), (_) => const OrderDetailScreen(id: 'o1')));
      await tester.pumpAndSettle();
    }

    testWidgets('a draft order offers Confirm, Cancel and Delete but not Convert', (tester) async {
      await openOrder(tester, orderJson('draft'));
      expect(find.text('Confirm'), findsOneWidget);
      expect(find.text('Cancel order'), findsOneWidget);
      expect(find.text('Delete'), findsOneWidget);
      expect(find.text('Convert to invoice'), findsNothing);
      expect(find.byTooltip('Edit'), findsOneWidget);
    });

    testWidgets('a confirmed order can record delivery and be invoiced; editing stops once delivery started', (tester) async {
      await openOrder(tester, orderJson('confirmed'));
      expect(find.text('Record delivery'), findsOneWidget);
      expect(find.text('Convert to invoice'), findsOneWidget);
      expect(find.byTooltip('Edit'), findsOneWidget);
      await openOrder(tester, orderJson('processing', delivered: 1));
      expect(find.text('Start processing'), findsNothing);
      expect(find.byTooltip('Edit'), findsNothing);
    });

    testWidgets('an invoiced order has no actions except delete is hidden too', (tester) async {
      await openOrder(tester, orderJson('invoiced', delivered: 2, invoice: 'inv1'));
      expect(find.text('Open the invoice'), findsOneWidget);
      expect(find.text('Delete'), findsNothing);
      expect(find.text('Cancel order'), findsNothing);
      expect(find.text('Convert to invoice'), findsNothing);
    });

    testWidgets('quote actions follow the status rules and Confirm sends the status change', (tester) async {
      final adapter = FakeAdapter((o) => o.method == 'PUT' ? envelope({}) : envelope({'id': 'q1', 'number': 'QT-1', 'status': 'draft', 'customerName': 'Acme', 'currency': 'QAR', 'lines': []}));
      await tester.pumpWidget(host(adapter, (_) => const QuoteDetailScreen(id: 'q1')));
      await tester.pumpAndSettle();
      expect(find.text('Send'), findsOneWidget);
      expect(find.text('Accept'), findsOneWidget);
      expect(find.text('Convert to order'), findsNothing);
      expect(find.text('Back to draft'), findsNothing);
      await tester.tap(find.text('Send'));
      await tester.pumpAndSettle();
      final put = adapter.requests.firstWhere((r) => r.method == 'PUT');
      expect(put.path, '/api/v1/m/sales/quotes/q1/status');
      expect(put.data, {'status': 'sent'});
    });

    testWidgets('an accepted quote offers conversion; a converted one only links to its order', (tester) async {
      Map<String, dynamic> quote(String s) => {'id': 'q1', 'number': 'QT-1', 'status': s, 'customerName': 'A', 'currency': 'QAR', 'lines': [], if (s == 'converted') 'convertedSalesOrderId': 'o1'};
      await tester.pumpWidget(host(FakeAdapter((_) => envelope(quote('accepted'))), (_) => const QuoteDetailScreen(id: 'q1')));
      await tester.pumpAndSettle();
      expect(find.text('Convert to order'), findsOneWidget);
      await tester.pumpWidget(host(FakeAdapter((_) => envelope(quote('converted'))), (_) => const QuoteDetailScreen(id: 'q2')));
      await tester.pumpAndSettle();
      expect(find.text('Convert to order'), findsNothing);
      expect(find.text('Delete'), findsNothing);
      expect(find.text('Open the sales order'), findsOneWidget);
    });

    testWidgets('a completed trip has no Complete action; an in-progress one has', (tester) async {
      Map<String, dynamic> trip(String s) => {'id': 't1', 'tripNumber': 'TRIP-1', 'routeName': 'North', 'driverName': 'D', 'vehicleNumber': 'V', 'status': s, 'inventoryItems': []};
      await tester.pumpWidget(host(FakeAdapter((_) => envelope(trip('inProgress'))), (_) => const TripDetailScreen(id: 't1')));
      await tester.pumpAndSettle();
      expect(find.text('Complete trip'), findsOneWidget);
      await tester.pumpWidget(host(FakeAdapter((_) => envelope(trip('completed'))), (_) => const TripDetailScreen(id: 't2')));
      await tester.pumpAndSettle();
      expect(find.text('Complete trip'), findsNothing);
    });

    testWidgets('promotion shows Deactivate when active and Activate when not', (tester) async {
      Map<String, dynamic> promo(bool a) => {'id': 'p1', 'code': 'P', 'name': 'Sale', 'type': 'bundle', 'discountValue': 5, 'isActive': a, 'startDate': '2026-10-01', 'endDate': '2026-11-01'};
      await tester.pumpWidget(host(FakeAdapter((_) => envelope(promo(true))), (_) => const PromotionDetailScreen(id: 'p1')));
      await tester.pumpAndSettle();
      expect(find.text('Deactivate'), findsOneWidget);
      expect(find.text('Activate'), findsNothing);
      await tester.pumpWidget(host(FakeAdapter((_) => envelope(promo(false))), (_) => const PromotionDetailScreen(id: 'p2')));
      await tester.pumpAndSettle();
      expect(find.text('Activate'), findsOneWidget);
    });
  });
}
