import 'package:flutter/material.dart';

import '../../kit/brand_mark.dart';
import '../../theme/tokens.dart';
import 'intro_motion.dart';
import 'intro_scene.dart';

/// The five tour scenes (mockup Concept B). Everything here is sample content for an illustration, not live data.
/// [featuresTitle] / [features] are the localised "Everything in one app" chips on the last screen.
List<IntroSceneSpec> introScenes({required String featuresTitle, required List<String> features}) => [
      IntroSceneSpec(screen: 0, pieces: [
        ..._slab(300, 330, -150, -170),
        const IntroPiece(layer: IntroLayer.hero, x: -134, y: -150, z: 22, lifted: false, child: _Chips()),
        for (var i = 0; i < 4; i++)
          IntroPiece(
            layer: IntroLayer.hero,
            index: i + 1,
            x: -134 + (i % 2) * 138,
            y: -104 + (i ~/ 2) * 100,
            z: 46 + i * 12,
            child: _Kpi(_kpis[i], width: 130),
          ),
        const IntroPiece(layer: IntroLayer.foreground, x: -50, y: 108, z: 30, lifted: false, child: _Dots()),
        const IntroPiece(
          layer: IntroLayer.foreground,
          x: 60,
          y: 96,
          z: 90,
          extraDelayMs: 120,
          child: _Toast(icon: Icons.swipe_outlined, title: 'Swipe for more', line: '37 metrics', small: true),
        ),
      ]),
      IntroSceneSpec(screen: 1, pieces: [
        ..._slab(318, 300, -159, -160),
        const IntroPiece(layer: IntroLayer.hero, x: -142, y: -144, z: 24, lifted: false, child: SizedBox(width: 286, child: _ChartHead())),
        const IntroPiece(layer: IntroLayer.hero, index: 1, x: -142, y: -88, z: 40, lifted: false, child: _Bars(width: 286)),
        const IntroPiece(layer: IntroLayer.hero, index: 2, x: -142, y: 96, z: 24, lifted: false, child: SizedBox(width: 286, child: _Legend())),
        const IntroPiece(
          layer: IntroLayer.foreground,
          x: -196,
          y: -214,
          z: 90,
          child: _Kpi(('Billed · Oct', '180K', 'QAR', ErpColors.billed), width: 128),
        ),
        const IntroPiece(
          layer: IntroLayer.foreground,
          x: 30,
          y: 118,
          z: 100,
          extraDelayMs: 150,
          child: _Kpi(('Collected · Oct', '316K', 'QAR', ErpColors.collected), width: 132),
        ),
      ]),
      IntroSceneSpec(screen: 2, pieces: [
        ..._slab(326, 340, -163, -175),
        for (var i = 0; i < _invoices.length; i++)
          IntroPiece(layer: IntroLayer.hero, index: i, x: -148, y: -158.0 + i * 78, z: 30.0 + i * 22, child: _Invoice(_invoices[i], width: 296)),
        const IntroPiece(
          layer: IntroLayer.foreground,
          x: -120,
          y: 150,
          z: 150,
          child: _Toast(icon: Icons.check_rounded, title: 'Receipt recorded', line: 'QAR 50,000 · Al Sadd Hospitality'),
        ),
      ]),
      IntroSceneSpec(screen: 3, pieces: [
        ..._slab(310, 350, -155, -180, inner: const _FadedOverview()),
        const IntroPiece(layer: IntroLayer.hero, x: -150, y: -214, z: 70, child: _Header(width: 300)),
        const IntroPiece(layer: IntroLayer.foreground, x: -162, y: -40, z: 110, child: _ProfileSheet(width: 324)),
      ]),
      IntroSceneSpec(screen: 4, pieces: [
        ..._slab(300, 330, -150, -170, inner: const _FadedInvoices()),
        const IntroPiece(layer: IntroLayer.hero, x: -168, y: -206, z: 80, child: _Palette(width: 336)),
        IntroPiece(
          layer: IntroLayer.foreground,
          x: -168,
          y: 6,
          z: 120,
          extraDelayMs: 120,
          child: _Features(width: 336, title: featuresTitle, features: features),
        ),
      ]),
    ];

