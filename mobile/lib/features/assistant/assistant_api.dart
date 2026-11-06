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
  const AssistantReplyItem(this.text, {this.id, this.helpful});
  final String text;

  /// The saved reply, known once its stream has ended. Without one there is nothing to rate.
  final String? id;

  /// The person's thumbs up or down, null until they choose.
  final bool? helpful;
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
        ((m['at'] ?? '').toString(), m['fromUser'] == true ? AssistantUserItem((m['text'] ?? '').toString()) : AssistantReplyItem((m['text'] ?? '').toString(), id: m['id']?.toString(), helpful: m['helpful'] as bool?)),
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

/// How much of the plan's messages the workspace has used. [monthlyAllowance] is null when the plan has no limit.
class AssistantUsage {
  const AssistantUsage({required this.messagesThisMonth, required this.monthlyAllowance, required this.peopleThisMonth, this.helpful = 0, this.notHelpful = 0});

  final int messagesThisMonth;
  final int? monthlyAllowance;
  final int peopleThisMonth;

  /// Replies rated this month. Counts only, never words.
  final int helpful, notHelpful;

  factory AssistantUsage.fromJson(Map<String, dynamic> json) => AssistantUsage(
        messagesThisMonth: (json['messagesThisMonth'] as num).toInt(),
        monthlyAllowance: (json['monthlyAllowance'] as num?)?.toInt(),
        peopleThisMonth: (json['peopleThisMonth'] as num?)?.toInt() ?? 0,
        helpful: (json['helpful'] as num?)?.toInt() ?? 0,
        notHelpful: (json['notHelpful'] as num?)?.toInt() ?? 0,
      );
}

/// The workspace assistant module (`/api/v1/m/assistant`).
class AssistantApi {
  const AssistantApi(this._api, {this.entityId});

  static const _base = '/api/v1/m/assistant';

  /// The header the web's entity interceptor adds. Here it goes on this module's calls only.
  static const entityHeader = 'X-Entity-Id';

  final ApiClient _api;

  /// The legal entity to work in right now. Asked on every call, so a new choice applies to the next one.
  final String? Function()? entityId;

  Map<String, String> get _scope {
    final id = entityId?.call();
    return {entityHeader: ?id};
  }

  Future<bool> status() async {
    final data = await _api.get('$_base/status', headers: _scope);
    return data is Map && data['available'] == true;
  }

  Future<AssistantUsage> usage() async => AssistantUsage.fromJson(await _api.get('$_base/usage', headers: _scope) as Map<String, dynamic>);

  Future<List<AssistantConversationRow>> conversations() async {
    final data = await _api.get('$_base/conversations', headers: _scope);
    final rows = data is Map ? data['items'] : data;
    return [for (final r in (rows as List<dynamic>).cast<Map<String, dynamic>>()) AssistantConversationRow.fromJson(r)];
  }

  Future<AssistantConversationDetail> conversation(String id) async =>
      AssistantConversationDetail.fromJson(await _api.get('$_base/conversations/$id', headers: _scope) as Map<String, dynamic>);

  Future<void> deleteConversation(String id) => _api.delete('$_base/conversations/$id', headers: _scope);

  /// The reply as it is written. Only [cancelToken] closes the connection, which also stops the reply at the AI service; cancelling the
  /// stream alone would leave it running. A failure before the first event throws [ApiException].
  Stream<AssistantEvent> ask({required String? conversationId, required String text, String language = 'en', required CancelToken cancelToken}) async* {
    final parser = AssistantStreamParser();
    // The phone has no page address to give, so the server's optional `page` stays empty.
    final bytes = await _api.postStream('$_base/messages', body: {'conversationId': conversationId, 'text': text, 'page': null, 'language': language}, cancelToken: cancelToken, headers: _scope);
    await for (final chunk in bytes) {
      for (final event in parser.add(chunk)) {
        yield event;
      }
    }
  }

  /// The key makes a double tap or a retry harmless: the server runs the change once and repeats its answer.
  Future<AssistantActionCard> confirm(String id) async => AssistantActionCard.fromJson(
      await _api.post('$_base/actions/$id/confirm', body: const <String, dynamic>{}, headers: {'Idempotency-Key': 'assistant-action-$id', ..._scope}) as Map<String, dynamic>);

  Future<AssistantActionCard> cancel(String id) async =>
      AssistantActionCard.fromJson(await _api.post('$_base/actions/$id/cancel', body: const <String, dynamic>{}, headers: _scope) as Map<String, dynamic>);

  /// Null takes the choice back.
  Future<void> feedback(String id, bool? helpful) => _api.post('$_base/messages/$id/feedback', body: {'helpful': helpful}, headers: _scope);
}

/// The legal entity picked on the assistant screen, null for none. Remembered per workspace, and used by the assistant only.
class AssistantEntityController extends Notifier<String?> {
  static String keyFor(String workspaceId) => 'assistant_entity_$workspaceId';

  bool _chosen = false;

  String? get _workspaceId => ref.read(sessionProvider).workspace?.id;

  @override
  String? build() {
    ref.watch(sessionProvider.select((s) => s.workspace?.id));
    _chosen = false;
    Future.microtask(_restore);
    return null;
  }

  Future<void> _restore() async {
    try {
      final workspace = _workspaceId;
      if (workspace == null) return;
      final saved = await ref.read(storeProvider).read(keyFor(workspace));
      if (ref.mounted && !_chosen) state = saved;
    } catch (_) {
      // No stored choice, or storage is unavailable: start with none.
    }
  }

  Future<void> choose(String? id) async {
    _chosen = true;
    state = id;
    final workspace = _workspaceId;
    if (workspace == null) return;
    try {
      final store = ref.read(storeProvider);
      await (id == null ? store.delete(keyFor(workspace)) : store.write(keyFor(workspace), id));
    } catch (_) {
      // Still applied for this run.
    }
  }
}

final assistantEntityProvider = NotifierProvider<AssistantEntityController, String?>(AssistantEntityController.new);

/// The picked legal entity while this person may still use it; the server would answer 403 to any other. Someone with only one needs no
/// header, as the server uses their only one.
final assistantEntityIdProvider = Provider<String?>((ref) {
  final chosen = ref.watch(assistantEntityProvider);
  final entities = ref.watch(meOrEmptyProvider).legalEntities;
  return entities.length > 1 && entities.any((e) => e.id == chosen) ? chosen : null;
});

final assistantApiProvider = Provider<AssistantApi>((ref) => AssistantApi(ref.watch(apiProvider), entityId: () => ref.read(assistantEntityIdProvider)));

/// For people who may read the workspace's usage. Any failure simply hides the line.
final assistantUsageProvider = FutureProvider.autoDispose<AssistantUsage?>((ref) async {
  ref.watch(sessionProvider.select((s) => s.workspace?.id));
  if (!ref.watch(meOrEmptyProvider).can('assistant.usage.read')) return null;
  try {
    return await ref.read(assistantApiProvider).usage();
  } catch (_) {
    return null;
  }
});

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
