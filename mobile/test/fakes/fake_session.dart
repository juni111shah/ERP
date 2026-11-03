// Fake session, user, menu and feeds for the tap-target audit. Every provider that would reach the server is overridden.

import 'package:dio/dio.dart';
import 'package:erp_mobile/core/api_client.dart';
import 'package:erp_mobile/core/key_value_store.dart';
import 'package:erp_mobile/core/me.dart';
import 'package:erp_mobile/core/models.dart';
import 'package:erp_mobile/core/session.dart';
import 'package:erp_mobile/features/dashboard/dashboard_page.dart';
import 'package:erp_mobile/features/notifications/notifications.dart';
import 'package:erp_mobile/features/onboarding/onboarding_tour.dart';
import 'package:erp_mobile/screens/all_screens.dart';
import 'package:erp_mobile/theme/app_theme.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;

import 'fake_api.dart';

const fakeWorkspaces = [
  Workspace(id: 'w1', code: 'ACME', name: 'Acme Trading', industry: 'trading'),
  Workspace(id: 'w2', code: 'BETA', name: 'Beta Realty', industry: 'realestate'),
];

const fakeSession = SessionInfo(accessToken: 'token', expiresIn: 3600, workspace: Workspace(id: 'w1', code: 'ACME', name: 'Acme Trading', industry: 'trading'), workspaces: fakeWorkspaces);

/// Signed in to ACME, allowed to switch to BETA. Sign-out and switching stay local.
class FakeSessionController extends SessionController {
  FakeSessionController([this.initial = const SessionState(AuthStatus.signedIn, fakeSession)]);

  final SessionState initial;

  @override
  SessionState build() => initial;

  @override
  Future<void> bootstrap() async {}

  @override
  Future<void> logout() async => state = const SessionState(AuthStatus.signedOut);

  @override
  Future<void> switchWorkspace(String workspaceId) async {}
}

/// An owner with every permission and module, so every button and menu entry is shown.
final fakeMe = Me(
  fullName: 'Muhammad Admin',
  email: 'admin@acme.test',
  baseCurrency: 'QAR',
  countryCode: 'QA',
  permissions: const {'*'},
  legalEntities: const [LegalEntity(id: 'le1', code: 'E1', name: 'Acme LLC', baseCurrency: 'QAR')],
  modules: const {'sales', 'pos', 'distribution', 'purchasing', 'inventory', 'manufacturing', 'hr', 'payroll', 'crm', 'projects', 'services', 'realestate', 'finance'},
);

/// The server menu: one entry per mobile screen, plus one web-only entry.
List<MenuGroup> fakeMenu() => [
      MenuGroup(id: 'all', label: 'All modules', items: [
        for (final s in allScreens)
          for (final id in s.menuIds.take(1)) MenuItem(id: id, label: s.title, route: '/$id', module: id.split('.').first),
        const MenuItem(id: 'web.only', label: 'Web only report', route: '/web/only', module: 'core'),
      ]),
    ];

Dashboard fakeDashboard() => Dashboard.fromJson({
      'asOf': '2026-11-03',
      'kpis': [
        for (final k in const ['revenue', 'receivables', 'payables', 'cash', 'orders', 'stock'])
          {'key': k, 'label': 'KPI $k', 'value': 125000, 'format': 'money', 'caption': 'this month', 'link': '/finance/invoices'},
      ],
      'charts': [
        {
          'key': 'sales',
          'title': 'Sales',
          'categories': ['Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov'],
          'series': [
            {'name': 'Sales', 'values': [10, 20, 15, 30, 25, 40]},
            {'name': 'Purchases', 'values': [5, 12, 9, 18, 20, 22]},
          ],
        },
      ],
      'alerts': [
        {'severity': 'warning', 'category': 'Receivables', 'message': '3 invoices overdue', 'link': '/finance/invoices'},
        {'severity': 'info', 'category': 'Inventory', 'message': '2 items below reorder level', 'link': '/core/items'},
      ],
    });

const fakeFeed = NotificationFeed(items: [
  AppNotification(id: 'n1', title: 'Invoice INV-1 approved', body: 'Receivables', unread: true, createdAt: null),
  AppNotification(id: 'n2', title: 'Leave request submitted', body: 'HR', severity: 'warning'),
], unread: 1);

/// Overrides for a signed-in owner talking to [FakeErpAdapter].
/// [tourSeen] pre-marks the onboarding coach tour as seen so it doesn't cover the shell.
List<Override> fakeOverrides({FakeErpAdapter? adapter, SessionState? session, PendingWorkspaceSelection? pending, bool tourSeen = true}) {
  final store = MemoryKeyValueStore();
  if (tourSeen) store.values[OnboardingTourStore.seenKey] = '1';
  final a = adapter ?? FakeErpAdapter();
  return [
    storeProvider.overrideWithValue(store),
    initialServerUrlProvider.overrideWithValue('http://api.test'),
    apiProvider.overrideWith((ref) => ApiClient(store: store, baseUrl: 'http://api.test', dio: Dio()
      // Generated records are large; Dio would decode them in an isolate, which never finishes under the test clock.
      ..transformer = SyncTransformer()
      ..httpClientAdapter = a)..accessToken = 'token'),
    sessionProvider.overrideWith(() => FakeSessionController(session ?? const SessionState(AuthStatus.signedIn, fakeSession))),
    if (pending != null) pendingWorkspaceProvider.overrideWith(() => _FixedPending(pending)),
    meProvider.overrideWith((ref) async => fakeMe),
    meOrEmptyProvider.overrideWithValue(fakeMe),
    menuProvider.overrideWith((ref) async => fakeMenu()),
    dashboardProvider.overrideWith((ref) async => fakeDashboard()),
    notificationsProvider.overrideWith((ref) async => fakeFeed),
  ];
}

class _FixedPending extends PendingWorkspaceController {
  _FixedPending(this.value);

  final PendingWorkspaceSelection value;

  @override
  PendingWorkspaceSelection? build() => value;
}

/// The app's theme around [child], in [direction]. RTL is forced with a Directionality because the app ships no
/// flutter_localizations (no new packages allowed), which is what a real `ar` locale would add.
Widget fakeHost(Widget child, {TextDirection direction = TextDirection.ltr, List<Override>? overrides}) => ProviderScope(
      overrides: overrides ?? fakeOverrides(),
      child: MaterialApp(
        theme: AppTheme.light(),
        debugShowCheckedModeBanner: false,
        builder: (context, inner) => Directionality(textDirection: direction, child: inner!),
        home: child,
      ),
    );