/// Base slab with its green underside 14dp below (mockup `.slab:before`) and a soft ground shadow.
List<IntroPiece> _slab(double w, double h, double x, double y, {Widget? inner}) => [
      IntroPiece(
        layer: IntroLayer.back,
        x: x + 10,
        y: y + 30,
        z: -60,
        lifted: false,
        child: Container(
          width: w - 20,
          height: h - 10,
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(24),
            boxShadow: [BoxShadow(color: IntroMotion.shadowTint.withValues(alpha: 0.22), blurRadius: 22, spreadRadius: 4)],
          ),
        ),
      ),
      IntroPiece(
        layer: IntroLayer.back,
        x: x,
        y: y,
        z: -14,
        lifted: false,
        child: Container(
          width: w,
          height: h,
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(22),
            gradient: const LinearGradient(
              begin: Alignment.topCenter,
              end: Alignment.bottomCenter,
              colors: [Color(0xE60D7B4C), Color(0xE6074A2E)],
            ),
          ),
        ),
      ),
      IntroPiece(
        layer: IntroLayer.back,
        x: x,
        y: y,
        lifted: false,
        child: Container(
          width: w,
          height: h,
          clipBehavior: Clip.antiAlias,
          decoration: BoxDecoration(
            color: ErpColors.surface,
            borderRadius: BorderRadius.circular(22),
            border: Border.all(color: ErpColors.border),
          ),
          child: Stack(children: [
            if (inner != null) Positioned.fill(child: inner),
            // Shine (mockup `.shine`).
            const Positioned.fill(
              child: DecoratedBox(
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    begin: Alignment(-1, -1),
                    end: Alignment(0.2, 0.4),
                    colors: [Color(0x8CFFFFFF), Color(0x00FFFFFF)],
                  ),
                ),
              ),
            ),
          ]),
        ),
      ),
    ];

typedef _KpiData = (String label, String value, String sub, Color? accent);

const List<_KpiData> _kpis = [
  ('Sales', '1.24M', 'QAR · this month', ErpColors.billed),
  ('Cash and bank', '2.07M', 'QAR · 4 accounts', ErpColors.billed),
  ('Receivables', '597K', '548K overdue', null),
  ('Collected', '316K', 'QAR · this month', null),
];

/// One line of sample text that always shows in full: scales down instead of ellipsising when space is tight.
Widget _fit(String text, TextStyle style, {AlignmentGeometry align = AlignmentDirectional.centerStart}) => FittedBox(
      fit: BoxFit.scaleDown,
      alignment: align,
      child: Text(text, maxLines: 1, softWrap: false, style: style),
    );

typedef _InvData = (String name, String amount, String number, ErpTone tone, String status, bool partly);

const List<_InvData> _invoices = [
  ('Qatar Building Supplies', '48,250.00', 'INV-2026-0412', ErpTone.info, 'Sent', false),
  ('Al Sadd Hospitality Group', '126,800.00', 'INV-2026-0411', ErpTone.warning, 'Partly paid', true),
  ('West Bay Facilities W.L.L.', '9,450.00', 'INV-2026-0410', ErpTone.success, 'Paid', false),
  ('Msheireb Retail Co.', '62,300.00', 'INV-2026-0337', ErpTone.error, '74 days overdue', false),
];

const _t1 = TextStyle(color: ErpColors.text, fontFamily: ErpFonts.family, fontFamilyFallback: ErpFonts.fallback);
const _muted = TextStyle(color: ErpColors.muted, fontFamily: ErpFonts.family, fontFamilyFallback: ErpFonts.fallback);

BoxDecoration _card({double radius = ErpRadius.card}) => BoxDecoration(
      color: ErpColors.surface,
      borderRadius: BorderRadius.circular(radius),
      border: Border.all(color: ErpColors.borderSubtle),
    );

class _Chips extends StatelessWidget {
  const _Chips();

