import 'package:erp_mobile/core/me.dart';
import 'package:erp_mobile/features/payables/bill_screens.dart';
import 'package:erp_mobile/features/payables/finance_screens.dart';
import 'package:erp_mobile/features/payables/journal_screens.dart';
import 'package:erp_mobile/features/payables/payment_screens.dart';
import 'package:erp_mobile/kit/field.dart';
import 'package:erp_mobile/kit/form_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

const _me = Me(
  fullName: 'Admin',
  email: 'a@b.c',
  baseCurrency: 'QAR',
  countryCode: 'QA',
  permissions: {'*'},
  legalEntities: [LegalEntity(id: 'le1', code: 'E1', name: 'Entity', baseCurrency: 'QAR')],
  modules: {},
);

/// Builds a spec inside a ProviderScope that knows one legal entity.
Future<FormSpec> build(WidgetTester tester, FormSpec Function(WidgetRef ref) make) async {
  late FormSpec spec;
  await tester.pumpWidget(ProviderScope(
    overrides: [meOrEmptyProvider.overrideWithValue(_me)],
    child: MaterialApp(home: Consumer(builder: (context, ref, _) {
      spec = make(ref);
      return const SizedBox();
    })),
  ));
  return spec;
}

Values fill(FormSpec spec, [Values extra = const {}]) => FormEngine.start(spec.fields, {...spec.initial, ...extra});

