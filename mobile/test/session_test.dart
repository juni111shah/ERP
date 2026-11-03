import 'package:dio/dio.dart';
import 'package:erp_mobile/core/api_client.dart';
import 'package:erp_mobile/core/key_value_store.dart';
import 'package:erp_mobile/core/session.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/fake_adapter.dart';

ProviderContainer containerWith(FakeAdapter adapter, MemoryKeyValueStore store) {
  final container = ProviderContainer(overrides: [
    storeProvider.overrideWithValue(store),
    apiProvider.overrideWith((ref) {
      final client = ApiClient(store: store, baseUrl: 'http://api.test', dio: Dio()..httpClientAdapter = adapter);
      client.onSessionExpired = () => ref.read(sessionProvider.notifier).expired();
      return client;
    }),
  ]);
  addTearDown(container.dispose);
  return container;
}

void main() {
  test('starts loading, then signed out when there is no stored cookie', () async {
    final container = containerWith(FakeAdapter((_) => problem(401, 'no')), MemoryKeyValueStore());
    expect(container.read(sessionProvider).status, AuthStatus.loading);
    await container.read(sessionProvider.notifier).bootstrap();
    expect(container.read(sessionProvider).status, AuthStatus.signedOut);
  });

  test('a stored cookie signs the user back in on start', () async {
    final adapter = FakeAdapter((_) => envelope(sessionBody('t1')));
    final store = MemoryKeyValueStore()..values[StoreKeys.refreshCookie] = 'c';
    final container = containerWith(adapter, store);
    await container.read(sessionProvider.notifier).bootstrap();
    final state = container.read(sessionProvider);
    expect(state.status, AuthStatus.signedIn);
    expect(state.workspace?.name, 'Acme Trading');
    expect(container.read(apiProvider).accessToken, 't1');
  });

  test('sign-in stores the refresh cookie and the e-mail', () async {
    final adapter = FakeAdapter((_) => envelope(sessionBody('t2'), headers: {
          'set-cookie': ['erp_rt=secret; path=/api/v1/auth; httponly']
        }));
    final store = MemoryKeyValueStore();
    final container = containerWith(adapter, store);

    final outcome = await container.read(sessionProvider.notifier).login(email: ' me@acme.test ', password: 'pw', rememberMe: true);

    expect(outcome, isA<LoggedIn>());
    expect(container.read(sessionProvider).status, AuthStatus.signedIn);
    expect(store.values[StoreKeys.refreshCookie], 'secret');
    expect(store.values[StoreKeys.lastEmail], 'me@acme.test');
    final body = adapter.requests.single.data as Map;
    expect(body['email'], 'me@acme.test');
    expect(body['rememberMe'], true);
    expect(body.containsKey('twoFactorCode'), isFalse);
  });

  test('two-factor accounts are asked for a code, which is then sent along', () async {
    var call = 0;
    final adapter = FakeAdapter((o) => ++call == 1 ? envelope({'requiresTwoFactor': true}) : envelope(sessionBody('t3')));
    final container = containerWith(adapter, MemoryKeyValueStore());
    final session = container.read(sessionProvider.notifier);

    expect(await session.login(email: 'a@b.c', password: 'pw', rememberMe: false), isA<NeedsTwoFactor>());
    expect(container.read(sessionProvider).status, AuthStatus.loading); // nothing changed yet

    expect(await session.login(email: 'a@b.c', password: 'pw', rememberMe: false, twoFactorCode: ' 123456 '), isA<LoggedIn>());
    expect((adapter.requests.last.data as Map)['twoFactorCode'], '123456');
  });

  test('an account in several workspaces must choose one', () async {
    var call = 0;
    final adapter = FakeAdapter((o) => ++call == 1
        ? envelope({
            'requiresWorkspaceSelection': true,
            'selectionToken': 'sel-1',
            'workspaces': [
              {'id': 'w1', 'code': 'A', 'name': 'Acme', 'industry': 'x'},
              {'id': 'w2', 'code': 'B', 'name': 'Beta', 'industry': 'y'},
            ],
          })
        : envelope(sessionBody('t4')));
    final container = containerWith(adapter, MemoryKeyValueStore());
    final session = container.read(sessionProvider.notifier);

    final outcome = await session.login(email: 'a@b.c', password: 'pw', rememberMe: true);
    expect(outcome, isA<NeedsWorkspace>());
    final needs = outcome as NeedsWorkspace;
    expect(needs.selectionToken, 'sel-1');
    expect(needs.workspaces.map((w) => w.id), ['w1', 'w2']);

    await session.selectWorkspace('sel-1', 'w2');
    expect(container.read(sessionProvider).status, AuthStatus.signedIn);
    expect((adapter.requests.last.data as Map)['workspaceId'], 'w2');
  });

  test('a wrong password surfaces the server message and stays signed out', () async {
    final container = containerWith(FakeAdapter((_) => problem(401, 'Invalid email or password.')), MemoryKeyValueStore());
    await expectLater(
      container.read(sessionProvider.notifier).login(email: 'a@b.c', password: 'bad', rememberMe: true),
      throwsA(isA<ApiException>().having((e) => e.message, 'message', 'Invalid email or password.')),
    );
    expect(container.read(sessionProvider).status, AuthStatus.loading);
  });

  test('signing out clears the cookie, the token and the session', () async {
    final adapter = FakeAdapter((o) => o.path.endsWith('/logout') ? envelope(null) : envelope(sessionBody('t5')));
    final store = MemoryKeyValueStore()..values[StoreKeys.refreshCookie] = 'c';
    final container = containerWith(adapter, store);
    final session = container.read(sessionProvider.notifier);
    await session.bootstrap();

    await session.logout();

    expect(container.read(sessionProvider).status, AuthStatus.signedOut);
    expect(store.values.containsKey(StoreKeys.refreshCookie), isFalse);
    expect(container.read(apiProvider).accessToken, isNull);
  });

  test('signing out works even when the server cannot be reached', () async {
    final adapter = FakeAdapter((o) => o.path.endsWith('/logout') ? throw DioException(requestOptions: o, type: DioExceptionType.connectionError) : envelope(sessionBody('t6')));
    final store = MemoryKeyValueStore()..values[StoreKeys.refreshCookie] = 'c';
    final container = containerWith(adapter, store);
    final session = container.read(sessionProvider.notifier);
    await session.bootstrap();
    await session.logout();
    expect(container.read(sessionProvider).status, AuthStatus.signedOut);
    expect(store.values.containsKey(StoreKeys.refreshCookie), isFalse);
  });

  test('an expired session that cannot be renewed returns to sign-in', () async {
    var refreshCalls = 0;
    final adapter = FakeAdapter((o) => o.path.endsWith("/refresh") && refreshCalls++ == 0 ? envelope(sessionBody("t7")) : problem(401, "expired"));
    final store = MemoryKeyValueStore()..values[StoreKeys.refreshCookie] = 'c';
    final container = containerWith(adapter, store);
    await container.read(sessionProvider.notifier).bootstrap();
    expect(container.read(sessionProvider).status, AuthStatus.signedIn);

    await expectLater(container.read(apiProvider).get('/api/v1/dashboard'), throwsA(isA<ApiException>()));
    expect(container.read(sessionProvider).status, AuthStatus.signedOut);
  });
}

