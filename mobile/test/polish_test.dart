import 'dart:async';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:erp_mobile/core/api_client.dart';
import 'package:erp_mobile/core/key_value_store.dart';
import 'package:erp_mobile/core/me.dart';
import 'package:erp_mobile/core/models.dart';
import 'package:erp_mobile/core/session.dart';
import 'package:erp_mobile/features/common/paged_list_view.dart';
import 'package:erp_mobile/features/dashboard/dashboard_page.dart';
import 'package:erp_mobile/features/home/profile_sheet.dart';
import 'package:erp_mobile/features/notifications/notifications.dart';
import 'package:erp_mobile/kit/field.dart';
import 'package:erp_mobile/kit/list_screen.dart';
import 'package:erp_mobile/kit/motion.dart';
import 'package:erp_mobile/kit/skeleton.dart';
import 'package:erp_mobile/kit/stat_card.dart';
import 'package:erp_mobile/kit/stat_carousel.dart';
import 'package:erp_mobile/kit/state_view.dart';
import 'package:erp_mobile/theme/app_theme.dart';
import 'package:erp_mobile/theme/tokens.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';

import 'support/fake_adapter.dart';

const _me = Me(fullName: 'Amna Khan', email: 'amna@acme.test', baseCurrency: 'PKR', countryCode: 'PK', permissions: {'*'}, legalEntities: [], modules: {});

class _FakeSession extends SessionController {
  @override
  SessionState build() => const SessionState(AuthStatus.signedIn);
}

void _phone(WidgetTester t) {
  t.view.physicalSize = const Size(1080, 2400);
  t.view.devicePixelRatio = 2.6;
  addTearDown(t.view.reset);
}

Widget _app(Widget home, {List<Override> overrides = const [], bool reduceMotion = false}) => ProviderScope(
      retry: (_, _) => null, // no automatic provider retries: the Retry button is what we test
      overrides: overrides,
      child: MaterialApp(
        theme: AppTheme.light(),
        builder: (context, child) => MediaQuery(data: MediaQuery.of(context).copyWith(disableAnimations: reduceMotion), child: child!),
        home: Scaffold(body: home),
      ),
    );

Map<String, dynamic> _kpi(String key, String label, String link) => {'key': key, 'label': label, 'value': 1000, 'format': 'money', 'caption': null, 'link': link};

Dashboard _dash({bool charts = true}) => Dashboard.fromJson({
      'asOf': '2026-11-03',
      'kpis': [
        for (var i = 0; i < 6; i++) _kpi('f$i', 'Finance $i', '/finance/x'),
        for (var i = 0; i < 2; i++) _kpi('r$i', 'Estate $i', '/real-estate/x'),
      ],
      'charts': [
        if (charts)
          {
            'key': 'empty',
            'title': 'Empty chart',
            'categories': <String>[],
            'series': [
              {'name': 'Count', 'values': <num>[]},
            ],
          },
      ],
      'alerts': [],
    });

List<Override> _dashOverrides(Future<Dashboard> Function() load) => [
      dashboardProvider.overrideWith((ref) => load()),
      menuProvider.overrideWith((ref) async => const <MenuGroup>[]),
    ];

/// A list host whose API answers from [handler].
List<Override> _apiOverrides(HttpClientAdapter adapter) {
  final store = MemoryKeyValueStore();
  return [
    storeProvider.overrideWithValue(store),
    apiProvider.overrideWith((ref) => ApiClient(store: store, baseUrl: 'http://api.test', dio: Dio()..httpClientAdapter = adapter)),
    meProvider.overrideWith((ref) async => _me),
  ];
}

ResponseBody _page(List<Map<String, dynamic>> items) => envelope({'items': items, 'total': items.length, 'page': 1, 'pageSize': 25});

