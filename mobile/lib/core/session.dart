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

/// Older API answer: the account uses two-factor and the code goes in with the password (no challenge token).
class NeedsTwoFactor extends LoginOutcome {
  const NeedsTwoFactor();
}

/// First sign-in on an account without an authenticator: set one up with [challengeToken] before a session is issued.
class NeedsTwoFactorSetup extends LoginOutcome {
  const NeedsTwoFactorSetup(this.challengeToken);

  final String challengeToken;
}

/// Returning user with two-factor: confirm the sign-in with a code and [challengeToken].
class NeedsTwoFactorCode extends LoginOutcome {
  const NeedsTwoFactorCode(this.challengeToken);

  final String challengeToken;
}

/// A first-sign-in setup was confirmed: show the one-time [recoveryCodes], then call [SessionController.complete] with [session].
class TwoFactorEnabled extends LoginOutcome {
  const TwoFactorEnabled(this.recoveryCodes, this.session, {this.workspaceChoice});

  final List<String> recoveryCodes;
  final SessionInfo? session;
  final NeedsWorkspace? workspaceChoice;
}

/// What the authenticator app needs: the otpauth:// link as a QR image (PNG data URI) and the key for typing in by hand.
class TwoFactorSetupInfo {
  const TwoFactorSetupInfo({required this.otpAuthUri, required this.secret, required this.qrCodeDataUri, required this.issuer, required this.account});

  factory TwoFactorSetupInfo.fromJson(Map<String, dynamic> json) => TwoFactorSetupInfo(
        otpAuthUri: json['otpAuthUri']?.toString() ?? '',
        secret: json['secret']?.toString() ?? '',
        qrCodeDataUri: json['qrCodeDataUri']?.toString() ?? '',
        issuer: json['issuer']?.toString() ?? '',
        account: json['account']?.toString() ?? '',
      );

  final String otpAuthUri;
  final String secret;
  final String qrCodeDataUri;
  final String issuer;
  final String account;
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
    final challenge = data['challengeToken']?.toString();
    if (data['twoFactorSetupRequired'] == true && challenge != null) return NeedsTwoFactorSetup(challenge);
    if ((data['twoFactorRequired'] == true || data['requiresTwoFactor'] == true) && challenge != null) return NeedsTwoFactorCode(challenge);
    if (data['requiresTwoFactor'] == true) return const NeedsTwoFactor();
    return _signedIn(data);
  }

  /// First sign-in: creates the authenticator key for the challenge and returns the QR code and the key.
  Future<TwoFactorSetupInfo> beginTwoFactorSetup(String challengeToken) async {
    final data = await _api.postAnonymous('/api/v1/auth/login/2fa/setup', body: {'challengeToken': challengeToken});
    return TwoFactorSetupInfo.fromJson(data as Map<String, dynamic>);
  }

  /// Confirms a sign-in challenge with a code. After a first-time setup the session is held back in [TwoFactorEnabled] so the
  /// recovery codes can be shown first; otherwise the session starts (or a workspace choice comes back).
  Future<LoginOutcome> verifyTwoFactor(String challengeToken, String code) async {
    final data = await _api.postAnonymous('/api/v1/auth/login/2fa/verify', body: {'challengeToken': challengeToken, 'code': code.trim()})
        as Map<String, dynamic>;
    final codes = [for (final c in (data['recoveryCodes'] as List<dynamic>? ?? const [])) c.toString()];
    if (codes.isNotEmpty) {
      return data['requiresWorkspaceSelection'] == true
          ? TwoFactorEnabled(codes, null, workspaceChoice: _workspaceChoice(data))
          : TwoFactorEnabled(codes, SessionInfo.fromJson(data));
    }
    return _signedIn(data);
  }

  /// Starts the session held back by [TwoFactorEnabled].
  void complete(SessionInfo session) => _start(session);

  LoginOutcome _signedIn(Map<String, dynamic> data) {
    if (data['requiresWorkspaceSelection'] == true) return _workspaceChoice(data);
    _start(SessionInfo.fromJson(data));
    return const LoggedIn();
  }

  NeedsWorkspace _workspaceChoice(Map<String, dynamic> data) => NeedsWorkspace(
        data['selectionToken'].toString(),
        [for (final w in (data['workspaces'] as List<dynamic>? ?? const [])) Workspace.fromJson(w as Map<String, dynamic>)],
      );

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
