import 'package:dio/dio.dart' show CancelToken;
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../../core/me.dart';
import '../../core/session.dart';
import 'assistant_stream.dart';

class AssistantConversationRow {
  const AssistantConversationRow({required this.id, required this.title, required this.lastMessageAt});

  final String id;
  final String title;
  final String lastMessageAt;

  factory AssistantConversationRow.fromJson(Map<String, dynamic> json) => AssistantConversationRow(
        id: json['id'].toString(),
        title: (json['title'] ?? '').toString(),
        lastMessageAt: (json['lastMessageAt'] ?? '').toString(),
      );
}

/// One entry of the chat: what the person wrote, what the assistant wrote, or a change waiting for the person.
sealed class AssistantItem {
  const AssistantItem();
}

class AssistantUserItem extends AssistantItem {
  const AssistantUserItem(this.text);
  final String text;
}

class AssistantReplyItem extends AssistantItem {
  const AssistantReplyItem(this.text);
  final String text;
}

class AssistantCardItem extends AssistantItem {
  const AssistantCardItem(this.card, {this.busy = false});
  final AssistantActionCard card;
  final bool busy;
}

class AssistantConversationDetail {
  const AssistantConversationDetail({required this.id, required this.items});

  final String id;

  /// Messages and cards together, oldest first.
  final List<AssistantItem> items;

  factory AssistantConversationDetail.fromJson(Map<String, dynamic> json) {
    final timed = <(String, AssistantItem)>[
      for (final m in (json['messages'] as List<dynamic>? ?? const []).cast<Map<String, dynamic>>())
        ((m['at'] ?? '').toString(), m['fromUser'] == true ? AssistantUserItem((m['text'] ?? '').toString()) : AssistantReplyItem((m['text'] ?? '').toString())),
      for (final a in (json['actions'] as List<dynamic>? ?? const []).cast<Map<String, dynamic>>())
        ((a['createdAt'] ?? '').toString(), AssistantCardItem(AssistantActionCard.fromJson(a))),
    ];
    final order = [for (var i = 0; i < timed.length; i++) i];
    // Times are compared as times: the server trims trailing zeros, so their text does not sort. The index keeps equal times in order.
    order.sort((x, y) {
      final byTime = _time(timed[x].$1).compareTo(_time(timed[y].$1));
      return byTime != 0 ? byTime : x.compareTo(y);
    });
    return AssistantConversationDetail(id: json['id'].toString(), items: [for (final i in order) timed[i].$2]);
  }

  static DateTime _time(String at) => DateTime.tryParse(at) ?? DateTime.fromMillisecondsSinceEpoch(0);
}

/// The workspace assistant module (`/api/v1/m/assistant`).
class AssistantApi {
  const AssistantApi(this._api);

  static const _base = '/api/v1/m/assistant';

  final ApiClient _api;

  Future<bool> status() async {
    final data = await _api.get('$_base/status');
    return data is Map && data['available'] == true;
  }

  Future<List<AssistantConversationRow>> conversations() async {
    final data = await _api.get('$_base/conversations');
    final rows = data is Map ? data['items'] : data;
    return [for (final r in (rows as List<dynamic>).cast<Map<String, dynamic>>()) AssistantConversationRow.fromJson(r)];
  }

  Future<AssistantConversationDetail> conversation(String id) async =>
      AssistantConversationDetail.fromJson(await _api.get('$_base/conversations/$id') as Map<String, dynamic>);

  Future<void> deleteConversation(String id) => _api.delete('$_base/conversations/$id');

  /// The reply as it is written. Only [cancelToken] closes the connection, which also stops the reply at the AI service; cancelling the
  /// stream alone would leave it running. A failure before the first event throws [ApiException].
  Stream<AssistantEvent> ask({required String? conversationId, required String text, required CancelToken cancelToken}) async* {
    final parser = AssistantStreamParser();
    // The phone has no page address to give, so the server's optional `page` stays empty.
    final bytes = await _api.postStream('$_base/messages', body: {'conversationId': conversationId, 'text': text, 'page': null}, cancelToken: cancelToken);
    await for (final chunk in bytes) {
      for (final event in parser.add(chunk)) {
        yield event;
      }
    }
  }

  /// The key makes a double tap or a retry harmless: the server runs the change once and repeats its answer.
  Future<AssistantActionCard> confirm(String id) async => AssistantActionCard.fromJson(
      await _api.post('$_base/actions/$id/confirm', body: const <String, dynamic>{}, headers: {'Idempotency-Key': 'assistant-action-$id'}) as Map<String, dynamic>);

  Future<AssistantActionCard> cancel(String id) async =>
      AssistantActionCard.fromJson(await _api.post('$_base/actions/$id/cancel', body: const <String, dynamic>{}) as Map<String, dynamic>);
}

final assistantApiProvider = Provider<AssistantApi>((ref) => AssistantApi(ref.watch(apiProvider)));

/// The person may use the assistant here: their permission, and the module on for this workspace.
bool assistantAllowed(Me me) => me.hasModule('assistant') && me.can('assistant.chats.use');

/// Whether to offer the assistant. Asked only of people who could use it, so nobody else causes a "module disabled" answer. It runs again
/// after a workspace switch, and any failure simply hides it.
final assistantAvailableProvider = FutureProvider<bool>((ref) async {
  ref.watch(sessionProvider.select((s) => s.workspace?.id));
  if (!assistantAllowed(ref.watch(meOrEmptyProvider))) return false;
  try {
    return await ref.watch(assistantApiProvider).status();
  } on ApiException {
    return false;
  }
});
