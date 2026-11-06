import 'dart:async';
import 'dart:ui' as ui;

import 'package:dio/dio.dart' show CancelToken;
import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart' show DateFormat; // intl has its own TextDirection

import '../../core/api_client.dart';
import '../../core/brand.dart';
import '../../core/me.dart';
import '../../core/session.dart';
import '../../kit/data_version.dart';
import '../../kit/registry.dart';
import '../../kit/skeleton.dart';
import '../../theme/app_theme.dart';
import '../../theme/tokens.dart';
import '../dashboard/overview_logic.dart';
import 'assistant_api.dart';
import 'assistant_stream.dart';
import 'assistant_text.dart';

/// The assistant as a screen for the command palette. It is not in `allScreens`: those are always listed, this one only when
/// [assistantAvailableProvider] says so.
final assistantScreen = MobileScreen(
  id: 'assistant',
  title: 'Assistant',
  menuIds: const [],
  icon: Icons.auto_awesome_outlined,
  builder: (_) => const AssistantPage(),
);

/// Copy for the assistant, in English and Arabic (same words as the web panel, same choice of language as `IntroStrings`).
class AssistantWords {
  const AssistantWords._({
    required this.title,
    required this.newChat,
    required this.history,
    required this.noHistory,
    required this.remove,
    required this.notice,
    required this.hello,
    required this.placeholder,
    required this.send,
    required this.stop,
    required this.working,
    required this.failed,
    required this.confirm,
    required this.cancel,
    required this.open,
    required this.cancelled,
    required this.expired,
    required this.check,
    required this.suggestions,
  });

  final String title, newChat, history, noHistory, remove, notice, hello, placeholder, send, stop, working, failed, confirm, cancel, open, cancelled, expired, check;
  final List<String> suggestions;

  static const en = AssistantWords._(
    title: 'Assistant',
    newChat: 'New chat',
    history: 'Past chats',
    noHistory: 'No past chats yet.',
    remove: 'Delete this chat',
    notice: 'AI answers can be wrong, so check anything important. It only uses what you are allowed to see.',
    hello: 'Ask me about your records, or where to find something.',
    placeholder: 'Ask a question',
    send: 'Send',
    stop: 'Stop',
    working: 'Working',
    failed: 'Sorry, something went wrong. Please try again.',
    confirm: 'Confirm',
    cancel: 'Cancel',
    open: 'Open',
    cancelled: 'Cancelled',
    expired: 'Expired',
    check: 'Please check this before confirming.',
    suggestions: ['What needs my attention today?', 'Who owes us the most?', 'Where do I add a new customer?'],
  );

  static const ar = AssistantWords._(
    title: 'المساعد',
    newChat: 'محادثة جديدة',
    history: 'المحادثات السابقة',
    noHistory: 'لا توجد محادثات سابقة بعد.',
    remove: 'حذف هذه المحادثة',
    notice: 'قد تكون إجابات الذكاء الاصطناعي خاطئة، لذا تحقق من أي أمر مهم. لا يستخدم إلا ما يحق لك الاطلاع عليه.',
    hello: 'اسألني عن سجلاتك أو عن مكان أي شيء في النظام.',
    placeholder: 'اطرح سؤالاً',
    send: 'إرسال',
    stop: 'إيقاف',
    working: 'جارٍ العمل',
    failed: 'عذراً، حدث خطأ ما. يرجى المحاولة مرة أخرى.',
    confirm: 'تأكيد',
    cancel: 'إلغاء',
    open: 'فتح',
    cancelled: 'أُلغي',
    expired: 'انتهت صلاحيته',
    check: 'يرجى التحقق من هذا قبل التأكيد.',
    suggestions: ['ما الذي يحتاج إلى انتباهي اليوم؟', 'من أكثر من يدين لنا بالمال؟', 'أين أضيف عميلاً جديداً؟'],
  );

  static AssistantWords of(BuildContext context) =>
      Localizations.maybeLocaleOf(context)?.languageCode == 'ar' || Directionality.maybeOf(context) == TextDirection.rtl ? ar : en;
}

/// The assistant's chat. A change is only ever made by the Confirm button on a card the server built.
class AssistantPage extends ConsumerStatefulWidget {
  const AssistantPage({super.key});

  @override
  ConsumerState<AssistantPage> createState() => _AssistantPageState();
}

class _AssistantPageState extends ConsumerState<AssistantPage> {
  final _input = TextEditingController();
  final _scroll = ScrollController();

