import 'package:dio/dio.dart';
import 'package:erp_mobile/core/api_client.dart';
import 'package:erp_mobile/core/key_value_store.dart';
import 'package:erp_mobile/core/me.dart';
import 'package:erp_mobile/core/session.dart';
import 'package:erp_mobile/features/hr/attendance_screens.dart';
import 'package:erp_mobile/features/hr/leave_screens.dart';
import 'package:erp_mobile/kit/field.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/fake_adapter.dart';
import 'support/more_actions.dart';

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

Map<String, dynamic> leave(String status) => {
      'id': 'l1',
      'employeeId': 'e1',
      'employeeName': 'Sara Ali',
      'leaveTypeId': 't1',
      'leaveTypeName': 'Annual leave',
      'isPaid': true,
      'fromDate': '2026-11-02',
      'toDate': '2026-11-04',
      'days': 3,
      'status': status,
      'reason': 'Trip',
    };

void main() {
  testWidgets('leave form needs employee and type, and builds the request body', (tester) async {
    Map<String, dynamic>? body;
    late WidgetRef captured;
    await tester.pumpWidget(host(FakeAdapter((_) => envelope({})), Consumer(builder: (c, ref, _) {
      captured = ref;
      return const SizedBox();
    })));
    final spec = leaveForm(captured);
    final values = FormEngine.start(spec.fields, spec.initial);
    final errors = FormEngine.validate(spec.fields, values);
    expect(errors['employeeId'], 'Employee is required');
    expect(errors['leaveTypeId'], 'Leave type is required');
    values
      ..['employeeId'] = 'e1'
      ..['leaveTypeId'] = 't1'
      ..['fromDate'] = '2026-11-02'
      ..['toDate'] = '2026-11-01';
    expect(FormEngine.validate(spec.fields, values)['toDate'], isNotNull);
    values['toDate'] = '2026-11-04';
    body = FormEngine.collect(spec.fields, values);
    expect(body, {'employeeId': 'e1', 'leaveTypeId': 't1', 'fromDate': '2026-11-02', 'toDate': '2026-11-04', 'submit': true, 'startHalfDay': false, 'endHalfDay': false});
  });

  testWidgets('leave tile shows the employee, days and a readable status', (tester) async {
    await tester.pumpWidget(host(FakeAdapter((_) => envelope([])), Scaffold(body: Builder(builder: (c) => leaveTile(c, {...leave('submitted'), 'leaveTypeId': 't1'})))));
    await tester.pumpAndSettle();
    expect(find.text('Sara Ali'), findsOneWidget);
    expect(find.text('3 d'), findsOneWidget);
    expect(find.text('Waiting for approval'), findsOneWidget);
  });

  testWidgets('a submitted request offers approve and reject, not submit', (tester) async {
    await tester.pumpWidget(host(FakeAdapter((_) => envelope(leave('submitted'))), const LeaveDetailScreen(id: 'l1')));
    await tester.pumpAndSettle();
    await openMoreActions(tester);
    expect(find.text('Approve'), findsOneWidget);
    expect(find.text('Reject'), findsOneWidget);
    expect(find.text('Cancel request'), findsOneWidget);
    expect(find.text('Submit'), findsNothing);
  });

  testWidgets('a draft request offers submit and edit', (tester) async {
    await tester.pumpWidget(host(FakeAdapter((_) => envelope(leave('draft'))), const LeaveDetailScreen(id: 'l1')));
    await tester.pumpAndSettle();
    expect(find.text('Submit'), findsOneWidget);
    expect(find.text('Approve'), findsNothing);
    expect(find.byTooltip('Edit'), findsOneWidget);
  });

  testWidgets('an approved request offers only cancel', (tester) async {
    await tester.pumpWidget(host(FakeAdapter((_) => envelope(leave('approved'))), const LeaveDetailScreen(id: 'l2')));
    await tester.pumpAndSettle();
    expect(find.text('Cancel request'), findsOneWidget);
    expect(find.text('Approve'), findsNothing);
    expect(find.byTooltip('Edit'), findsNothing);
  });

  testWidgets('approving someone without the permission is not offered', (tester) async {
    await tester.pumpWidget(host(FakeAdapter((_) => envelope(leave('submitted'))), const LeaveDetailScreen(id: 'l1'), permissions: {'hr.leave.request'}));
    await tester.pumpAndSettle();
    expect(find.text('Approve'), findsNothing);
    expect(find.text('Cancel request'), findsOneWidget);
  });

  test('attendance form produces integer days and the fixed month', () {
    final spec = attendanceForm({'employeeId': 'e1', 'employeeName': 'Sara', 'workingDays': 26, 'daysPresent': 26, 'unpaidAbsentDays': 0, 'paidLeaveDays': 0, 'overtimeHours': 0}, 2026, 10);
    final values = FormEngine.start(spec.fields, spec.initial)
      ..['daysPresent'] = '30';
    expect(FormEngine.validate(spec.fields, values)['daysPresent'], 'Days present cannot exceed the working days');
    values
      ..['daysPresent'] = '24'
      ..['unpaidAbsentDays'] = '2'
      ..['overtimeHours'] = '3.5';
    expect(FormEngine.collect(spec.fields, values), {'workingDays': 26, 'daysPresent': 24, 'unpaidAbsentDays': 2, 'paidLeaveDays': 0, 'overtimeHours': 3.5});
  });
}
