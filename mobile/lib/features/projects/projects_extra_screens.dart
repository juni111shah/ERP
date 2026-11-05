import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../../core/format.dart';
import '../../core/me.dart';
import '../../core/session.dart';
import '../../kit/data_version.dart';
import '../../kit/detail_page.dart';
import '../../kit/field.dart';
import '../../kit/form_page.dart';
import '../../kit/list_screen.dart';
import '../../kit/skeleton.dart';
import '../../kit/widgets.dart';
import '../common/paged_list_view.dart';
import 'package:erp_mobile/theme/tokens.dart';
import 'projects_screens.dart' show pBase, items, statusColor, timesheetForm;

Route<T> _route<T>(Widget w) => MaterialPageRoute<T>(builder: (_) => w);

const expenseCategories = [Option('Travel', 'Travel'), Option('Accommodation', 'Accommodation'), Option('Materials', 'Materials'), Option('Subcontractor', 'Subcontractor'), Option('Software', 'Software'), Option('Meals', 'Meals'), Option('Other', 'Other')];
const riskSeverities = [Option('low', 'Low'), Option('medium', 'Medium'), Option('high', 'High'), Option('critical', 'Critical')];
const riskStatuses = [Option('open', 'Open'), Option('mitigated', 'Mitigated'), Option('closed', 'Closed')];
const riskKinds = [Option('risk', 'Risk (might happen)'), Option('issue', 'Issue (has happened)')];

/// "2026-06-01" for today's date, or [d].
String isoDay(DateTime d) => '${d.year.toString().padLeft(4, '0')}-${d.month.toString().padLeft(2, '0')}-${d.day.toString().padLeft(2, '0')}';

/// Monday of the week that contains [d].
DateTime mondayOf(DateTime d) => DateTime(d.year, d.month, d.day).subtract(Duration(days: (d.weekday + 6) % 7));

/// Hours rounded to the nearest quarter, at least a quarter: what a timer adds to the day.
double timerHours(Duration elapsed) {
  final quarters = (elapsed.inSeconds / 900).round();
  return (quarters < 1 ? 1 : quarters) / 4;
}

// ---------------------------------------------------------------- shared list of plain arrays (the endpoints below are not paged)

class ArrayScreen extends ConsumerWidget {
  const ArrayScreen({super.key, required this.title, required this.path, required this.tile, this.create, this.createPermission, this.emptyText = 'Nothing here yet.', this.query});
  final String title;
  final String path;
  final Map<String, dynamic>? query;
  final Widget Function(BuildContext, WidgetRef, Json) tile;
  final FormSpec Function(WidgetRef ref)? create;
  final String? createPermission;
  final String emptyText;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    ref.watch(dataVersionProvider);
    final can = createPermission == null || ref.watch(meOrEmptyProvider).can(createPermission!);
    return Scaffold(
      appBar: AppBar(title: Text(title)),
      floatingActionButton: create != null && can ? FloatingActionButton(tooltip: 'New', onPressed: () => openForm<Object?>(context, create!(ref)), child: const Icon(Icons.add)) : null,
      body: FutureBuilder<dynamic>(
        future: ref.read(apiProvider).get(path, query: query),
        builder: (context, snap) {
          if (snap.connectionState != ConnectionState.done) return const ErpSkeleton.list();
          if (snap.hasError) return Center(child: Text('${snap.error}'));
          final rows = items(snap.data);
          if (rows.isEmpty) return Center(child: Text(emptyText));
          return ListView.separated(itemCount: rows.length, separatorBuilder: (_, _) => const Divider(height: 1), itemBuilder: (context, i) => tile(context, ref, rows[i]));
        },
      ),
    );
  }
}

/// A bottom sheet of actions for one row; each action runs, then the lists reload.
Future<void> rowActions(BuildContext context, WidgetRef ref, List<({String label, IconData icon, Future<void> Function() run})> actions) => showModalBottomSheet<void>(
      context: context,
      builder: (sheet) => SafeArea(
        child: Column(mainAxisSize: MainAxisSize.min, children: [
          for (final a in actions)
            ListTile(
              leading: Icon(a.icon),
              title: Text(a.label),
              onTap: () async {
                Navigator.pop(sheet);
                try {
                  await a.run();
                  ref.read(dataVersionProvider.notifier).bump();
                } on ApiException catch (e) {
                  if (context.mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.message)));
                }
              },
            ),
        ]),
      ),
    );