  var _items = <AssistantItem>[];
  var _sending = false;
  String? _working;
  String? _error;
  var _showHistory = false;
  var _loadingPast = false;
  var _past = <AssistantConversationRow>[];
  String? _conversationId;
  StreamSubscription<AssistantEvent>? _run;
  CancelToken? _token;

  /// Moves on whenever the chat on screen changes, so an answer that arrives late cannot land in another chat.
  var _chat = 0;

  AssistantApi get _api => ref.read(assistantApiProvider);
  AssistantWords get _w => AssistantWords.of(context);

  @override
  void dispose() {
    _stopRun();
    _input.dispose();
    _scroll.dispose();
    super.dispose();
  }

  /// Ends the running reply. The token closes the connection; ignore() because a reply cancelled before its first event reports that here.
  void _stopRun() {
    _run?.cancel().ignore();
    _run = null;
    _token?.cancel();
    _token = null;
  }

  void _newChat() {
    _stopRun();
    _chat++;
    setState(() {
      _sending = false;
      _working = null;
      _conversationId = null;
      _items = [];
      _error = null;
      _showHistory = false;
    });
  }

  Future<void> _toggleHistory() async {
    if (_showHistory) {
      setState(() => _showHistory = false);
      return;
    }
    setState(() {
      _showHistory = true;
      _loadingPast = true;
    });
    try {
      final list = await _api.conversations();
      if (mounted) setState(() => _past = list);
    } on ApiException catch (e) {
      _toast(e);
    } finally {
      if (mounted) setState(() => _loadingPast = false);
    }
  }

  Future<void> _openConversation(String id) async {
    final chat = ++_chat;
    _stopRun();
    setState(() {
      _sending = false;
      _working = null;
    });
    try {
      final c = await _api.conversation(id);
      if (!mounted || chat != _chat) return;
      setState(() {
        _items = c.items;
        _conversationId = c.id;
        _error = null;
        _showHistory = false;
      });
      _scrollDown();
    } on ApiException catch (e) {
      _toast(e);
    }
  }

  Future<void> _remove(String id) async {
    try {
      await _api.deleteConversation(id);
      if (!mounted) return;
      setState(() {
        _past = _past.where((c) => c.id != id).toList();
        if (_conversationId == id) {
          _stopRun();
          _chat++;
          _sending = false;
          _working = null;
          _conversationId = null;
          _items = [];
        }
      });
    } on ApiException catch (e) {
      _toast(e);
    }
  }

  void _send(String raw) {
    final text = raw.trim();
    if (text.isEmpty || _sending) return;
    final chat = ++_chat;
    final token = CancelToken();
    _input.clear();
    setState(() {
      _error = null;
      _sending = true;
      _working = _w.working;
      _items = [..._items, AssistantUserItem(text)];
    });
    _scrollDown();
    _token = token;
    _run = _api.ask(conversationId: _conversationId, text: text, cancelToken: token).listen(
      (e) {
        if (mounted && chat == _chat) _onEvent(e);
      },
      onError: (Object e) {
        if (!mounted || chat != _chat) return;
        _finish();
        setState(() => _error = assistantErrorMessage(e, _w.failed));
      },
      onDone: () {
        if (mounted && chat == _chat) _finish();
      },
      cancelOnError: true,
    );
  }

  void _onEvent(AssistantEvent e) {
    switch (e) {
      case AssistantConversationEvent(:final id):
        _conversationId = id;
      case AssistantStatusEvent(:final text):
        setState(() => _working = identical(_w, AssistantWords.ar) ? _w.working : text);
      case AssistantDeltaEvent(:final text):
        setState(() {
          _working = null;
          final last = _items.isEmpty ? null : _items.last;
          _items = last is AssistantReplyItem
              ? [..._items.take(_items.length - 1), AssistantReplyItem(last.text + text)]
              : [..._items, AssistantReplyItem(text)];
        });
        _scrollDown();
      case AssistantActionEvent(:final card):
        setState(() => _items = [..._items, AssistantCardItem(card)]);
        _scrollDown();
      case AssistantErrorEvent(:final message):
        setState(() => _error = message);
    }
  }

  void _stop() {
    _stopRun();
    _chat++;
    _finish();
  }

  void _finish() {
    _run = null;
    _token = null;
    setState(() {
      _sending = false;
      _working = null;
    });
  }

