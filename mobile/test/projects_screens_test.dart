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
      // the everyday actions stay in the bar, so the others no longer cover the record
      expect(find.text('Log time'), findsOneWidget);
      expect(find.text('Add task'), findsOneWidget);
      expect(find.text('Put on hold'), findsNothing);
      expect(find.text('Delete project'), findsNothing);

      await tester.tap(find.byKey(const ValueKey('detail-more')));
      await tester.pumpAndSettle();
      expect(find.text('Put on hold'), findsOneWidget);
      expect(find.text('Complete project'), findsOneWidget);
      expect(find.text('Cancel project'), findsOneWidget);
      expect(find.text('Delete project'), findsOneWidget);
      expect(find.text('Activate'), findsNothing);
      // the destructive actions come last
      expect(tester.getTopLeft(find.text('Cancel project')).dy, greaterThan(tester.getTopLeft(find.text('Clone project')).dy));
      expect(tester.getTopLeft(find.text('Delete project')).dy, greaterThan(tester.getTopLeft(find.text('Cancel project')).dy));
    });

    testWidgets('a planned project is activated from the bar', (tester) async {
      await tester.pumpWidget(app(serverWith({'id': 'p1', 'number': 'PRJ-1', 'name': 'Web', 'legalEntityId': 'le1', 'status': 'planning', 'billingType': 'fixedFee', 'budgetAmount': 10}), const ProjectDetailScreen(id: 'p1')));
      await tester.pumpAndSettle();
      expect(find.text('Activate'), findsOneWidget);
      expect(find.text('Log time'), findsOneWidget);
      expect(find.text('Add task'), findsNothing);
    });

    testWidgets('status, health, billing and progress are shown together', (tester) async {
      await tester.pumpWidget(app(serverWith({'id': 'p1', 'number': 'PRJ-1', 'name': 'Web', 'legalEntityId': 'le1', 'status': 'active', 'health': 'atRisk', 'billingType': 'fixedFee', 'budgetAmount': 100, 'effectiveBudget': 200, 'totalTimesheetCost': 300, 'percentComplete': 53.3, 'completedTasks': 1, 'totalTasks': 2, 'overdueTasks': 1, 'totalLoggedHours': 51.0, 'unbilledAmount': 7}), const ProjectDetailScreen(id: 'p1')));
      await tester.pumpAndSettle();
      expect(find.text('Active'), findsOneWidget);
      expect(find.text('At risk'), findsOneWidget);
      expect(find.text('Fixed fee'), findsOneWidget);
      expect(find.text('53%'), findsOneWidget);
      expect(find.text('150%'), findsOneWidget);
      expect(find.text('51 h'), findsOneWidget);
      expect(find.text('1 of 2 tasks done, 1 overdue'), findsOneWidget);
      expect(find.text('Ready to bill'), findsOneWidget);
    });

    testWidgets('time entry rows fit their chips and leave out the project name', (tester) async {
      tester.view.physicalSize = const Size(1080, 7200);
      tester.view.devicePixelRatio = 3;
      addTearDown(tester.view.reset);
      final adapter = FakeAdapter((o) {
        if (o.path.endsWith('/timesheets')) {
          return envelope({'items': [{'id': 't1', 'hours': 7.0, 'workerName': 'Sarah', 'date': '2026-11-02', 'projectName': 'Big Project', 'description': 'Project work', 'totalAmount': 1050, 'isBillable': true, 'isBilled': false, 'approvalStatus': 'draft'}], 'total': 1});
        }
        if (o.path.endsWith('/tasks') || o.path.endsWith('/milestones') || o.path.endsWith('/expenses')) return envelope([]);
        return envelope({'id': 'p1', 'number': 'PRJ-1', 'name': 'Web', 'legalEntityId': 'le1', 'status': 'active', 'billingType': 'fixedFee', 'budgetAmount': 10});
      });
      await tester.pumpWidget(app(adapter, const ProjectDetailScreen(id: 'p1')));
      await tester.pumpAndSettle();
      expect(find.text('7 h · Sarah'), findsOneWidget);
      expect(find.text('Unbilled'), findsOneWidget);
      expect(find.text('Draft'), findsOneWidget);
      expect(find.textContaining('Big Project'), findsNothing);
      expect(tester.takeException(), isNull);
    });

    testWidgets('a task in progress keeps its next step and logging time in the bar', (tester) async {
      await tester.pumpWidget(app(serverWith({'id': 't1', 'number': 'TSK-1', 'title': 'Design', 'projectId': 'p1', 'projectName': 'Web', 'status': 'inProgress', 'priority': 'medium'}), const TaskDetailScreen(id: 't1')));
      await tester.pumpAndSettle();
      expect(find.text('Send to review'), findsOneWidget);
      expect(find.text('Log time'), findsOneWidget);
      expect(find.text('Cancel task'), findsNothing);
      expect(find.text('Delete'), findsNothing);
      await tester.tap(find.byKey(const ValueKey('detail-more')));
      await tester.pumpAndSettle();
      for (final t in ['Back to to do', 'Mark blocked', 'Cancel task', 'Delete']) {
        expect(find.text(t), findsOneWidget, reason: t);
      }
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
      expect(adapter.requests.single.data, {'projectId': 'p1', 'grouping': 'detailed', 'discountPercent': 0});
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