Future<String?> askReason(BuildContext context, String title) async {
  final c = TextEditingController();
  return showDialog<String>(
    context: context,
    builder: (ctx) => AlertDialog(
      title: Text(title),
      content: TextField(controller: c, autofocus: true, maxLength: 300, decoration: const InputDecoration(labelText: 'Reason')),
      actions: [TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('Cancel')), FilledButton(onPressed: () => Navigator.pop(ctx, c.text.trim().isEmpty ? null : c.text.trim()), child: const Text('Reject'))],
    ),
  );
}

// ---------------------------------------------------------------- expenses

FormSpec expenseForm(WidgetRef ref, {Json? existing, String? projectId, String? projectName}) {
  final editing = existing != null;
  return FormSpec(
    title: editing ? 'Edit expense' : 'New expense',
    initial: {
      if (editing) ...{
        'projectId': existing['projectId'],
        '__label_projectId': existing['projectName'],
        'date': existing['date'],
        'category': existing['category'],
        'description': existing['description'],
        'amount': existing['amount']?.toString(),
        'isBillable': existing['isBillable'],
        'markupPercent': existing['markupPercent']?.toString(),
      } else if (projectId != null) ...{'projectId': projectId, '__label_projectId': projectName},
    },
    fields: [
      Field.lookup('projectId', 'Project', path: '$pBase/projects', subtitleKey: 'number', required: true),
      Field.date('date', 'Date', required: true, initial: 'today'),
      Field.select('category', 'Category', options: expenseCategories, required: true, initial: 'Travel'),
      Field.text('description', 'What was it for', required: true, maxLength: 500),
      Field.money('amount', 'Amount (cost)', required: true, min: 0.01),
      Field.toggle('isBillable', 'Charge to the customer', initial: true),
      Field.decimal('markupPercent', 'Mark-up %', min: 0, max: 500, decimals: 2, initial: '0', visibleWhen: (v) => v['isBillable'] != false),
    ],
    submit: (ref, body, raw) {
      final api = ref.read(apiProvider);
      return editing ? api.put('$pBase/expenses/${existing['id']}', body: body) : api.post('$pBase/expenses', body: body);
    },
  );
}

Widget expenseTile(BuildContext context, Json e) => ListTile(
      onTap: () => Navigator.of(context).push(_route(ExpenseDetailScreen(id: '${e['id']}'))),
      title: Text('${e['category']} · ${e['description']}', maxLines: 1, overflow: TextOverflow.ellipsis),
      subtitle: Text('${formatDate(e['date'])}${(e['projectName'] ?? '').toString().isEmpty ? '' : ' · ${e['projectName']}'}'),
      trailing: Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.end, children: [
        Text(formatMoney((e['amount'] as num?) ?? 0), style: const TextStyle(fontWeight: FontWeight.w600)),
        StatusChip(e['writtenOff'] == true ? 'Written off' : e['isBilled'] == true ? 'Billed' : humanize('${e['status']}'), color: statusColor('${e['status']}' == 'approved' ? 'done' : '${e['status']}' == 'rejected' ? 'cancelled' : 'review')),
      ]),
    );

class ExpensesScreen extends StatelessWidget {
  const ExpensesScreen({super.key});
  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Expenses',
        path: '$pBase/expenses',
        searchHint: 'Search expenses',
        emptyText: 'No expenses yet.',
        itemBuilder: expenseTile,
        filterKey: 'status',
        filters: const [Option('Draft', 'Draft'), Option('Submitted', 'Submitted'), Option('Approved', 'Approved'), Option('Rejected', 'Rejected')],
        create: (ref) => expenseForm(ref),
        createPermission: 'projects.expenses.manage',
      );
}

class ExpenseDetailScreen extends StatelessWidget {
  const ExpenseDetailScreen({super.key, required this.id});
  final String id;