  /// Runs only from the person's tap on a card's button.
  Future<void> _decide(AssistantActionCard card, {required bool confirm}) async {
    if (_items.any((i) => i is AssistantCardItem && i.card.id == card.id && i.busy)) return;
    _setCard(card.id, AssistantCardItem(card, busy: true));
    try {
      final updated = confirm ? await _api.confirm(card.id) : await _api.cancel(card.id);
      if (!mounted) return;
      _setCard(card.id, AssistantCardItem(updated));
      if (updated.status == 'done') ref.read(dataVersionProvider.notifier).bump(); // open lists and details show the change
      _scrollDown();
    } on ApiException catch (e) {
      if (!mounted) return;
      _setCard(card.id, AssistantCardItem(card));
      _toast(e);
    }
  }

  void _setCard(String id, AssistantCardItem item) {
    setState(() => _items = [for (final i in _items) i is AssistantCardItem && i.card.id == id ? item : i]);
  }

  void _toast(ApiException e) {
    if (mounted) ScaffoldMessenger.maybeOf(context)?.showSnackBar(SnackBar(content: Text(assistantErrorMessage(e, _w.failed))));
  }

  void _scrollDown() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted && _scroll.hasClients) _scroll.jumpTo(_scroll.position.maxScrollExtent);
    });
  }

  @override
  Widget build(BuildContext context) {
    // Another workspace must never see this one's open chat.
    ref.listen(sessionProvider.select((s) => s.workspace?.id), (_, _) => _newChat());
    final w = AssistantWords.of(context);
    final t = ErpTheme.of(context);
    return Scaffold(
      appBar: AppBar(
        title: Row(children: [
          const AssistantMark(size: 28),
          const SizedBox(width: ErpSpace.s2),
          Flexible(child: Text(w.title, maxLines: 1, overflow: TextOverflow.ellipsis)),
        ]),
        actions: [
          IconButton(key: const ValueKey('assistant-new'), tooltip: w.newChat, icon: const Icon(Icons.add), onPressed: _newChat),
          IconButton(key: const ValueKey('assistant-history'), tooltip: w.history, icon: const Icon(Icons.history), onPressed: _toggleHistory),
        ],
      ),
      body: Column(children: [
        Container(
          width: double.infinity,
          color: t.sunken,
          padding: const EdgeInsets.symmetric(horizontal: ErpSpace.s4, vertical: ErpSpace.s2),
          child: Text(w.notice, style: TextStyle(fontSize: ErpType.caption, color: t.muted)),
        ),
        Expanded(child: _showHistory ? _history(w, t) : _log(w, t)),
        if (!_showHistory) _sendBar(w),
      ]),
    );
  }

  Widget _history(AssistantWords w, ErpTheme t) {
    if (_loadingPast && _past.isEmpty) return const ErpSkeleton.list();
    if (_past.isEmpty) {
      return Padding(
        padding: const EdgeInsets.all(ErpSpace.s4),
        child: Align(alignment: AlignmentDirectional.topStart, child: Text(w.noHistory, style: TextStyle(color: t.muted))),
      );
    }
    return ListView.separated(
      key: const ValueKey('assistant-past'),
      padding: const EdgeInsets.symmetric(vertical: ErpSpace.s2),
      itemCount: _past.length,
      separatorBuilder: (_, _) => Divider(height: 1, color: t.borderSubtle),
      itemBuilder: (_, i) {
        final c = _past[i];
        return Row(children: [
          Expanded(
            child: InkWell(
              onTap: () => _openConversation(c.id),
              child: ConstrainedBox(
                constraints: const BoxConstraints(minHeight: 48),
                child: Padding(
                  padding: const EdgeInsetsDirectional.fromSTEB(ErpSpace.s4, ErpSpace.s2, ErpSpace.s2, ErpSpace.s2),
                  child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                    Text(c.title, textDirection: assistantDirection(c.title), maxLines: 2, overflow: TextOverflow.ellipsis),
                    Text(_when(c.lastMessageAt), style: TextStyle(fontSize: ErpType.caption, color: t.muted)),
                  ]),
                ),
              ),
            ),
          ),
          IconButton(tooltip: w.remove, icon: const Icon(Icons.delete_outline, color: ErpColors.danger), onPressed: () => _remove(c.id)),
        ]);
      },
    );
  }

  static String _when(String at) {
    final time = DateTime.tryParse(at);
    return time == null ? at : DateFormat.yMMMd().add_jm().format(time.toLocal());
  }

  Widget _log(AssistantWords w, ErpTheme t) {
    return ListView(
      key: const ValueKey('assistant-log'),
      controller: _scroll,
      padding: const EdgeInsets.all(ErpSpace.s3),
      children: [
        if (_items.isEmpty) ...[
          _Bubble(mine: false, child: Text(w.hello)),
          for (final s in w.suggestions)
            Padding(
              padding: const EdgeInsets.only(bottom: ErpSpace.s15),
              child: Align(
                alignment: AlignmentDirectional.centerStart,
                child: OutlinedButton(key: ValueKey('assistant-suggestion-$s'), onPressed: () => _send(s), child: Text(s)),
              ),
            ),
        ],
        for (final item in _items)
          switch (item) {
            AssistantUserItem(:final text) => _Bubble(mine: true, child: Text(text, textDirection: assistantDirection(text))),
            AssistantReplyItem(:final text) => _Bubble(mine: false, child: AssistantReplyText(text: text)),
            AssistantCardItem() => _ActionCardView(
                item: item,
                words: w,
                onConfirm: () => _decide(item.card, confirm: true),
                onCancel: () => _decide(item.card, confirm: false),
              ),
          },
        if (_working != null)
          Semantics(
            liveRegion: true,
            child: Text('$_working...', key: const ValueKey('assistant-status'), style: TextStyle(fontSize: ErpType.bodySmall, color: t.muted)),
          ),
        if (_error != null)
          Semantics(
            liveRegion: true,
            child: Text(_error!, key: const ValueKey('assistant-error'), textDirection: assistantDirection(_error!), style: const TextStyle(fontSize: ErpType.bodySmall, color: ErpColors.danger)),
          ),
      ],
    );
  }

  Widget _sendBar(AssistantWords w) {
    return SafeArea(
      top: false,
      child: Padding(
        padding: const EdgeInsets.all(ErpSpace.s3),
        child: ListenableBuilder(
          listenable: _input,
          builder: (context, _) => Row(crossAxisAlignment: CrossAxisAlignment.end, children: [
            Expanded(
              child: TextField(
                key: const ValueKey('assistant-input'),
                controller: _input,
                minLines: 1,
                maxLines: 4,
                maxLength: 2000,
                buildCounter: (_, {required currentLength, required isFocused, maxLength}) => null,
                textDirection: assistantDirection(_input.text) ?? Directionality.of(context),
                decoration: InputDecoration(hintText: w.placeholder),
              ),
            ),
            const SizedBox(width: ErpSpace.s2),
            if (_sending)
              OutlinedButton(key: const ValueKey('assistant-stop'), onPressed: _stop, child: Text(w.stop))
            else
              FilledButton(key: const ValueKey('assistant-send'), onPressed: _input.text.trim().isEmpty ? null : () => _send(_input.text), child: Text(w.send)),
          ]),
        ),
      ),
    );
  }
}

