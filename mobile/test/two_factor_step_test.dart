import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:erp_mobile/core/api_client.dart';
import 'package:erp_mobile/core/key_value_store.dart';
import 'package:erp_mobile/core/session.dart';
import 'package:erp_mobile/features/auth/login_page.dart';
import 'package:erp_mobile/features/auth/two_factor_step.dart';
import 'package:erp_mobile/theme/app_theme.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/fake_adapter.dart';

const png = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==';

Map<String, dynamic> setupBody() => {
      'otpAuthUri': 'otpauth://totp/ERP:me%40acme.test?secret=JBSWY3DPEHPK3PXP&issuer=ERP&digits=6&period=30',
      'secret': 'JBSW Y3DP EHPK 3PXP',
      'qrCodeDataUri': png,
      'issuer': 'ERP',
      'account': 'me@acme.test',
      'digits': 6,
      'period': 30,
    };

Map<String, dynamic> bodyOf(RequestOptions o) => o.data is String ? jsonDecode(o.data as String) as Map<String, dynamic> : o.data as Map<String, dynamic>;

/// The sign-in page with a scripted API; returns the container so the session can be inspected.
Future<ProviderContainer> pumpLogin(WidgetTester tester, FakeAdapter adapter, {ThemeMode mode = ThemeMode.light, TextDirection dir = TextDirection.ltr}) async {
  await tester.binding.setSurfaceSize(const Size(420, 1400));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  final store = MemoryKeyValueStore();
  final container = ProviderContainer(overrides: [
    storeProvider.overrideWithValue(store),
    apiProvider.overrideWith((ref) => ApiClient(store: store, baseUrl: 'http://api.test', dio: Dio()..httpClientAdapter = adapter)),
  ]);
  addTearDown(container.dispose);
  await tester.pumpWidget(UncontrolledProviderScope(
    container: container,
    child: MaterialApp(
      theme: AppTheme.light(),
      darkTheme: AppTheme.dark(),
      themeMode: mode,
      builder: (context, child) => Directionality(textDirection: dir, child: child!),
      home: const LoginPage(),
    ),
  ));
  return container;
}

