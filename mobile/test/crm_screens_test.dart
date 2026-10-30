import 'package:dio/dio.dart';
import 'package:erp_mobile/core/api_client.dart';
import 'package:erp_mobile/core/key_value_store.dart';
import 'package:erp_mobile/core/me.dart';
import 'package:erp_mobile/core/session.dart';
import 'package:erp_mobile/features/crm/crm_screens.dart';
import 'package:erp_mobile/kit/field.dart';
import 'package:erp_mobile/kit/form_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/fake_adapter.dart';

const _me = Me(
  fullName: 'Tester',
  email: 't@x.test',
  baseCurrency: 'QAR',
  countryCode: 'QA',
  permissions: {'*'},
  legalEntities: [LegalEntity(id: 'le1', code: 'E1', name: 'Entity 1', baseCurrency: 'QAR')],
  modules: {'crm'},
);

Widget app(FakeAdapter adapter, Widget home) {
  final store = MemoryKeyValueStore();
  return ProviderScope(
    overrides: [
      storeProvider.overrideWithValue(store),
      apiProvider.overrideWith((ref) => ApiClient(store: store, baseUrl: 'http://api.test', dio: Dio()..httpClientAdapter = adapter)),
      meOrEmptyProvider.overrideWithValue(_me),
    ],
    child: MaterialApp(home: home),
  );
}

FakeAdapter serverWith(Map<String, dynamic> record) => FakeAdapter((o) => o.path.contains('by-entity') ? envelope([]) : envelope(record));

/// Opens the form built by [build] and returns what the last request sent.
Widget formHost(FormSpec Function(WidgetRef ref) build) => Consumer(
      builder: (context, ref, _) => Scaffold(body: ElevatedButton(onPressed: () => openForm<Object?>(context, build(ref)), child: const Text('open'))),
    );

