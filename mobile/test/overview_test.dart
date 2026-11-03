import 'dart:async';

import 'package:erp_mobile/core/me.dart';
import 'package:erp_mobile/core/models.dart';
import 'package:erp_mobile/features/dashboard/dashboard_page.dart';
import 'package:erp_mobile/features/dashboard/overview_logic.dart';
import 'package:erp_mobile/kit/stat_card.dart';
import 'package:erp_mobile/kit/stat_carousel.dart';
import 'package:erp_mobile/theme/app_theme.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

Map<String, dynamic> _kpi(String key, String label, double value, String? link, {String format = 'money'}) =>
    {'key': key, 'label': label, 'value': value, 'format': format, 'caption': 'cap $key', 'link': link};

/// 10 KPIs: 6 finance, 3 real estate, 1 without a link.
final _kpis = [
  _kpi('receivables', 'Receivables', 712000, '/finance/invoices'),
  _kpi('collected', 'Collected', 4110000, '/finance/receipts'),
  _kpi('cash', 'Cash and bank', 1360000, '/finance/bank'),
  _kpi('payables', 'Payables', 98000, '/finance/bills'),
  _kpi('pdcs', 'Cheques in custody', 45500, '/finance/cheques'),
  _kpi('net', 'Net result', 820, '/finance/reports'),
  _kpi('units', 'Units', 120, '/real-estate/units', format: 'number'),
  _kpi('occupancy', 'Occupancy', 91.5, '/real-estate/units', format: 'percent'),
  _kpi('leases', 'Active leases', 98, '/real-estate/leases', format: 'number'),
  _kpi('misc', 'Approvals waiting', 3, null, format: 'number'),
];

Dashboard _dashboard({List<Map<String, dynamic>>? kpis, int alerts = 2}) => Dashboard.fromJson({
      'asOf': '2026-11-03',
      'kpis': kpis ?? _kpis,
      'charts': [
        {
          'key': 'billed-collected',
          'title': 'Billed vs collected',
          'categories': ['Sep 26', 'Oct 26', 'Nov 26'],
          'series': [
            {'name': 'Billed', 'values': [100, 200, 300]},
            {'name': 'Collected', 'values': [90, 150, 280]},
          ],
        },
        {
          'key': 'status',
          'title': 'Work orders by status',
          'categories': ['Open', 'In progress', 'Waiting for parts', 'Completed', 'Cancelled', 'On hold'],
          'series': [
            {'name': 'Count', 'values': [3, 4, 1, 9, 0, 2]},
          ],
        },
      ],
      'alerts': [
        for (var i = 0; i < alerts; i++)
          {'severity': i == 0 ? 'critical' : 'warning', 'category': 'Cheques', 'message': 'Alert $i', 'link': i == 0 ? '/finance/cheques' : null},
      ],
    });

Future<void> _pump(WidgetTester t, Dashboard d, {TextDirection dir = TextDirection.ltr, ThemeData? theme}) async {
  t.view.physicalSize = const Size(1080, 2400);
  t.view.devicePixelRatio = 2.6;
  addTearDown(t.view.reset);
  await t.pumpWidget(ProviderScope(
    overrides: [
      dashboardProvider.overrideWith((ref) async => d),
      menuProvider.overrideWith((ref) async => const <MenuGroup>[]),
    ],
    child: MaterialApp(
      theme: theme ?? AppTheme.light(),
      home: Directionality(textDirection: dir, child: const Scaffold(body: DashboardPage())),
    ),
  ));
  await t.pumpAndSettle();
}

/// Cards on the visible carousel page (the invisible sizer is excluded); a single page has no PageView.
Finder _visibleCards() => find.byType(PageView).evaluate().isNotEmpty
    ? find.descendant(of: find.byType(PageView), matching: find.byType(ErpStatCard))
    : find.descendant(of: find.byType(ErpStatCarousel), matching: find.byType(ErpStatCard));

List<String> _visibleLabels(WidgetTester t) => [for (final c in t.widgetList<ErpStatCard>(_visibleCards())) c.label];

