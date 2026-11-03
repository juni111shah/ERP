import 'dart:math' as math;

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';

import 'package:erp_mobile/theme/tokens.dart';

/// One month of the billed-vs-collected series.
@immutable
class ErpMonthValue {
  const ErpMonthValue({required this.label, required this.billed, required this.collected, this.fullLabel});

  /// Short axis label, e.g. "Jan" or "J".
  final String label;

  /// Full period name shown when the month is tapped, e.g. "Nov 26"; falls back to [label].
  final String? fullLabel;
  final double billed;
  final double collected;

  @override
  bool operator ==(Object other) =>
      other is ErpMonthValue && other.label == label && other.fullLabel == fullLabel && other.billed == billed && other.collected == collected;

  @override
  int get hashCode => Object.hash(label, fullLabel, billed, collected);
}

/// How [ErpBilledCollectedChart] draws the series.
enum ErpChartMode { bar, line }

/// "4820000" -> "4.82M", "712000" -> "712K", "45500" -> "45.5K", "900" -> "900".
String erpCompactNumber(double value) {
  final abs = value.abs();
  final sign = value < 0 ? '-' : '';
  String trim(double v, int decimals) {
    var s = v.toStringAsFixed(decimals);
    if (s.contains('.')) s = s.replaceFirst(RegExp(r'\.?0+$'), '');
    return s;
  }

  if (abs >= 1e9) return '$sign${trim(abs / 1e9, 2)}B';
  if (abs >= 1e6) return '$sign${trim(abs / 1e6, 2)}M';
  if (abs >= 1e3) return '$sign${trim(abs / 1e3, abs >= 1e5 ? 0 : 1)}K';
  return '$sign${trim(abs, 0)}';
}

/// Billed-vs-collected panel: grouped monthly bars (or two lines), y-axis
/// gridlines with compact labels, month labels, legend chips with totals and a
/// collected-% badge. Drawn with [CustomPaint]; no chart package.
///
/// RTL: months run right-to-left and the y-axis sits on the start (right) side.
/// Tap a month to show its figures under the chart; tap it again to clear.
///
/// ```dart
/// ErpBilledCollectedChart(
///   title: 'Billed vs collected',
///   subtitle: 'Nov 2025 – Oct 2026 · QAR',
///   data: const [
///     ErpMonthValue(label: 'N', billed: 352000, collected: 301000),
///     ErpMonthValue(label: 'D', billed: 368000, collected: 330000),
///     // ...
///   ],
/// )
/// ```
class ErpBilledCollectedChart extends StatefulWidget {
  const ErpBilledCollectedChart({
    super.key,
    required this.data,
    this.height = 220,
    this.currency = 'QAR',
    this.showToggle = true,
    this.initialMode = ErpChartMode.bar,
    this.title,
    this.subtitle,
    this.eyebrow,
    this.billedLabel = 'Billed',
    this.collectedLabel = 'Collected',
    this.barLabel = 'Bar',
    this.lineLabel = 'Line',
    this.emptyLabel = 'No billing in this period',
    this.onModeChanged,
  });

  final List<ErpMonthValue> data;

  /// Height of the plot area (axes included), not the whole panel.
  final double height;

  /// Shown in the selected-month line and the semantics summary; null hides it.
  final String? currency;
  final bool showToggle;
  final ErpChartMode initialMode;

  final String? title;
  final String? subtitle;

  /// Small uppercase badge above the title, e.g. "Finance".
  final String? eyebrow;

  // Translatable strings (pass the AR values from the app's l10n).
  final String billedLabel;
  final String collectedLabel;
  final String barLabel;
  final String lineLabel;
  final String emptyLabel;

  final ValueChanged<ErpChartMode>? onModeChanged;

  @override
  State<ErpBilledCollectedChart> createState() => _ErpBilledCollectedChartState();
}

class _ErpBilledCollectedChartState extends State<ErpBilledCollectedChart> {
  late ErpChartMode _mode = widget.initialMode;
  int? _selected;