  @override
  Widget build(BuildContext context) => Consumer(builder: (context, ref, _) {
        bool editable(Json d) => d['isBilled'] != true && d['writtenOff'] != true && d['status'] != 'submitted';
        return DetailPage(
          path: '$pBase/expenses/$id',
          titleOf: (d) => '${d['category']}',
          canEdit: (d) => ref.read(meOrEmptyProvider).can('projects.expenses.manage') && editable(d),
          edit: (d) => expenseForm(ref, existing: d),
          body: (context, d) => DetailBody(children: [
            DetailHeader(title: '${d['description']}', subtitle: '${d['projectName']}'),
            InfoCard(rows: [
              InfoRow('Date', formatDate(d['date'])),
              InfoRow('Cost', formatMoney((d['amount'] as num?) ?? 0), bold: true),
              InfoRow('Charged to the customer', d['isBillable'] == true ? formatMoney((d['chargeAmount'] as num?) ?? 0) : 'Not charged'),
              InfoRow('Mark-up', '${d['markupPercent'] ?? 0}%'),
              InfoRow('Approval', humanize('${d['status']}')),
              InfoRow('Why rejected', text(d['rejectReason'])),
              InfoRow('Billing', d['writtenOff'] == true ? 'Written off' : d['isBilled'] == true ? 'Billed' : 'Not billed yet'),
            ]),
          ]),
          actions: [
            ActionSpec(
              label: 'Submit for approval',
              icon: Icons.send_outlined,
              permission: 'projects.expenses.manage',
              visible: (d) => const {'draft', 'rejected'}.contains(d['status']),
              successMessage: 'Submitted',
              run: (ref, d, _) => ref.read(apiProvider).post('$pBase/expenses/submit', body: {'ids': [id]}),
            ),
            ActionSpec(
              label: 'Approve',
              icon: Icons.check_circle_outline,
              permission: 'projects.expenses.approve',
              visible: (d) => d['status'] == 'submitted',
              successMessage: 'Approved',
              run: (ref, d, _) => ref.read(apiProvider).post('$pBase/expenses/decide', body: {'ids': [id], 'approve': true}),
            ),
            ActionSpec(
              label: 'Reject',
              icon: Icons.cancel_outlined,
              permission: 'projects.expenses.approve',
              destructive: true,
              reasonLabel: 'Why is it rejected?',
              visible: (d) => d['status'] == 'submitted',
              successMessage: 'Rejected',
              run: (ref, d, reason) => ref.read(apiProvider).post('$pBase/expenses/decide', body: {'ids': [id], 'approve': false, 'reason': reason}),
            ),
            ActionSpec(
              label: 'Delete',
              icon: Icons.delete_outline,
              permission: 'projects.expenses.manage',
              destructive: true,
              confirm: 'Delete this expense?',
              visible: editable,
              run: (ref, d, _) async {
                await ref.read(apiProvider).delete('$pBase/expenses/$id');
                if (context.mounted) Navigator.of(context).pop();
                return null;
              },
            ),
          ],
        );
      });
}

// ---------------------------------------------------------------- approvals

/// Submitted time and expenses, with Approve and Reject on every line.
class ApprovalsScreen extends ConsumerWidget {
  const ApprovalsScreen({super.key});

  Future<List<List<Json>>> _load(WidgetRef ref) async {
    final api = ref.read(apiProvider);
    final time = await api.get('$pBase/timesheets', query: {'filter': 'approvalStatus:eq:Submitted', 'pageSize': 100});
    final cost = await api.get('$pBase/expenses', query: {'filter': 'status:eq:Submitted', 'pageSize': 100});
    return [items(time), items(cost)];
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    ref.watch(dataVersionProvider);
    final canTime = ref.watch(meOrEmptyProvider).can('projects.timesheets.approve');
    final canCost = ref.watch(meOrEmptyProvider).can('projects.expenses.approve');
    Future<void> decide(String kind, String id, bool approve) async {
      String? reason;
      if (!approve) {
        reason = await askReason(context, 'Why is it rejected?');
        if (reason == null) return;
      }
      try {
        await ref.read(apiProvider).post('$pBase/$kind/decide', body: {'ids': [id], 'approve': approve, 'reason': reason});
        ref.read(dataVersionProvider.notifier).bump();
      } on ApiException catch (e) {
        if (context.mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.message)));
      }
    }

    Widget line(String kind, Json r, bool can, String title, String subtitle, num amount) => ListTile(
          title: Text(title, maxLines: 1, overflow: TextOverflow.ellipsis),
          subtitle: Text(subtitle),
          trailing: can
              ? Row(mainAxisSize: MainAxisSize.min, children: [
                  Text(formatMoney(amount)),
                  IconButton(tooltip: 'Approve', icon: const Icon(Icons.check_circle_outline), onPressed: () => decide(kind, '${r['id']}', true)),
                  IconButton(tooltip: 'Reject', icon: const Icon(Icons.cancel_outlined), onPressed: () => decide(kind, '${r['id']}', false)),
                ])
              : Text(formatMoney(amount)),
        );

    return Scaffold(
      appBar: AppBar(title: const Text('Approvals')),
      body: FutureBuilder<List<List<Json>>>(
        future: _load(ref),
        builder: (context, snap) {
          if (snap.connectionState != ConnectionState.done) return const ErpSkeleton.list();
          if (snap.hasError) return Center(child: Text('${snap.error}'));
          final time = snap.data![0], cost = snap.data![1];
          if (time.isEmpty && cost.isEmpty) return const Center(child: Text('Nothing is waiting for approval.'));
          return ListView(children: [
            if (time.isNotEmpty) const Padding(padding: EdgeInsets.all(ErpSpace.s3), child: Text('Time', style: TextStyle(fontWeight: FontWeight.w700))),
            for (final t in time) line('timesheets', t, canTime, '${t['hours']} h · ${t['workerName']}', '${formatDate(t['date'])} · ${t['description'] ?? ''}', (t['totalAmount'] as num?) ?? 0),
            if (cost.isNotEmpty) const Padding(padding: EdgeInsets.all(ErpSpace.s3), child: Text('Expenses', style: TextStyle(fontWeight: FontWeight.w700))),
            for (final e in cost) line('expenses', e, canCost, '${e['category']} · ${e['description']}', formatDate(e['date']), (e['amount'] as num?) ?? 0),
          ]);
        },
      ),
    );
  }
}

