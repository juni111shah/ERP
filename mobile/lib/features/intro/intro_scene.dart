import 'package:flutter/material.dart';

import '../../theme/tokens.dart';
import 'intro_motion.dart';

/// Design canvas of one illustration (the mockup's 390-wide `.ill` box); scaled to fit the space it gets.
const introStageSize = Size(390, 440);

/// Centre of the rig on the design canvas (mockup `.stage{left:50%;top:226px}`).
const introStageCentre = Offset(195, 226);

/// One flat piece of app UI floating in the scene.
class IntroPiece {
  const IntroPiece({
    required this.layer,
    this.index = 0,
    required this.x,
    required this.y,
    this.z = 0,
    required this.child,
    this.lifted = true,
    this.extraDelayMs = 0,
  });

  /// Depth layer; sets timing, parallax and drift phase ([IntroMotion.layers]).
  final IntroLayer layer;

  /// Child index inside the layer, for the stagger.
  final int index;

  /// Resting place relative to the stage centre (CSS `translate3d(x, y, z)`), z toward the viewer.
  final double x;
  final double y;
  final double z;

  /// Whether the piece gets a lift shadow that deepens with its height.
  final bool lifted;

  /// Extra start delay on top of the layer timing (e.g. the second foreground card).
  final int extraDelayMs;

  final Widget child;
}

/// A scene: pieces back to front for one screen ([screen] picks the rig tilt).
class IntroSceneSpec {
  const IntroSceneSpec({required this.screen, required this.pieces});

  final int screen;
  final List<IntroPiece> pieces;
}

/// Raw 0..1 entrance of [piece] at [timeline] (0..1 over [IntroMotion.totalMs]).
double introPieceT(IntroPiece piece, double timeline) {
  final iv = IntroMotion.interval(piece.layer, index: piece.index);
  final shift = piece.extraDelayMs / IntroMotion.totalMs;
  final begin = (iv.begin + shift).clamp(0.0, 0.999);
  final end = (iv.end + shift).clamp(begin + 0.001, 1.0);
  return ((timeline - begin) / (end - begin)).clamp(0.0, 1.0);
}

/// Renders an [IntroSceneSpec]: floor + glow behind, then each piece with its own full Matrix4
/// ([IntroMotion.layerTransform]), since Flutter has no `preserve-3d`.
class IntroScene extends StatelessWidget {
  const IntroScene({
    super.key,
    required this.spec,
    required this.timeline,
    required this.seconds,
    required this.pageOffset,
    required this.pageWidth,
    required this.reduceMotion,
    this.rtl = false,
  });

  final IntroSceneSpec spec;

  /// Entrance timeline 0..1 for this page (1 = settled).
  final double timeline;

  /// Seconds since the page became active, for the ambient drift and floor travel.
  final double seconds;

  /// `page - index`, -1..1.
  final double pageOffset;

  /// Width of one page in design units (parallax travel).
  final double pageWidth;
  final bool reduceMotion;

  /// Mirror the rig, drift and parallax for right-to-left (the sample UI itself stays left-to-right).
  final bool rtl;

  @override
  Widget build(BuildContext context) {
    final driftT = (seconds % (IntroMotion.driftPeriod.inMilliseconds / 1000)) / (IntroMotion.driftPeriod.inMilliseconds / 1000);
    final amount = IntroMotion.driftAmount(seconds);
    final floorT = introPieceT(const IntroPiece(layer: IntroLayer.floor, x: 0, y: 0, child: SizedBox()), timeline);
    final floorOpacity = reduceMotion ? 1.0 : IntroMotion.opacityFor(floorT);

    final children = <Widget>[
      // Horizon glow (mockup `.glow`).
      Positioned(
        left: introStageCentre.dx - IntroMotion.glowRadius * 2 / 2,
        top: 40,
        width: IntroMotion.glowRadius * 2,
        height: IntroMotion.glowRadius * 2,
        child: Opacity(
          opacity: floorOpacity,
          child: const DecoratedBox(
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              gradient: RadialGradient(
                colors: [Color(0x4D10955D), Color(0x0010955D)],
                stops: [0, IntroMotion.glowStop],
              ),
            ),
          ),
        ),
      ),
      // Perspective floor (mockup `.floor`: left/right -40, top 250, height 300, rotateX 70° about its top edge).
      Positioned(
        left: -40,
        top: 250,
        width: introStageSize.width + 80,
        height: 300,
        child: Transform.translate(
          offset: Offset(reduceMotion ? 0 : IntroMotion.parallaxDx(IntroLayer.floor, pageOffset, rtl: rtl, pageWidth: pageWidth), 0),
          child: Transform(
            alignment: Alignment.topCenter,
            transform: reduceMotion ? Matrix4.identity() : IntroMotion.floorPerspective(),
            child: Opacity(
              opacity: floorOpacity * (reduceMotion ? 0.5 : 1),
              child: CustomPaint(painter: IntroFloorPainter(travel: reduceMotion ? 0 : IntroMotion.floorTravel(seconds))),
            ),
          ),
        ),
      ),
    ];

