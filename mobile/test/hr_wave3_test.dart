import 'package:dio/dio.dart';
import 'package:erp_mobile/core/api_client.dart';
import 'package:erp_mobile/core/key_value_store.dart';
import 'package:erp_mobile/core/me.dart';
import 'package:erp_mobile/core/session.dart';
import 'package:erp_mobile/features/hr/people_screens.dart';
import 'package:erp_mobile/features/hr/self_service_screens.dart';
import 'package:erp_mobile/features/hr/setup_screens.dart';
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

void main() {
  test('leave type tiers and pay bands go to the server as lists, and a blank form clears them', () {
    expect(pairsOf('5:30, 10:35'), [[5, 30], [10, 35]]);
    expect(pairsOf(''), isEmpty);
    expect(pairsOf('nonsense, 3:x'), isEmpty);
    final tiers = [{'fromServiceYears': 0, 'days': 21}, {'fromServiceYears': 5, 'days': 30}];
    final bands = [{'fromDay': 1, 'payPercent': 100}, {'fromDay': 16, 'payPercent': 50}];
    final body = leaveTypeBody({'code': 'SICK', 'tiers': tiers, 'payBands': bands});
    expect(body['tiers'], tiers);
    expect(body['payBands'], bands);
    expect(leaveTypeBody({'code': 'X'})['tiers'], isEmpty); // nothing entered clears them
  });

  test('document expiry counts days from today and colours the chip', () {
    final now = DateTime(2026, 11, 4);
    expect(daysUntil('2026-11-04', now: now), 0);
    expect(daysUntil('2026-11-14', now: now), 10);
    expect(daysUntil('2026-11-01', now: now), -3);
    expect(daysUntil(null, now: now), isNull);
    expect(documentTypeLabel('residenceId'), 'Residence ID');
  });

  test('a loan needs the employee, amount and recovery month, and builds the body', () {
    final spec = loanForm();
    final values = FormEngine.start(spec.fields, spec.initial);
    final errors = FormEngine.validate(spec.fields, values);
    expect(errors['employeeId'], isNotNull);
    expect(errors['principal'], isNotNull);
    values
      ..['employeeId'] = 'e1'
      ..['principal'] = '3000'
      ..['instalments'] = '3';
    expect(FormEngine.validate(spec.fields, values), isEmpty);
    final body = FormEngine.collect(spec.fields, values);
    expect(body['employeeId'], 'e1');
    expect(body['kind'], 'loan');
    expect(body['principal'], 3000);
    expect(body['instalments'], 3);
  });

  test('my leave form offers the types from my balances, with the days left', () {
    final spec = myLeaveForm([
      {'leaveTypeId': 't1', 'leaveTypeName': 'Annual leave', 'remaining': 12},
      {'leaveTypeId': 't2', 'leaveTypeName': 'Sick leave', 'remaining': 4.5},
    ]);
    final type = spec.fields.first;
    expect([for (final o in type.options) o.label], ['Annual leave (12 left)', 'Sick leave (4.5 left)']);
    final values = FormEngine.start(spec.fields, spec.initial);
    expect(FormEngine.validate(spec.fields, values)['leaveTypeId'], isNotNull);
  });

  testWidgets('My HR shows a message when the sign-in is not linked to an employee', (tester) async {
    final adapter = FakeAdapter((o) => o.path.endsWith('/hr/me') ? problem(404, 'no employee') : envelope([]));
    await tester.pumpWidget(host(adapter, const MyHrScreen()));
    await tester.pumpAndSettle();
    expect(find.textContaining('not linked to an employee record'), findsOneWidget);
  });

  testWidgets('My HR shows balances, and a team tab only for line managers', (tester) async {
    final adapter = FakeAdapter((o) {
      if (o.path.endsWith('/hr/me')) return envelope({'id': 'e1', 'fullName': 'Sara Ali', 'number': 'EMP-001', 'designation': 'Analyst', 'managerName': 'Omar'});
      if (o.path.endsWith('/hr/me/balances')) return envelope([{'leaveTypeId': 't1', 'leaveTypeName': 'Annual leave', 'entitled': 30, 'carried': 0, 'taken': 5, 'pending': 0, 'remaining': 25}]);
      if (o.path.endsWith('/hr/me/team/leave')) return envelope({'items': [{'id': 'l9', 'employeeName': 'Dana', 'fromDate': '2026-11-10', 'toDate': '2026-11-11', 'days': 2, 'status': 'submitted'}], 'total': 1, 'page': 1, 'pageSize': 50});
      return envelope({'items': [], 'total': 0, 'page': 1, 'pageSize': 50});
    });
    await tester.pumpWidget(host(adapter, const MyHrScreen()));
    await tester.pumpAndSettle();
    expect(find.text('Sara Ali'), findsOneWidget);
    expect(find.text('Annual leave'), findsOneWidget);
    expect(find.text('Remaining'), findsOneWidget);
    expect(find.text('Team (1)'), findsOneWidget);
    await tester.tap(find.text('Team (1)'));
    await tester.pumpAndSettle();
    expect(find.text('Dana'), findsOneWidget);
    await tester.tap(find.byTooltip('Approve'));
    await tester.pumpAndSettle();
    expect(adapter.requests.any((r) => r.method == 'POST' && r.path.endsWith('/hr/leave/l9/approve')), isTrue);
  });
}
