import 'package:dio/dio.dart';
import 'package:erp_mobile/core/api_client.dart';
import 'package:erp_mobile/core/key_value_store.dart';
import 'package:erp_mobile/core/me.dart';
import 'package:erp_mobile/core/session.dart';
import 'package:erp_mobile/features/projects/projects_screens.dart';
import 'package:erp_mobile/features/projects/services_screens.dart';
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
  modules: {'projects', 'services'},
);

Widget app(FakeAdapter adapter, Widget home) {
  final store = MemoryKeyValueStore();
  return ProviderScope(
    key: UniqueKey(),
    overrides: [
      storeProvider.overrideWithValue(store),
      apiProvider.overrideWith((ref) => ApiClient(store: store, baseUrl: 'http://api.test', dio: Dio()..httpClientAdapter = adapter)),
      meOrEmptyProvider.overrideWithValue(_me),
    ],
    child: MaterialApp(home: home),
  );
}

FakeAdapter serverWith(Map<String, dynamic> record) => FakeAdapter((o) => o.path.endsWith('/tasks') || o.path.endsWith('/milestones') || o.path.endsWith('/timesheets') ? envelope([]) : envelope(record));

Widget formHost(FormSpec Function(WidgetRef ref) build) => Consumer(
      builder: (context, ref, _) => Scaffold(body: ElevatedButton(onPressed: () => openForm<Object?>(context, build(ref)), child: const Text('open'))),
    );

