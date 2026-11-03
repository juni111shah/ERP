import 'package:erp_mobile/features/intro/intro_motion.dart';
import 'package:flutter/widgets.dart';
import 'package:flutter_test/flutter_test.dart';

bool _finite(Matrix4 m) => m.storage.every((v) => v.isFinite);

void main() {
  test('intervals inside 0..1 and ordered', () {
    for (final l in IntroLayer.values) {
      for (var i = 0; i < 4; i++) {
        final iv = IntroMotion.interval(l, index: i);
        expect(iv.begin, inInclusiveRange(0, 1));
        expect(iv.end, inInclusiveRange(0, 1));
        expect(iv.end, greaterThan(iv.begin));
      }
    }
  });

  test('t=1 rests, t=0 differs, no NaN, RTL mirrors', () {
    for (final l in IntroLayer.values) {
      final a = IntroMotion.layerTransform(l, 0, 0, 0);
      final b = IntroMotion.layerTransform(l, 1, 0, 0);
      expect(_finite(a) && _finite(b), isTrue);
      if (l != IntroLayer.floor) expect(a == b, isFalse);
      expect(_finite(IntroMotion.layerTransform(l, double.nan, double.infinity, double.nan)), isTrue);
    }
    final ltr = IntroMotion.layerTransform(IntroLayer.hero, 1, 0.5, 0.25);
    final rtl = IntroMotion.layerTransform(IntroLayer.hero, 1, 0.5, 0.25, rtl: true);
    // Mirror about the Y axis: x-coupled terms flip sign.
    expect(rtl.entry(0, 1), closeTo(-ltr.entry(0, 1), 1e-9));
    expect(rtl.entry(0, 2), closeTo(-ltr.entry(0, 2), 1e-9));
    expect(rtl.entry(1, 1), closeTo(ltr.entry(1, 1), 1e-9));
  });

  test('reduced motion is identity', () {
    for (final l in IntroLayer.values) {
      expect(IntroMotion.layerTransform(l, 0, 0.7, 0.3, reduceMotion: true), Matrix4.identity());
    }
  });

  test('shadow grows with lift', () {
    var prev = IntroMotion.shadowForLift(0);
    for (var z = 10.0; z <= 150; z += 10) {
      final s = IntroMotion.shadowForLift(z);
      expect(s.blurRadius, greaterThan(prev.blurRadius));
      expect(s.offset.dy, greaterThan(prev.offset.dy));
      expect(s.color.a, greaterThan(prev.color.a));
      prev = s;
    }
    expect(IntroMotion.floorPerspective().entry(3, 2), isNonZero);
  });
}