double _dotWidth(WidgetTester t, int i) => t.getSize(find.byKey(ValueKey('erp-stat-carousel-dot-$i'))).width;

IconButton _button(WidgetTester t, String which) => t.widget<IconButton>(find.byKey(ValueKey('erp-stat-carousel-$which')));

void main() {
  group('overview logic', () {
    test('category comes from the first link segment, or general without a link', () {
      final d = _dashboard();
      expect(kpiCategory(d.kpis[0]), 'finance');
      expect(kpiCategory(d.kpis[6]), 'real-estate');
      expect(kpiCategory(d.kpis[9]), kOverviewGeneralCategory);
      expect(kpiCategories(d.kpis), [('finance', 6), ('real-estate', 3), (kOverviewGeneralCategory, 1)]);
      expect(categoryLabel('real-estate'), 'Real estate');
      expect(categoryLabel('hr'), 'HR');
    });

    test('stripe is stable per KPI key', () {
      expect(stripeForKey('cash'), same(stripeForKey('cash')));
      final stripes = {for (final k in ['receivables', 'collected', 'cash', 'payables', 'pdcs', 'net']) stripeForKey(k)};
      expect(stripes.length, greaterThan(1));
    });

    test('compact values keep percentages and small figures in full', () {
      expect(compactKpi(4820000, 'money', '4,820,000.00'), '4.82M');
      expect(compactKpi(712000, 'money', '712,000.00'), '712K');
      expect(compactKpi(91.5, 'percent', '91.5%'), '91.5%');
      expect(compactKpi(820, 'money', '820.00'), '820.00');
    });

    test('axis labels abbreviate instead of hard-cutting', () {
      expect(axisLabel('Nov 26'), 'Nov');
      expect(axisLabel('Oct–Dec 26'), 'Oct–Dec');
      expect(axisLabel('03 Nov'), '03 Nov');
      expect(axisLabel('Open'), 'Open');
      expect(axisLabel('Waiting for parts', max: 10), 'Waiting f…');
    });

    test('links map to in-app screens when the app has one', () {
      expect(screenForLink('/finance/invoices')?.menuIds, ['finance.invoices']);
      expect(screenForLink('/real-estate/work-orders')?.menuIds, ['realestate.workorders']);
      expect(screenForLink('/finance/ar-ageing')?.menuIds, ['finance.ar-ageing']);
      expect(screenForLink('/approvals')?.menuIds, ['core.approvals']);
      expect(screenForLink('/finance/reports')?.menuIds, ['core.reports']);
      expect(screenForLink('/nowhere/at-all'), isNull);
      expect(screenForLink(null), isNull);
      final menu = [MenuGroup(id: 'g', label: null, items: const [MenuItem(id: 'finance.receipts', label: 'R', route: '/money-in', module: 'core')])];
      expect(screenForLink('/money-in', menu: menu)?.menuIds, ['finance.receipts']);
    });
  });

  group('overview page', () {
    testWidgets('shows 4 cards by default with dots and arrows; prev disabled on page 1', (t) async {
      await _pump(t, _dashboard());
      expect(_visibleCards(), findsNWidgets(4));
      expect(_visibleLabels(t), ['Receivables', 'Collected', 'Cash and bank', 'Payables']);
      expect(find.byKey(const ValueKey('erp-stat-carousel-dot-2')), findsOneWidget);
      expect(find.byKey(const ValueKey('erp-stat-carousel-dot-3')), findsNothing);
      expect(_dotWidth(t, 0), greaterThan(_dotWidth(t, 1)));
      expect(_button(t, 'prev').onPressed, isNull);
      expect(_button(t, 'next').onPressed, isNotNull);
      expect(find.byType(Scrollbar), findsNothing);
      // Compact value, full value in the tooltip.
      expect(t.widget<ErpStatCard>(_visibleCards().at(0)).value, '712K');
      expect(find.byTooltip('Receivables: 712,000.00'), findsWidgets);
    });

    testWidgets('arrows page through all metrics and update the dots', (t) async {
      await _pump(t, _dashboard());
      await t.tap(find.byKey(const ValueKey('erp-stat-carousel-next')));
      await t.pumpAndSettle();
      expect(_visibleLabels(t), ['Cheques in custody', 'Net result', 'Units', 'Occupancy']);
      expect(_dotWidth(t, 1), greaterThan(_dotWidth(t, 0)));
      await t.tap(find.byKey(const ValueKey('erp-stat-carousel-next')));
      await t.pumpAndSettle();
      expect(_visibleLabels(t), ['Active leases', 'Approvals waiting']);
      expect(_button(t, 'next').onPressed, isNull);
      await t.tap(find.byKey(const ValueKey('erp-stat-carousel-prev')));
      await t.pumpAndSettle();
      expect(_visibleLabels(t).first, 'Cheques in custody');
    });

    testWidgets('swiping reveals the next page', (t) async {
      await _pump(t, _dashboard());
      await t.fling(find.byType(PageView), const Offset(-300, 0), 1000);
      await t.pumpAndSettle();
      expect(_visibleLabels(t).first, 'Cheques in custody');
      expect(_dotWidth(t, 1), greaterThan(_dotWidth(t, 0)));
    });

    testWidgets('chips filter the carousel and reset it to page 1', (t) async {
      await _pump(t, _dashboard());
      expect(find.text('All'), findsOneWidget);
      expect(find.text('Finance'), findsOneWidget);
      expect(find.text('Real estate'), findsOneWidget);
      await t.tap(find.byKey(const ValueKey('erp-stat-carousel-next')));
      await t.pumpAndSettle();

      await t.tap(find.text('Real estate'));
      await t.pumpAndSettle();
      expect(_visibleLabels(t), ['Units', 'Occupancy', 'Active leases']);
      expect(find.byKey(const ValueKey('erp-stat-carousel-next')), findsNothing, reason: 'one page: no controls');

      await t.tap(find.text('Finance'));
      await t.pumpAndSettle();
      expect(_visibleLabels(t), ['Receivables', 'Collected', 'Cash and bank', 'Payables']);
      await t.tap(find.byKey(const ValueKey('erp-stat-carousel-next')));
      await t.pumpAndSettle();
      expect(_visibleLabels(t), ['Cheques in custody', 'Net result']);

      await t.tap(find.text('All'));
      await t.pumpAndSettle();
      expect(_visibleLabels(t).first, 'Receivables', reason: 'changing the chip goes back to page 1');
      expect(_dotWidth(t, 0), greaterThan(_dotWidth(t, 1)));
    });

    testWidgets('charts sit directly below the carousel, on screen without scrolling', (t) async {
      await _pump(t, _dashboard());
      final screenH = t.view.physicalSize.height / t.view.devicePixelRatio;
      final carouselBottom = t.getBottomLeft(find.byType(ErpStatCarousel)).dy;
      final chartTop = t.getTopLeft(find.text('Billed vs collected')).dy;
      expect(chartTop, greaterThan(carouselBottom));
      expect(chartTop, lessThan(screenH));
      expect(find.text('HIGHLIGHTS'), findsOneWidget);
    });

    testWidgets('fewer than 4 cards: one page, no arrows or dots', (t) async {
      await _pump(t, _dashboard(kpis: _kpis.take(3).toList()));
      expect(find.byType(ErpStatCard), findsNWidgets(3));
      expect(find.byKey(const ValueKey('erp-stat-carousel-prev')), findsNothing);
      expect(find.byKey(const ValueKey('erp-stat-carousel-dot-0')), findsNothing);
      expect(find.byKey(const ValueKey('overview-chips')), findsNothing, reason: 'a single category needs no chips');
    });

    testWidgets('RTL: arrows mirror and next still moves forward', (t) async {
      await _pump(t, _dashboard(), dir: TextDirection.rtl);
      final prevX = t.getCenter(find.byKey(const ValueKey('erp-stat-carousel-prev'))).dx;
      final nextX = t.getCenter(find.byKey(const ValueKey('erp-stat-carousel-next'))).dx;
      expect(nextX, lessThan(prevX));
      final first = t.getCenter(find.descendant(of: find.byType(PageView), matching: find.byKey(const ValueKey('kpi-receivables')))).dx;
      final second = t.getCenter(find.descendant(of: find.byType(PageView), matching: find.byKey(const ValueKey('kpi-collected')))).dx;
      expect(first, greaterThan(second), reason: 'first card on the right in RTL');
      await t.tap(find.byKey(const ValueKey('erp-stat-carousel-next')));
      await t.pumpAndSettle();
      expect(_visibleLabels(t).first, 'Cheques in custody');
    });

    testWidgets('cards and alerts with a known link are tappable; alert rows are 48dp with a chevron', (t) async {
      await _pump(t, _dashboard());
      expect(t.widget<ErpStatCard>(find.descendant(of: find.byType(PageView), matching: find.byKey(const ValueKey('kpi-receivables')))).onTap, isNotNull);
      await t.scrollUntilVisible(find.byKey(const ValueKey('alert-1')), 200, scrollable: find.descendant(of: find.byKey(const ValueKey('overview-list')), matching: find.byType(Scrollable)).first);
      expect(t.getSize(find.byKey(const ValueKey('alert-0'))).height, greaterThanOrEqualTo(48));
      expect(t.widget<InkWell>(find.byKey(const ValueKey('alert-0'))).onTap, isNotNull);
      expect(t.widget<InkWell>(find.byKey(const ValueKey('alert-1'))).onTap, isNull);
      expect(find.descendant(of: find.byKey(const ValueKey('alert-0')), matching: find.byIcon(Icons.chevron_right_rounded)), findsOneWidget);
      expect(find.descendant(of: find.byKey(const ValueKey('alert-1')), matching: find.byIcon(Icons.chevron_right_rounded)), findsNothing);
    });

    testWidgets('more than 5 alerts collapse behind View all', (t) async {
      await _pump(t, _dashboard(alerts: 7));
      final list = find.descendant(of: find.byKey(const ValueKey('overview-list')), matching: find.byType(Scrollable)).first;
      await t.scrollUntilVisible(find.text('View all (7)'), 200, scrollable: list);
      expect(find.byKey(const ValueKey('alert-5')), findsNothing);
      await t.ensureVisible(find.text('View all (7)'));
      await t.pumpAndSettle();
      await t.tap(find.text('View all (7)'));
      await t.pumpAndSettle();
      expect(find.byKey(const ValueKey('alert-6'), skipOffstage: false), findsOneWidget);
      expect(find.text('Show fewer', skipOffstage: false), findsOneWidget);
    });

    testWidgets('empty dashboard shows the empty state', (t) async {
      await _pump(t, Dashboard.fromJson(const {'asOf': '', 'kpis': [], 'charts': [], 'alerts': []}));
      expect(find.byKey(const ValueKey('overview-empty')), findsOneWidget);
    });

    testWidgets('dark theme renders without errors', (t) async {
      await _pump(t, _dashboard(), theme: AppTheme.dark());
      expect(_visibleCards(), findsNWidgets(4));
      expect(t.takeException(), isNull);
    });

    testWidgets('loading shows the skeleton', (t) async {
      final pending = Completer<Dashboard>();
      await t.pumpWidget(ProviderScope(
        overrides: [
          dashboardProvider.overrideWith((ref) => pending.future),
          menuProvider.overrideWith((ref) async => const <MenuGroup>[]),
        ],
        child: MaterialApp(theme: AppTheme.light(), home: const Scaffold(body: DashboardPage())),
      ));
      await t.pump();
      expect(find.byKey(const ValueKey('overview-skeleton')), findsOneWidget);
      pending.complete(_dashboard());
      await t.pumpAndSettle();
      expect(find.byKey(const ValueKey('overview-skeleton')), findsNothing);
      expect(_visibleCards(), findsNWidgets(4));
    });
  });
}
