import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/format.dart';
import '../../core/me.dart';
import '../../core/session.dart';
import '../../kit/detail_page.dart';
import '../../kit/field.dart';
import '../../kit/form_page.dart';
import '../../kit/list_screen.dart';
import '../../kit/skeleton.dart';
import '../../kit/widgets.dart';
import '../common/paged_list_view.dart';
import 'hr_common.dart';
import 'package:erp_mobile/theme/tokens.dart';

// ---- Employee documents ----------------------------------------------------------------------------------------------------

const _docsPath = '$hrPath/documents';
const documentTypes = [
  Option('passport', 'Passport'), Option('visa', 'Visa'), Option('residenceId', 'Residence ID'), Option('labourCard', 'Labour card'),
  Option('medical', 'Medical'), Option('contract', 'Contract'), Option('certificate', 'Certificate'), Option('other', 'Other'),
];

String documentTypeLabel(String t) => documentTypes.where((o) => o.value == t).map((o) => o.label).firstOrNull ?? humanize(t);

/// Days from today to a yyyy-MM-dd date (negative when past); null for no date.
int? daysUntil(Object? iso, {DateTime? now}) {
  final d = DateTime.tryParse('$iso');
  if (d == null) return null;
  final today = now ?? DateTime.now();
  return DateTime(d.year, d.month, d.day).difference(DateTime(today.year, today.month, today.day)).inDays;
}

Widget expiryChip(int? left) {
  if (left == null) return const SizedBox.shrink();
  if (left < 0) return StatusChip('Expired', color: Colors.red.shade700);
  if (left <= 30) return StatusChip('$left days left', color: Colors.orange.shade800);
  return StatusChip('Valid', color: Colors.green.shade700);
}

FormSpec documentForm(String employeeId, {Json? existing}) => FormSpec(
      title: existing == null ? 'Add document' : 'Edit document',
      initial: {if (existing != null) for (final k in ['type', 'number', 'issueDate', 'expiryDate', 'issuedBy', 'notes']) k: existing[k]},
      fields: [
        Field.select('type', 'Type', options: documentTypes, required: true, initial: 'passport'),
        Field.text('number', 'Number', maxLength: 100),
        Field.date('issueDate', 'Issue date'),
        Field.date('expiryDate', 'Expiry date'),
        Field.text('issuedBy', 'Issued by', maxLength: 150),
        Field.multiline('notes', 'Notes', maxLength: 1000),
      ],
      submit: (ref, body, raw) {
        final api = ref.read(apiProvider);
        final full = {...body, 'employeeId': employeeId};
        return existing == null ? api.post(_docsPath, body: full) : api.put('$_docsPath/${existing['id']}', body: full);
      },
    );

class EmployeeDocumentsScreen extends StatelessWidget {
  const EmployeeDocumentsScreen({super.key, required this.employeeId, required this.name});

  final String employeeId;
  final String name;

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Documents · $name',
        path: _docsPath,
        query: {'filter': 'employeeId:eq:$employeeId', 'sort': 'expiryDate'},
        emptyText: 'No documents recorded.',
        create: (ref) => documentForm(employeeId),
        createPermission: 'hr.employees.manage',
        itemBuilder: (context, d) => Consumer(
          builder: (context, ref, _) => ListTile(
            title: Text(documentTypeLabel('${d['type']}')),
            subtitle: Text('${(d['number'] ?? '').toString().isEmpty ? '' : '${d['number']} · '}${d['expiryDate'] == null ? 'No expiry' : 'Expires ${formatDate(d['expiryDate'])}'}'),
            trailing: d['expiryDate'] == null ? null : expiryChip(daysUntil(d['expiryDate'])),
            onTap: ref.read(meOrEmptyProvider).can('hr.employees.manage') ? () => openForm<Object?>(context, documentForm(employeeId, existing: d)) : null,
          ),
        ),
      );
}

/// Every employee's documents that are expired or expire soon.
class DocumentExpiryScreen extends ConsumerStatefulWidget {
  const DocumentExpiryScreen({super.key});

  @override
  ConsumerState<DocumentExpiryScreen> createState() => _DocumentExpiryState();
}

class _DocumentExpiryState extends ConsumerState<DocumentExpiryScreen> {
  int _days = 60;
  late Future<List<Json>> _future = _load();

