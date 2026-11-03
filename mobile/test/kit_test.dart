import 'package:dio/dio.dart';
import 'package:erp_mobile/core/api_client.dart';
import 'package:erp_mobile/core/key_value_store.dart';
import 'package:erp_mobile/core/session.dart';
import 'package:erp_mobile/kit/field.dart';
import 'package:erp_mobile/kit/form_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/fake_adapter.dart';

final lineFields = [
  Field.lookup('itemId', 'Item', path: '/api/v1/items', copy: {'description': 'name', 'price': 'salesPrice'}),
  Field.text('description', 'Description', required: true),
  Field.decimal('quantity', 'Quantity', required: true, min: 0.0001),
  Field.money('price', 'Price', required: true),
];

final invoiceFields = [
  Field.date('invoiceDate', 'Invoice date', required: true, initial: 'today'),
  Field.lookup('partyId', 'Customer', path: '/api/v1/parties', required: true),
  Field.toggle('isCreditNote', 'Credit note'),
  Field.text('notes', 'Notes', visibleWhen: (v) => v['isCreditNote'] == true),
  Field.lines('lines', 'Lines', fields: lineFields),
];

void main() {
  group('FormEngine', () {
    test('starts with today for dates, an empty first line and any given initial values', () {
      final v = FormEngine.start(invoiceFields, {'partyId': 'p1', '__label_partyId': 'Acme'});
      expect(v['invoiceDate'], todayIso());
      expect(v['partyId'], 'p1');
      expect(v['__label_partyId'], 'Acme');
      expect((v['lines'] as List).length, 1);
    });

    test('reports required, number and decimal-place problems, including on lines', () {
      final v = FormEngine.start(invoiceFields)..['partyId'] = 'p1';
      (v['lines'] as List).first
        ..['description'] = 'Work'
        ..['quantity'] = '1.00001'
        ..['price'] = '-5';
      final errors = FormEngine.validate(invoiceFields, v);
      expect(errors['lines[0].quantity'], 'At most 4 decimal places');
      expect(errors['lines[0].price'], 'Must be at least 0');
      expect(errors.containsKey('partyId'), isFalse);
      expect(FormEngine.validate(invoiceFields, FormEngine.start(invoiceFields))['partyId'], 'Customer is required');
    });

    test('collects numbers as numbers, drops empties and labels, and skips hidden fields', () {
      final v = FormEngine.start(invoiceFields)
        ..['partyId'] = 'p1'
        ..['__label_partyId'] = 'Acme'
        ..['notes'] = 'hidden because it is not a credit note';
      (v['lines'] as List).first
        ..['description'] = ' Work '
        ..['quantity'] = '2'
        ..['price'] = '1,250.50'
        ..['__label_itemId'] = 'X';
      final body = FormEngine.collect(invoiceFields, v);
      expect(body['partyId'], 'p1');
      expect(body['isCreditNote'], false);
      expect(body.containsKey('notes'), isFalse);
      expect(body.containsKey('__label_partyId'), isFalse);
      expect(body['lines'], [
        {'description': 'Work', 'quantity': 2, 'price': 1250.5}
      ]);
    });

    test('a validator on the field can reject a value', () {
      final f = Field.money('amount', 'Amount', validator: (v, all) => (toNum(v) ?? 0) > 100 ? 'Too much' : null);
      expect(FormEngine.check(f, '150', {}), 'Too much');
      expect(FormEngine.check(f, '50', {}), isNull);
    });
  });

  group('FormPage', () {
    Widget host(FakeAdapter adapter, FormSpec spec) {
      final store = MemoryKeyValueStore();
      return ProviderScope(
        overrides: [
          storeProvider.overrideWithValue(store),
          apiProvider.overrideWith((ref) => ApiClient(store: store, baseUrl: 'http://api.test', dio: Dio()..httpClientAdapter = adapter)),
        ],
        child: MaterialApp(home: Builder(builder: (context) => Scaffold(body: Center(child: ElevatedButton(onPressed: () => openForm<Object?>(context, spec), child: const Text('open')))))),
      );
    }

    testWidgets('does not submit while required fields are empty', (tester) async {
      var submitted = 0;
      final spec = FormSpec(title: 'New invoice', fields: invoiceFields, submit: (ref, body, raw) async => submitted++);
      await tester.pumpWidget(host(FakeAdapter((_) => envelope({})), spec));
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Save'));
      await tester.pumpAndSettle();

      expect(submitted, 0);
      expect(find.text('Customer is required'), findsOneWidget);
      expect(find.text('Please fix the highlighted fields.'), findsOneWidget);
    });

    testWidgets('shows the server message and the field it names when saving fails', (tester) async {
      final spec = FormSpec(
        title: 'New receipt',
        fields: [Field.money('amount', 'Amount', required: true), Field.text('reference', 'Reference')],
        submit: (ref, body, raw) async => throw ApiException(400, 'Validation failed', {'Input.Amount': ['Amount must be positive']}),
      );
      await tester.pumpWidget(host(FakeAdapter((_) => envelope({})), spec));
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();

      await tester.enterText(find.widgetWithText(TextField, 'Amount *'), '10');
      await tester.tap(find.text('Save'));
      await tester.pumpAndSettle();

      expect(find.text('Amount must be positive'), findsOneWidget);
      expect(find.text('Validation failed'), findsOneWidget);
    });

    testWidgets('submits the cleaned body and closes with the result', (tester) async {
      Map<String, dynamic>? sent;
      final spec = FormSpec(
        title: 'New receipt',
        fields: [Field.money('amount', 'Amount', required: true), Field.text('reference', 'Reference'), Field.toggle('auto', 'Allocate automatically')],
        submit: (ref, body, raw) async {
          sent = body;
          return {'id': 'r1'};
        },
      );
      await tester.pumpWidget(host(FakeAdapter((_) => envelope({})), spec));
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();

      await tester.enterText(find.widgetWithText(TextField, 'Amount *'), '1250.5');
      await tester.tap(find.byType(Switch));
      await tester.tap(find.text('Save'));
      await tester.pumpAndSettle();

      expect(sent, {'amount': 1250.5, 'auto': true});
      expect(find.text('New receipt'), findsNothing); // closed
    });

    testWidgets('picking a lookup record fills the field and copies values into the line', (tester) async {
      final adapter = FakeAdapter((o) => envelope([
            {'id': 'i1', 'name': 'Consulting day', 'salesPrice': 800}
          ]));
      Map<String, dynamic>? sent;
      final spec = FormSpec(
        title: 'New invoice',
        fields: [
          Field.lines('lines', 'Lines', fields: lineFields),
        ],
        submit: (ref, body, raw) async {
          sent = body;
          return null;
        },
      );
      await tester.pumpWidget(host(adapter, spec));
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Select').first); // the Item lookup of line 1
      await tester.pumpAndSettle();
      await tester.tap(find.text('Consulting day'));
      await tester.pumpAndSettle();
      await tester.enterText(find.widgetWithText(TextField, 'Quantity *'), '3');
      await tester.tap(find.text('Save'));
      await tester.pumpAndSettle();

      expect(sent, {
        'lines': [
          {'itemId': 'i1', 'description': 'Consulting day', 'quantity': 3, 'price': 800}
        ]
      });
    });
  });
}
