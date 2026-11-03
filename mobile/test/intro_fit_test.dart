import 'dart:io';

import 'package:erp_mobile/core/key_value_store.dart';
import 'package:erp_mobile/core/session.dart';
import 'package:erp_mobile/features/intro/intro_page.dart';
import 'package:erp_mobile/features/intro/intro_strings.dart';
import 'package:erp_mobile/theme/tokens.dart';
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';

Future<ByteData> _font(String name) async => ByteData.sublistView(Uint8List.fromList(await File('assets/fonts/$name').readAsBytes()));

/// Every line of tour text (illustration and captions) shows in full: no RenderFlex overflow, no ellipsis or clipped
/// line, at a small (360x780) and a common (390x844) phone size, in the real Inter / Noto Sans Arabic fonts.
void main() {
  setUpAll(() async {
    final inter = FontLoader(ErpFonts.family);
    for (final w in ['Regular', 'Medium', 'SemiBold', 'Bold']) {
      inter.addFont(_font('Inter-$w.ttf'));
    }
    await inter.load();
    final arabic = FontLoader(ErpFonts.fallback.first);
    for (final w in ['Regular', 'Medium', 'SemiBold', 'Bold']) {
      arabic.addFont(_font('NotoSansArabic-$w.ttf'));
    }
    await arabic.load();
  });

  Future<void> walk(WidgetTester tester, Size size, {bool rtl = false}) async {
    tester.view.physicalSize = size * 3;
    tester.view.devicePixelRatio = 3;
    addTearDown(tester.view.reset);
    await tester.pumpWidget(ProviderScope(
      overrides: [storeProvider.overrideWithValue(MemoryKeyValueStore())],
      child: MaterialApp(
        theme: ThemeData(fontFamily: ErpFonts.family, fontFamilyFallback: ErpFonts.fallback),
        home: Directionality(textDirection: rtl ? TextDirection.rtl : TextDirection.ltr, child: const IntroPage()),
      ),
    ));
    await tester.pumpAndSettle();
    final s = rtl ? IntroStrings.ar : IntroStrings.en;
    for (var n = 1; n <= 5; n++) {
      expect(find.text(s.step(n, 5)), findsOneWidget);
      final cut = [
        for (final p in tester.allRenderObjects.whereType<RenderParagraph>())
          if (p.didExceedMaxLines) p.text.toPlainText(),
      ];
      expect(cut, isEmpty, reason: 'truncated text on screen $n at $size');
      expect(tester.takeException(), isNull);
      if (n < 5) {
        await tester.tap(find.text(s.next));
        await tester.pumpAndSettle();
      }
    }
    expect(find.text(s.getStarted), findsOneWidget);
    for (final f in s.features) {
      expect(find.text(f), findsOneWidget);
    }
  }

  testWidgets('all text fits at 360x780', (tester) => walk(tester, const Size(360, 780)));
  testWidgets('all text fits at 390x844', (tester) => walk(tester, const Size(390, 844)));
  testWidgets('all Arabic text fits at 360x780', (tester) => walk(tester, const Size(360, 780), rtl: true));
}