  Future<List<Json>> _load() async {
    final data = await ref.read(apiProvider).get('$_docsPath/expiring', query: {'days': _days});
    return [for (final r in (data is List ? data : const [])) r as Json];
  }

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: const Text('Document expiry')),
        body: Column(children: [
          Padding(
            padding: const EdgeInsets.all(ErpSpace.s3),
            child: Wrap(spacing: ErpSpace.s2, children: [
              for (final d in const [30, 60, 90, 180, 365])
                ChoiceChip(label: Text('$d days'), selected: _days == d, onSelected: (_) => setState(() { _days = d; _future = _load(); })),
            ]),
          ),
          Expanded(
            child: FutureBuilder<List<Json>>(
              future: _future,
              builder: (context, snap) {
                if (snap.connectionState != ConnectionState.done) return const ErpSkeleton.list();
                if (snap.hasError) return Center(child: Text('${snap.error}'));
                final rows = snap.data ?? const [];
                if (rows.isEmpty) return const Center(child: Text('Nothing expires in this period.'));
                return ListView.separated(
                  itemCount: rows.length,
                  separatorBuilder: (_, _) => const Divider(height: 1),
                  itemBuilder: (context, i) {
                    final r = rows[i];
                    return ListTile(
                      title: Text('${r['employeeName']}'),
                      subtitle: Text('${documentTypeLabel('${r['type']}')}${(r['number'] ?? '').toString().isEmpty ? '' : ' · ${r['number']}'} · ${formatDate(r['expiryDate'])}'),
                      trailing: expiryChip((r['daysLeft'] as num?)?.toInt()),
                    );
                  },
                );
              },
            ),
          ),
        ]),
      );
}

// ---- Loans and advances ----------------------------------------------------------------------------------------------------

const _loansPath = '$hrPath/loans';
const loanKinds = [Option('loan', 'Loan'), Option('advance', 'Salary advance')];
const loanStatuses = [Option('active', 'Active'), Option('settled', 'Settled'), Option('cancelled', 'Cancelled')];

Color loanColor(String s) => switch (s) { 'active' => Colors.blue.shade700, 'settled' => Colors.green.shade700, _ => Colors.grey.shade700 };

/// The recovery month and the number of instalments define the schedule; the instalment defaults to amount / count.
FormSpec loanForm() {
  final now = DateTime.now();
  return FormSpec(
    title: 'New loan or advance',
    intro: 'Instalments are taken from the monthly payroll and never reduce the take-home pay below zero.',
    fields: [
      Field.lookup('employeeId', 'Employee', path: '$hrPath/employees', labelKey: 'fullName', subtitleKey: 'number', required: true),
      Field.select('kind', 'Type', options: loanKinds, required: true, initial: 'loan'),
      Field.money('principal', 'Amount', required: true, min: 0.01),
      Field.integer('instalments', 'Number of instalments', required: true, min: 1, max: 120, initial: '1'),
      Field.money('instalmentAmount', 'Instalment amount (optional)', hint: 'Left blank, the amount is divided equally.'),
      Field.integer('startYear', 'First recovery year', required: true, min: 2000, max: 2100, initial: '${now.year}'),
      Field.integer('startMonth', 'First recovery month (1-12)', required: true, min: 1, max: 12, initial: '${now.month}'),
      Field.text('reason', 'Reason', maxLength: 300),
    ],
    submit: (ref, body, raw) => ref.read(apiProvider).post(_loansPath, body: body),
  );
}

FormSpec disburseForm(Json loan) => FormSpec(
      title: 'Pay out the loan',
      submitLabel: 'Pay out',
      fields: [
        Field.lookup('bankAccountId', 'Pay from', path: '/api/v1/finance/accounts', query: const {'filter': 'isBank:eq:true'}, subtitleKey: 'code', required: true),
        Field.date('date', 'Payment date', required: true, initial: 'today'),
      ],
      submit: (ref, body, raw) => ref.read(apiProvider).post('$_loansPath/${loan['id']}/disburse', body: body),
    );

class LoansScreen extends StatelessWidget {
  const LoansScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Loans and advances',
        path: _loansPath,
        emptyText: 'No loans or advances yet.',
        filters: loanStatuses,
        create: (ref) => loanForm(),
        createPermission: 'hr.payroll.manage',
        itemBuilder: (context, l) {
          final status = '${l['status']}';
          return ListTile(
            onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => LoanDetailScreen(id: '${l['id']}'))),
            title: NameText(employeeNames, l['employeeId']),
            subtitle: Text('${humanize('${l['kind']}')} · ${formatMoney((l['principal'] as num?) ?? 0)} · from ${monthName((l['startMonth'] as num).toInt())} ${l['startYear']}'),
            trailing: Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.end, children: [
              Text(formatMoney((l['remaining'] as num?) ?? 0), style: const TextStyle(fontWeight: FontWeight.w600)),
              const SizedBox(height: ErpSpace.s1),
              StatusChip(humanize(status), color: loanColor(status)),
            ]),
          );
        },
      );
}

