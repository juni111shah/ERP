import 'package:dio/dio.dart';
import 'package:erp_mobile/core/api_client.dart';
import 'package:erp_mobile/core/key_value_store.dart';
import 'package:erp_mobile/core/me.dart';
import 'package:erp_mobile/core/session.dart';
import 'package:erp_mobile/features/hr/employee_screens.dart';
import 'package:erp_mobile/features/hr/payroll_screens.dart';
import 'package:erp_mobile/features/hr/review_screens.dart';
import 'package:erp_mobile/kit/field.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/fake_adapter.dart';

const entity = LegalEntity(id: 'le1', code: 'MAIN', name: 'Main', baseCurrency: 'QAR');

Widget host(FakeAdapter adapter, Widget child) {
  final store = MemoryKeyValueStore();
  return ProviderScope(
    overrides: [
      storeProvider.overrideWithValue(store),
      apiProvider.overrideWith((ref) => ApiClient(store: store, baseUrl: 'http://api.test', dio: Dio()..httpClientAdapter = adapter)),
      meProvider.overrideWith((ref) async => const Me(fullName: 'Dana', email: 'd@x', baseCurrency: 'QAR', countryCode: 'QA', permissions: {'*'}, legalEntities: [entity], modules: {'hr'})),
    ],
    child: MaterialApp(home: child),
  );
}

Future<WidgetRef> grabRef(WidgetTester tester) async {
  late WidgetRef ref;
  await tester.pumpWidget(host(FakeAdapter((_) => envelope({})), Consumer(builder: (c, r, _) {
    ref = r;
    r.watch(meOrEmptyProvider);
    return const SizedBox();
  })));
  await tester.pumpAndSettle();
  return ref;
}

Map<String, dynamic> run(String status) => {
      'id': 'p1',
      'number': 'PR-1',
      'year': 2026,
      'month': 10,
      'status': status,
      'employees': 1,
      'grossEarnings': 5000,
      'deductions': 0,
      'netPay': 5000,
      'lines': [
        {'id': 'l1', 'employeeName': 'Sara', 'employeeNumber': 'E1', 'basic': 5000, 'allowances': 0, 'overtime': 0, 'absence': 0, 'adjustment': 0, 'deductions': 0, 'netPay': 5000, 'absentDays': 0, 'overtimeHours': 0}
      ],
    };

