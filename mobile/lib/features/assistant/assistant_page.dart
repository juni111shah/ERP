import 'dart:async';
import 'dart:ui' as ui;

import 'package:dio/dio.dart' show CancelToken;
import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart' show DateFormat; // intl has its own TextDirection

import '../../core/api_client.dart';
import '../../core/app_language.dart';
import '../../core/brand.dart';
import '../../core/me.dart';
import '../../core/session.dart';
import '../../kit/data_version.dart';
import '../../kit/registry.dart';
import '../../kit/skeleton.dart';
import '../../theme/app_theme.dart';
import '../../theme/tokens.dart';
import 'assistant_api.dart';
import 'assistant_links.dart';
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

/// Copy for the assistant, in English and Arabic (same words as the web panel).
class AssistantWords {
  const AssistantWords._({
    required this.direction,
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
    required this.entity,
    required this.allEntities,
    required this.chooseEntity,
    required this.paused,
    required this.suggestions,
    required this.usage,
    required this.ratings,
    required this.helpful,
    required this.notHelpful,
    required this.errors,
    required this.statuses,
    required this.serverTexts,
  });

  final TextDirection direction;
  final String title, newChat, history, noHistory, remove, notice, hello, placeholder, send, stop, working, failed, confirm, cancel, open, cancelled, expired, check;
  final String entity, allEntities, chooseEntity;

  /// For a workspace that cannot make changes (402).
  final String paused;
  final List<String> suggestions;

  /// The workspace's usage this month; the second figure is null when the plan has no limit.
  final String Function(int used, int? max) usage;

  /// How this month's replies were rated, as counts.
  final String Function(int helpful, int notHelpful) ratings;

  /// The names of the thumbs on a reply.
  final String helpful, notHelpful;

  /// By the code of the stream's `error` event.
  final Map<String, String> errors;

  /// By the words of the stream's `status` event, which come only in English. Null means show them as they are.
  final Map<String, String>? statuses;

  /// Fixed sentences the server can send, by their English words.
  final Map<String, String> serverTexts;

  static String _enUsage(int used, int? max) => max == null ? 'Your workspace has used $used messages this month.' : 'Your workspace has used $used of $max messages this month.';
  static String _arUsage(int used, int? max) => max == null ? 'استخدمت مساحة عملك $used رسالة هذا الشهر.' : 'استخدمت مساحة عملك $used من أصل $max رسالة هذا الشهر.';
  static String _enRatings(int helpful, int notHelpful) => 'Answers rated this month: $helpful helpful, $notHelpful not helpful.';
  static String _arRatings(int helpful, int notHelpful) => 'الإجابات المقيّمة هذا الشهر: $helpful مفيدة، $notHelpful غير مفيدة.';

  static const en = AssistantWords._(
    direction: TextDirection.ltr,
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
    entity: 'Legal entity',
    allEntities: 'All legal entities',
    chooseEntity: 'Choose a legal entity',
    paused: 'This workspace is view-only until its subscription is brought up to date, so this change was not made.',
    suggestions: ['What needs my attention today?', 'Who owes us the most?', 'Where do I add a new customer?'],
    usage: _enUsage,
    ratings: _enRatings,
    helpful: 'This answer was helpful',
    notHelpful: 'This answer was not helpful',
    errors: {
      'user-limit': "You have used today's messages for the assistant. Please try again tomorrow.",
      'client-limit': "Your workspace has used today's assistant messages. Please try again tomorrow.",
      'busy': 'Please wait for the current reply to finish.',
      'month-limit': "Your workspace has used this month's assistant messages. Your administrator can ask for a larger plan.",
      'daily-limit': 'The assistant has reached its limit for today. Please try again tomorrow.',
      'no-service': 'The assistant is not available right now.',
      'off': 'The assistant is not available right now.',
      'interrupted': 'The reply was interrupted. Please try again.',
      'unavailable': 'The assistant is not available right now. Please try again in a moment.',
      'failed': 'Sorry, something went wrong. Please try again.',
    },
    statuses: null,
    serverTexts: {},
  );