  @override
  Widget build(BuildContext context) {
    Widget chip(String label, String count, {bool on = false}) => Container(
          height: 30,
          padding: const EdgeInsetsDirectional.symmetric(horizontal: 11),
          decoration: BoxDecoration(
            color: on ? ErpColors.brandSubtle : ErpColors.surface,
            borderRadius: BorderRadius.circular(ErpRadius.pill),
            border: Border.all(color: on ? ErpColors.brand.withValues(alpha: 0.35) : ErpColors.border),
          ),
          child: Row(mainAxisSize: MainAxisSize.min, children: [
            Text(label, style: _t1.copyWith(fontSize: 12.5, fontWeight: on ? FontWeight.w600 : FontWeight.w500, color: on ? ErpColors.brand : ErpColors.text2)),
            const SizedBox(width: ErpSpace.s15),
            Container(
              padding: const EdgeInsetsDirectional.symmetric(horizontal: ErpSpace.s15),
              decoration: BoxDecoration(color: on ? ErpColors.surface : ErpColors.sunken, borderRadius: BorderRadius.circular(ErpRadius.pill)),
              child: Text(count, style: _t1.copyWith(fontSize: 10.5, height: 1.5)),
            ),
          ]),
        );
    return SizedBox(
      width: 270,
      height: 30,
      child: ClipRect(
        child: OverflowBox(
          alignment: AlignmentDirectional.centerStart,
          maxWidth: double.infinity,
          child: Row(mainAxisSize: MainAxisSize.min, children: [
            chip('All', '37', on: true),
            const SizedBox(width: 7),
            chip('Finance', '6'),
            const SizedBox(width: 7),
            chip('Sales', '5'),
            const SizedBox(width: 7),
            chip('Approvals', '4'),
          ]),
        ),
      ),
    );
  }
}

class _Kpi extends StatelessWidget {
  const _Kpi(this.data, {required this.width});

  final _KpiData data;
  final double width;

  @override
  Widget build(BuildContext context) {
    final accent = data.$4 ?? ErpColors.brand;
    final value = data.$4 ?? ErpColors.brandHover;
    return Container(
      width: width,
      decoration: _card(),
      clipBehavior: Clip.antiAlias,
      child: Stack(children: [
        PositionedDirectional(
          start: 0,
          top: 10,
          bottom: 10,
          child: Container(
            width: 3,
            decoration: BoxDecoration(color: accent, borderRadius: const BorderRadiusDirectional.horizontal(end: Radius.circular(3))),
          ),
        ),
        Padding(
          padding: const EdgeInsetsDirectional.fromSTEB(15, ErpSpace.s3, ErpSpace.s3, ErpSpace.s3),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, mainAxisSize: MainAxisSize.min, children: [
            _fit(data.$1.toUpperCase(), _muted.copyWith(fontSize: 9.5, fontWeight: FontWeight.w600, letterSpacing: 0.57)),
            const SizedBox(height: ErpSpace.s1),
            Text(data.$2, style: _t1.copyWith(fontSize: ErpType.headline, fontWeight: FontWeight.w700, letterSpacing: -0.44, color: value, fontFeatures: ErpFonts.tabular)),
            const SizedBox(height: ErpSpace.s05),
            _fit(data.$3, _muted.copyWith(fontSize: 11)),
          ]),
        ),
      ]),
    );
  }
}

class _Dots extends StatelessWidget {
  const _Dots();

  @override
  Widget build(BuildContext context) => Row(mainAxisSize: MainAxisSize.min, children: [
        for (var i = 0; i < 5; i++)
          Container(
            width: i == 0 ? 18 : 6,
            height: 6,
            margin: const EdgeInsetsDirectional.only(end: 5),
            decoration: BoxDecoration(color: i == 0 ? ErpColors.brand : ErpColors.border, borderRadius: BorderRadius.circular(3)),
          ),
      ]);
}

class _Toast extends StatelessWidget {
  const _Toast({required this.icon, required this.title, required this.line, this.small = false});

  final IconData icon;
  final String title;
  final String line;
  final bool small;

  @override
  Widget build(BuildContext context) {
    final d = small ? 26.0 : 30.0;
    return Container(
      padding: EdgeInsetsDirectional.fromSTEB(small ? 8 : 10, small ? 8 : 10, small ? 12 : 14, small ? 8 : 10),
      decoration: _card(radius: 14),
      child: Row(mainAxisSize: MainAxisSize.min, children: [
        Container(
          width: d,
          height: d,
          decoration: const BoxDecoration(color: ErpColors.brand, shape: BoxShape.circle),
          child: Icon(icon, size: small ? 15 : 16, color: Colors.white),
        ),
        const SizedBox(width: ErpSpace.s25),
        Column(crossAxisAlignment: CrossAxisAlignment.start, mainAxisSize: MainAxisSize.min, children: [
          Text(title, style: _t1.copyWith(fontSize: small ? 12 : 13, fontWeight: FontWeight.w700)),
          Text(line, style: _t1.copyWith(fontSize: 12.5, color: ErpColors.text2)),
        ]),
      ]),
    );
  }
}

