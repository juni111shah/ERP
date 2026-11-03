import 'package:dio/dio.dart';
import 'package:erp_mobile/core/api_client.dart';
import 'package:erp_mobile/core/key_value_store.dart';
import 'package:erp_mobile/core/models.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/fake_adapter.dart';

ApiClient clientFor(FakeAdapter adapter, MemoryKeyValueStore store) {
  final dio = Dio()..httpClientAdapter = adapter;
  return ApiClient(store: store, baseUrl: 'http://api.test/', dio: dio);
}

void main() {
  group('refresh cookie', () {
    test('is read from a Set-Cookie header', () {
      expect(ApiClient.parseRefreshCookie(['other=1; Path=/', 'erp_rt=abc123; path=/api/v1/auth; httponly']), 'abc123');
    });

    test('is empty when the server clears it', () {
      expect(ApiClient.parseRefreshCookie(['erp_rt=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/api/v1/auth']), '');
    });

    test('is null when absent', () {
      expect(ApiClient.parseRefreshCookie(['theme=dark']), isNull);
      expect(ApiClient.parseRefreshCookie(null), isNull);
    });
  });

  group('unwrap', () {
    test('returns the data of a successful envelope', () {
      expect(ApiClient.unwrap(200, {'success': true, 'data': {'a': 1}}), {'a': 1});
    });

    test('folds the paging meta of a list answer back in, as the real API sends it', () {
      final result = ApiClient.unwrap(200, {
        'meta': {'page': 2, 'pageSize': 25, 'total': 45, 'totalPages': 2, 'hasNext': false, 'hasPrevious': true},
        'success': true,
        'statusCode': 200,
        'data': [
          {'id': 'a'},
          {'id': 'b'}
        ],
      }) as Map<String, dynamic>;
      final paged = Paged.fromJson(result);
      expect(paged.items.length, 2);
      expect((paged.total, paged.page, paged.pageSize), (45, 2, 25));
      expect(paged.hasNext, isFalse);
    });

    test('returns a body that is not wrapped as it is', () {
      expect(ApiClient.unwrap(200, {'accessToken': 't'}), {'accessToken': 't'});
    });

    test('turns a failure into an ApiException with the server message', () {
      expect(
        () => ApiClient.unwrap(409, {'success': false, 'detail': 'Only a draft can be edited.'}),
        throwsA(isA<ApiException>().having((e) => e.status, 'status', 409).having((e) => e.message, 'message', 'Only a draft can be edited.')),
      );
    });

    test('joins field errors into the message for a validation failure', () {
      try {
        ApiClient.unwrap(400, {
          'message': 'Validation failed',
          'errors': {'invoiceDate': ['Enter a valid date']}
        });
        fail('should throw');
      } on ApiException catch (e) {
        expect(e.message, 'Enter a valid date');
        expect(e.fieldErrors['invoiceDate'], ['Enter a valid date']);
      }
    });
  });

  group('requests', () {
    test('send the bearer token and the client header', () async {
      final adapter = FakeAdapter((o) => envelope({'ok': true}));
      final client = clientFor(adapter, MemoryKeyValueStore())..accessToken = 'tok';
      final data = await client.get('/api/v1/me');
      expect(data, {'ok': true});
      final sent = adapter.requests.single;
      expect(sent.uri.toString(), 'http://api.test/api/v1/me');
      expect(sent.headers['Authorization'], 'Bearer tok');
      expect(sent.headers[ApiClient.clientHeader], ApiClient.clientName);
    });

    test('a 401 refreshes once with the stored cookie and retries', () async {
      var dashboardCalls = 0;
      final adapter = FakeAdapter((o) {
        if (o.path == '/api/v1/auth/refresh') {
          return envelope(sessionBody('new-token'), headers: {
            'set-cookie': ['erp_rt=cookie-2; path=/api/v1/auth; httponly']
          });
        }
        dashboardCalls++;
        return o.headers['Authorization'] == 'Bearer new-token' ? envelope({'kpis': []}) : problem(401, 'expired');
      });
      final store = MemoryKeyValueStore()..values[StoreKeys.refreshCookie] = 'cookie-1';
      final client = clientFor(adapter, store)..accessToken = 'old-token';

      final data = await client.get('/api/v1/dashboard');

      expect(data, {'kpis': []});
      expect(dashboardCalls, 2);
      final refresh = adapter.requests.firstWhere((r) => r.path == '/api/v1/auth/refresh');
      expect(refresh.headers['Cookie'], 'erp_rt=cookie-1');
      expect(refresh.headers[ApiClient.clientHeader], ApiClient.clientName);
      expect(store.values[StoreKeys.refreshCookie], 'cookie-2'); // the rotated cookie is kept
      expect(client.accessToken, 'new-token');
    });

    test('calls made at the same time share one refresh', () async {
      var refreshes = 0;
      final adapter = FakeAdapter((o) {
        if (o.path == '/api/v1/auth/refresh') {
          refreshes++;
          return envelope(sessionBody('fresh'));
        }
        return o.headers['Authorization'] == 'Bearer fresh' ? envelope({'ok': true}) : problem(401, 'expired');
      });
      final store = MemoryKeyValueStore()..values[StoreKeys.refreshCookie] = 'c';
      final client = clientFor(adapter, store)..accessToken = 'stale';

      await Future.wait([client.get('/a'), client.get('/b'), client.get('/c')]);

      expect(refreshes, 1);
    });

    test('a failed refresh signs the user out and drops the cookie', () async {
      final adapter = FakeAdapter((o) => problem(401, 'no'));
      final store = MemoryKeyValueStore()..values[StoreKeys.refreshCookie] = 'dead';
      var expired = 0;
      final client = clientFor(adapter, store)
        ..accessToken = 'old'
        ..onSessionExpired = () => expired++;

      await expectLater(client.get('/api/v1/dashboard'), throwsA(isA<ApiException>().having((e) => e.status, 'status', 401)));

      expect(expired, 1);
      expect(store.values.containsKey(StoreKeys.refreshCookie), isFalse);
      expect(client.accessToken, isNull);
    });

    test('with no stored cookie a 401 is final', () async {
      final adapter = FakeAdapter((o) => problem(401, 'no'));
      var expired = 0;
      final client = clientFor(adapter, MemoryKeyValueStore())..onSessionExpired = () => expired++;
      await expectLater(client.get('/x'), throwsA(isA<ApiException>()));
      expect(expired, 1);
      expect(adapter.requests.length, 1);
    });

    test('anonymous calls never refresh', () async {
      final adapter = FakeAdapter((o) => problem(401, 'Invalid email or password.'));
      final store = MemoryKeyValueStore()..values[StoreKeys.refreshCookie] = 'c';
      final client = clientFor(adapter, store);
      await expectLater(client.postAnonymous('/api/v1/auth/login', body: {}), throwsA(isA<ApiException>().having((e) => e.message, 'message', 'Invalid email or password.')));
      expect(adapter.requests.length, 1);
    });

    test('an unreachable server gives a readable message', () async {
      final adapter = FakeAdapter((o) => throw DioException(requestOptions: o, type: DioExceptionType.connectionError));
      final client = clientFor(adapter, MemoryKeyValueStore());
      await expectLater(
        client.get('/x'),
        throwsA(isA<ApiException>().having((e) => e.status, 'status', 0).having((e) => e.message, 'message', contains('Cannot reach the server'))),
      );
    });
  });
}
