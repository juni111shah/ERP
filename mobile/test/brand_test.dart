import 'dart:io';

import 'package:dio/dio.dart';
import 'package:erp_mobile/core/api_client.dart';
import 'package:erp_mobile/core/brand.dart';
import 'package:erp_mobile/core/key_value_store.dart';
import 'package:erp_mobile/core/me.dart';
import 'package:erp_mobile/core/models.dart';
import 'package:erp_mobile/core/session.dart';
import 'package:erp_mobile/features/auth/login_page.dart';
import 'package:erp_mobile/features/auth/workspace_page.dart';
import 'package:erp_mobile/features/home/home_page.dart';
import 'package:erp_mobile/features/notifications/notifications.dart';
import 'package:erp_mobile/kit/brand_mark.dart';
import 'package:erp_mobile/theme/app_theme.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';

import 'support/fake_adapter.dart';

class _SignedIn extends SessionController {
  @override
  SessionState build() => const SessionState(AuthStatus.signedIn);
}

class _Choosing extends PendingWorkspaceController {
  @override
  PendingWorkspaceSelection? build() => const PendingWorkspaceSelection('sel', [
        Workspace(id: 'w1', code: 'ACME', name: 'Acme Trading', industry: 'Retail'),
        Workspace(id: 'w2', code: 'BETA', name: 'Beta Foods', industry: ''),
      ]);
}

const _me = Me(fullName: 'Amna Khan', email: 'amna@acme.test', baseCurrency: 'PKR', countryCode: 'PK', permissions: {'*'}, legalEntities: [], modules: {});

Future<void> _phone(WidgetTester t) async {
  t.view.physicalSize = const Size(1080, 2340);
  t.view.devicePixelRatio = 3;
  addTearDown(t.view.reset);
}

ProviderScope _scope(Widget home, {List<Override> overrides = const [], ThemeData? theme}) {
  final store = MemoryKeyValueStore();
  return ProviderScope(
    overrides: [
      storeProvider.overrideWithValue(store),
      apiProvider.overrideWith((ref) => ApiClient(store: store, baseUrl: 'http://api.test', dio: Dio()..httpClientAdapter = FakeAdapter((_) => envelope({})))),
      ...overrides,
    ],
    child: MaterialApp(theme: theme ?? AppTheme.light(), home: home),
  );
}

