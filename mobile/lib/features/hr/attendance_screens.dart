import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../../core/me.dart';
import '../../core/session.dart';
import '../../kit/data_version.dart';
import '../../kit/detail_page.dart';
import '../../kit/field.dart';
import '../../kit/form_page.dart';
import '../common/paged_list_view.dart';
import 'hr_common.dart';
import 'package:erp_mobile/theme/tokens.dart';
import '../../kit/skeleton.dart';

const _attendancePath = '$hrPath/attendance';

/// The month's exceptions of one employee. Year and month are fixed by the screen; the rest is typed in.
FormSpec attendanceForm(Json row, int year, int month) {
  String s(String k) => '${row[k] ?? 0}';
  return FormSpec(
    title: '${row['employeeName']}',
    intro: '${monthName(month)} $year. Approved leave is added by payroll on its own.',
    initial: {
      'workingDays': s('workingDays'),
      'daysPresent': s('daysPresent'),
      'unpaidAbsentDays': s('unpaidAbsentDays'),
      'paidLeaveDays': s('paidLeaveDays'),
      'overtimeHours': s('overtimeHours'),
    },
    fields: [
      Field.integer('workingDays', 'Working days', required: true, min: 1, max: 31),
      Field.decimal('daysPresent', 'Days present', required: true, min: 0, decimals: 0, validator: (v, all) {
        final p = toNum(v), w = toNum(all['workingDays']);
        return p != null && w != null && p > w ? 'Days present cannot exceed the working days' : null;
      }),
      Field.decimal('unpaidAbsentDays', 'Unpaid absence (days)', required: true, min: 0, decimals: 0, validator: (v, all) {
        final a = toNum(v), l = toNum(all['paidLeaveDays']), w = toNum(all['workingDays']);
        return a != null && w != null && a + (l ?? 0) > w ? 'Absence and leave cannot exceed the working days' : null;
      }),
      Field.integer('paidLeaveDays', 'Paid leave (days)', required: true, min: 0),
      Field.decimal('overtimeHours', 'Overtime hours', required: true, min: 0, decimals: 2),
    ],
    submit: (ref, body, raw) => ref.read(apiProvider).put(_attendancePath, body: {...body, 'employeeId': row['employeeId'], 'year': year, 'month': month}),
  );
}

/// Attendance of every active employee for one month; tap a row to record exceptions.
class AttendanceScreen extends ConsumerStatefulWidget {
  const AttendanceScreen({super.key});

  @override
  ConsumerState<AttendanceScreen> createState() => _AttendanceState();
}

class _AttendanceState extends ConsumerState<AttendanceScreen> {
  late int _year = DateTime.now().year;
  late int _month = DateTime.now().month;
  List<Json> _rows = [];
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = _rows.isEmpty;
      _error = null;
    });
    try {
      final data = await ref.read(apiProvider).get(_attendancePath, query: {'year': _year, 'month': _month});
      final list = data is List ? data : ((data as Map)['items'] as List<dynamic>? ?? const []);
      if (mounted) setState(() => _rows = [for (final r in list) r as Json]);
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  void _shift(int delta) {
    final d = DateTime(_year, _month + delta);
    _year = d.year;
    _month = d.month;
    _rows = [];
    _load();
  }

  @override
  Widget build(BuildContext context) {
    ref.listen(dataVersionProvider, (_, _) => _load());
    final canManage = ref.watch(meOrEmptyProvider).can('hr.attendance.manage');
    return Scaffold(
      appBar: AppBar(title: const Text('Attendance')),
      body: Column(children: [
        Row(mainAxisAlignment: MainAxisAlignment.center, children: [
          IconButton(tooltip: 'Previous month', onPressed: () => _shift(-1), icon: const Icon(Icons.chevron_left)),
          Text('${monthName(_month)} $_year', style: Theme.of(context).textTheme.titleMedium),
          IconButton(tooltip: 'Next month', onPressed: () => _shift(1), icon: const Icon(Icons.chevron_right)),
        ]),
        Expanded(
          child: _loading
              ? const ErpSkeleton.list()
              : _error != null
                  ? ErrorView(message: _error!, onRetry: _load)
                  : _rows.isEmpty
                      ? const Center(child: Text('No active employees.'))
                      : RefreshIndicator(
                          onRefresh: _load,
                          child: ListView.separated(
                            physics: const AlwaysScrollableScrollPhysics(),
                            itemCount: _rows.length,
                            separatorBuilder: (_, _) => const Divider(height: 1),
                            itemBuilder: (context, i) => attendanceTile(context, _rows[i], canManage ? () async {
                              await openForm<Object?>(context, attendanceForm(_rows[i], _year, _month));
                            } : null),
                          ),
                        ),
        ),
      ]),
    );
  }
}

Widget attendanceTile(BuildContext context, Json r, VoidCallback? onTap) {
  final recorded = r['recorded'] == true;
  final extras = <String>[
    if (((r['unpaidAbsentDays'] as num?) ?? 0) > 0) '${days(r['unpaidAbsentDays'])} unpaid absent',
    if (((r['leavePaidDays'] as num?) ?? 0) + ((r['paidLeaveDays'] as num?) ?? 0) > 0) '${days(((r['leavePaidDays'] as num?) ?? 0) + ((r['paidLeaveDays'] as num?) ?? 0))} paid leave',
    if (((r['leaveUnpaidDays'] as num?) ?? 0) > 0) '${days(r['leaveUnpaidDays'])} unpaid leave',
    if (((r['overtimeHours'] as num?) ?? 0) > 0) '${days(r['overtimeHours'])} h overtime',
  ];
  return ListTile(
    onTap: onTap,
    title: Text('${r['employeeName']}', maxLines: 1, overflow: TextOverflow.ellipsis),
    subtitle: Text(extras.isEmpty ? '${r['designation'] ?? ''}' : extras.join(' · ')),
    trailing: Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.end, children: [
      Text('${days(r['daysPresent'])} / ${days(r['workingDays'])} days', style: const TextStyle(fontWeight: FontWeight.w600)),
      const SizedBox(height: ErpSpace.s1),
      StatusChip(recorded ? 'Recorded' : 'Full attendance', color: recorded ? Colors.blue.shade700 : Colors.grey.shade700),
    ]),
  );
}
