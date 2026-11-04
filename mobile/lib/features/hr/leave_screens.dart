import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/format.dart';
import '../../core/me.dart';
import '../../core/session.dart';
import '../../kit/detail_page.dart';
import '../../kit/field.dart';
import '../../kit/form_page.dart';
import '../../kit/list_screen.dart';
import '../../kit/widgets.dart';
import 'hr_common.dart';
import 'package:erp_mobile/theme/tokens.dart';
import '../../kit/skeleton.dart';

const _leavePath = '$hrPath/leave';

const leaveStatuses = [Option('draft', 'Draft'), Option('submitted', 'Waiting'), Option('approved', 'Approved'), Option('rejected', 'Rejected'), Option('cancelled', 'Cancelled')];

String leaveStatusLabel(String s) => s == 'submitted' ? 'Waiting for approval' : humanize(s);

/// Request leave (existing == null) or edit a draft request. "Send now" submits it straight to the approver.
FormSpec leaveForm(WidgetRef ref, {Json? existing}) {
  final editing = existing != null;
  return FormSpec(
    title: editing ? 'Edit leave request' : 'Request leave',
    initial: {
      if (editing) ...{
        'employeeId': existing['employeeId'],
        '__label_employeeId': existing['employeeName'],
        'leaveTypeId': existing['leaveTypeId'],
        '__label_leaveTypeId': existing['leaveTypeName'],
        'fromDate': existing['fromDate'],
        'toDate': existing['toDate'],
        'reason': existing['reason'],
        'startHalfDay': existing['startHalfDay'],
        'endHalfDay': existing['endHalfDay'],
      },
    },
    fields: [
      Field.lookup('employeeId', 'Employee', path: '$hrPath/employees', labelKey: 'fullName', subtitleKey: 'number', required: true),
      Field.lookup('leaveTypeId', 'Leave type', path: '$hrPath/leave-types', subtitleKey: 'code', required: true),
      Field.date('fromDate', 'From', required: true, initial: 'today'),
      Field.date('toDate', 'To', required: true, initial: 'today', validator: (v, all) {
        final from = all['fromDate']?.toString() ?? '';
        return v != null && from.isNotEmpty && v.toString().compareTo(from) < 0 ? 'The end date is before the start date' : null;
      }),
      Field.toggle('startHalfDay', 'First day is a half day'),
      Field.toggle('endHalfDay', 'Last day is a half day'),
      Field.multiline('reason', 'Reason', maxLength: 500),
      if (!editing) Field.toggle('submit', 'Send for approval now', initial: true),
    ],
    intro: 'Weekends and public holidays are not counted. The server checks the balance.',
    submit: (ref, body, raw) {
      final api = ref.read(apiProvider);
      return editing ? api.put('$_leavePath/${existing['id']}', body: body) : api.post(_leavePath, body: body);
    },
  );
}

Widget leaveTile(BuildContext context, Json r) {
  final status = (r['status'] ?? '').toString();
  return ListTile(
    onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => LeaveDetailScreen(id: r['id'].toString()))),
    title: Text('${r['employeeName']}', maxLines: 1, overflow: TextOverflow.ellipsis),
    subtitle: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
      NameText(leaveTypeNames, r['leaveTypeId']),
      Text('${formatDate(r['fromDate'])} - ${formatDate(r['toDate'])}'),
    ]),
    isThreeLine: true,
    trailing: Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.end, children: [
      Text('${days(r['days'])} d', style: const TextStyle(fontWeight: FontWeight.w600)),
      const SizedBox(height: ErpSpace.s1),
      hrStatusChip(status, leaveStatusLabel(status)),
    ]),
  );
}

class LeaveScreen extends StatelessWidget {
  const LeaveScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Leave requests',
        path: _leavePath,
        searchHint: 'Search by employee',
        emptyText: 'No leave requests.',
        itemBuilder: leaveTile,
        filters: leaveStatuses,
        create: (ref) => leaveForm(ref),
        createPermission: 'hr.leave.request',
      );
}

class LeaveDetailScreen extends StatelessWidget {
  const LeaveDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) => Consumer(builder: (context, ref, _) {
        final path = '$_leavePath/$id';
        return DetailPage(
          path: path,
          titleOf: (d) => 'Leave: ${d['employeeName']}',
          canEdit: (d) => d['status'] == 'draft' && ref.read(meOrEmptyProvider).can('hr.leave.request'),
          edit: (d) => leaveForm(ref, existing: d),
          body: (context, d) => leaveBody(context, d),
          actions: leaveActions(path),
        );
      });
}

