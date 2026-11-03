import 'package:flutter/material.dart';

import 'package:erp_mobile/theme/tokens.dart';

/// Ant-style status pill: tone background, tone border, tone text and a
/// leading 6dp dot (mockup `.tag`: 22dp high, 11.5sp/600, pill radius).
///
/// The [label] is always supplied by the caller (already translated), e.g.
/// "Paid", "Sent", "Partly paid", "Overdue", "Draft". Use
/// [ErpToneColors.forStatus] to pick a [tone] from a backend status code.
///
/// Layout mirrors in RTL (the dot sits at the start edge). If the label
/// contains figures ("74 days overdue") they keep their own direction via the
/// normal bidi algorithm; pass [forceLtr] for labels that are purely numeric
/// or codes (e.g. "PDC 10 Nov").
class StatusTag extends StatelessWidget {
  const StatusTag({
    super.key,
    required this.label,
    this.tone = ErpTone.neutral,
    this.showDot = true,
    this.showBorder = true,
    this.semanticsLabel,
    this.forceLtr = false,
  });

  /// Visible, already-localized text.
  final String label;

  final ErpTone tone;

  /// Show the leading dot (hide it inside very tight rows).
  final bool showDot;

  /// Draw the tone border; set false on tinted/hero backgrounds.
  final bool showBorder;

  /// Optional screen-reader text (e.g. "Status: Paid"); defaults to [label].
  final String? semanticsLabel;

  /// Wrap the label in a left-to-right isolate.
  final bool forceLtr;

  @override
  Widget build(BuildContext context) {
    final brightness = Theme.of(context).brightness;
    final bg = tone.bgFor(brightness);
    final fg = tone.fgFor(brightness);
    final border = tone.borderFor(brightness);
    final text = forceLtr ? '\u2066$label\u2069' : label;

    return Semantics(
      label: semanticsLabel ?? label,
      excludeSemantics: true,
      child: Container(
        height: 22,
        padding: const EdgeInsetsDirectional.symmetric(horizontal: ErpSpace.s2),
        decoration: BoxDecoration(
          color: bg,
          borderRadius: BorderRadius.circular(ErpRadius.pill),
          border: Border.all(color: showBorder ? border : Colors.transparent),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            if (showDot) ...[
              Container(
                width: 6,
                height: 6,
                decoration: BoxDecoration(color: fg, shape: BoxShape.circle),
              ),
              const SizedBox(width: 5),
            ],
            Flexible(
              child: Text(
                text,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  fontSize: ErpType.caption,
                  height: 1.2,
                  fontWeight: FontWeight.w600,
                  color: fg,
                  fontFeatures: ErpFonts.tabular,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
