import 'package:flutter/material.dart';

import '../core/format.dart';
import '../features/common/paged_list_view.dart';

/// A titled card of label/value rows, the usual body of a detail screen.
class InfoCard extends StatelessWidget {
  const InfoCard({super.key, this.title, required this.rows});

  final String? title;
  final List<InfoRow> rows;

  @override
  Widget build(BuildContext context) {
    final shown = rows.where((r) => r.value != null && r.value!.isNotEmpty).toList();
    if (shown.isEmpty) return const SizedBox.shrink();
    return Card(
      margin: const EdgeInsets.only(bottom: 12),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (title != null) Padding(padding: const EdgeInsets.only(bottom: 6), child: Text(title!, style: Theme.of(context).textTheme.titleSmall)),
            ...shown,
          ],
        ),
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
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 3),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(flex: 4, child: Text(label)),
            Expanded(flex: 5, child: Text(value ?? '', textAlign: TextAlign.end, style: TextStyle(fontWeight: bold ? FontWeight.w700 : FontWeight.w400))),
          ],
        ),
      );
}

/// Title row with a status pill: the top of most detail screens.
class DetailHeader extends StatelessWidget {
  const DetailHeader({super.key, required this.title, this.subtitle, this.status});

  final String title;
  final String? subtitle;
  final String? status;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.only(bottom: 12),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Text(title, style: Theme.of(context).textTheme.titleLarge),
                if (subtitle != null && subtitle!.isNotEmpty) Text(subtitle!, style: Theme.of(context).textTheme.bodyMedium),
              ]),
            ),
            if (status != null && status!.isNotEmpty) StatusChip(humanize(status), color: StatusChip.forStatus(status!)),
          ],
        ),
      );
}

/// A scrolling detail body: children in a padded list that can be pulled to refresh.
class DetailBody extends StatelessWidget {
  const DetailBody({super.key, required this.children});

  final List<Widget> children;

  @override
  Widget build(BuildContext context) => ListView(physics: const AlwaysScrollableScrollPhysics(), padding: const EdgeInsets.all(16), children: children);
}

String money(Map<String, dynamic> m, String key, {String currencyKey = 'currencyCode'}) =>
    '${m[currencyKey] ?? ''} ${formatMoney((m[key] as num?) ?? 0)}'.trim();

String? text(Object? v) => v?.toString();
