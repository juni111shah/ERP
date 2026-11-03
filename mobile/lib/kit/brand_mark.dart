import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../core/brand.dart';

/// The HigherSquare 360 emblem, drawn as vectors so it is crisp at every size: a green rounded square holding a white
/// disc with an open 360 ring, four rising bars (teal, teal, green, navy) and a trend line with an arrow breaking out of
/// the ring. Same geometry as `assets/brand/hs360-mark.svg` and the launcher icons (100 x 100 design units).
class BrandMark extends StatelessWidget {
  const BrandMark({super.key, this.size = 40, this.semanticLabel = Brand.name, this.shadow = false});

  final double size;
  final String? semanticLabel;

  /// Soft brand-tinted drop shadow, for hero placements (sign-in, splash).
  final bool shadow;

  @override
  Widget build(BuildContext context) {
    Widget mark = CustomPaint(size: Size.square(size), painter: const BrandMarkPainter());
    if (shadow) {
      mark = DecoratedBox(
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(size * 0.22),
          boxShadow: [BoxShadow(color: BrandColors.tileDeep.withValues(alpha: 0.28), blurRadius: size * 0.28, offset: Offset(0, size * 0.12))],
        ),
        child: mark,
      );
    }
    final label = semanticLabel;
    return SizedBox.square(
      dimension: size,
      child: label == null ? ExcludeSemantics(child: mark) : Semantics(label: label, image: true, child: ExcludeSemantics(child: mark)),
    );
  }
}

/// Paints the emblem into its square. Public so other painters (and tests) can reuse it.
class BrandMarkPainter extends CustomPainter {
  const BrandMarkPainter();

  static const _bars = [(24.0, 33.0, 63.0, BrandColors.teal), (37.0, 46.0, 55.0, BrandColors.teal), (50.0, 59.0, 46.0, BrandColors.tile), (63.0, 72.0, 35.0, BrandColors.navy)];
  static const _line = [Offset(22, 56), Offset(36, 44), Offset(45, 49), Offset(58, 38)];
  static const _tip = Offset(78, 21);

  @override
  void paint(Canvas canvas, Size size) {
    final s = size.shortestSide / 100;
    canvas.save();
    canvas.translate((size.width - 100 * s) / 2, (size.height - 100 * s) / 2);
    canvas.scale(s);

    const box = Rect.fromLTWH(0, 0, 100, 100);
    canvas.drawRRect(
      RRect.fromRectAndRadius(box, const Radius.circular(22)),
      Paint()..shader = const LinearGradient(begin: Alignment.topLeft, end: Alignment.bottomRight, colors: [BrandColors.tile, BrandColors.tileDeep]).createShader(box),
    );
    const c = Offset(50, 50);
    canvas.drawCircle(c, 45, Paint()..color = Colors.white);

    final green = Paint()
      ..color = BrandColors.tile
      ..style = PaintingStyle.stroke
      ..strokeWidth = 5
      ..strokeCap = StrokeCap.round;
    final ring = Rect.fromCircle(center: c, radius: 39);
    double rad(double deg) => deg * math.pi / 180;
    canvas.drawArc(ring, rad(105), rad(296 - 105), false, green);
    canvas.drawArc(ring, rad(332), rad(80 + 360 - 332), false, green);

    canvas.save();
    canvas.clipPath(Path()..addOval(Rect.fromCircle(center: c, radius: 35)));
    for (final (x0, x1, top, color) in _bars) {
      canvas.drawRRect(
        RRect.fromRectAndCorners(Rect.fromLTRB(x0, top, x1, 90), topLeft: const Radius.circular(1.5), topRight: const Radius.circular(1.5)),
        Paint()..color = color,
      );
    }
    canvas.restore();

    final last = _line.last;
    final d = _tip - last;
    final u = d / d.distance;
    final n = Offset(-u.dy, u.dx);
    final shaftEnd = _tip - u * 5;
    final line = Path()..moveTo(_line.first.dx, _line.first.dy);
    for (final p in _line.skip(1)) {
      line.lineTo(p.dx, p.dy);
    }
    line.lineTo(shaftEnd.dx, shaftEnd.dy);
    green
      ..strokeWidth = 3.4
      ..strokeJoin = StrokeJoin.round;
    canvas.drawPath(line, green);

    final fill = Paint()..color = BrandColors.tile;
    for (final p in _line) {
      canvas.drawCircle(p, 2.9, fill);
    }
    final base = _tip - u * 8;
    final head = Path()
      ..moveTo(_tip.dx, _tip.dy)
      ..lineTo(base.dx + n.dx * 4.6, base.dy + n.dy * 4.6)
      ..lineTo(base.dx - n.dx * 4.6, base.dy - n.dy * 4.6)
      ..close();
    canvas.drawPath(head, fill);
    canvas.drawPath(
      head,
      Paint()
        ..color = BrandColors.tile
        ..style = PaintingStyle.stroke
        ..strokeWidth = 1
        ..strokeJoin = StrokeJoin.round,
    );
    canvas.restore();
  }

