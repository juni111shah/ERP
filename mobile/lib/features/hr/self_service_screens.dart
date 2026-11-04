import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../../core/format.dart';
import '../../core/session.dart';
import '../../kit/data_version.dart';
import '../../kit/detail_page.dart';
import '../../kit/field.dart';
import '../../kit/form_page.dart';
import '../../kit/pdf_share.dart';
import '../../kit/skeleton.dart';
import '../../kit/widgets.dart';
import 'hr_common.dart';
import 'leave_screens.dart';
import 'package:erp_mobile/theme/tokens.dart';

List<Json> _items(dynamic data) => [for (final r in (data is List ? data : ((data as Map?)?['items'] as List<dynamic>? ?? const []))) r as Json];

/// The signed-in person's own employee record; null when the account is not linked to one.
final myEmployeeProvider = FutureProvider.autoDispose<Json?>((ref) async {
  ref.watch(dataVersionProvider);
  try {
    return await ref.read(apiProvider).get('$hrPath/me') as Json;
  } on ApiException {
    return null;
  }
});

final myBalancesProvider = FutureProvider.autoDispose<List<Json>>((ref) async {
  ref.watch(dataVersionProvider);
  return _items(await ref.read(apiProvider).get('$hrPath/me/balances'));
});

final myLeaveProvider = FutureProvider.autoDispose<List<Json>>((ref) async {
  ref.watch(dataVersionProvider);
  return _items(await ref.read(apiProvider).get('$hrPath/me/leave', query: {'page': 1, 'pageSize': 50}));
});

final myPayslipsProvider = FutureProvider.autoDispose<List<Json>>((ref) async {
  ref.watch(dataVersionProvider);
  try {
    return _items(await ref.read(apiProvider).get('$hrPath/me/payslips'));
  } on ApiException {
    return const [];
  }
});

/// Leave waiting for the signed-in line manager. Empty (and the tab hidden) when the user manages nobody.
final teamLeaveProvider = FutureProvider.autoDispose<List<Json>?>((ref) async {
  ref.watch(dataVersionProvider);
  try {
    return _items(await ref.read(apiProvider).get('$hrPath/me/team/leave', query: {'page': 1, 'pageSize': 50, 'status': 'submitted'}));
  } on ApiException {
    return null;
  }
});

/// Request leave for yourself. The leave types come from your balances, so no extra permission is needed.
FormSpec myLeaveForm(List<Json> balances) => FormSpec(
      title: 'Request leave',
      intro: 'Weekends and public holidays are not counted. Your line manager decides.',
      submitLabel: 'Send request',
      fields: [
        Field.select('leaveTypeId', 'Leave type', options: [for (final b in balances) Option('${b['leaveTypeId']}', '${b['leaveTypeName']} (${days(b['remaining'])} left)')], required: true),
        Field.date('fromDate', 'From', required: true, initial: 'today'),
        Field.date('toDate', 'To', required: true, initial: 'today', validator: (v, all) {
          final from = all['fromDate']?.toString() ?? '';
          return v != null && from.isNotEmpty && v.toString().compareTo(from) < 0 ? 'The end date is before the start date' : null;
        }),
        Field.multiline('reason', 'Reason', maxLength: 500),
      ],
      submit: (ref, body, raw) => ref.read(apiProvider).post('$hrPath/me/leave', body: {...body, 'submit': true}),
    );

/// My HR: profile, leave balances, my leave requests, payslips, and (for line managers) the team's leave to decide.
class MyHrScreen extends ConsumerWidget {
  const MyHrScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final me = ref.watch(myEmployeeProvider);
    final team = ref.watch(teamLeaveProvider).asData?.value;
    return me.when(
      loading: () => Scaffold(appBar: AppBar(title: const Text('My HR')), body: const ErpSkeleton.list()),
      error: (e, _) => Scaffold(appBar: AppBar(title: const Text('My HR')), body: Center(child: Text('$e'))),
      data: (profile) {
        if (profile == null) {
          return Scaffold(
            appBar: AppBar(title: const Text('My HR')),
            body: const Center(child: Padding(padding: EdgeInsets.all(ErpSpace.s4), child: Text('Your sign-in is not linked to an employee record yet. Ask HR to link it on your employee profile.', textAlign: TextAlign.center))),
          );
        }
        final tabs = <(String, Widget)>[
          ('Balances', const _BalancesTab()),
          ('My leave', const _MyLeaveTab()),
          ('Payslips', const _PayslipsTab()),
          if (team != null) ('Team (${team.length})', _TeamTab(team)),
        ];
        return DefaultTabController(
          length: tabs.length,
          child: Scaffold(
            appBar: AppBar(
              title: const Text('My HR'),
              bottom: TabBar(isScrollable: true, tabs: [for (final t in tabs) Tab(text: t.$1)]),
            ),
            body: Column(children: [
              Padding(
                padding: const EdgeInsets.all(ErpSpace.s3),
                child: DetailHeader(title: '${profile['fullName']}', subtitle: '${profile['number']} · ${profile['designation']}${profile['managerName'] == null ? '' : ' · Manager: ${profile['managerName']}'}'),
              ),
              Expanded(child: TabBarView(children: [for (final t in tabs) t.$2])),
            ]),
          ),
        );
      },
    );
  }
}

