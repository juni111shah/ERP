import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'api_client.dart';
import 'key_value_store.dart';
import 'models.dart';

/// The address of the ERP API for a phone. The Android emulator reaches the PC through 10.0.2.2; a real phone needs the PC's address or the
/// staging/production URL, which the user can enter on the sign-in screen.
String defaultServerUrl() => defaultTargetPlatform == TargetPlatform.android ? 'http://10.0.2.2:5080' : 'http://localhost:5080';

final storeProvider = Provider<KeyValueStore>((ref) => SecureKeyValueStore());

/// Overridden in main() with the address saved on the last run.
final initialServerUrlProvider = Provider<String>((ref) => defaultServerUrl());

final apiProvider = Provider<ApiClient>((ref) {
  final client = ApiClient(store: ref.watch(storeProvider), baseUrl: ref.watch(initialServerUrlProvider));
  client.onSessionExpired = () => ref.read(sessionProvider.notifier).expired();
  return client;
});

class ServerUrlController extends Notifier<String> {
  @override
  String build() => ref.read(initialServerUrlProvider);

  Future<void> set(String url) async {
    final cleaned = url.trim();
    if (cleaned.isEmpty) return;
    ref.read(apiProvider).baseUrl = cleaned;
    await ref.read(storeProvider).write(StoreKeys.serverUrl, cleaned);
    state = cleaned;
  }
}

final serverUrlProvider = NotifierProvider<ServerUrlController, String>(ServerUrlController.new);

enum AuthStatus { loading, signedOut, signedIn }

class SessionState {
  const SessionState(this.status, [this.info]);

  final AuthStatus status;
  final SessionInfo? info;

  Workspace? get workspace => info?.workspace;
}

/// What a sign-in attempt needs next.
sealed class LoginOutcome {
  const LoginOutcome();
}

class LoggedIn extends LoginOutcome {
  const LoggedIn();
}

class NeedsTwoFactor extends LoginOutcome {
  const NeedsTwoFactor();
}

class NeedsWorkspace extends LoginOutcome {
  const NeedsWorkspace(this.selectionToken, this.workspaces);

  final String selectionToken;
  final List<Workspace> workspaces;
}

class SessionController extends Notifier<SessionState> {
  ApiClient get _api => ref.read(apiProvider);

  @override
  SessionState build() => const SessionState(AuthStatus.loading);

  /// On app start: turn a stored refresh cookie back into a session, or fall back to the sign-in screen.
  Future<void> bootstrap() async {
    final session = await _api.refresh();
    state = session == null ? const SessionState(AuthStatus.signedOut) : SessionState(AuthStatus.signedIn, session);
  }

  Future<LoginOutcome> login({required String email, required String password, required bool rememberMe, String? twoFactorCode}) async {
    final data = await _api.postAnonymous('/api/v1/auth/login', body: {
      'email': email.trim(),
      'password': password,
      'rememberMe': rememberMe,
      if (twoFactorCode != null && twoFactorCode.isNotEmpty) 'twoFactorCode': twoFactorCode.trim(),
    }) as Map<String, dynamic>;
    await ref.read(storeProvider).write(StoreKeys.lastEmail, email.trim());
    if (data['requiresTwoFactor'] == true) return const NeedsTwoFactor();
    if (data['requiresWorkspaceSelection'] == true) {
      return NeedsWorkspace(
        data['selectionToken'].toString(),
        [for (final w in (data['workspaces'] as List<dynamic>? ?? const [])) Workspace.fromJson(w as Map<String, dynamic>)],
      );
    }
    _start(SessionInfo.fromJson(data));
    return const LoggedIn();
  }

  Future<void> selectWorkspace(String selectionToken, String workspaceId) async {
    final data = await _api.postAnonymous('/api/v1/auth/select-workspace', body: {'selectionToken': selectionToken, 'workspaceId': workspaceId});
    _start(SessionInfo.fromJson(data as Map<String, dynamic>));
  }

  Future<void> switchWorkspace(String workspaceId) async {
    final data = await _api.post('/api/v1/auth/switch-workspace', body: {'workspaceId': workspaceId});
    _start(SessionInfo.fromJson(data as Map<String, dynamic>));
  }

  Future<void> logout() async {
    try {
      await _api.post('/api/v1/auth/logout');
    } on ApiException {
      // Signing out locally is enough if the server cannot be reached.
    }
    await ref.read(storeProvider).delete(StoreKeys.refreshCookie);
    _api.accessToken = null;
    state = const SessionState(AuthStatus.signedOut);
  }

  /// The API client could not renew the session.
  void expired() {
    _api.accessToken = null;
    state = const SessionState(AuthStatus.signedOut);
  }

  void _start(SessionInfo info) {
    _api.accessToken = info.accessToken;
    state = SessionState(AuthStatus.signedIn, info);
  }
}

final sessionProvider = NotifierProvider<SessionController, SessionState>(SessionController.new);

/// Set while the user is choosing a workspace after signing in.
class PendingWorkspaceSelection {
  const PendingWorkspaceSelection(this.selectionToken, this.workspaces);

  final String selectionToken;
  final List<Workspace> workspaces;
}

class PendingWorkspaceController extends Notifier<PendingWorkspaceSelection?> {
  @override
  PendingWorkspaceSelection? build() => null;

  void set(PendingWorkspaceSelection? value) => state = value;
}

final pendingWorkspaceProvider = NotifierProvider<PendingWorkspaceController, PendingWorkspaceSelection?>(PendingWorkspaceController.new);
