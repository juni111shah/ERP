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
import 'hr_common.dart';
import 'setup_screens.dart' show pairsOf;
import 'package:erp_mobile/theme/tokens.dart';

List<Json> _items(dynamic data) => [for (final r in (data is List ? data : ((data as Map?)?['items'] as List<dynamic>? ?? const []))) r as Json];

/// "8:5" / "08:05" -> "08:05:00"; blank -> null; invalid -> the string "invalid".
String? timeOf(Object? text) {
  final t = '${text ?? ''}'.trim();
  if (t.isEmpty) return null;
  final m = RegExp(r'^(\d{1,2}):(\d{2})(?::(\d{2}))?$').firstMatch(t);
  if (m == null || int.parse(m.group(1)!) > 23 || int.parse(m.group(2)!) > 59) return 'invalid';
  return '${m.group(1)!.padLeft(2, '0')}:${m.group(2)}:${m.group(3) ?? '00'}';
}

String? _timeError(Object? v, Values all) => v == null || '$v'.trim().isEmpty || timeOf(v) != 'invalid' ? null : 'Enter the time as hh:mm';

// ---- Statutory rules -------------------------------------------------------------------------------------------------------

const _rulesPath = '$hrPath/statutory-rules';
const statutoryKinds = [Option('employeeDeduction', 'Employee deduction'), Option('employerCost', 'Employer cost')];

/// "0:0, 1000:10" pairs -> slab objects (the same text the web form uses).
List<Map<String, num>> slabsOf(Object? text) => [for (final p in pairsOf(text)) {'fromAmount': p[0], 'rate': p[1]}];

FormSpec statutoryForm({Json? existing}) {
  final editing = existing != null;
  return FormSpec(
    title: editing ? 'Edit rule' : 'New statutory rule',
    initial: {
      if (editing) ...{
        for (final k in ['code', 'name', 'kind', 'method', 'base', 'rate', 'threshold', 'cap', 'nationalities', 'effectiveFrom', 'effectiveTo', 'isActive']) k: existing[k],
        'slabsText': [for (final s in (existing['slabs'] as List<dynamic>? ?? const []).cast<Json>()) '${s['fromAmount']}:${s['rate']}'].join(', '),
      },
    },
    intro: 'Applied to every regular payroll run of the entity. An employer cost is added on top of the pay.',
    fields: [
      if (!editing) Field.select('legalEntityId', 'Legal entity', options: const [], required: true),
      Field.text('code', 'Code', required: true, maxLength: 20),
      Field.text('name', 'Name', required: true, maxLength: 150),
      Field.select('kind', 'Taken from', options: statutoryKinds, required: true, initial: 'employeeDeduction'),
      Field.select('method', 'Method', options: const [Option('flat', 'Flat percent'), Option('slab', 'Slabs (progressive)')], required: true, initial: 'flat'),
      Field.select('base', 'Applied to', options: const [Option('basic', 'Basic salary'), Option('gross', 'Gross pay')], required: true, initial: 'basic'),
      Field.decimal('rate', 'Rate (%)', min: 0, max: 100, decimals: 2, initial: '0', visibleWhen: (v) => v['method'] != 'slab'),
      Field.text('slabsText', 'Slabs (from:percent)', maxLength: 300, hint: 'For example 0:0, 1000:10, 3000:20', visibleWhen: (v) => v['method'] == 'slab'),
      Field.money('threshold', 'Nothing below (monthly)', initial: '0'),
      Field.money('cap', 'Cap on the base (monthly, 0 = none)', initial: '0'),
      Field.text('nationalities', 'Nationalities', maxLength: 300, hint: 'Blank = everyone; "!Qatari" = everyone except Qataris'),
      Field.date('effectiveFrom', 'Effective from'),
      Field.date('effectiveTo', 'Effective to'),
      Field.toggle('isActive', 'Active', initial: true),
    ],
    submit: (ref, body, raw) {
      final api = ref.read(apiProvider);
      final rest = {...body}..remove('slabsText');
      final full = {...rest, 'slabs': body['method'] == 'slab' ? slabsOf(body['slabsText']) : <Object>[], if (!editing && body['legalEntityId'] == null) 'legalEntityId': ref.read(meOrEmptyProvider).defaultEntity?.id};
      if (editing) full['legalEntityId'] = existing['legalEntityId'];
      return editing ? api.put('$_rulesPath/${existing['id']}', body: full) : api.post(_rulesPath, body: full);
    },
  );
}

