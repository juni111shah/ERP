import 'package:fl_chart/fl_chart.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/format.dart';
import '../../core/me.dart';
import '../../core/models.dart';
import '../../core/session.dart';
import '../../kit/bar_chart.dart';
import '../../kit/filter_chips.dart';
import '../../kit/motion.dart';
import '../../kit/registry.dart';
import '../../kit/skeleton.dart';
import '../../kit/stat_card.dart';
import '../../kit/stat_carousel.dart';
import '../../kit/state_view.dart';
import '../../theme/app_theme.dart';
import '../../theme/tokens.dart';
import '../common/paged_list_view.dart';
import 'overview_logic.dart';

/// Reloads when the workspace changes.
final dashboardProvider = FutureProvider.autoDispose<Dashboard>((ref) async {
  ref.watch(sessionProvider.select((s) => s.workspace?.id));
  final data = await ref.watch(apiProvider).get('/api/v1/dashboard');
  return Dashboard.fromJson(data as Map<String, dynamic>);
});

/// Chip value for "every category".
const _all = '*';

void _open(BuildContext context, MobileScreen screen) => Navigator.of(context).push(MaterialPageRoute(builder: screen.builder));

/// Overview: category chips, a 2x2 KPI carousel, then charts and alerts. Pull down to refresh.
class DashboardPage extends ConsumerStatefulWidget {
  const DashboardPage({super.key});

  @override
  ConsumerState<DashboardPage> createState() => _DashboardPageState();
}

class _DashboardPageState extends ConsumerState<DashboardPage> {
  String _category = _all;

  @override
  Widget build(BuildContext context) {
    final dashboard = ref.watch(dashboardProvider);
    final menu = ref.watch(menuProvider).asData?.value ?? const <MenuGroup>[];
    return dashboard.when(
      loading: () => const KeyedSubtree(key: ValueKey('overview-skeleton'), child: ErpSkeleton.overview()),
      error: (e, _) => ErrorView(message: e.toString().replaceFirst(RegExp(r'^ApiException\(\d+\): '), ''), onRetry: () => ref.invalidate(dashboardProvider)),
      data: (d) => RefreshIndicator(
        onRefresh: () async => ref.refresh(dashboardProvider.future),
        child: _content(context, d, menu),
      ),
    );
  }

  Widget _content(BuildContext context, Dashboard d, List<MenuGroup> menu) {
    final t = ErpTheme.of(context);
    const side = EdgeInsets.symmetric(horizontal: ErpSpace.s4);
    final bottom = MediaQuery.paddingOf(context).bottom + ErpSpace.s6;

    if (d.kpis.isEmpty && d.charts.isEmpty && d.alerts.isEmpty) {
      return ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: EdgeInsets.fromLTRB(ErpSpace.s4, ErpSpace.s6, ErpSpace.s4, bottom),
        children: [
          ErpStateView.empty(
            key: const ValueKey('overview-empty'),
            icon: Icons.insights_outlined,
            title: 'Nothing to show yet',
            message: 'Figures appear here once this workspace has activity.',
            actionLabel: 'Refresh',
            actionIcon: Icons.refresh_rounded,
            onAction: () => ref.invalidate(dashboardProvider),
          ),
        ],
      );
    }

    final categories = kpiCategories(d.kpis);
    final selected = _category == _all || categories.any((c) => c.$1 == _category) ? _category : _all;
    final kpis = selected == _all ? d.kpis : [for (final k in d.kpis) if (kpiCategory(k) == selected) k];