void main() {
  group('brand constants', () {
    test('the product name is HigherSquare 360, split into word and suffix', () {
      expect(Brand.name, 'HigherSquare 360');
      expect('${Brand.word} ${Brand.suffix}', Brand.name);
      expect(Brand.launcherLabel, 'HS 360');
    });

    test('Android and iOS home-screen labels use the brand launcher label', () {
      final manifest = File('android/app/src/main/AndroidManifest.xml').readAsStringSync();
      expect(manifest, contains('android:label="${Brand.launcherLabel}"'));
      expect(manifest, contains('android:roundIcon="@mipmap/ic_launcher_round"'));
      final plist = File('ios/Runner/Info.plist').readAsStringSync();
      expect(plist, matches(RegExp('<key>CFBundleDisplayName</key>\\s*<string>${Brand.launcherLabel}</string>')));
    });

    test('launcher icons exist for every Android density and every iOS slot', () {
      for (final d in ['mdpi', 'hdpi', 'xhdpi', 'xxhdpi', 'xxxhdpi']) {
        for (final f in ['ic_launcher', 'ic_launcher_round', 'ic_launcher_foreground']) {
          expect(File('android/app/src/main/res/mipmap-$d/$f.png').existsSync(), isTrue, reason: '$d/$f');
        }
      }
      for (final f in ['ic_launcher', 'ic_launcher_round']) {
        final xml = File('android/app/src/main/res/mipmap-anydpi-v26/$f.xml').readAsStringSync();
        expect(xml, contains('@color/ic_launcher_background'));
        expect(xml, contains('@mipmap/ic_launcher_foreground'));
      }
      final contents = File('ios/Runner/Assets.xcassets/AppIcon.appiconset/Contents.json').readAsStringSync();
      for (final m in RegExp(r'"filename" : "([^"]+)"').allMatches(contents)) {
        expect(File('ios/Runner/Assets.xcassets/AppIcon.appiconset/${m.group(1)}').existsSync(), isTrue, reason: m.group(1));
      }
      expect(File('assets/brand/hs360-mark.svg').existsSync(), isTrue);
    });
  });

  group('BrandMark and BrandLockup', () {
    testWidgets('paints crisply at small and large sizes and is announced by name', (t) async {
      final handle = t.ensureSemantics();
      await t.pumpWidget(MaterialApp(
        home: Scaffold(
          body: Column(children: const [BrandMark(size: 16), BrandMark(size: 48), BrandMark(size: 160, shadow: true)]),
        ),
      ));
      expect(t.takeException(), isNull);
      expect(t.getSize(find.byType(BrandMark).first), const Size(16, 16));
      expect(t.getSize(find.byType(BrandMark).last), const Size(160, 160));
      expect(find.bySemanticsLabel(Brand.name), findsNWidgets(3));
      handle.dispose();
    });

    testWidgets('wordmark uses the web colours on light and on dark', (t) async {
      Future<List<Color?>> colours(ThemeData theme) async {
        await t.pumpWidget(MaterialApp(theme: theme, home: const Scaffold(body: BrandLockup())));
        await t.pumpAndSettle(); // MaterialApp animates between themes
        final rich = t.widget<Text>(find.text(Brand.name)).textSpan! as TextSpan;
        return [for (final c in rich.children!) (c as TextSpan).style?.color];
      }

      expect(await colours(AppTheme.light()), [BrandColors.wordLight, BrandColors.suffixLight]);
      expect(await colours(AppTheme.dark()), [BrandColors.wordDark, BrandColors.suffixDark]);
    });

    testWidgets('splash shows the stacked lockup', (t) async {
      await t.pumpWidget(MaterialApp(theme: AppTheme.light(), home: const BrandSplash()));
      expect(find.byType(BrandMark), findsOneWidget);
      expect(find.text(Brand.name), findsOneWidget);
    });
  });

  group('screens', () {
    testWidgets('sign-in page shows the mark and the brand name above the form', (t) async {
      await _phone(t);
      await t.pumpWidget(_scope(const LoginPage()));
      await t.pumpAndSettle();
      expect(find.byType(BrandMark), findsOneWidget);
      expect(find.text(Brand.name), findsOneWidget);
      expect(find.text('ERP'), findsNothing);
      final markBottom = t.getBottomLeft(find.byType(BrandMark)).dy;
      expect(markBottom, lessThan(t.getTopLeft(find.widgetWithText(TextFormField, 'Email')).dy));
      expect(t.getBottomLeft(find.text(Brand.name)).dy, lessThan(t.getTopLeft(find.widgetWithText(TextFormField, 'Email')).dy));
    });

    testWidgets('sign-in branding fits a 320dp phone with no overflow', (t) async {
      t.view.physicalSize = const Size(960, 1700);
      t.view.devicePixelRatio = 3;
      addTearDown(t.view.reset);
      await t.pumpWidget(_scope(const LoginPage()));
      await t.pumpAndSettle();
      expect(t.takeException(), isNull);
      expect(find.text(Brand.name), findsOneWidget);
    });

    testWidgets('workspace picker shows the brand lockup', (t) async {
      await _phone(t);
      await t.pumpWidget(_scope(const WorkspacePage(), overrides: [pendingWorkspaceProvider.overrideWith(_Choosing.new)]));
      await t.pumpAndSettle();
      expect(find.byType(BrandLockup), findsOneWidget);
      expect(find.text(Brand.name), findsOneWidget);
      expect(find.text('Acme Trading'), findsOneWidget);
    });

    testWidgets('home header shows the brand mark', (t) async {
      await _phone(t);
      await t.pumpWidget(_scope(const HomePage(), overrides: [
        sessionProvider.overrideWith(_SignedIn.new),
        meProvider.overrideWith((ref) async => _me),
        menuProvider.overrideWith((ref) async => const <MenuGroup>[]),
        notificationsProvider.overrideWith((ref) async => const NotificationFeed(items: [], unread: 0)),
      ]));
      await t.pump(const Duration(milliseconds: 300));
      final bar = find.byType(AppBar);
      expect(find.descendant(of: bar, matching: find.byType(BrandMark)), findsOneWidget);
      expect(find.descendant(of: bar, matching: find.text('Overview')), findsOneWidget);
    });
  });
}