FormSpec statutoryCreateForm(WidgetRef ref) {
  final me = ref.read(meOrEmptyProvider);
  final spec = statutoryForm();
  return FormSpec(
    title: spec.title,
    intro: spec.intro,
    initial: {...spec.initial, 'legalEntityId': me.defaultEntity?.id},
    fields: [for (final f in spec.fields) f.key == 'legalEntityId' ? Field.select('legalEntityId', 'Legal entity', options: [for (final e in me.legalEntities) Option(e.id, e.name)], required: true, initial: me.defaultEntity?.id) : f],
    submit: spec.submit,
  );
}

class StatutoryRulesScreen extends StatelessWidget {
  const StatutoryRulesScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Statutory rules',
        path: _rulesPath,
        emptyText: 'No statutory rules yet.',
        create: statutoryCreateForm,
        createPermission: 'hr.payroll.manage',
        itemBuilder: (context, r) => Consumer(
          builder: (context, ref, _) => ListTile(
            title: Text('${r['code']} · ${r['name']}'),
            subtitle: Text('${r['kind'] == 'employerCost' ? 'Employer cost' : 'Employee deduction'} · ${r['method'] == 'slab' ? 'slabs' : '${r['rate']}% of ${r['base']}'}'),
            trailing: r['isActive'] == false ? StatusChip('Off', color: Colors.grey.shade700) : null,
            onTap: ref.read(meOrEmptyProvider).can('hr.payroll.manage')
                ? () async {
                    final full = await ref.read(apiProvider).get('$_rulesPath/${r['id']}') as Json;
                    if (context.mounted) await openForm<Object?>(context, statutoryForm(existing: full));
                  }
                : null,
          ),
        ),
      );
}

// ---- Daily attendance ------------------------------------------------------------------------------------------------------

const _dailyPath = '$hrPath/attendance/daily';
const dailyStatuses = [Option('present', 'Present'), Option('absent', 'Absent'), Option('leave', 'On leave'), Option('holiday', 'Public holiday'), Option('weeklyOff', 'Weekly off'), Option('halfDay', 'Half day')];

FormSpec dailyForm({Json? existing}) => FormSpec(
      title: existing == null ? 'Add a day' : 'Edit day',
      initial: {
        if (existing != null) ...{
          'employeeId': existing['employeeId'],
          'date': existing['date'],
          'status': existing['status'],
          'checkIn': '${existing['checkIn'] ?? ''}'.length >= 5 ? '${existing['checkIn']}'.substring(0, 5) : null,
          'checkOut': '${existing['checkOut'] ?? ''}'.length >= 5 ? '${existing['checkOut']}'.substring(0, 5) : null,
          'notes': existing['notes'],
        },
      },
      intro: 'Left blank, the day is present when there is a check-in, otherwise absent.',
      fields: [
        if (existing == null) Field.lookup('employeeId', 'Employee', path: '$hrPath/employees', labelKey: 'fullName', subtitleKey: 'number', required: true),
        if (existing == null) Field.date('date', 'Date', required: true, initial: 'today'),
        Field.select('status', 'Status', options: dailyStatuses),
        Field.text('checkIn', 'Check-in (hh:mm)', maxLength: 8, validator: _timeError, visibleWhen: (v) => v['status'] == null || v['status'] == 'present'),
        Field.text('checkOut', 'Check-out (hh:mm)', maxLength: 8, validator: _timeError, visibleWhen: (v) => v['status'] == null || v['status'] == 'present'),
        Field.text('notes', 'Notes', maxLength: 300),
      ],
      submit: (ref, body, raw) => ref.read(apiProvider).put(_dailyPath, body: {
        'employeeId': existing?['employeeId'] ?? body['employeeId'],
        'date': existing?['date'] ?? body['date'],
        'status': body['status'],
        'checkIn': timeOf(body['checkIn']),
        'checkOut': timeOf(body['checkOut']),
        'notes': body['notes'],
      }),
    );