void main() {
  group('lead form', () {
    testWidgets('needs a full name and sends the cleaned body', (tester) async {
      final adapter = FakeAdapter((o) => envelope({'id': 'l1'}));
      await tester.pumpWidget(app(adapter, formHost((ref) => leadForm(ref))));
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Save'));
      await tester.pumpAndSettle();
      expect(find.text('Full name is required'), findsOneWidget);
      expect(adapter.requests.where((r) => r.method == 'POST'), isEmpty);

      await tester.enterText(find.widgetWithText(TextField, 'Full name *'), 'Ada Lovelace');
      await tester.tap(find.text('Save'));
      await tester.pumpAndSettle();

      final post = adapter.requests.singleWhere((r) => r.method == 'POST');
      expect(post.path, '/api/v1/m/crm/leads');
      expect(post.data, {'legalEntityId': 'le1', 'fullName': 'Ada Lovelace', 'source': 'website', 'estimatedValue': 0});
    });
  });

  group('activity form', () {
    testWidgets('logging from a lead binds it to that lead and defaults to a done call', (tester) async {
      final adapter = FakeAdapter((o) => envelope({'id': 'i1'}));
      await tester.pumpWidget(app(adapter, formHost((ref) => interactionForm(ref, kind: 'Lead', entityId: 'l1', entityLabel: 'Ada', legalEntityId: 'le1'))));
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Log'));
      await tester.pumpAndSettle();
      expect(find.text('Subject is required'), findsOneWidget);

      await tester.enterText(find.widgetWithText(TextField, 'Subject *'), 'Intro call');
      await tester.tap(find.text('Log'));
      await tester.pumpAndSettle();

      final post = adapter.requests.singleWhere((r) => r.method == 'POST');
      expect(post.path, '/api/v1/m/crm/interactions');
      expect(post.data, {'type': 'call', 'subject': 'Intro call', 'isResolved': true, 'legalEntityId': 'le1', 'relatedEntityKind': 'Lead', 'relatedEntityId': 'l1'});
    });
  });

  group('tiles', () {
    testWidgets('lead and opportunity tiles show a readable status', (tester) async {
      await tester.pumpWidget(app(FakeAdapter((_) => envelope({})), Scaffold(body: Column(children: [
        Builder(builder: (c) => leadTile(c, {'id': 'l1', 'fullName': 'Ada', 'leadNumber': 'LEAD-1', 'companyName': 'Acme', 'status': 'tradeShow' == '' ? '' : 'contacted', 'estimatedValue': 100})),
        Builder(builder: (c) => opportunityTile(c, {'id': 'o1', 'title': 'Big deal', 'customerName': 'Acme', 'stage': 'closedWon', 'probabilityPercent': 100, 'expectedRevenue': 5000})),
      ]))));
      expect(find.text('Contacted'), findsOneWidget);
      expect(find.text('Closed won'), findsOneWidget);
      expect(find.text('Ada'), findsOneWidget);
    });
  });

  group('lead detail actions', () {
    Future<void> open(WidgetTester tester, String status) async {
      await tester.pumpWidget(app(serverWith({'id': 'l1', 'legalEntityId': 'le1', 'leadNumber': 'LEAD-1', 'fullName': 'Ada', 'source': 'website', 'status': status, 'estimatedValue': 10}), const LeadDetailScreen(id: 'l1')));
      await tester.pumpAndSettle();
    }

    testWidgets('a new lead can be contacted, qualified, unqualified and converted', (tester) async {
      await open(tester, 'new');
      for (final a in ['Contacted', 'Qualify', 'Unqualify', 'Convert', 'Log activity']) {
        expect(find.text(a), findsOneWidget, reason: a);
      }
    });

    testWidgets('a qualified lead is not offered Qualify again', (tester) async {
      await open(tester, 'qualified');
      expect(find.text('Qualify'), findsNothing);
      expect(find.text('Convert'), findsOneWidget);
    });

    testWidgets('a converted lead only offers to log activity', (tester) async {
      await open(tester, 'converted');
      expect(find.text('Convert'), findsNothing);
      expect(find.text('Qualify'), findsNothing);
      expect(find.text('Unqualify'), findsNothing);
      expect(find.byTooltip('Edit'), findsNothing);
      expect(find.text('Log activity'), findsOneWidget);
    });
  });

  group('opportunity detail actions', () {
    Future<void> open(WidgetTester tester, String stage) async {
      await tester.pumpWidget(app(serverWith({'id': 'o1', 'legalEntityId': 'le1', 'opportunityNumber': 'OPP-1', 'title': 'Deal', 'customerName': 'Acme', 'stage': stage, 'probabilityPercent': 25, 'expectedRevenue': 100}), const OpportunityDetailScreen(id: 'o1')));
      await tester.pumpAndSettle();
    }

    testWidgets('qualification can only go to proposal or be closed lost', (tester) async {
      await open(tester, 'qualification');
      expect(find.text('Move to Proposal'), findsOneWidget);
      expect(find.text('Move to Negotiation'), findsNothing);
      expect(find.text('Close won'), findsNothing);
      expect(find.text('Close lost'), findsOneWidget);
    });

    testWidgets('only negotiation can be won', (tester) async {
      await open(tester, 'negotiation');
      expect(find.text('Close won'), findsOneWidget);
    });

    testWidgets('a won deal has no stage actions', (tester) async {
      await open(tester, 'closedWon');
      expect(find.text('Close won'), findsNothing);
      expect(find.text('Close lost'), findsNothing);
      expect(find.textContaining('Move to'), findsNothing);
    });

    testWidgets('closing as lost needs a reason and sends isWon false', (tester) async {
      final adapter = FakeAdapter((o) => envelope({'id': 'o1'}));
      await tester.pumpWidget(app(adapter, formHost((ref) => closeOpportunityForm({'id': 'o1', 'expectedRevenue': 100}, won: false))));
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Close lost'));
      await tester.pumpAndSettle();
      expect(find.text('Reason lost is required'), findsOneWidget);
      await tester.enterText(find.widgetWithText(TextField, 'Reason lost *'), 'Budget');
      await tester.tap(find.text('Close lost'));
      await tester.pumpAndSettle();
      final post = adapter.requests.singleWhere((r) => r.method == 'POST');
      expect(post.path, '/api/v1/m/crm/opportunities/o1/close');
      expect(post.data, {'id': 'o1', 'isWon': false, 'actualRevenue': 0, 'closeReason': 'Budget'});
    });
  });

  test('stage moves mirror the backend rules', () {
    expect(stageMoves['negotiation'], ['proposal']);
    expect(stageMoves['closedWon'], isEmpty);
    expect(FormEngine.check(Field.integer('p', 'P', min: 0, max: 100), '150', {}), 'Must be at most 100');
  });
}
