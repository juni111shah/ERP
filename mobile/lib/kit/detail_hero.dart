import 'package:flutter/material.dart';

import 'package:erp_mobile/theme/tokens.dart';

/// One label/value pair in the [DetailHero] key-facts grid.
@immutable
class HeroFact {
  const HeroFact(this.label, this.value);

  final String label;
  final String value;
}

/// Brand-green header (eyebrow, title, subtitle, status tag slot) with a raised white "facts" card overlapping its
/// bottom edge: balance first, optional total on the end side, a progress bar, then a 2-column key-facts grid.
///
/// RTL-safe: all paddings/alignments are directional; numbers are rendered LTR so they hold in Arabic.
class DetailHero extends StatelessWidget {
  const DetailHero({
    super.key,
    required this.title,
    this.eyebrow,
    this.subtitle,
    this.statusTag,
    this.balanceLabel = 'BALANCE DUE',
    required this.balance,
    this.currency = 'QAR',
    this.totalLabel = 'TOTAL',
    this.total,
    this.progress,
    this.facts = const <HeroFact>[],
    this.overlap = 40,
  });

  final String title;

  /// Small uppercase line above the title, e.g. "TAX INVOICE · INV-2026-0411".
  final String? eyebrow;
  final String? subtitle;

  /// Status tag slot (top end of the header), e.g. a tone pill.
  final Widget? statusTag;
  final String balanceLabel;

  /// Already formatted amount, e.g. "50,720.00".
  final String balance;
  final String? currency;
  final String totalLabel;

  /// Already formatted total shown on the end side of the balance row.
  final String? total;

  /// 0..1; hidden when null.
  final double? progress;
  final List<HeroFact> facts;

  /// How far the facts card overlaps the green header.
  final double overlap;

  static const _onBrand = ErpColors.surface;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Container(
          decoration: const BoxDecoration(
            gradient: LinearGradient(
              begin: Alignment.topCenter,
              end: Alignment.bottomCenter,
              colors: [ErpColors.brandInk, ErpColors.brandHover],
            ),
          ),
          padding: const EdgeInsetsDirectional.fromSTEB(ErpSpace.s4, ErpSpace.s2, ErpSpace.s4, ErpSpace.s4),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(
                    child: eyebrow == null
                        ? const SizedBox.shrink()
                        : Text(
                            eyebrow!.toUpperCase(),
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: textTheme.labelSmall?.copyWith(
                              color: _onBrand.withValues(alpha: 0.8),
                              letterSpacing: 0.6,
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                  ),
                  if (statusTag != null) ...[
                    const SizedBox(width: ErpSpace.s2),
                    statusTag!,
                  ],
                ],
              ),
              const SizedBox(height: ErpSpace.s1),
              Text(
                title,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: textTheme.headlineSmall?.copyWith(color: _onBrand, fontWeight: FontWeight.w700),
              ),
              if (subtitle != null && subtitle!.isNotEmpty)
                Padding(
                  padding: const EdgeInsetsDirectional.only(top: ErpSpace.s1),
                  child: Text(
                    subtitle!,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: textTheme.bodyMedium?.copyWith(color: _onBrand.withValues(alpha: 0.85)),
                  ),
                ),
            ],
          ),
        ),
        // The card sits on a strip that continues the header colour for [overlap] px, so it reads as overlapping.
        Stack(
          children: [
            PositionedDirectional(
              start: 0,
              end: 0,
              top: 0,
              height: overlap,
              child: const ColoredBox(color: ErpColors.brandHover),
            ),
            Padding(
              padding: const EdgeInsetsDirectional.symmetric(horizontal: ErpSpace.s3),
              child: _FactsCard(
                balanceLabel: balanceLabel,
                balance: balance,
                currency: currency,
                totalLabel: totalLabel,
                total: total,
                progress: progress,
                facts: facts,
              ),
            ),
          ],
        ),
      ],
    );
  }
}