class DailyAttendanceScreen extends ConsumerStatefulWidget {
  const DailyAttendanceScreen({super.key});

  @override
  ConsumerState<DailyAttendanceScreen> createState() => _DailyState();
}

class _DailyState extends ConsumerState<DailyAttendanceScreen> {
  DateTime _month = DateTime(DateTime.now().year, DateTime.now().month);
  late Future<List<Json>> _future = _load();

  Future<List<Json>> _load() async => _items(await ref.read(apiProvider).get(_dailyPath, query: {'year': _month.year, 'month': _month.month}));

  void _go(int delta) => setState(() {
        _month = DateTime(_month.year, _month.month + delta);
        _future = _load();
      });

  Future<void> _rollUp() async {
    try {
      final rows = _items(await ref.read(apiProvider).post('$_dailyPath/roll-up', body: {'year': _month.year, 'month': _month.month}));
      if (!mounted) return;
      ref.read(dataVersionProvider.notifier).bump();
      await showDialog<void>(
        context: context,
        builder: (c) => AlertDialog(
          title: const Text('Monthly attendance updated'),
          content: SizedBox(
            width: double.maxFinite,
            child: rows.isEmpty
                ? const Text('There are no daily records in this month yet.')
                : ListView(shrinkWrap: true, children: [
                    for (final r in rows) ListTile(dense: true, title: Text('${r['employeeName']}'), subtitle: Text('Present ${r['daysPresent']} · absent ${r['unpaidAbsentDays']} · overtime ${days(r['overtimeHours'])} h · late ${r['lateDays']} d')),
                  ]),
          ),
          actions: [TextButton(onPressed: () => Navigator.pop(c), child: const Text('Close'))],
        ),
      );
    } on ApiException catch (e) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.message)));
    }
  }

  @override
  Widget build(BuildContext context) {
    ref.watch(dataVersionProvider);
    final me = ref.watch(meOrEmptyProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Daily attendance'), actions: [
        if (me.can('hr.attendance.manage')) IconButton(tooltip: 'Roll up to the month', icon: const Icon(Icons.summarize_outlined), onPressed: _rollUp),
      ]),
      floatingActionButton: me.can('hr.attendance.manage')
          ? FloatingActionButton.extended(
              onPressed: () async {
                await openForm<Object?>(context, dailyForm());
                if (mounted) setState(() => _future = _load());
              },
              icon: const Icon(Icons.add),
              label: const Text('Add a day'))
          : null,
      body: Column(children: [
        Row(mainAxisAlignment: MainAxisAlignment.center, children: [
          IconButton(tooltip: 'Previous month', onPressed: () => _go(-1), icon: const Icon(Icons.chevron_left)),
          Text('${monthName(_month.month)} ${_month.year}', style: Theme.of(context).textTheme.titleMedium),
          IconButton(tooltip: 'Next month', onPressed: () => _go(1), icon: const Icon(Icons.chevron_right)),
        ]),
        Expanded(
          child: FutureBuilder<List<Json>>(
            future: _future,
            builder: (context, snap) {
              if (snap.connectionState != ConnectionState.done) return const ErpSkeleton.list();
              if (snap.hasError) return Center(child: Text('${snap.error}'));
              final rows = snap.data ?? const [];
              if (rows.isEmpty) return const Center(child: Text('No days recorded for this month.'));
              return ListView.separated(
                padding: const EdgeInsets.only(bottom: 88),
                itemCount: rows.length,
                separatorBuilder: (_, _) => const Divider(height: 1),
                itemBuilder: (context, i) {
                  final r = rows[i];
                  final status = '${r['status']}';
                  final worked = (r['workedMinutes'] as num?)?.toInt() ?? 0;
                  return ListTile(
                    title: NameText(employeeNames, r['employeeId']),
                    subtitle: Text('${formatDate(r['date'])}${r['checkIn'] == null ? '' : ' · ${'${r['checkIn']}'.substring(0, 5)} - ${r['checkOut'] == null ? '?' : '${r['checkOut']}'.substring(0, 5)}'}${worked > 0 ? ' · ${worked ~/ 60}:${(worked % 60).toString().padLeft(2, '0')} h' : ''}'),
                    trailing: Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.end, children: [
                      StatusChip(dailyStatuses.where((o) => o.value == status).map((o) => o.label).firstOrNull ?? humanize(status), color: status == 'present' ? Colors.green.shade700 : status == 'absent' ? Colors.red.shade700 : Colors.blueGrey),
                      if (((r['lateMinutes'] as num?) ?? 0) > 0) Text('late ${r['lateMinutes']} min', style: Theme.of(context).textTheme.bodySmall),
                    ]),
                    onTap: me.can('hr.attendance.manage')
                        ? () async {
                            await openForm<Object?>(context, dailyForm(existing: r));
                            if (mounted) setState(() => _future = _load());
                          }
                        : null,
                  );
                },
              );
            },
          ),
        ),
      ]),
    );
  }
}

