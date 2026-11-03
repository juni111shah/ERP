import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/format.dart';
import '../../core/models.dart';
import '../../core/session.dart';
import '../../kit/bar_chart.dart';
import '../../kit/stat_card.dart';
import '../../theme/app_theme.dart';
import '../../theme/tokens.dart';
import '../common/paged_list_view.dart';

/// Reloads when the workspace changes.
final dashboardProvider = FutureProvider.autoDispose<Dashboard>((ref) async {
  ref.watch(sessionProvider.select((s) => s.workspace?.id));
  final data = await ref.watch(apiProvider).get('/api/v1/dashboard');
  return Dashboard.fromJson(data as Map<String, dynamic>);
});

class DashboardPage extends ConsumerWidget {
  const DashboardPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final dashboard = ref.watch(dashboardProvider);
    return dashboard.when(
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (e, _) => ErrorView(message: e.toString().replaceFirst(RegExp(r'^ApiException\(\d+\): '), ''), onRetry: () => ref.invalidate(dashboardProvider)),
      data: (d) => RefreshIndicator(
        onRefresh: () async => ref.refresh(dashboardProvider.future),
        child: ListView(
          padding: EdgeInsets.fromLTRB(ErpSpace.s4, ErpSpace.s2, ErpSpace.s4, MediaQuery.paddingOf(context).bottom + ErpSpace.s6),
          children: [
            if (d.kpis.isNotEmpty)
              ErpStatGrid(children: [
                for (var i = 0; i < d.kpis.length; i++)
                  ErpStatCard(
                    label: d.kpis[i].label,
                    value: formatKpi(d.kpis[i].value, d.kpis[i].format),
                    currency: null,
                    stripe: ErpStripe.all[i % ErpStripe.all.length],
                    subtitle: d.kpis[i].caption,
                  ),
              ]),
            for (final c in d.charts) Padding(padding: const EdgeInsets.only(top: ErpSpace.s3), child: _chart(c)),
            if (d.alerts.isNotEmpty) Padding(padding: const EdgeInsets.only(top: ErpSpace.s3), child: _AlertsCard(alerts: d.alerts)),
            if (d.asOf.isNotEmpty)
              Padding(
                padding: const EdgeInsets.all(ErpSpace.s3),
                child: Center(child: Text('As of ${formatDate(d.asOf)}', style: Theme.of(context).textTheme.bodySmall?.copyWith(color: ErpTheme.of(context).muted))),
              ),
          ],
        ),
      ),
    );
  }

  /// Two-series month charts (billed / collected) use the custom panel; anything else keeps the generic bar chart.
  static Widget _chart(DashboardChart c) {
    if (c.series.length == 2 && c.categories.isNotEmpty) {
      return ErpBilledCollectedChart(
        title: c.title,
        subtitle: c.subtitle,
        currency: null,
        billedLabel: c.series[0].name,
        collectedLabel: c.series[1].name,
        data: [
          for (var i = 0; i < c.categories.length; i++)
            ErpMonthValue(
              label: c.categories[i].length > 3 ? c.categories[i].substring(0, 3) : c.categories[i],
              billed: i < c.series[0].values.length ? c.series[0].values[i] : 0,
              collected: i < c.series[1].values.length ? c.series[1].values[i] : 0,
            ),
        ],
      );
    }
    return _ChartCard(chart: c);
  }
}

class _AlertsCard extends StatelessWidget {
  const _AlertsCard({required this.alerts});

  final List<DashboardAlert> alerts;

  static (IconData, ErpTone) _style(String severity) => switch (severity) {
        'critical' => (Icons.error_outline, ErpTone.error),
        'warning' => (Icons.warning_amber_rounded, ErpTone.warning),
        _ => (Icons.info_outline, ErpTone.info),
      };

