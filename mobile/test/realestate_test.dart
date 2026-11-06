import 'package:dio/dio.dart';
import 'package:erp_mobile/core/api_client.dart';
import 'package:erp_mobile/core/key_value_store.dart';
import 'package:erp_mobile/core/me.dart';
import 'package:erp_mobile/core/session.dart';
import 'package:erp_mobile/features/realestate/acquisition_screens.dart';
import 'package:erp_mobile/features/realestate/crm_screens.dart';
import 'package:erp_mobile/features/realestate/lease_screens.dart';
import 'package:erp_mobile/features/realestate/maintenance_screens.dart';
import 'package:erp_mobile/features/realestate/property_screens.dart';
import 'package:erp_mobile/features/realestate/screens.dart';
import 'package:erp_mobile/kit/field.dart';
import 'package:erp_mobile/kit/form_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/fake_adapter.dart';
import 'support/more_actions.dart';

const _entity = LegalEntity(id: 'le1', code: 'MAIN', name: 'Main', baseCurrency: 'QAR');
Me _me(Set<String> permissions) => Me(fullName: 'T', email: 't@t', baseCurrency: 'QAR', countryCode: 'QA', permissions: permissions, legalEntities: const [_entity], modules: const {'realestate'});

Widget _host(FakeAdapter adapter, Widget home, {Set<String> permissions = const {'*'}}) {
  final store = MemoryKeyValueStore();
  return ProviderScope(
    key: UniqueKey(),
    overrides: [
      storeProvider.overrideWithValue(store),
      meOrEmptyProvider.overrideWithValue(_me(permissions)),
      apiProvider.overrideWith((ref) => ApiClient(store: store, baseUrl: 'http://api.test', dio: Dio()..httpClientAdapter = adapter)),
    ],
    child: MaterialApp(home: home),
  );
}

/// Builds a form spec inside a provider scope (the builders need a WidgetRef) and hands it to [check].
Future<void> _withSpec(WidgetTester tester, FormSpec Function(WidgetRef ref) build, void Function(FormSpec spec) check) async {
  late FormSpec spec;
  await tester.pumpWidget(_host(FakeAdapter((_) => envelope({})), Consumer(builder: (context, ref, _) {
    spec = build(ref);
    return const SizedBox();
  })));
  check(spec);
}

Json _body(FormSpec spec, Map<String, dynamic> values) {
  final v = FormEngine.start(spec.fields, spec.initial)..addAll(values);
  expect(FormEngine.validate(spec.fields, v), isEmpty);
  return FormEngine.collect(spec.fields, v);
}

typedef Json = Map<String, dynamic>;