class _Bubble extends StatelessWidget {
  const _Bubble({required this.mine, required this.child});

  final bool mine;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    final t = ErpTheme.of(context);
    return Padding(
      padding: const EdgeInsets.only(bottom: ErpSpace.s2),
      child: Align(
        alignment: mine ? AlignmentDirectional.centerEnd : AlignmentDirectional.centerStart,
        child: ConstrainedBox(
          constraints: BoxConstraints(maxWidth: MediaQuery.sizeOf(context).width * 0.85),
          child: DecoratedBox(
            decoration: BoxDecoration(color: mine ? t.brandSubtle : t.sunken, borderRadius: BorderRadius.circular(ErpRadius.card)),
            child: Padding(padding: const EdgeInsets.symmetric(horizontal: ErpSpace.s3, vertical: ErpSpace.s2), child: child),
          ),
        ),
      ),
    );
  }
}

/// A reply, drawn only as text: no HTML, no images, and the only tappable words are links the app can open itself. Every other link shows
/// just its words. Each block takes its direction from its own words, so an Arabic reply reads right to left.
class AssistantReplyText extends ConsumerStatefulWidget {
  const AssistantReplyText({super.key, required this.text});

  final String text;

  @override
  ConsumerState<AssistantReplyText> createState() => _AssistantReplyTextState();
}

class _AssistantReplyTextState extends ConsumerState<AssistantReplyText> {
  final _links = <TapGestureRecognizer>[];

  void _clearLinks() {
    for (final r in _links) {
      r.dispose();
    }
    _links.clear();
  }

