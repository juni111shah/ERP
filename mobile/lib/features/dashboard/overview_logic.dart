import '../../core/me.dart';
import '../../core/models.dart';
import '../../kit/bar_chart.dart';
import '../../kit/registry.dart';
import '../../theme/tokens.dart';
import '../more/more_page.dart';

/// Category key used by the Overview chips for KPIs the server sent without a link.
const kOverviewGeneralCategory = 'general';

/// The category of a KPI: the first segment of its link ("/finance/invoices" -> "finance",
/// "/real-estate/units" -> "real-estate"). KPIs without a link go to [kOverviewGeneralCategory].
String kpiCategory(Kpi kpi) {
  final link = kpi.link;
  if (link == null || link.isEmpty) return kOverviewGeneralCategory;
  final path = Uri.tryParse(link)?.path ?? link;
  final first = path.split('/').firstWhere((s) => s.isNotEmpty, orElse: () => '');
  return first.isEmpty ? kOverviewGeneralCategory : first.toLowerCase();
}

/// "real-estate" -> "Real estate", "hr" -> "HR", "crm" -> "CRM".
String categoryLabel(String key) {
  const acronyms = {'hr', 'crm', 'pos'};
  if (acronyms.contains(key)) return key.toUpperCase();
  final words = key.replaceAll(RegExp(r'[-_.]+'), ' ').trim();
  if (words.isEmpty) return key;
  return words[0].toUpperCase() + words.substring(1);
}

/// Categories in first-seen order (the server's KPI order), each with its KPI count.
List<(String, int)> kpiCategories(List<Kpi> kpis) {
  final counts = <String, int>{};
  for (final k in kpis) {
    counts.update(kpiCategory(k), (c) => c + 1, ifAbsent: () => 1);
  }
  return [for (final e in counts.entries) (e.key, e.value)];
}

/// A stable stripe per KPI key (FNV-1a hash), so a card keeps its colour whatever its position or filter.
ErpStripe stripeForKey(String key) {
  var h = 0x811c9dc5;
  for (final unit in key.codeUnits) {
    h ^= unit;
    h = (h * 0x01000193) & 0xffffffff;
  }
  return ErpStripe.all[h % ErpStripe.all.length];
}

/// Compact figure for a card ("4.82M"); percentages and small figures keep the full format.
String compactKpi(double value, String format, String full) {
  if (format == 'percent' || value.abs() < 1000) return full;
  return erpCompactNumber(value);
}

/// The in-app screen for a server link (a web route), or null when the app has none.
///
/// Uses the user's server menu first (exact route match), then the menu-id convention
/// "/real-estate/work-orders" -> "realestate.workorders", "/approvals" -> "core.approvals".
MobileScreen? screenForLink(String? link, {List<MenuGroup> menu = const []}) {
  if (link == null || link.isEmpty) return null;
  final uri = Uri.tryParse(link);
  final path = (uri?.path ?? link).replaceAll(RegExp(r'/+$'), '');
  if (path.isEmpty) return null;
  for (final g in menu) {
    for (final i in g.items) {
      final route = (Uri.tryParse(i.route)?.path ?? i.route).replaceAll(RegExp(r'/+$'), '');
      if (route == path) {
        final s = screenForMenuId(i.id);
        if (s != null) return s;
      }
    }
  }
  final raw = [for (final s in path.split('/')) if (s.isNotEmpty) s.toLowerCase()];
  final candidates = <String>{
    for (final segs in [raw, [for (final s in raw) s.replaceAll('-', '')]]) ...[
      if (segs.length == 1) 'core.${segs[0]}',
      if (segs.length >= 2) '${segs[0]}.${segs[1]}',
      if (segs.length >= 2) 'core.${segs[1]}',
    ],
  };
  for (final id in candidates) {
    final s = screenForMenuId(id);
    if (s != null) return s;
  }
  return null;
}

const _months = {'jan', 'feb', 'mar', 'apr', 'may', 'jun', 'jul', 'aug', 'sep', 'oct', 'nov', 'dec'};

/// Short x-axis label without blind character cuts:
/// "Nov 26" -> "Nov", "Oct–Dec 26" -> "Oct–Dec", "03 Nov" -> "03 Nov", "In progress" -> "In prog…".
String axisLabel(String label, {int max = 7}) {
  final s = label.trim();
  final words = s.split(RegExp(r'\s+'));
  // Month buckets ("MMM yy", "MMM–MMM yy"): drop the year, the full label shows on tap.
  if (words.length == 2 && RegExp(r"^'?\d{2,4}$").hasMatch(words[1])) {
    final head = words[0];
    final firstMonth = head.split(RegExp(r'[–-]')).first.toLowerCase();
    if (_months.contains(firstMonth)) return head;
  }
  if (s.length <= max) return s;
  return '${s.substring(0, max - 1).trimRight()}…';
}