void main() {
  testWidgets('employee form needs name, title and a salary above zero, and sends the entity', (tester) async {
    final ref = await grabRef(tester);
    final spec = employeeForm(ref);
    final values = FormEngine.start(spec.fields, spec.initial);
    final errors = FormEngine.validate(spec.fields, values);
    expect(errors['fullName'], 'Full name is required');
    expect(errors['designation'], 'Job title is required');
    expect(errors['basicSalary'], 'Basic salary is required');
    values
      ..['fullName'] = 'Sara Ali'
      ..['designation'] = 'Analyst'
      ..['basicSalary'] = '0';
    expect(FormEngine.validate(spec.fields, values)['basicSalary'], isNotNull);
    values['basicSalary'] = '5000';
    expect(FormEngine.validate(spec.fields, values), isEmpty);
    final body = FormEngine.collect(spec.fields, values);
    expect(body['legalEntityId'], 'le1');
    expect(body['status'], 'active');
    expect(body['basicSalary'], 5000);
    expect(body.containsKey('terminationDate'), isFalse);
  });

  testWidgets('a terminated employee needs a termination date', (tester) async {
    final ref = await grabRef(tester);
    final spec = employeeForm(ref);
    final values = FormEngine.start(spec.fields, spec.initial)
      ..['fullName'] = 'A'
      ..['designation'] = 'B'
      ..['basicSalary'] = '1'
      ..['status'] = 'terminated';
    expect(FormEngine.validate(spec.fields, values)['terminationDate'], 'Enter the termination date');
  });

  testWidgets('employee tile shows number, title and status', (tester) async {
    await tester.pumpWidget(host(FakeAdapter((_) => envelope({})), Scaffold(body: Builder(builder: (c) => employeeTile(c, {'id': 'e1', 'fullName': 'Sara Ali', 'number': 'EMP-9', 'designation': 'Analyst', 'status': 'onLeave'})))));
    await tester.pumpAndSettle();
    expect(find.text('Sara Ali'), findsOneWidget);
    expect(find.textContaining('EMP-9 · Analyst'), findsOneWidget);
    expect(find.text('On leave'), findsOneWidget);
  });

  testWidgets('a draft payroll can be recalculated, posted and deleted, not paid', (tester) async {
    await tester.pumpWidget(host(FakeAdapter((_) => envelope(run('draft'))), const PayrollDetailScreen(id: 'p1')));
    await tester.pumpAndSettle();
    for (final t in ['Recalculate', 'Record', 'Delete']) {
      expect(find.text(t), findsOneWidget, reason: t);
    }
    expect(find.text('Pay'), findsNothing);
    expect(find.text('Sara'), findsOneWidget);
    expect(find.text('Adjust'), findsNothing); // inside the collapsed payslip
  });

  testWidgets('a posted payroll can only be paid', (tester) async {
    await tester.pumpWidget(host(FakeAdapter((_) => envelope(run('posted'))), const PayrollDetailScreen(id: 'p1')));
    await tester.pumpAndSettle();
    expect(find.text('Pay'), findsOneWidget);
    expect(find.text('Record'), findsNothing);
    expect(find.text('Delete'), findsNothing);
  });

  testWidgets('a paid payroll can only be emailed or voided', (tester) async {
    await tester.pumpWidget(host(FakeAdapter((_) => envelope(run('paid'))), const PayrollDetailScreen(id: 'p1')));
    await tester.pumpAndSettle();
    expect(find.text('Email payslips'), findsOneWidget);
    expect(find.text('Void'), findsOneWidget);
    expect(find.text('Pay'), findsNothing);
    expect(find.text('Record'), findsNothing);
    expect(find.text('Delete'), findsNothing);
  });

  testWidgets('payroll and pay forms validate and shape the body', (tester) async {
    final ref = await grabRef(tester);
    final spec = payrollForm(ref);
    final values = FormEngine.start(spec.fields, spec.initial)..['month'] = '13';
    expect(FormEngine.validate(spec.fields, values)['month'], isNotNull);
    values['month'] = '10';
    expect(FormEngine.validate(spec.fields, values), isEmpty);
    expect(FormEngine.collect(spec.fields, values)['legalEntityId'], 'le1');

    final pay = payForm(run('posted'));
    final pv = FormEngine.start(pay.fields, pay.initial);
    expect(FormEngine.validate(pay.fields, pv)['bankAccountId'], 'Bank account is required');
  });

  testWidgets('review actions follow the status', (tester) async {
    Map<String, dynamic> review(String s) => {'id': 'v1', 'employeeName': 'Sara', 'cycleName': 'H2', 'status': s, 'competencies': []};
    await tester.pumpWidget(host(FakeAdapter((_) => envelope(review('draft'))), const ReviewDetailScreen(id: 'v1')));
    await tester.pumpAndSettle();
    expect(find.text('Submit'), findsOneWidget);
    expect(find.text('Acknowledge'), findsNothing);
    expect(find.byTooltip('Edit'), findsOneWidget);
  });

  testWidgets('a submitted review can be acknowledged but not edited', (tester) async {
    await tester.pumpWidget(host(FakeAdapter((_) => envelope({'id': 'v1', 'employeeName': 'Sara', 'cycleName': 'H2', 'status': 'submitted', 'competencies': []})), const ReviewDetailScreen(id: 'v1')));
    await tester.pumpAndSettle();
    expect(find.text('Acknowledge'), findsOneWidget);
    expect(find.text('Submit'), findsNothing);
    expect(find.byTooltip('Edit'), findsNothing);
  });

  testWidgets('review form needs an employee and a cycle; competencies are optional', (tester) async {
    final ref = await grabRef(tester);
    final spec = reviewForm(ref);
    final values = FormEngine.start(spec.fields, spec.initial);
    final errors = FormEngine.validate(spec.fields, values);
    expect(errors['employeeId'], 'Employee is required');
    expect(errors['cycleId'], 'Review cycle is required');
    values
      ..['employeeId'] = 'e1'
      ..['cycleId'] = 'c1'
      ..['overallRating'] = '6';
    expect(FormEngine.validate(spec.fields, values)['overallRating'], isNotNull);
    values['overallRating'] = '4';
    expect(FormEngine.validate(spec.fields, values), isEmpty);
    expect(FormEngine.collect(spec.fields, values)['reviewerName'], 'Dana');
  });
}
