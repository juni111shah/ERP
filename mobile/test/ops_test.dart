import 'package:dio/dio.dart';
import 'package:erp_mobile/core/api_client.dart';
import 'package:erp_mobile/core/key_value_store.dart';
import 'package:erp_mobile/core/me.dart';
import 'package:erp_mobile/core/session.dart';
import 'package:erp_mobile/features/ops/inventory_screens.dart';
import 'package:erp_mobile/features/ops/manufacturing_screens.dart';
import 'package:erp_mobile/features/ops/pos_cart.dart';
import 'package:erp_mobile/features/ops/pos_screens.dart';
import 'package:erp_mobile/features/ops/screens.dart';
import 'package:erp_mobile/kit/field.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/fake_adapter.dart';

const _admin = Me(fullName: 'A', email: 'a@x', baseCurrency: 'QAR', countryCode: 'QA', permissions: {'*'}, legalEntities: [], modules: {});

Widget host(FakeAdapter adapter, Widget child) {
  final store = MemoryKeyValueStore();
  return ProviderScope(
    key: UniqueKey(),
    overrides: [
      storeProvider.overrideWithValue(store),
      meOrEmptyProvider.overrideWithValue(_admin),
      apiProvider.overrideWith((ref) => ApiClient(store: store, baseUrl: 'http://api.test', dio: Dio()..httpClientAdapter = adapter)),
    ],
    child: MaterialApp(home: child),
  );
}