  @override
  void didUpdateWidget(covariant ErpBilledCollectedChart oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (_selected != null && _selected! >= widget.data.length) _selected = null;
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final dark = theme.brightness == Brightness.dark;
    final dir = Directionality.of(context);
    final muted = dark ? ErpDark.muted : ErpColors.muted;
    final text2 = dark ? ErpDark.text2 : ErpColors.text2;
    final grid = dark ? ErpDark.borderSubtle : ErpColors.borderSubtle;
    final data = widget.data;

    final billedTotal = data.fold<double>(0, (s, m) => s + m.billed);
    final collectedTotal = data.fold<double>(0, (s, m) => s + m.collected);
    final pct = billedTotal > 0 ? collectedTotal / billedTotal * 100 : null;
    final tone = pct == null
        ? ErpTone.neutral
        : pct >= 90
            ? ErpTone.success
            : pct >= 70
                ? ErpTone.warning
                : ErpTone.error;
    final cur = (widget.currency ?? '').isEmpty ? '' : ' ${widget.currency}';

    final summary = [
      if (widget.title != null) widget.title!,
      if (widget.subtitle != null) widget.subtitle!,
      '${widget.billedLabel} ${erpCompactNumber(billedTotal)}$cur',
      '${widget.collectedLabel} ${erpCompactNumber(collectedTotal)}$cur',
      if (pct != null) '${pct.round()}%',
    ].join(', ');

    final header = Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Expanded(
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            if (widget.eyebrow != null)
              Row(children: [
                Icon(Icons.show_chart_rounded, size: 14, color: ErpColors.brand),
                const SizedBox(width: 5),
                Flexible(
                  child: Text(
                    Localizations.localeOf(context).languageCode == 'ar' ? widget.eyebrow! : widget.eyebrow!.toUpperCase(),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(fontSize: 11, fontWeight: FontWeight.w600, letterSpacing: 0.55, color: ErpColors.brand),
                  ),
                ),
              ]),
            if (widget.title != null)
              Padding(
                padding: const EdgeInsets.only(top: 2),
                child: Text(widget.title!, maxLines: 1, overflow: TextOverflow.ellipsis,
                    style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700)),
              ),
            if (widget.subtitle != null)
              Text(widget.subtitle!, maxLines: 1, overflow: TextOverflow.ellipsis, style: TextStyle(fontSize: 12, color: muted)),
          ]),
        ),
        if (widget.showToggle && data.isNotEmpty)
          SegmentedButton<ErpChartMode>(
            segments: [
              ButtonSegment(value: ErpChartMode.bar, label: Text(widget.barLabel)),
              ButtonSegment(value: ErpChartMode.line, label: Text(widget.lineLabel)),
            ],
            selected: {_mode},
            showSelectedIcon: false,
            style: const ButtonStyle(
              visualDensity: VisualDensity(horizontal: -3, vertical: -3),
              tapTargetSize: MaterialTapTargetSize.shrinkWrap,
              textStyle: WidgetStatePropertyAll(TextStyle(fontSize: 12, fontWeight: FontWeight.w600)),
            ),
            onSelectionChanged: (s) {
              setState(() => _mode = s.first);
              widget.onModeChanged?.call(s.first);
            },
          ),
      ],
    );

    final Widget plot;
    if (data.isEmpty) {
      plot = SizedBox(
        height: widget.height,
        child: Center(
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            Icon(Icons.bar_chart_rounded, size: 32, color: muted.withValues(alpha: 0.6)),
            const SizedBox(height: ErpSpace.s2),
            Text(widget.emptyLabel, textAlign: TextAlign.center, style: TextStyle(fontSize: 13, color: muted)),
          ]),
        ),
      );
    } else {
      plot = SizedBox(
        height: widget.height,
        width: double.infinity,
        child: LayoutBuilder(builder: (context, constraints) {
          final painter = _ChartPainter(
            data: data,
            mode: _mode,
            selected: _selected,
            textDirection: dir,
            gridColor: grid,
            labelColor: muted,
            highlightColor: (dark ? Colors.white : ErpColors.text).withValues(alpha: 0.05),
            dotFill: dark ? ErpDark.surface : ErpColors.surface,
          );
          return GestureDetector(
            behavior: HitTestBehavior.opaque,
            onTapUp: (d) {
              final i = painter.layoutFor(constraints.biggest).indexAt(d.localPosition.dx);
              setState(() => _selected = (i == null || i == _selected) ? null : i);
            },
            child: CustomPaint(size: constraints.biggest, painter: painter),
          );
        }),
      );
    }

    final sel = _selected;
    return Semantics(
      container: true,
      label: summary,
      child: Container(
        padding: const EdgeInsets.fromLTRB(ErpSpace.s4, 14, ErpSpace.s4, 14),
        decoration: BoxDecoration(
          color: dark ? ErpDark.surface : ErpColors.surface,
          borderRadius: BorderRadius.circular(ErpRadius.panel),
          border: Border.all(color: grid),
          boxShadow: dark ? null : ErpShadow.card,
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          mainAxisSize: MainAxisSize.min,
          children: [
            if (widget.title != null || widget.eyebrow != null || (widget.showToggle && data.isNotEmpty)) header,
            const SizedBox(height: ErpSpace.s2),
            ExcludeSemantics(child: plot),
            if (sel != null && sel < data.length)
              Padding(
                padding: const EdgeInsets.only(top: ErpSpace.s1),
                child: Semantics(
                  liveRegion: true,
                  child: Text.rich(
                    TextSpan(children: [
                      TextSpan(text: '${data[sel].fullLabel ?? data[sel].label}  ', style: const TextStyle(fontWeight: FontWeight.w700)),
                      TextSpan(text: '${widget.billedLabel} '),
                      _ltrNumber('${erpCompactNumber(data[sel].billed)}$cur'),
                      TextSpan(text: '  ·  ${widget.collectedLabel} '),
                      _ltrNumber('${erpCompactNumber(data[sel].collected)}$cur'),
                    ]),
                    style: TextStyle(fontSize: 12, color: text2, fontFeatures: const [FontFeature.tabularFigures()]),
                  ),
                ),
              ),
            if (data.isNotEmpty)
              Padding(
                padding: const EdgeInsets.only(top: 10),
                child: Row(children: [
                  Flexible(child: _LegendChip(color: ErpColors.billed, label: widget.billedLabel, total: erpCompactNumber(billedTotal), textColor: text2)),
                  const SizedBox(width: 14),
                  Flexible(child: _LegendChip(color: ErpColors.collected, label: widget.collectedLabel, total: erpCompactNumber(collectedTotal), textColor: text2)),
                  const Spacer(),
                  if (pct != null) _PercentBadge(percent: pct, tone: tone, brightness: theme.brightness),
                ]),
              ),
          ],
        ),
      ),
    );
  }

  /// Keeps "412K QAR" in LTR order inside an RTL sentence (Unicode LRI … PDI).
  static TextSpan _ltrNumber(String s) => TextSpan(text: '\u2066$s\u2069');
}