    return ListView(
      key: const ValueKey('overview-list'),
      physics: const AlwaysScrollableScrollPhysics(),
      padding: EdgeInsets.only(top: ErpSpace.s1, bottom: bottom),
      children: [
        if (d.kpis.isNotEmpty) ...[
          if (categories.length > 1)
            FilterChipsRow<String>(
              key: const ValueKey('overview-chips'),
              items: [
                FilterChipItem(value: _all, label: 'All', count: d.kpis.length),
                for (final c in categories) FilterChipItem(value: c.$1, label: categoryLabel(c.$1), count: c.$2),
              ],
              selected: selected,
              onSelected: (v) => setState(() => _category = v),
            ),
          Padding(
            padding: const EdgeInsets.fromLTRB(ErpSpace.s4, ErpSpace.s2, ErpSpace.s4, ErpSpace.s2),
            child: _SectionHeading(
              'Highlights',
              trailing: '${kpis.length} ${kpis.length == 1 ? 'metric' : 'metrics'}',
            ),
          ),
          if (kpis.isEmpty)
            ErpStateView.empty(
              key: const ValueKey('overview-category-empty'),
              compact: true,
              icon: Icons.filter_alt_off_outlined,
              title: 'No metrics in this category',
              actionLabel: 'Clear filter',
              actionIcon: Icons.close_rounded,
              onAction: () => setState(() => _category = _all),
            )
          else
            Padding(
              padding: side,
              // A new key per chip replays the fade / slide-in when the filter changes.
              child: ErpFadeSlideIn(
                key: ValueKey('overview-cards-$selected'),
                child: ErpStatCarousel(
                  key: ValueKey('overview-carousel-$selected'),
                  children: [for (final k in kpis) _kpiCard(context, k, menu)],
                ),
              ),
            ),
        ],
        for (final c in d.charts) Padding(padding: const EdgeInsets.fromLTRB(ErpSpace.s4, ErpSpace.s3, ErpSpace.s4, 0), child: _chart(c)),
        if (d.alerts.isNotEmpty) ...[
          const Padding(
            padding: EdgeInsets.fromLTRB(ErpSpace.s4, ErpSpace.s5, ErpSpace.s4, ErpSpace.s2),
            child: _SectionHeading('Needs attention'),
          ),
          Padding(padding: side, child: _AlertsCard(alerts: d.alerts, menu: menu)),
        ],
        if (d.asOf.isNotEmpty)
          Padding(
            padding: const EdgeInsets.all(ErpSpace.s3),
            child: Center(child: Text('As of ${formatDate(d.asOf)}', style: Theme.of(context).textTheme.bodySmall?.copyWith(color: t.muted))),
          ),
      ],
    );
  }

  static Widget _kpiCard(BuildContext context, Kpi k, List<MenuGroup> menu) {
    final full = formatKpi(k.value, k.format);
    final screen = screenForLink(k.link, menu: menu);
    return Tooltip(
      message: '${k.label}: $full',
      triggerMode: TooltipTriggerMode.longPress,
      child: ErpStatCard(
        key: ValueKey('kpi-${k.key}'),
        label: k.label,
        value: compactKpi(k.value, k.format, full),
        currency: null,
        stripe: stripeForKey(k.key),
        subtitle: k.caption,
        semanticsLabel: [k.label, full, if ((k.caption ?? '').isNotEmpty) k.caption!].join(', '),
        onTap: screen == null ? null : () => _open(context, screen),
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
              label: axisLabel(c.categories[i], max: 6),
              fullLabel: c.categories[i],
              billed: i < c.series[0].values.length ? c.series[0].values[i] : 0,
              collected: i < c.series[1].values.length ? c.series[1].values[i] : 0,
            ),
        ],
      );
    }
    return _ChartCard(chart: c);
  }
}

/// Small-caps section title (mockup: "EXECUTIVE HIGHLIGHTS", "NEEDS ATTENTION").
class _SectionHeading extends StatelessWidget {
  const _SectionHeading(this.text, {this.trailing});

  final String text;
  final String? trailing;

  @override
  Widget build(BuildContext context) {
    final t = ErpTheme.of(context);
    final isArabic = Localizations.localeOf(context).languageCode == 'ar';
    return Semantics(
      header: true,
      child: Row(children: [
        Expanded(
          child: Text(
            isArabic ? text : text.toUpperCase(),
            style: ErpType.sectionLabel.copyWith(letterSpacing: isArabic ? 0 : null, color: t.muted),
          ),
        ),
        if (trailing != null)
          Text(trailing!, style: TextStyle(fontSize: ErpType.caption, fontWeight: FontWeight.w500, color: t.muted, fontFeatures: ErpFonts.tabular)),
      ]),
    );
  }
}

class _AlertsCard extends StatefulWidget {
  const _AlertsCard({required this.alerts, required this.menu});

  final List<DashboardAlert> alerts;
  final List<MenuGroup> menu;

  @override
  State<_AlertsCard> createState() => _AlertsCardState();
}

class _AlertsCardState extends State<_AlertsCard> {
  static const _collapsed = 5;
  bool _expanded = false;

  static (IconData, ErpTone) _style(String severity) => switch (severity) {
        'critical' => (Icons.error_outline, ErpTone.error),
        'warning' => (Icons.warning_amber_rounded, ErpTone.warning),
        _ => (Icons.info_outline, ErpTone.info),
      };