  @override
  void dispose() {
    _clearLinks();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    _clearLinks();
    final menu = ref.watch(menuProvider).asData?.value ?? const <MenuGroup>[];
    final brand = ErpTheme.of(context).brand;

    InlineSpan span(AssistantInline part) {
      final screen = screenForLink(part.link, menu: menu);
      if (screen == null) return TextSpan(text: part.text, style: part.bold ? const TextStyle(fontWeight: FontWeight.w700) : null);
      final tap = TapGestureRecognizer()..onTap = () => Navigator.of(context).push(MaterialPageRoute<void>(builder: screen.builder));
      _links.add(tap);
      return TextSpan(text: part.text, recognizer: tap, style: TextStyle(color: brand, decoration: TextDecoration.underline));
    }

    Widget line(List<AssistantInline> parts) => Text.rich(TextSpan(children: [for (final p in parts) span(p)]));
    String words(List<AssistantInline> parts) => parts.map((p) => p.text).join();

    return IntrinsicWidth(
      child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
        for (final block in parseAssistantText(widget.text))
          Padding(
            padding: const EdgeInsets.only(bottom: ErpSpace.s1),
            child: switch (block) {
              AssistantParagraph(:final parts) => Directionality(
                  textDirection: assistantDirection(words(parts)) ?? Directionality.of(context),
                  child: line(parts),
                ),
              AssistantList(:final ordered, :final items) => Directionality(
                  textDirection: assistantDirection(words(items.first)) ?? Directionality.of(context),
                  child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                    for (var i = 0; i < items.length; i++)
                      Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                        Text(ordered ? '${i + 1}. ' : '•  '),
                        Expanded(child: line(items[i])),
                      ]),
                  ]),
                ),
            },
          ),
      ]),
    );
  }
}

class _ActionCardView extends ConsumerWidget {
  const _ActionCardView({required this.item, required this.words, required this.onConfirm, required this.onCancel});

  final AssistantCardItem item;
  final AssistantWords words;
  final VoidCallback onConfirm;
  final VoidCallback onCancel;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final t = ErpTheme.of(context);
    final card = item.card;
    final direction = assistantDirection(card.title) ?? Directionality.of(context);
    final link = card.link;
    final screen = link != null && isInternalLink(link) ? screenForLink(link, menu: ref.watch(menuProvider).asData?.value ?? const <MenuGroup>[]) : null;
    return Padding(
      key: ValueKey('assistant-card-${card.status}'),
      padding: const EdgeInsets.only(bottom: ErpSpace.s2),
      child: DecoratedBox(
        decoration: BoxDecoration(border: Border.all(color: t.brand), borderRadius: BorderRadius.circular(ErpRadius.card)),
        child: Padding(
          padding: const EdgeInsets.all(ErpSpace.s3),
          child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
            Text(card.title, textDirection: direction, style: const TextStyle(fontWeight: FontWeight.w700)),
            const SizedBox(height: ErpSpace.s15),
            Directionality(
              textDirection: direction,
              child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                for (final l in card.lines)
                  Padding(
                    padding: const EdgeInsets.only(bottom: ErpSpace.s05),
                    child: Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                      Flexible(child: Text(l.label, style: TextStyle(color: t.muted))),
                      const SizedBox(width: ErpSpace.s3),
                      Expanded(child: Text(l.value)),
                    ]),
                  ),
              ]),
            ),
            const SizedBox(height: ErpSpace.s2),
            if (card.status == 'pending') ...[
              Text(words.check, style: TextStyle(fontSize: ErpType.bodySmall, color: t.muted)),
              const SizedBox(height: ErpSpace.s2),
              Wrap(spacing: ErpSpace.s2, runSpacing: ErpSpace.s2, children: [
                FilledButton(key: const ValueKey('assistant-confirm'), onPressed: item.busy ? null : onConfirm, child: Text(words.confirm)),
                OutlinedButton(key: const ValueKey('assistant-cancel'), onPressed: item.busy ? null : onCancel, child: Text(words.cancel)),
              ]),
            ] else if (card.status == 'cancelled')
              Text(words.cancelled, style: const TextStyle(fontSize: ErpType.bodySmall))
            else ...[
              Text(
                card.message?.isNotEmpty == true ? card.message! : words.expired,
                key: const ValueKey('assistant-outcome'),
                textDirection: assistantDirection(card.message ?? ''),
                style: TextStyle(fontSize: ErpType.bodySmall, color: card.status == 'done' ? null : ErpColors.danger),
              ),
              if (screen != null)
                Align(
                  alignment: AlignmentDirectional.centerStart,
                  child: TextButton(onPressed: () => Navigator.of(context).push(MaterialPageRoute<void>(builder: screen.builder)), child: Text(words.open)),
                ),
            ],
          ]),
        ),
      ),
    );
  }
}