// ---- Employee history and tasks --------------------------------------------------------------------------------------------

const historyKinds = {'joined': 'Joined', 'salary': 'Pay', 'designation': 'Designation', 'department': 'Department', 'position': 'Position', 'manager': 'Manager', 'contract': 'Contract', 'status': 'Status', 'terminated': 'Left', 'note': 'Note'};

class EmployeeHistoryScreen extends ConsumerWidget {
  const EmployeeHistoryScreen({super.key, required this.employeeId, required this.name});

  final String employeeId;
  final String name;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    ref.watch(dataVersionProvider);
    final canNote = ref.watch(meOrEmptyProvider).can('hr.employees.manage');
    return Scaffold(
      appBar: AppBar(title: Text('History · $name')),
      floatingActionButton: canNote
          ? FloatingActionButton.extended(
              onPressed: () => openForm<Object?>(
                context,
                FormSpec(
                  title: 'Add a note',
                  fields: [Field.multiline('summary', 'Note', required: true, maxLength: 500)],
                  submit: (ref, body, raw) => ref.read(apiProvider).post('$hrPath/employees/$employeeId/history', body: body),
                ),
              ),
              icon: const Icon(Icons.note_add_outlined),
              label: const Text('Add a note'))
          : null,
      body: FutureBuilder<dynamic>(
        future: ref.read(apiProvider).get('$hrPath/employees/$employeeId/history'),
        builder: (context, snap) {
          if (snap.connectionState != ConnectionState.done) return const ErpSkeleton.list();
          if (snap.hasError) return Center(child: Text('${snap.error}'));
          final rows = _items(snap.data);
          if (rows.isEmpty) return const Center(child: Text('Nothing recorded yet.'));
          return ListView.separated(
            padding: const EdgeInsets.only(bottom: 88),
            itemCount: rows.length,
            separatorBuilder: (_, _) => const Divider(height: 1),
            itemBuilder: (context, i) {
              final h = rows[i];
              final change = h['oldValue'] != null || h['newValue'] != null ? '${h['oldValue'] ?? '—'} → ${h['newValue'] ?? '—'}' : (h['isPay'] == true ? 'Pay details hidden' : '');
              return ListTile(
                title: Text('${h['summary']}'),
                subtitle: Text('${historyKinds['${h['kind']}'] ?? humanize('${h['kind']}')} · ${formatDate(h['effectiveDate'])}${change.isEmpty ? '' : '\n$change'}'),
                isThreeLine: change.isNotEmpty,
              );
            },
          );
        },
      ),
    );
  }
}

FormSpec taskForm(String employeeId) => FormSpec(
      title: 'Add a task',
      fields: [Field.text('title', 'Task', required: true, maxLength: 200), Field.date('dueDate', 'Due date'), Field.select('kind', 'Kind', options: const [Option('onboarding', 'Onboarding'), Option('offboarding', 'Offboarding')], initial: 'onboarding')],
      submit: (ref, body, raw) => ref.read(apiProvider).post('$hrPath/employees/$employeeId/tasks', body: body),
    );