class _BalancesTab extends ConsumerWidget {
  const _BalancesTab();

  @override
  Widget build(BuildContext context, WidgetRef ref) => ref.watch(myBalancesProvider).when(
        loading: () => const ErpSkeleton.list(),
        error: (e, _) => Center(child: Text('$e')),
        data: (rows) => rows.isEmpty
            ? const Center(child: Text('No leave balances yet.'))
            : ListView(padding: const EdgeInsets.all(ErpSpace.s3), children: [
                for (final b in rows)
                  Card(
                    child: Padding(
                      padding: const EdgeInsets.all(ErpSpace.s3),
                      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                        Text('${b['leaveTypeName']}', style: Theme.of(context).textTheme.titleSmall),
                        const SizedBox(height: ErpSpace.s1),
                        InfoRow('Entitled', days(b['entitled'])),
                        InfoRow('Carried over', days(b['carried'])),
                        InfoRow('Taken', days(b['taken'])),
                        InfoRow('Waiting for approval', days(b['pending'])),
                        InfoRow('Remaining', days(b['remaining']), bold: true),
                      ]),
                    ),
                  ),
              ]),
      );
}

class _MyLeaveTab extends ConsumerWidget {
  const _MyLeaveTab();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final rows = ref.watch(myLeaveProvider);
    return Scaffold(
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () async {
          final balances = await ref.read(myBalancesProvider.future);
          if (context.mounted) await openForm<Object?>(context, myLeaveForm(balances));
        },
        icon: const Icon(Icons.add),
        label: const Text('Request leave'),
      ),
      body: rows.when(
        loading: () => const ErpSkeleton.list(),
        error: (e, _) => Center(child: Text('$e')),
        data: (items) => items.isEmpty
            ? const Center(child: Text('You have not asked for leave yet.'))
            : ListView.separated(
                padding: const EdgeInsets.only(bottom: 88),
                itemCount: items.length,
                separatorBuilder: (_, _) => const Divider(height: 1),
                itemBuilder: (context, i) {
                  final r = items[i];
                  final status = '${r['status']}';
                  return ListTile(
                    title: Text('${formatDate(r['fromDate'])} - ${formatDate(r['toDate'])}'),
                    subtitle: NameText(leaveTypeNames, r['leaveTypeId'], prefix: '${days(r['days'])} days · '),
                    trailing: hrStatusChip(status, leaveStatusLabel(status)),
                    onLongPress: const {'draft', 'submitted', 'approved'}.contains(status) ? () => _cancel(context, ref, r) : null,
                    onTap: () => showModalBottomSheet<void>(context: context, builder: (_) => _LeaveSheet(r)),
                  );
                },
              ),
      ),
    );
  }

  Future<void> _cancel(BuildContext context, WidgetRef ref, Json r) async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (c) => AlertDialog(
        title: const Text('Cancel this leave request?'),
        actions: [TextButton(onPressed: () => Navigator.pop(c, false), child: const Text('Keep')), FilledButton(onPressed: () => Navigator.pop(c, true), child: const Text('Cancel request'))],
      ),
    );
    if (ok != true) return;
    try {
      await ref.read(apiProvider).post('$hrPath/me/leave/${r['id']}/cancel');
      ref.read(dataVersionProvider.notifier).bump();
    } on ApiException catch (e) {
      if (context.mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.message)));
    }
  }
}

class _LeaveSheet extends ConsumerWidget {
  const _LeaveSheet(this.r);

