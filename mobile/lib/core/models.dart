/// A workspace (company/tenant) the signed-in user can work in.
class Workspace {
  const Workspace({required this.id, required this.code, required this.name, required this.industry});

  final String id;
  final String code;
  final String name;
  final String industry;

  factory Workspace.fromJson(Map<String, dynamic> json) => Workspace(
        id: json['id'].toString(),
        code: (json['code'] ?? '').toString(),
        name: (json['name'] ?? json['code'] ?? '').toString(),
        industry: (json['industry'] ?? '').toString(),
      );
}

/// The signed-in session as the API returns it (the refresh token itself stays in the secure store).
class SessionInfo {
  const SessionInfo({required this.accessToken, required this.expiresIn, required this.workspace, required this.workspaces});

  final String accessToken;
  final int expiresIn;
  final Workspace? workspace;
  final List<Workspace> workspaces;

  factory SessionInfo.fromJson(Map<String, dynamic> json) => SessionInfo(
        accessToken: json['accessToken'].toString(),
        expiresIn: (json['expiresIn'] as num?)?.toInt() ?? 0,
        workspace: json['workspace'] is Map<String, dynamic> ? Workspace.fromJson(json['workspace'] as Map<String, dynamic>) : null,
        workspaces: [
          for (final w in (json['workspaces'] as List<dynamic>? ?? const [])) Workspace.fromJson(w as Map<String, dynamic>),
        ],
      );
}

/// One page of a list endpoint (`items`, `total`, `page`, `pageSize`).
class Paged {
  const Paged({required this.items, required this.total, required this.page, required this.pageSize});

  final List<Map<String, dynamic>> items;
  final int total;
  final int page;
  final int pageSize;

  bool get hasNext => page * pageSize < total;

  factory Paged.fromJson(Map<String, dynamic> json) => Paged(
        items: [for (final i in (json['items'] as List<dynamic>? ?? const [])) i as Map<String, dynamic>],
        total: (json['total'] as num?)?.toInt() ?? 0,
        page: (json['page'] as num?)?.toInt() ?? 1,
        pageSize: (json['pageSize'] as num?)?.toInt() ?? 25,
      );
}

/// A headline number on the dashboard. [format] is "money", "percent" or "number".
class Kpi {
  const Kpi({required this.key, required this.label, required this.value, required this.format, this.caption});

  final String key;
  final String label;
  final double value;
  final String format;
  final String? caption;

  factory Kpi.fromJson(Map<String, dynamic> json) => Kpi(
        key: json['key'].toString(),
        label: json['label'].toString(),
        value: (json['value'] as num?)?.toDouble() ?? 0,
        format: (json['format'] ?? 'number').toString(),
        caption: json['caption'] as String?,
      );
}

class ChartSeries {
  const ChartSeries({required this.name, required this.values});

  final String name;
  final List<double> values;

  factory ChartSeries.fromJson(Map<String, dynamic> json) => ChartSeries(
        name: json['name'].toString(),
        values: [for (final v in (json['values'] as List<dynamic>? ?? const [])) (v as num).toDouble()],
      );
}

/// A chart; [kind] is "bar", "stacked" or "hbar". The phone shows all of them as vertical bars.
class DashboardChart {
  const DashboardChart({required this.key, required this.title, required this.kind, required this.categories, required this.series, this.subtitle});

  final String key;
  final String title;
  final String kind;
  final List<String> categories;
  final List<ChartSeries> series;
  final String? subtitle;

  factory DashboardChart.fromJson(Map<String, dynamic> json) => DashboardChart(
        key: json['key'].toString(),
        title: json['title'].toString(),
        kind: (json['kind'] ?? 'bar').toString(),
        categories: [for (final c in (json['categories'] as List<dynamic>? ?? const [])) c.toString()],
        series: [for (final s in (json['series'] as List<dynamic>? ?? const [])) ChartSeries.fromJson(s as Map<String, dynamic>)],
        subtitle: json['subtitle'] as String?,
      );
}

/// [severity] is "critical", "warning" or "info".
class DashboardAlert {
  const DashboardAlert({required this.severity, required this.category, required this.message});

  final String severity;
  final String category;
  final String message;

  factory DashboardAlert.fromJson(Map<String, dynamic> json) => DashboardAlert(
        severity: (json['severity'] ?? 'info').toString(),
        category: (json['category'] ?? '').toString(),
        message: (json['message'] ?? '').toString(),
      );
}

class Dashboard {
  const Dashboard({required this.asOf, required this.kpis, required this.charts, required this.alerts});

  final String asOf;
  final List<Kpi> kpis;
  final List<DashboardChart> charts;
  final List<DashboardAlert> alerts;

  factory Dashboard.fromJson(Map<String, dynamic> json) => Dashboard(
        asOf: (json['asOf'] ?? '').toString(),
        kpis: [for (final k in (json['kpis'] as List<dynamic>? ?? const [])) Kpi.fromJson(k as Map<String, dynamic>)],
        charts: [for (final c in (json['charts'] as List<dynamic>? ?? const [])) DashboardChart.fromJson(c as Map<String, dynamic>)],
        alerts: [for (final a in (json['alerts'] as List<dynamic>? ?? const [])) DashboardAlert.fromJson(a as Map<String, dynamic>)],
      );
}
