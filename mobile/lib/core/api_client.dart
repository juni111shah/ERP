import 'package:dio/dio.dart';

import 'key_value_store.dart';
import 'models.dart';

/// A failed call, with a message that is safe to show to the user.
class ApiException implements Exception {
  ApiException(this.status, this.message, [this.fieldErrors = const {}]);

  /// HTTP status, or 0 when the server could not be reached.
  final int status;
  final String message;
  final Map<String, List<String>> fieldErrors;

  bool get isUnauthorized => status == 401;

  @override
  String toString() => 'ApiException($status): $message';

  factory ApiException.fromBody(int status, dynamic body) {
    var message = 'The request failed ($status).';
    final errors = <String, List<String>>{};
    if (body is Map) {
      final detail = body['detail'] ?? body['message'] ?? body['title'];
      if (detail is String && detail.isNotEmpty) message = detail;
      final raw = body['errors'];
      if (raw is Map) {
        raw.forEach((key, value) {
          errors[key.toString()] = value is List ? [for (final v in value) v.toString()] : [value.toString()];
        });
        if (errors.isNotEmpty && (message == 'Validation failed' || message.startsWith('The request failed'))) {
          message = errors.values.expand((e) => e).join('\n');
        }
      }
    }
    return ApiException(status, message, errors);
  }
}

/// Talks to the ERP API: bearer token on every call, one automatic refresh when a call comes back 401, and the
/// refresh cookie kept in the secure store (the API hands it out as an HttpOnly cookie, which a phone does not manage for us).
class ApiClient {
  ApiClient({required this.store, required String baseUrl, Dio? dio}) : _dio = dio ?? Dio() {
    _dio.options
      ..baseUrl = _clean(baseUrl)
      ..connectTimeout = const Duration(seconds: 15)
      ..receiveTimeout = const Duration(seconds: 40)
      ..validateStatus = (_) => true; // statuses are handled here, not thrown by Dio
  }

  static const refreshCookieName = 'erp_rt';
  static const clientHeader = 'X-ERP-Client';
  static const clientName = 'mobile';

  final KeyValueStore store;
  final Dio _dio;

  /// Held in memory only.
  String? accessToken;

  /// Called when the session cannot be renewed any more (the user has to sign in again).
  void Function()? onSessionExpired;

  Future<SessionInfo?>? _refreshing;

  String get baseUrl => _dio.options.baseUrl;
  set baseUrl(String value) => _dio.options.baseUrl = _clean(value);

  static String _clean(String url) {
    var u = url.trim();
    while (u.endsWith('/')) {
      u = u.substring(0, u.length - 1);
    }
    return u;
  }

  Future<dynamic> get(String path, {Map<String, dynamic>? query}) => _send('GET', path, query: query);

  Future<dynamic> post(String path, {Object? body}) => _send('POST', path, body: body);

  Future<dynamic> put(String path, {Object? body}) => _send('PUT', path, body: body);

  Future<dynamic> delete(String path) => _send('DELETE', path);

  /// Calls that happen before there is a session (sign-in, workspace selection): no bearer token, no refresh attempt.
  Future<dynamic> postAnonymous(String path, {Object? body}) => _send('POST', path, body: body, auth: false);

  Future<dynamic> _send(String method, String path, {Map<String, dynamic>? query, Object? body, bool auth = true, bool retry = true}) async {
    final Response<dynamic> res;
    try {
      res = await _dio.request<dynamic>(
        path,
        data: body,
        queryParameters: query,
        options: Options(method: method, headers: {
          clientHeader: clientName,
          if (auth && accessToken != null) 'Authorization': 'Bearer $accessToken',
        }),
      );
    } on DioException catch (e) {
      throw ApiException(0, _networkMessage(e));
    }
    await _captureCookie(res);
    if (res.statusCode == 401 && auth && retry) {
      if (await refresh() != null) return _send(method, path, query: query, body: body, auth: auth, retry: false);
      onSessionExpired?.call();
    }
    return unwrap(res.statusCode ?? 0, res.data);
  }

  /// Exchanges the stored refresh cookie for a new access token. Calls made at the same time share one refresh.
  Future<SessionInfo?> refresh() => _refreshing ??= _refresh().whenComplete(() => _refreshing = null);

  Future<SessionInfo?> _refresh() async {
    final cookie = await store.read(StoreKeys.refreshCookie);
    if (cookie == null || cookie.isEmpty) return null;
    final Response<dynamic> res;
    try {
      res = await _dio.request<dynamic>('/api/v1/auth/refresh',
          options: Options(method: 'POST', headers: {clientHeader: clientName, 'Cookie': '$refreshCookieName=$cookie'}));
    } on DioException {
      return null; // offline: keep the cookie, the user can try again later
    }
    await _captureCookie(res);
    if (res.statusCode == 401 || res.statusCode == 403) {
      await store.delete(StoreKeys.refreshCookie);
      accessToken = null;
      return null;
    }
    if ((res.statusCode ?? 0) >= 300) return null;
    final session = SessionInfo.fromJson(unwrap(res.statusCode ?? 200, res.data) as Map<String, dynamic>);
    accessToken = session.accessToken;
    return session;
  }

  Future<void> _captureCookie(Response<dynamic> res) async {
    final value = parseRefreshCookie(res.headers.map['set-cookie']);
    if (value == null) return;
    if (value.isEmpty) {
      await store.delete(StoreKeys.refreshCookie); // the server cleared it (sign out)
    } else {
      await store.write(StoreKeys.refreshCookie, value);
    }
  }

  /// The value of the refresh cookie in a list of Set-Cookie headers: null when absent, empty when the server is clearing it.
  static String? parseRefreshCookie(List<String>? setCookies) {
    if (setCookies == null) return null;
    for (final header in setCookies) {
      final trimmed = header.trim();
      if (!trimmed.startsWith('$refreshCookieName=')) continue;
      final end = trimmed.indexOf(';');
      return trimmed.substring(refreshCookieName.length + 1, end < 0 ? trimmed.length : end);
    }
    return null;
  }

  /// The API wraps every answer as {success, statusCode, message, data}; the payload is `data`. A list answer carries its paging in a
  /// sibling `meta` ({page, pageSize, total, ...}), which is folded back into {items, total, page, pageSize}. Failures become [ApiException].
  static dynamic unwrap(int status, dynamic body) {
    if (status >= 200 && status < 300) {
      if (body is Map && body.containsKey('success') && body.containsKey('data')) {
        final data = body['data'];
        final meta = body['meta'];
        if (data is List && meta is Map) {
          return {'items': data, 'total': meta['total'], 'page': meta['page'], 'pageSize': meta['pageSize']};
        }
        return data;
      }
      return body;
    }
    throw ApiException.fromBody(status, body);
  }

  static String _networkMessage(DioException e) => switch (e.type) {
        DioExceptionType.connectionTimeout || DioExceptionType.receiveTimeout || DioExceptionType.sendTimeout =>
          'The server took too long to answer. Check the connection and try again.',
        DioExceptionType.connectionError => 'Cannot reach the server. Check the server address in Settings and your connection.',
        _ => 'Something went wrong talking to the server.',
      };
}