class _ChartHead extends StatelessWidget {
  const _ChartHead();

  @override
  Widget build(BuildContext context) => Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Expanded(
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            _fit('Billed vs collected', _t1.copyWith(fontSize: 14.5, fontWeight: FontWeight.w700)),
            const SizedBox(height: ErpSpace.s05),
            _fit('QAR · last 12 months', _muted.copyWith(fontSize: 11.5)),
          ]),
        ),
        Container(
          padding: const EdgeInsets.all(2),
          decoration: BoxDecoration(color: ErpColors.subtle, borderRadius: BorderRadius.circular(9), border: Border.all(color: ErpColors.border)),
          child: Row(mainAxisSize: MainAxisSize.min, children: [
            Container(
              height: 24,
              padding: const EdgeInsetsDirectional.symmetric(horizontal: ErpSpace.s25),
              alignment: Alignment.center,
              decoration: BoxDecoration(color: ErpColors.surface, borderRadius: BorderRadius.circular(7), border: Border.all(color: ErpColors.brand100)),
              child: Text('Bar', style: _t1.copyWith(fontSize: 11, fontWeight: FontWeight.w600, color: ErpColors.brand)),
            ),
            Container(
              height: 24,
              padding: const EdgeInsetsDirectional.symmetric(horizontal: ErpSpace.s25),
              alignment: Alignment.center,
              child: Text('Line', style: _t1.copyWith(fontSize: 11, fontWeight: FontWeight.w600, color: ErpColors.text2)),
            ),
          ]),
        ),
      ]);
}

class _Bars extends StatelessWidget {
  const _Bars({required this.width});

  final double width;

  static const billed = [88, 92, 64, 120, 72, 95, 130, 87, 110, 240, 125, 180];
  static const collected = [70, 80, 40, 98, 60, 90, 101, 75, 85, 180, 110, 316];
  static const months = ['N', 'D', 'J', 'F', 'M', 'A', 'M', 'J', 'J', 'A', 'S', 'O'];

  @override
  Widget build(BuildContext context) => SizedBox(
        width: width,
        child: Column(mainAxisSize: MainAxisSize.min, children: [
          SizedBox(height: 150, width: width, child: const CustomPaint(painter: _BarsPainter())),
          const SizedBox(height: ErpSpace.s15),
          Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
            for (final m in months) SizedBox(width: 16, child: _fit(m, _muted.copyWith(fontSize: ErpType.micro), align: Alignment.center)),
          ]),
        ]),
      );
}

class _BarsPainter extends CustomPainter {
  const _BarsPainter();

  @override
  void paint(Canvas canvas, Size size) {
    final grid = Paint()
      ..color = ErpColors.borderSubtle
      ..strokeWidth = 1;
    for (final p in [0.25, 0.5, 0.75]) {
      final y = size.height * (1 - p);
      for (var x = 0.0; x < size.width; x += 6) {
        canvas.drawLine(Offset(x, y), Offset(x + 3, y), grid);
      }
    }
    canvas.drawLine(Offset(0, size.height), Offset(size.width, size.height), grid);
    final slot = size.width / 12;
    final b = Paint()..color = ErpColors.billed;
    final c = Paint()..color = ErpColors.collected;
    for (var i = 0; i < 12; i++) {
      final x = i * slot + (slot - 16) / 2;
      void bar(double left, num v, Paint p) {
        final h = size.height * v / 330;
        canvas.drawRRect(
          RRect.fromRectAndCorners(Rect.fromLTWH(left, size.height - h, 7, h), topLeft: const Radius.circular(3), topRight: const Radius.circular(3)),
          p,
        );
      }

      bar(x, _Bars.billed[i], b);
      bar(x + 9, _Bars.collected[i], c);
    }
  }

  @override
  bool shouldRepaint(_BarsPainter old) => false;
}

class _Legend extends StatelessWidget {
  const _Legend();

