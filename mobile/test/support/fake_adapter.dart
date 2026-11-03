import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';

typedef FakeHandler = ResponseBody Function(RequestOptions options);

/// A scripted server for tests: every request is answered by [handler] and recorded in [requests].
class FakeAdapter implements HttpClientAdapter {
  FakeAdapter(this.handler);

  final FakeHandler handler;
  final List<RequestOptions> requests = [];

  @override
  Future<ResponseBody> fetch(RequestOptions options, Stream<Uint8List>? requestStream, Future<void>? cancelFuture) async {
    requests.add(options);
    return handler(options);
  }

  @override
  void close({bool force = false}) {}
}

/// A JSON answer wrapped the way the API does: {success, statusCode, data}.
ResponseBody envelope(Object? data, {int status = 200, Map<String, List<String>> headers = const {}}) => ResponseBody.fromString(
      jsonEncode({'success': status < 400, 'statusCode': status, 'message': null, 'data': data}),
      status,
      headers: {
        Headers.contentTypeHeader: ['application/json'],
        ...headers,
      },
    );

ResponseBody problem(int status, String detail) => ResponseBody.fromString(
      jsonEncode({'success': false, 'statusCode': status, 'message': detail, 'detail': detail}),
      status,
      headers: {
        Headers.contentTypeHeader: ['application/json'],
      },
    );

Map<String, dynamic> sessionBody(String token) => {
      'accessToken': token,
      'tokenType': 'Bearer',
      'expiresIn': 900,
      'workspace': {'id': 'w1', 'code': 'ACME', 'name': 'Acme Trading', 'industry': 'trading'},
      'workspaces': [
        {'id': 'w1', 'code': 'ACME', 'name': 'Acme Trading', 'industry': 'trading'},
        {'id': 'w2', 'code': 'BETA', 'name': 'Beta Properties', 'industry': 'realestate'},
      ],
      'persistent': true,
    };