/// The way in on the More tab, shown only when the assistant is on offer. It is a card, not a grid tile, because a tile's icon cannot be
/// the assistant's own mark.
class AssistantEntryCard extends StatelessWidget {
  const AssistantEntryCard({super.key});

  @override
  Widget build(BuildContext context) {
    final t = ErpTheme.of(context);
    final w = AssistantWords.of(context);
    return Padding(
      padding: const EdgeInsets.fromLTRB(ErpSpace.s4, ErpSpace.s3, ErpSpace.s4, 0),
      child: Material(
        color: t.surface,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(ErpRadius.card), side: BorderSide(color: t.borderSubtle)),
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          key: const ValueKey('assistant-entry'),
          onTap: () => Navigator.of(context).push(MaterialPageRoute<void>(builder: assistantScreen.builder)),
          child: Padding(
            padding: const EdgeInsets.all(ErpSpace.s3),
            child: Row(children: [
              const AssistantMark(size: 36),
              const SizedBox(width: ErpSpace.s3),
              Expanded(
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Text(w.title, style: const TextStyle(fontWeight: FontWeight.w600)),
                  Text(w.hello, style: TextStyle(fontSize: ErpType.caption, color: t.muted)),
                ]),
              ),
            ]),
          ),
        ),
      ),
    );
  }
}

/// The assistant's mark: a copy of `frontend/public/highersquare-assistant.svg`, drawn because the app has no SVG package (as [BrandMark]).
class AssistantMark extends StatelessWidget {
  const AssistantMark({super.key, this.size = 28});

  final double size;

  @override
  Widget build(BuildContext context) => ExcludeSemantics(child: CustomPaint(size: Size.square(size), painter: const AssistantMarkPainter()));
}

class AssistantMarkPainter extends CustomPainter {
  const AssistantMarkPainter();

  @override
  void paint(Canvas canvas, Size size) {
    final s = size.shortestSide / 64;
    canvas.save();
    canvas.scale(s);
    final brand = ui.Gradient.linear(const Offset(10, 54), const Offset(54, 10), const [BrandColors.suffixLight, BrandColors.wordLight]);

    canvas.drawCircle(const Offset(32, 32), 31, Paint()..color = Colors.white);
    canvas.drawPath(
      Path()
        ..moveTo(58.08, 25.01)
        ..arcToPoint(const Offset(41.23, 6.63), radius: const Radius.circular(27), largeArc: true),
      Paint()
        ..shader = brand
        ..style = PaintingStyle.stroke
        ..strokeWidth = 5
        ..strokeCap = StrokeCap.round,
    );
    const corner = Radius.circular(6);
    canvas.drawPath(
      Path()
        ..moveTo(22, 19)
        ..lineTo(38, 19)
        ..arcToPoint(const Offset(44, 25), radius: corner)
        ..lineTo(44, 35)
        ..arcToPoint(const Offset(38, 41), radius: corner)
        ..lineTo(28, 41)
        ..lineTo(20, 47.5)
        ..lineTo(20, 40.6)
        ..arcToPoint(const Offset(16, 35), radius: corner)
        ..lineTo(16, 25)
        ..arcToPoint(const Offset(22, 19), radius: corner)
        ..close(),
      Paint()..shader = brand,
    );
    final white = Paint()..color = Colors.white;
    for (final bar in const [Rect.fromLTWH(21.4, 30, 4, 6.5), Rect.fromLTWH(28, 27, 4, 9.5), Rect.fromLTWH(34.6, 23.5, 4, 13)]) {
      canvas.drawRRect(RRect.fromRectAndRadius(bar, const Radius.circular(1.2)), white);
    }
    canvas.drawPath(
      Path()
        ..moveTo(50, 7)
        ..quadraticBezierTo(51, 13, 57, 14)
        ..quadraticBezierTo(51, 15, 50, 21)
        ..quadraticBezierTo(49, 15, 43, 14)
        ..quadraticBezierTo(49, 13, 50, 7)
        ..close(),
      Paint()..color = ErpColors.brand,
    );
    canvas.restore();
  }

  @override
  bool shouldRepaint(covariant AssistantMarkPainter oldDelegate) => false;
}