// ---------------------------------------------------------------- change orders

FormSpec changeOrderForm(WidgetRef ref, {Json? existing, String? projectId, String? projectName}) {
  final editing = existing != null;
  return FormSpec(
    title: editing ? 'Edit change order' : 'New change order',
    initial: {
      if (editing) ...{
        'projectId': existing['projectId'],
        '__label_projectId': existing['number'],
        'title': existing['title'],
        'description': existing['description'],
        'amountChange': existing['amountChange']?.toString(),
        'hoursChange': existing['hoursChange']?.toString(),
      } else if (projectId != null) ...{'projectId': projectId, '__label_projectId': projectName},
    },
    fields: [
      if (!editing) Field.lookup('projectId', 'Project', path: '$pBase/projects', subtitleKey: 'number', required: true),
      Field.text('title', 'Title', required: true, maxLength: 200),
      Field.multiline('description', 'What changes', maxLength: 2000),
      Field.money('amountChange', 'Change to the budget / fee', required: true, min: null, hint: 'Negative to reduce it'),
      Field.decimal('hoursChange', 'Change to the budgeted hours', decimals: 2, initial: '0'),
    ],
    submit: (ref, body, raw) {
      final api = ref.read(apiProvider);
      final full = {...body, if (editing) 'projectId': existing['projectId']};
      return editing ? api.put('$pBase/change-orders/${existing['id']}', body: full) : api.post('$pBase/change-orders', body: full);
    },
  );
}

class ChangeOrdersScreen extends StatelessWidget {
  const ChangeOrdersScreen({super.key});
  @override
  Widget build(BuildContext context) => ArrayScreen(
        title: 'Change orders',
        path: '$pBase/change-orders',
        emptyText: 'No change orders yet.',
        create: (ref) => changeOrderForm(ref),
        createPermission: 'projects.changeorders.manage',
        tile: (context, ref, c) {
          final draft = c['status'] == 'draft';
          final can = ref.read(meOrEmptyProvider).can('projects.changeorders.manage');
          return ListTile(
            title: Text('${c['number']} · ${c['title']}', maxLines: 1, overflow: TextOverflow.ellipsis),
            subtitle: Text('${formatMoney((c['amountChange'] as num?) ?? 0)} · ${(c['hoursChange'] ?? 0)} h'),
            trailing: StatusChip(humanize('${c['status']}'), color: statusColor(c['status'] == 'approved' ? 'done' : c['status'] == 'rejected' ? 'cancelled' : 'review')),
            onTap: draft && can
                ? () => rowActions(context, ref, [
                      (label: 'Edit', icon: Icons.edit_outlined, run: () async { await openForm<Object?>(context, changeOrderForm(ref, existing: c)); }),
                      (label: 'Approve', icon: Icons.check_circle_outline, run: () async { await ref.read(apiProvider).post('$pBase/change-orders/${c['id']}/decide', body: {'approve': true}); }),
                      (label: 'Reject', icon: Icons.cancel_outlined, run: () async { await ref.read(apiProvider).post('$pBase/change-orders/${c['id']}/decide', body: {'approve': false}); }),
                      (label: 'Delete', icon: Icons.delete_outline, run: () async { await ref.read(apiProvider).delete('$pBase/change-orders/${c['id']}'); }),
                    ])
                : null,
          );
        },
      );
}

// ---------------------------------------------------------------- risks and issues

