import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/session.dart';
import '../common/paged_list_view.dart';

const hrPath = '/api/v1/m/hr';

/// Colour for a status pill of the HR documents (leave, payroll, reviews, employees).
Color hrStatusColor(String status) => switch (status) {
      'approved' || 'paid' || 'active' || 'acknowledged' || 'open' => Colors.green.shade700,
      'submitted' || 'pendingApproval' || 'posted' || 'onLeave' => Colors.blue.shade700,
      'rejected' || 'cancelled' || 'terminated' => Colors.red.shade700,
      'draft' || 'closed' => Colors.grey.shade700,
      _ => Colors.blueGrey,
    };

Widget hrStatusChip(String status, String label) => StatusChip(label, color: hrStatusColor(status));

typedef NameLookup = ({String path, String labelKey});

/// id -> label for a list endpoint (employee names, leave type names...), loaded once and shared by the tiles that need them.
final hrNamesProvider = FutureProvider.family<Map<String, String>, NameLookup>((ref, q) async {
  final data = await ref.watch(apiProvider).get(q.path, query: {'page': 1, 'pageSize': 200});
  final items = data is List ? data : ((data as Map<String, dynamic>)['items'] as List<dynamic>? ?? const []);
  return {for (final i in items) (i as Map<String, dynamic>)['id'].toString(): (i[q.labelKey] ?? '').toString()};
});

const NameLookup employeeNames = (path: '$hrPath/employees', labelKey: 'fullName');
const NameLookup leaveTypeNames = (path: '$hrPath/leave-types', labelKey: 'name');
const NameLookup cycleNames = (path: '$hrPath/review-cycles', labelKey: 'name');

/// A text that shows the name looked up by id (empty until loaded or when unknown).
class NameText extends ConsumerWidget {
  const NameText(this.lookup, this.id, {super.key, this.style, this.prefix = ''});

  final NameLookup lookup;
  final Object? id;
  final TextStyle? style;
  final String prefix;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final names = ref.watch(hrNamesProvider(lookup)).asData?.value ?? const {};
    return Text('$prefix${names[id?.toString()] ?? ''}', style: style, maxLines: 1, overflow: TextOverflow.ellipsis);
  }
}

String monthName(int m) => const ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'][m - 1];

String days(Object? v) {
  final n = v is num ? v : num.tryParse('$v') ?? 0;
  return n == n.truncate() ? '${n.toInt()}' : n.toStringAsFixed(1);
}
