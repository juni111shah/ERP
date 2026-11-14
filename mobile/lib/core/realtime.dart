import 'dart:async';
import 'dart:convert';
import 'dart:io';

/// The record separator that ends every SignalR JSON-protocol message.
const signalRSeparator = '\u001e';

/// One event the server invoked on this client ("access-changed", "notification") with its first argument.
class HubEvent {
  const HubEvent(this.target, this.argument);

  final String target;
  final Object? argument;
}

/// The hub address next to the API: http://host:5080 → ws://host:5080/hubs/notifications?access_token=…, https → wss.
Uri hubUri(String baseUrl, String accessToken) {
  final base = Uri.parse(baseUrl);
  return base.replace(
    scheme: base.scheme == 'https' ? 'wss' : 'ws',
    path: '${base.path.endsWith('/') ? base.path.substring(0, base.path.length - 1) : base.path}/hubs/notifications',
    queryParameters: {'access_token': accessToken},
  );
}

/// Splits a frame into SignalR messages and keeps the invocations (type 1). The handshake answer ({}), pings (type 6) and anything that
/// is not JSON are skipped; a close message (type 7) comes back as a `close` event so the caller can reconnect.
List<HubEvent> parseHubFrame(String frame) {
  final events = <HubEvent>[];
  for (final part in frame.split(signalRSeparator)) {
    if (part.trim().isEmpty) continue;
    final Object? message;
    try {
      message = jsonDecode(part);
    } on FormatException {
      continue;
    }
    if (message is! Map<String, dynamic>) continue;
    final type = message['type'];
    if (type == 7) {
      events.add(const HubEvent('close', null));
    } else if (type == 1 && message['target'] is String) {
      final args = message['arguments'];
      events.add(HubEvent(message['target'] as String, args is List && args.isNotEmpty ? args.first : null));
    }
  }
  return events;
}

/// A small client for the API's SignalR hub (JSON protocol over a WebSocket, no negotiation), enough to receive server events.
/// It reconnects with a growing pause after a drop and calls [onReconnected], since events sent while it was away are lost.
class RealtimeConnection {
  RealtimeConnection({required this.baseUrl, required this.token, required this.onEvent, this.onReconnected});

  final String baseUrl;

  /// Read at every (re)connect, so a renewed access token is used.
  final String? Function() token;
  final void Function(HubEvent event) onEvent;
  final void Function()? onReconnected;

  static const _pauses = [Duration(seconds: 1), Duration(seconds: 3), Duration(seconds: 10), Duration(seconds: 30)];

  WebSocket? _socket;
  Timer? _ping;
  Timer? _retry;
  bool _closed = false;
  bool _connectedOnce = false;
  int _failures = 0;

  Future<void> start() async {
    _closed = false;
    await _connect();
  }

  Future<void> _connect() async {
    if (_closed) return;
    final t = token();
    if (t == null || t.isEmpty) return;
    try {
      final socket = await WebSocket.connect(hubUri(baseUrl, t).toString()).timeout(const Duration(seconds: 15));
      if (_closed) {
        await socket.close();
        return;
      }
      _socket = socket;
      socket.add('${jsonEncode({'protocol': 'json', 'version': 1})}$signalRSeparator');
      // The server drops a client that says nothing for 30 seconds.
      _ping = Timer.periodic(const Duration(seconds: 15), (_) => _send({'type': 6}));
      final reconnected = _connectedOnce;
      _connectedOnce = true;
      _failures = 0;
      if (reconnected) onReconnected?.call();
      socket.listen(
        (data) {
          if (data is! String) return;
          for (final e in parseHubFrame(data)) {
            if (e.target == 'close') {
              _drop();
              return;
            }
            onEvent(e);
          }
        },
        onDone: _drop,
        onError: (Object _) => _drop(),
        cancelOnError: true,
      );
    } on Object {
      _scheduleRetry();
    }
  }

  void _send(Map<String, Object?> message) {
    try {
      _socket?.add('${jsonEncode(message)}$signalRSeparator');
    } on Object {
      _drop();
    }
  }

  void _drop() {
    _ping?.cancel();
    _ping = null;
    final socket = _socket;
    _socket = null;
    if (socket != null) unawaited(socket.close().catchError((Object _) => null));
    _scheduleRetry();
  }

  void _scheduleRetry() {
    if (_closed || _retry != null) return;
    final pause = _pauses[_failures < _pauses.length ? _failures : _pauses.length - 1];
    _failures++;
    _retry = Timer(pause, () {
      _retry = null;
      unawaited(_connect());
    });
  }

  Future<void> stop() async {
    _closed = true;
    _retry?.cancel();
    _retry = null;
    _ping?.cancel();
    _ping = null;
    final socket = _socket;
    _socket = null;
    if (socket != null) await socket.close().catchError((Object _) => null);
  }
}
