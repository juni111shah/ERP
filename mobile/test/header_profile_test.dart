import 'package:dio/dio.dart';
import 'package:erp_mobile/core/api_client.dart';
import 'package:erp_mobile/core/key_value_store.dart';
import 'package:erp_mobile/core/me.dart';
import 'package:erp_mobile/core/models.dart';
import 'package:erp_mobile/core/session.dart';
import 'package:erp_mobile/core/theme_mode.dart';
import 'package:erp_mobile/features/dashboard/dashboard_page.dart';
import 'package:erp_mobile/features/home/profile_sheet.dart';
import 'package:erp_mobile/features/notifications/notifications.dart';
import 'package:erp_mobile/theme/app_theme.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';

import 'support/fake_adapter.dart';

class _FakeSession extends SessionController {
  bool loggedOut = false;

  @override
  SessionState build() => const SessionState(AuthStatus.signedIn);

  @override
  Future<void> logout() async => loggedOut = true;
}

const _me = Me(
  fullName: 'Amna Khan',
  email: 'amna@acme.test',
  baseCurrency: 'PKR',
  countryCode: 'PK',
  permissions: {'*'},
  legalEntities: [],
  modules: {},
);

NotificationFeed _feed(int n) => NotificationFeed(
      items: [for (var i = 0; i < n; i++) AppNotification(id: '$i', title: 'Invoice INV-$i approved', body: 'Ready to send', severity: 'info', createdAt: DateTime(2026, 11, 3, 9, 30), unread: true)],
      unread: n,
    );

Future<MemoryKeyValueStore> _pump(WidgetTester t, {NotificationFeed? feed, ThemeMode? initial}) async {
  t.view.physicalSize = const Size(1080, 2400);
  t.view.devicePixelRatio = 2.6;
  addTearDown(t.view.reset);
  final store = MemoryKeyValueStore();
  if (initial != null) store.values[themeModeKey] = initial.name;
  final router = GoRouter(routes: [
    GoRoute(path: '/', builder: (_, _) => Scaffold(appBar: AppBar(title: const Text('Overview'), actions: const [HomeHeaderActions()]))),
    GoRoute(path: '/settings', builder: (_, _) => const Scaffold(body: Text('settings-page'))),
  ]);
  addTearDown(router.dispose);
  await t.pumpWidget(ProviderScope(
    overrides: [
      storeProvider.overrideWithValue(store),
      sessionProvider.overrideWith(_FakeSession.new),
      meProvider.overrideWith((ref) async => _me),
      menuProvider.overrideWith((ref) async => const <MenuGroup>[]),
      notificationsProvider.overrideWith((ref) async => feed ?? _feed(3)),
    ],
    child: Consumer(
      builder: (context, ref, _) => MaterialApp.router(
        theme: AppTheme.light(),
        darkTheme: AppTheme.dark(),
        themeMode: ref.watch(themeModeProvider),
        routerConfig: router,
      ),
    ),
  ));
  await t.pumpAndSettle();
  return store;
}

ProviderContainer _container(WidgetTester t) => ProviderScope.containerOf(t.element(find.byType(HomeHeaderActions)));

Future<void> _openProfile(WidgetTester t) async {
  await t.tap(find.byKey(const ValueKey('header-avatar')));
  await t.pumpAndSettle();
}

