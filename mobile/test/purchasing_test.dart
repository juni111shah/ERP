import 'package:dio/dio.dart';
import 'package:erp_mobile/core/api_client.dart';
import 'package:erp_mobile/core/key_value_store.dart';
import 'package:erp_mobile/core/me.dart';
import 'package:erp_mobile/core/session.dart';
import 'package:erp_mobile/features/purchasing/order_screens.dart';
import 'package:erp_mobile/features/purchasing/receipt_screens.dart';
import 'package:erp_mobile/features/purchasing/requisition_screens.dart';
import 'package:erp_mobile/kit/field.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/fake_adapter.dart';
import 'support/more_actions.dart';

const entity = LegalEntity(id: 'le1', code: 'MAIN', name: 'Main', baseCurrency: 'QAR');

Widget host(FakeAdapter adapter, Widget child, {Set<String> permissions = const {'*'}}) {
  final store = MemoryKeyValueStore();
  return ProviderScope(
    overrides: [
      storeProvider.overrideWithValue(store),
      apiProvider.overrideWith((ref) => ApiClient(store: store, baseUrl: 'http://api.test', dio: Dio()..httpClientAdapter = adapter)),
      meProvider.overrideWith((ref) async => Me(fullName: 'Dana', email: 'd@x', baseCurrency: 'QAR', countryCode: 'QA', permissions: permissions, legalEntities: const [entity], modules: const {'purchasing'})),
    ],
    child: MaterialApp(home: child),
  );
}

Future<WidgetRef> grabRef(WidgetTester tester, FakeAdapter adapter) async {
  late WidgetRef ref;
  await tester.pumpWidget(host(adapter, Consumer(builder: (c, r, _) {
    ref = r;
    r.watch(meOrEmptyProvider);
    return const SizedBox();
  })));
  await tester.pumpAndSettle();
  return ref;
}

Map<String, dynamic> order(String status) => {
      'id': 'o1',
      'number': 'PO-1',
      'legalEntityId': 'le1',
      'vendorName': 'Acme',
      'orderDate': '2026-10-01',
      'currency': 'QAR',
      'status': status,
      'subTotal': 100,
      'taxTotal': 0,
      'discountTotal': 0,
      'totalAmount': 100,
      'lines': [
        {'id': 'ol1', 'description': 'Desk', 'quantity': 4, 'receivedQuantity': 1, 'billedQuantity': 0, 'unitOfMeasure': 'EA', 'unitPrice': 25, 'lineTotal': 100, 'itemCode': ''}
      ],
    };