  @override
  Widget build(BuildContext context) {
    Widget key(Color c, String text) => Row(mainAxisSize: MainAxisSize.min, children: [
          Container(width: 10, height: 10, decoration: BoxDecoration(color: c, borderRadius: BorderRadius.circular(3))),
          const SizedBox(width: ErpSpace.s15),
          Flexible(child: _fit(text, _t1.copyWith(fontSize: 11.5, color: ErpColors.text2))),
        ]);
    return Row(children: [
      Flexible(child: key(ErpColors.billed, 'Billed 1.40M')),
      const SizedBox(width: ErpSpace.s3),
      Flexible(child: key(ErpColors.collected, 'Collected 1.31M')),
      const SizedBox(width: ErpSpace.s2),
      Container(
        padding: const EdgeInsetsDirectional.symmetric(horizontal: ErpSpace.s15, vertical: 1),
        decoration: BoxDecoration(
          color: ErpTone.success.bg,
          borderRadius: BorderRadius.circular(ErpRadius.pill),
          border: Border.all(color: ErpTone.success.border),
        ),
        child: Text('94%', style: _t1.copyWith(fontSize: 11, fontWeight: FontWeight.w600, color: ErpTone.success.fg)),
      ),
    ]);
  }
}

class _Invoice extends StatelessWidget {
  const _Invoice(this.data, {required this.width});

  final _InvData data;
  final double width;

  @override
  Widget build(BuildContext context) {
    final overdue = data.$4 == ErpTone.error;
    return Container(
      width: width,
      decoration: _card(),
      clipBehavior: Clip.antiAlias,
      child: Container(
        padding: const EdgeInsetsDirectional.fromSTEB(13, 11, 13, 11),
        decoration: overdue ? BoxDecoration(border: BorderDirectional(start: BorderSide(color: ErpTone.error.fg, width: 3))) : null,
        child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, mainAxisSize: MainAxisSize.min, children: [
          Row(children: [
            Expanded(child: _fit(data.$1, _t1.copyWith(fontSize: 12.5, fontWeight: FontWeight.w600))),
            const SizedBox(width: ErpSpace.s2),
            Text('QAR ', style: _muted.copyWith(fontSize: ErpType.micro, fontWeight: FontWeight.w600)),
            Text(data.$2, style: _t1.copyWith(fontSize: 12.5, fontWeight: FontWeight.w700, fontFeatures: ErpFonts.tabular)),
          ]),
          const SizedBox(height: 5),
          Row(children: [
            Expanded(child: _fit(data.$3, _muted.copyWith(fontSize: 10.5, fontFamily: 'monospace'))),
            _Tag(data.$4, data.$5),
          ]),
          if (data.$6) ...[
            const SizedBox(height: 5),
            ClipRRect(
              borderRadius: BorderRadius.circular(2),
              child: const LinearProgressIndicator(value: 0.6, minHeight: 4, color: ErpColors.brand, backgroundColor: ErpColors.sunken),
            ),
          ],
        ]),
      ),
    );
  }
}

class _Tag extends StatelessWidget {
  const _Tag(this.tone, this.text);

  final ErpTone tone;
  final String text;

  @override
  Widget build(BuildContext context) => Container(
        height: 21,
        padding: const EdgeInsetsDirectional.symmetric(horizontal: ErpSpace.s2),
        decoration: BoxDecoration(color: tone.bg, borderRadius: BorderRadius.circular(ErpRadius.pill), border: Border.all(color: tone.border)),
        child: Row(mainAxisSize: MainAxisSize.min, children: [
          Container(width: 6, height: 6, decoration: BoxDecoration(color: tone.fg, shape: BoxShape.circle)),
          const SizedBox(width: 5),
          Text(text, style: _t1.copyWith(fontSize: 11, fontWeight: FontWeight.w600, color: tone.fg)),
        ]),
      );
}

class _Avatar extends StatelessWidget {
  const _Avatar({this.size = 34});

  final double size;

  @override
  Widget build(BuildContext context) => Container(
        width: size,
        height: size,
        alignment: Alignment.center,
        decoration: const BoxDecoration(
          shape: BoxShape.circle,
          gradient: LinearGradient(begin: Alignment.topLeft, end: Alignment.bottomRight, colors: [ErpColors.brandHover, ErpColors.brandInk]),
        ),
        child: Text('MH', style: _t1.copyWith(fontSize: 12.5, fontWeight: FontWeight.w700, color: Colors.white)),
      );
}