class _LegendChip extends StatelessWidget {
  const _LegendChip({required this.color, required this.label, required this.total, required this.textColor});

  final Color color;
  final String label;
  final String total;
  final Color textColor;

  @override
  Widget build(BuildContext context) => Row(mainAxisSize: MainAxisSize.min, children: [
        Container(width: 10, height: 10, decoration: BoxDecoration(color: color, borderRadius: BorderRadius.circular(3))),
        const SizedBox(width: 6),
        Flexible(
          child: Text.rich(
            TextSpan(children: [TextSpan(text: '$label '), TextSpan(text: '\u2066$total\u2069')]),
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(fontSize: 12, color: textColor, fontFeatures: const [FontFeature.tabularFigures()]),
          ),
        ),
      ]);
}

class _PercentBadge extends StatelessWidget {
  const _PercentBadge({required this.percent, required this.tone, required this.brightness});

  final double percent;
  final ErpTone tone;
  final Brightness brightness;

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.symmetric(horizontal: ErpSpace.s2, vertical: 2),
        decoration: BoxDecoration(
          color: tone.bgFor(brightness),
          borderRadius: BorderRadius.circular(ErpRadius.pill),
          border: Border.all(color: tone.borderFor(brightness)),
        ),
        child: Directionality(
          textDirection: TextDirection.ltr,
          child: Text(
            '${percent.round()}%',
            style: TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w600,
              color: tone.fgFor(brightness),
              fontFeatures: const [FontFeature.tabularFigures()],
            ),
          ),
        ),
      );
}

