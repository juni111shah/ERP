import 'package:flutter/material.dart';

import 'package:erp_mobile/theme/tokens.dart';

import 'motion.dart';

/// Highlights stat card: a 4px start stripe, an uppercase label, a tinted
/// tabular value with a currency suffix and an optional delta / subtitle line.
///
/// The stripe is positioned with [PositionedDirectional] and rounded with
/// [BorderRadiusDirectional], so it sits on the start edge and flips in RTL.
/// The number is always laid out LTR (like `<bdi>` on the web), so "4.82M QAR"
/// keeps its order inside Arabic text.
///
/// ```dart
/// ErpStatGrid(children: [
///   ErpStatCard(
///     label: 'Revenue',
///     value: '4.82M',
///     stripe: ErpStripe.green,
///     delta: '12.4%',
///     deltaTone: ErpTone.success,
///     deltaDirection: ErpDeltaDirection.up,
///     subtitle: 'vs prior year',
///     onTap: () => context.go('/reports/revenue'),
///   ),
///   const ErpStatCard(label: 'Collected', value: '4.11M', stripe: ErpStripe.blue, subtitle: '85% of billed'),
///   const ErpStatCard(label: 'Receivables', value: '712K', stripe: ErpStripe.amber,
///       delta: '186K', deltaTone: ErpTone.error, subtitle: 'over 60 days'),
///   const ErpStatCard(label: 'Cash & bank', value: '1.36M', stripe: ErpStripe.cyan, subtitle: '4 accounts, at 3 Nov'),
/// ]);
/// ```
class ErpStatCard extends StatelessWidget {
  const ErpStatCard({
    super.key,
    required this.label,
    required this.value,
    this.stripe = ErpStripe.green,
    this.currency = 'QAR',
    this.delta,
    this.deltaTone = ErpTone.neutral,
    this.deltaDirection,
    this.subtitle,
    this.onTap,
    this.semanticsLabel,
  });

  /// Card title, e.g. "Revenue". Upper-cased except in Arabic.
  final String label;

  /// The figure, already formatted (e.g. "4.82M" or "1,250.00").
  final String value;

  /// Stripe / value colour pair.
  final ErpStripe stripe;

  /// Suffix after the value; null or empty hides it.
  final String? currency;

  /// Optional change figure, e.g. "12.4%" or "186K".
  final String? delta;

  /// Colour of [delta].
  final ErpTone deltaTone;

  /// Arrow shown before [delta]; null shows no arrow.
  final ErpDeltaDirection? deltaDirection;

  /// Muted text after the delta (or on its own), e.g. "vs prior year".
  final String? subtitle;

  final VoidCallback? onTap;

  /// Overrides the combined screen-reader label.
  final String? semanticsLabel;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final dark = theme.brightness == Brightness.dark;
    final isArabic = Localizations.localeOf(context).languageCode == 'ar';
    final muted = dark ? ErpDark.muted : ErpColors.muted;
    final valueColor = dark ? Color.lerp(stripe.value, Colors.white, 0.35)! : stripe.value;
    final hasCurrency = currency != null && currency!.isNotEmpty;
    final hasDelta = delta != null && delta!.isNotEmpty;
    final hasSubtitle = subtitle != null && subtitle!.isNotEmpty;
    final radius = BorderRadius.circular(ErpRadius.card);

    final semantic = semanticsLabel ??
        [
          label,
          hasCurrency ? '$value $currency' : value,
          if (hasDelta) '${deltaDirection == null ? '' : '${deltaDirection!.name} '}$delta',
          if (hasSubtitle) subtitle!,
        ].join(', ');