FormSpec applyChecklistForm(String employeeId) => FormSpec(
      title: 'Start a checklist',
      submitLabel: 'Apply',
      fields: [Field.lookup('templateId', 'Checklist', path: '$hrPath/checklists', labelKey: 'name', required: true)],
      submit: (ref, body, raw) => ref.read(apiProvider).post('$hrPath/employees/$employeeId/checklists/${body['templateId']}'),
    );

class EmployeeTasksScreen extends ConsumerWidget {
  const EmployeeTasksScreen({super.key, required this.employeeId, required this.name});

  final String employeeId;
  final String name;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    ref.watch(dataVersionProvider);
    final canManage = ref.watch(meOrEmptyProvider).can('hr.employees.manage');
    return Scaffold(
      appBar: AppBar(title: Text('Tasks · $name'), actions: [
        if (canManage) IconButton(tooltip: 'Start a checklist', icon: const Icon(Icons.checklist), onPressed: () => openForm<Object?>(context, applyChecklistForm(employeeId))),
      ]),
      floatingActionButton: canManage ? FloatingActionButton.extended(onPressed: () => openForm<Object?>(context, taskForm(employeeId)), icon: const Icon(Icons.add_task), label: const Text('Add a task')) : null,
      body: FutureBuilder<dynamic>(
        future: ref.read(apiProvider).get('$hrPath/employees/$employeeId/tasks'),
        builder: (context, snap) {
          if (snap.connectionState != ConnectionState.done) return const ErpSkeleton.list();
          if (snap.hasError) return Center(child: Text('${snap.error}'));
          final rows = _items(snap.data);
          if (rows.isEmpty) return const Center(child: Text('No tasks.'));
          return ListView(padding: const EdgeInsets.only(bottom: 88), children: [for (final t in rows) _TaskTile(t, canManage)]);
        },
      ),
    );
  }
}

class _TaskTile extends ConsumerWidget {
  const _TaskTile(this.t, this.canManage);

  final Json t;
  final bool canManage;

  @override
  Widget build(BuildContext context, WidgetRef ref) => CheckboxListTile(
        value: t['isDone'] == true,
        onChanged: canManage
            ? (v) async {
                await ref.read(apiProvider).post('$hrPath/tasks/${t['id']}/done', body: {'done': v == true});
                ref.read(dataVersionProvider.notifier).bump();
              }
            : null,
        title: Text('${t['title']}', style: TextStyle(decoration: t['isDone'] == true ? TextDecoration.lineThrough : null)),
        subtitle: t['dueDate'] == null ? null : Text('Due ${formatDate(t['dueDate'])}', style: TextStyle(color: t['overdue'] == true ? Colors.red.shade700 : null)),
      );
}

/// Every unfinished onboarding or offboarding step, overdue first.
class OpenTasksScreen extends ConsumerWidget {
  const OpenTasksScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    ref.watch(dataVersionProvider);
    final canManage = ref.watch(meOrEmptyProvider).can('hr.employees.manage');
    return Scaffold(
      appBar: AppBar(title: const Text('HR tasks')),
      body: FutureBuilder<dynamic>(
        future: ref.read(apiProvider).get('$hrPath/tasks/open'),
        builder: (context, snap) {
          if (snap.connectionState != ConnectionState.done) return const ErpSkeleton.list();
          if (snap.hasError) return Center(child: Text('${snap.error}'));
          final rows = _items(snap.data);
          if (rows.isEmpty) return const Center(child: Text('Nothing is waiting.'));
          return ListView.separated(
            itemCount: rows.length,
            separatorBuilder: (_, _) => const Divider(height: 1),
            itemBuilder: (context, i) {
              final t = rows[i];
              return ListTile(
                title: Text('${t['title']}'),
                subtitle: Text('${t['employeeName']}${t['dueDate'] == null ? '' : ' · due ${formatDate(t['dueDate'])}'}'),
                leading: Icon(t['overdue'] == true ? Icons.warning_amber : Icons.radio_button_unchecked, color: t['overdue'] == true ? Colors.red.shade700 : null),
                trailing: canManage
                    ? TextButton(
                        onPressed: () async {
                          await ref.read(apiProvider).post('$hrPath/tasks/${t['id']}/done', body: const {'done': true});
                          ref.read(dataVersionProvider.notifier).bump();
                        },
                        child: const Text('Done'))
                    : null,
              );
            },
          );
        },
      ),
    );
  }
}