  final Json r;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final status = '${r['status']}';
    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.all(ErpSpace.s4),
        child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text('${formatDate(r['fromDate'])} - ${formatDate(r['toDate'])}', style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: ErpSpace.s2),
          hrStatusChip(status, leaveStatusLabel(status)),
          const SizedBox(height: ErpSpace.s2),
          InfoRow('Working days', days(r['days'])),
          InfoRow('Reason', text(r['reason'])),
          const SizedBox(height: ErpSpace.s3),
          Row(children: [
            if (status == 'draft')
              FilledButton(
                onPressed: () async {
                  Navigator.pop(context);
                  await ref.read(apiProvider).post('$hrPath/me/leave/${r['id']}/submit');
                  ref.read(dataVersionProvider.notifier).bump();
                },
                child: const Text('Send for approval'),
              ),
          ]),
        ]),
      ),
    );
  }
}

class _PayslipsTab extends ConsumerWidget {
  const _PayslipsTab();

  @override
  Widget build(BuildContext context, WidgetRef ref) => ref.watch(myPayslipsProvider).when(
        loading: () => const ErpSkeleton.list(),
        error: (e, _) => Center(child: Text('$e')),
        data: (rows) => rows.isEmpty
            ? const Center(child: Text('No payslips yet.'))
            : ListView(padding: const EdgeInsets.all(ErpSpace.s3), children: [
                for (final p in rows)
                  Card(
                    child: ExpansionTile(
                      title: Text('${monthName((p['month'] as num).toInt())} ${p['year']}'),
                      subtitle: Text('${p['runNumber']} · ${humanize('${p['status']}')}'),
                      trailing: Text(formatMoney((p['netPay'] as num?) ?? 0), style: const TextStyle(fontWeight: FontWeight.w700)),
                      childrenPadding: const EdgeInsets.fromLTRB(ErpSpace.s4, 0, ErpSpace.s4, ErpSpace.s3),
                      children: [
                        InfoRow('Basic', formatMoney((p['basic'] as num?) ?? 0)),
                        InfoRow('Allowances', formatMoney((p['allowances'] as num?) ?? 0)),
                        InfoRow('Overtime', formatMoney((p['overtime'] as num?) ?? 0)),
                        InfoRow('Absence (${days(p['absentDays'])} d)', formatMoney(-((p['absence'] as num?) ?? 0).abs())),
                        if (((p['adjustment'] as num?) ?? 0) != 0) InfoRow('Adjustment', formatMoney((p['adjustment'] as num))),
                        InfoRow('Deductions', formatMoney((p['deductions'] as num?) ?? 0)),
                        InfoRow('Take-home pay', formatMoney((p['netPay'] as num?) ?? 0), bold: true),
                      ],
                    ),
                  ),
              ]),
      );
}

class _TeamTab extends ConsumerWidget {
  const _TeamTab(this.rows);

  final List<Json> rows;

  @override
  Widget build(BuildContext context, WidgetRef ref) => rows.isEmpty
      ? const Center(child: Text('Nothing waiting for your decision.'))
      : ListView.separated(
          itemCount: rows.length,
          separatorBuilder: (_, _) => const Divider(height: 1),
          itemBuilder: (context, i) {
            final r = rows[i];
            return ListTile(
              title: Text('${r['employeeName']}'),
              subtitle: Text('${formatDate(r['fromDate'])} - ${formatDate(r['toDate'])} · ${days(r['days'])} d${(r['reason'] ?? '').toString().isEmpty ? '' : '\n${r['reason']}'}'),
              isThreeLine: (r['reason'] ?? '').toString().isNotEmpty,
              trailing: Row(mainAxisSize: MainAxisSize.min, children: [
                IconButton(tooltip: 'Reject', icon: const Icon(Icons.close, color: Colors.red), onPressed: () => _decide(context, ref, r, 'reject')),
                IconButton(tooltip: 'Approve', icon: const Icon(Icons.check, color: Colors.green), onPressed: () => _decide(context, ref, r, 'approve')),
              ]),
            );
          },
        );

  Future<void> _decide(BuildContext context, WidgetRef ref, Json r, String what) async {
    try {
      await ref.read(apiProvider).post('$hrPath/leave/${r['id']}/$what', body: const {});
      ref.read(dataVersionProvider.notifier).bump();
      if (context.mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(what == 'approve' ? 'Approved' : 'Rejected')));
    } on ApiException catch (e) {
      if (context.mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.message)));
    }
  }
}

/// Share the payslip of a payroll line as PDF (HR staff, from a payroll run).
Widget payslipPdfButton(Json run, Json line) => PrintShareButton(
      path: '$hrPath/payroll/${run['id']}/payslips/${line['id']}/payslip.pdf',
      fallbackFileName: 'Payslip-${line['employeeNumber'] ?? line['id']}.pdf',
    );