class _Header extends StatelessWidget {
  const _Header({required this.width});

  final double width;

  @override
  Widget build(BuildContext context) => Container(
        width: width,
        padding: const EdgeInsetsDirectional.symmetric(horizontal: ErpSpace.s3, vertical: ErpSpace.s25),
        decoration: _card(radius: 16),
        child: Row(children: [
          Container(
            width: 34,
            height: 34,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(10),
              gradient: const LinearGradient(colors: [ErpColors.brandInk, ErpColors.brandHover]),
            ),
            child: Text('AB', style: _t1.copyWith(fontSize: 12.5, fontWeight: FontWeight.w700, color: Colors.white)),
          ),
          const SizedBox(width: ErpSpace.s25),
          Expanded(
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              _fit('Overview', _t1.copyWith(fontSize: ErpType.body, fontWeight: FontWeight.w700)),
              _fit('Al Bidda Holding · Doha', _muted.copyWith(fontSize: 11)),
            ]),
          ),
          SizedBox(
            width: 40,
            height: 40,
            child: Stack(clipBehavior: Clip.none, children: [
              const Center(child: Icon(Icons.notifications_none_rounded, size: 22, color: ErpColors.text2)),
              PositionedDirectional(
                top: 3,
                end: 1,
                child: Container(
                  constraints: const BoxConstraints(minWidth: 18),
                  height: 18,
                  padding: const EdgeInsetsDirectional.symmetric(horizontal: ErpSpace.s1),
                  alignment: Alignment.center,
                  decoration: BoxDecoration(
                    color: ErpColors.danger,
                    borderRadius: BorderRadius.circular(9),
                    border: Border.all(color: ErpColors.surface, width: 2),
                  ),
                  child: Text('18', style: _t1.copyWith(fontSize: ErpType.micro, fontWeight: FontWeight.w700, color: Colors.white, height: 1)),
                ),
              ),
            ]),
          ),
          const SizedBox(width: ErpSpace.s2),
          Container(
            decoration: const BoxDecoration(shape: BoxShape.circle, boxShadow: [BoxShadow(color: ErpColors.brandSubtle, spreadRadius: 3)]),
            child: const _Avatar(),
          ),
        ]),
      );
}

class _ProfileSheet extends StatelessWidget {
  const _ProfileSheet({required this.width});

  final double width;

