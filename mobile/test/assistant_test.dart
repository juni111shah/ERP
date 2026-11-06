import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:erp_mobile/core/api_client.dart';
import 'package:erp_mobile/core/key_value_store.dart';
import 'package:erp_mobile/core/me.dart';
import 'package:erp_mobile/core/palette_registry.dart';
import 'package:erp_mobile/core/session.dart';
import 'package:erp_mobile/features/assistant/assistant_api.dart';
import 'package:erp_mobile/features/assistant/assistant_page.dart';
import 'package:erp_mobile/features/assistant/assistant_stream.dart';
import 'package:erp_mobile/features/assistant/assistant_text.dart';
import 'package:erp_mobile/features/more/more_page.dart';
import 'package:erp_mobile/kit/data_version.dart';
import 'package:erp_mobile/theme/app_theme.dart';
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_riverpod/misc.dart' show Override;
import 'package:flutter_test/flutter_test.dart';

import 'fakes/fake_session.dart';
import 'support/fake_adapter.dart';

const _base = '/api/v1/m/assistant';

Me _me({Set<String> permissions = const {'assistant.chats.use'}, Set<String> modules = const {'assistant'}}) =>
    Me(fullName: 'A', email: 'a@a.test', baseCurrency: 'QAR', countryCode: 'QA', permissions: permissions, legalEntities: const [], modules: modules);

String _sse(String event, Object data) => 'event: $event\ndata: ${jsonEncode(data)}\n\n';

ResponseBody _streamed(Stream<List<int>> bytes, {int status = 200}) =>
    ResponseBody(bytes.map(Uint8List.fromList), status, headers: {Headers.contentTypeHeader: ['text/event-stream; charset=utf-8']});

ResponseBody _sseOnce(String text) => _streamed(Stream.value(utf8.encode(text)));

const _menu = [
  MenuGroup(id: 'g', label: 'G', items: [MenuItem(id: 'finance.invoices', label: 'Invoices', route: '/finance/invoices', module: 'finance')]),
];

List<Override> _overrides(FakeAdapter adapter, {Me? me}) {
  final store = MemoryKeyValueStore();
  final who = me ?? _me();
  return [
    storeProvider.overrideWithValue(store),
    initialServerUrlProvider.overrideWithValue('http://api.test'),
    apiProvider.overrideWith((ref) => ApiClient(store: store, baseUrl: 'http://api.test', dio: Dio()
      ..transformer = SyncTransformer()
      ..httpClientAdapter = adapter)..accessToken = 'token'),
    sessionProvider.overrideWith(() => FakeSessionController()),
    meProvider.overrideWith((ref) async => who),
    meOrEmptyProvider.overrideWithValue(who),
    menuProvider.overrideWith((ref) async => _menu),
  ];
}

Widget _host(Widget child, FakeAdapter adapter, {Me? me}) => ProviderScope(
      overrides: _overrides(adapter, me: me),
      child: MaterialApp(theme: AppTheme.light(), home: child),
    );

/// The rendered text of a [Text] found by its words, for looking at what is tappable.
List<InlineSpan> _spansOf(WidgetTester t, String words) {
  final paragraph = t.renderObject<RenderParagraph>(find.textContaining(words, findRichText: true));
  final out = <InlineSpan>[];
  paragraph.text.visitChildren((s) {
    out.add(s);
    return true;
  });
  return out;
}

