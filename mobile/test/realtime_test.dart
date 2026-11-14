import 'package:erp_mobile/core/realtime.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('hub address', () {
    test('sits next to the API, over ws or wss, with the token', () {
      expect(hubUri('http://10.0.2.2:5080', 'abc').toString(), 'ws://10.0.2.2:5080/hubs/notifications?access_token=abc');
      expect(hubUri('https://erp.example.test', 't').toString(), 'wss://erp.example.test/hubs/notifications?access_token=t');
    });
  });

  group('hub frames', () {
    test('keep invocations with their first argument', () {
      final events = parseHubFrame('{}$signalRSeparator{"type":1,"target":"access-changed","arguments":[{"orgId":"o1","reason":"role-changed"}]}$signalRSeparator');
      expect(events, hasLength(1));
      expect(events.single.target, 'access-changed');
      expect((events.single.argument as Map)['reason'], 'role-changed');
    });

    test('skip pings and noise, and report a close', () {
      final events = parseHubFrame('{"type":6}${signalRSeparator}not json$signalRSeparator{"type":7,"error":"bye"}$signalRSeparator');
      expect(events.map((e) => e.target), ['close']);
    });

    test('carry several messages in one frame', () {
      final events = parseHubFrame('{"type":1,"target":"notification","arguments":[]}$signalRSeparator{"type":1,"target":"access-changed","arguments":[{}]}$signalRSeparator');
      expect(events.map((e) => e.target), ['notification', 'access-changed']);
      expect(events.first.argument, isNull);
    });
  });
}
