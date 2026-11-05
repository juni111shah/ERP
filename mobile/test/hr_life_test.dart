import 'package:dio/dio.dart';
import 'package:erp_mobile/core/api_client.dart';
import 'package:erp_mobile/core/key_value_store.dart';
import 'package:erp_mobile/core/me.dart';
import 'package:erp_mobile/core/session.dart';
import 'package:erp_mobile/features/hr/life_screens.dart';
import 'package:erp_mobile/kit/field.dart';
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
      meProvider.overrideWith((ref) async => Me(fullName: 'A', email: 'a@x', baseCurrency: 'QAR', countryCode: 'QA', permissions: permissions, legalEntities: const [], modules: const {'hr'})),
    ],
    child: MaterialApp(home: child),
  );
}

Map<String, dynamic> candidate(String stage) => {'id': 'c1', 'openingId': 'o1', 'fullName': 'Noor Hassan', 'email': 'noor@x.com', 'stage': stage, 'expectedSalary': 4000};

void main() {
  shiftTests();
  rosterTests();
  test('times are read as hh:mm and bad ones are flagged', () {
    expect(timeOf('8:05'), '08:05:00');
    expect(timeOf('17:30'), '17:30:00');
    expect(timeOf(''), isNull);
    expect(timeOf(null), isNull);
    expect(timeOf('25:00'), 'invalid');
    expect(timeOf('nine'), 'invalid');
  });

  test('slab and checklist text become the lists the server expects', () {
    expect(slabsOf('0:0, 1000:10'), [
      {'fromAmount': 0, 'rate': 0},
      {'fromAmount': 1000, 'rate': 10},
    ]);
    expect(slabsOf(''), isEmpty);
    expect(checklistItemsOf('Laptop | 0\nContract | 7\n\nExit | -3'), [
      {'title': 'Laptop', 'dueOffsetDays': 0},
      {'title': 'Contract', 'dueOffsetDays': 7},
      {'title': 'Exit', 'dueOffsetDays': -3},
    ]);
    expect(checklistItemsOf('No offset'), [
      {'title': 'No offset', 'dueOffsetDays': 0},
    ]);
  });

  test('the daily attendance form checks times and needs an employee and date when adding', () {
    final spec = dailyForm();
    final values = FormEngine.start(spec.fields, spec.initial);
    final errors = FormEngine.validate(spec.fields, values);
    expect(errors['employeeId'], isNotNull);
    values
      ..['employeeId'] = 'e1'
      ..['date'] = '2026-10-05'
      ..['checkIn'] = '8am';
    expect(FormEngine.validate(spec.fields, values)['checkIn'], isNotNull);
    values['checkIn'] = '08:00';
    expect(FormEngine.validate(spec.fields, values), isEmpty);
  });

  test('a statutory rule needs a code, a name and an entity, and slabs only show for the slab method', () {
    final spec = statutoryForm();
    final flat = FormEngine.start(spec.fields, spec.initial);
    expect(spec.fields.firstWhere((f) => f.key == 'slabs').visibleWhen?.call({...flat, 'method': 'flat'}), isFalse);
    expect(spec.fields.firstWhere((f) => f.key == 'slabs').visibleWhen?.call({...flat, 'method': 'slab'}), isTrue);
    expect(spec.fields.firstWhere((f) => f.key == 'rate').visibleWhen?.call({...flat, 'method': 'slab'}), isFalse);
    final errors = FormEngine.validate(spec.fields, flat);
    expect(errors['code'], isNotNull);
    expect(errors['name'], isNotNull);
  });

  testWidgets('a candidate with an offer can be hired but not moved on', (tester) async {
    await tester.pumpWidget(host(FakeAdapter((_) => envelope(candidate('offer'))), const CandidateDetailScreen(id: 'c1')));
    await tester.pumpAndSettle();
    expect(find.text('Hire'), findsOneWidget);
    expect(find.text('Next stage'), findsNothing);
    expect(find.text('Reject'), findsOneWidget);

  });

  testWidgets('a new candidate can be moved on but not hired', (tester) async {
    await tester.pumpWidget(host(FakeAdapter((_) => envelope(candidate('applied'))), const CandidateDetailScreen(id: 'c1')));
    await tester.pumpAndSettle();
    expect(find.text('Next stage'), findsOneWidget);
    expect(find.text('Hire'), findsNothing);
  });

  testWidgets('a hired candidate has no actions', (tester) async {
    await tester.pumpWidget(host(FakeAdapter((_) => envelope(candidate('hired'))), const CandidateDetailScreen(id: 'c1')));
    await tester.pumpAndSettle();
    expect(find.text('Hire'), findsNothing);
    expect(find.text('Reject'), findsNothing);
    expect(find.text('Next stage'), findsNothing);
  });

  testWidgets('open tasks list the employee and let HR mark a step done', (tester) async {
    final adapter = FakeAdapter((o) => o.method == 'GET'
        ? envelope([{'id': 't1', 'employeeId': 'e1', 'employeeName': 'Dana', 'title': 'Order laptop', 'dueDate': '2026-11-01', 'overdue': true}])
        : envelope({'id': 't1', 'isDone': true}));
    await tester.pumpWidget(host(adapter, const OpenTasksScreen()));
    await tester.pumpAndSettle();
    expect(find.text('Order laptop'), findsOneWidget);
    expect(find.textContaining('Dana'), findsOneWidget);
    await tester.tap(find.text('Done'));
    await tester.pumpAndSettle();
    expect(adapter.requests.any((r) => r.method == 'POST' && r.path.endsWith('/hr/tasks/t1/done')), isTrue);
  });
}

void shiftTests() {
  test('a shift needs a code, a name and a valid start time', () {
    final spec = shiftForm();
    final values = FormEngine.start(spec.fields, spec.initial);
    expect(FormEngine.validate(spec.fields, values)['code'], isNotNull);
    values
      ..['code'] = 'N1'
      ..['name'] = 'Night'
      ..['startTime'] = '25:00';
    expect(FormEngine.validate(spec.fields, values)['startTime'], isNotNull);
    values['startTime'] = '22:00';
    expect(FormEngine.validate(spec.fields, values), isEmpty);
  });

  testWidgets('the shifts list shows start time and head count', (tester) async {
    await tester.pumpWidget(host(
        FakeAdapter((_) => envelope([{'id': 's1', 'code': 'N1', 'name': 'Night', 'startTime': '22:00:00', 'graceMinutes': 5, 'hoursPerDay': 9, 'isActive': true, 'employees': 3}])),
        const ShiftsScreen()));
    await tester.pumpAndSettle();
    expect(find.text('Night'), findsOneWidget);
    expect(find.textContaining('from 22:00'), findsOneWidget);
    expect(find.textContaining('3 employee'), findsOneWidget);
  });
}

void rosterTests() {
  test('a roster entry needs an employee and dates, and the end cannot be before the start', () {
    final spec = rosterForm({'id': 's1', 'name': 'Night'});
    final values = FormEngine.start(spec.fields, spec.initial);
    expect(FormEngine.validate(spec.fields, values)['employeeId'], isNotNull);
    values
      ..['employeeId'] = 'e1'
      ..['fromDate'] = '2026-10-10'
      ..['toDate'] = '2026-10-01';
    expect(FormEngine.validate(spec.fields, values)['toDate'], isNotNull);
    values['toDate'] = '2026-10-12';
    expect(FormEngine.validate(spec.fields, values), isEmpty);
  });
}