FormSpec riskForm(WidgetRef ref, {Json? existing, String? projectId, String? projectName}) {
  final editing = existing != null;
  return FormSpec(
    title: editing ? 'Edit risk or issue' : 'Log a risk or issue',
    initial: {
      if (editing) ...{
        'projectId': existing['projectId'],
        '__label_projectId': 'Project',
        'kind': existing['kind'],
        'title': existing['title'],
        'notes': existing['notes'],
        'severity': existing['severity'],
        'status': existing['status'],
        'ownerUserId': existing['ownerUserId'],
        '__label_ownerUserId': existing['ownerName'],
        'dueDate': existing['dueDate'],
      } else if (projectId != null) ...{'projectId': projectId, '__label_projectId': projectName},
    },
    fields: [
      if (!editing) Field.lookup('projectId', 'Project', path: '$pBase/projects', subtitleKey: 'number', required: true),
      Field.select('kind', 'Kind', options: riskKinds, required: true, initial: 'risk'),
      Field.text('title', 'Title', required: true, maxLength: 200),
      Field.multiline('notes', 'Details and plan', maxLength: 2000),
      Field.select('severity', 'Severity', options: riskSeverities, required: true, initial: 'medium'),
      Field.select('status', 'Status', options: riskStatuses, required: true, initial: 'open'),
      Field.lookup('ownerUserId', 'Owner', path: '/api/v1/users', labelKey: 'fullName', subtitleKey: 'email'),
      Field.date('dueDate', 'Resolve by'),
    ],
    submit: (ref, body, raw) {
      final api = ref.read(apiProvider);
      final full = {...body, if (editing) 'projectId': existing['projectId']};
      return editing ? api.put('$pBase/risks/${existing['id']}', body: full) : api.post('$pBase/risks', body: full);
    },
  );
}

class RisksScreen extends StatelessWidget {
  const RisksScreen({super.key});
  @override
  Widget build(BuildContext context) => ArrayScreen(
        title: 'Risks and issues',
        path: '$pBase/risks',
        emptyText: 'Nothing logged.',
        create: (ref) => riskForm(ref),
        createPermission: 'projects.risks.manage',
        tile: (context, ref, r) {
          final can = ref.read(meOrEmptyProvider).can('projects.risks.manage');
          final late = r['status'] == 'open' && r['dueDate'] != null && '${r['dueDate']}'.compareTo(isoDay(DateTime.now())) < 0;
          return ListTile(
            title: Text('${humanize('${r['kind']}')} · ${r['title']}', maxLines: 1, overflow: TextOverflow.ellipsis),
            subtitle: Text('${r['ownerName'] ?? 'No owner'}${r['dueDate'] == null ? '' : ' · due ${formatDate(r['dueDate'])}'}${late ? ' (overdue)' : ''}'),
            trailing: Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.end, children: [
              StatusChip(humanize('${r['severity']}'), color: statusColor(r['severity'] == 'critical' ? 'urgent' : '${r['severity']}')),
              StatusChip(humanize('${r['status']}'), color: r['status'] == 'closed' ? Colors.green.shade700 : Colors.blueGrey),
            ]),
            onTap: can
                ? () => rowActions(context, ref, [
                      (label: 'Edit', icon: Icons.edit_outlined, run: () async { await openForm<Object?>(context, riskForm(ref, existing: r)); }),
                      (label: 'Delete', icon: Icons.delete_outline, run: () async { await ref.read(apiProvider).delete('$pBase/risks/${r['id']}'); }),
                    ])
                : null,
          );
        },
      );
}

// ---------------------------------------------------------------- rate cards and allocations

FormSpec rateForm(WidgetRef ref, {Json? existing}) => FormSpec(
      title: existing == null ? 'New rate' : 'Edit rate',
      initial: {
        if (existing != null) ...{
          'projectId': existing['projectId'],
          '__label_projectId': 'Project',
          'userId': existing['userId'],
          '__label_userId': existing['userName'],
          'role': existing['role'],
          'billRate': existing['billRate']?.toString(),
          'costRate': existing['costRate']?.toString(),
        },
      },
      intro: 'The most specific rate wins: a person on a project, the person, a role on the project, the project, then everyone.',
      fields: [
        Field.lookup('projectId', 'Project (blank = every project)', path: '$pBase/projects', subtitleKey: 'number'),
        Field.lookup('userId', 'Person', path: '/api/v1/users', labelKey: 'fullName', subtitleKey: 'email'),
        Field.text('role', 'or a role', maxLength: 100, visibleWhen: (v) => v['userId'] == null),
        Field.money('billRate', 'Bill rate per hour', required: true),
        Field.money('costRate', 'Cost rate per hour', required: true),
      ],
      submit: (ref, body, raw) {
        final api = ref.read(apiProvider);
        return existing == null ? api.post('$pBase/rates', body: body) : api.put('$pBase/rates/${existing['id']}', body: body);
      },
    );