class LoanDetailScreen extends StatelessWidget {
  const LoanDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) {
    final path = '$_loansPath/$id';
    return DetailPage(
      path: path,
      titleOf: (d) => '${d['employeeName']}',
      body: (context, d) => DetailBody(children: [
        DetailHeader(title: '${d['employeeName']}', subtitle: '${humanize('${d['kind']}')} · ${formatMoney((d['principal'] as num?) ?? 0)}'),
        Align(alignment: Alignment.centerLeft, child: Padding(padding: const EdgeInsets.only(bottom: ErpSpace.s3), child: StatusChip(humanize('${d['status']}'), color: loanColor('${d['status']}')))),
        InfoCard(rows: [
          InfoRow('Remaining', formatMoney((d['remaining'] as num?) ?? 0), bold: true),
          InfoRow('Instalment', formatMoney((d['instalmentAmount'] as num?) ?? 0)),
          InfoRow('Instalments', '${d['instalments']}'),
          InfoRow('Recovery starts', '${monthName((d['startMonth'] as num).toInt())} ${d['startYear']}'),
          InfoRow('Paid out on', d['disbursedOn'] == null ? 'Not yet' : formatDate(d['disbursedOn'])),
          InfoRow('Reason', text(d['reason'])),
        ]),
      ]),
      actions: [
        ActionSpec(
          label: 'Pay out',
          icon: Icons.payments_outlined,
          permission: 'hr.payroll.pay',
          visible: (d) => d['status'] == 'active' && d['disbursedOn'] == null,
          form: disburseForm,
          run: (ref, d, _) async => null,
        ),
        ActionSpec(
          label: 'Cancel loan',
          icon: Icons.block,
          permission: 'hr.payroll.manage',
          destructive: true,
          visible: (d) => d['status'] == 'active',
          confirm: 'Stop recovering this loan? Instalments already taken stay.',
          successMessage: 'Cancelled',
          run: (ref, d, _) => ref.read(apiProvider).post('$path/cancel'),
        ),
      ],
    );
  }
}

// ---- Departments and positions ---------------------------------------------------------------------------------------------

const _departmentsPath = '$hrPath/departments';
const _positionsPath = '$hrPath/positions';

FormSpec departmentForm({Json? existing}) => FormSpec(
      title: existing == null ? 'New department' : 'Edit department',
      initial: {
        if (existing != null) ...{
          for (final k in ['code', 'name', 'parentDepartmentId', 'headEmployeeId', 'isActive']) k: existing[k],
        },
      },
      fields: [
        Field.text('code', 'Code', required: true, maxLength: 20),
        Field.text('name', 'Name', required: true, maxLength: 150),
        Field.lookup('parentDepartmentId', 'Parent department', path: _departmentsPath, labelKey: 'name', subtitleKey: 'code'),
        Field.lookup('headEmployeeId', 'Head of department', path: '$hrPath/employees', labelKey: 'fullName', subtitleKey: 'number'),
        Field.toggle('isActive', 'Active', initial: true),
      ],
      submit: (ref, body, raw) {
        final api = ref.read(apiProvider);
        return existing == null ? api.post(_departmentsPath, body: body) : api.put('$_departmentsPath/${existing['id']}', body: body);
      },
    );

FormSpec positionForm({Json? existing}) => FormSpec(
      title: existing == null ? 'New position' : 'Edit position',
      initial: {if (existing != null) for (final k in ['code', 'title', 'grade', 'isActive']) k: existing[k]},
      fields: [
        Field.text('code', 'Code', required: true, maxLength: 20),
        Field.text('title', 'Title', required: true, maxLength: 150),
        Field.text('grade', 'Grade', maxLength: 30),
        Field.toggle('isActive', 'Active', initial: true),
      ],
      submit: (ref, body, raw) {
        final api = ref.read(apiProvider);
        return existing == null ? api.post(_positionsPath, body: body) : api.put('$_positionsPath/${existing['id']}', body: body);
      },
    );