    final body = Padding(
      padding: const EdgeInsetsDirectional.fromSTEB(ErpSpace.s4, ErpSpace.s3, ErpSpace.s35, ErpSpace.s3),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(
            isArabic ? label : label.toUpperCase(),
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(
              fontSize: ErpType.label,
              fontWeight: FontWeight.w600,
              color: muted,
              letterSpacing: isArabic ? 0 : 0.44,
              height: 1.3,
            ),
          ),
          Padding(
            padding: const EdgeInsets.symmetric(vertical: 3),
            child: SizedBox(
              width: double.infinity,
              child: FittedBox(
                fit: BoxFit.scaleDown,
                alignment: AlignmentDirectional.centerStart,
                child: Directionality(
                  textDirection: TextDirection.ltr,
                  child: Text.rich(
                    TextSpan(children: [
                      TextSpan(text: value),
                      if (hasCurrency)
                        TextSpan(
                          text: ' $currency',
                          style: TextStyle(
                            fontSize: ErpType.caption,
                            fontWeight: FontWeight.w600,
                            letterSpacing: 0,
                            color: valueColor.withValues(alpha: 0.8),
                          ),
                        ),
                    ]),
                    maxLines: 1,
                    softWrap: false,
                    style: TextStyle(
                      fontSize: ErpType.display,
                      fontWeight: FontWeight.w700,
                      letterSpacing: -0.48,
                      height: 1.2,
                      color: valueColor,
                      fontFeatures: const [FontFeature.tabularFigures()],
                    ),
                  ),
                ),
              ),
            ),
          ),
          if (hasDelta || hasSubtitle) _DeltaLine(
            delta: hasDelta ? delta : null,
            tone: deltaTone,
            direction: deltaDirection,
            subtitle: hasSubtitle ? subtitle : null,
            muted: muted,
            brightness: theme.brightness,
          ),
        ],
      ),
    );

    return Semantics(
      container: true,
      button: onTap != null,
      label: semantic,
      excludeSemantics: true,
      child: ErpPressable(
        enabled: onTap != null,
        child: Container(
        constraints: const BoxConstraints(minHeight: 100),
        decoration: BoxDecoration(
          color: dark ? ErpDark.surface : ErpColors.surface,
          borderRadius: radius,
          border: Border.all(color: dark ? ErpDark.borderSubtle : ErpColors.borderSubtle),
          boxShadow: dark ? null : ErpShadow.card,
        ),
        child: Material(
          type: MaterialType.transparency,
          child: InkWell(
            onTap: onTap,
            borderRadius: radius,
            child: Stack(
              children: [
                PositionedDirectional(
                  start: 0,
                  top: ErpSpace.s3,
                  bottom: ErpSpace.s3,
                  width: 4,
                  child: DecoratedBox(
                    decoration: BoxDecoration(
                      color: stripe.stripe,
                      borderRadius: const BorderRadiusDirectional.horizontal(end: Radius.circular(4)),
                    ),
                  ),
                ),
                body,
              ],
            ),
          ),
        ),
      ),
      ),
    );
  }
}

/// Direction of the delta arrow. Up/down are vertical, so they need no RTL mirroring.
enum ErpDeltaDirection { up, down }

class _DeltaLine extends StatelessWidget {
  const _DeltaLine({
    required this.delta,
    required this.tone,
    required this.direction,
    required this.subtitle,
    required this.muted,
    required this.brightness,
  });

  final String? delta;
  final ErpTone tone;
  final ErpDeltaDirection? direction;
  final String? subtitle;
  final Color muted;
  final Brightness brightness;

  @override
  Widget build(BuildContext context) {
    final fg = tone == ErpTone.neutral ? muted : tone.fgFor(brightness);
    return Row(
      children: [
        if (delta != null) ...[
          if (direction != null)
            Icon(
              direction == ErpDeltaDirection.up ? Icons.arrow_drop_up_rounded : Icons.arrow_drop_down_rounded,
              size: 16,
              color: fg,
            ),
          Directionality(
            textDirection: TextDirection.ltr,
            child: Text(
              delta!,
              maxLines: 1,
              style: TextStyle(
                fontSize: ErpType.caption,
                fontWeight: FontWeight.w600,
                color: fg,
                fontFeatures: const [FontFeature.tabularFigures()],
              ),
            ),
          ),
          if (subtitle != null) const SizedBox(width: ErpSpace.s1),
        ],
        if (subtitle != null)
          Flexible(
            child: Text(
              subtitle!,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(fontSize: ErpType.caption, color: muted),
            ),
          ),
      ],
    );
  }
}

/// Two-column grid of [ErpStatCard]s with ErpSpace gaps. Rows size to the
/// taller card, so long labels never overflow. Not scrollable; place it inside
/// the page's own scroll view.
///
/// ```dart
/// ErpStatGrid(padding: const EdgeInsets.symmetric(horizontal: ErpSpace.s4), children: cards)
/// ```
class ErpStatGrid extends StatelessWidget {
  const ErpStatGrid({
    super.key,
    required this.children,
    this.columns = 2,
    this.spacing = 10,
    this.padding = EdgeInsets.zero,
  });

  final List<Widget> children;
  final int columns;

  /// Gap between cards, horizontal and vertical (mockup: 10).
  final double spacing;
  final EdgeInsetsGeometry padding;

  @override
  Widget build(BuildContext context) {
    final rows = <Widget>[];
    for (var i = 0; i < children.length; i += columns) {
      final cells = <Widget>[];
      for (var c = 0; c < columns; c++) {
        if (c > 0) cells.add(SizedBox(width: spacing));
        final index = i + c;
        cells.add(Expanded(child: index < children.length ? children[index] : const SizedBox.shrink()));
      }
      if (rows.isNotEmpty) rows.add(SizedBox(height: spacing));
      rows.add(IntrinsicHeight(
        child: Row(crossAxisAlignment: CrossAxisAlignment.stretch, children: cells),
      ));
    }
    return Padding(padding: padding, child: Column(mainAxisSize: MainAxisSize.min, children: rows));
  }
}