FormSpec allocationForm(WidgetRef ref, {Json? existing}) => FormSpec(
      title: existing == null ? 'New allocation' : 'Edit allocation',
      initial: {
        if (existing != null) ...{
          'projectId': existing['projectId'],
          '__label_projectId': 'Project',
          'userId': existing['userId'],
          '__label_userId': existing['userName'],
          'fromDate': existing['fromDate'],
          'toDate': existing['toDate'],
          'hoursPerWeek': existing['hoursPerWeek']?.toString(),
        },
      },
      fields: [
        Field.lookup('projectId', 'Project', path: '$pBase/projects', subtitleKey: 'number', required: true),
        Field.lookup('userId', 'Person', path: '/api/v1/users', labelKey: 'fullName', subtitleKey: 'email', required: true),
        Field.date('fromDate', 'From', required: true, initial: 'today'),
        Field.date('toDate', 'To', required: true, initial: 'today', validator: (v, all) => v != null && all['fromDate'] != null && '$v'.compareTo('${all['fromDate']}') < 0 ? 'Not before the start date' : null),
        Field.decimal('hoursPerWeek', 'Hours per week', required: true, min: 1, max: 168, decimals: 1, initial: '20'),
      ],
      submit: (ref, body, raw) {
        final api = ref.read(apiProvider);
        return existing == null ? api.post('$pBase/allocations', body: body) : api.put('$pBase/allocations/${existing['id']}', body: body);
      },
    );

class RatesScreen extends StatelessWidget {
  const RatesScreen({super.key});
  @override
  Widget build(BuildContext context) => ArrayScreen(
        title: 'Rate cards',
        path: '$pBase/rates',
        emptyText: 'No rate cards. Time uses the workspace default rate.',
        create: (ref) => rateForm(ref),
        createPermission: 'projects.rates.manage',
        tile: (context, ref, r) => ListTile(
          title: Text('${r['userName'] ?? r['role'] ?? 'Everyone'}'),
          subtitle: Text(r['projectId'] == null ? 'All projects' : 'One project'),
          trailing: Text('${formatMoney((r['billRate'] as num?) ?? 0)} / ${formatMoney((r['costRate'] as num?) ?? 0)}'),
          onTap: ref.read(meOrEmptyProvider).can('projects.rates.manage')
              ? () => rowActions(context, ref, [
                    (label: 'Edit', icon: Icons.edit_outlined, run: () async { await openForm<Object?>(context, rateForm(ref, existing: r)); }),
                    (label: 'Delete', icon: Icons.delete_outline, run: () async { await ref.read(apiProvider).delete('$pBase/rates/${r['id']}'); }),
                  ])
              : null,
        ),
      );
}

class AllocationsScreen extends StatelessWidget {
  const AllocationsScreen({super.key});
  @override
  Widget build(BuildContext context) => ArrayScreen(
        title: 'Allocations',
        path: '$pBase/allocations',
        emptyText: 'Nobody is allocated yet.',
        create: (ref) => allocationForm(ref),
        createPermission: 'projects.team.manage',
        tile: (context, ref, a) => ListTile(
          title: Text('${a['userName']}'),
          subtitle: Text('${formatDate(a['fromDate'])} to ${formatDate(a['toDate'])}'),
          trailing: Text('${a['hoursPerWeek']} h / week'),
          onTap: ref.read(meOrEmptyProvider).can('projects.team.manage')
              ? () => rowActions(context, ref, [
                    (label: 'Edit', icon: Icons.edit_outlined, run: () async { await openForm<Object?>(context, allocationForm(ref, existing: a)); }),
                    (label: 'Delete', icon: Icons.delete_outline, run: () async { await ref.read(apiProvider).delete('$pBase/allocations/${a['id']}'); }),
                  ])
              : null,
        ),
      );
}

/// Rate cards and allocations behind one menu entry.
class ResourcesScreen extends StatelessWidget {
  const ResourcesScreen({super.key});
  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: const Text('Resources')),
        body: ListView(children: [
          ListTile(leading: const Icon(Icons.payments_outlined), title: const Text('Rate cards'), subtitle: const Text('What an hour costs and bills at'), onTap: () => Navigator.of(context).push(_route(const RatesScreen()))),
          ListTile(leading: const Icon(Icons.groups_outlined), title: const Text('Allocations'), subtitle: const Text('Planned hours per person and project'), onTap: () => Navigator.of(context).push(_route(const AllocationsScreen()))),
        ]),
      );
}

// ---------------------------------------------------------------- clone

FormSpec cloneForm(WidgetRef ref, String projectId, String projectName) => FormSpec(
      title: 'Clone project',
      initial: {'name': '$projectName (copy)'},
      intro: 'Tasks, milestones, dependencies and rate cards are copied; dates move with the start date. Time, expenses and billing are not copied.',
      fields: [
        Field.text('name', 'Name of the copy', required: true, maxLength: 200),
        Field.date('startDate', 'Start date'),
        Field.toggle('includeTeam', 'Keep the team and assignees'),
        Field.toggle('asTemplate', 'Save the copy as a template'),
      ],
      submit: (ref, body, raw) => ref.read(apiProvider).post('$pBase/projects/$projectId/clone', body: body),
    );