void main() {
  test('registers a screen for every menu entry built', () {
    final ids = opsScreens.expand((s) => s.menuIds).toSet();
    expect(ids, containsAll(['pos.terminal', 'pos.sessions', 'pos.orders', 'pos.returns', 'pos.registers', 'pos.analytics', 'inventory.stock', 'inventory.movements', 'inventory.counts', 'inventory.warehouses', 'manufacturing.boms', 'manufacturing.workorders', 'manufacturing.costing', 'manufacturing.analytics']));
  });

  group('forms', () {
    test('a sale sends no price, and the change comes off the cash', () {
      final body = saleBody(
        {'id': 's', 'registerId': 'r', 'legalEntityId': 'l'},
        lines: const [CartLine(key: 'i1', itemId: 'i1', itemCode: 'A', description: 'A', quantity: 2, unitPrice: 850, listPrice: 850, discountPercent: 10)],
        tenders: const [Tender('cash', 2000)],
      );
      final line = (body['lines'] as List).single as Map;
      expect(line.containsKey('unitPrice'), isFalse);
      expect(line['discountPercent'], 10);
      final pay = (body['payments'] as List).single as Map;
      expect(pay['amount'], 1530);
      expect(pay['tenderedAmount'], 2000);
      expect(pay['changeAmount'], 470);
      expect(body['sessionId'], 's');
    });

    test('close shift and cash movement require their amounts', () {
      final close = closeShiftForm({'id': 's', 'expectedCash': 80});
      expect(FormEngine.validate(close.fields, FormEngine.start(close.fields)).keys, contains('actualCash'));
      final move = cashMovementForm({'id': 's'});
      final errors = FormEngine.validate(move.fields, FormEngine.start(move.fields));
      expect(errors.keys, containsAll(['amount', 'reason']));
      expect(FormEngine.collect(move.fields, {'type': 'payOut', 'amount': '20', 'reason': 'milk'}), {'type': 'payOut', 'amount': 20, 'reason': 'milk'});
    });

    test('open shift sends register, entity and opening cash', () {
      final spec = openShiftForm({'id': 'r', 'legalEntityId': 'l', 'name': 'Reg'});
      expect(FormEngine.validate(spec.fields, {'openingCash': '100'}), isEmpty);
      expect(FormEngine.validate(spec.fields, {}).keys, contains('openingCash'));
    });

    test('stock forms: receive, issue, transfer and adjust validate their required fields', () {
      for (final spec in [receiveForm(), issueForm(), transferForm(), adjustForm()]) {
        expect(FormEngine.validate(spec.fields, FormEngine.start(spec.fields)).keys, contains('itemId'), reason: spec.title);
      }
      final adjust = adjustForm();
      final v = FormEngine.start(adjust.fields)..['quantityDelta'] = '0';
      expect(FormEngine.validate(adjust.fields, v)['quantityDelta'], isNotNull);
      v['quantityDelta'] = '-3';
      expect(FormEngine.validate(adjust.fields, v).containsKey('quantityDelta'), isFalse);
    });
  });

  group('tiles', () {
    Future<void> show(WidgetTester t, Widget w) async {
      await t.pumpWidget(host(FakeAdapter((_) => envelope({})), Scaffold(body: w)));
    }

    testWidgets('order tile shows number, total and a readable status', (t) async {
      await show(t, Builder(builder: (c) => orderTile(c, {'id': '1', 'number': 'POS-1', 'customerName': 'Walk-in', 'orderDate': '2026-10-30T10:00:00Z', 'lineCount': 2, 'totalAmount': 1530, 'status': 'voided'})));
      expect(find.text('POS-1'), findsOneWidget);
      expect(find.text('1,530.00'), findsOneWidget);
      expect(find.text('Voided'), findsOneWidget);
    });

    testWidgets('work order tile shows progress and status', (t) async {
      await show(t, Builder(builder: (c) => workOrderTile(c, {'id': '1', 'workOrderNumber': 'WO-1', 'producedItemName': 'Gateway', 'completedQuantity': 65, 'plannedQuantity': 200, 'uom': 'PCS', 'dueDate': '2026-11-04', 'status': 'inProgress'})));
      expect(find.text('WO-1'), findsOneWidget);
      expect(find.textContaining('65 / 200'), findsOneWidget);
      expect(find.text('In progress'), findsOneWidget);
    });

    testWidgets('stock tile shows quantity and value', (t) async {
      await show(t, stockTile({'item': 'Scanner', 'sku': 'S1', 'warehouse': 'Main', 'uom': 'pcs', 'quantity': 12, 'averageCost': 480, 'value': 5760}));
      expect(find.text('12 pcs'), findsOneWidget);
      expect(find.text('5,760.00'), findsOneWidget);
    });
  });

  group('detail actions', () {
    FakeAdapter wo(String status, List<RequestOptions> log) => FakeAdapter((o) {
          log.add(o);
          if (o.method == 'GET') {
            return envelope({'id': 'w1', 'workOrderNumber': 'WO-1', 'producedItemName': 'G', 'producedItemCode': 'G1', 'bomCode': 'B', 'plannedQuantity': 3, 'completedQuantity': 0, 'scrappedQuantity': 0, 'uom': 'PCS', 'status': status, 'operations': []});
          }
          return status == 'completed' ? problem(409, 'Cannot transition') : envelope({'id': 'w1'});
        });

    testWidgets('planned work order offers Release and Cancel, and release posts', (t) async {
      final log = <RequestOptions>[];
      await t.pumpWidget(host(wo('planned', log), const WorkOrderDetailScreen(id: 'w1')));
      await t.pumpAndSettle();
      expect(find.text('Release'), findsOneWidget);
      expect(find.text('Cancel'), findsOneWidget);
      expect(find.text('Complete'), findsNothing);
      await t.tap(find.text('Release'));
      await t.pumpAndSettle();
      await t.tap(find.text('Continue'));
      await t.pumpAndSettle();
      expect(log.any((o) => o.method == 'POST' && o.path.endsWith('/workorders/w1/release')), isTrue);
    });

    testWidgets('in-progress work order offers Complete; completed offers nothing', (t) async {
      await t.pumpWidget(host(wo('inProgress', []), const WorkOrderDetailScreen(id: 'w1')));
      await t.pumpAndSettle();
      expect(find.text('Complete'), findsOneWidget);
      expect(find.text('Release'), findsNothing);
      await t.pumpWidget(host(wo('completed', []), const WorkOrderDetailScreen(id: 'w2')));
      await t.pumpAndSettle();
      expect(find.text('Release'), findsNothing);
      expect(find.text('Cancel'), findsNothing);
    });

    testWidgets('completed POS order cannot be voided twice; completed one asks for a reason', (t) async {
      final log = <RequestOptions>[];
      FakeAdapter order(String status) => FakeAdapter((o) {
            log.add(o);
            return o.method == 'GET' ? envelope({'id': 'o1', 'number': 'POS-1', 'customerName': 'W', 'status': status, 'lines': [], 'payments': []}) : envelope({'id': 'o1'});
          });
      await t.pumpWidget(host(order('voided'), const OrderDetailScreen(id: 'o1')));
      await t.pumpAndSettle();
      expect(find.text('Void'), findsNothing);
      await t.pumpWidget(host(order('completed'), const OrderDetailScreen(id: 'o2')));
      await t.pumpAndSettle();
      await t.tap(find.text('Void'));
      await t.pumpAndSettle();
      await t.enterText(find.byType(TextField), 'wrong item');
      await t.tap(find.text('Confirm'));
      await t.pumpAndSettle();
      final post = log.lastWhere((o) => o.method == 'POST');
      expect(post.path, endsWith('/orders/o2/void'));
      expect(post.data, {'reason': 'wrong item'});
    });

    testWidgets('open stock count offers Post, a posted one does not', (t) async {
      FakeAdapter count(String status) => FakeAdapter((o) => envelope({'id': 'c', 'number': 'CNT-1', 'warehouse': 'W', 'status': status, 'lines': []}));
      await t.pumpWidget(host(count('counting'), const CountDetailScreen(id: 'c')));
      await t.pumpAndSettle();
      expect(find.text('Record'), findsOneWidget);
      expect(find.text('Enter counts'), findsOneWidget);
      await t.pumpWidget(host(count('posted'), const CountDetailScreen(id: 'c2')));
      await t.pumpAndSettle();
      expect(find.text('Record'), findsNothing);
    });
  });
}