/// Geometry shared by painting and hit-testing.
class _ChartLayout {
  _ChartLayout({required this.plot, required this.count, required this.rtl});

  final Rect plot;
  final int count;
  final bool rtl;

  double get slot => plot.width / count;

  /// Centre x of month [i]; month 0 is on the start side.
  double centerX(int i) => rtl ? plot.right - (i + 0.5) * slot : plot.left + (i + 0.5) * slot;

  int? indexAt(double dx) {
    if (dx < plot.left || dx > plot.right || count == 0) return null;
    final fromStart = rtl ? plot.right - dx : dx - plot.left;
    return (fromStart / slot).floor().clamp(0, count - 1);
  }
}

class _ChartPainter extends CustomPainter {
  _ChartPainter({
    required this.data,
    required this.mode,
    required this.selected,
    required this.textDirection,
    required this.gridColor,
    required this.labelColor,
    required this.highlightColor,
    required this.dotFill,
  }) : ticks = _niceTicks(data.fold<double>(0, (m, v) => math.max(m, math.max(v.billed, v.collected))));

  final List<ErpMonthValue> data;
  final ErpChartMode mode;
  final int? selected;
  final TextDirection textDirection;
  final Color gridColor;
  final Color labelColor;
  final Color highlightColor;
  final Color dotFill;
  final List<double> ticks;

  static const _labelStyleSize = 10.0;
  static const _xAxisHeight = 18.0;
  static const _axisGap = 6.0;
  static const _topPad = 6.0;

  /// 0 plus four evenly spaced "nice" steps (1, 2, 2.5, 5 × 10^n) covering [max].
  static List<double> _niceTicks(double max) {
    if (max <= 0 || !max.isFinite) return const [0, 1, 2, 3, 4];
    final raw = max / 4;
    final mag = math.pow(10, (math.log(raw) / math.ln10).floor()).toDouble();
    final norm = raw / mag;
    final step = (norm <= 1 ? 1 : norm <= 2 ? 2 : norm <= 2.5 ? 2.5 : norm <= 5 ? 5 : 10) * mag;
    final n = (max / step).ceil();
    return [for (var i = 0; i <= n; i++) i * step];
  }

  TextPainter _text(String s) => TextPainter(
        text: TextSpan(
          text: s,
          style: TextStyle(fontSize: _labelStyleSize, color: labelColor, fontFeatures: const [FontFeature.tabularFigures()]),
        ),
        textDirection: textDirection,
        maxLines: 1,
      )..layout();

  _ChartLayout layoutFor(Size size) {
    var axisW = 0.0;
    for (final t in ticks) {
      final p = _text(erpCompactNumber(t));
      axisW = math.max(axisW, p.width);
      p.dispose();
    }
    axisW += _axisGap;
    final rtl = textDirection == TextDirection.rtl;
    final plot = Rect.fromLTRB(
      rtl ? 0 : axisW,
      _topPad,
      rtl ? size.width - axisW : size.width,
      math.max(_topPad + 1, size.height - _xAxisHeight),
    );
    return _ChartLayout(plot: plot, count: data.length, rtl: rtl);
  }