  static const ar = AssistantWords._(
    direction: TextDirection.rtl,
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
    entity: 'الكيان القانوني',
    allEntities: 'كل الكيانات القانونية',
    chooseEntity: 'اختر كياناً قانونياً',
    paused: 'مساحة العمل هذه للعرض فقط إلى أن يتم تحديث اشتراكها، لذا لم يُنفَّذ هذا التغيير.',
    suggestions: ['ما الذي يحتاج إلى انتباهي اليوم؟', 'من أكثر من يدين لنا بالمال؟', 'أين أضيف عميلاً جديداً؟'],
    usage: _arUsage,
    ratings: _arRatings,
    helpful: 'كانت هذه الإجابة مفيدة',
    notHelpful: 'لم تكن هذه الإجابة مفيدة',
    errors: {
      'user-limit': 'لقد استخدمت رسائل اليوم المتاحة للمساعد. يرجى المحاولة مرة أخرى غداً.',
      'client-limit': 'استخدمت مساحة عملك رسائل اليوم المخصصة للمساعد. يرجى المحاولة مرة أخرى غداً.',
      'busy': 'يرجى الانتظار حتى ينتهي الرد الحالي.',
      'month-limit': 'استخدمت مساحة عملك رسائل المساعد المتاحة لهذا الشهر. يمكن لمسؤول مساحة العمل طلب خطة أكبر.',
      'daily-limit': 'بلغ المساعد الحد المسموح به لهذا اليوم. يرجى المحاولة مرة أخرى غداً.',
      'no-service': 'المساعد غير متاح الآن.',
      'off': 'المساعد غير متاح الآن.',
      'interrupted': 'انقطع الرد. يرجى المحاولة مرة أخرى.',
      'unavailable': 'المساعد غير متاح الآن. يرجى المحاولة مرة أخرى بعد قليل.',
      'failed': 'عذراً، حدث خطأ ما. يرجى المحاولة مرة أخرى.',
    },
    statuses: {
      'Thinking': 'جارٍ التفكير',
      'Searching your records': 'جارٍ البحث في سجلاتك',
      'Checking the help articles': 'جارٍ الاطلاع على مقالات المساعدة',
      'Looking through your records': 'جارٍ الاطلاع على سجلاتك',
      'Running the report': 'جارٍ إعداد التقرير',
      'Checking your overview': 'جارٍ الاطلاع على نظرتك العامة',
      'Checking approvals': 'جارٍ التحقق من الموافقات',
      'Looking through the menu': 'جارٍ البحث في القائمة',
      'Preparing the change': 'جارٍ تحضير التغيير',
    },
    serverTexts: {
      'Choose a legal entity at the top of the page first.': 'اختر كياناً قانونياً من أعلى الشاشة أولاً.',
      'This suggestion has expired. Ask again if you still want it.': 'انتهت صلاحية هذا الاقتراح. اطلبه مرة أخرى إن كنت لا تزال تريده.',
      'You cannot work in that legal entity.': 'لا يمكنك العمل في هذا الكيان القانوني.',
    },
  );

  /// What to show for a stream `error` event: the words for its code, or the server's own for a code this app does not know.
  String errorFor(String code, String serverMessage) => errors[code] ?? (serverMessage.isEmpty ? failed : serverMessage);

  /// What to show for a stream `status` event. An unknown status in Arabic stays general rather than showing English.
  String statusFor(String serverText) {
    final words = statuses;
    if (words != null) return words[serverText] ?? working;
    return serverText.isEmpty ? working : serverText;
  }

  /// A sentence from the server, in this language when it is one of the fixed ones.
  String serverText(String text) => serverTexts[text] ?? text;

  /// What to show for a failed call.
  String problem(Object error) {
    if (error is ApiException && error.status == 402) {
      return identical(this, ar) ? paused : assistantErrorMessage(error, paused);
    }
    return serverText(assistantErrorMessage(error, failed));
  }

  static AssistantWords of(BuildContext context) =>
      AppLanguage.maybeOf(context)?.languageCode == 'ar' || Localizations.maybeLocaleOf(context)?.languageCode == 'ar' || Directionality.maybeOf(context) == TextDirection.rtl ? ar : en;
}