  @override
  Widget build(BuildContext context) {
    Widget row(IconData icon, String label, {Widget? trailing, bool chevron = true}) => SizedBox(
          height: 44,
          child: Row(children: [
            Icon(icon, size: 18, color: ErpColors.text2),
            const SizedBox(width: ErpSpace.s3),
            Expanded(child: _fit(label, _t1.copyWith(fontSize: 13.5, fontWeight: FontWeight.w500))),
            ?trailing,
            if (chevron) const Icon(Icons.chevron_right_rounded, size: 18, color: ErpColors.muted),
          ]),
        );
    Widget seg(IconData icon, String label, {bool on = false}) => Expanded(
          child: Container(
            height: 34,
            decoration: on
                ? BoxDecoration(color: ErpColors.surface, borderRadius: BorderRadius.circular(9), border: Border.all(color: ErpColors.brand100), boxShadow: ErpShadow.card)
                : null,
            child: Row(mainAxisAlignment: MainAxisAlignment.center, children: [
              Icon(icon, size: 15, color: on ? ErpColors.brand : ErpColors.text2),
              const SizedBox(width: ErpSpace.s15),
              Flexible(child: _fit(label, _t1.copyWith(fontSize: 12.5, fontWeight: FontWeight.w600, color: on ? ErpColors.brand : ErpColors.text2))),
            ]),
          ),
        );
    return Container(
      width: width,
      padding: const EdgeInsetsDirectional.fromSTEB(ErpSpace.s35, ErpSpace.s2, ErpSpace.s35, ErpSpace.s3),
      decoration: BoxDecoration(
        color: ErpColors.surface,
        borderRadius: const BorderRadius.vertical(top: Radius.circular(22), bottom: Radius.circular(18)),
        border: Border.all(color: ErpColors.borderSubtle),
      ),
      child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
        Center(
          child: Container(
            width: 36,
            height: 4,
            margin: const EdgeInsets.only(top: 2, bottom: 10),
            decoration: BoxDecoration(color: ErpColors.border, borderRadius: BorderRadius.circular(2)),
          ),
        ),
        Row(children: [
          const _Avatar(size: 40),
          const SizedBox(width: 11),
          Expanded(
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              _fit('Mariam Hassan', _t1.copyWith(fontSize: 14.5, fontWeight: FontWeight.w700)),
              _fit('Workspace owner · Al Bidda Holding', _muted.copyWith(fontSize: 11.5)),
            ]),
          ),
        ]),
        const SizedBox(height: 10),
        const Divider(height: 1, color: ErpColors.borderSubtle),
        row(Icons.notifications_none_rounded, 'Notifications', trailing: const Padding(padding: EdgeInsetsDirectional.only(end: 4), child: _Tag(ErpTone.error, '18'))),
        row(Icons.wb_sunny_outlined, 'Appearance', chevron: false),
        Container(
          padding: const EdgeInsets.all(3),
          decoration: BoxDecoration(color: ErpColors.subtle, borderRadius: BorderRadius.circular(12), border: Border.all(color: ErpColors.border)),
          child: Row(children: [
            seg(Icons.wb_sunny_outlined, 'Light', on: true),
            const SizedBox(width: 3),
            seg(Icons.dark_mode_outlined, 'Dark'),
            const SizedBox(width: 3),
            seg(Icons.desktop_windows_outlined, 'System'),
          ]),
        ),
        row(Icons.settings_outlined, 'Settings'),
      ]),
    );
  }
}

class _Palette extends StatelessWidget {
  const _Palette({required this.width});

  final double width;

  @override
  Widget build(BuildContext context) {
    Widget group(String text) => Padding(
          padding: const EdgeInsetsDirectional.fromSTEB(10, 8, 10, 4),
          child: Text(text.toUpperCase(), style: _muted.copyWith(fontSize: ErpType.micro, fontWeight: FontWeight.w700, letterSpacing: 0.7)),
        );
    Widget result(IconData icon, String title, String line, {bool hi = false}) => Container(
          padding: const EdgeInsetsDirectional.symmetric(horizontal: 10, vertical: 9),
          decoration: hi ? BoxDecoration(color: ErpColors.brandSubtle, borderRadius: BorderRadius.circular(10)) : null,
          child: Row(children: [
            Container(
              width: 32,
              height: 32,
              decoration: BoxDecoration(color: hi ? ErpColors.brand : ErpColors.sunken, borderRadius: BorderRadius.circular(9)),
              child: Icon(icon, size: 17, color: hi ? Colors.white : ErpColors.text2),
            ),
            const SizedBox(width: 11),
            Expanded(
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                _fit(title, _t1.copyWith(fontSize: 13.5, fontWeight: FontWeight.w600)),
                _fit(line, _muted.copyWith(fontSize: 11.5)),
              ]),
            ),
            if (hi) const _Kbd('↵'),
          ]),
        );
    return Container(
      width: width,
      padding: const EdgeInsets.all(ErpSpace.s3),
      decoration: BoxDecoration(color: ErpColors.surface, borderRadius: BorderRadius.circular(18), border: Border.all(color: ErpColors.borderSubtle)),
      child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
        Container(
          height: 46,
          padding: const EdgeInsetsDirectional.symmetric(horizontal: ErpSpace.s3),
          decoration: BoxDecoration(
            color: ErpColors.surface,
            borderRadius: BorderRadius.circular(12),
            border: Border.all(color: ErpColors.brand, width: 1.5),
            boxShadow: [BoxShadow(color: ErpColors.brand.withValues(alpha: 0.14), spreadRadius: 4)],
          ),
          child: Row(children: [
            const Icon(Icons.search_rounded, size: 18, color: ErpColors.brand),
            const SizedBox(width: 10),
            Flexible(child: _fit('al sadd', _t1.copyWith(fontSize: ErpType.bodyLarge))),
            Container(width: 2, height: 18, margin: const EdgeInsetsDirectional.only(start: 1), color: ErpColors.brand),
            const Spacer(),
            const _Kbd('Esc'),
          ]),
        ),
        group('Best match'),
        result(Icons.person_outline_rounded, 'Al Sadd Hospitality Group', 'Customer · balance QAR 50,720', hi: true),
        result(Icons.description_outlined, 'INV-2026-0411', 'Invoice · Partly paid'),
      ]),
    );
  }
}