// ---------------------------------------------------------------- reports

/// Profit, budget and utilisation in one scroll.
class ProjectReportsScreen extends ConsumerWidget {
  const ProjectReportsScreen({super.key});

  Future<List<List<Json>>> _load(WidgetRef ref) async {
    final api = ref.read(apiProvider);
    final now = DateTime.now();
    final from = isoDay(DateTime(now.year, now.month, 1)), to = isoDay(now);
    final profit = await api.get('$pBase/reports/profitability', query: {'from': from, 'to': to});
    final budget = await api.get('$pBase/reports/budget');
    final util = await api.get('$pBase/reports/utilisation', query: {'from': from, 'to': to});
    final ageing = await api.get('$pBase/reports/unbilled-ageing');
    return [items(profit), items(budget), items(util), items(ageing)];
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    ref.watch(dataVersionProvider);
    String m(Object? v) => formatMoney((v as num?) ?? 0);
    return Scaffold(
      appBar: AppBar(title: const Text('Project reports')),
      body: FutureBuilder<List<List<Json>>>(
        future: _load(ref),
        builder: (context, snap) {
          if (snap.connectionState != ConnectionState.done) return const ErpSkeleton.list();
          if (snap.hasError) return Center(child: Text('${snap.error}'));
          final profit = snap.data![0], budget = snap.data![1], util = snap.data![2], ageing = snap.data![3];
          return ListView(padding: const EdgeInsets.all(ErpSpace.s3), children: [
            InfoCard(title: 'Profit this month', rows: [
              for (final r in profit) InfoRow('${r['number']} · ${r['name']}', '${m(r['margin'])} (${r['marginPercent']}%)'),
              if (profit.isEmpty) const InfoRow('No projects', ''),
            ]),
            InfoCard(title: 'Budget and health', rows: [
              for (final r in budget) InfoRow('${r['number']} · ${r['name']}', '${r['usedPercent']}% used · ${humanize('${r['health']}')}'),
              if (budget.isEmpty) const InfoRow('No projects', ''),
            ]),
            InfoCard(title: 'Utilisation this month', rows: [
              for (final r in util) InfoRow('${r['person']}', '${r['loggedHours']} h of ${r['capacityHours']} h (${r['utilisationPercent']}%)'),
              if (util.isEmpty) const InfoRow('No time logged', ''),
            ]),
            InfoCard(title: 'Unbilled ageing', rows: [
              for (final r in ageing) InfoRow('${r['number']} · ${r['name']}', '${m(r['total'])} (over 90 days: ${m(r['over90'])})'),
              if (ageing.isEmpty) const InfoRow('Nothing unbilled', ''),
            ]),
          ]);
        },
      ),
    );
  }
}

// ---------------------------------------------------------------- my week and the timer

const _timerKey = 'projects_timer';

/// This week's time by day, a quick "log time", and a start/stop timer that survives a restart.
class MyWeekScreen extends ConsumerStatefulWidget {
  const MyWeekScreen({super.key});

  @override
  ConsumerState<MyWeekScreen> createState() => _MyWeekState();
}

class _MyWeekState extends ConsumerState<MyWeekScreen> {
  DateTime _monday = mondayOf(DateTime.now());
  DateTime? _started;
  String? _timerProject;
  String? _timerProjectName;

  @override
  void initState() {
    super.initState();
    _restoreTimer();
  }

  Future<void> _restoreTimer() async {
    final raw = await ref.read(storeProvider).read(_timerKey);
    if (raw == null || !raw.contains('|')) return;
    final parts = raw.split('|');
    if (mounted) setState(() { _started = DateTime.tryParse(parts[0]); _timerProject = parts[1]; _timerProjectName = parts.length > 2 ? parts[2] : null; });
  }

  Future<Json> _load() async {
    final from = isoDay(_monday), to = isoDay(_monday.add(const Duration(days: 6)));
    final r = await ref.read(apiProvider).get('$pBase/timesheets', query: {'filter': 'date:gte:$from,date:lte:$to', 'pageSize': 200, 'sort': 'date'});
    return {'rows': items(r)};
  }

  Future<void> _stop() async {
    final started = _started;
    if (started == null) return;
    final hours = timerHours(DateTime.now().difference(started));
    final projectId = _timerProject, name = _timerProjectName;
    await ref.read(storeProvider).delete(_timerKey);
    setState(() { _started = null; _timerProject = null; });
    if (!mounted) return;
    await openForm<Object?>(context, timesheetForm(ref, projectId: projectId, projectName: name, hours: '$hours'));
    ref.read(dataVersionProvider.notifier).bump();
  }

