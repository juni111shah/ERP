import 'package:dio/dio.dart';
import 'package:erp_mobile/core/api_client.dart';
import 'package:erp_mobile/core/key_value_store.dart';
import 'package:erp_mobile/core/me.dart';
import 'package:erp_mobile/core/session.dart';
import 'package:erp_mobile/features/receivables/party_screens.dart';
import 'package:erp_mobile/features/receivables/receipt_screens.dart';
import 'package:erp_mobile/kit/field.dart';
import 'package:erp_mobile/kit/form_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/fake_adapter.dart';

const _me = Me(
  fullName: 'A',
  email: 'a@x',
  baseCurrency: 'QAR',
  countryCode: 'QA',
  permissions: {'*'},
  legalEntities: [LegalEntity(id: 'le1', code: 'LE', name: 'Main', baseCurrency: 'QAR')],
  modules: {},
);

Widget host(FakeAdapter adapter, Widget Function(BuildContext, WidgetRef) build) {
  final store = MemoryKeyValueStore();
  return ProviderScope(
    overrides: [
      storeProvider.overrideWithValue(store),
      apiProvider.overrideWith((ref) => ApiClient(store: store, baseUrl: 'http://api.test', dio: Dio()..httpClientAdapter = adapter)),
      meProvider.overrideWith((ref) async => _me),
    ],
    child: MaterialApp(home: Consumer(builder: (c, ref, _) => build(c, ref))),
  );
}

