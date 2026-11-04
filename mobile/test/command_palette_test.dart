import 'package:erp_mobile/core/palette_registry.dart';
import 'package:erp_mobile/kit/command_palette.dart';
import 'package:erp_mobile/theme/app_theme.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';

const _entries = [
  PaletteEntry(id: 'invoices', label: 'Invoices', subtitle: 'Bills you sent to customers', icon: Icons.receipt_long_outlined, route: '/home'),
  PaletteEntry(id: 'inventory', label: 'Stock on hand', subtitle: 'How much of each item you have', icon: Icons.inventory_2_outlined, keywords: ['inventory']),
  PaletteEntry(id: 'vendors', label: 'Vendors', subtitle: 'Suppliers you buy from', icon: Icons.store_outlined),
  PaletteEntry(id: 'settings', label: 'Settings', subtitle: 'Workspace, server and sign out', icon: Icons.settings_outlined, route: '/settings'),
  PaletteEntry(id: 'notifications', label: 'Notifications', subtitle: 'Alerts that need attention', icon: Icons.notifications_none_rounded),
];

Widget _host({
  required ValueChanged<PaletteEntry> onSelect,
  PaletteRecents? recents,
  TextDirection dir = TextDirection.ltr,
  ThemeData? theme,
}) =>
    MaterialApp(
      theme: theme ?? AppTheme.light(),
      home: Directionality(
        textDirection: dir,
        child: Scaffold(body: CommandPalette(entries: _entries, recents: recents, onSelect: onSelect)),
      ),
    );

List<String> _ids(String q) => [for (final m in rankPaletteEntries(q, _entries)) m.entry.id];