class _Kbd extends StatelessWidget {
  const _Kbd(this.text);

  final String text;

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsetsDirectional.symmetric(horizontal: ErpSpace.s15, vertical: ErpSpace.s05),
        decoration: BoxDecoration(color: ErpColors.subtle, borderRadius: BorderRadius.circular(6), border: Border.all(color: ErpColors.border)),
        child: Text(text, style: _muted.copyWith(fontSize: 10.5, fontWeight: FontWeight.w600)),
      );
}

/// "Everything in one app": a compact grid of real feature chips (labels come from [IntroStrings.features]).
class _Features extends StatelessWidget {
  const _Features({required this.width, required this.title, required this.features});

  final double width;
  final String title;
  final List<String> features;

  static const _icons = [
    Icons.receipt_long_outlined,
    Icons.payments_outlined,
    Icons.menu_book_outlined,
    Icons.account_tree_outlined,
    Icons.task_alt_rounded,
    Icons.inventory_2_outlined,
    Icons.badge_outlined,
    Icons.apartment_rounded,
    Icons.handshake_outlined,
    Icons.print_outlined,
    Icons.public_rounded,
    Icons.contrast_rounded,
  ];

  @override
  Widget build(BuildContext context) => Container(
        width: width,
        padding: const EdgeInsets.all(ErpSpace.s3),
        decoration: BoxDecoration(color: ErpColors.surface, borderRadius: BorderRadius.circular(18), border: Border.all(color: ErpColors.borderSubtle)),
        child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          Row(children: [
            const BrandMark(size: 20, semanticLabel: null),
            const SizedBox(width: ErpSpace.s2),
            Expanded(child: _fit(title, _t1.copyWith(fontSize: ErpType.bodySmall, fontWeight: FontWeight.w700))),
          ]),
          const SizedBox(height: ErpSpace.s25),
          Wrap(spacing: ErpSpace.s15, runSpacing: ErpSpace.s15, children: [
            for (var i = 0; i < features.length; i++)
              Container(
                height: 26,
                constraints: BoxConstraints(maxWidth: width - ErpSpace.s6),
                padding: const EdgeInsetsDirectional.fromSTEB(ErpSpace.s2, 0, ErpSpace.s25, 0),
                decoration: BoxDecoration(
                  color: ErpColors.brandSubtle,
                  borderRadius: BorderRadius.circular(ErpRadius.pill),
                  border: Border.all(color: ErpColors.brand100),
                ),
                child: Row(mainAxisSize: MainAxisSize.min, children: [
                  Icon(_icons[i % _icons.length], size: 13, color: ErpColors.brandHover),
                  const SizedBox(width: ErpSpace.s1),
                  Flexible(child: _fit(features[i], _t1.copyWith(fontSize: ErpType.label, fontWeight: FontWeight.w600, color: ErpColors.brandInk))),
                ]),
              ),
          ]),
        ]),
      );
}

/// Dimmed overview behind the profile sheet.
class _FadedOverview extends StatelessWidget {
  const _FadedOverview();

  @override
  Widget build(BuildContext context) => Opacity(
        opacity: 0.55,
        child: Padding(
          padding: const EdgeInsets.all(ErpSpace.s35),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            const SizedBox(height: 40),
            Wrap(spacing: 8, runSpacing: 8, children: [for (final k in _kpis) _Kpi(k, width: 132)]),
          ]),
        ),
      );
}

/// Dimmed invoice list behind the search palette.
class _FadedInvoices extends StatelessWidget {
  const _FadedInvoices();

  @override
  Widget build(BuildContext context) => Opacity(
        opacity: 0.5,
        child: Padding(
          padding: const EdgeInsets.all(ErpSpace.s4),
          child: Column(children: [
            for (final v in _invoices.take(3)) ...[_Invoice(v, width: 268), const SizedBox(height: ErpSpace.s2)],
          ]),
        ),
      );
}