void main() {
  group('SSE parser', () {
    test('reads events, skips keep-alives and bad JSON', () {
      final events = AssistantStreamParser().add(utf8.encode(': keep-alive\n\n'
          '${_sse('conversation', {'id': 'c1'})}'
          '${_sse('status', {'text': 'Looking'})}'
          'event: delta\ndata: {broken\n\n'
          '${_sse('delta', {'text': 'Hi'})}'
          '${_sse('service', {'name': 'x'})}'
          '${_sse('error', {'code': 'off', 'message': 'Not now'})}'
          '${_sse('done', {'inputTokens': 1, 'outputTokens': 2})}'));
      expect(events.map((e) => e.runtimeType), [AssistantConversationEvent, AssistantStatusEvent, AssistantDeltaEvent, AssistantErrorEvent]);
      expect((events[0] as AssistantConversationEvent).id, 'c1');
      expect((events[3] as AssistantErrorEvent).message, 'Not now');
    });

    test('an event split across chunks arrives once it is whole', () {
      final parser = AssistantStreamParser();
      final bytes = utf8.encode(_sse('delta', {'text': 'Hello'}));
      final seen = <AssistantEvent>[];
      for (final b in bytes) {
        seen.addAll(parser.add([b]));
        if (b != bytes.last) expect(seen, isEmpty, reason: 'nothing until the blank line');
      }
      expect((seen.single as AssistantDeltaEvent).text, 'Hello');
    });

    test('a character split between chunks is not damaged', () {
      const text = 'مرحبا 🙂 é';
      final bytes = utf8.encode(_sse('delta', {'text': text}));
      // Inside the two-byte first Arabic letter.
      final at = utf8.encode('event: delta\ndata: {"text":"').length + 1;
      final parser = AssistantStreamParser();
      expect(parser.add(bytes.sublist(0, at)), isEmpty);
      final events = parser.add(bytes.sublist(at));
      expect((events.single as AssistantDeltaEvent).text, text);
    });

    test('a card carries the server\'s lines and status', () {
      final events = AssistantStreamParser().add(utf8.encode(_sse('action', {
        'id': 'a1',
        'title': 'Create customer',
        'lines': [
          {'label': 'Name', 'value': 'Acme'}
        ],
        'status': 'pending',
        'message': null,
        'link': null,
        'createdAt': '2026-11-06T10:00:00Z',
      })));
      final card = (events.single as AssistantActionEvent).card;
      expect((card.id, card.status, card.lines.single.value), ('a1', 'pending', 'Acme'));
    });
  });

  group('reply text', () {
    List<AssistantInline> para(String text) => (parseAssistantText(text).single as AssistantParagraph).parts;

    test('bold and internal links are kept', () {
      final parts = para('Open **now**: [Acme](/parties/123)');
      expect(parts.map((p) => p.text), ['Open ', 'now', ': ', 'Acme']);
      expect(parts[1].bold, isTrue);
      expect(parts[3].link, '/parties/123');
    });

    test('external links lose their address', () {
      for (final href in ['https://evil.test/x', '//evil.test', r'/\evil.test', r'/a\b', 'javascript:alert(1)', 'mailto:a@b.c']) {
        final parts = para('See [the words]($href)');
        expect(parts.map((p) => p.text), contains('the words'), reason: href);
        expect(parts.every((p) => p.link == null), isTrue, reason: href);
      }
    });

    test('an image or a tag stays plain words', () {
      final image = para('![logo](https://evil.test/a.png)');
      expect(image.every((p) => p.link == null), isTrue);
      expect(para('<img src=x onerror=y>').single.text, '<img src=x onerror=y>');
    });

    test('table rows become list items and the heading row is dropped', () {
      final blocks = parseAssistantText('Who owes:\n| Name | Amount |\n|---|---|\n| Acme | 100 |\n| **Beta** | [200](/parties/2) |');
      expect(blocks.length, 2);
      final list = blocks[1] as AssistantList;
      expect(list.ordered, isFalse);
      expect(list.items.map((i) => i.map((p) => p.text).join()), ['Acme · 100', 'Beta · 200']);
    });

    test('lists and headings', () {
      final blocks = parseAssistantText('## Title\n- one\n* two\n\n1. first\n2) second');
      expect(((blocks[0] as AssistantParagraph).parts.single).text, 'Title');
      expect((blocks[1] as AssistantList).items.length, 2);
      expect((blocks[2] as AssistantList).ordered, isTrue);
    });

    test('direction comes from the first letter', () {
      expect(assistantDirection('مرحبا'), TextDirection.rtl);
      expect(assistantDirection('123 - مرحبا Hi'), TextDirection.rtl);
      expect(assistantDirection('Hi مرحبا'), TextDirection.ltr);
      expect(assistantDirection('123 !'), isNull);
    });
  });

  group('entry visibility', () {
    Future<(bool, FakeAdapter)> visible(Me me, ResponseBody Function(RequestOptions) answer) async {
      final adapter = FakeAdapter(answer);
      final container = ProviderContainer(overrides: _overrides(adapter, me: me));
      addTearDown(container.dispose);
      return (await container.read(assistantAvailableProvider.future), adapter);
    }

    ResponseBody on(RequestOptions _) => envelope({'available': true});

    test('needs the permission, without asking the server', () async {
      final (shown, adapter) = await visible(_me(permissions: const {'finance.invoices.read'}), on);
      expect(shown, isFalse);
      expect(adapter.requests, isEmpty);
    });

    test('needs the module, even for an owner, without asking the server', () async {
      final (shown, adapter) = await visible(_me(permissions: const {'*'}, modules: const {'sales'}), on);
      expect(shown, isFalse);
      expect(adapter.requests, isEmpty);
    });

    test('follows the service status', () async {
      expect((await visible(_me(), on)).$1, isTrue);
      expect((await visible(_me(permissions: const {'*'}), on)).$1, isTrue);
      expect((await visible(_me(), (_) => envelope({'available': false}))).$1, isFalse);
    });

    test('a failed status call hides it without an error', () async {
      expect((await visible(_me(), (_) => problem(403, 'module-disabled'))).$1, isFalse);
      expect((await visible(_me(), (_) => problem(500, 'boom'))).$1, isFalse);
      expect((await visible(_me(), (_) => throw DioException.connectionError(requestOptions: RequestOptions(), reason: 'x'))).$1, isFalse);
    });

    testWidgets('the More tab shows the entry only when available', (t) async {
      await t.pumpWidget(_host(const Scaffold(body: MorePage()), FakeAdapter((_) => envelope({'available': true}))));
      await t.pumpAndSettle();
      expect(find.byKey(const ValueKey('assistant-entry')), findsOneWidget);
    });

    testWidgets('the More tab has no entry and no error when it is off', (t) async {
      await t.pumpWidget(_host(const Scaffold(body: MorePage()), FakeAdapter((_) => problem(500, 'boom'))));
      await t.pumpAndSettle();
      expect(find.byKey(const ValueKey('assistant-entry')), findsNothing);
      expect(find.textContaining('boom'), findsNothing);
    });

    test('the palette lists it only when given, with its own words', () {
      expect(buildPaletteEntries().where((e) => e.id == 'screen.assistant'), isEmpty);
      final entry = buildPaletteEntries(screens: [assistantScreen]).singleWhere((e) => e.id == 'screen.assistant');
      expect(entry.data, isA<ScreenTarget>());
      expect(entry.subtitle, isNot('Open Assistant'));
    });
  });

  group('API', () {
    ApiClient client(FakeAdapter adapter, {MemoryKeyValueStore? store}) =>
        ApiClient(store: store ?? MemoryKeyValueStore(), baseUrl: 'http://api.test', dio: Dio()..httpClientAdapter = adapter)..accessToken = 'token';

    test('an error before the first event comes as the normal envelope', () async {
      final api = AssistantApi(client(FakeAdapter((_) => ResponseBody.fromString(
          jsonEncode({'success': false, 'statusCode': 400, 'message': 'Validation failed', 'errors': {'text': ['Type a question.']}}), 400))));
      final error = await api.ask(conversationId: null, text: 'x', cancelToken: CancelToken()).first.then<Object?>((_) => null, onError: (Object e) => e);
      expect(error, isA<ApiException>());
      expect(assistantErrorMessage(error!, 'fallback'), 'Type a question.');
    });

    test('a server failure\'s detail is not shown', () {
      expect(assistantErrorMessage(ApiException(500, 'NullReference at line 4'), 'fallback'), 'fallback');
      expect(assistantErrorMessage(ApiException(0, 'offline'), 'fallback'), 'fallback');
      expect(assistantErrorMessage(ApiException(403, 'The module is off.'), 'fallback'), 'The module is off.');
      expect(assistantErrorMessage(StateError('x'), 'fallback'), 'fallback');
    });

    test('a 401 renews the session once and asks again', () async {
      var asks = 0;
      final store = MemoryKeyValueStore()..values[StoreKeys.refreshCookie] = 'cookie';
      final adapter = FakeAdapter((o) {
        if (o.path.endsWith('/auth/refresh')) return envelope(sessionBody('fresh'));
        return asks++ == 0 ? problem(401, 'expired') : _sseOnce(_sse('delta', {'text': 'Hi'}));
      });
      final api = AssistantApi(client(adapter, store: store));
      final events = await api.ask(conversationId: null, text: 'x', cancelToken: CancelToken()).toList();
      expect((events.single as AssistantDeltaEvent).text, 'Hi');
      expect(adapter.requests.last.headers['Authorization'], 'Bearer fresh');
    });

    test('messages are sent as the web sends them, and read as a stream', () async {
      final adapter = FakeAdapter((_) => _sseOnce(_sse('delta', {'text': 'Hi'})));
      await AssistantApi(client(adapter)).ask(conversationId: 'c1', text: 'Hello', cancelToken: CancelToken()).toList();
      final o = adapter.requests.single;
      expect((o.method, o.path), ('POST', '$_base/messages'));
      expect(o.responseType, ResponseType.stream);
      expect(o.data, {'conversationId': 'c1', 'text': 'Hello', 'page': null});
    });

    test('a saved chat puts messages and cards in time order', () {
      final detail = AssistantConversationDetail.fromJson({
        'id': 'c1',
        'title': 'T',
        'messages': [
          {'at': '2026-11-06T10:00:00.5Z', 'fromUser': true, 'text': 'one'},
          {'at': '2026-11-06T10:00:02Z', 'fromUser': false, 'text': 'three'},
        ],
        'actions': [
          {'id': 'a', 'title': 'two', 'lines': [], 'status': 'done', 'createdAt': '2026-11-06T10:00:00.51Z'},
        ],
      });
      expect(detail.items[0], isA<AssistantUserItem>());
      expect((detail.items[1] as AssistantCardItem).card.title, 'two');
      expect(detail.items[2], isA<AssistantReplyItem>());
    });
  });

  group('screen', () {
    final finder = find.byKey;
    const input = ValueKey('assistant-input');
    const send = ValueKey('assistant-send');
    const stop = ValueKey('assistant-stop');

    /// A server whose reply the test writes piece by piece.
    (FakeAdapter, StreamController<List<int>>) replying({ResponseBody Function(RequestOptions)? other}) {
      final reply = StreamController<List<int>>();
      return (
        FakeAdapter((o) => o.path == '$_base/messages' ? _streamed(reply.stream) : (other?.call(o) ?? envelope(<String, dynamic>{}))),
        reply,
      );
    }

    /// Dio takes several turns of the event loop under the test clock, and pumpAndSettle can stop before they are done.
    Future<void> turns(WidgetTester t) async {
      for (var i = 0; i < 20; i++) {
        await t.pump(const Duration(milliseconds: 1));
      }
    }

    Future<void> ask(WidgetTester t, String text) async {
      await t.enterText(finder(input), text);
      await turns(t);
      await t.tap(finder(send));
      await turns(t);
    }

    Map<String, dynamic> card({String status = 'pending', String? message, String? link}) =>
        {'id': 'a1', 'title': 'Create customer', 'lines': [{'label': 'Name', 'value': 'Acme'}], 'status': status, 'message': message, 'link': link, 'createdAt': '2026-11-06T10:00:00Z'};

    testWidgets('an empty chat offers suggestions, and one sends straight away', (t) async {
      final (adapter, reply) = replying();
      await t.pumpWidget(_host(const AssistantPage(), adapter));
      await turns(t);
      expect(find.text(AssistantWords.en.hello), findsOneWidget);
      await t.tap(find.text(AssistantWords.en.suggestions[1]));
      await turns(t);
      expect(adapter.requests.single.data['text'], AssistantWords.en.suggestions[1]);
      expect(find.text(AssistantWords.en.hello), findsNothing);
      unawaited(reply.close());
      await turns(t);
    });

    testWidgets('a reply streams in with its status line, and Stop shows only while replying', (t) async {
      final reply = StreamController<List<int>>();
      var asked = 0;
      final adapter = FakeAdapter((_) => asked++ == 0 ? _streamed(reply.stream) : _sseOnce(_sse('delta', {'text': 'Done'})));
      await t.pumpWidget(_host(const AssistantPage(), adapter));
      await ask(t, 'Hello');
      expect(find.text('Hello'), findsOneWidget);
      expect(find.text('Working...'), findsOneWidget);
      expect(finder(stop), findsOneWidget);

      reply.add(utf8.encode(_sse('conversation', {'id': 'c1'}) + _sse('status', {'text': 'Looking up records'})));
      await turns(t);
      expect(find.text('Looking up records...'), findsOneWidget);

      reply.add(utf8.encode(_sse('delta', {'text': 'Hi '})));
      await turns(t);
      expect(find.textContaining('Hi', findRichText: true), findsOneWidget);
      expect(find.byKey(const ValueKey('assistant-status')), findsNothing);
      reply.add(utf8.encode(_sse('delta', {'text': 'there'})));
      await turns(t);
      expect(find.textContaining('Hi there', findRichText: true), findsOneWidget);

      unawaited(reply.close());
      await turns(t);
      expect(finder(stop), findsNothing);
      expect(finder(send), findsOneWidget);

      // The next question continues the saved chat.
      await ask(t, 'More');
      expect(adapter.requests.last.data['conversationId'], 'c1');
    });

    testWidgets('Stop closes the connection and keeps what was written', (t) async {
      var closed = false;
      final reply = StreamController<List<int>>(onCancel: () => closed = true);
      final adapter = FakeAdapter((_) => _streamed(reply.stream));
      await t.pumpWidget(_host(const AssistantPage(), adapter));
      await ask(t, 'Hello');
      reply.add(utf8.encode(_sse('delta', {'text': 'Partial'})));
      await turns(t);
      await t.tap(finder(stop));
      await turns(t);
      expect(adapter.requests.single.cancelToken?.isCancelled, isTrue);
      expect(closed, isTrue);
      expect(finder(send), findsOneWidget);
      expect(find.textContaining('Partial', findRichText: true), findsOneWidget);
    });

    testWidgets('a failure before the first event is shown', (t) async {
      final adapter = FakeAdapter((_) => problem(403, 'The module is off.'));
      await t.pumpWidget(_host(const AssistantPage(), adapter));
      await ask(t, 'Hello');
      await turns(t);
      expect(find.text('The module is off.'), findsOneWidget);
      expect(finder(send), findsOneWidget);
    });

    testWidgets('an error event is shown', (t) async {
      final (adapter, reply) = replying();
      await t.pumpWidget(_host(const AssistantPage(), adapter));
      await ask(t, 'Hello');
      reply.add(utf8.encode(_sse('error', {'code': 'user-limit', 'message': 'Try tomorrow.'})));
      unawaited(reply.close());
      await turns(t);
      expect(find.text('Try tomorrow.'), findsOneWidget);
    });

    testWidgets('a card changes nothing until Confirm, which sends the idempotency key', (t) async {
      final (adapter, reply) = replying(other: (_) => envelope(card(status: 'done', message: 'Created Acme.', link: '/finance/invoices')));
      late ProviderContainer container;
      await t.pumpWidget(_host(Consumer(builder: (context, ref, _) {
        container = ProviderScope.containerOf(context);
        return const AssistantPage();
      }), adapter));
      await ask(t, 'Add Acme');
      reply.add(utf8.encode(_sse('action', card())));
      unawaited(reply.close());
      await turns(t);
      expect(find.text('Create customer'), findsOneWidget);
      expect(find.text('Acme'), findsOneWidget);
      expect(adapter.requests.where((o) => o.path.contains('/actions/')), isEmpty);

      await t.tap(finder(const ValueKey('assistant-confirm')));
      await turns(t);
      final confirm = adapter.requests.singleWhere((o) => o.path.contains('/actions/'));
      expect((confirm.method, confirm.path), ('POST', '$_base/actions/a1/confirm'));
      expect(confirm.headers['Idempotency-Key'], 'assistant-action-a1');
      expect(find.text('Created Acme.'), findsOneWidget);
      expect(finder(const ValueKey('assistant-confirm')), findsNothing);
      expect(container.read(dataVersionProvider), 1);
    });

    testWidgets('Cancel calls cancel with no key, and a card in another state offers no buttons', (t) async {
      final (adapter, reply) = replying(other: (_) => envelope(card(status: 'cancelled')));
      await t.pumpWidget(_host(const AssistantPage(), adapter));
      await ask(t, 'Add Acme');
      reply.add(utf8.encode(_sse('action', card())));
      unawaited(reply.close());
      await turns(t);
      await t.tap(finder(const ValueKey('assistant-cancel')));
      await turns(t);
      final cancel = adapter.requests.singleWhere((o) => o.path.contains('/actions/'));
      expect(cancel.path, '$_base/actions/a1/cancel');
      expect(cancel.headers.containsKey('Idempotency-Key'), isFalse);
      expect(find.text(AssistantWords.en.cancelled), findsOneWidget);
      expect(finder(const ValueKey('assistant-confirm')), findsNothing);
    });

    testWidgets('history lists chats, opens one, deletes one, and new chat clears', (t) async {
      final adapter = FakeAdapter((o) {
        if (o.method == 'DELETE') return envelope(null);
        if (o.path == '$_base/conversations') {
          return envelope([
            {'id': 'c1', 'title': 'Overdue invoices', 'lastMessageAt': '2026-11-06T10:00:00Z'},
            {'id': 'c2', 'title': 'Stock', 'lastMessageAt': '2026-11-05T10:00:00Z'},
          ]);
        }
        return envelope({
          'id': 'c1',
          'title': 'Overdue invoices',
          'messages': [
            {'at': '2026-11-06T10:00:00Z', 'fromUser': true, 'text': 'Which are overdue?'}
          ],
          'actions': <dynamic>[],
        });
      });
      await t.pumpWidget(_host(const AssistantPage(), adapter));
      await t.tap(finder(const ValueKey('assistant-history')));
      await turns(t);
      expect(find.text('Stock'), findsOneWidget);

      await t.tap(find.byTooltip(AssistantWords.en.remove).last);
      await turns(t);
      expect(adapter.requests.last.path, '$_base/conversations/c2');
      expect(find.text('Stock'), findsNothing);

      await t.tap(find.text('Overdue invoices'));
      await turns(t);
      expect(find.text('Which are overdue?'), findsOneWidget);

      await t.tap(finder(const ValueKey('assistant-new')));
      await turns(t);
      expect(find.text('Which are overdue?'), findsNothing);
      expect(find.text(AssistantWords.en.hello), findsOneWidget);
    });

    testWidgets('only links the app can open are tappable', (t) async {
      await t.pumpWidget(_host(
        const Scaffold(
          body: AssistantReplyText(text: 'Open [Invoices](/finance/invoices), [Acme](/parties/123) or [site](https://evil.test)'),
        ),
        FakeAdapter((_) => envelope(null)),
      ));
      await turns(t);
      final tappable = _spansOf(t, 'Invoices').whereType<TextSpan>().where((s) => s.recognizer != null).map((s) => s.text);
      expect(tappable, ['Invoices']);
      expect(find.textContaining('evil.test', findRichText: true), findsNothing);
      expect(find.textContaining('/parties', findRichText: true), findsNothing);
    });

    testWidgets('an Arabic reply reads right to left and an English one left to right', (t) async {
      await t.pumpWidget(_host(
        const Scaffold(body: Column(children: [AssistantReplyText(text: 'مرحبا بك'), AssistantReplyText(text: 'Welcome')])),
        FakeAdapter((_) => envelope(null)),
      ));
      await turns(t);
      expect(t.renderObject<RenderParagraph>(find.textContaining('مرحبا', findRichText: true)).textDirection, TextDirection.rtl);
      expect(t.renderObject<RenderParagraph>(find.textContaining('Welcome', findRichText: true)).textDirection, TextDirection.ltr);
    });

    testWidgets('in a right-to-left layout the words are Arabic and tap targets hold', (t) async {
      await t.binding.setSurfaceSize(const Size(360, 780));
      addTearDown(() => t.binding.setSurfaceSize(null));
      final handle = t.ensureSemantics();
      await t.pumpWidget(ProviderScope(
        overrides: _overrides(FakeAdapter((_) => envelope(null))),
        child: MaterialApp(
          theme: AppTheme.light(),
          builder: (context, inner) => Directionality(textDirection: TextDirection.rtl, child: inner!),
          home: const AssistantPage(),
        ),
      ));
      await turns(t);
      expect(find.text(AssistantWords.ar.hello), findsOneWidget);
      await expectLater(t, meetsGuideline(androidTapTargetGuideline));
      await expectLater(t, meetsGuideline(labeledTapTargetGuideline));
      handle.dispose();
    });

    testWidgets('the mark paints', (t) async {
      await t.pumpWidget(const MaterialApp(home: Center(child: AssistantMark(size: 64))));
      expect(t.takeException(), isNull);
      expect(find.byType(CustomPaint), findsWidgets);
    });
  });
}
