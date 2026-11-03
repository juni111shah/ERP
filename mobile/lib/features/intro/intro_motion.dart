import 'dart:math' as math;

import 'package:flutter/widgets.dart';

/// Motion tokens and helpers for the first-open intro tour, Concept B "3D depth".
///
/// Values come from `intro-mockup.html` (CSS keyframes `rise`, `par`, `.floor`,
/// `.glow`) and are refined in `motion-spec.md`. Flutter built-ins only.
///
/// Usage (one [AnimationController] per page for the entrance, one shared
/// repeating controller for the ambient drift):
///
/// ```dart
/// final reduce = IntroMotion.reduce(context);
/// final local = IntroMotion.interval(IntroLayer.hero, index: 0)
///     .transform(entrance.value); // raw 0..1 for this layer
/// Transform(
///   alignment: Alignment.center,
///   transform: IntroMotion.layerTransform(
///     IntroLayer.hero, local, pageOffset, drift.value,
///     rtl: Directionality.of(context) == TextDirection.rtl,
///     reduceMotion: reduce,
///     screen: pageIndex,
///   ),
///   child: Opacity(opacity: IntroMotion.opacityFor(local), child: card),
/// )
/// ```
///
/// `pageOffset` = `controller.page! - pageIndex` (0 when settled, -1..1 while
/// swiping). `drift` is a 0..1 sawtooth from a 6 s repeating controller.

/// Depth layers, back to front.
enum IntroLayer {
  /// Perspective grid + horizon glow. Never rises; fades in.
  floor,

  /// The tilted base slab (green underside) and its blurred ground shadow.
  back,

  /// Main content card(s) on the slab: KPI grid, chart, invoice rows, sheet, palette.
  hero,

  /// Floating chips, toasts and badges with the highest Z.
  foreground,

  /// Title + body copy below the illustration. 2D only.
  caption,
}

/// Static timing for one layer: delay and duration in ms, plus parallax and depth.
@immutable
class IntroLayerSpec {
  const IntroLayerSpec({
    required this.delayMs,
    required this.durationMs,
    required this.parallax,
    required this.z,
    required this.driftPhase,
    this.childStaggerMs = 0,
  });

  /// Entrance start, from the page's entrance controller start.
  final int delayMs;

  /// Entrance length.
  final int durationMs;

  /// Horizontal travel vs the PageView (1.0 = moves exactly with the page).
  final double parallax;

  /// Resting depth in dp (positive = towards the viewer). Drives the shadow.
  final double z;

  /// Ambient drift phase offset (fraction of a period).
  final double driftPhase;

  /// Extra delay per child index inside the layer (KPI cards, invoice rows).
  final int childStaggerMs;
}

/// Constants, curves and transform helpers for the intro tour.
abstract final class IntroMotion {
  // ---------------------------------------------------------------- timing
  /// Total entrance timeline per page (last foreground child lands ~1.55 s).
  static const int totalMs = 1600;

  /// Page-to-page settle when tapping Next (PageController.animateToPage).
  static const Duration pageTurn = Duration(milliseconds: 420);

  /// Ambient drift period.
  static const Duration driftPeriod = Duration(milliseconds: 6000);

  /// Reduced-motion crossfade (whole page, no 3D).
  static const Duration reducedFade = Duration(milliseconds: 200);

  /// Per-layer timing. Child stagger applies to indexed children of a layer.
  static const Map<IntroLayer, IntroLayerSpec> layers = {
    IntroLayer.floor: IntroLayerSpec(delayMs: 0, durationMs: 700, parallax: 0.2, z: -200, driftPhase: 0),
    IntroLayer.back: IntroLayerSpec(delayMs: 0, durationMs: 900, parallax: 0.5, z: 0, driftPhase: 0),
    IntroLayer.hero: IntroLayerSpec(
        delayMs: 220, durationMs: 800, parallax: 1.0, z: 50, driftPhase: 0.08, childStaggerMs: 110),
    IntroLayer.foreground: IntroLayerSpec(delayMs: 900, durationMs: 650, parallax: 1.3, z: 110, driftPhase: 0.16),
    IntroLayer.caption: IntroLayerSpec(
        delayMs: 450, durationMs: 500, parallax: 1.0, z: 0, driftPhase: 0, childStaggerMs: 120),
  };

  // ---------------------------------------------------------------- curves
  /// 3D rise. Mockup `cubic-bezier(.17,.84,.3,1.08)`: fast lift, ~3 % overshoot.
  static const Curve rise = Cubic(0.17, 0.84, 0.3, 1.08);

  /// 2D fades/slides (caption, CTA, floor). Mockup `cubic-bezier(.2,.8,.2,1)`.
  static const Curve settle = Cubic(0.2, 0.8, 0.2, 1.0);

  /// Opacity reaches 1 at 35 % of the rise (mockup keyframe `35%{opacity:1}`).
  static const Curve fadeIn = Interval(0.0, 0.35, curve: Curves.easeOut);

  /// Ambient drift (mockup `par` ease-in-out). Implemented as cosine below.
  static const Curve drift = Curves.easeInOut;

