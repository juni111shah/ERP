import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Where the app keeps the few things that must survive a restart: the refresh cookie, the server address and the last e-mail.
abstract class KeyValueStore {
  Future<String?> read(String key);
  Future<void> write(String key, String value);
  Future<void> delete(String key);
}

/// Keys used with [KeyValueStore].
class StoreKeys {
  static const refreshCookie = 'refresh_cookie';
  static const serverUrl = 'server_url';
  static const lastEmail = 'last_email';
}

/// Backed by the platform keystore (Android Keystore / iOS Keychain). The refresh cookie is as sensitive as a password.
class SecureKeyValueStore implements KeyValueStore {
  SecureKeyValueStore([FlutterSecureStorage? storage]) : _storage = storage ?? const FlutterSecureStorage();

  final FlutterSecureStorage _storage;

  @override
  Future<String?> read(String key) => _storage.read(key: key);

  @override
  Future<void> write(String key, String value) => _storage.write(key: key, value: value);

  @override
  Future<void> delete(String key) => _storage.delete(key: key);
}

/// For tests.
class MemoryKeyValueStore implements KeyValueStore {
  final Map<String, String> values = {};

  @override
  Future<String?> read(String key) async => values[key];

  @override
  Future<void> write(String key, String value) async => values[key] = value;

  @override
  Future<void> delete(String key) async => values.remove(key);
}