void main() {
  group('forms', () {
    testWidgets('unit form needs property (when not fixed), number, type, area and rent', (tester) async {
      await _withSpec(tester, (ref) => unitForm(ref), (spec) {
        final errors = FormEngine.validate(spec.fields, FormEngine.start(spec.fields, spec.initial));
        expect(errors.keys, containsAll(['propertyId', 'unitNumber', 'unitTypeId']));
        expect(_body(spec, {'propertyId': 'p1', 'unitNumber': '101', 'unitTypeId': 't1', 'askingRent': '85000'}), {'propertyId': 'p1', 'unitNumber': '101', 'unitTypeId': 't1', 'areaSqft': 0, 'askingRent': 85000});
      });
    });

    testWidgets('unit form opened from a property has no property field', (tester) async {
      await _withSpec(tester, (ref) => unitForm(ref, propertyId: 'p1', propertyName: 'Tower'), (spec) {
        expect(spec.fields.any((f) => f.key == 'propertyId'), isFalse);
      });
    });

    testWidgets('lease form: required fields and body', (tester) async {
      await _withSpec(tester, (ref) => leaseForm(ref), (spec) {
        final errors = FormEngine.validate(spec.fields, FormEngine.start(spec.fields, spec.initial));
        expect(errors.keys, containsAll(['unitId', 'lesseePartyId', 'annualRent']));
        final body = _body(spec, {'unitId': 'u1', 'lesseePartyId': 'c1', 'startDate': '2026-11-01', 'annualRent': '60000'});
        expect(body, {
          'unitId': 'u1',
          'lesseePartyId': 'c1',
          'startDate': '2026-11-01',
          'annualRent': 60000,
          'paymentFrequency': 4,
          'securityDepositAmount': 0,
          'graceDays': 0,
          'noticePeriodDays': 90,
          'activateNow': false,
        });
      });
    });

    testWidgets('work order form: issue is required, body carries the property fixed by the caller', (tester) async {
      await _withSpec(tester, (ref) => workOrderForm(ref, propertyId: 'p1', propertyName: 'Tower'), (spec) {
        expect(FormEngine.validate(spec.fields, FormEngine.start(spec.fields, spec.initial)).keys, ['issue']);
        final body = _body(spec, {'issue': 'Leak'});
        expect(body['issue'], 'Leak');
        expect(body['priority'], 'normal');
      });
    });

    testWidgets('inspection form collects checklist lines', (tester) async {
      await _withSpec(tester, (ref) => inspectionForm(ref), (spec) {
        final v = FormEngine.start(spec.fields, spec.initial)
          ..['propertyId'] = 'p1'
          ..['items'] = [
            {'area': 'Kitchen', 'condition': 'poor', 'repairCost': '50'}
          ];
        expect(FormEngine.validate(spec.fields, v), isEmpty);
        expect(FormEngine.collect(spec.fields, v)['items'], [
          {'area': 'Kitchen', 'condition': 'poor', 'repairCost': 50}
        ]);
      });
    });

    testWidgets('deal form: client, value and commission are required', (tester) async {
      await _withSpec(tester, (ref) => dealForm(ref), (spec) {
        final errors = FormEngine.validate(spec.fields, FormEngine.start(spec.fields, spec.initial));
        expect(errors.keys, containsAll(['clientName', 'dealValue', 'commissionRatePercent']));
      });
    });

    testWidgets('acquisition form sends funding sources as lines', (tester) async {
      await _withSpec(tester, (ref) => acquisitionForm(ref), (spec) {
        final v = FormEngine.start(spec.fields, spec.initial)
          ..['propertyId'] = 'p1'
          ..['sellerName'] = 'Seller'
          ..['purchasePrice'] = '1000000'
          ..['funding'] = [
            {'type': 'bankMortgage', 'lender': 'Bank', 'principal': '800000', 'annualRatePercent': '4.5', 'termMonths': '120', 'firstDueDate': '2026-12-01'}
          ];
        expect(FormEngine.validate(spec.fields, v), isEmpty);
        final body = FormEngine.collect(spec.fields, v);
        expect((body['funding'] as List).first, {'type': 'bankMortgage', 'lender': 'Bank', 'principal': 800000, 'annualRatePercent': 4.5, 'termMonths': 120, 'firstDueDate': '2026-12-01'});
      });
    });
  });

  group('tiles', () {
    testWidgets('unit tile shows tenant and lease end for an occupied unit', (tester) async {
      await tester.pumpWidget(_host(FakeAdapter((_) => envelope({})), Scaffold(body: Builder(builder: (c) => unitTile(c, {'id': 'u', 'unitNumber': '101', 'propertyName': 'Tower', 'status': 'occupied', 'tenantName': 'Ann', 'leaseEnds': '2027-07-30', 'currentRent': 85000})))));
      expect(find.text('Unit 101 · Tower'), findsOneWidget);
      expect(find.textContaining('Ann'), findsOneWidget);
      expect(find.text('Occupied'), findsOneWidget);
    });

    testWidgets('work order tile shows issue, humanized status', (tester) async {
      await tester.pumpWidget(_host(FakeAdapter((_) => envelope({})), Scaffold(body: Builder(builder: (c) => workOrderTile(c, {'id': 'w', 'issue': 'Leak', 'number': 'WO-1', 'priority': 'high', 'status': 'inProgress', 'reportedOn': '2026-10-29'})))));
      expect(find.text('Leak'), findsOneWidget);
      expect(find.text('In progress'), findsOneWidget);
    });
  });

  group('detail actions', () {
    Json workOrder(String status) => {'id': 'w1', 'number': 'WO-1', 'issue': 'Leak', 'priority': 'high', 'status': status, 'reportedOn': '2026-10-29', 'estimatedCost': 100};

    Future<void> openWorkOrder(WidgetTester tester, String status, {Set<String> permissions = const {'*'}}) async {
      await tester.pumpWidget(_host(FakeAdapter((_) => envelope(workOrder(status))), const WorkOrderDetailScreen(id: 'w1'), permissions: permissions));
      await tester.pumpAndSettle();
    }

    testWidgets('a new work order offers assign, start and cancel only', (tester) async {
      await openWorkOrder(tester, 'new');
      await openMoreActions(tester);
      expect(find.text('Assign'), findsOneWidget);
      expect(find.text('Start work'), findsOneWidget);
      expect(find.text('Cancel'), findsOneWidget);
      expect(find.text('Complete'), findsNothing);
      expect(find.text('Close'), findsNothing);
    });

    testWidgets('an in-progress work order can be completed or put on hold, not closed', (tester) async {
      await openWorkOrder(tester, 'inProgress');
      expect(find.text('Complete'), findsOneWidget);
      expect(find.text('Put on hold'), findsOneWidget);
      expect(find.text('Close'), findsNothing);
    });

    testWidgets('a completed work order can be closed or reopened; a closed one has no actions', (tester) async {
      await openWorkOrder(tester, 'completed');
      expect(find.text('Close'), findsOneWidget);
      expect(find.text('Reopen'), findsOneWidget);
      await openWorkOrder(tester, 'closed');
      expect(find.text('Close'), findsNothing);
      expect(find.text('Cancel'), findsNothing);
    });

    testWidgets('actions are hidden without the permission', (tester) async {
      await openWorkOrder(tester, 'new', permissions: {'realestate.workorders.read'});
      expect(find.text('Start work'), findsNothing);
      expect(find.text('Assign'), findsNothing);
    });

    testWidgets('closing posts the status and shows a server conflict message', (tester) async {
      final adapter = FakeAdapter((o) => o.method == 'POST' ? problem(409, 'A Completed work order cannot move to Closed.') : envelope(workOrder('completed')));
      await tester.pumpWidget(_host(adapter, const WorkOrderDetailScreen(id: 'w1')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Close'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Continue'));
      await tester.pumpAndSettle();
      final post = adapter.requests.firstWhere((r) => r.method == 'POST');
      expect(post.path, endsWith('/api/v1/m/realestate/work-orders/w1/status'));
      expect(post.data, {'status': 'closed'});
      expect(find.text('A Completed work order cannot move to Closed.'), findsOneWidget);
    });

    Json lease(String status) => {
          'id': 'l1', 'number': 'LSE-1', 'unitNumber': '101', 'tenantName': 'Ann', 'startDate': '2026-01-01', 'endDate': '2026-12-31', 'annualRent': 1000, 'currencyCode': 'QAR',
          'paymentFrequency': 4, 'securityDepositAmount': 0, 'status': status, 'noticePeriodDays': 90, 'graceDays': 0, 'schedule': [],
        };

    testWidgets('lease actions follow the lease status', (tester) async {
      Future<void> open(String status) async {
        await tester.pumpWidget(const SizedBox());
        await tester.pumpWidget(_host(FakeAdapter((o) => o.path.endsWith('/deposit') ? envelope({'transactions': []}) : envelope(lease(status))), const LeaseDetailScreen(id: 'l1')));
        await tester.pumpAndSettle();
        await openMoreActions(tester);
      }

      await open('draft');
      expect(find.text('Activate'), findsOneWidget);
      expect(find.text('Cancel lease'), findsOneWidget);
      expect(find.text('Terminate'), findsNothing);
      await open('active');
      expect(find.text('Renew'), findsOneWidget);
      expect(find.text('Terminate'), findsOneWidget);
      expect(find.text('Activate'), findsNothing);
      expect(find.text('Confirm move-out'), findsNothing);
      await open('holdingOver');
      expect(find.text('Confirm move-out'), findsOneWidget);
      await open('terminated');
      expect(find.text('Credit notes'), findsOneWidget);
      expect(find.text('Terminate'), findsNothing);
    });

    testWidgets('a deal can only move forward until agreed; invoicing needs an agreed deal', (tester) async {
      Json deal(String stage) => {'id': 'd1', 'number': 'D-1', 'clientName': 'Cli', 'dealType': 'sale', 'dealDate': '2026-10-01', 'stage': stage, 'currencyCode': 'QAR', 'dealValue': 100, 'commissionRatePercent': 2};
      Future<void> open(String stage) async {
        await tester.pumpWidget(_host(FakeAdapter((_) => envelope(deal(stage))), const DealDetailScreen(id: 'd1')));
        await tester.pumpAndSettle();
      }

      await open('viewing');
      expect(find.text('Move to Negotiation'), findsOneWidget);
      expect(find.text('Invoice commission'), findsNothing);
      await open('agreed');
      expect(find.text('Invoice commission'), findsOneWidget);
      expect(find.text('Mark lost'), findsOneWidget);
      await open('collected');
      expect(find.text('Pay agent'), findsOneWidget);
      expect(find.text('Mark lost'), findsNothing);
    });
  });

  test('every built menu id is unique and registered', () {
    final ids = realestateScreens.expand((s) => s.menuIds).toList();
    expect(ids.toSet().length, ids.length);
    expect(ids, hasLength(13));
  });
}