  /// Page settle after a fling / Next tap.
  static const Curve pageCurve = Cubic(0.2, 0.8, 0.2, 1.0);

  /// Optional spring for the indicator pill and CTA press.
  static const SpringDescription pillSpring = SpringDescription(mass: 1, stiffness: 420, damping: 32);

  // ---------------------------------------------------------------- geometry
  /// CSS `perspective:1100px`. Flutter keeps CSS's axes (+y down) and only drops z after the divide, so the matrix
  /// entry (3,2) is `-perspective` (w = 1 - z/1100): +z then comes toward the viewer exactly as in the mockup.
  static const double perspective = 1 / 1100;

  /// After this many seconds on one screen the ambient drift has eased back to rest and the page stops ticking
  /// (two [driftPeriod]s). Saves battery and lets tests settle.
  static const double driftWindowSeconds = 12;

  /// Envelope (0..1) for the ambient drift at [seconds] after the screen became active: eases in, eases out,
  /// 0 after [driftWindowSeconds].
  static double driftAmount(double seconds) {
    if (!seconds.isFinite || seconds <= 0 || seconds >= driftWindowSeconds) return 0;
    return math.sin(math.pi * seconds / driftWindowSeconds);
  }

  /// Floor grid forward travel in grid cells, starting and stopping with zero speed.
  static double floorTravel(double seconds) {
    final s = (seconds.isFinite ? seconds : 0.0).clamp(0.0, driftWindowSeconds);
    return 0.6 * (0.5 - 0.5 * math.cos(math.pi * s / driftWindowSeconds));
  }

  /// Horizontal parallax shift (dp) for [layer] at [pageOffset]; what [layerTransform] adds, for 2D-only pieces
  /// like the floor.
  static double parallaxDx(IntroLayer layer, double pageOffset, {bool rtl = false, double pageWidth = 390}) {
    final off = _finite(pageOffset.clamp(-1.0, 1.0));
    return -off * pageWidth * (layers[layer]!.parallax - 1.0) * (rtl ? -1.0 : 1.0);
  }

  /// Stage scale (mockup `.stage{transform:scale(.84)}`).
  static const double stageScale = 0.84;

  /// Rise start: translate (0, +140, -260) dp, rotateX -55 deg, rotateZ -8 deg, scale 0.92.
  static const double riseDy = 140;
  static const double riseDz = -260;
  static const double riseRotX = -55 * math.pi / 180; // -0.9599 rad
  static const double riseRotZ = -8 * math.pi / 180; // -0.1396 rad
  static const double riseRotY = -10 * math.pi / 180; // -0.1745 rad (LTR)
  static const double riseScale = 0.92;

  /// Foreground chips use a softer start so they don't cross the hero.
  static const double fgRiseDy = 60;
  static const double fgRiseRotX = -30 * math.pi / 180;

  /// Caption slide distance (2D).
  static const double captionDy = 24;

  /// Rig (whole-scene) resting tilt per screen, degrees (rx, ry, rz) in LTR.
  static const List<(double, double, double)> rigDeg = [
    (20, -16, 3), // s1 overview
    (18, 16, -3), // s2 charts
    (20, -15, 3), // s3 invoices
    (18, 15, -3), // s4 profile
    (18, -14, 3), // s5 search
  ];

  /// Extra rig rotateY per page of swipe offset (scene yaws as it leaves).
  static const double swipeYaw = 22 * math.pi / 180; // 0.3840 rad

  /// Ambient drift amplitude: rig rx +3 deg, ry +6 deg peak (mockup `par`),
  /// plus a per-layer vertical bob.
  static const double driftRotX = 3 * math.pi / 180;
  static const double driftRotY = 6 * math.pi / 180;
  static const double driftBobDp = 3;

  // ---------------------------------------------------------------- floor
  static const double floorTilt = 70 * math.pi / 180; // 1.2217 rad
  static const double gridSpacing = 34;
  static const double gridStroke = 1;
  static const double gridOpacity = 0.9;

  /// Radial mask stops for the grid, centre at (0.5, 0.2) of the floor rect.
  static const List<double> gridFadeStops = [0.20, 0.65];

  /// Horizon glow: brand at 30 % -> transparent at 62 % of a 210 dp radius.
  static const double glowRadius = 210;
  static const double glowOpacity = 0.30;
  static const double glowStop = 0.62;

  /// Shadow tint (`--deep`, brandInk 0x074A2E).
  static const Color shadowTint = Color(0xFF074A2E);

  /// Highest lift used to normalise [shadowForLift] (foreground toast z).
  static const double maxLiftZ = 150;

  // ---------------------------------------------------------------- helpers
  /// True when the platform asks for reduced motion.
  static bool reduce(BuildContext context) => MediaQuery.maybeDisableAnimationsOf(context) ?? false;

  /// The layer's entrance as an [Interval] of the [totalMs] page timeline.
  /// [index] adds [IntroLayerSpec.childStaggerMs] per child.
  static Interval interval(IntroLayer layer, {int index = 0, Curve curve = Curves.linear}) {
    final s = layers[layer]!;
    final start = (s.delayMs + s.childStaggerMs * index).clamp(0, totalMs - 1);
    final end = (start + s.durationMs).clamp(start + 1, totalMs);
    return Interval(start / totalMs, end / totalMs, curve: curve);
  }

