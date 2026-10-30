import 'package:dio/dio.dart';
import 'package:erp_mobile/core/api_client.dart';
import 'package:erp_mobile/core/key_value_store.dart';
import 'package:erp_mobile/core/session.dart';
import 'package:erp_mobile/features/auth/login_page.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/fake_adapter.dart';

Widget app(FakeAdapter adapter, MemoryKeyValueStore store) => ProviderScope(
      overrides: [
        storeProvider.overrideWithValue(store),
        apiProvider.overrideWith((ref) => ApiClient(store: store, baseUrl: 'http://api.test', dio: Dio()..httpClientAdapter = adapter)),
      ],
      child: const MaterialApp(home: LoginPage()),
    );

void main() {
  testWidgets('asks for e-mail and password before calling the server', (tester) async {
    final adapter = FakeAdapter((_) => envelope({}));
    await tester.pumpWidget(app(adapter, MemoryKeyValueStore()));

    await tester.tap(find.text('Sign in').last);
    await tester.pump();

    expect(find.text('Enter your email address'), findsOneWidget);
    expect(find.text('Enter your password'), findsOneWidget);
    expect(adapter.requests, isEmpty);
  });

  testWidgets('shows the server message when the password is wrong', (tester) async {
    final adapter = FakeAdapter((_) => problem(401, 'Invalid email or password.'));
    await tester.pumpWidget(app(adapter, MemoryKeyValueStore()));

    await tester.enterText(find.widgetWithText(TextFormField, 'Email'), 'me@acme.test');
    await tester.enterText(find.widgetWithText(TextFormField, 'Password'), 'wrong');
    await tester.tap(find.text('Sign in').last);
    await tester.pumpAndSettle();

    expect(find.text('Invalid email or password.'), findsOneWidget);
  });

  testWidgets('asks for the authenticator code when the account uses two-factor', (tester) async {
    final adapter = FakeAdapter((_) => envelope({'requiresTwoFactor': true}));
    await tester.pumpWidget(app(adapter, MemoryKeyValueStore()));

    await tester.enterText(find.widgetWithText(TextFormField, 'Email'), 'me@acme.test');
    await tester.enterText(find.widgetWithText(TextFormField, 'Password'), 'pw');
    await tester.tap(find.text('Sign in').last);
    await tester.pumpAndSettle();

    expect(find.text('Authenticator or recovery code'), findsOneWidget);
    expect(find.text('Verify and sign in'), findsOneWidget);
  });

  testWidgets('remembers the e-mail used last time', (tester) async {
    final store = MemoryKeyValueStore()..values[StoreKeys.lastEmail] = 'last@acme.test';
    await tester.pumpWidget(app(FakeAdapter((_) => envelope({})), store));
    await tester.pumpAndSettle();
    expect(find.text('last@acme.test'), findsOneWidget);
  });
}