// ---- Checklists ------------------------------------------------------------------------------------------------------------

const _checklistsPath = '$hrPath/checklists';

/// One step per line, "title | days" -> [{title, dueOffsetDays}].
List<Map<String, Object>> checklistItemsOf(Object? text) => [
      for (final line in '${text ?? ''}'.split('\n').map((l) => l.trim()).where((l) => l.isNotEmpty))
        if (line.split('|').first.trim().isNotEmpty) {'title': line.split('|').first.trim(), 'dueOffsetDays': line.contains('|') ? (int.tryParse(line.split('|')[1].trim()) ?? 0) : 0},
    ];

FormSpec checklistForm({Json? existing}) => FormSpec(
      title: existing == null ? 'New checklist' : 'Edit checklist',
      initial: {
        if (existing != null) ...{
          'name': existing['name'],
          'kind': existing['kind'],
          'isActive': existing['isActive'],
          'itemsText': [for (final i in (existing['items'] as List<dynamic>? ?? const []).cast<Json>()) '${i['title']} | ${i['dueOffsetDays']}'].join('\n'),
        },
      },
      intro: 'One step per line: title | days. Days count from the join date (onboarding) or back from the last day (offboarding, negative).',
      fields: [
        Field.text('name', 'Name', required: true, maxLength: 150),
        Field.select('kind', 'Kind', options: const [Option('onboarding', 'Onboarding'), Option('offboarding', 'Offboarding')], required: true, initial: 'onboarding'),
        Field.multiline('itemsText', 'Steps', maxLength: 4000),
        Field.toggle('isActive', 'Active', initial: true),
      ],
      submit: (ref, body, raw) {
        final api = ref.read(apiProvider);
        final full = {...body}..remove('itemsText');
        full['items'] = checklistItemsOf(body['itemsText']);
        return existing == null ? api.post(_checklistsPath, body: full) : api.put('$_checklistsPath/${existing['id']}', body: full);
      },
    );

class ChecklistsScreen extends StatelessWidget {
  const ChecklistsScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Checklists',
        path: _checklistsPath,
        emptyText: 'No checklists yet.',
        create: (ref) => checklistForm(),
        createPermission: 'hr.employees.manage',
        itemBuilder: (context, c) => Consumer(
          builder: (context, ref, _) => ListTile(
            title: Text('${c['name']}'),
            subtitle: Text(humanize('${c['kind']}')),
            trailing: c['isActive'] == false ? StatusChip('Off', color: Colors.grey.shade700) : null,
            onTap: ref.read(meOrEmptyProvider).can('hr.employees.manage')
                ? () async {
                    final full = await ref.read(apiProvider).get('$_checklistsPath/${c['id']}') as Json;
                    if (context.mounted) await openForm<Object?>(context, checklistForm(existing: full));
                  }
                : null,
          ),
        ),
      );
}

// ---- Recruitment -----------------------------------------------------------------------------------------------------------

const _openingsPath = '$hrPath/openings';
const _candidatesPath = '$hrPath/candidates';
const candidateStages = [Option('applied', 'Applied'), Option('screening', 'Screening'), Option('interview', 'Interview'), Option('offer', 'Offer'), Option('hired', 'Hired'), Option('rejected', 'Rejected')];
const _nextStage = {'applied': 'screening', 'screening': 'interview', 'interview': 'offer'};

String stageLabel(String s) => candidateStages.where((o) => o.value == s).map((o) => o.label).firstOrNull ?? humanize(s);
Color stageColor(String s) => switch (s) { 'hired' => Colors.green.shade700, 'rejected' => Colors.red.shade700, 'offer' => Colors.orange.shade800, _ => Colors.blue.shade700 };