class _FactsCard extends StatelessWidget {
  const _FactsCard({
    required this.balanceLabel,
    required this.balance,
    required this.currency,
    required this.totalLabel,
    required this.total,
    required this.progress,
    required this.facts,
  });

  final String balanceLabel;
  final String balance;
  final String? currency;
  final String totalLabel;
  final String? total;
  final double? progress;
  final List<HeroFact> facts;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;
    final labelStyle = textTheme.labelSmall?.copyWith(color: ErpColors.muted, letterSpacing: 0.5, fontWeight: FontWeight.w600);
    final rows = <Widget>[];
    for (var i = 0; i < facts.length; i += 2) {
      rows.add(Padding(
        padding: EdgeInsetsDirectional.only(top: i == 0 ? 0 : ErpSpace.s3),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(child: _Fact(fact: facts[i], labelStyle: labelStyle)),
            const SizedBox(width: ErpSpace.s3),
            Expanded(child: i + 1 < facts.length ? _Fact(fact: facts[i + 1], labelStyle: labelStyle) : const SizedBox.shrink()),
          ],
        ),
      ));
    }

    return Container(
      decoration: BoxDecoration(
        color: ErpColors.surface,
        borderRadius: BorderRadius.circular(ErpRadius.panel),
        border: Border.all(color: ErpColors.borderSubtle),
        boxShadow: ErpShadow.raised,
      ),
      padding: const EdgeInsetsDirectional.all(ErpSpace.s4),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(balanceLabel.toUpperCase(), style: labelStyle),
                    const SizedBox(height: ErpSpace.s1),
                    Text.rich(
                      TextSpan(children: [
                        if (currency != null && currency!.isNotEmpty)
                          TextSpan(text: '${currency!} ', style: textTheme.bodyMedium?.copyWith(color: ErpColors.text2)),
                        TextSpan(
                          text: balance,
                          style: textTheme.headlineMedium?.copyWith(
                            color: ErpColors.text,
                            fontWeight: FontWeight.w700,
                            fontFeatures: ErpFonts.tabular,
                          ),
                        ),
                      ]),
                      textDirection: TextDirection.ltr,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                    ),
                  ],
                ),
              ),
              if (total != null && total!.isNotEmpty) ...[
                const SizedBox(width: ErpSpace.s3),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    Text(totalLabel.toUpperCase(), style: labelStyle),
                    const SizedBox(height: ErpSpace.s1),
                    Text(
                      total!,
                      textDirection: TextDirection.ltr,
                      style: textTheme.titleMedium?.copyWith(
                        color: ErpColors.text,
                        fontWeight: FontWeight.w700,
                        fontFeatures: ErpFonts.tabular,
                      ),
                    ),
                  ],
                ),
              ],
            ],
          ),
          if (progress != null) ...[
            const SizedBox(height: ErpSpace.s3),
            ClipRRect(
              borderRadius: BorderRadius.circular(ErpRadius.pill),
              child: LinearProgressIndicator(
                value: progress!.clamp(0.0, 1.0),
                minHeight: 6,
                color: ErpColors.brand,
                backgroundColor: ErpColors.borderSubtle,
              ),
            ),
          ],
          if (rows.isNotEmpty) ...[
            const SizedBox(height: ErpSpace.s4),
            ...rows,
          ],
        ],
      ),
    );
  }
}

class _Fact extends StatelessWidget {
  const _Fact({required this.fact, required this.labelStyle});

  final HeroFact fact;
  final TextStyle? labelStyle;

  @override
  Widget build(BuildContext context) => Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(fact.label.toUpperCase(), style: labelStyle, maxLines: 1, overflow: TextOverflow.ellipsis),
          const SizedBox(height: ErpSpace.s05),
          Text(
            fact.value,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(color: ErpColors.text, fontWeight: FontWeight.w600),
          ),
        ],
      );
}
