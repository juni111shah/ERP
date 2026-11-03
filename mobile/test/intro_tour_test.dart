import 'package:dio/dio.dart';
import 'package:erp_mobile/app.dart';
import 'package:erp_mobile/core/api_client.dart';
import 'package:erp_mobile/core/brand.dart';
import 'package:erp_mobile/core/key_value_store.dart';
import 'package:erp_mobile/core/session.dart';
import 'package:erp_mobile/features/auth/login_page.dart';
import 'package:erp_mobile/features/intro/intro_flag.dart';
import 'package:erp_mobile/features/intro/intro_motion.dart';
import 'package:erp_mobile/features/intro/intro_page.dart';
import 'package:erp_mobile/features/intro/intro_strings.dart';
import 'package:erp_mobile/kit/brand_mark.dart';
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

const _back = ValueKey('intro-back');

Future<void> _nextTimes(WidgetTester tester, int times) async {
  for (var i = 0; i < times; i++) {
    await tester.tap(find.text('Next'));
    await tester.pumpAndSettle();
  }
}

void main() {
  testWidgets('shows the tour on first open', (tester) async {
    await phone(tester);
    await tester.pumpWidget(app(MemoryKeyValueStore()));
    await tester.pumpAndSettle();

    expect(find.byType(IntroPage), findsOneWidget);
    expect(find.text('1 of 5'), findsOneWidget);
    expect(find.text('Skip'), findsOneWidget);
    expect(find.text(Brand.name), findsOneWidget);
    expect(find.byType(BrandMark), findsWidgets);
    expect(find.text('Know where your business stands in seconds'), findsOneWidget);
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
        expect(find.text(IntroStrings.en.titles[n - 1]), findsOneWidget);
        await tester.tap(find.text('Next'));
        await tester.pumpAndSettle();
      }
    }

    expect(find.text('Run your whole business from one app'), findsOneWidget);
    expect(find.text('Everything in one app'), findsOneWidget);
    for (final f in IntroStrings.en.features) {
      expect(find.text(f), findsOneWidget);
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

  testWidgets('system back on screen 1 still counts as skip', (tester) async {
    await phone(tester);
    final store = MemoryKeyValueStore();
    await tester.pumpWidget(app(store));
    await tester.pumpAndSettle();

    expect(find.text('1 of 5'), findsOneWidget);
    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(store.values[IntroKeys.seen], '1');
    expect(find.byType(IntroPage), findsNothing);
    expect(find.byType(LoginPage), findsOneWidget);
  });

  testWidgets('Back is absent on screen 1 and present, 48dp and labelled on screens 2-5', (tester) async {
    await phone(tester);
    final semantics = tester.ensureSemantics();
    await tester.pumpWidget(app(MemoryKeyValueStore()));
    await tester.pumpAndSettle();

    expect(find.byKey(_back), findsNothing);
    for (var n = 2; n <= 5; n++) {
      await tester.tap(find.text('Next'));
      await tester.pumpAndSettle();
      expect(find.text('$n of 5'), findsOneWidget);
      expect(find.byKey(_back), findsOneWidget);
      final size = tester.getSize(find.byKey(_back));
      expect(size.width, greaterThanOrEqualTo(48));
      expect(size.height, greaterThanOrEqualTo(48));
      expect(
        tester.getSemantics(find.byKey(_back)),
        isSemantics(label: 'Back', hint: 'Previous screen', isButton: true, hasTapAction: true),
      );
      await expectLater(tester, meetsGuideline(androidTapTargetGuideline));
      // Skip and the "n of 5" indicator are still there, unchanged.
      expect(find.text('Skip'), findsOneWidget);
    }
    expect(find.text('Get started'), findsOneWidget);
    semantics.dispose();
  });

  testWidgets('tapping Back on screen 3 goes to screen 2 and replays its entrance', (tester) async {
    await phone(tester);
    final store = MemoryKeyValueStore();
    await tester.pumpWidget(app(store));
    await tester.pumpAndSettle();
    await _nextTimes(tester, 2);
    expect(find.text('3 of 5'), findsOneWidget);

    await tester.tap(find.byKey(_back));
    await tester.pump();
    await tester.pump(IntroMotion.pageTurn + const Duration(milliseconds: 50));
    expect(find.text('2 of 5'), findsOneWidget);
    // The entrance + drift restart for the screen we came back to, then settle and the ticker stops again.
    expect(tester.binding.hasScheduledFrame, isTrue);
    await tester.pumpAndSettle();
    expect(tester.binding.hasScheduledFrame, isFalse);
    expect(find.text(IntroStrings.en.titles[1]), findsOneWidget);
    expect(store.values[IntroKeys.seen], isNull);
    expect(tester.takeException(), isNull);
  });

  testWidgets('swiping right on screen 2 goes back to screen 1; swiping on screen 5 does not finish', (tester) async {
    await phone(tester);
    final store = MemoryKeyValueStore();
    await tester.pumpWidget(app(store));
    await tester.pumpAndSettle();
    await _nextTimes(tester, 1);
    expect(find.text('2 of 5'), findsOneWidget);

    await tester.fling(find.byType(PageView), const Offset(300, 0), 1000);
    await tester.pumpAndSettle();
    expect(find.text('1 of 5'), findsOneWidget);
    expect(find.byKey(_back), findsNothing);
    expect(store.values[IntroKeys.seen], isNull);

    await _nextTimes(tester, 4);
    expect(find.text('5 of 5'), findsOneWidget);
    await tester.fling(find.byType(PageView), const Offset(-300, 0), 1000);
    await tester.pumpAndSettle();
    expect(find.text('5 of 5'), findsOneWidget);
    expect(find.byType(IntroPage), findsOneWidget);
    expect(store.values[IntroKeys.seen], isNull);
  });

  testWidgets('system back on screen 4 goes to screen 3 and does not mark the tour seen', (tester) async {
    await phone(tester);
    final store = MemoryKeyValueStore();
    await tester.pumpWidget(app(store));
    await tester.pumpAndSettle();
    await _nextTimes(tester, 3);
    expect(find.text('4 of 5'), findsOneWidget);

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(find.text('3 of 5'), findsOneWidget);
    expect(find.byType(IntroPage), findsOneWidget);
    expect(find.byType(LoginPage), findsNothing);
    expect(store.values[IntroKeys.seen], isNull);

    // Back again twice reaches screen 1; one more system back there is Skip, as before.
    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();
    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();
    expect(find.text('1 of 5'), findsOneWidget);
    expect(store.values[IntroKeys.seen], isNull);
    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();
    expect(store.values[IntroKeys.seen], '1');
    expect(find.byType(LoginPage), findsOneWidget);
  });

  testWidgets('Skip on a later screen and Get started after going back still remember and route on', (tester) async {
    await phone(tester);
    final store = MemoryKeyValueStore();
    await tester.pumpWidget(app(store));
    await tester.pumpAndSettle();
    await _nextTimes(tester, 4);
    await tester.tap(find.byKey(_back));
    await tester.pumpAndSettle();
    expect(find.text('4 of 5'), findsOneWidget);
    await _nextTimes(tester, 1);
    await tester.tap(find.text('Get started'));
    await tester.pumpAndSettle();
    expect(store.values[IntroKeys.seen], '1');
    expect(find.byType(LoginPage), findsOneWidget);

    final store2 = MemoryKeyValueStore();
    await tester.pumpWidget(const SizedBox());
    await tester.pumpWidget(app(store2));
    await tester.pumpAndSettle();
    await _nextTimes(tester, 2);
    await tester.tap(find.byKey(_back));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Skip'));
    await tester.pumpAndSettle();
    expect(store2.values[IntroKeys.seen], '1');
    expect(find.byType(LoginPage), findsOneWidget);
  });

  testWidgets('Back works with reduced motion', (tester) async {
    await phone(tester);
    await tester.pumpWidget(app(MemoryKeyValueStore(), reduceMotion: true));
    await tester.pumpAndSettle();
    await _nextTimes(tester, 2);
    await tester.tap(find.byKey(_back));
    await tester.pump();
    await tester.pumpAndSettle();
    expect(find.text('2 of 5'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('Back in Arabic / RTL and with a dark system theme: labelled, mirrored swipe goes back', (tester) async {
    await phone(tester);
    final semantics = tester.ensureSemantics();
    final store = MemoryKeyValueStore();
    await tester.pumpWidget(ProviderScope(
      overrides: [storeProvider.overrideWithValue(store)],
      child: MaterialApp(
        darkTheme: ThemeData.dark(),
        themeMode: ThemeMode.dark,
        home: const Directionality(textDirection: TextDirection.rtl, child: IntroPage()),
      ),
    ));
    await tester.pumpAndSettle();
    final ar = IntroStrings.ar;
    expect(find.byKey(_back), findsNothing);
    await tester.tap(find.text(ar.next));
    await tester.pumpAndSettle();
    expect(find.text(ar.step(2, 5)), findsOneWidget);
    expect(
      tester.getSemantics(find.byKey(_back)),
      isSemantics(label: ar.back, hint: ar.previousScreen, isButton: true, hasTapAction: true),
    );
    // Back sits at the start (right) side in RTL; the Next button at the end (left).
    expect(tester.getCenter(find.byKey(_back)).dx, greaterThan(tester.getCenter(find.text(ar.next)).dx));
    await expectLater(tester, meetsGuideline(androidTapTargetGuideline));

    // In RTL the previous screen is to the right, so the finger moves right-to-left.
    await tester.fling(find.byType(PageView), const Offset(-300, 0), 1000);
    await tester.pumpAndSettle();
    expect(find.text(ar.step(1, 5)), findsOneWidget);

    await tester.tap(find.text(ar.next));
    await tester.pumpAndSettle();
    await tester.tap(find.text(ar.next));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(_back));
    await tester.pumpAndSettle();
    expect(find.text(ar.step(2, 5)), findsOneWidget);
    expect(store.values[IntroKeys.seen], isNull);
    expect(tester.takeException(), isNull);
    semantics.dispose();
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