void main() {
  group('fuzzy ranking', () {
    test('subsequence matches, non-matches are dropped', () {
      expect(_ids('inv'), ['invoices', 'inventory']); // label prefix beats keyword match
      expect(_ids('stng'), ['settings']);
      expect(_ids('zzz'), isEmpty);
    });

    test('word starts beat scattered letters and label beats subtitle', () {
      expect(_ids('soh').first, 'inventory'); // S-tock O-n H-and
      expect(_ids('suppliers'), ['vendors']); // subtitle-only match still found
      final label = scorePaletteEntry('vendors', _entries[2])!.score;
      final sub = scorePaletteEntry('suppliers', _entries[2])!.score;
      expect(label, greaterThan(sub));
    });

    test('label hits are reported for highlighting', () {
      expect(scorePaletteEntry('inv', _entries[0])!.labelHits, [0, 1, 2]);
      expect(scorePaletteEntry('soh', _entries[1])!.labelHits, [0, 6, 9]);
      expect(scorePaletteEntry('inventory', _entries[1])!.labelHits, isEmpty); // keyword-only
    });

    test('empty query keeps the given order', () {
      expect(_ids(''), _entries.map((e) => e.id).toList());
    });
  });

  group('recents', () {
    test('newest first, de-duplicated, capped', () {
      final r = MemoryPaletteRecents(capacity: 3);
      r
        ..add('a')
        ..add('b')
        ..add('c')
        ..add('a')
        ..add('d');
      expect(r.ids, ['d', 'a', 'c']);
    });

    testWidgets('empty query lists recents first under a Recent header', (t) async {
      final r = MemoryPaletteRecents(initial: ['settings', 'vendors']);
      await t.pumpWidget(_host(onSelect: (_) {}, recents: r));
      expect(find.text('RECENT'), findsOneWidget);
      double y(String label) => t.getTopLeft(find.text(label)).dy;
      expect(y('Settings'), lessThan(y('Vendors')));
      expect(y('Vendors'), lessThan(y('Invoices')));
      expect(find.text('Settings'), findsOneWidget); // not repeated under All
    });
  });

  group('keyboard', () {
    testWidgets('arrows move the highlight, Enter selects it', (t) async {
      PaletteEntry? picked;
      await t.pumpWidget(_host(onSelect: (e) => picked = e));
      await t.pump();
      await t.enterText(find.byType(TextField), 'in');
      await t.pump();
      await t.sendKeyEvent(LogicalKeyboardKey.arrowDown);
      await t.sendKeyEvent(LogicalKeyboardKey.arrowDown);
      await t.sendKeyEvent(LogicalKeyboardKey.arrowUp);
      await t.pump();
      final second = rankPaletteEntries('in', _entries)[1].entry;
      await t.sendKeyEvent(LogicalKeyboardKey.enter);
      await t.pump();
      expect(picked, second);
    });

    testWidgets('Esc closes the sheet; selection resolves showCommandPalette and calls onSelect with the route', (t) async {
      final recents = MemoryPaletteRecents();
      String? route;
      Future<PaletteEntry?>? result;
      await t.pumpWidget(MaterialApp(
        theme: AppTheme.light(),
        home: Builder(
          builder: (context) => TextButton(
            onPressed: () => result = showCommandPalette(context, entries: _entries, recents: recents, onSelect: (e) => route = e.route),
            child: const Text('open'),
          ),
        ),
      ));
      await t.tap(find.text('open'));
      await t.pumpAndSettle();
      expect(find.byType(CommandPalette), findsOneWidget);
      await t.sendKeyEvent(LogicalKeyboardKey.escape);
      await t.pumpAndSettle();
      expect(find.byType(CommandPalette), findsNothing);
      expect(await result, isNull);

      await t.tap(find.text('open'));
      await t.pumpAndSettle();
      await t.enterText(find.byType(TextField), 'sett');
      await t.pump();
      await t.sendKeyEvent(LogicalKeyboardKey.enter);
      await t.pumpAndSettle();
      expect(find.byType(CommandPalette), findsNothing);
      expect((await result)?.id, 'settings');
      expect(route, '/settings');
      expect(recents.ids, ['settings']);
    });

    testWidgets('tapping a row selects it', (t) async {
      PaletteEntry? picked;
      await t.pumpWidget(_host(onSelect: (e) => picked = e));
      await t.tap(find.text('Vendors'));
      expect(picked?.id, 'vendors');
    });
  });

  testWidgets('empty state and 48dp+ rows with semantics', (t) async {
    final handle = t.ensureSemantics();
    await t.pumpWidget(_host(onSelect: (_) {}));
    final row = find.ancestor(of: find.text('Vendors'), matching: find.byType(InkWell)).first;
    expect(t.getSize(row).height, greaterThanOrEqualTo(48));
    expect(find.bySemanticsLabel('Vendors. Suppliers you buy from'), findsOneWidget);
    await t.enterText(find.byType(TextField), 'qqq');
    await t.pump();
    expect(find.textContaining('No matches for'), findsOneWidget);
    handle.dispose();
  });

  testWidgets('RTL: icon on the right, text right-aligned', (t) async {
    await t.pumpWidget(_host(onSelect: (_) {}, dir: TextDirection.rtl));
    expect(t.takeException(), isNull);
    final width = t.getSize(find.byType(CommandPalette)).width;
    final icon = t.getCenter(find.byIcon(Icons.store_outlined)).dx;
    final label = t.getCenter(find.text('Vendors')).dx;
    expect(icon, greaterThan(width * 0.8));
    expect(label, lessThan(icon));
    final search = t.getCenter(find.byIcon(Icons.search_rounded)).dx;
    expect(search, greaterThan(width / 2));
  });

  testWidgets('dark theme renders with dark surfaces and highlight', (t) async {
    await t.pumpWidget(_host(onSelect: (_) {}, theme: AppTheme.dark()));
    expect(t.takeException(), isNull);
    final erp = ErpTheme.of(t.element(find.byType(CommandPalette)));
    expect(erp, ErpTheme.dark);
    final highlighted = t.widgetList<Material>(find.byType(Material)).where((m) => m.color == ErpTheme.dark.brandSubtle);
    expect(highlighted, hasLength(1)); // first row highlighted
  });

  group('registry', () {
    final entries = buildPaletteEntries();
    test('core entries map to existing go_router paths', () {
      final byId = {for (final e in entries) e.id: e};
      for (final id in ['tab.overview', 'tab.invoices', 'tab.receipts', 'tab.customers', 'tab.more']) {
        expect(byId[id]!.route, '/home');
        expect(byId[id]!.data, isA<TabTarget>());
      }
      expect(byId['settings']!.route, '/settings');
      expect(byId['switch-workspace']!.route, '/settings');
      expect(byId['notifications']!.data, isA<ActionTarget>());
      expect(byId['appearance']!.data, isA<ActionTarget>());
      const known = {'/home', '/settings'};
      for (final e in entries) {
        if (e.route != null) expect(known, contains(e.route));
      }
    });

    test('every More screen except tab duplicates is listed with a real subtitle', () {
      final screens = entries.where((e) => e.data is ScreenTarget).toList();
      expect(screens.length, 83); // 87 screens minus the 4 that are tabs
      expect(screens.where((e) => e.subtitle == 'Open ${e.label}'), isEmpty); // every screen has hand-written metadata
      expect(entries.map((e) => e.id).toSet().length, entries.length);
    });

    test('allowedMenuIds filters screens', () {
      expect(buildPaletteEntries(allowedMenuIds: const {}).length, corePaletteEntries.length);
    });
  });
}