  /// Opacity for a layer-local raw entrance value.
  static double opacityFor(double t) => fadeIn.transform(t.clamp(0.0, 1.0));

  /// Perspective base for the floor plane: entry (3,2) then rotateX 70 deg.
  static Matrix4 floorPerspective() =>
      Matrix4.identity()..setEntry(3, 2, -perspective)..multiply(Matrix4.rotationX(floorTilt));

  /// Full transform for one layer.
  ///
  /// * [t]: layer-local raw entrance 0..1 (curve applied here).
  /// * [pageOffset]: `page - index`, negative when the page is to the start side.
  /// * [driftT]: 0..1 sawtooth over [driftPeriod].
  /// * [screen]: 0..4 for the rig tilt. [pageWidth] for parallax.
  ///
  /// * [at] / [depth]: the piece's resting place relative to the stage centre (CSS `translate3d(x, y, z)`), applied
  ///   inside the rig so the entrance rotates about the piece, not the stage.
  /// * [driftAmount]: envelope for the drift ([IntroMotion.driftAmount]); 1 = full mockup drift.
  ///
  /// Returns identity when [reduceMotion] is true.
  static Matrix4 layerTransform(
    IntroLayer layer,
    double t,
    double pageOffset,
    double driftT, {
    bool rtl = false,
    bool reduceMotion = false,
    int screen = 0,
    double pageWidth = 390,
    Offset at = Offset.zero,
    double depth = 0,
    double driftAmount = 1,
  }) {
    if (reduceMotion) return Matrix4.identity();
    final spec = layers[layer]!;
    final dir = rtl ? -1.0 : 1.0;
    final tc = _finite(t.clamp(0.0, 1.0));
    final off = _finite(pageOffset.clamp(-1.0, 1.0));
    final dr = _finite(driftT);

    // Parallax: PageView moves the child by -off*width; add the difference.
    final dx = -off * pageWidth * (spec.parallax - 1.0) * dir;

    if (layer == IntroLayer.caption) {
      final p = settle.transform(tc);
      return Matrix4.translationValues(dx, captionDy * (1 - p), 0);
    }

    final rig = rigDeg[screen.clamp(0, rigDeg.length - 1)];
    // Drift: 0 -> peak -> 0 over one period, per-layer phase.
    final wave = (1 - math.cos(2 * math.pi * (dr + spec.driftPhase))) / 2 * _finite(driftAmount).clamp(0.0, 1.0);
    final rx = rig.$1 * math.pi / 180 + driftRotX * wave;
    final ry = (rig.$2 * math.pi / 180 + driftRotY * wave + swipeYaw * off) * dir;
    final rz = rig.$3 * math.pi / 180 * dir;
    final bob = layer == IntroLayer.floor ? 0.0 : -driftBobDp * wave;

    final m = Matrix4.identity()
      ..setEntry(3, 2, -perspective)
      ..multiply(Matrix4.translationValues(dx, bob, 0))
      ..multiply(Matrix4.diagonal3Values(stageScale, stageScale, stageScale))
      ..multiply(Matrix4.rotationX(rx))
      ..multiply(Matrix4.rotationY(ry))
      ..multiply(Matrix4.rotationZ(rz));

    if (layer == IntroLayer.floor) return m;

    m.multiply(Matrix4.translationValues(_finite(at.dx), _finite(at.dy), _finite(depth)));

    // Entrance (from rise start to rest).
    final p = rise.transform(tc);
    final inv = 1 - p;
    final fg = layer == IntroLayer.foreground;
    final s = riseScale + (1 - riseScale) * p;
    m
      ..multiply(Matrix4.translationValues(0, (fg ? fgRiseDy : riseDy) * inv, riseDz * inv))
      ..multiply(Matrix4.rotationX((fg ? fgRiseRotX : riseRotX) * inv))
      ..multiply(Matrix4.rotationY(riseRotY * inv * dir))
      ..multiply(Matrix4.rotationZ(riseRotZ * inv * dir))
      ..multiply(Matrix4.diagonal3Values(s, s, 1));
    return m;
  }

  /// Contact shadow for a card lifted [lift] dp off the slab (0..[maxLiftZ]).
  ///
  /// n = lift / maxLiftZ; blur = 8 + 32n; dy = 4 + 22n; spread = -4 - 10n;
  /// alpha = 0.10 + 0.18n (tint [shadowTint]).
  static BoxShadow shadowForLift(double lift) {
    final n = (_finite(lift) / maxLiftZ).clamp(0.0, 1.0);
    return BoxShadow(
      color: shadowTint.withValues(alpha: 0.10 + 0.18 * n),
      blurRadius: 8 + 32 * n,
      spreadRadius: -4 - 10 * n,
      offset: Offset(0, 4 + 22 * n),
    );
  }

  static double _finite(double v) => v.isFinite ? v : 0.0;
}