/// Sets the assistant's language around [child]: with an Arabic device this also gives its buttons and tooltips Flutter's own Arabic. The rest
/// of the app stays English for now (see [appLanguageBuilder]), so this is the only place that turns right to left.
class AssistantScope extends StatelessWidget {
  const AssistantScope({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    final language = AppLanguage.maybeOf(context);
    if (language?.languageCode == 'ar') return Localizations.override(context: context, locale: language, child: child);
    return Directionality(textDirection: AssistantWords.of(context).direction, child: child);
  }
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
  String? _replyId;
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

  /// Pressing the chosen thumb again takes it back. A failure puts back what was there.
  Future<void> _rate(AssistantReplyItem item, bool choice) async {
    final id = item.id;
    if (id == null) return;
    void show(bool? helpful) => setState(() => _items = [for (final i in _items) i is AssistantReplyItem && i.id == id ? AssistantReplyItem(i.text, id: id, helpful: helpful) : i]);
    final helpful = item.helpful == choice ? null : choice;
    show(helpful);
    try {
      await _api.feedback(id, helpful);
    } on ApiException catch (e) {
      if (!mounted) return;
      show(item.helpful);
      _toast(e);
    }
  }

  void _newChat() {
    _stopRun();
    _chat++;
    _replyId = null;
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
    _replyId = null;
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
          _replyId = null;
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
    _replyId = null;
    setState(() {
      _error = null;
      _sending = true;
      _working = _w.working;
      _items = [..._items, AssistantUserItem(text)];
    });
    _scrollDown();
    _token = token;
    _run = _api.ask(conversationId: _conversationId, text: text, language: AppLanguage.maybeOf(context)?.languageCode == 'ar' || Localizations.maybeLocaleOf(context)?.languageCode == 'ar' ? 'ar' : 'en', cancelToken: token).listen(
      (e) {
        if (mounted && chat == _chat) _onEvent(e);
      },
      onError: (Object e) {
        if (!mounted || chat != _chat) return;
        _finish();
        setState(() => _error = _w.problem(e));
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
      case AssistantReplyEvent(:final id):
        _replyId = id;
      case AssistantStatusEvent(:final text):
        setState(() => _working = _w.statusFor(text));
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
      case AssistantErrorEvent(:final code, :final message):
        setState(() => _error = _w.errorFor(code, message));
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
    // The server saves a reply as its stream ends, so the buttons wait until now.
    final id = _replyId;
    _replyId = null;
    setState(() {
      _sending = false;
      _working = null;
      final last = _items.lastIndexWhere((i) => i is AssistantReplyItem);
      if (id != null && last >= 0) _items = [for (var n = 0; n < _items.length; n++) n == last ? AssistantReplyItem((_items[n] as AssistantReplyItem).text, id: id) : _items[n]];
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
    if (mounted) ScaffoldMessenger.maybeOf(context)?.showSnackBar(SnackBar(content: Text(_w.problem(e))));
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
    final usage = ref.watch(assistantUsageProvider).asData?.value;
    return AssistantScope(
      child: Scaffold(
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
            child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              Text(w.notice, style: TextStyle(fontSize: ErpType.caption, color: t.muted)),
              if (usage != null)
                Padding(
                  padding: const EdgeInsets.only(top: ErpSpace.s1),
                  child: Text(w.usage(usage.messagesThisMonth, usage.monthlyAllowance), key: const ValueKey('assistant-usage'), style: TextStyle(fontSize: ErpType.caption, color: t.muted)),
                ),
              if (usage != null && (usage.helpful > 0 || usage.notHelpful > 0))
                Padding(
                  padding: const EdgeInsets.only(top: ErpSpace.s1),
                  child: Text(w.ratings(usage.helpful, usage.notHelpful), key: const ValueKey('assistant-ratings'), style: TextStyle(fontSize: ErpType.caption, color: t.muted)),
                ),
            ]),
          ),
          _entityPicker(w, t),
          Expanded(child: _showHistory ? _history(w, t) : _log(w, t)),
          if (!_showHistory) _sendBar(w),
        ]),
      ),
    );
  }

  /// Only for people with more than one legal entity. It stays put during a reply, which is already running in the one it started in.
  Widget _entityPicker(AssistantWords w, ErpTheme t) {
    final me = ref.watch(meOrEmptyProvider);
    if (me.legalEntities.length <= 1) return const SizedBox.shrink();
    final items = <DropdownMenuItem<String?>>[
      if (me.allLegalEntities) DropdownMenuItem<String?>(value: null, child: Text(w.allEntities, maxLines: 1, overflow: TextOverflow.ellipsis)),
      for (final e in me.legalEntities) DropdownMenuItem<String?>(value: e.id, child: Text(e.name, maxLines: 1, overflow: TextOverflow.ellipsis)),
    ];
    return DecoratedBox(
      decoration: BoxDecoration(border: Border(bottom: BorderSide(color: t.borderSubtle))),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: ErpSpace.s4),
        child: Row(children: [
          Icon(Icons.account_balance_outlined, size: 18, color: t.muted, semanticLabel: w.entity),
          const SizedBox(width: ErpSpace.s2),
          Expanded(
            child: DropdownButtonHideUnderline(
              child: DropdownButton<String?>(
                key: const ValueKey('assistant-entity'),
                isExpanded: true,
                value: ref.watch(assistantEntityIdProvider),
                hint: Text(w.chooseEntity, maxLines: 1, overflow: TextOverflow.ellipsis),
                items: items,
                onChanged: _sending ? null : (id) => ref.read(assistantEntityProvider.notifier).choose(id),
              ),
            ),
          ),
        ]),
      ),
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
                    Text(_when(c.lastMessageAt, _dateLocale(context)), style: TextStyle(fontSize: ErpType.caption, color: t.muted)),
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

  /// Arabic dates need the data Flutter's delegates load for an Arabic device, which a right-to-left direction alone does not bring.
  /// Null is the default English, as in the rest of the app.
  static String? _dateLocale(BuildContext context) => AppLanguage.maybeOf(context)?.languageCode == 'ar' ? 'ar' : null;