FormSpec openingForm({Json? existing}) => FormSpec(
      title: existing == null ? 'New vacancy' : 'Edit vacancy',
      initial: {if (existing != null) for (final k in ['title', 'headcount', 'status', 'closingDate', 'description']) k: existing[k]},
      fields: [
        Field.text('title', 'Title', required: true, maxLength: 150),
        Field.integer('headcount', 'Positions to fill', required: true, min: 1, max: 500, initial: '1'),
        Field.select('status', 'Status', options: const [Option('open', 'Open'), Option('onHold', 'On hold'), Option('closed', 'Closed')], initial: 'open'),
        Field.date('closingDate', 'Closing date'),
        Field.multiline('description', 'Description', maxLength: 4000),
      ],
      submit: (ref, body, raw) {
        final api = ref.read(apiProvider);
        return existing == null ? api.post(_openingsPath, body: body) : api.put('$_openingsPath/${existing['id']}', body: body);
      },
    );

FormSpec candidateForm({Json? existing}) => FormSpec(
      title: existing == null ? 'New candidate' : 'Edit candidate',
      initial: {
        if (existing != null) ...{'openingId': existing['openingId'], for (final k in ['fullName', 'email', 'phone', 'source', 'expectedSalary', 'notes']) k: existing[k]},
      },
      fields: [
        if (existing == null) Field.lookup('openingId', 'Vacancy', path: _openingsPath, labelKey: 'title', required: true),
        Field.text('fullName', 'Full name', required: true, maxLength: 200),
        Field.text('email', 'Email', maxLength: 200, keyboard: TextInputType.emailAddress),
        Field.text('phone', 'Phone', maxLength: 50, keyboard: TextInputType.phone),
        Field.text('source', 'Source', maxLength: 100),
        Field.money('expectedSalary', 'Expected salary'),
        Field.multiline('notes', 'Notes', maxLength: 2000),
      ],
      submit: (ref, body, raw) {
        final api = ref.read(apiProvider);
        final full = {...body, 'openingId': existing?['openingId'] ?? body['openingId']};
        return existing == null ? api.post(_candidatesPath, body: full) : api.put('$_candidatesPath/${existing['id']}', body: full);
      },
    );

FormSpec hireForm(Json candidate, WidgetRef ref) {
  final me = ref.read(meOrEmptyProvider);
  return FormSpec(
    title: 'Hire ${candidate['fullName']}',
    submitLabel: 'Hire',
    initial: {'legalEntityId': me.defaultEntity?.id, 'basicSalary': candidate['expectedSalary']},
    fields: [
      Field.select('legalEntityId', 'Legal entity', options: [for (final e in me.legalEntities) Option(e.id, e.name)], required: true, initial: me.defaultEntity?.id),
      Field.date('joinDate', 'Join date', required: true, initial: 'today'),
      Field.text('designation', 'Designation', required: true, maxLength: 150),
      Field.money('basicSalary', 'Basic salary (monthly)', required: true, min: 0.01),
      Field.money('housingAllowance', 'Housing allowance', initial: '0'),
      Field.money('transportAllowance', 'Transport allowance', initial: '0'),
      Field.text('nationality', 'Nationality', maxLength: 100),
      Field.lookup('onboardingTemplateId', 'Start the onboarding checklist', path: _checklistsPath, query: const {'filter': 'kind:eq:onboarding'}, labelKey: 'name'),
    ],
    submit: (ref, body, raw) => ref.read(apiProvider).post('$_candidatesPath/${candidate['id']}/hire', body: body),
  );
}

