import 'package:dio/dio.dart';
import 'package:erp_mobile/core/api_client.dart';
import 'package:erp_mobile/core/key_value_store.dart';
import 'package:erp_mobile/core/me.dart';
import 'package:erp_mobile/core/session.dart';
import 'package:erp_mobile/features/projects/projects_extra_screens.dart';
import 'package:erp_mobile/kit/field.dart';
import 'package:erp_mobile/kit/form_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/fake_adapter.dart';

Widget host(FakeAdapter adapter, Widget child, {Set<String> permissions = const {'*'}}) {
  final store = MemoryKeyValueStore();
  return ProviderScope(
    overrides: [
      storeProvider.overrideWithValue(store),
      apiProvider.overrideWith((ref) => ApiClient(store: store, baseUrl: 'http://api.test', dio: Dio()..httpClientAdapter = adapter)),
      meProvider.overrideWith((ref) async => Me(fullName: 'A', email: 'a@x', baseCurrency: 'QAR', countryCode: 'QA', permissions: permissions, legalEntities: const [], modules: const {'projects'})),
    ],
    child: MaterialApp(home: child),
  );
}

void main() {
  test('the timer adds whole quarter hours and never less than a quarter', () {
    expect(timerHours(const Duration(minutes: 2)), 0.25);
    expect(timerHours(const Duration(minutes: 40)), 0.75);
    expect(timerHours(const Duration(minutes: 95)), 1.5);
    expect(timerHours(const Duration(hours: 3)), 3.0);
  });

  test('a week starts on Monday and days are written yyyy-mm-dd', () {
    expect(isoDay(mondayOf(DateTime(2026, 6, 7))), '2026-06-01'); // a Sunday belongs to the week that began on Monday
    expect(isoDay(mondayOf(DateTime(2026, 6, 1))), '2026-06-01');
    expect(isoDay(DateTime(2026, 1, 5)), '2026-01-05');
  });

  testWidgets('an expense needs a project, a description and an amount', (tester) async {
    late FormSpec spec;
    await tester.pumpWidget(ProviderScope(child: Consumer(builder: (context, ref, _) { spec = expenseForm(ref); return const SizedBox(); })));
    final values = FormEngine.start(spec.fields, spec.initial);
    final errors = FormEngine.validate(spec.fields, values);
    expect(errors['projectId'], isNotNull);
    expect(errors['description'], isNotNull);
    expect(errors['amount'], isNotNull);
    values
      ..['projectId'] = 'p1'
      ..['description'] = 'Taxi'
      ..['amount'] = '40';
    expect(FormEngine.validate(spec.fields, values), isEmpty);
    expect(spec.fields.firstWhere((f) => f.key == 'markupPercent').visibleWhen?.call({...values, 'isBillable': false}), isFalse);
  });

  testWidgets('an allocation cannot end before it starts', (tester) async {
    late FormSpec spec;
    await tester.pumpWidget(ProviderScope(child: Consumer(builder: (context, ref, _) { spec = allocationForm(ref); return const SizedBox(); })));
    final values = FormEngine.start(spec.fields, spec.initial)
      ..['projectId'] = 'p1'
      ..['userId'] = 'u1'
      ..['fromDate'] = '2026-06-10'
      ..['toDate'] = '2026-06-01';
    expect(FormEngine.validate(spec.fields, values)['toDate'], isNotNull);
    values['toDate'] = '2026-06-30';
    expect(FormEngine.validate(spec.fields, values), isEmpty);
  });

  testWidgets('approvals list what is waiting and offer approve and reject', (tester) async {
    final adapter = FakeAdapter((o) {
      if (o.path.endsWith('/timesheets')) return envelope({'items': [{'id': 't1', 'hours': 3, 'workerName': 'Ann', 'date': '2026-06-01', 'description': 'Design', 'totalAmount': 300}], 'total': 1});
      if (o.path.endsWith('/expenses')) return envelope({'items': [], 'total': 0});
      return envelope({'decided': 1});
    });
    await tester.pumpWidget(host(adapter, const ApprovalsScreen()));
    await tester.pumpAndSettle();
    expect(find.textContaining('Ann'), findsOneWidget);
    expect(find.byTooltip('Approve'), findsOneWidget);
    await tester.tap(find.byTooltip('Approve'));
    await tester.pumpAndSettle();
    expect(adapter.requests.any((r) => r.method == 'POST' && r.path.endsWith('/timesheets/decide')), isTrue);
  });

  testWidgets('a change order shows its amount and status', (tester) async {
    final adapter = FakeAdapter((_) => envelope([{'id': 'c1', 'number': 'CO-PRJ-01', 'title': 'Extra scope', 'amountChange': 500, 'hoursChange': 10, 'status': 'draft'}]));
    await tester.pumpWidget(host(adapter, const ChangeOrdersScreen()));
    await tester.pumpAndSettle();
    expect(find.textContaining('Extra scope'), findsOneWidget);
    expect(find.text('Draft'), findsOneWidget);
  });

  testWidgets('my week adds up the hours of the week and starts a timer', (tester) async {
    final adapter = FakeAdapter((o) => envelope({'items': [
          {'id': 't1', 'hours': 2, 'projectName': 'Site', 'date': '2026-06-01', 'workerName': 'Ann', 'approvalStatus': 'approved'},
          {'id': 't2', 'hours': 3.5, 'projectName': 'Site', 'date': '2026-06-02', 'workerName': 'Ann', 'approvalStatus': 'draft'},
        ], 'total': 2}));
    await tester.pumpWidget(host(adapter, const MyWeekScreen()));
    await tester.pumpAndSettle();
    expect(find.textContaining('Total this week: 5.5 h'), findsOneWidget);
    expect(find.text('Start'), findsOneWidget);
  });
}