  @override
  bool shouldRepaint(covariant BrandMarkPainter oldDelegate) => false;
}

/// The wordmark: "HigherSquare" in brand green and "360" in deep teal (lighter green / sky on dark surfaces), as on
/// the web portal. One text node so screen readers and tests read the whole [Brand.name].
class BrandWordmark extends StatelessWidget {
  const BrandWordmark({super.key, this.fontSize = 20, this.dark, this.textAlign});

  final double fontSize;

  /// Force the light or dark colours; null follows the current theme.
  final bool? dark;
  final TextAlign? textAlign;

  @override
  Widget build(BuildContext context) {
    final onDark = dark ?? Theme.of(context).brightness == Brightness.dark;
    final base = TextStyle(fontSize: fontSize, fontWeight: FontWeight.w700, letterSpacing: -0.02 * fontSize, height: 1.15);
    return Text.rich(
      TextSpan(children: [
        TextSpan(text: Brand.word, style: base.copyWith(color: onDark ? BrandColors.wordDark : BrandColors.wordLight)),
        TextSpan(text: ' ${Brand.suffix}', style: base.copyWith(fontWeight: FontWeight.w800, color: onDark ? BrandColors.suffixDark : BrandColors.suffixLight)),
      ]),
      maxLines: 1,
      overflow: TextOverflow.ellipsis,
      textAlign: textAlign,
      // Brand names read left-to-right even in Arabic layouts.
      textDirection: TextDirection.ltr,
    );
  }
}

/// Mark + wordmark, side by side ([Axis.horizontal]) or stacked ([Axis.vertical]), with an optional small caption.
class BrandLockup extends StatelessWidget {
  const BrandLockup({
    super.key,
    this.markSize = 40,
    this.fontSize,
    this.axis = Axis.horizontal,
    this.caption,
    this.captionStyle,
    this.dark,
    this.shadow = false,
  });

  final double markSize;
  final double? fontSize;
  final Axis axis;
  final String? caption;
  final TextStyle? captionStyle;
  final bool? dark;
  final bool shadow;

  @override
  Widget build(BuildContext context) {
    final size = fontSize ?? (axis == Axis.vertical ? markSize * 0.36 : markSize * 0.5);
    final vertical = axis == Axis.vertical;
    final text = Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: vertical ? CrossAxisAlignment.center : CrossAxisAlignment.start,
      children: [
        BrandWordmark(fontSize: size, dark: dark, textAlign: vertical ? TextAlign.center : null),
        if (caption != null)
          Padding(
            padding: const EdgeInsets.only(top: 2),
            child: Text(caption!, maxLines: 1, overflow: TextOverflow.ellipsis, textAlign: vertical ? TextAlign.center : null, style: captionStyle),
          ),
      ],
    );
    final mark = BrandMark(size: markSize, semanticLabel: null, shadow: shadow);
    return Semantics(
      container: true,
      child: vertical
          ? Column(mainAxisSize: MainAxisSize.min, children: [mark, SizedBox(height: markSize * 0.25), text])
          : Row(mainAxisSize: MainAxisSize.min, children: [mark, SizedBox(width: markSize * 0.25), Flexible(child: text)]),
    );
  }
}

/// First frame while the session and intro flag load: the stacked lockup over the canvas with a slim progress bar.
class BrandSplash extends StatelessWidget {
  const BrandSplash({super.key});

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Scaffold(
      body: Center(
        child: Column(mainAxisSize: MainAxisSize.min, children: [
          const BrandLockup(markSize: 88, axis: Axis.vertical, fontSize: 26, shadow: true),
          const SizedBox(height: 28),
          SizedBox(
            width: 120,
            child: ClipRRect(
              borderRadius: BorderRadius.circular(2),
              child: LinearProgressIndicator(minHeight: 3, color: BrandColors.tile, backgroundColor: theme.colorScheme.surfaceContainerHighest),
            ),
          ),
        ]),
      ),
    );
  }
}
