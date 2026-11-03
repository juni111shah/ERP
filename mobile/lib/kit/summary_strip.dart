import 'package:flutter/material.dart';

import 'package:erp_mobile/theme/tokens.dart';

/// One tile of a [SummaryStrip]. [label] and [value] are already localized /
/// formatted by the caller (e.g. "Outstanding", "712,450").
@immutable
class SummaryItem {
  const SummaryItem({
    required this.label,
    required this.value,
    this.tone,
    this.semanticsLabel,
    this.onTap,
  });

  final String label;

  /// Pre-formatted amount or count. Always rendered left-to-right.
  final String value;

  /// Value colour; null = default text colour.
  final ErpTone? tone;

  /// Optional screen-reader text, e.g. "Overdue 186,400 QAR".
  final String? semanticsLabel;

  /// Optional tap (e.g. apply the matching quick filter).
  final VoidCallback? onTap;
}

/// Row of 2-3 equal label/value tiles shown above a list (mockup `.sum`):
/// white tiles, subtle border, 10dp radius, uppercase 10.5sp muted label,
/// 15sp/700 tone-coloured value. Mirrors in RTL; values stay LTR.
class SummaryStrip extends StatelessWidget {
  const SummaryStrip({
    super.key,
    required this.items,
    this.padding = const EdgeInsetsDirectional.fromSTEB(ErpSpace.s4, ErpSpace.s3, ErpSpace.s4, 0),
    this.uppercaseLabels = true,
  });

  /// 1 to 3 tiles (the mockup uses 3). More will squeeze; keep it to 3.
  final List<SummaryItem> items;
  final EdgeInsetsGeometry padding;

  /// Uppercase labels (no effect on scripts without case, e.g. Arabic).
  final bool uppercaseLabels;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: padding,
      child: IntrinsicHeight(
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            for (var i = 0; i < items.length; i++) ...[
              if (i > 0) const SizedBox(width: ErpSpace.s2),
              Expanded(child: _Tile(item: items[i], uppercase: uppercaseLabels)),
            ],
          ],
        ),
      ),
    );
  }
}

class _Tile extends StatelessWidget {
  const _Tile({required this.item, required this.uppercase});

  final SummaryItem item;
  final bool uppercase;

  @override
  Widget build(BuildContext context) {
    final brightness = Theme.of(context).brightness;
    final valueColor = item.tone?.fgFor(brightness) ?? ErpColors.text;
    final body = Container(
      constraints: const BoxConstraints(minHeight: 48),
      padding: const EdgeInsetsDirectional.symmetric(horizontal: 10, vertical: ErpSpace.s2),
      alignment: AlignmentDirectional.centerStart,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            uppercase ? item.label.toUpperCase() : item.label,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              fontSize: 10.5,
              fontWeight: FontWeight.w600,
              letterSpacing: 0.4,
              color: ErpColors.muted,
            ),
          ),
          const SizedBox(height: 1),
          // Numbers stay LTR inside a mirrored layout; aligned to the start edge.
          SizedBox(
            width: double.infinity,
            child: FittedBox(
              fit: BoxFit.scaleDown,
              alignment: AlignmentDirectional.centerStart,
              child: Text(
                item.value,
                maxLines: 1,
                textDirection: TextDirection.ltr,
                style: TextStyle(
                  fontSize: 15,
                  fontWeight: FontWeight.w700,
                  color: valueColor,
                  fontFeatures: ErpFonts.tabular,
                ),
              ),
            ),
          ),
        ],
      ),
    );

    return Semantics(
      label: item.semanticsLabel ?? '${item.label} ${item.value}',
      button: item.onTap != null,
      excludeSemantics: true,
      child: Material(
        color: ErpColors.surface,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(ErpRadius.input),
          side: const BorderSide(color: ErpColors.borderSubtle),
        ),
        clipBehavior: Clip.antiAlias,
        child: item.onTap == null ? body : InkWell(onTap: item.onTap, child: body),
      ),
    );
  }
}
