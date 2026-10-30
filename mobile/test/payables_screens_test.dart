import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:erp_mobile/core/api_client.dart';
import 'package:erp_mobile/core/key_value_store.dart';
import 'package:erp_mobile/core/me.dart';
import 'package:erp_mobile/core/session.dart';
import 'package:erp_mobile/features/payables/approval_screens.dart';
import 'package:erp_mobile/features/payables/bill_screens.dart';
import 'package:erp_mobile/features/payables/finance_screens.dart';
import 'package:erp_mobile/features/payables/journal_screens.dart';
import 'package:erp_mobile/features/payables/payment_screens.dart';
import 'package:erp_mobile/features/payables/report_screens.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/fake_adapter.dart';

const _me = Me(
  fullName: 'Admin',
  email: 'a@b.c',
  baseCurrency: 'QAR',
  countryCode: 'QA',
  permissions: {'*'},
  legalEntities: [LegalEntity(id: 'le1', code: 'E1', name: 'Entity', baseCurrency: 'QAR')],
  modules: {},
);

Widget host(FakeAdapter adapter, Widget child, {Me me = _me}) {
  final store = MemoryKeyValueStore();
  return ProviderScope(
    overrides: [
      storeProvider.overrideWithValue(store),
      meOrEmptyProvider.overrideWithValue(me),
      apiProvider.overrideWith((ref) => ApiClient(store: store, baseUrl: 'http://api.test', dio: Dio()..httpClientAdapter = adapter)),
    ],
    child: MaterialApp(home: child),
  );
}

Map<String, dynamic> bill(String status) => {
      'id': 'b1',
      'number': 'BIL-1',
      'legalEntityId': 'le1',
      'partyId': 'p1',
      'partyName': 'Acme Supplies',
      'vendorReference': 'AC-77',
      'billDate': '2026-10-01',
      'dueDate': '2026-10-31',
      'currencyCode': 'QAR',
      'net': 0,
      'tax': 0,
      'total': 1000,
      'paid': 250,
      'status': status,
      'lines': [
        {'description': 'Consulting', 'quantity': 2, 'price': 500, 'net': 0, 'taxAmount': 0, 'accountId': 'a1'}
      ],
    };

FakeAdapter serve(Object? detail, {List<String>? log}) => FakeAdapter((o) {
      if (o.method != 'GET') log?.add('${o.method} ${o.path}${o.data == null ? '' : ' ${jsonEncode(o.data)}'}');
      return envelope(o.method == 'GET' ? detail : {});
    });