void main() {
  group('bill form', () {
    testWidgets('requires vendor, a description, an expense account and a quantity', (tester) async {
      final spec = await build(tester, (ref) => billForm(ref));
      final errors = FormEngine.validate(spec.fields, fill(spec));
      expect(errors['partyId'], 'Vendor is required');
      expect(errors['lines[0].description'], 'Description is required');
      expect(errors['lines[0].accountId'], 'Expense account is required');
      expect(errors['lines[0].price'], 'Unit price is required');
      expect(errors.containsKey('legalEntityId'), isFalse); // preselected
    });

    testWidgets('rejects quantity with 5 decimals, due date before bill date and far future dates', (tester) async {
      final spec = await build(tester, (ref) => billForm(ref));
      final v = fill(spec, {'partyId': 'p1', 'billDate': '2026-10-30', 'dueDate': '2026-10-01'});
      (v['lines'] as List).first
        ..['description'] = 'x'
        ..['accountId'] = 'a1'
        ..['quantity'] = '1.00001'
        ..['price'] = '2.1234';
      var errors = FormEngine.validate(spec.fields, v);
      expect(errors['lines[0].quantity'], 'At most 4 decimal places');
      expect(errors.containsKey('lines[0].price'), isFalse); // price allows 4 decimals
      expect(errors['dueDate'], 'The due date cannot be before the bill date');
      v['billDate'] = '2090-01-01';
      v['dueDate'] = null;
      errors = FormEngine.validate(spec.fields, v);
      expect(errors['billDate'], 'At most one year ahead');
    });

    testWidgets('produces the body the API expects', (tester) async {
      final spec = await build(tester, (ref) => billForm(ref));
      final v = fill(spec, {'partyId': 'p1', '__label_partyId': 'Vendor', 'vendorReference': 'INV-9', 'billDate': '2026-10-30'});
      (v['lines'] as List).first
        ..['description'] = 'Paper'
        ..['accountId'] = 'a1'
        ..['quantity'] = '2'
        ..['price'] = '12.5'
        ..['taxCodeId'] = 't1';
      expect(FormEngine.validate(spec.fields, v), isEmpty);
      expect(FormEngine.collect(spec.fields, v), {
        'legalEntityId': 'le1',
        'partyId': 'p1',
        'vendorReference': 'INV-9',
        'billDate': '2026-10-30',
        'lines': [
          {'description': 'Paper', 'quantity': 2, 'price': 12.5, 'accountId': 'a1', 'taxCodeId': 't1'}
        ],
      });
    });

    testWidgets('editing starts from the stored bill', (tester) async {
      final spec = await build(
          tester,
          (ref) => billForm(ref, existing: {
                'id': 'b1',
                'number': 'BIL-1',
                'legalEntityId': 'le1',
                'partyId': 'p1',
                'partyName': 'Acme',
                'billDate': '2026-10-01',
                'dueDate': '2026-10-31',
                'lines': [
                  {'description': 'Rent', 'quantity': 1, 'price': 500, 'accountId': 'a2', 'taxCodeId': null}
                ],
              }));
      expect(spec.title, 'Edit BIL-1');
      final v = fill(spec);
      expect(FormEngine.validate(spec.fields, v), isEmpty);
      expect(v['__label_partyId'], 'Acme');
    });
  });

  group('payment form', () {
    testWidgets('cheque number is required only for cheques and the cheque fields are hidden otherwise', (tester) async {
      final spec = await build(tester, (ref) => paymentForm(ref));
      final v = fill(spec, {'partyId': 'p1', 'bankAccountId': 'b1', 'amount': '100'});
      expect(FormEngine.validate(spec.fields, v), isEmpty);
      expect(FormEngine.collect(spec.fields, v).containsKey('chequeNo'), isFalse);
      v['method'] = 'instrument';
      expect(FormEngine.validate(spec.fields, v)['chequeNo'], 'Cheque number is required');
      v['chequeNo'] = '1001';
      v['chequeDate'] = '2026-11-05';
      final body = FormEngine.collect(spec.fields, v);
      expect(body['method'], 'instrument');
      expect(body['chequeNo'], '1001');
      expect(body['chequeDate'], '2026-11-05');
    });

    testWidgets('amount is at most 2 decimals and positive; allocations are collected with bill ids', (tester) async {
      final spec = await build(tester, (ref) => paymentForm(ref));
      final v = fill(spec, {'partyId': 'p1', 'bankAccountId': 'b1', 'amount': '10.123'});
      expect(FormEngine.validate(spec.fields, v)['amount'], 'At most 2 decimal places');
      v['amount'] = '0';
      expect(FormEngine.validate(spec.fields, v)['amount'], 'Must be at least 0.01');
      v['amount'] = '150';
      v['allocations'] = [
        {'billId': 'bill1', 'amount': '100'}
      ];
      expect(FormEngine.validate(spec.fields, v), isEmpty);
      expect(FormEngine.collect(spec.fields, v)['allocations'], [
        {'billId': 'bill1', 'amount': 100}
      ]);
      expect(allocationTotal(v['allocations']), 100);
    });
  });

  group('journal form', () {
    testWidgets('needs two lines, each with only a debit or only a credit, and balances', (tester) async {
      final spec = await build(tester, (ref) => journalForm(ref));
      final v = fill(spec);
      v['lines'] = [
        {'accountId': 'a1', 'debit': '5'}
      ];
      expect(FormEngine.validate(spec.fields, v)['lines'], 'Add at least 2 lines');
      v['lines'] = [
        {'accountId': 'a1', 'debit': '50', 'credit': '10'},
        {'accountId': 'a2', 'debit': '', 'credit': ''},
      ];
      final errors = FormEngine.validate(spec.fields, v);
      expect(errors['lines[0].credit'], 'Enter either a debit or a credit');
      expect(errors['lines[1].credit'], 'Enter either a debit or a credit');
      expect(balanceCheck([
        {'debit': '50', 'credit': ''},
        {'debit': '', 'credit': '40'},
      ], {}), contains('must be equal'));
      expect(balanceCheck([
        {'debit': '50', 'credit': ''},
        {'debit': '', 'credit': '50'},
      ], {}), isNull);
    });
  });

  group('account and tax code forms', () {
    testWidgets('account requires code, name and type', (tester) async {
      final spec = await build(tester, (ref) => accountForm(ref));
      final errors = FormEngine.validate(spec.fields, fill(spec));
      expect(errors.keys, containsAll(['code', 'name', 'type']));
      final v = fill(spec, {'code': '6100', 'name': 'Rent', 'type': 'expense'});
      expect(FormEngine.collect(spec.fields, v), {'code': '6100', 'name': 'Rent', 'type': 'expense', 'isPostable': true, 'isBank': false, 'isActive': true});
    });

    testWidgets('tax rate is a percentage between 0 and 100', (tester) async {
      final spec = await build(tester, (ref) => taxCodeForm(ref));
      final v = fill(spec, {'code': 'V5', 'name': 'VAT 5', 'rate': '120'});
      expect(FormEngine.validate(spec.fields, v)['rate'], 'Must be at most 100');
      v['rate'] = '5';
      expect(FormEngine.validate(spec.fields, v), isEmpty);
      expect(FormEngine.collect(spec.fields, v)['rate'], 5);
      expect(FormEngine.collect(spec.fields, v)['kind'], 'vat');
    });
  });
}