class CandidateDetailScreen extends StatelessWidget {
  const CandidateDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) => Consumer(builder: (context, ref, _) {
        final path = '$_candidatesPath/$id';
        return DetailPage(
          path: path,
          titleOf: (d) => '${d['fullName']}',
          canEdit: (d) => d['stage'] != 'hired' && ref.read(meOrEmptyProvider).can('hr.recruitment.manage'),
          edit: (d) => candidateForm(existing: d),
          body: (context, d) => DetailBody(children: [
            DetailHeader(title: '${d['fullName']}', subtitle: text(d['email'])),
            Align(alignment: Alignment.centerLeft, child: Padding(padding: const EdgeInsets.only(bottom: ErpSpace.s3), child: StatusChip(stageLabel('${d['stage']}'), color: stageColor('${d['stage']}')))),
            InfoCard(rows: [
              InfoRow('Phone', text(d['phone'])),
              InfoRow('Source', text(d['source'])),
              InfoRow('Expected salary', d['expectedSalary'] == null ? null : formatMoney(d['expectedSalary'] as num)),
              InfoRow('Notes', text(d['notes'])),
            ]),
          ]),
          actions: [
            ActionSpec(
              label: 'Next stage',
              icon: Icons.arrow_forward,
              permission: 'hr.recruitment.manage',
              visible: (d) => _nextStage.containsKey(d['stage']),
              confirm: 'Move this candidate to the next stage?',
              successMessage: 'Moved',
              run: (ref, d, _) => ref.read(apiProvider).post('$path/move', body: {'stage': _nextStage[d['stage']]}),
            ),
            ActionSpec(
              label: 'Hire',
              icon: Icons.how_to_reg_outlined,
              permission: 'hr.recruitment.manage',
              visible: (d) => d['stage'] == 'offer',
              form: (d) => hireForm(d, ref),
              run: (ref, d, _) async => null,
            ),
            ActionSpec(
              label: 'Reject',
              icon: Icons.cancel_outlined,
              permission: 'hr.recruitment.manage',
              destructive: true,
              visible: (d) => d['stage'] != 'hired' && d['stage'] != 'rejected',
              confirm: 'Reject this candidate?',
              successMessage: 'Rejected',
              run: (ref, d, _) => ref.read(apiProvider).post('$path/move', body: const {'stage': 'rejected'}),
            ),
          ],
        );
      });
}

/// Vacancies and candidates.
class RecruitmentScreen extends StatelessWidget {
  const RecruitmentScreen({super.key});

  @override
  Widget build(BuildContext context) => DefaultTabController(
        length: 2,
        child: Scaffold(
          appBar: AppBar(title: const Text('Recruitment'), bottom: const TabBar(tabs: [Tab(text: 'Vacancies'), Tab(text: 'Candidates')])),
          body: TabBarView(children: [
            ListScreen(
              title: 'Vacancies',
              showAppBar: false,
              path: _openingsPath,
              emptyText: 'No vacancies yet.',
              filters: const [Option('open', 'Open'), Option('onHold', 'On hold'), Option('closed', 'Closed')],
              create: (ref) => openingForm(),
              createPermission: 'hr.recruitment.manage',
              itemBuilder: (context, o) => Consumer(
                builder: (context, ref, _) => ListTile(
                  title: Text('${o['title']}'),
                  subtitle: Text('${o['headcount']} position(s)${o['closingDate'] == null ? '' : ' · closes ${formatDate(o['closingDate'])}'}'),
                  trailing: StatusChip(humanize('${o['status']}'), color: o['status'] == 'open' ? Colors.green.shade700 : Colors.grey.shade700),
                  onTap: ref.read(meOrEmptyProvider).can('hr.recruitment.manage')
                      ? () async {
                          final full = await ref.read(apiProvider).get('$_openingsPath/${o['id']}') as Json;
                          if (context.mounted) await openForm<Object?>(context, openingForm(existing: full));
                        }
                      : null,
                ),
              ),
            ),
            ListScreen(
              title: 'Candidates',
              showAppBar: false,
              path: _candidatesPath,
              emptyText: 'No candidates yet.',
              filterKey: 'stage',
              filters: candidateStages,
              create: (ref) => candidateForm(),
              createPermission: 'hr.recruitment.manage',
              itemBuilder: (context, c) => ListTile(
                title: Text('${c['fullName']}'),
                subtitle: Text(text(c['email']) ?? ''),
                trailing: StatusChip(stageLabel('${c['stage']}'), color: stageColor('${c['stage']}')),
                onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => CandidateDetailScreen(id: '${c['id']}'))),
              ),
            ),
          ]),
        ),
      );
}