  Future<void> _start() async {
    final picked = await Navigator.of(context).push<Json>(_route(const _ProjectPicker()));
    if (picked == null) return;
    final now = DateTime.now();
    await ref.read(storeProvider).write(_timerKey, '${now.toIso8601String()}|${picked['id']}|${picked['name']}');
    if (mounted) setState(() { _started = now; _timerProject = '${picked['id']}'; _timerProjectName = '${picked['name']}'; });
  }

  @override
  Widget build(BuildContext context) {
    ref.watch(dataVersionProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('My week')),
      floatingActionButton: FloatingActionButton(tooltip: 'Log time', onPressed: () => openForm<Object?>(context, timesheetForm(ref)).then((_) => ref.read(dataVersionProvider.notifier).bump()), child: const Icon(Icons.add)),
      body: Column(children: [
        Card(
          margin: const EdgeInsets.all(ErpSpace.s3),
          child: ListTile(
            leading: Icon(_started == null ? Icons.timer_outlined : Icons.timer),
            title: Text(_started == null ? 'Timer' : 'Timing ${_timerProjectName ?? 'a project'}'),
            subtitle: Text(_started == null ? 'Start it when you begin; stop it to log the time.' : 'Since ${TimeOfDay.fromDateTime(_started!).format(context)}'),
            trailing: FilledButton(onPressed: _started == null ? _start : _stop, child: Text(_started == null ? 'Start' : 'Stop')),
          ),
        ),
        Row(mainAxisAlignment: MainAxisAlignment.spaceBetween, children: [
          IconButton(tooltip: 'Previous week', icon: const Icon(Icons.chevron_left), onPressed: () => setState(() => _monday = _monday.subtract(const Duration(days: 7)))),
          Text('${isoDay(_monday)} to ${isoDay(_monday.add(const Duration(days: 6)))}', style: const TextStyle(fontWeight: FontWeight.w600)),
          IconButton(tooltip: 'Next week', icon: const Icon(Icons.chevron_right), onPressed: () => setState(() => _monday = _monday.add(const Duration(days: 7)))),
        ]),
        Expanded(
          child: FutureBuilder<Json>(
            key: ValueKey('$_monday${ref.watch(dataVersionProvider)}'),
            future: _load(),
            builder: (context, snap) {
              if (snap.connectionState != ConnectionState.done) return const ErpSkeleton.list();
              if (snap.hasError) return Center(child: Text('${snap.error}'));
              final rows = (snap.data!['rows'] as List).cast<Json>();
              final total = rows.fold<num>(0, (a, r) => a + ((r['hours'] as num?) ?? 0));
              return Column(children: [
                Padding(padding: const EdgeInsets.all(ErpSpace.s2), child: Text('Total this week: $total h', style: const TextStyle(fontWeight: FontWeight.w700))),
                Expanded(
                  child: rows.isEmpty
                      ? const Center(child: Text('No time logged this week.'))
                      : ListView.separated(
                          itemCount: rows.length,
                          separatorBuilder: (_, _) => const Divider(height: 1),
                          itemBuilder: (context, i) => ListTile(
                            title: Text('${rows[i]['hours']} h · ${rows[i]['projectName'] ?? ''}'),
                            subtitle: Text('${formatDate(rows[i]['date'])} · ${rows[i]['workerName']}${(rows[i]['description'] ?? '').toString().isEmpty ? '' : ' · ${rows[i]['description']}'}'),
                            trailing: StatusChip(humanize('${rows[i]['approvalStatus']}'), color: statusColor('${rows[i]['approvalStatus']}' == 'approved' ? 'done' : '${rows[i]['approvalStatus']}' == 'rejected' ? 'cancelled' : 'review')),
                          ),
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

class _ProjectPicker extends ConsumerWidget {
  const _ProjectPicker();
  @override
  Widget build(BuildContext context, WidgetRef ref) => Scaffold(
        appBar: AppBar(title: const Text('Which project?')),
        body: FutureBuilder<dynamic>(
          future: ref.read(apiProvider).get('$pBase/projects', query: {'pageSize': 100, 'filter': 'status:eq:Active'}),
          builder: (context, snap) {
            if (snap.connectionState != ConnectionState.done) return const ErpSkeleton.list();
            if (snap.hasError) return Center(child: Text('${snap.error}'));
            final rows = items(snap.data);
            if (rows.isEmpty) return const Center(child: Text('No active projects.'));
            return ListView(children: [for (final p in rows) ListTile(title: Text('${p['name']}'), subtitle: Text('${p['number']}'), onTap: () => Navigator.pop(context, p))]);
          },
        ),
      );
}