Future<void> signIn(WidgetTester tester) async {
  await tester.enterText(find.widgetWithText(TextFormField, 'Email'), 'me@acme.test');
  await tester.enterText(find.widgetWithText(TextFormField, 'Password'), 'pw');
  await tester.tap(find.text('Sign in').last);
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('first sign-in: QR, "Can\'t scan? Show key" with copy, auto-submit, recovery codes, then the session', (tester) async {
    final copied = <String>[];
    tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(SystemChannels.platform, (call) async {
      if (call.method == 'Clipboard.setData') copied.add((call.arguments as Map)['text'] as String);
      return null;
    });
    addTearDown(() => tester.binding.defaultBinaryMessenger.setMockMethodCallHandler(SystemChannels.platform, null));

    final adapter = FakeAdapter((o) => switch (o.path) {
          '/api/v1/auth/login' => envelope({'twoFactorSetupRequired': true, 'challengeToken': 'chal-setup', 'challengeExpiresIn': 600}),
          '/api/v1/auth/login/2fa/setup' => envelope(setupBody()),
          '/api/v1/auth/login/2fa/verify' => envelope({
              ...sessionBody('at-1'),
              'recoveryCodes': ['AAAAA-11111', 'BBBBB-22222'],
            }),
          _ => problem(404, 'not found'),
        });
    final container = await pumpLogin(tester, adapter);
    await signIn(tester);

    expect(find.text('Set up two-factor sign-in'), findsOneWidget);
    expect(find.byKey(const Key('twofa-qr')), findsOneWidget);
    expect(bodyOf(adapter.requests[1]), {'challengeToken': 'chal-setup'});
    expect(container.read(sessionProvider).status, isNot(AuthStatus.signedIn));

    expect(find.byKey(const Key('twofa-secret')), findsNothing);
    await tester.tap(find.text("Can't scan? Show key"));
    await tester.pumpAndSettle();
    expect(find.text('JBSW Y3DP EHPK 3PXP'), findsOneWidget);
    await tester.tap(find.byKey(const Key('twofa-copy-key')));
    await tester.pump();
    expect(copied, ['JBSWY3DPEHPK3PXP']);

    final field = tester.widget<TextField>(find.byKey(const Key('twofa-code')));
    expect(field.autofillHints, contains(AutofillHints.oneTimeCode));
    expect(field.keyboardType, TextInputType.number);

    await tester.enterText(find.byKey(const Key('twofa-code')), '12a34');
    await tester.pump();
    expect(adapter.requests.length, 2, reason: 'not six digits yet');
    await tester.enterText(find.byKey(const Key('twofa-code')), '123456');
    await tester.pumpAndSettle();
    expect(bodyOf(adapter.requests[2]), {'challengeToken': 'chal-setup', 'code': '123456'});

    expect(find.text('Save your recovery codes'), findsOneWidget);
    expect(find.text('AAAAA-11111'), findsOneWidget);
    expect(container.read(sessionProvider).status, isNot(AuthStatus.signedIn), reason: 'held back until the codes are seen');
    await tester.tap(find.byKey(const Key('twofa-continue')));
    await tester.pump();
    expect(container.read(sessionProvider).status, AuthStatus.signedIn);
    expect(container.read(sessionProvider).info?.accessToken, 'at-1');
  });

  testWidgets('returning user: a wrong code shows an error, the next one signs in', (tester) async {
    var attempts = 0;
    final adapter = FakeAdapter((o) => switch (o.path) {
          '/api/v1/auth/login' => envelope({'requiresTwoFactor': true, 'twoFactorRequired': true, 'challengeToken': 'chal-v', 'challengeExpiresIn': 600}),
          '/api/v1/auth/login/2fa/verify' => ++attempts == 1 ? problem(401, 'The verification code is incorrect.') : envelope(sessionBody('at-2')),
          _ => problem(404, 'not found'),
        });
    final container = await pumpLogin(tester, adapter);
    await signIn(tester);

    expect(find.text('Enter your verification code'), findsOneWidget);
    expect(find.byKey(const Key('twofa-qr')), findsNothing);
    await tester.enterText(find.byKey(const Key('twofa-code')), '000000');
    await tester.pumpAndSettle();
    expect(find.textContaining("That code didn't work"), findsOneWidget);
    expect(container.read(sessionProvider).status, isNot(AuthStatus.signedIn));

    await tester.enterText(find.byKey(const Key('twofa-code')), '654321');
    await tester.pumpAndSettle();
    expect(container.read(sessionProvider).status, AuthStatus.signedIn);
    expect(container.read(sessionProvider).info?.accessToken, 'at-2');
  });

  testWidgets('lockout and an expired challenge; Back returns to email and password', (tester) async {
    var attempts = 0;
    final adapter = FakeAdapter((o) => switch (o.path) {
          '/api/v1/auth/login' => envelope({'twoFactorRequired': true, 'challengeToken': 'chal-v', 'challengeExpiresIn': 600}),
          '/api/v1/auth/login/2fa/verify' => ++attempts == 1 ? problem(423, 'Locked.') : problem(401, 'This sign-in has expired. Sign in again.'),
          _ => problem(404, 'not found'),
        });
    await pumpLogin(tester, adapter);
    await signIn(tester);

    await tester.enterText(find.byKey(const Key('twofa-code')), '111111');
    await tester.pumpAndSettle();
    expect(find.textContaining('locked'), findsOneWidget);
    await tester.enterText(find.byKey(const Key('twofa-code')), '222222');
    await tester.pumpAndSettle();
    expect(find.text('Sign in again'), findsOneWidget);
    await tester.tap(find.byKey(const Key('twofa-restart')));
    await tester.pumpAndSettle();
    expect(find.widgetWithText(TextFormField, 'Email'), findsOneWidget);
  });

  testWidgets('a recovery code is typed in full and sent with the button', (tester) async {
    final adapter = FakeAdapter((o) => switch (o.path) {
          '/api/v1/auth/login' => envelope({'twoFactorRequired': true, 'challengeToken': 'chal-v'}),
          '/api/v1/auth/login/2fa/verify' => envelope(sessionBody('at-3')),
          _ => problem(404, 'not found'),
        });
    final container = await pumpLogin(tester, adapter);
    await signIn(tester);
    await tester.tap(find.byKey(const Key('twofa-use-recovery')));
    await tester.pumpAndSettle();
    await tester.enterText(find.byKey(const Key('twofa-code')), 'ABCDE-12345');
    await tester.pump();
    expect(adapter.requests.length, 1);
    await tester.tap(find.byKey(const Key('twofa-submit')));
    await tester.pumpAndSettle();
    expect(bodyOf(adapter.requests[1])['code'], 'ABCDE-12345');
    expect(container.read(sessionProvider).status, AuthStatus.signedIn);
  });

  testWidgets('dark mode and right-to-left: green tokens, key kept left-to-right, 48dp targets', (tester) async {
    final adapter = FakeAdapter((o) => switch (o.path) {
          '/api/v1/auth/login' => envelope({'twoFactorSetupRequired': true, 'challengeToken': 'chal-setup'}),
          '/api/v1/auth/login/2fa/setup' => envelope(setupBody()),
          _ => problem(404, 'not found'),
        });
    await pumpLogin(tester, adapter, mode: ThemeMode.dark, dir: TextDirection.rtl);
    await signIn(tester);
    await tester.tap(find.text("Can't scan? Show key"));
    await tester.pumpAndSettle();

    final ctx = tester.element(find.byType(TwoFactorStep));
    expect(Theme.of(ctx).brightness, Brightness.dark);
    expect(Directionality.of(ctx), TextDirection.rtl);
    expect(ErpTheme.of(ctx).brand, const Color(0xFF34BC86));
    expect(Directionality.of(tester.element(find.byKey(const Key('twofa-secret')))), TextDirection.ltr);
    for (final key in ['twofa-show-key', 'twofa-copy-key', 'twofa-submit', 'twofa-cancel']) {
      final size = tester.getSize(find.byKey(Key(key)));
      expect(size.height, greaterThanOrEqualTo(48), reason: key);
      expect(size.width, greaterThanOrEqualTo(48), reason: key);
    }
  });

  test('qrBytes reads a PNG data URI and refuses anything else', () {
    expect(qrBytes(png)!.sublist(0, 4), [0x89, 0x50, 0x4E, 0x47]);
    expect(qrBytes('https://example.com/qr.png'), isNull);
  });
}