class DepartmentsScreen extends StatelessWidget {
  const DepartmentsScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Departments',
        path: _departmentsPath,
        emptyText: 'No departments yet.',
        create: (ref) => departmentForm(),
        createPermission: 'hr.employees.manage',
        itemBuilder: (context, d) => Consumer(
          builder: (context, ref, _) => ListTile(
            title: Text('${d['name']}'),
            subtitle: Text('${d['code']}'),
            trailing: d['isActive'] == false ? StatusChip('Inactive', color: Colors.grey.shade700) : null,
            onTap: ref.read(meOrEmptyProvider).can('hr.employees.manage') ? () => openForm<Object?>(context, departmentForm(existing: d)) : null,
          ),
        ),
      );
}

class PositionsScreen extends StatelessWidget {
  const PositionsScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Positions',
        path: _positionsPath,
        emptyText: 'No positions yet.',
        create: (ref) => positionForm(),
        createPermission: 'hr.employees.manage',
        itemBuilder: (context, p) => Consumer(
          builder: (context, ref, _) => ListTile(
            title: Text('${p['title']}'),
            subtitle: Text('${p['code']}${(p['grade'] ?? '').toString().isEmpty ? '' : ' · grade ${p['grade']}'}'),
            trailing: p['isActive'] == false ? StatusChip('Inactive', color: Colors.grey.shade700) : null,
            onTap: ref.read(meOrEmptyProvider).can('hr.employees.manage') ? () => openForm<Object?>(context, positionForm(existing: p)) : null,
          ),
        ),
      );
}

/// Departments (with people) and reporting lines, as expandable trees.
class OrgChartScreen extends ConsumerStatefulWidget {
  const OrgChartScreen({super.key});

  @override
  ConsumerState<OrgChartScreen> createState() => _OrgChartState();
}

class _OrgChartState extends ConsumerState<OrgChartScreen> {
  late final Future<Json> _future = ref.read(apiProvider).get('$hrPath/org-chart').then((d) => d as Json);

  @override
  Widget build(BuildContext context) => DefaultTabController(
        length: 2,
        child: Scaffold(
          appBar: AppBar(title: const Text('Organisation'), bottom: const TabBar(tabs: [Tab(text: 'Departments'), Tab(text: 'Reporting lines')])),
          body: FutureBuilder<Json>(
            future: _future,
            builder: (context, snap) {
              if (snap.connectionState != ConnectionState.done) return const ErpSkeleton.list();
              if (snap.hasError) return Center(child: Text('${snap.error}'));
              final chart = snap.data!;
              final departments = (chart['departments'] as List<dynamic>? ?? const []).cast<Json>();
              final unassigned = (chart['unassigned'] as List<dynamic>? ?? const []).cast<Json>();
              final lines = (chart['reportingLines'] as List<dynamic>? ?? const []).cast<Json>();
              return TabBarView(children: [
                ListView(padding: const EdgeInsets.all(ErpSpace.s3), children: [
                  for (final d in departments) _DepartmentNode(d),
                  if (unassigned.isNotEmpty) Card(child: ListTile(title: const Text('No department'), subtitle: Text(unassigned.map((m) => '${m['name']}').join(', ')))),
                ]),
                ListView(padding: const EdgeInsets.all(ErpSpace.s3), children: [for (final p in lines) _PersonNode(p)]),
              ]);
            },
          ),
        ),
      );
}

class _DepartmentNode extends StatelessWidget {
  const _DepartmentNode(this.d);

  final Json d;

  @override
  Widget build(BuildContext context) {
    final members = (d['members'] as List<dynamic>? ?? const []).cast<Json>();
    final children = (d['children'] as List<dynamic>? ?? const []).cast<Json>();
    return Card(
      child: ExpansionTile(
        title: Text('${d['name']}'),
        subtitle: Text('${d['code']} · ${members.length} people'),
        childrenPadding: const EdgeInsets.only(left: ErpSpace.s3),
        children: [
          for (final m in members) ListTile(dense: true, title: Text('${m['name']}'), subtitle: Text('${m['designation']}')),
          for (final c in children) _DepartmentNode(c),
        ],
      ),
    );
  }
}

class _PersonNode extends StatelessWidget {
  const _PersonNode(this.p);

  final Json p;

