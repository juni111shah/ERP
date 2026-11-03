import 'package:dio/dio.dart';
import 'package:erp_mobile/app.dart';
import 'package:erp_mobile/core/api_client.dart';
import 'package:erp_mobile/core/key_value_store.dart';
import 'package:erp_mobile/core/session.dart';
import 'package:erp_mobile/features/auth/login_page.dart';
import 'package:erp_mobile/features/intro/intro_flag.dart';
import 'package:erp_mobile/features/intro/intro_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/fake_adapter.dart';

Widget app(MemoryKeyValueStore store, {bool reduceMotion = false}) {
  final adapter = FakeAdapter((_) => envelope({}));
  Widget child = const ErpApp();
  if (reduceMotion) {
    child = Builder(
      builder: (context) => MediaQuery(data: MediaQuery.of(context).copyWith(disableAnimations: true), child: const ErpApp()),
    );
  }
  return ProviderScope(
    overrides: [
      storeProvider.overrideWithValue(store),
      apiProvider.overrideWith((ref) => ApiClient(store: store, baseUrl: 'http://api.test', dio: Dio()..httpClientAdapter = adapter)),
    ],
    child: child,
  );
}

Future<void> phone(WidgetTester tester) async {
  tester.view.physicalSize = const Size(1080, 2340);
  tester.view.devicePixelRatio = 3;
  addTearDown(tester.view.reset);
}

void main() {
  testWidgets('shows the tour on first open', (tester) async {
    await phone(tester);
    await tester.pumpWidget(app(MemoryKeyValueStore()));
    await tester.pumpAndSettle();

    expect(find.byType(IntroPage), findsOneWidget);
    expect(find.text('1 of 5'), findsOneWidget);
    expect(find.text('Skip'), findsOneWidget);
    expect(find.byType(LoginPage), findsNothing);
  });

  testWidgets('does not show the tour once it was seen', (tester) async {
    await phone(tester);
    final store = MemoryKeyValueStore()..values[IntroKeys.seen] = '1';
    await tester.pumpWidget(app(store));
    await tester.pumpAndSettle();

    expect(find.byType(IntroPage), findsNothing);
    expect(find.byType(LoginPage), findsOneWidget);
  });

  testWidgets('Next walks all five screens, each entrance settles, Get started remembers', (tester) async {
    await phone(tester);
    final store = MemoryKeyValueStore();
    await tester.pumpWidget(app(store));
    await tester.pumpAndSettle();

    for (var n = 1; n <= 5; n++) {
      expect(find.text('$n of 5'), findsOneWidget);
      // Entrance + ambient drift finish and the single ticker stops (pumpAndSettle would time out otherwise).
      await tester.pumpAndSettle();
      expect(tester.binding.hasScheduledFrame, isFalse);
      if (n < 5) {
        expect(find.text('Get started'), findsNothing);
        await tester.tap(find.text('Next'));
        await tester.pumpAndSettle();
      }
    }

    await tester.tap(find.text('Get started'));
    await tester.pumpAndSettle();
    expect(store.values[IntroKeys.seen], '1');
    expect(find.byType(IntroPage), findsNothing);
    expect(find.byType(LoginPage), findsOneWidget);
  });

  testWidgets('Skip remembers and goes on to sign-in', (tester) async {
    await phone(tester);
    final store = MemoryKeyValueStore();
    await tester.pumpWidget(app(store));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Skip'));
    await tester.pumpAndSettle();

    expect(store.values[IntroKeys.seen], '1');
    expect(find.byType(LoginPage), findsOneWidget);
  });

  testWidgets('system back counts as skip', (tester) async {
    await phone(tester);
    final store = MemoryKeyValueStore();
    await tester.pumpWidget(app(store));
    await tester.pumpAndSettle();

    await tester.tap(find.text('Next'));
    await tester.pumpAndSettle();
    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(store.values[IntroKeys.seen], '1');
    expect(find.byType(IntroPage), findsNothing);
    expect(find.byType(LoginPage), findsOneWidget);
  });

  testWidgets('renders with reduced motion', (tester) async {
    await phone(tester);
    final store = MemoryKeyValueStore();
    await tester.pumpWidget(app(store, reduceMotion: true));
    await tester.pumpAndSettle();

    for (var n = 1; n <= 5; n++) {
      expect(find.text('$n of 5'), findsOneWidget);
      if (n < 5) {
        await tester.tap(find.text('Next'));
        await tester.pump();
        await tester.pumpAndSettle();
      }
    }
    expect(tester.takeException(), isNull);
    await tester.tap(find.text('Get started'));
    await tester.pumpAndSettle();
    expect(store.values[IntroKeys.seen], '1');
  });

  testWidgets('Arabic strings and RTL render', (tester) async {
    await phone(tester);
    await tester.pumpWidget(ProviderScope(
      overrides: [storeProvider.overrideWithValue(MemoryKeyValueStore())],
      child: const MaterialApp(
        home: Directionality(textDirection: TextDirection.rtl, child: IntroPage()),
      ),
    ));
    await tester.pumpAndSettle();
    expect(find.text('1 من 5'), findsOneWidget);
    expect(find.text('تخطي'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });
}
