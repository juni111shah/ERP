import 'package:erp_mobile/kit/bottom_nav.dart';
import 'package:erp_mobile/theme/tokens.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

const _items = [
  ErpNavItem(icon: Icons.dashboard_outlined, activeIcon: Icons.dashboard, label: 'Overview'),
  ErpNavItem(icon: Icons.receipt_long_outlined, activeIcon: Icons.receipt_long, label: 'Invoices'),
  ErpNavItem(icon: Icons.payments_outlined, activeIcon: Icons.payments, label: 'Receipts'),
  ErpNavItem(icon: Icons.people_outline, activeIcon: Icons.people, label: 'Customers'),
  ErpNavItem(icon: Icons.apps_outlined, activeIcon: Icons.apps, label: 'More'),
];

Widget _host(int index, TextDirection dir, {ValueChanged<int>? onTap}) => MaterialApp(
      home: Directionality(
        textDirection: dir,
        child: Scaffold(extendBody: true, body: const SizedBox.expand(), bottomNavigationBar: ErpBottomNav(items: _items, currentIndex: index, onTap: onTap ?? (_) {})),
      ),
    );

/// Centre x of the lifted selection circle.
double _circleX(WidgetTester t) {
  final circle = find.byWidgetPredicate((w) => w is DecoratedBox && w.decoration is BoxDecoration && (w.decoration as BoxDecoration).shape == BoxShape.circle && (w.decoration as BoxDecoration).color == ErpColors.brandSubtle);
  expect(circle, findsOneWidget);
  return t.getCenter(circle).dx;
}

void main() {
  testWidgets('first tab notch sits at the start edge in LTR and the end (right) edge in RTL', (t) async {
    await t.pumpWidget(_host(0, TextDirection.ltr));
    await t.pumpAndSettle();
    final width = t.getSize(find.byType(ErpBottomNav)).width;
    final ltr = _circleX(t);
    expect(ltr, lessThan(width * 0.25));
    final ltrLabel = t.getCenter(find.text('Overview')).dx;
    expect((ltr - ltrLabel).abs(), lessThan(4));

    await t.pumpWidget(_host(0, TextDirection.rtl));
    await t.pumpAndSettle();
    final rtl = _circleX(t);
    expect(rtl, greaterThan(width * 0.75));
    final rtlLabel = t.getCenter(find.text('Overview')).dx;
    expect((rtl - rtlLabel).abs(), lessThan(4), reason: 'circle must sit over the Overview tab, which RTL moves to the right');
  });

  testWidgets('selection slides between tabs instead of jumping', (t) async {
    await t.pumpWidget(_host(0, TextDirection.ltr));
    await t.pumpAndSettle();
    final start = _circleX(t);
    await t.pumpWidget(_host(2, TextDirection.ltr));
    await t.pump(const Duration(milliseconds: 120));
    final mid = _circleX(t);
    await t.pumpAndSettle();
    final end = _circleX(t);
    expect(mid, greaterThan(start));
    expect(mid, lessThan(end));
    expect((end - t.getCenter(find.text('Receipts')).dx).abs(), lessThan(4));
  });

  testWidgets('tapping a tab reports its index', (t) async {
    int? tapped;
    await t.pumpWidget(_host(0, TextDirection.ltr, onTap: (i) => tapped = i));
    await t.tap(find.text('Customers'));
    expect(tapped, 3);
  });
}
