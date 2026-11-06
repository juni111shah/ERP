import 'dart:convert';

import '../../core/api_client.dart';

class AssistantActionLine {
  const AssistantActionLine(this.label, this.value);
  final String label;
  final String value;
}

/// Built by the server from what would really happen, never from the model's words.
class AssistantActionCard {
  const AssistantActionCard({required this.id, required this.title, required this.lines, required this.status, this.message, this.link, this.createdAt = '', this.expiresAt});

  final String id;
  final String title;
  final List<AssistantActionLine> lines;

  /// pending, done, cancelled, failed or expired.
  final String status;
  final String? message;
  final String? link;
  final String createdAt;

  /// When the server stops accepting Confirm, if it says. The server still decides; this only saves a tap that could not work.
  final DateTime? expiresAt;

  bool isPastExpiry(DateTime now) => expiresAt != null && now.isAfter(expiresAt!);

  static final _hasZone = RegExp(r'(Z|[+-]\d{2}(:?\d{2})?)$', caseSensitive: false);

  /// The server's times are UTC, even when the text does not say so.
  static DateTime? _instant(Object? raw) {
    if (raw is! String) return null;
    final text = raw.trim();
    return DateTime.tryParse(_hasZone.hasMatch(text) ? text : '${text}Z');
  }

  factory AssistantActionCard.fromJson(Map<String, dynamic> json) {
    final lines = json['lines'];
    return AssistantActionCard(
      id: (json['id'] ?? '').toString(),
      title: (json['title'] ?? '').toString(),
      lines: [
        if (lines is List)
          for (final l in lines)
            if (l is Map) AssistantActionLine((l['label'] ?? '').toString(), (l['value'] ?? '').toString()),
      ],
      status: (json['status'] ?? '').toString(),
      message: json['message']?.toString(),
      link: json['link']?.toString(),
      createdAt: (json['createdAt'] ?? '').toString(),
      expiresAt: _instant(json['expiresAt']),
    );
  }
}

sealed class AssistantEvent {
  const AssistantEvent();
}

class AssistantStatusEvent extends AssistantEvent {
  const AssistantStatusEvent(this.text);
  final String text;
}

class AssistantDeltaEvent extends AssistantEvent {
  const AssistantDeltaEvent(this.text);
  final String text;
}

/// The saved conversation this reply belongs to.
class AssistantConversationEvent extends AssistantEvent {
  const AssistantConversationEvent(this.id);
  final String id;
}

/// A change waiting for the person's tap.
class AssistantActionEvent extends AssistantEvent {
  const AssistantActionEvent(this.card);
  final AssistantActionCard card;
}

class AssistantErrorEvent extends AssistantEvent {
  const AssistantErrorEvent(this.code, this.message);
  final String code;
  final String message;
}

/// Reads server-sent events from bytes that arrive in pieces, which can split an event or even a character anywhere.
class AssistantStreamParser {
  final _decoded = StringBuffer();
  late final _decoder = const Utf8Decoder(allowMalformed: true).startChunkedConversion(StringConversionSink.fromStringSink(_decoded));
  var _buffer = '';

  List<AssistantEvent> add(List<int> bytes) {
    _decoder.add(bytes);
    _buffer = (_buffer + _decoded.toString()).replaceAll('\r\n', '\n');
    _decoded.clear();
    final events = <AssistantEvent>[];
    var end = _buffer.indexOf('\n\n');
    while (end >= 0) {
      final event = _parseBlock(_buffer.substring(0, end));
      if (event != null) events.add(event);
      _buffer = _buffer.substring(end + 2);
      end = _buffer.indexOf('\n\n');
    }
    return events;
  }

  static AssistantEvent? _parseBlock(String block) {
    var name = '';
    var data = '';
    for (final line in block.split('\n')) {
      if (line.startsWith('event:')) {
        name = line.substring(6).trim();
      } else if (line.startsWith('data:')) {
        data += line.substring(5).trim();
      }
    }
    if (name.isEmpty || data.isEmpty) return null; // keep-alive comments have neither
    final Object? body;
    try {
      body = jsonDecode(data);
    } on FormatException {
      return null;
    }
    if (body is! Map<String, dynamic>) return null;
    return switch (name) {
      'status' => AssistantStatusEvent((body['text'] ?? '').toString()),
      'delta' => AssistantDeltaEvent((body['text'] ?? '').toString()),
      'conversation' => AssistantConversationEvent((body['id'] ?? '').toString()),
      'action' => AssistantActionEvent(AssistantActionCard.fromJson(body)),
      'error' => AssistantErrorEvent((body['code'] ?? '').toString(), (body['message'] ?? '').toString()),
      _ => null, // service and done mean nothing to this screen
    };
  }
}

/// What to show for a failed streaming call: a field's message, or the envelope's words. A server failure's detail is for developers.
String assistantErrorMessage(Object error, String fallback) {
  if (error is! ApiException || error.status == 0 || error.status >= 500) return fallback;
  for (final messages in error.fieldErrors.values) {
    if (messages.isNotEmpty) return messages.first;
  }
  return error.message.startsWith('The request failed') ? fallback : error.message;
}