void main() {
  group('tokens', () {
    test('the theme text styles come from the one type scale', () {
      final tt = AppTheme.light().textTheme;
      expect(tt.displaySmall!.fontSize, ErpType.display);
      expect(tt.displaySmall!.fontFeatures, ErpFonts.tabular);
      expect(tt.headlineSmall!.fontSize, ErpType.headline);
      expect(tt.titleLarge!.fontSize, ErpType.heading);
      expect(tt.titleMedium!.fontSize, ErpType.title);
      expect(tt.bodyMedium!.fontSize, ErpType.body);
      expect(tt.bodySmall!.fontSize, ErpType.caption);
      expect(tt.labelSmall!.fontSize, ErpType.label);
      expect(AppTheme.dark().textTheme.bodyMedium!.fontSize, ErpType.body);
    });

    test('spacing sits on a 2px grid and the scale is ordered', () {
      const steps = [ErpSpace.s05, ErpSpace.s1, ErpSpace.s15, ErpSpace.s2, ErpSpace.s25, ErpSpace.s3, ErpSpace.s35, ErpSpace.s4, ErpSpace.s5, ErpSpace.s6, ErpSpace.s8, ErpSpace.s10, ErpSpace.s12];
      for (var i = 0; i < steps.length; i++) {
        expect(steps[i] % 2, 0);
        if (i > 0) expect(steps[i], greaterThan(steps[i - 1]));
      }
      const sizes = [ErpType.micro, ErpType.label, ErpType.caption, ErpType.bodySmall, ErpType.body, ErpType.bodyLarge, ErpType.title, ErpType.titleLarge, ErpType.heading, ErpType.headline, ErpType.display];
      for (var i = 1; i < sizes.length; i++) {
        expect(sizes[i], greaterThan(sizes[i - 1]));
      }
    });

    testWidgets('screens render with the scale (section label, KPI value, state title)', (t) async {
      _phone(t);
      await t.pumpWidget(_app(const DashboardPage(), overrides: _dashOverrides(() async => _dash())));
      await t.pumpAndSettle();
      final heading = t.widget<Text>(find.text('HIGHLIGHTS'));
      expect(heading.style!.fontSize, ErpType.label);
      expect(heading.style!.letterSpacing, ErpType.sectionLabel.letterSpacing);
      final value = t.widget<RichText>(find.descendant(of: find.byKey(const ValueKey('kpi-f0')), matching: find.byType(RichText)).at(1));
      expect(value.text.style!.fontSize, ErpType.display);
      final stateTitle = t.widget<Text>(find.descendant(of: find.byKey(const ValueKey('chart-empty')), matching: find.byKey(const ValueKey('state-title'))));
      expect(stateTitle.style!.fontSize, ErpType.body);
    });
  });

  group('skeletons and states', () {
    testWidgets('overview: skeleton with shimmer while loading, then content', (t) async {
      _phone(t);
      final pending = Completer<Dashboard>();
      await t.pumpWidget(_app(const DashboardPage(), overrides: _dashOverrides(() => pending.future)));
      await t.pump();
      expect(find.byKey(const ValueKey('overview-skeleton')), findsOneWidget);
      expect(find.byType(ErpShimmer), findsOneWidget);
      expect(find.byType(CircularProgressIndicator), findsNothing);
      await t.pump(const Duration(milliseconds: 600)); // the shimmer keeps running without errors
      pending.complete(_dash());
      await t.pumpAndSettle();
      expect(find.byKey(const ValueKey('overview-skeleton')), findsNothing);
      expect(find.byType(ErpStatCard), findsWidgets);
    });

    testWidgets('a chart with no data shows a state, not a blank box', (t) async {
      _phone(t);
      await t.pumpWidget(_app(const DashboardPage(), overrides: _dashOverrides(() async => _dash())));
      await t.pumpAndSettle();
      await t.scrollUntilVisible(find.byKey(const ValueKey('chart-empty')), 200, scrollable: find.byType(Scrollable).first);
      expect(find.text('No chart data'), findsOneWidget);
    });

    testWidgets('overview error: Retry reloads', (t) async {
      _phone(t);
      var calls = 0;
      await t.pumpWidget(_app(const DashboardPage(), overrides: _dashOverrides(() async {
        calls++;
        if (calls == 1) throw Exception('Network is unreachable');
        return _dash();
      })));
      await t.pumpAndSettle();
      expect(find.byType(ErpStateView), findsOneWidget);
      expect(find.text('Retry'), findsOneWidget);
      await t.tap(find.byKey(const ValueKey('state-action')));
      await t.pumpAndSettle();
      expect(calls, 2);
      expect(find.byType(ErpStatCard), findsWidgets);
    });

    testWidgets('list: skeleton while loading, then rows', (t) async {
      _phone(t);
      final gate = Completer<void>();
      final adapter = FakeAdapter((_) => _page([
            {'id': '1', 'name': 'Row one'},
          ]));
      final slow = _GatedAdapter(adapter, gate.future);
      await t.pumpWidget(_app(PagedListView(path: '/x', itemBuilder: (c, i) => ListTile(title: Text(i['name'] as String))), overrides: _apiOverrides(slow)));
      await t.pump();
      expect(find.byKey(const ValueKey('list-skeleton')), findsOneWidget);
      gate.complete();
      await t.pumpAndSettle();
      expect(find.byKey(const ValueKey('list-skeleton')), findsNothing);
      expect(find.text('Row one'), findsOneWidget);
    });

    testWidgets('list error: Retry loads again', (t) async {
      _phone(t);
      var n = 0;
      final adapter = FakeAdapter((_) => ++n == 1 ? problem(503, 'Server unavailable') : _page([
            {'id': '1', 'name': 'Back again'},
          ]));
      await t.pumpWidget(_app(PagedListView(path: '/x', itemBuilder: (c, i) => ListTile(title: Text(i['name'] as String))), overrides: _apiOverrides(adapter)));
      await t.pumpAndSettle();
      expect(find.text('Server unavailable'), findsOneWidget);
      await t.tap(find.text('Retry'));
      await t.pumpAndSettle();
      expect(adapter.requests, hasLength(2));
      expect(find.text('Back again'), findsOneWidget);
    });

    testWidgets('empty list shows the empty state', (t) async {
      _phone(t);
      await t.pumpWidget(_app(PagedListView(path: '/x', emptyText: 'No invoices found.', itemBuilder: (c, i) => const SizedBox()), overrides: _apiOverrides(FakeAdapter((_) => _page([])))));
      await t.pumpAndSettle();
      expect(find.byKey(const ValueKey('list-empty')), findsOneWidget);
      expect(find.text('No invoices found.'), findsOneWidget);
    });

    testWidgets('empty filtered list offers Clear filter, which drops the filter', (t) async {
      _phone(t);
      final adapter = FakeAdapter((o) => o.queryParameters.containsKey('filter') ? _page([]) : _page([
            {'id': '1', 'name': 'Unfiltered row'},
          ]));
      await t.pumpWidget(_app(
        ListScreen(title: 'Invoices', path: '/x', showAppBar: false, emptyText: 'No invoices found.', filters: const [Option('paid', 'Paid')], itemBuilder: (c, i) => ListTile(title: Text(i['name'] as String))),
        overrides: _apiOverrides(adapter),
      ));
      await t.pumpAndSettle();
      await t.tap(find.text('Paid'));
      await t.pumpAndSettle();
      expect(find.byKey(const ValueKey('list-empty-filtered')), findsOneWidget);
      await t.tap(find.text('Clear filter'));
      await t.pumpAndSettle();
      expect(find.text('Unfiltered row'), findsOneWidget);
      expect(adapter.requests.last.queryParameters.containsKey('filter'), isFalse);
    });

    testWidgets('notifications: skeleton while loading, then the empty state', (t) async {
      _phone(t);
      final pending = Completer<NotificationFeed>();
      await t.pumpWidget(_app(const NotificationsSheet(), overrides: [
        notificationsProvider.overrideWith((ref) => pending.future),
        menuProvider.overrideWith((ref) async => const <MenuGroup>[]),
      ]));
      await t.pump();
      expect(find.byKey(const ValueKey('notifications-skeleton')), findsOneWidget);
      pending.complete(NotificationFeed.empty);
      await t.pumpAndSettle();
      expect(find.byKey(const ValueKey('notifications-empty')), findsOneWidget);
      expect(find.text("You're all caught up"), findsOneWidget);
    });
  });

  group('motion', () {
    testWidgets('carousel: dots animate width on page change; arrows fade at the ends', (t) async {
      _phone(t);
      await t.pumpWidget(_app(const DashboardPage(), overrides: _dashOverrides(() async => _dash(charts: false))));
      await t.pumpAndSettle();
      double dot(int i) => t.getSize(find.byKey(ValueKey('erp-stat-carousel-dot-$i'))).width;
      double opacity(String which) => t.widget<AnimatedOpacity>(find.ancestor(of: find.byKey(ValueKey('erp-stat-carousel-$which')), matching: find.byType(AnimatedOpacity)).first).opacity;
      expect(dot(0), 18);
      expect(opacity('prev'), lessThan(1));
      expect(opacity('next'), 1);
      await t.tap(find.byKey(const ValueKey('erp-stat-carousel-next')));
      await t.pump();
      await t.pump(const Duration(milliseconds: 150));
      await t.pump(const Duration(milliseconds: 60));
      expect(dot(1), inExclusiveRange(6, 18)); // mid-animation
      await t.pumpAndSettle();
      expect(dot(1), 18);
      expect(dot(0), 6);
      expect(opacity('prev'), 1);
      expect(opacity('next'), lessThan(1));
    });

    testWidgets('carousel: chip change replays the card fade-in', (t) async {
      _phone(t);
      await t.pumpWidget(_app(const DashboardPage(), overrides: _dashOverrides(() async => _dash(charts: false))));
      await t.pumpAndSettle();
      await t.tap(find.descendant(of: find.byKey(const ValueKey('overview-chips')), matching: find.textContaining('Real')).first);
      await t.pump();
      await t.pump(const Duration(milliseconds: 40));
      final o = t.widget<Opacity>(find.descendant(of: find.byKey(const ValueKey('overview-cards-real-estate')), matching: find.byType(Opacity)).first);
      expect(o.opacity, lessThan(1));
      await t.pumpAndSettle();
      final done = t.widget<Opacity>(find.descendant(of: find.byKey(const ValueKey('overview-cards-real-estate')), matching: find.byType(Opacity)).first);
      expect(done.opacity, 1);
    });

    testWidgets('reduced motion: pages jump and dots finish in one frame', (t) async {
      _phone(t);
      await t.pumpWidget(_app(const DashboardPage(), reduceMotion: true, overrides: _dashOverrides(() async => _dash(charts: false))));
      await t.pumpAndSettle();
      await t.tap(find.byKey(const ValueKey('erp-stat-carousel-next')));
      await t.pump();
      await t.pump();
      expect(t.getSize(find.byKey(const ValueKey('erp-stat-carousel-dot-1'))).width, 18);
      expect(find.descendant(of: find.byType(ErpStatCarousel), matching: find.byType(TweenAnimationBuilder<double>)), findsNothing);
    });

    testWidgets('reduced motion: the skeleton does not shimmer', (t) async {
      _phone(t);
      final pending = Completer<Dashboard>();
      await t.pumpWidget(_app(const DashboardPage(), reduceMotion: true, overrides: _dashOverrides(() => pending.future)));
      await t.pump();
      expect(find.byKey(const ValueKey('overview-skeleton')), findsOneWidget);
      expect(find.byType(ShaderMask), findsNothing);
      pending.complete(_dash());
      await t.pumpAndSettle();
    });

    testWidgets('reduced motion: ErpFadeSlideIn and ErpPressable are pass-through', (t) async {
      await t.pumpWidget(_app(const ErpFadeSlideIn(child: ErpPressable(child: Text('x'))), reduceMotion: true));
      expect(find.byType(Opacity), findsNothing);
      expect(find.byType(AnimatedScale), findsNothing);
    });

    testWidgets('profile sheet rows stagger in and reach their final state', (t) async {
      _phone(t);
      await t.pumpWidget(ProviderScope(
        overrides: [
          storeProvider.overrideWithValue(MemoryKeyValueStore()),
          sessionProvider.overrideWith(_FakeSession.new),
          meProvider.overrideWith((ref) async => _me),
          menuProvider.overrideWith((ref) async => const <MenuGroup>[]),
          notificationsProvider.overrideWith((ref) async => NotificationFeed.empty),
        ],
        child: MaterialApp(theme: AppTheme.light(), home: Scaffold(appBar: AppBar(actions: const [HomeHeaderActions()]))),
      ));
      await t.pumpAndSettle();
      await t.tap(find.byKey(const ValueKey('header-avatar')));
      await t.pump();
      await t.pump(const Duration(milliseconds: 60));
      Opacity rowOpacity(String key) => t.widget<Opacity>(find.ancestor(of: find.byKey(ValueKey(key)), matching: find.byType(Opacity)).first);
      expect(rowOpacity('profile-signout').opacity, lessThan(1));
      await t.pumpAndSettle();
      for (final k in ['profile-notifications', 'profile-settings', 'profile-about', 'profile-signout']) {
        expect(rowOpacity(k).opacity, 1, reason: k);
      }
      // The appearance thumb slides to the tapped segment.
      AlignmentDirectional thumb() => t.widget<AnimatedAlign>(find.byKey(const ValueKey('segmented-thumb'))).alignment as AlignmentDirectional;
      expect(thumb().start, 1); // system
      await t.tap(find.text('Light'));
      await t.pumpAndSettle();
      expect(thumb().start, -1);
    });

    testWidgets('bell badge pulses when the count changes', (t) async {
      _phone(t);
      final feed = NotifierProvider<_Count, int>(_Count.new);
      await t.pumpWidget(ProviderScope(
        overrides: [
          storeProvider.overrideWithValue(MemoryKeyValueStore()),
          sessionProvider.overrideWith(_FakeSession.new),
          meProvider.overrideWith((ref) async => _me),
          notificationsProvider.overrideWith((ref) async {
            final n = ref.watch(feed);
            return NotificationFeed(items: const [], unread: n);
          }),
        ],
        child: MaterialApp(theme: AppTheme.light(), home: Scaffold(appBar: AppBar(actions: const [HomeHeaderActions()]))),
      ));
      await t.pumpAndSettle();
      double scale() => t.widget<Transform>(find.descendant(of: find.byKey(const ValueKey('bell-pulse')), matching: find.byType(Transform)).first).transform.getMaxScaleOnAxis();
      expect(scale(), 1);
      ProviderScope.containerOf(t.element(find.byType(HomeHeaderActions))).read(feed.notifier).set(4);
      await t.pump();
      await t.pump();
      await t.pump(const Duration(milliseconds: 150));
      expect(scale(), greaterThan(1));
      await t.pumpAndSettle();
      expect(scale(), 1);
    });
  });
}

/// Holds every answer until [gate] completes, so loading states can be seen.
class _GatedAdapter implements HttpClientAdapter {
  _GatedAdapter(this.inner, this.gate);

  final HttpClientAdapter inner;
  final Future<void> gate;

  @override
  Future<ResponseBody> fetch(RequestOptions options, Stream<Uint8List>? requestStream, Future<void>? cancelFuture) async {
    await gate;
    return inner.fetch(options, null, cancelFuture);
  }

  @override
  void close({bool force = false}) {}
}

class _Count extends Notifier<int> {
  @override
  int build() => 1;

  void set(int v) => state = v;
}