List<ActionSpec> leaveActions(String path) => [
      ActionSpec(
        label: 'Approve',
        icon: Icons.check_circle_outline,
        permission: 'hr.leave.approve',
        visible: (d) => d['status'] == 'submitted',
        confirm: 'Approve this leave request?',
        successMessage: 'Approved',
        run: (ref, d, _) => ref.read(apiProvider).post('$path/approve', body: const {}),
      ),
      ActionSpec(
        label: 'Reject',
        icon: Icons.cancel_outlined,
        permission: 'hr.leave.approve',
        destructive: true,
        visible: (d) => d['status'] == 'submitted',
        reasonLabel: 'Why is this request rejected?',
        successMessage: 'Rejected',
        run: (ref, d, reason) => ref.read(apiProvider).post('$path/reject', body: {'comment': reason}),
      ),
      ActionSpec(
        label: 'Submit',
        icon: Icons.send_outlined,
        permission: 'hr.leave.request',
        visible: (d) => d['status'] == 'draft',
        confirm: 'Send this request to the approver?',
        successMessage: 'Sent for approval',
        run: (ref, d, _) => ref.read(apiProvider).post('$path/submit'),
      ),
      ActionSpec(
        label: 'Cancel request',
        icon: Icons.block,
        permission: 'hr.leave.request',
        destructive: true,
        visible: (d) => const {'draft', 'submitted', 'approved'}.contains(d['status']),
        confirm: 'Cancel this leave request?',
        successMessage: 'Cancelled',
        run: (ref, d, _) => ref.read(apiProvider).post('$path/cancel'),
      ),
    ];

Widget leaveBody(BuildContext context, Json d) => DetailBody(children: [
      DetailHeader(title: d['employeeName']?.toString() ?? '', subtitle: d['leaveTypeName']?.toString()),
      Align(alignment: Alignment.centerLeft, child: Padding(padding: const EdgeInsets.only(bottom: ErpSpace.s3), child: hrStatusChip('${d['status']}', leaveStatusLabel('${d['status']}')))),
      InfoCard(rows: [
        InfoRow('From', formatDate(d['fromDate'])),
        InfoRow('To', formatDate(d['toDate'])),
        InfoRow('Working days', days(d['days']), bold: true),
        if (d['startHalfDay'] == true || d['endHalfDay'] == true) InfoRow('Half days', [if (d['startHalfDay'] == true) 'first day', if (d['endHalfDay'] == true) 'last day'].join(' and ')),
        InfoRow('Paid leave', d['isPaid'] == true ? 'Yes' : 'No'),
        InfoRow('Reason', text(d['reason'])),
        InfoRow('Decision note', text(d['decisionComment'])),
      ]),
    ]);

/// Leave balances of the year, grouped by employee.
class LeaveBalancesScreen extends ConsumerStatefulWidget {
  const LeaveBalancesScreen({super.key});

  @override
  ConsumerState<LeaveBalancesScreen> createState() => _LeaveBalancesState();
}

class _LeaveBalancesState extends ConsumerState<LeaveBalancesScreen> {
  int _year = DateTime.now().year;
  late Future<List<Json>> _future = _load();

  Future<List<Json>> _load() async {
    final data = await ref.read(apiProvider).get('$_leavePath/balances', query: {'year': _year});
    return [for (final r in (data is List ? data : ((data as Map)['items'] as List<dynamic>? ?? const []))) r as Json];
  }

  void _go(int delta) => setState(() {
        _year += delta;
        _future = _load();
      });

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Leave balances')),
      body: Column(children: [
        Row(mainAxisAlignment: MainAxisAlignment.center, children: [
          IconButton(tooltip: 'Previous year', onPressed: () => _go(-1), icon: const Icon(Icons.chevron_left)),
          Text('$_year', style: Theme.of(context).textTheme.titleMedium),
          IconButton(tooltip: 'Next year', onPressed: () => _go(1), icon: const Icon(Icons.chevron_right)),
        ]),
        Expanded(
          child: FutureBuilder<List<Json>>(
            future: _future,
            builder: (context, snap) {
              if (snap.connectionState != ConnectionState.done) return const ErpSkeleton.list();
              if (snap.hasError) return Center(child: Padding(padding: const EdgeInsets.all(ErpSpace.s6), child: Text('${snap.error}')));
              final rows = snap.data!;
              if (rows.isEmpty) return const Center(child: Text('No balances for this year.'));
              final byEmployee = <String, List<Json>>{};
              for (final r in rows) {
                byEmployee.putIfAbsent('${r['employeeName']} (${r['employeeNumber']})', () => []).add(r);
              }
              return ListView(padding: const EdgeInsets.all(ErpSpace.s3), children: [
                for (final e in byEmployee.entries)
                  Card(
                    child: Padding(
                      padding: const EdgeInsets.all(ErpSpace.s3),
                      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                        Text(e.key, style: Theme.of(context).textTheme.titleSmall),
                        for (final b in e.value) balanceRow(b),
                      ]),
                    ),
                  ),
              ]);
            },
          ),
        ),
      ]),
    );
  }
}

Widget balanceRow(Json b) {
  final entitled = ((b['entitled'] as num?) ?? 0) + ((b['carried'] as num?) ?? 0);
  final taken = (b['taken'] as num?) ?? 0;
  return Padding(
    padding: const EdgeInsets.only(top: ErpSpace.s2),
    child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
      Row(children: [
        Expanded(child: Text('${b['leaveTypeName']}')),
        Text('${days(b['remaining'])} left', style: const TextStyle(fontWeight: FontWeight.w700)),
      ]),
      const SizedBox(height: ErpSpace.s1),
      LinearProgressIndicator(value: entitled <= 0 ? 0 : (taken / entitled).clamp(0, 1).toDouble()),
      Text('Taken ${days(taken)} of ${days(entitled)} · pending ${days(b['pending'])}', style: const TextStyle(fontSize: ErpType.caption)),
    ]),
  );
}