void main() {
  test('initials come from the name, else the email', () {
    expect(initialsFor('Amna Khan', 'a@b.c'), 'AK');
    expect(initialsFor('Cher', ''), 'C');
    expect(initialsFor('', 'zed@x.io'), 'Z');
    expect(initialsFor('', ''), '?');
  });

  test('NotificationFeed reads the server list', () {
    final f = NotificationFeed.fromJson({
      'unread': 1,
      'items': [
        {'id': 'a', 'title': 'Approved', 'body': null, 'link': '/sales/invoices', 'severity': 'Info', 'createdAt': '2026-11-03T04:30:00Z', 'readAt': null},
        {'id': 'b', 'title': 'Old', 'severity': 'warning', 'createdAt': '2026-11-01T04:30:00Z', 'readAt': '2026-11-02T00:00:00Z'},
      ],
    });
    expect(f.unread, 1);
    expect(f.items.first.unread, isTrue);
    expect(f.items.first.severity, 'info');
    expect(f.items.last.unread, isFalse);
    expect(f.fromAlerts, isFalse);
  });

  test('falls back to dashboard alerts when the endpoint fails', () async {
    final store = MemoryKeyValueStore();
    final c = ProviderContainer(overrides: [
      storeProvider.overrideWithValue(store),
      apiProvider.overrideWith((ref) => ApiClient(store: store, baseUrl: 'http://api.test', dio: Dio()..httpClientAdapter = FakeAdapter((_) => problem(500, 'down')))),
      dashboardProvider.overrideWith((ref) async => const Dashboard(asOf: '2026-11-03', kpis: [], charts: [], alerts: [
            DashboardAlert(severity: 'warning', category: 'Receivables', message: '2 invoices overdue', link: '/sales/invoices'),
          ])),
    ]);
    addTearDown(c.dispose);
    final sub = c.listen(notificationsProvider, (_, _) {});
    addTearDown(sub.close);
    final f = await c.read(notificationsProvider.future);
    expect(f.fromAlerts, isTrue);
    expect(f.items.single.title, '2 invoices overdue');
    expect(f.unread, 1);
  });

  test('theme mode is restored from and saved to the store', () async {
    final store = MemoryKeyValueStore()..values[themeModeKey] = 'dark';
    final c = ProviderContainer(overrides: [storeProvider.overrideWithValue(store)]);
    addTearDown(c.dispose);
    final sub = c.listen(themeModeProvider, (_, _) {});
    addTearDown(sub.close);
    expect(c.read(themeModeProvider), ThemeMode.system);
    await Future<void>.delayed(Duration.zero);
    expect(c.read(themeModeProvider), ThemeMode.dark);
    await c.read(themeModeProvider.notifier).set(ThemeMode.light);
    expect(store.values[themeModeKey], 'light');
  });

  testWidgets('header shows the bell count and an avatar dot, and no gear or theme icon', (t) async {
    await _pump(t);
    expect(find.byTooltip('Settings'), findsNothing);
    expect(find.byIcon(Icons.settings_outlined), findsNothing);
    expect(find.byIcon(Icons.dark_mode_outlined), findsNothing);
    expect(t.widget<Badge>(find.byKey(const ValueKey('bell-badge'))).isLabelVisible, isTrue);
    expect(find.descendant(of: find.byKey(const ValueKey('bell-badge')), matching: find.text('3')), findsOneWidget);
    expect(t.widget<Badge>(find.byKey(const ValueKey('avatar-dot'))).isLabelVisible, isTrue);
    expect(find.text('AK'), findsOneWidget);
    expect(t.getSize(find.byKey(const ValueKey('header-bell'))), const Size(48, 48));
    expect(t.getSize(find.byKey(const ValueKey('header-avatar'))), const Size(48, 48));
  });

  testWidgets('no unread hides both badges', (t) async {
    await _pump(t, feed: NotificationFeed.empty);
    expect(t.widget<Badge>(find.byKey(const ValueKey('bell-badge'))).isLabelVisible, isFalse);
    expect(t.widget<Badge>(find.byKey(const ValueKey('avatar-dot'))).isLabelVisible, isFalse);
  });

  testWidgets('avatar opens the account sheet with user, workspace rows and 48dp rows', (t) async {
    await _pump(t);
    await _openProfile(t);
    expect(find.byType(DraggableScrollableSheet), findsOneWidget);
    expect(find.text('Amna Khan'), findsOneWidget);
    expect(find.text('amna@acme.test'), findsOneWidget);
    for (final k in ['profile-notifications', 'profile-settings', 'profile-about', 'profile-signout']) {
      expect(find.byKey(ValueKey(k)), findsOneWidget);
      expect(t.getSize(find.byKey(ValueKey(k))).height, greaterThanOrEqualTo(48));
    }
    expect(find.text('Appearance'), findsOneWidget);
    expect(t.getSize(find.byKey(const ValueKey('appearance'))).height, greaterThanOrEqualTo(48));
  });

  testWidgets('Appearance switches themeMode and the app goes dark, then persists', (t) async {
    final store = await _pump(t);
    await _openProfile(t);
    await t.tap(find.text('Dark'));
    await t.pumpAndSettle();
    expect(_container(t).read(themeModeProvider), ThemeMode.dark);
    expect(store.values[themeModeKey], 'dark');
    expect(Theme.of(t.element(find.byType(HomeHeaderActions))).brightness, Brightness.dark);
    await t.tap(find.text('Light'));
    await t.pumpAndSettle();
    expect(Theme.of(t.element(find.byType(HomeHeaderActions))).brightness, Brightness.light);
    await t.tap(find.text('System'));
    await t.pumpAndSettle();
    expect(_container(t).read(themeModeProvider), ThemeMode.system);
  });

  testWidgets('renders in dark mode without errors', (t) async {
    await _pump(t, initial: ThemeMode.dark);
    expect(Theme.of(t.element(find.byType(HomeHeaderActions))).brightness, Brightness.dark);
    await _openProfile(t);
    expect(t.takeException(), isNull);
    expect(find.text('Amna Khan'), findsOneWidget);
  });

  testWidgets('Settings is still reachable from the sheet', (t) async {
    await _pump(t);
    await _openProfile(t);
    await t.tap(find.byKey(const ValueKey('profile-settings')));
    await t.pumpAndSettle();
    expect(find.text('settings-page'), findsOneWidget);
  });

  testWidgets('Sign out asks first; Cancel keeps you in, Sign out logs out', (t) async {
    await _pump(t);
    final session = _container(t).read(sessionProvider.notifier) as _FakeSession;
    await _openProfile(t);
    await t.tap(find.byKey(const ValueKey('profile-signout')));
    await t.pumpAndSettle();
    expect(find.text('Sign out?'), findsOneWidget);
    await t.tap(find.text('Cancel'));
    await t.pumpAndSettle();
    expect(session.loggedOut, isFalse);
    await _openProfile(t);
    await t.tap(find.byKey(const ValueKey('profile-signout')));
    await t.pumpAndSettle();
    await t.tap(find.widgetWithText(FilledButton, 'Sign out'));
    await t.pumpAndSettle();
    expect(session.loggedOut, isTrue);
  });

  testWidgets('bell opens the notifications sheet with rows', (t) async {
    await _pump(t);
    await t.tap(find.byKey(const ValueKey('header-bell')));
    await t.pumpAndSettle();
    expect(find.text('Notifications'), findsOneWidget);
    expect(find.text('3 unread'), findsOneWidget);
    expect(find.text('Invoice INV-0 approved'), findsOneWidget);
    expect(find.byKey(const ValueKey('notifications-empty')), findsNothing);
  });

  testWidgets('notifications row in the account sheet opens the list; empty state', (t) async {
    await _pump(t, feed: NotificationFeed.empty);
    await _openProfile(t);
    await t.tap(find.byKey(const ValueKey('profile-notifications')));
    await t.pumpAndSettle();
    expect(find.byKey(const ValueKey('notifications-empty')), findsOneWidget);
    expect(find.text("You're all caught up"), findsOneWidget);
  });
}