  @override
  Widget build(BuildContext context) {
    final t = ErpTheme.of(context);
    final b = Theme.of(context).brightness;
    return _Panel(
      title: 'Needs attention',
      child: Column(
        children: [
          for (final a in alerts.take(5))
            Container(
              margin: const EdgeInsets.only(top: ErpSpace.s2),
              padding: const EdgeInsetsDirectional.fromSTEB(ErpSpace.s3, 10, ErpSpace.s3, 10),
              decoration: BoxDecoration(
                color: _style(a.severity).$2.bgFor(b),
                borderRadius: BorderRadius.circular(ErpRadius.input),
                border: Border.all(color: _style(a.severity).$2.borderFor(b)),
              ),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(_style(a.severity).$1, size: 18, color: _style(a.severity).$2.fgFor(b)),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                      Text(a.message, style: TextStyle(fontSize: 13.5, fontWeight: FontWeight.w500, color: t.text)),
                      if (a.category.isNotEmpty) Text(a.category, style: TextStyle(fontSize: 12, color: t.muted)),
                    ]),
                  ),
                ],
              ),
            ),
        ],
      ),
    );
  }
}

/// Dashboard panel: radius 14, hairline border, soft shadow, 14/16 padding.
class _Panel extends StatelessWidget {
  const _Panel({this.title, this.subtitle, required this.child});

  final String? title;
  final String? subtitle;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    final t = ErpTheme.of(context);
    return Container(
      padding: const EdgeInsetsDirectional.fromSTEB(ErpSpace.s4, 14, ErpSpace.s4, 14),
      decoration: BoxDecoration(
        color: t.surface,
        borderRadius: BorderRadius.circular(ErpRadius.panel),
        border: Border.all(color: t.borderSubtle),
        boxShadow: Theme.of(context).brightness == Brightness.dark ? null : ErpShadow.card,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (title != null) Text(title!, style: TextStyle(fontSize: 15, fontWeight: FontWeight.w700, color: t.text)),
          if (subtitle != null) Text(subtitle!, style: TextStyle(fontSize: 12, color: t.muted)),
          child,
        ],
      ),
    );
  }
}

class _ChartCard extends StatelessWidget {
  const _ChartCard({required this.chart});

  final DashboardChart chart;

  static const _palette = [ErpColors.billed, ErpColors.collected, ErpColors.brand, Color(0xFF8B5CF6)];

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final groups = <BarChartGroupData>[
      for (var i = 0; i < chart.categories.length; i++)
        BarChartGroupData(x: i, barsSpace: 2, barRods: [
          for (var s = 0; s < chart.series.length; s++)
            BarChartRodData(
              toY: i < chart.series[s].values.length ? chart.series[s].values[i] : 0,
              color: _palette[s % _palette.length],
              width: chart.series.length > 1 ? 7 : 14,
              borderRadius: const BorderRadius.vertical(top: Radius.circular(3)),
            ),
        ]),
    ];
    return _Panel(
      title: chart.title,
      subtitle: chart.subtitle,
      child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const SizedBox(height: 12),
            SizedBox(
              height: 180,
              child: groups.isEmpty
                  ? const Center(child: Text('No data'))
                  : BarChart(BarChartData(
                      barGroups: groups,
                      gridData: const FlGridData(show: false),
                      borderData: FlBorderData(show: false),
                      titlesData: FlTitlesData(
                        leftTitles: const AxisTitles(sideTitles: SideTitles(showTitles: false)),
                        rightTitles: const AxisTitles(sideTitles: SideTitles(showTitles: false)),
                        topTitles: const AxisTitles(sideTitles: SideTitles(showTitles: false)),
                        bottomTitles: AxisTitles(
                          sideTitles: SideTitles(
                            showTitles: true,
                            reservedSize: 24,
                            getTitlesWidget: (value, meta) {
                              final label = chart.categories[value.toInt().clamp(0, chart.categories.length - 1)];
                              return SideTitleWidget(meta: meta, child: Text(label.length > 5 ? label.substring(0, 5) : label, style: const TextStyle(fontSize: 10)));
                            },
                          ),
                        ),
                      ),
                    )),
            ),
            if (chart.series.length > 1)
              Wrap(spacing: 12, children: [
                for (var s = 0; s < chart.series.length; s++)
                  Row(mainAxisSize: MainAxisSize.min, children: [
                    Container(width: 10, height: 10, decoration: BoxDecoration(color: _palette[s % _palette.length], borderRadius: BorderRadius.circular(3))),
                    const SizedBox(width: 4),
                    Text(chart.series[s].name, style: theme.textTheme.bodySmall),
                  ]),
              ]),
          ],
        ),
    );
  }
}