  static String _when(String at, String? locale) {
    final time = DateTime.tryParse(at);
    return time == null ? at : DateFormat.yMMMd(locale).add_jm().format(time.toLocal());
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
            AssistantReplyItem(:final text, :final id, :final helpful) => Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                _Bubble(mine: false, child: AssistantReplyText(text: text)),
                if (id != null)
                  Align(
                    alignment: AlignmentDirectional.centerStart,
                    child: Row(mainAxisSize: MainAxisSize.min, children: [
                      IconButton(key: const ValueKey('assistant-helpful'), tooltip: w.helpful, isSelected: helpful == true, icon: const Icon(Icons.thumb_up_outlined, size: 18), selectedIcon: const Icon(Icons.thumb_up, size: 18), onPressed: () => _rate(item, true)),
                      IconButton(key: const ValueKey('assistant-not-helpful'), tooltip: w.notHelpful, isSelected: helpful == false, icon: const Icon(Icons.thumb_down_outlined, size: 18), selectedIcon: const Icon(Icons.thumb_down, size: 18), onPressed: () => _rate(item, false)),
                    ]),
                  ),
              ]),
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
      final target = assistantLinkTarget(part.link, menu: menu);
      if (target == null) return TextSpan(text: part.text, style: part.bold ? const TextStyle(fontWeight: FontWeight.w700) : null);
      final tap = TapGestureRecognizer()..onTap = () => Navigator.of(context).push(MaterialPageRoute<void>(builder: target));
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

class _ActionCardView extends ConsumerStatefulWidget {
  const _ActionCardView({required this.item, required this.words, required this.onConfirm, required this.onCancel});

  final AssistantCardItem item;
  final AssistantWords words;
  final VoidCallback onConfirm;
  final VoidCallback onCancel;

  @override
  ConsumerState<_ActionCardView> createState() => _ActionCardViewState();
}

class _ActionCardViewState extends ConsumerState<_ActionCardView> {
  Timer? _expiry;
  var _ranOut = false;

  /// So that a card left open on the screen loses its Confirm button when its time is up, not just the next time it is drawn.
  void _watchExpiry() {
    _expiry?.cancel();
    _expiry = null;
    _ranOut = false;
    final card = widget.item.card;
    final at = card.expiresAt;
    if (at == null || card.status != 'pending') return;
    final left = at.difference(DateTime.now());
    if (left <= Duration.zero) return;
    _expiry = Timer(left, () {
      if (mounted) setState(() => _ranOut = true);
    });
  }

  @override
  void initState() {
    super.initState();
    _watchExpiry();
  }

  @override
  void didUpdateWidget(_ActionCardView old) {
    super.didUpdateWidget(old);
    if (old.item.card.id != widget.item.card.id || old.item.card.status != widget.item.card.status || old.item.card.expiresAt != widget.item.card.expiresAt) _watchExpiry();
  }

  @override
  void dispose() {
    _expiry?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final t = ErpTheme.of(context);
    final item = widget.item;
    final words = widget.words;
    final card = item.card;
    final expired = card.status == 'pending' && (_ranOut || card.isPastExpiry(DateTime.now()));
    final direction = assistantDirection(card.title) ?? Directionality.of(context);
    final target = assistantLinkTarget(card.link, menu: ref.watch(menuProvider).asData?.value ?? const <MenuGroup>[]);
    final outcome = expired || card.message?.isNotEmpty != true ? words.expired : words.serverText(card.message!);
    return Padding(
      key: ValueKey('assistant-card-${expired ? 'expired' : card.status}'),
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
            if (card.status == 'pending' && !expired) ...[
              Text(words.check, style: TextStyle(fontSize: ErpType.bodySmall, color: t.muted)),
              const SizedBox(height: ErpSpace.s2),
              Wrap(spacing: ErpSpace.s2, runSpacing: ErpSpace.s2, children: [
                FilledButton(key: const ValueKey('assistant-confirm'), onPressed: item.busy ? null : widget.onConfirm, child: Text(words.confirm)),
                OutlinedButton(key: const ValueKey('assistant-cancel'), onPressed: item.busy ? null : widget.onCancel, child: Text(words.cancel)),
              ]),
            ] else if (card.status == 'cancelled')
              Text(words.cancelled, style: const TextStyle(fontSize: ErpType.bodySmall))
            else ...[
              Text(
                outcome,
                key: const ValueKey('assistant-outcome'),
                textDirection: assistantDirection(outcome),
                style: TextStyle(fontSize: ErpType.bodySmall, color: card.status == 'done' ? null : ErpColors.danger),
              ),
              if (target != null)
                Align(
                  alignment: AlignmentDirectional.centerStart,
                  child: TextButton(onPressed: () => Navigator.of(context).push(MaterialPageRoute<void>(builder: target)), child: Text(words.open)),
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
    return AssistantScope(
      child: Padding(
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