  @override
  void paint(Canvas canvas, Size size) {
    if (data.isEmpty || size.isEmpty) return;
    final l = layoutFor(size);
    final plot = l.plot;
    final maxY = ticks.last;
    double y(double v) => plot.bottom - (v.clamp(0, maxY) / maxY) * plot.height;

    // Gridlines + y labels on the start side.
    final gridPaint = Paint()
      ..color = gridColor
      ..strokeWidth = 1;
    for (final t in ticks) {
      final ty = y(t);
      canvas.drawLine(Offset(plot.left, ty), Offset(plot.right, ty), gridPaint);
      final p = _text(erpCompactNumber(t));
      final dx = l.rtl ? plot.right + _axisGap : plot.left - _axisGap - p.width;
      p.paint(canvas, Offset(dx, ty - p.height / 2));
      p.dispose();
    }

    // Selected month highlight.
    if (selected != null && selected! < data.length) {
      final cx = l.centerX(selected!);
      canvas.drawRRect(
        RRect.fromRectAndRadius(
          Rect.fromCenter(center: Offset(cx, plot.center.dy), width: l.slot - 2, height: plot.height + 4),
          const Radius.circular(4),
        ),
        Paint()..color = highlightColor,
      );
    }

    // Month labels (skip some when crowded).
    var every = 1;
    final widest = data.fold<double>(0, (m, v) {
      final p = _text(v.label);
      final w = p.width;
      p.dispose();
      return math.max(m, w);
    });
    while (every < data.length && widest + 4 > l.slot * every) {
      every++;
    }
    for (var i = 0; i < data.length; i += every) {
      final p = _text(data[i].label);
      p.paint(canvas, Offset(l.centerX(i) - p.width / 2, plot.bottom + (_xAxisHeight - p.height) / 2 + 1));
      p.dispose();
    }

    final billedPaint = Paint()..color = ErpColors.billed;
    final collectedPaint = Paint()..color = ErpColors.collected;

    if (mode == ErpChartMode.bar) {
      final barW = math.min(10.0, math.max(2.0, (l.slot - 6) / 2));
      const gap = 1.0;
      for (var i = 0; i < data.length; i++) {
        final cx = l.centerX(i);
        // Billed is the first bar on the start side, collected follows.
        final startX = l.rtl ? cx + gap / 2 : cx - gap / 2 - barW;
        final endX = l.rtl ? cx - gap / 2 - barW : cx + gap / 2;
        _bar(canvas, Rect.fromLTRB(startX, y(data[i].billed), startX + barW, plot.bottom), billedPaint);
        _bar(canvas, Rect.fromLTRB(endX, y(data[i].collected), endX + barW, plot.bottom), collectedPaint);
      }
    } else {
      _line(canvas, l, y, (m) => m.billed, ErpColors.billed);
      _line(canvas, l, y, (m) => m.collected, ErpColors.collected);
    }
  }

  void _bar(Canvas canvas, Rect r, Paint paint) {
    if (r.height <= 0) return;
    canvas.drawRRect(
      RRect.fromRectAndCorners(r, topLeft: const Radius.circular(2), topRight: const Radius.circular(2)),
      paint,
    );
  }

  void _line(Canvas canvas, _ChartLayout l, double Function(double) y, double Function(ErpMonthValue) pick, Color color) {
    final path = Path();
    for (var i = 0; i < data.length; i++) {
      final o = Offset(l.centerX(i), y(pick(data[i])));
      i == 0 ? path.moveTo(o.dx, o.dy) : path.lineTo(o.dx, o.dy);
    }
    canvas.drawPath(
      path,
      Paint()
        ..color = color
        ..style = PaintingStyle.stroke
        ..strokeWidth = 2
        ..strokeJoin = StrokeJoin.round
        ..strokeCap = StrokeCap.round,
    );
    final fill = Paint()..color = dotFill;
    final ring = Paint()
      ..color = color
      ..style = PaintingStyle.stroke
      ..strokeWidth = 2;
    for (var i = 0; i < data.length; i++) {
      final o = Offset(l.centerX(i), y(pick(data[i])));
      final r = i == selected ? 4.5 : 3.0;
      canvas.drawCircle(o, r, fill);
      canvas.drawCircle(o, r, ring);
    }
  }

  @override
  bool shouldRepaint(covariant _ChartPainter old) =>
      old.mode != mode ||
      old.selected != selected ||
      old.textDirection != textDirection ||
      old.gridColor != gridColor ||
      old.labelColor != labelColor ||
      old.highlightColor != highlightColor ||
      old.dotFill != dotFill ||
      !listEquals(old.data, data);
}
