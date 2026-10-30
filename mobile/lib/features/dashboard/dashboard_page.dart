import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/format.dart';
import '../../core/models.dart';
import '../../core/session.dart';
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
          padding: const EdgeInsets.all(12),
          children: [
            if (d.alerts.isNotEmpty) _AlertsCard(alerts: d.alerts),
            if (d.kpis.isNotEmpty)
              GridView.count(
                crossAxisCount: 2,
                childAspectRatio: 1.55,
                mainAxisSpacing: 8,
                crossAxisSpacing: 8,
                shrinkWrap: true,
                physics: const NeverScrollableScrollPhysics(),
                children: [for (final k in d.kpis) _KpiTile(kpi: k)],
              ),
            for (final c in d.charts) _ChartCard(chart: c),
            if (d.asOf.isNotEmpty) Padding(padding: const EdgeInsets.all(8), child: Center(child: Text('As of ${formatDate(d.asOf)}', style: Theme.of(context).textTheme.bodySmall))),
          ],
        ),
      ),
    );
  }
}

class _KpiTile extends StatelessWidget {
  const _KpiTile({required this.kpi});

  final Kpi kpi;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Text(kpi.label, style: theme.textTheme.labelMedium, maxLines: 2, overflow: TextOverflow.ellipsis),
            FittedBox(
              fit: BoxFit.scaleDown,
              alignment: Alignment.centerLeft,
              child: Text(formatKpi(kpi.value, kpi.format), style: theme.textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.w700)),
            ),
            Text(kpi.caption ?? '', style: theme.textTheme.bodySmall, maxLines: 1, overflow: TextOverflow.ellipsis),
          ],
        ),
      ),
    );
  }
}

class _AlertsCard extends StatelessWidget {
  const _AlertsCard({required this.alerts});

  final List<DashboardAlert> alerts;

  static (IconData, Color) _style(String severity) => switch (severity) {
        'critical' => (Icons.error_outline, Colors.red.shade700),
        'warning' => (Icons.warning_amber_outlined, Colors.orange.shade800),
        _ => (Icons.info_outline, Colors.blue.shade700),
      };

  @override
  Widget build(BuildContext context) => Card(
        margin: const EdgeInsets.only(bottom: 8),
        child: Column(
          children: [
            for (final a in alerts.take(5))
              ListTile(
                dense: true,
                leading: Icon(_style(a.severity).$1, color: _style(a.severity).$2),
                title: Text(a.message),
                subtitle: a.category.isEmpty ? null : Text(a.category),
              ),
          ],
        ),
      );
}

class _ChartCard extends StatelessWidget {
  const _ChartCard({required this.chart});

  final DashboardChart chart;

  static const _palette = [Color(0xFF1565C0), Color(0xFF2E7D32), Color(0xFFEF6C00), Color(0xFF6A1B9A)];

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
              borderRadius: BorderRadius.circular(2),
            ),
        ]),
    ];
    return Card(
      margin: const EdgeInsets.only(top: 8),
      child: Padding(
        padding: const EdgeInsets.fromLTRB(12, 12, 12, 8),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(chart.title, style: theme.textTheme.titleSmall),
            if (chart.subtitle != null) Text(chart.subtitle!, style: theme.textTheme.bodySmall),
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
                    Container(width: 10, height: 10, color: _palette[s % _palette.length]),
                    const SizedBox(width: 4),
                    Text(chart.series[s].name, style: theme.textTheme.bodySmall),
                  ]),
              ]),
          ],
        ),
      ),
    );
  }
}
