import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'me.dart';
import 'realtime.dart';
import 'session.dart';

/// The app's root navigator and snack-bar host, so a change of access can close screens and say why from outside the widget tree.
final rootNavigatorKey = GlobalKey<NavigatorState>();
final rootMessengerKey = GlobalKey<ScaffoldMessengerState>();

/// Bumped by every pushed "notification" event, so the notification feed can be asked again at once.
class NotificationTick extends Notifier<int> {
  @override
  int build() => 0;

  void bump() => state++;
}

final notificationTickProvider = NotifierProvider<NotificationTick, int>(NotificationTick.new);

/// Keeps the permissions in memory in line with the server while the app is open:
/// - the API's live connection says "access-changed" (an administrator changed this person's roles, a role they hold, or the
///   workspace's modules, plan or subscription): the session is renewed and /me and the menu are loaded again;
/// - a 401 was answered by renewing the session (the server ends old tokens when access is narrowed), a call got 403 or 402, the
///   connection came back after a drop, or the app returned to the foreground: the same reload.
/// A workspace that was just suspended or cancelled closes every screen, so the home shell can say why.
/// When something was taken away, every open screen is closed back to the home shell with a short message, so nobody is left on a
/// screen they can no longer use. Buttons and menus follow by themselves: they watch [meOrEmptyProvider] and [menuProvider].
class AccessSync {
  AccessSync(this._ref);

  final Ref _ref;
  RealtimeConnection? _connection;
  Timer? _timer;
  bool _renewNext = false;
  DateTime _lastForbidden = DateTime.fromMillisecondsSinceEpoch(0);

  /// Asks for a reload; several asks within a short time make one. [renew]: get a new access token first.
  void reloadSoon({bool renew = true}) {
    _renewNext = _renewNext || renew;
    _timer?.cancel();
    _timer = Timer(const Duration(milliseconds: 300), () {
      final r = _renewNext;
      _renewNext = false;
      unawaited(_reload(renew: r));
    });
  }

  /// A 403 may mean the permissions in memory are stale; check at most every 10 seconds, so a screen that retries does not flood the API.
  void forbidden() {
    final now = DateTime.now();
    if (now.difference(_lastForbidden) < const Duration(seconds: 10)) return;
    _lastForbidden = now;
    reloadSoon();
  }

  Future<void> _reload({required bool renew}) async {
    if (_ref.read(sessionProvider).status != AuthStatus.signedIn) return;
    final before = _ref.read(meProvider).value?.permissions;
    final beforeAccess = _ref.read(meProvider).value?.access;
    if (renew && await _ref.read(apiProvider).refresh() == null) {
      _ref.read(sessionProvider.notifier).expired();
      return;
    }
    _ref.invalidate(meProvider);
    _ref.invalidate(menuProvider);
    final Me after;
    try {
      after = await _ref.read(meProvider.future);
    } on Object {
      return; // offline: the next call or resume tries again
    }
    if (before != null && !after.permissions.contains('*') && before.any((p) => !after.permissions.contains(p))) {
      rootNavigatorKey.currentState?.popUntil((route) => route.isFirst);
      rootMessengerKey.currentState?.showSnackBar(
        const SnackBar(content: Text('Your access has changed. Some screens and actions are no longer available.')),
      );
    }
    // Suspended or cancelled just now: every screen closes, so the home shell shows why (see WorkspaceBlockedView).
    if (after.access == SubscriptionAccess.blocked && beforeAccess != SubscriptionAccess.blocked) {
      rootNavigatorKey.currentState?.popUntil((route) => route.isFirst);
    }
  }

  void _onEvent(HubEvent e) {
    if (e.target == 'notification') {
      _ref.read(notificationTickProvider.notifier).bump();
      return;
    }
    if (e.target != 'access-changed') return;
    // The event is per person; a change in another workspace of theirs does not touch this session.
    final arg = e.argument;
    final org = arg is Map ? arg['orgId']?.toString() : null;
    final current = _ref.read(sessionProvider).workspace?.id;
    if (org != null && current != null && org.toLowerCase() != current.toLowerCase()) return;
    reloadSoon();
  }

  void connect() {
    if (_connection != null) return;
    final api = _ref.read(apiProvider);
    final connection = RealtimeConnection(
      baseUrl: api.baseUrl,
      token: () => api.accessToken,
      onEvent: _onEvent,
      onReconnected: () => reloadSoon(renew: false),
    );
    _connection = connection;
    unawaited(connection.start());
  }

  void disconnect() {
    final connection = _connection;
    _connection = null;
    if (connection != null) unawaited(connection.stop());
  }

  void dispose() {
    _timer?.cancel();
    disconnect();
  }
}

/// Read once at start-up (ErpApp): connects while signed in and wires the API client's renew and 403 callbacks.
final accessSyncProvider = Provider<AccessSync>((ref) {
  final sync = AccessSync(ref);
  final api = ref.read(apiProvider);
  api.onSessionRenewed = () => sync.reloadSoon(renew: false);
  api.onForbidden = sync.forbidden;
  api.onPaymentRequired = sync.forbidden;
  ref.listen<SessionState>(sessionProvider, (previous, next) {
    if (next.status == AuthStatus.signedIn) {
      sync.connect();
    } else {
      sync.disconnect();
    }
  }, fireImmediately: true);
  ref.onDispose(sync.dispose);
  return sync;
});