  @override
  Widget build(BuildContext context) {
    final reports = (p['reports'] as List<dynamic>? ?? const []).cast<Json>();
    final tile = ListTile(title: Text('${p['name']}'), subtitle: Text('${p['designation']} · ${p['number']}'));
    if (reports.isEmpty) return Card(child: tile);
    return Card(
      child: ExpansionTile(
        title: Text('${p['name']}'),
        subtitle: Text('${p['designation']} · ${reports.length} direct reports'),
        childrenPadding: const EdgeInsets.only(left: ErpSpace.s3),
        children: [for (final r in reports) _PersonNode(r)],
      ),
    );
  }
}

// ---- Reports ---------------------------------------------------------------------------------------------------------------

/// Headcount and mix, joiners and leavers, payroll cost, leave, birthdays and anniversaries, end-of-service liability.
class HrReportsScreen extends ConsumerStatefulWidget {
  const HrReportsScreen({super.key});

  @override
  ConsumerState<HrReportsScreen> createState() => _HrReportsState();
}

class _HrReportsState extends ConsumerState<HrReportsScreen> {
  late final Future<Json> _summary = ref.read(apiProvider).get('$hrPath/reports/summary').then((d) => d as Json);
  late final Future<Json> _occasions = ref.read(apiProvider).get('$hrPath/reports/birthdays-anniversaries', query: {'days': 30}).then((d) => d as Json);

  Widget _section<T>(Future<T> future, Widget Function(T) build) => FutureBuilder<T>(
        future: future,
        builder: (context, snap) => snap.connectionState != ConnectionState.done
            ? const Padding(padding: EdgeInsets.all(ErpSpace.s4), child: Center(child: CircularProgressIndicator()))
            : snap.hasError
                ? Padding(padding: const EdgeInsets.all(ErpSpace.s3), child: Text('${snap.error}'))
                : build(snap.data as T),
      );

  List<InfoRow> _counts(List<dynamic> counts, String empty) => [for (final c in counts.cast<Json>()) InfoRow(('${c['key'] ?? ''}').isEmpty ? empty : '${c['key']}', '${c['count']}')];

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: const Text('HR reports')),
        body: ListView(padding: const EdgeInsets.all(ErpSpace.s3), children: [
          _section<Json>(_summary, (s) {
            final cost = (s['payrollCost'] as List<dynamic>?)?.cast<Json>();
            final movements = (s['movements'] as List<dynamic>? ?? const []).cast<Json>();
            return Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
              InfoCard(title: 'Headcount', rows: [
                InfoRow('Employees', '${s['headcount']}', bold: true),
                InfoRow('Average service', '${days(s['averageServiceYears'])} years'),
                InfoRow('Absence days', '${s['absenceDays']}'),
              ]),
              InfoCard(title: 'By department', rows: _counts(s['byDepartment'] as List<dynamic>? ?? const [], 'No department')),
              InfoCard(title: 'By nationality', rows: _counts(s['byNationality'] as List<dynamic>? ?? const [], 'Not set')),
              InfoCard(title: 'By contract', rows: _counts(s['byContractType'] as List<dynamic>? ?? const [], 'Not set')),
              if (movements.isNotEmpty)
                InfoCard(title: 'Joiners and leavers', rows: [for (final m in movements) InfoRow('${monthName((m['month'] as num).toInt())} ${m['year']}', '+${m['joiners']} / -${m['leavers']}')]),
              if (cost != null && cost.isNotEmpty)
                InfoCard(title: 'Payroll cost', rows: [for (final m in cost) InfoRow('${monthName((m['month'] as num).toInt())} ${m['year']}', formatMoney((m['net'] as num?) ?? 0))]),
              InfoCard(title: 'Leave taken (days)', rows: [for (final l in (s['leaveDaysByType'] as List<dynamic>? ?? const []).cast<Json>()) InfoRow('${l['leaveType']}', days(l['days']))]),
            ]);
          }),
          _section<Json>(_occasions, (o) {
            final birthdays = (o['birthdays'] as List<dynamic>? ?? const []).cast<Json>();
            final anniversaries = (o['anniversaries'] as List<dynamic>? ?? const []).cast<Json>();
            return Column(children: [
              InfoCard(title: 'Birthdays (next 30 days)', rows: [for (final b in birthdays) InfoRow('${b['employee']}', formatDate(b['date']))]),
              InfoCard(title: 'Work anniversaries', rows: [for (final a in anniversaries) InfoRow('${a['employee']}', '${a['years']} years · ${formatDate(a['date'])}')]),
            ]);
          }),
        ]),
      );
}
