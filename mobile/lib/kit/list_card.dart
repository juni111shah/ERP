import 'package:flutter/material.dart';

import 'package:erp_mobile/theme/tokens.dart';

/// Document row card for invoice / bill / receipt lists.
///
/// Layout (mirrors in RTL):
///   Title ...................... QAR 48,250.00
///   INV-2026-0412 (mono)                 [tag]
///   optional meta line
///   optional progress bar + "76,080 of 126,800 received ...... 60%"
/// [overdue] adds a 3px error border on the start edge.
class ErpListCard extends StatelessWidget {
  const ErpListCard({
    super.key,
    required this.title,
    required this.docNumber,
    required this.amount,
    this.currency = 'QAR',
    this.amountColor,
    this.tag,
    this.meta,
    this.progress,
    this.progressLabel,
    this.progressTrailing,
    this.overdue = false,
    this.onTap,
    this.margin = const EdgeInsetsDirectional.only(bottom: ErpSpace.s2),
  });

  final String title;

  /// Rendered in a monospace font, always LTR.
  final String docNumber;

  /// Already formatted amount, e.g. "48,250.00".
  final String amount;
  final String? currency;

  /// Defaults to the text colour, or the error tone when [overdue].
  final Color? amountColor;

  /// Trailing tag slot (status pill), aligned to the end under the amount.
  final Widget? tag;

  /// Optional muted line under the doc number, e.g. "Due 17 Nov · 14 days".
  final String? meta;

  /// 0..1; the progress row is shown only when non-null.
  final double? progress;
  final String? progressLabel;

  /// End-side text of the progress row, e.g. "60%". Defaults to the rounded percentage.
  final String? progressTrailing;
  final bool overdue;
  final VoidCallback? onTap;
  final EdgeInsetsGeometry margin;

  static const double _stripe = 3;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;
    final radius = BorderRadius.circular(ErpRadius.card);
    final amtColor = amountColor ?? (overdue ? ErpTone.error.fg : ErpColors.text);
    final p = progress?.clamp(0.0, 1.0);

    final content = Padding(
      padding: const EdgeInsetsDirectional.fromSTEB(ErpSpace.s3, ErpSpace.s3, ErpSpace.s3, ErpSpace.s3),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Text(
                  title,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: textTheme.titleSmall?.copyWith(color: ErpColors.text, fontWeight: FontWeight.w600),
                ),
              ),
              const SizedBox(width: ErpSpace.s2),
              Text.rich(
                TextSpan(children: [
                  if (currency != null && currency!.isNotEmpty)
                    TextSpan(text: '${currency!} ', style: textTheme.labelSmall?.copyWith(color: amtColor.withValues(alpha: 0.75))),
                  TextSpan(
                    text: amount,
                    style: textTheme.titleSmall?.copyWith(
                      color: amtColor,
                      fontWeight: FontWeight.w700,
                      fontFeatures: ErpFonts.tabular,
                    ),
                  ),
                ]),
                textDirection: TextDirection.ltr,
                textAlign: TextAlign.end,
                maxLines: 1,
              ),
            ],
          ),
          const SizedBox(height: ErpSpace.s1),
          Row(
            crossAxisAlignment: CrossAxisAlignment.center,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      docNumber,
                      textDirection: TextDirection.ltr,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: textTheme.bodySmall?.copyWith(
                        color: ErpColors.muted,
                        fontFamily: 'monospace',
                        fontFamilyFallback: const ['RobotoMono', 'Menlo', 'Courier New'],
                        letterSpacing: 0.2,
                      ),
                    ),
                    if (meta != null && meta!.isNotEmpty)
                      Padding(
                        padding: const EdgeInsetsDirectional.only(top: ErpSpace.s05),
                        child: Text(
                          meta!,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: textTheme.bodySmall?.copyWith(color: ErpColors.muted),
                        ),
                      ),
                  ],
                ),
              ),
              if (tag != null) ...[
                const SizedBox(width: ErpSpace.s2),
                tag!,
              ],
            ],
          ),
          if (p != null) ...[
            const SizedBox(height: ErpSpace.s2),
            ClipRRect(
              borderRadius: BorderRadius.circular(ErpRadius.pill),
              child: LinearProgressIndicator(
                value: p,
                minHeight: 4,
                color: ErpColors.brand,
                backgroundColor: ErpColors.borderSubtle,
              ),
            ),
            const SizedBox(height: ErpSpace.s1),
            Row(
              children: [
                Expanded(
                  child: Text(
                    progressLabel ?? '',
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: textTheme.bodySmall?.copyWith(color: ErpColors.text2),
                  ),
                ),
                Text(
                  progressTrailing ?? '${(p * 100).round()}%',
                  textDirection: TextDirection.ltr,
                  textAlign: TextAlign.end,
                  style: textTheme.bodySmall?.copyWith(color: ErpColors.text2, fontFeatures: ErpFonts.tabular),
                ),
              ],
            ),
          ],
        ],
      ),
    );

    return Padding(
      padding: margin,
      child: Material(
        color: ErpColors.surface,
        shape: RoundedRectangleBorder(borderRadius: radius, side: const BorderSide(color: ErpColors.borderSubtle)),
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: onTap,
          child: overdue
              ? IntrinsicHeight(
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      ColoredBox(color: ErpTone.error.fg, child: const SizedBox(width: _stripe)),
                      Expanded(child: content),
                    ],
                  ),
                )
              : content,
        ),
      ),
    );
  }
}