void main() {
  group('receipt form', () {
    late FormSpec spec;
    Future<void> make(WidgetTester t, {Map<String, dynamic>? invoice}) async {
      await t.pumpWidget(host(FakeAdapter((_) => envelope({})), (c, ref) {
        spec = receiptForm(ref, partyId: invoice?['partyId'], partyName: invoice?['partyName'], invoice: invoice);
        return const SizedBox();
      }));
      await t.pump();
    }

    testWidgets('requires customer, bank account and amount', (t) async {
      await make(t);
      final errors = FormEngine.validate(spec.fields, FormEngine.start(spec.fields, spec.initial));
      expect(errors.keys, containsAll(['partyId', 'bankAccountId', 'amount']));
    });

    testWidgets('rejects more than 2 decimals, zero and allocations above the amount', (t) async {
      await make(t);
      final v = FormEngine.start(spec.fields, spec.initial)
        ..['partyId'] = 'p'
        ..['bankAccountId'] = 'b'
        ..['amount'] = '10.005';
      expect(FormEngine.validate(spec.fields, v)['amount'], 'At most 2 decimal places');
      v['amount'] = '0';
      expect(FormEngine.validate(spec.fields, v)['amount'], isNotNull);
      v['amount'] = '100';
      v['allocations'] = [
        {'invoiceId': 'i1', 'amount': '150'}
      ];
      expect(FormEngine.validate(spec.fields, v)['amount'], 'Allocations exceed the receipt amount');
    });

    testWidgets('prefilled from an invoice: customer, outstanding amount and one allocation', (t) async {
      await make(t, invoice: {'id': 'i1', 'number': 'INV-1', 'partyId': 'p1', 'partyName': 'Acme', 'total': 1000, 'paid': 250.5, 'legalEntityId': 'le1'});
      final v = FormEngine.start(spec.fields, spec.initial)..['bankAccountId'] = 'b1';
      expect(FormEngine.validate(spec.fields, v), isEmpty);
      expect(FormEngine.collect(spec.fields, v), {
        'legalEntityId': 'le1',
        'partyId': 'p1',
        'date': todayIso(),
        'method': 'bankTransfer',
        'bankAccountId': 'b1',
        'amount': 749.5,
        'autoAllocate': false,
        'allocations': [
          {'invoiceId': 'i1', 'amount': 749.5}
        ],
      });
    });
  });

  group('party form', () {
    test('needs a name; validates email', () {
      final spec = partyForm(null, defaultRole: 'Customer');
      final v = FormEngine.start(spec.fields, spec.initial);
      expect(FormEngine.validate(spec.fields, v)['name'], 'Name is required');
      v['name'] = 'Zed';
      expect(FormEngine.validate(spec.fields, v), isEmpty);
      expect(FormEngine.collect(spec.fields, v)['roleCustomer'], true);
      v['email'] = 'nope';
      expect(FormEngine.validate(spec.fields, v)['email'], 'Enter a valid email');
    });

    testWidgets('submits roles joined and drops the toggles', (t) async {
      final adapter = FakeAdapter((_) => envelope({'id': 'x'}));
      await t.pumpWidget(host(adapter, (c, ref) => Scaffold(body: ElevatedButton(onPressed: () => openForm<Object?>(c, partyForm(null, defaultRole: 'Customer')), child: const Text('open')))));
      await t.tap(find.text('open'));
      await t.pumpAndSettle();
      await t.enterText(find.widgetWithText(TextField, 'Name *'), 'Zed');
      await t.tap(find.text('Save'));
      await t.pumpAndSettle();
      final post = adapter.requests.firstWhere((r) => r.method == 'POST');
      expect(post.path, endsWith('/api/v1/parties'));
      expect((post.data as Map)['roles'], 'Customer');
      expect((post.data as Map).containsKey('roleCustomer'), isFalse);
    });
  });

  group('tiles and details', () {
    testWidgets('receipt tile shows number, customer and unallocated amount', (t) async {
      await t.pumpWidget(MaterialApp(
        home: Scaffold(body: Builder(builder: (c) => receiptTile(c, {'id': 'r', 'number': 'RCT-1', 'partyName': 'Acme', 'date': '2026-10-30', 'method': 'bankTransfer', 'amount': 1000, 'unallocated': 200, 'status': 'issued', 'currencyCode': 'QAR'}))),
      ));
      expect(find.text('RCT-1'), findsOneWidget);
      expect(find.textContaining('Acme'), findsOneWidget);
      expect(find.textContaining('200.00 unallocated'), findsOneWidget);
      expect(find.text('Posted'), findsOneWidget);
    });

    Map<String, dynamic> receipt(String status, num unallocated) => {
          'id': 'r1',
          'number': 'RCT-1',
          'partyName': 'Acme',
          'date': '2026-10-30',
          'method': 'cash',
          'amount': 500,
          'unallocated': unallocated,
          'status': status,
          'currencyCode': 'QAR',
          'allocations': [
            {'invoiceNumber': 'INV-9', 'amount': 300}
          ],
        };

    testWidgets('open receipt shows allocations, Allocate and Void; void posts the reason', (t) async {
      final adapter = FakeAdapter((o) => envelope(receipt('issued', 200)));
      await t.pumpWidget(host(adapter, (c, ref) => const ReceiptDetailScreen(id: 'r1')));
      await t.pumpAndSettle();
      expect(find.text('INV-9'), findsOneWidget);
      expect(find.text('Allocate'), findsOneWidget);
      await t.tap(find.text('Void'));
      await t.pumpAndSettle();
      await t.enterText(find.byType(TextField), 'entered twice');
      await t.tap(find.text('Confirm'));
      await t.pumpAndSettle();
      final post = adapter.requests.firstWhere((r) => r.method == 'POST');
      expect(post.path, endsWith('/finance/receipts/r1/void'));
      expect((post.data as Map)['reason'], 'entered twice');
    });

    testWidgets('voided receipt offers no actions', (t) async {
      await t.pumpWidget(host(FakeAdapter((o) => envelope(receipt('voided', 0))), (c, ref) => const ReceiptDetailScreen(id: 'r1')));
      await t.pumpAndSettle();
      expect(find.text('Void'), findsNothing);
      expect(find.text('Allocate'), findsNothing);
    });

    testWidgets('party tile shows roles', (t) async {
      await t.pumpWidget(MaterialApp(home: Scaffold(body: Builder(builder: (c) => partyTile(c, {'id': 'p', 'code': 'C-1', 'name': 'Acme', 'roles': ['Customer', 'Vendor'], 'email': 'a@b.c'})))));
      expect(find.text('Acme'), findsOneWidget);
      expect(find.textContaining('Customer · Vendor'), findsOneWidget);
    });
  });
}