void main() {
  testWidgets('order form requires a vendor and lines, and sends currency, vendor name and line defaults', (tester) async {
    final ref = await grabRef(tester, FakeAdapter((_) => envelope({})));
    final spec = orderForm(ref);
    final values = FormEngine.start(spec.fields, spec.initial);
    final errors = FormEngine.validate(spec.fields, values);
    expect(errors['vendorPartyId'], 'Supplier is required');
    expect(errors['lines[0].description'], 'Description is required');
    expect(errors['lines[0].unitPrice'], 'Unit price is required');

    values
      ..['vendorPartyId'] = 'v1'
      ..['__label_vendorPartyId'] = 'Acme';
    (values['lines'] as List).first
      ..['description'] = 'Desk'
      ..['unitPrice'] = '250'
      ..['quantity'] = '2';
    expect(FormEngine.validate(spec.fields, values), isEmpty);

    Object? sent;
    final adapter = FakeAdapter((o) {
      sent = o.data;
      return envelope({'id': 'o9'});
    });
    final ref2 = await grabRef(tester, adapter);
    final spec2 = orderForm(ref2);
    await tester.runAsync(() => spec2.submit(ref2, FormEngine.collect(spec2.fields, values), values));
    final body = sent as Map<String, dynamic>;
    expect(body['legalEntityId'], 'le1');
    expect(body['currency'], 'QAR');
    expect(body['vendorName'], 'Acme');
    expect((body['lines'] as List).first, containsPair('unitOfMeasure', 'EA'));
    expect((body['lines'] as List).first, containsPair('itemCode', ''));
    expect((body['lines'] as List).first, containsPair('quantity', 2));
  });

  testWidgets('a draft order can be submitted, approved, cancelled and deleted but not received or billed', (tester) async {
    await tester.pumpWidget(host(FakeAdapter((_) => envelope(order('draft'))), const OrderDetailScreen(id: 'o1')));
    await tester.pumpAndSettle();
    await openMoreActions(tester);
    for (final t in ['Submit', 'Approve', 'Cancel order', 'Delete']) {
      expect(find.text(t), findsOneWidget, reason: t);
    }
    for (final t in ['Receive goods', 'Convert to bill', 'Back to draft']) {
      expect(find.text(t), findsNothing, reason: t);
    }
    expect(find.byTooltip('Edit'), findsOneWidget);
  });

  testWidgets('a partly received order offers receiving and billing, and cannot be edited', (tester) async {
    await tester.pumpWidget(host(FakeAdapter((_) => envelope(order('partiallyReceived'))), const OrderDetailScreen(id: 'o1')));
    await tester.pumpAndSettle();
    await openMoreActions(tester);
    for (final t in ['Receive goods', 'Mark received', 'Convert to bill']) {
      expect(find.text(t), findsOneWidget, reason: t);
    }
    expect(find.text('Approve'), findsNothing);
    expect(find.text('Delete'), findsNothing);
    expect(find.byTooltip('Edit'), findsNothing);
  });

  testWidgets('a billed order has no actions', (tester) async {
    await tester.pumpWidget(host(FakeAdapter((_) => envelope(order('billed'))), const OrderDetailScreen(id: 'o1')));
    await tester.pumpAndSettle();
    expect(find.byType(FilledButton), findsNothing);
    expect(find.byType(OutlinedButton), findsNothing);
  });

  testWidgets('order tile shows number, vendor, readable status', (tester) async {
    await tester.pumpWidget(host(FakeAdapter((_) => envelope([])), Scaffold(body: Builder(builder: (c) => orderTile(c, {...order('partiallyReceived'), 'lineCount': 1})))));
    await tester.pumpAndSettle();
    expect(find.text('PO-1'), findsOneWidget);
    expect(find.text('Partly received'), findsOneWidget);
  });

  testWidgets('receipt form starts at the open quantity and sends order line ids; tolerance errors come back as messages', (tester) async {
    Map<String, dynamic>? sent;
    final adapter = FakeAdapter((o) {
      sent = o.data as Map<String, dynamic>;
      return envelope({'id': 'g1'});
    });
    final ref = await grabRef(tester, adapter);
    final spec = receiptForm(ref, order: order('partiallyReceived'));
    final values = FormEngine.start(spec.fields, spec.initial);
    expect(((values['lines'] as List).first as Map)['quantityReceived'], '3');
    expect(values['receivedBy'], 'Dana');
    (values['lines'] as List).first['quantityReceived'] = '0';
    expect(FormEngine.validate(spec.fields, values)['lines[0].quantityReceived'], isNotNull);
    (values['lines'] as List).first['quantityReceived'] = '2';
    expect(FormEngine.validate(spec.fields, values), isEmpty);
    await tester.runAsync(() => spec.submit(ref, FormEngine.collect(spec.fields, values), values));
    expect(sent!['purchaseOrderId'], 'o1');
    expect(sent!['legalEntityId'], 'le1');
    expect((sent!['lines'] as List).single, containsPair('purchaseOrderLineId', 'ol1'));
    expect((sent!['lines'] as List).single, containsPair('quantityReceived', 2));
  });

  testWidgets('requisition actions follow the status', (tester) async {
    Map<String, dynamic> req(String s) => {'id': 'r1', 'number': 'PR-1', 'requesterName': 'Dana', 'status': s, 'estimatedTotal': 10, 'requisitionDate': '2026-10-01', 'lines': []};
    await tester.pumpWidget(host(FakeAdapter((_) => envelope(req('draft'))), const RequisitionDetailScreen(id: 'r1')));
    await tester.pumpAndSettle();
    await openMoreActions(tester);
    for (final t in ['Submit', 'Approve', 'Reject', 'Delete']) {
      expect(find.text(t), findsOneWidget, reason: t);
    }
    expect(find.text('Convert to order'), findsNothing);
  });

  testWidgets('an approved requisition can be converted; an ordered one has no actions', (tester) async {
    Map<String, dynamic> req(String s) => {'id': 'r1', 'number': 'PR-1', 'requesterName': 'Dana', 'status': s, 'estimatedTotal': 10, 'requisitionDate': '2026-10-01', 'lines': []};
    await tester.pumpWidget(host(FakeAdapter((_) => envelope(req('approved'))), const RequisitionDetailScreen(id: 'r1')));
    await tester.pumpAndSettle();
    expect(find.text('Convert to order'), findsOneWidget);
    expect(find.text('Submit'), findsNothing);
  });

  testWidgets('an ordered requisition is read only', (tester) async {
    await tester.pumpWidget(host(FakeAdapter((_) => envelope({'id': 'r1', 'number': 'PR-1', 'requesterName': 'Dana', 'status': 'ordered', 'estimatedTotal': 10, 'requisitionDate': '2026-10-01', 'lines': []})), const RequisitionDetailScreen(id: 'r1')));
    await tester.pumpAndSettle();
    expect(find.byType(FilledButton), findsNothing);
    expect(find.byTooltip('Edit'), findsNothing);
  });

  testWidgets('requisition form needs a requester and lines and sends line defaults', (tester) async {
    final ref = await grabRef(tester, FakeAdapter((_) => envelope({})));
    final spec = requisitionForm(ref);
    final values = FormEngine.start(spec.fields, spec.initial);
    expect(values['requesterName'], 'Dana');
    expect(FormEngine.validate(spec.fields, values)['lines[0].description'], 'Description is required');
    (values['lines'] as List).first
      ..['description'] = 'Chairs'
      ..['quantity'] = '5'
      ..['estimatedUnitPrice'] = '100';
    expect(FormEngine.validate(spec.fields, values), isEmpty);
    expect(FormEngine.collect(spec.fields, values)['lines'], [
      {'description': 'Chairs', 'quantity': 5, 'unitOfMeasure': 'EA', 'estimatedUnitPrice': 100}
    ]);
  });
}
