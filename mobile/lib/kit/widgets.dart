import 'package:flutter/material.dart';

import '../core/format.dart';
import '../features/common/paged_list_view.dart';
import '../theme/app_theme.dart';
import '../theme/tokens.dart';

/// A titled card of label/value rows, the usual body of a detail screen.
class InfoCard extends StatelessWidget {
  const InfoCard({super.key, this.title, required this.rows});

  final String? title;
  final List<InfoRow> rows;

  @override
  Widget build(BuildContext context) {
    final shown = rows.where((r) => r.value != null && r.value!.isNotEmpty).toList();
    if (shown.isEmpty) return const SizedBox.shrink();
    final t = ErpTheme.of(context);
    return Container(
      margin: const EdgeInsets.only(bottom: ErpSpace.s3),
      padding: const EdgeInsetsDirectional.fromSTEB(ErpSpace.s4, ErpSpace.s35, ErpSpace.s4, ErpSpace.s25),
      decoration: BoxDecoration(
        color: t.surface,
        borderRadius: BorderRadius.circular(ErpRadius.panel),
        border: Border.all(color: t.borderSubtle),
        boxShadow: Theme.of(context).brightness == Brightness.dark ? null : ErpShadow.card,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (title != null)
            Padding(
              padding: const EdgeInsets.only(bottom: ErpSpace.s2),
              child: Text(title!, style: Theme.of(context).textTheme.titleSmall?.copyWith(fontWeight: FontWeight.w700, color: t.text)),
            ),
          for (var i = 0; i < shown.length; i++) ...[
            if (i > 0) Divider(height: 1, color: t.borderSubtle),
            shown[i],
          ],
        ],
      ),
    );
  }
}

class InfoRow extends StatelessWidget {
  const InfoRow(this.label, this.value, {super.key, this.bold = false});

  final String label;
  final String? value;
  final bool bold;

  @override
  Widget build(BuildContext context) {
    final t = ErpTheme.of(context);
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 7),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(flex: 4, child: Text(label, style: TextStyle(fontSize: ErpType.bodySmall, color: t.muted))),
          Expanded(
            flex: 5,
            child: Text(value ?? '',
                textAlign: TextAlign.end,
                style: TextStyle(fontSize: bold ? 14.5 : 13.5, color: t.text, fontWeight: bold ? FontWeight.w700 : FontWeight.w500, fontFeatures: ErpFonts.tabular)),
          ),
        ],
      ),
    );
  }
}

/// Title row with a status pill: the top of most detail screens.
class DetailHeader extends StatelessWidget {
  const DetailHeader({super.key, required this.title, this.subtitle, this.status});

  final String title;
  final String? subtitle;
  final String? status;

  /// Brand-gradient header card: title, subtitle and status tag on the brand hero colours.
  @override
  Widget build(BuildContext context) {
    final t = ErpTheme.of(context);
    return Container(
      margin: const EdgeInsets.only(bottom: ErpSpace.s3),
      padding: const EdgeInsetsDirectional.fromSTEB(ErpSpace.s4, ErpSpace.s4, ErpSpace.s4, 18),
      decoration: BoxDecoration(gradient: t.heroGradient, borderRadius: BorderRadius.circular(ErpRadius.panel)),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(
            child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              Text(title, style: Theme.of(context).textTheme.titleLarge?.copyWith(color: Colors.white, fontWeight: FontWeight.w700)),
              if (subtitle != null && subtitle!.isNotEmpty)
                Padding(
                  padding: const EdgeInsets.only(top: ErpSpace.s05),
                  child: Text(subtitle!, style: Theme.of(context).textTheme.bodyMedium?.copyWith(color: Colors.white.withValues(alpha: 0.82))),
                ),
            ]),
          ),
          if (status != null && status!.isNotEmpty) ...[
            const SizedBox(width: ErpSpace.s2),
            StatusChip(humanize(status), color: StatusChip.forStatus(status!)),
          ],
        ],
      ),
    );
  }
}

/// A scrolling detail body: children in a padded list that can be pulled to refresh.
class DetailBody extends StatelessWidget {
  const DetailBody({super.key, required this.children});

  final List<Widget> children;

  @override
  Widget build(BuildContext context) => ListView(physics: const AlwaysScrollableScrollPhysics(), padding: const EdgeInsets.all(ErpSpace.s4), children: children);
}

String money(Map<String, dynamic> m, String key, {String currencyKey = 'currencyCode'}) =>
    '${m[currencyKey] ?? ''} ${formatMoney((m[key] as num?) ?? 0)}'.trim();

String? text(Object? v) => v?.toString();