  @override
  Widget build(BuildContext context) {
    final t = ErpTheme.of(context);
    final b = Theme.of(context).brightness;
    final alerts = widget.alerts;
    final shown = _expanded ? alerts : alerts.take(_collapsed).toList();
    return _Panel(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          for (var i = 0; i < shown.length; i++)
            Builder(builder: (context) {
              final a = shown[i];
              final style = _style(a.severity);
              final screen = screenForLink(a.link, menu: widget.menu);
              return Padding(
                padding: EdgeInsets.only(top: i == 0 ? 0 : ErpSpace.s2),
                child: Material(
                  color: style.$2.bgFor(b),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(ErpRadius.input),
                    side: BorderSide(color: style.$2.borderFor(b)),
                  ),
                  clipBehavior: Clip.antiAlias,
                  child: InkWell(
                    key: ValueKey('alert-$i'),
                    onTap: screen == null ? null : () => _open(context, screen),
                    child: ConstrainedBox(
                      constraints: const BoxConstraints(minHeight: 48),
                      child: Padding(
                        padding: const EdgeInsetsDirectional.fromSTEB(ErpSpace.s3, ErpSpace.s25, ErpSpace.s2, ErpSpace.s25),
                        child: Row(
                          children: [
                            Icon(style.$1, size: 18, color: style.$2.fgFor(b)),
                            const SizedBox(width: ErpSpace.s25),
                            Expanded(
                              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                                Text(a.message, style: TextStyle(fontSize: ErpType.bodySmall, fontWeight: FontWeight.w500, color: t.text)),
                                if (a.category.isNotEmpty) Text(a.category, style: TextStyle(fontSize: ErpType.caption, color: t.muted)),
                              ]),
                            ),
                            if (screen != null) Icon(Icons.chevron_right_rounded, size: 20, color: t.muted),
                          ],
                        ),
                      ),
                    ),
                  ),
                ),
              );
            }),
          if (alerts.length > _collapsed)
            Align(
              alignment: AlignmentDirectional.centerEnd,
              child: TextButton(
                onPressed: () => setState(() => _expanded = !_expanded),
                child: Text(_expanded ? 'Show fewer' : 'View all (${alerts.length})'),
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
      padding: const EdgeInsetsDirectional.fromSTEB(ErpSpace.s4, ErpSpace.s35, ErpSpace.s4, ErpSpace.s35),
      decoration: BoxDecoration(
        color: t.surface,
        borderRadius: BorderRadius.circular(ErpRadius.panel),
        border: Border.all(color: t.borderSubtle),
        boxShadow: Theme.of(context).brightness == Brightness.dark ? null : ErpShadow.card,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (title != null) Text(title!, style: TextStyle(fontSize: ErpType.bodyLarge, fontWeight: FontWeight.w700, color: t.text)),
          if (subtitle != null) Text(subtitle!, style: TextStyle(fontSize: ErpType.caption, color: t.muted)),
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
    final t = ErpTheme.of(context);
    final cats = chart.categories;
    // Many or long labels: rotate them instead of cutting them; tap a bar for the full label.
    final rotate = cats.length > 5 && cats.any((c) => axisLabel(c).length > 4);
    final groups = <BarChartGroupData>[
      for (var i = 0; i < cats.length; i++)
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
            const SizedBox(height: ErpSpace.s3),
            SizedBox(
              height: rotate ? 200 : 180,
              child: groups.isEmpty
                  ? const ErpStateView.empty(key: ValueKey('chart-empty'), compact: true, icon: Icons.bar_chart_rounded, title: 'No chart data', message: 'Nothing to plot for this period.')
                  : BarChart(BarChartData(
                      barGroups: groups,
                      gridData: const FlGridData(show: false),
                      borderData: FlBorderData(show: false),
                      barTouchData: BarTouchData(
                        enabled: true,
                        touchTooltipData: BarTouchTooltipData(
                          fitInsideHorizontally: true,
                          fitInsideVertically: true,
                          getTooltipColor: (_) => t.text.withValues(alpha: 0.92),
                          getTooltipItem: (group, groupIndex, rod, rodIndex) => BarTooltipItem(
                            '${cats[group.x.clamp(0, cats.length - 1)]}\n',
                            TextStyle(color: t.surface, fontSize: ErpType.caption, fontWeight: FontWeight.w700),
                            children: [
                              TextSpan(
                                text: '${chart.series[rodIndex].name}: ${erpCompactNumber(rod.toY)}',
                                style: TextStyle(color: t.surface, fontSize: ErpType.caption, fontWeight: FontWeight.w500, fontFeatures: ErpFonts.tabular),
                              ),
                            ],
                          ),
                        ),
                      ),
                      titlesData: FlTitlesData(
                        leftTitles: const AxisTitles(sideTitles: SideTitles(showTitles: false)),
                        rightTitles: const AxisTitles(sideTitles: SideTitles(showTitles: false)),
                        topTitles: const AxisTitles(sideTitles: SideTitles(showTitles: false)),
                        bottomTitles: AxisTitles(
                          sideTitles: SideTitles(
                            showTitles: true,
                            reservedSize: rotate ? 44 : 24,
                            getTitlesWidget: (value, meta) {
                              final label = cats[value.toInt().clamp(0, cats.length - 1)];
                              return SideTitleWidget(
                                meta: meta,
                                angle: rotate ? -0.6 : 0,
                                child: Text(axisLabel(label, max: rotate ? 10 : 7), style: TextStyle(fontSize: ErpType.micro, color: t.muted)),
                              );
                            },
                          ),
                        ),
                      ),
                    )),
            ),
            if (chart.series.length > 1)
              Wrap(spacing: ErpSpace.s3, children: [
                for (var s = 0; s < chart.series.length; s++)
                  Row(mainAxisSize: MainAxisSize.min, children: [
                    Container(width: 10, height: 10, decoration: BoxDecoration(color: _palette[s % _palette.length], borderRadius: BorderRadius.circular(3))),
                    const SizedBox(width: ErpSpace.s1),
                    Text(chart.series[s].name, style: theme.textTheme.bodySmall),
                  ]),
              ]),
          ],
        ),
    );
  }
}