void main() {
  group('tiles', () {
    testWidgets('bill tile shows number, vendor, total and the posted status', (tester) async {
      await tester.pumpWidget(host(serve({}), Scaffold(body: Builder(builder: (c) => billTile(c, {...bill('issued'), 'id': 'b1'})))));
      expect(find.text('BIL-1 · AC-77'), findsOneWidget);
      expect(find.textContaining('Acme Supplies'), findsOneWidget);
      expect(find.text('QAR 1,000.00'), findsOneWidget);
      expect(find.text('Posted'), findsOneWidget);
    });

    testWidgets('payment tile shows the method', (tester) async {
      final p = {'id': 'p1', 'number': 'PAY-1', 'partyName': 'Acme', 'date': '2026-10-30', 'method': 'instrument', 'amount': 20, 'currencyCode': 'QAR', 'status': 'issued'};
      await tester.pumpWidget(host(serve({}), Scaffold(body: Builder(builder: (c) => paymentTile(c, p)))));
      expect(find.textContaining('Cheque'), findsOneWidget);
      expect(find.text('QAR 20.00'), findsOneWidget);
    });

    testWidgets('journal and cheque tiles render', (tester) async {
      final j = {'id': 'j1', 'number': 'JV-1', 'memo': 'Accrual', 'date': '2026-10-30', 'status': 'draft', 'amount': 5, 'currencyCode': 'QAR', 'sourceType': 'manual'};
      final c = {'id': 'c1', 'number': 'CHQ-1', 'instrumentNo': '100412', 'partyName': 'Acme', 'direction': 'issued', 'instrumentDate': '2026-10-18', 'amount': 42500, 'currencyCode': 'QAR', 'status': 'cleared'};
      await tester.pumpWidget(host(serve({}), Scaffold(body: Builder(builder: (ctx) => Column(children: [journalTile(ctx, j), chequeTile(ctx, c)])))));
      expect(find.text('JV-1'), findsOneWidget);
      expect(find.textContaining('Accrual'), findsOneWidget);
      expect(find.text('CHQ-1 · 100412'), findsOneWidget);
      expect(find.text('Cleared'), findsOneWidget);
    });
  });

  group('bill detail actions', () {
    testWidgets('a draft can be posted or deleted but not voided', (tester) async {
      await tester.pumpWidget(host(serve(bill('draft')), const BillDetailScreen(id: 'b1')));
      await tester.pumpAndSettle();
      expect(find.text('Post'), findsOneWidget);
      expect(find.text('Delete'), findsOneWidget);
      expect(find.text('Void'), findsNothing);
      expect(find.byTooltip('Edit'), findsOneWidget);
      // outstanding is total - paid and the line is quantity x price even though net is 0
      expect(find.text('QAR 750.00'), findsOneWidget);
      expect(find.text('1,000.00'), findsOneWidget);
    });

    testWidgets('posting calls the post endpoint after confirming', (tester) async {
      final log = <String>[];
      await tester.pumpWidget(host(serve(bill('draft'), log: log), const BillDetailScreen(id: 'b1')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Post'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Continue'));
      await tester.pumpAndSettle();
      expect(log, ['POST /api/v1/finance/bills/b1/post']);
    });

    testWidgets('a posted bill can be voided with a reason and not edited', (tester) async {
      final log = <String>[];
      await tester.pumpWidget(host(serve(bill('issued'), log: log), const BillDetailScreen(id: 'b1')));
      await tester.pumpAndSettle();
      expect(find.text('Post'), findsNothing);
      expect(find.byTooltip('Edit'), findsNothing);
      await tester.tap(find.text('Void'));
      await tester.pumpAndSettle();
      await tester.enterText(find.byType(TextField), 'Duplicate bill');
      await tester.tap(find.text('Confirm'));
      await tester.pumpAndSettle();
      expect(log, ['POST /api/v1/finance/bills/b1/void {"reason":"Duplicate bill"}']);
    });

    testWidgets('without the post permission the Post button is hidden', (tester) async {
      const reader = Me(fullName: '', email: '', baseCurrency: 'QAR', countryCode: '', permissions: {'finance.bills.read'}, legalEntities: [], modules: {});
      await tester.pumpWidget(host(serve(bill('draft')), const BillDetailScreen(id: 'b1'), me: reader));
      await tester.pumpAndSettle();
      expect(find.text('Post'), findsNothing);
      expect(find.text('Delete'), findsNothing);
    });
  });

  group('payment detail actions', () {
    Map<String, dynamic> payment({String status = 'issued', num unallocated = 0, String? chequeStatus}) => {
          'id': 'pay1',
          'number': 'PAY-1',
          'partyName': 'Acme',
          'date': '2026-10-30',
          'method': chequeStatus == null ? 'bankTransfer' : 'instrument',
          'currencyCode': 'QAR',
          'amount': 100,
          'unallocated': unallocated,
          'status': status,
          'paymentInstrumentId': chequeStatus == null ? null : 'ins1',
          'chequeNumber': chequeStatus == null ? null : 'CHQ1',
          'chequeStatus': chequeStatus,
          'allocations': [],
        };

    testWidgets('allocate shows only with an unallocated amount; clear only for an issued cheque', (tester) async {
      await tester.pumpWidget(host(serve(payment(unallocated: 40, chequeStatus: 'Issued')), const PaymentDetailScreen(id: 'pay1')));
      await tester.pumpAndSettle();
      expect(find.text('Allocate'), findsOneWidget);
      expect(find.text('Clear cheque'), findsOneWidget);
      expect(find.text('Void'), findsOneWidget);
    });

    testWidgets('a fully allocated bank transfer can only be voided; a voided payment has no actions', (tester) async {
      await tester.pumpWidget(host(serve(payment()), const PaymentDetailScreen(id: 'pay1')));
      await tester.pumpAndSettle();
      expect(find.text('Allocate'), findsNothing);
      expect(find.text('Clear cheque'), findsNothing);
      expect(find.text('Void'), findsOneWidget);
      await tester.pumpWidget(const SizedBox());
      await tester.pumpWidget(host(serve(payment(status: 'voided', unallocated: 40)), const PaymentDetailScreen(id: 'pay1')));
      await tester.pumpAndSettle();
      expect(find.text('Void'), findsNothing);
      expect(find.text('Allocate'), findsNothing);
    });

    testWidgets('voiding sends the reason', (tester) async {
      final log = <String>[];
      await tester.pumpWidget(host(serve(payment(), log: log), const PaymentDetailScreen(id: 'pay1')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Void'));
      await tester.pumpAndSettle();
      await tester.enterText(find.byType(TextField), 'Wrong vendor');
      await tester.tap(find.text('Confirm'));
      await tester.pumpAndSettle();
      expect(log, ['POST /api/v1/finance/payments/pay1/void {"reason":"Wrong vendor"}']);
    });
  });

  group('journal detail actions', () {
    Map<String, dynamic> journal(String status, {String source = 'manual'}) => {
          'id': 'j1',
          'number': 'JV-1',
          'memo': 'Accrual',
          'date': '2026-10-30',
          'sourceType': source,
          'status': status,
          'amount': 5,
          'currencyCode': 'QAR',
          'lines': [
            {'accountCode': '1000', 'accountName': 'Cash', 'debit': 5, 'credit': 0},
            {'accountCode': '1010', 'accountName': 'Bank', 'debit': 0, 'credit': 5},
          ],
        };

    testWidgets('draft: post and delete, with the debit and credit lines shown', (tester) async {
      await tester.pumpWidget(host(serve(journal('draft')), const JournalDetailScreen(id: 'j1')));
      await tester.pumpAndSettle();
      expect(find.text('Post'), findsOneWidget);
      expect(find.text('Delete'), findsOneWidget);
      expect(find.text('Reverse'), findsNothing);
      expect(find.text('Dr 5.00'), findsOneWidget);
      expect(find.text('Cr 5.00'), findsOneWidget);
    });

    testWidgets('posted manual journal can be reversed with a memo; a system journal cannot', (tester) async {
      final log = <String>[];
      await tester.pumpWidget(host(serve(journal('posted'), log: log), const JournalDetailScreen(id: 'j1')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Reverse'));
      await tester.pumpAndSettle();
      await tester.enterText(find.byType(TextField), 'Booked twice');
      await tester.tap(find.text('Confirm'));
      await tester.pumpAndSettle();
      expect(log, ['POST /api/v1/finance/journals/j1/reverse {"memo":"Booked twice"}']);

      await tester.pumpWidget(const SizedBox());
      await tester.pumpWidget(host(serve(journal('posted', source: 'invoice')), const JournalDetailScreen(id: 'j1')));
      await tester.pumpAndSettle();
      expect(find.text('Reverse'), findsNothing);
    });
  });

  group('cheque detail', () {
    testWidgets('only an issued cheque can be cleared', (tester) async {
      Map<String, dynamic> cheque(String status) => {'id': 'c1', 'number': 'CHQ-1', 'instrumentNo': '1', 'direction': 'issued', 'kind': 'cheque', 'status': status, 'amount': 5, 'currencyCode': 'QAR', 'instrumentDate': '2026-10-30'};
      await tester.pumpWidget(host(serve(cheque('issued')), const ChequeDetailScreen(id: 'c1')));
      await tester.pumpAndSettle();
      expect(find.text('Clear'), findsOneWidget);
      await tester.pumpWidget(const SizedBox());
      await tester.pumpWidget(host(serve(cheque('cleared')), const ChequeDetailScreen(id: 'c1')));
      await tester.pumpAndSettle();
      expect(find.text('Clear'), findsNothing);
    });
  });

  group('approvals', () {
    final pending = {
      'id': 'ap1',
      'subject': 'Bill AC-77 from Acme',
      'documentType': 'bill',
      'amount': 5000,
      'currencyCode': 'QAR',
      'status': 'pending',
      'requestedAt': '2026-10-30T10:00:00Z',
    };

    test('accepts a plain array or a paged answer', () {
      expect(approvalRows([pending]), hasLength(1));
      expect(approvalRows({'items': [pending], 'total': 1}), hasLength(1));
      expect(approvalRows(null), isEmpty);
    });

    testWidgets('approving posts the comment to the approve endpoint', (tester) async {
      final log = <String>[];
      await tester.pumpWidget(host(serve([pending], log: log), const ApprovalsScreen()));
      await tester.pumpAndSettle();
      expect(find.text('Bill AC-77 from Acme'), findsOneWidget);
      expect(find.textContaining('Vendor bill'), findsOneWidget);
      await tester.tap(find.widgetWithText(FilledButton, 'Approve'));
      await tester.pumpAndSettle();
      await tester.enterText(find.byType(TextField), 'Looks right');
      await tester.tap(find.widgetWithText(FilledButton, 'Approve').last);
      await tester.pumpAndSettle();
      expect(log, ['POST /api/v1/approvals/ap1/approve {"comment":"Looks right"}']);
    });

    testWidgets('a user who cannot decide sees no buttons; decided requests show their status', (tester) async {
      const viewer = Me(fullName: '', email: '', baseCurrency: 'QAR', countryCode: '', permissions: {}, legalEntities: [], modules: {});
      await tester.pumpWidget(host(serve([pending, {...pending, 'id': 'ap2', 'status': 'approved'}]), const ApprovalsScreen(), me: viewer));
      await tester.pumpAndSettle();
      expect(find.text('Approve'), findsNothing);
      expect(find.text('Approved'), findsOneWidget);
    });
  });

  group('reports', () {
    Widget body(ReportKind k, Map<String, dynamic> d) => MaterialApp(home: Scaffold(body: Builder(builder: (c) => reportBody(c, k, d, 'QAR'))));

    testWidgets('ageing shows buckets per vendor and the totals', (tester) async {
      await tester.pumpWidget(body(ReportKind.apAgeing, {
        'rows': [
          {'partyName': 'Acme', 'current': 100, 'days1To30': 200, 'days31To60': 0, 'days61To90': 0, 'over90': 50, 'total': 350}
        ],
        'totals': {'current': 100, 'days1To30': 200, 'days31To60': 0, 'days61To90': 0, 'over90': 50, 'total': 350},
      }));
      expect(find.text('Acme'), findsOneWidget);
      expect(find.text('Total'), findsWidgets);
      expect(find.text('350.00'), findsNWidgets(2));
    });

    testWidgets('trial balance, profit and loss, balance sheet and cash render their figures', (tester) async {
      await tester.pumpWidget(body(ReportKind.trialBalance, {
        'rows': [
          {'code': '1000', 'name': 'Cash', 'opening': 0, 'debit': 10, 'credit': 2, 'closing': 8}
        ],
        'totalDebit': 10,
        'totalCredit': 10,
        'balanced': true,
      }));
      expect(find.text('1000 Cash'), findsOneWidget);
      expect(find.text('Yes'), findsOneWidget);
      await tester.pumpWidget(body(ReportKind.incomeStatement, {
        'income': [
          {'group': 'Revenue', 'code': '4000', 'name': 'Sales', 'amount': 900}
        ],
        'expenses': [],
        'totalIncome': 900,
        'totalExpenses': 0,
        'netProfit': 900,
      }));
      expect(find.text('4000 Sales'), findsOneWidget);
      expect(find.text('Net profit'), findsOneWidget);
      await tester.pumpWidget(body(ReportKind.balanceSheet, {'assets': [], 'liabilities': [], 'equity': [], 'totalAssets': 5, 'totalLiabilities': 2, 'totalEquity': 3, 'currentEarnings': 0, 'balanced': true}));
      expect(find.text('Total assets'), findsWidgets);
      await tester.pumpWidget(body(ReportKind.cashPosition, {
        'total': 77,
        'accounts': [
          {'code': '1010', 'name': 'Bank', 'isBank': true, 'balance': 77}
        ]
      }));
      expect(find.text('QAR 77.00'), findsOneWidget);
    });

    testWidgets('the reports menu lists every report', (tester) async {
      await tester.pumpWidget(host(serve({}), const ReportsScreen()));
      for (final r in reportSpecs) {
        expect(find.text(r.title), findsOneWidget);
      }
    });
  });
}