    for (final piece in spec.pieces) {
      final t = reduceMotion ? 1.0 : introPieceT(piece, timeline);
      final opacity = reduceMotion ? 1.0 : IntroMotion.opacityFor(t);
      if (opacity <= 0) continue;
      final settled = IntroMotion.rise.transform(t).clamp(0.0, 1.0);
      final m = Matrix4.translationValues(introStageCentre.dx, introStageCentre.dy, 0)
        ..multiply(reduceMotion
            // Flat fallback: the stage scale and resting position only.
            ? (Matrix4.diagonal3Values(IntroMotion.stageScale, IntroMotion.stageScale, 1)..translateByDouble(piece.x, piece.y, 0, 1))
            : IntroMotion.layerTransform(
                piece.layer,
                t,
                pageOffset,
                driftT,
                rtl: rtl,
                screen: spec.screen,
                pageWidth: pageWidth,
                at: Offset(piece.x, piece.y),
                depth: piece.z,
                driftAmount: amount,
              ));
      Widget child = piece.child;
      if (piece.lifted) {
        child = DecoratedBox(
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(ErpRadius.panel),
            boxShadow: [IntroMotion.shadowForLift(piece.z * (reduceMotion ? 1 : settled))],
          ),
          child: child,
        );
      }
      children.add(Positioned(
        left: 0,
        top: 0,
        child: Transform(
          transform: m,
          child: opacity >= 1 ? child : Opacity(opacity: opacity, child: child),
        ),
      ));
    }

    return SizedBox.fromSize(size: introStageSize, child: Stack(clipBehavior: Clip.none, children: children));
  }
}

/// Flat grid for the floor plane; the 3D comes from [IntroMotion.floorPerspective]. Lines fade with distance through a
/// radial shader baked into the stroke paint (centre 0.5/0.2, stops [IntroMotion.gridFadeStops]), no saveLayer.
class IntroFloorPainter extends CustomPainter {
  IntroFloorPainter({required this.travel});

  /// Forward travel in cells.
  final double travel;

  @override
  void paint(Canvas canvas, Size size) {
    const g = IntroMotion.gridSpacing;
    final rect = Offset.zero & size;
    final paint = Paint()
      ..strokeWidth = IntroMotion.gridStroke
      ..style = PaintingStyle.stroke
      ..shader = RadialGradient(
        center: const Alignment(0, -0.6), // (0.5, 0.2) of the rect
        radius: 0.75,
        colors: [
          ErpColors.border.withValues(alpha: IntroMotion.gridOpacity),
          ErpColors.border.withValues(alpha: IntroMotion.gridOpacity),
          ErpColors.border.withValues(alpha: 0),
        ],
        stops: [0, IntroMotion.gridFadeStops[0], IntroMotion.gridFadeStops[1]],
      ).createShader(rect);
    final shift = (travel % 1) * g;
    for (var y = shift; y <= size.height; y += g) {
      canvas.drawLine(Offset(0, y), Offset(size.width, y), paint);
    }
    for (var x = 0.0; x <= size.width; x += g) {
      canvas.drawLine(Offset(x, 0), Offset(x, size.height), paint);
    }
  }

  @override
  bool shouldRepaint(IntroFloorPainter old) => old.travel != travel;
}