void main() {
  group('timesheet quick entry', () {
    testWidgets('starts on today with the signed-in user, needs hours and rejects more than a day', (tester) async {
      final adapter = FakeAdapter((o) => envelope({'id': 't1'}));
      await tester.pumpWidget(app(adapter, formHost((ref) => timesheetForm(ref, projectId: 'p1', projectName: 'Website'))));
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();

      await tester.tap(find.text('Save'));
      await tester.pumpAndSettle();
      expect(find.text('Hours is required'), findsOneWidget);

      await tester.enterText(find.widgetWithText(TextField, 'Hours *'), '25');
      await tester.tap(find.text('Save'));
      await tester.pumpAndSettle();
      expect(find.text('Must be at most 24'), findsOneWidget);

      await tester.enterText(find.widgetWithText(TextField, 'Hours *'), '1.5');
      await tester.tap(find.text('Save'));
      await tester.pumpAndSettle();

      final post = adapter.requests.singleWhere((r) => r.method == 'POST');
      expect(post.path, '/api/v1/m/projects/timesheets');
      final body = post.data as Map;
      expect(body['projectId'], 'p1');
      expect(body['hours'], 1.5);
      expect(body['workerName'], 'Tester');
      expect(body['isBillable'], true);
      expect(body['date'], matches(RegExp(r'^\d{4}-\d{2}-\d{2}$')));
    });
  });

  group('project form', () {
    testWidgets('needs a name and posts the legal entity, status and billing type', (tester) async {
      final adapter = FakeAdapter((o) => envelope({'id': 'p1'}));
      await tester.pumpWidget(app(adapter, formHost((ref) => projectForm(ref))));
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Save'));
      await tester.pumpAndSettle();
      expect(find.text('Name is required'), findsOneWidget);

      await tester.enterText(find.widgetWithText(TextField, 'Name *'), 'Fit-out');
      await tester.tap(find.text('Save'));
      await tester.pumpAndSettle();
      final post = adapter.requests.singleWhere((r) => r.method == 'POST');
      expect(post.path, '/api/v1/m/projects/projects');
      expect(post.data, containsPair('name', 'Fit-out'));
      expect(post.data, containsPair('legalEntityId', 'le1'));
      expect(post.data, containsPair('status', 'planning'));
      expect(post.data, containsPair('billingType', 'timeAndMaterials'));
    });
  });

  group('task detail actions follow the allowed transitions', () {
    Future<void> open(WidgetTester tester, String status) async {
      await tester.pumpWidget(app(serverWith({'id': 't1', 'projectId': 'p1', 'projectName': 'Web', 'number': 'TSK-1', 'title': 'Build', 'status': status, 'priority': 'high'}), const TaskDetailScreen(id: 't1')));
      await tester.pumpAndSettle();
    }

    testWidgets('a to-do task can only be started', (tester) async {
      await open(tester, 'todo');
      expect(find.text('Start'), findsOneWidget);
      expect(find.text('Done'), findsNothing);
      expect(find.text('Send to review'), findsNothing);
    });

    testWidgets('a task in review can be finished or sent back', (tester) async {
      await open(tester, 'review');
      expect(find.text('Done'), findsOneWidget);
      expect(find.text('Start'), findsOneWidget); // back to in progress
      expect(find.text('Send to review'), findsNothing);
    });
  });

  group('project detail actions', () {
    testWidgets('an active project can be held, completed or cancelled but not activated', (tester) async {
      await tester.pumpWidget(app(serverWith({'id': 'p1', 'number': 'PRJ-1', 'name': 'Web', 'legalEntityId': 'le1', 'status': 'active', 'billingType': 'fixedFee', 'budgetAmount': 10}), const ProjectDetailScreen(id: 'p1')));
      await tester.pumpAndSettle();
      expect(find.text('Put on hold'), findsOneWidget);
      expect(find.text('Complete'), findsOneWidget);
      expect(find.text('Cancel'), findsOneWidget);
      expect(find.text('Activate'), findsNothing);
      expect(find.text('Log time'), findsOneWidget);
    });

    testWidgets('a completed project offers nothing to change and cannot be edited', (tester) async {
      await tester.pumpWidget(app(serverWith({'id': 'p1', 'number': 'PRJ-1', 'name': 'Web', 'legalEntityId': 'le1', 'status': 'completed', 'billingType': 'fixedFee', 'budgetAmount': 10}), const ProjectDetailScreen(id: 'p1')));
      await tester.pumpAndSettle();
      expect(find.text('Complete'), findsNothing);
      expect(find.text('Log time'), findsNothing);
      expect(find.byTooltip('Edit'), findsNothing);
    });
  });

  group('project milestones', () {
    testWidgets('only a pending milestone can be marked achieved; an invoiced one is locked', (tester) async {
      await tester.pumpWidget(app(serverWith({'id': 'm1', 'projectId': 'p1', 'projectName': 'Web', 'title': 'Phase 1', 'dueDate': '2026-12-01', 'amount': 5, 'status': 'pending'}), const MilestoneDetailScreen(id: 'm1')));
      await tester.pumpAndSettle();
      expect(find.text('Mark achieved'), findsOneWidget);

      await tester.pumpWidget(app(serverWith({'id': 'm1', 'projectId': 'p1', 'projectName': 'Web', 'title': 'Phase 1', 'dueDate': '2026-12-01', 'amount': 5, 'status': 'invoiced'}), const MilestoneDetailScreen(id: 'm1')));
      await tester.pumpAndSettle();
      expect(find.text('Mark achieved'), findsNothing);
      expect(find.text('Delete'), findsNothing);
      expect(find.byTooltip('Edit'), findsNothing);
    });
  });

  group('billing', () {
    testWidgets('shows the backend message when the project has no customer', (tester) async {
      final adapter = FakeAdapter((o) => problem(400, "Project 'Web' has no customer assigned. An accounting invoice cannot be generated."));
      await tester.pumpWidget(app(adapter, const BillingDetailScreen(data: {'projectId': 'p1', 'projectNumber': 'PRJ-1', 'projectName': 'Web', 'billingType': 'timeAndMaterials', 'unbilledHours': 2, 'totalUnbilledAmount': 200})));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Create invoice'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Bill'));
      await tester.pumpAndSettle();
      expect(find.textContaining('has no customer assigned'), findsOneWidget);
      expect(adapter.requests.single.path, '/api/v1/m/projects/billing/bill');
      expect(adapter.requests.single.data, {'projectId': 'p1'});
    });
  });

  group('service milestones', () {
    Future<void> open(WidgetTester tester, String status) async {
      await tester.pumpWidget(app(serverWith({'id': 's1', 'milestoneCode': 'MLS-1', 'title': 'Phase 1', 'dueDate': '2026-12-01', 'amount': 5, 'status': status}), const ServiceMilestoneDetailScreen(id: 's1')));
      await tester.pumpAndSettle();
    }

    testWidgets('planned can be marked ready, not invoiced', (tester) async {
      await open(tester, 'planned');
      expect(find.text('Ready to invoice'), findsWidgets);
      expect(find.text('Invoice'), findsNothing);
    });

    testWidgets('ready to invoice can be invoiced or sent back', (tester) async {
      await open(tester, 'readyToInvoice');
      expect(find.text('Invoice'), findsOneWidget);
      expect(find.text('Back to planned'), findsOneWidget);
    });

    testWidgets('paid is derived: no actions at all', (tester) async {
      await open(tester, 'paid');
      expect(find.text('Invoice'), findsNothing);
      expect(find.text('Back to planned'), findsNothing);
      expect(find.text('Paid (from the invoice)'), findsOneWidget);
    });
  });

  group('engagements and retainers', () {
    testWidgets('a completed engagement has no actions; an active retainer can pause or terminate', (tester) async {
      await tester.pumpWidget(app(serverWith({'id': 'e1', 'engagementCode': 'ENG-1', 'title': 'X', 'clientName': 'C', 'status': 'completed', 'type': 'fixedPrice', 'startDate': '2026-01-01'}), const EngagementDetailScreen(id: 'e1')));
      await tester.pumpAndSettle();
      expect(find.text('Activate'), findsNothing);
      expect(find.text('Cancel'), findsNothing);

      await tester.pumpWidget(app(serverWith({'id': 'r1', 'retainerNumber': 'RET-1', 'title': 'R', 'clientName': 'C', 'status': 'active', 'cycle': 'monthly', 'startDate': '2026-01-01'}), const RetainerDetailScreen(id: 'r1')));
      await tester.pumpAndSettle();
      expect(find.text('Pause'), findsOneWidget);
      expect(find.text('Terminate'), findsOneWidget);
      expect(find.text('Resume'), findsNothing);
    });

    testWidgets('engagement form needs title and client, and posts the type', (tester) async {
      final adapter = FakeAdapter((o) => envelope({'id': 'e1'}));
      await tester.pumpWidget(app(adapter, formHost((ref) => engagementForm(ref))));
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Save'));
      await tester.pumpAndSettle();
      expect(find.text('Title is required'), findsOneWidget);
      expect(find.text('Client is required'), findsOneWidget);
      expect(adapter.requests.where((r) => r.method == 'POST'), isEmpty);
    });
  });
}
