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
import '../../kit/widgets.dart';
import '../common/paged_list_view.dart';

const pBase = '/api/v1/m/projects';

const projectStatuses = [Option('planning', 'Planning'), Option('active', 'Active'), Option('onHold', 'On hold'), Option('completed', 'Completed'), Option('cancelled', 'Cancelled')];
const billingTypes = [Option('timeAndMaterials', 'Time & materials'), Option('fixedFee', 'Fixed fee'), Option('nonBillable', 'Non-billable')];
const taskStatuses = [Option('todo', 'To do'), Option('inProgress', 'In progress'), Option('review', 'Review'), Option('done', 'Done')];
const taskPriorities = [Option('low', 'Low'), Option('medium', 'Medium'), Option('high', 'High'), Option('urgent', 'Urgent')];
const milestoneStatuses = [Option('pending', 'Pending'), Option('achieved', 'Achieved'), Option('invoiced', 'Invoiced')];

/// Allowed next statuses, mirroring ProjectsTransitions on the backend.
const projectMoves = <String, List<String>>{
  'planning': ['active', 'cancelled'],
  'active': ['onHold', 'completed', 'cancelled'],
  'onHold': ['active', 'cancelled'],
  'completed': <String>[],
  'cancelled': <String>[],
};
const taskMoves = <String, List<String>>{
  'todo': ['inProgress'],
  'inProgress': ['review', 'todo'],
  'review': ['done', 'inProgress'],
  'done': ['inProgress'],
};

String label(List<Option> options, Object? value) {
  for (final o in options) {
    if (o.value == value) return o.label;
  }
  return humanize(value?.toString());
}

Color statusColor(String s) => switch (s) {
      'active' || 'inProgress' || 'achieved' => Colors.blue.shade700,
      'completed' || 'done' || 'invoiced' => Colors.green.shade700,
      'cancelled' || 'urgent' => Colors.red.shade700,
      'onHold' || 'review' || 'high' => Colors.orange.shade800,
      _ => Colors.blueGrey,
    };

Widget chip(List<Option> options, Object? v) => StatusChip(label(options, v), color: statusColor('$v'));

List<Json> items(Object? r) => ((r is Map ? r['items'] : r) as List<dynamic>? ?? const []).cast<Json>();

Route<T> _route<T>(Widget w) => MaterialPageRoute<T>(builder: (_) => w);

List<Option> _entities(Me me) => [for (final e in me.legalEntities) Option(e.id, e.name)];

// ---------------------------------------------------------------- projects

FormSpec projectForm(WidgetRef ref, {Json? existing}) {
  final me = ref.read(meOrEmptyProvider);
  final editing = existing != null;
  return FormSpec(
    title: editing ? 'Edit ${existing['number']}' : 'New project',
    initial: {
      if (editing) ...{
        'legalEntityId': existing['legalEntityId'],
        'number': existing['number'],
        'name': existing['name'],
        'description': existing['description'],
        'customerId': existing['customerId'],
        '__label_customerId': existing['customerName'],
        'customerName': existing['customerName'],
        'managerName': existing['managerName'],
        'status': existing['status'],
        'billingType': existing['billingType'],
        'budgetAmount': existing['budgetAmount']?.toString(),
        'startDate': existing['startDate'],
        'endDate': existing['endDate'],
      },
    },
    fields: [
      if (!editing) Field.select('legalEntityId', 'Legal entity', options: _entities(me), required: true, initial: me.defaultEntity?.id),
      Field.text('name', 'Name', required: true, maxLength: 200),
      Field.lookup('customerId', 'Customer (needed to bill)', path: '/api/v1/parties', query: const {'role': 'Customer'}, subtitleKey: 'code', copy: const {'customerName': 'name'}),
      Field.text('managerName', 'Project manager', maxLength: 150),
      Field.select('billingType', 'Billing', options: billingTypes, required: true, initial: 'timeAndMaterials'),
      Field.money('budgetAmount', 'Budget', initial: '0'),
      Field.date('startDate', 'Start date'),
      Field.date('endDate', 'End date', validator: (v, all) => v != null && all['startDate'] != null && '$v'.compareTo('${all['startDate']}') < 0 ? 'End date is before the start date' : null),
      if (!editing) Field.select('status', 'Status', options: projectStatuses.take(2).toList(), required: true, initial: 'planning'),
      Field.multiline('description', 'Description', maxLength: 2000),
    ],
    submit: (ref, body, raw) {
      final api = ref.read(apiProvider);
      if (!editing) return api.post('$pBase/projects', body: {...body, if (raw['customerName'] != null) 'customerName': raw['customerName']});
      return api.put('$pBase/projects/${existing['id']}', body: {
        ...body,
        'legalEntityId': existing['legalEntityId'],
        'status': existing['status'],
        'customerName': raw['customerName'] ?? existing['customerName'],
      });
    },
  );
}

Widget projectTile(BuildContext context, Json p) {
  final status = '${p['status']}';
  return ListTile(
    onTap: () => Navigator.of(context).push(_route(ProjectDetailScreen(id: '${p['id']}'))),
    title: Text('${p['name']}', maxLines: 1, overflow: TextOverflow.ellipsis),
    subtitle: Text('${p['number']} · ${p['customerName'] ?? 'No customer'}'),
    trailing: Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.end, children: [
      Text(label(billingTypes, p['billingType']), style: const TextStyle(fontSize: 12)),
      const SizedBox(height: 4),
      chip(projectStatuses, status),
    ]),
  );
}

class ProjectsScreen extends StatelessWidget {
  const ProjectsScreen({super.key});
  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Projects',
        path: '$pBase/projects',
        searchHint: 'Search projects',
        emptyText: 'No projects found.',
        itemBuilder: projectTile,
        filters: projectStatuses,
        create: (ref) => projectForm(ref),
        createPermission: 'projects.projects.manage',
      );
}

ActionSpec _projectMove(String target, String text, IconData icon, {bool destructive = false, String? confirm}) => ActionSpec(
      label: text,
      icon: icon,
      permission: 'projects.projects.manage',
      destructive: destructive,
      confirm: confirm,
      visible: (d) => (projectMoves[d['status']] ?? const []).contains(target),
      successMessage: 'Project ${label(projectStatuses, target).toLowerCase()}',
      run: (ref, d, _) => ref.read(apiProvider).put('$pBase/projects/${d['id']}/status', body: {'status': target}),
    );

class ProjectDetailScreen extends StatelessWidget {
  const ProjectDetailScreen({super.key, required this.id});
  final String id;

  @override
  Widget build(BuildContext context) => Consumer(builder: (context, ref, _) {
        return DetailPage(
          path: '$pBase/projects/$id',
          titleOf: (d) => '${d['number']}',
          canEdit: (d) => ref.read(meOrEmptyProvider).can('projects.projects.manage') && !const {'completed', 'cancelled'}.contains(d['status']),
          edit: (d) => projectForm(ref, existing: d),
          body: (context, d) => DetailBody(children: [
            DetailHeader(title: '${d['name']}', subtitle: d['customerName'] == null ? 'No customer assigned' : '${d['customerName']}', status: null),
            Align(alignment: Alignment.centerLeft, child: chip(projectStatuses, d['status'])),
            const SizedBox(height: 8),
            InfoCard(rows: [
              InfoRow('Billing', label(billingTypes, d['billingType'])),
              InfoRow('Manager', text(d['managerName'])),
              InfoRow('Budget', formatMoney((d['budgetAmount'] as num?) ?? 0), bold: true),
              InfoRow('Start', d['startDate'] == null ? null : formatDate(d['startDate'])),
              InfoRow('End', d['endDate'] == null ? null : formatDate(d['endDate'])),
              InfoRow('Hours logged', '${d['totalLoggedHours'] ?? 0}'),
              InfoRow('Hours billed', '${d['totalBilledHours'] ?? 0}'),
              InfoRow('Timesheet cost', formatMoney((d['totalTimesheetCost'] as num?) ?? 0)),
              InfoRow('Tasks', '${d['completedTasks'] ?? 0} / ${d['totalTasks'] ?? 0} done'),
              InfoRow('Milestones', '${d['achievedMilestones'] ?? 0} / ${d['totalMilestones'] ?? 0} achieved'),
            ]),
            if ((d['description'] ?? '').toString().isNotEmpty) ...[Text(d['description'].toString()), const SizedBox(height: 12)],
            _Related(title: 'Tasks', path: '$pBase/tasks', projectId: id, tile: taskTile),
            _Related(title: 'Milestones', path: '$pBase/milestones', projectId: id, tile: milestoneTile),
            _Related(title: 'Time entries', path: '$pBase/timesheets', projectId: id, tile: timesheetTile),
          ]),
          actions: [
            _projectMove('active', 'Activate', Icons.play_arrow),
            _projectMove('onHold', 'Put on hold', Icons.pause),
            _projectMove('completed', 'Complete', Icons.check_circle_outline, confirm: 'Complete this project? It cannot be reopened.'),
            _projectMove('cancelled', 'Cancel', Icons.block, destructive: true, confirm: 'Cancel this project? It cannot be reopened.'),
            ActionSpec(
              label: 'Log time',
              icon: Icons.timer_outlined,
              permission: 'projects.timesheets.manage',
              visible: (d) => !const {'completed', 'cancelled'}.contains(d['status']),
              run: (ref, d, _) async => null,
              form: (d) => timesheetForm(ref, projectId: id, projectName: '${d['name']}'),
            ),
            ActionSpec(
              label: 'Add task',
              icon: Icons.add_task,
              permission: 'projects.tasks.manage',
              visible: (d) => !const {'completed', 'cancelled'}.contains(d['status']),
              run: (ref, d, _) async => null,
              form: (d) => taskForm(ref, projectId: id, projectName: '${d['name']}'),
            ),
            ActionSpec(
              label: 'Add milestone',
              icon: Icons.flag_outlined,
              permission: 'projects.milestones.manage',
              visible: (d) => !const {'completed', 'cancelled'}.contains(d['status']),
              run: (ref, d, _) async => null,
              form: (d) => milestoneForm(ref, projectId: id, projectName: '${d['name']}'),
            ),
            ActionSpec(
              label: 'Delete',
              icon: Icons.delete_outline,
              permission: 'projects.projects.manage',
              destructive: true,
              confirm: 'Delete this project?',
              successMessage: 'Deleted',
              run: (ref, d, _) async {
                await ref.read(apiProvider).delete('$pBase/projects/$id');
                if (context.mounted) Navigator.of(context).pop();
                return null;
              },
            ),
          ],
        );
      });
}

/// Records of a project (tasks, milestones, time) listed inside its detail screen.
class _Related extends ConsumerStatefulWidget {
  const _Related({required this.title, required this.path, required this.projectId, required this.tile});
  final String title;
  final String path;
  final String projectId;
  final Widget Function(BuildContext, Json) tile;

  @override
  ConsumerState<_Related> createState() => _RelatedState();
}

class _RelatedState extends ConsumerState<_Related> {
  List<Json>? _items;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final r = await ref.read(apiProvider).get(widget.path, query: {'filter': 'projectId:eq:${widget.projectId}', 'pageSize': 20});
      if (mounted) setState(() => _items = items(r));
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    }
  }

  @override
  Widget build(BuildContext context) {
    ref.listen(dataVersionProvider, (_, _) => _load());
    final list = _items;
    if (list != null && list.isEmpty) return const SizedBox.shrink();
    return Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
      Text(widget.title, style: Theme.of(context).textTheme.titleMedium),
      if (_error != null) Text(_error!) else if (list == null) const LinearProgressIndicator(),
      for (final i in list ?? const <Json>[]) widget.tile(context, i),
      const SizedBox(height: 8),
    ]);
  }
}

// ---------------------------------------------------------------- tasks

FormSpec taskForm(WidgetRef ref, {Json? existing, String? projectId, String? projectName}) {
  final editing = existing != null;
  return FormSpec(
    title: editing ? 'Edit ${existing['number']}' : 'New task',
    initial: {
      if (editing) ...{
        'projectId': existing['projectId'],
        '__label_projectId': existing['projectName'],
        'milestoneId': existing['milestoneId'],
        '__label_milestoneId': existing['milestoneTitle'],
        'title': existing['title'],
        'description': existing['description'],
        'priority': existing['priority'],
        'assignedTo': existing['assignedTo'],
        'estimatedHours': existing['estimatedHours']?.toString(),
        'actualHours': existing['actualHours']?.toString(),
        'dueDate': existing['dueDate'],
      } else if (projectId != null) ...{'projectId': projectId, '__label_projectId': projectName},
    },
    fields: [
      Field.lookup('projectId', 'Project', path: '$pBase/projects', subtitleKey: 'number', required: true),
      Field.text('title', 'Title', required: true, maxLength: 200),
      Field.select('priority', 'Priority', options: taskPriorities, required: true, initial: 'medium'),
      Field.text('assignedTo', 'Assigned to', maxLength: 100),
      Field.date('dueDate', 'Due date'),
      Field.decimal('estimatedHours', 'Estimated hours', min: 0, initial: '0'),
      if (editing) Field.decimal('actualHours', 'Actual hours', min: 0, initial: '0'),
      Field.multiline('description', 'Description', maxLength: 2000),
    ],
    submit: (ref, body, raw) {
      final api = ref.read(apiProvider);
      final full = {'estimatedHours': 0, 'actualHours': 0, ...body, 'status': editing ? existing['status'] : 'todo'};
      return editing ? api.put('$pBase/tasks/${existing['id']}', body: full) : api.post('$pBase/tasks', body: full);
    },
  );
}

Widget taskTile(BuildContext context, Json t) => ListTile(
      onTap: () => Navigator.of(context).push(_route(TaskDetailScreen(id: '${t['id']}'))),
      title: Text('${t['title']}', maxLines: 1, overflow: TextOverflow.ellipsis),
      subtitle: Text('${t['number']} · ${label(taskPriorities, t['priority'])}${t['dueDate'] == null ? '' : ' · due ${formatDate(t['dueDate'])}'}${(t['assignedTo'] ?? '').toString().isEmpty ? '' : '\n${t['assignedTo']}'}'),
      isThreeLine: (t['assignedTo'] ?? '').toString().isNotEmpty,
      trailing: chip(taskStatuses, t['status']),
    );

class TasksScreen extends StatelessWidget {
  const TasksScreen({super.key});
  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Tasks',
        path: '$pBase/tasks',
        searchHint: 'Search tasks',
        emptyText: 'No tasks found.',
        itemBuilder: taskTile,
        filters: taskStatuses,
        create: (ref) => taskForm(ref),
        createPermission: 'projects.tasks.manage',
      );
}

ActionSpec _taskMove(String target, String text, IconData icon) => ActionSpec(
      label: text,
      icon: icon,
      permission: 'projects.tasks.manage',
      visible: (d) => (taskMoves[d['status']] ?? const []).contains(target),
      successMessage: 'Task ${label(taskStatuses, target).toLowerCase()}',
      run: (ref, d, _) => ref.read(apiProvider).put('$pBase/tasks/${d['id']}/status', body: {'status': target}),
    );

class TaskDetailScreen extends StatelessWidget {
  const TaskDetailScreen({super.key, required this.id});
  final String id;

  @override
  Widget build(BuildContext context) => Consumer(builder: (context, ref, _) {
        return DetailPage(
          path: '$pBase/tasks/$id',
          titleOf: (d) => '${d['number']}',
          canEdit: (d) => ref.read(meOrEmptyProvider).can('projects.tasks.manage'),
          edit: (d) => taskForm(ref, existing: d),
          body: (context, d) => DetailBody(children: [
            DetailHeader(title: '${d['title']}', subtitle: '${d['projectName']}'),
            Align(alignment: Alignment.centerLeft, child: chip(taskStatuses, d['status'])),
            const SizedBox(height: 8),
            InfoCard(rows: [
              InfoRow('Priority', label(taskPriorities, d['priority'])),
              InfoRow('Assigned to', text(d['assignedTo'])),
              InfoRow('Milestone', text(d['milestoneTitle'])),
              InfoRow('Due', d['dueDate'] == null ? null : formatDate(d['dueDate'])),
              InfoRow('Estimated hours', '${d['estimatedHours'] ?? 0}'),
              InfoRow('Actual hours', '${d['actualHours'] ?? 0}'),
            ]),
            if ((d['description'] ?? '').toString().isNotEmpty) Text(d['description'].toString()),
          ]),
          actions: [
            _taskMove('inProgress', 'Start', Icons.play_arrow),
            _taskMove('review', 'Send to review', Icons.rate_review_outlined),
            _taskMove('done', 'Done', Icons.check_circle_outline),
            _taskMove('todo', 'Back to to do', Icons.undo),
            ActionSpec(
              label: 'Log time',
              icon: Icons.timer_outlined,
              permission: 'projects.timesheets.manage',
              run: (ref, d, _) async => null,
              form: (d) => timesheetForm(ref, projectId: '${d['projectId']}', projectName: '${d['projectName']}', taskId: id, taskTitle: '${d['title']}'),
            ),
            ActionSpec(
              label: 'Delete',
              icon: Icons.delete_outline,
              permission: 'projects.tasks.manage',
              destructive: true,
              confirm: 'Delete this task?',
              successMessage: 'Deleted',
              run: (ref, d, _) async {
                await ref.read(apiProvider).delete('$pBase/tasks/$id');
                if (context.mounted) Navigator.of(context).pop();
                return null;
              },
            ),
          ],
        );
      });
}

// ---------------------------------------------------------------- milestones

FormSpec milestoneForm(WidgetRef ref, {Json? existing, String? projectId, String? projectName}) {
  final editing = existing != null;
  return FormSpec(
    title: editing ? 'Edit milestone' : 'New milestone',
    initial: {
      if (editing) ...{
        'projectId': existing['projectId'],
        '__label_projectId': existing['projectName'],
        'title': existing['title'],
        'dueDate': existing['dueDate'],
        'amount': existing['amount']?.toString(),
      } else if (projectId != null) ...{'projectId': projectId, '__label_projectId': projectName},
    },
    fields: [
      Field.lookup('projectId', 'Project', path: '$pBase/projects', subtitleKey: 'number', required: true),
      Field.text('title', 'Title', required: true, maxLength: 200),
      Field.date('dueDate', 'Due date', required: true),
      Field.money('amount', 'Billing amount', required: true, initial: '0'),
    ],
    submit: (ref, body, raw) {
      final api = ref.read(apiProvider);
      final full = {...body, 'status': editing ? existing['status'] : 'pending', if (editing && existing['achievedDate'] != null) 'achievedDate': existing['achievedDate']};
      return editing ? api.put('$pBase/milestones/${existing['id']}', body: full) : api.post('$pBase/milestones', body: full);
    },
  );
}

Widget milestoneTile(BuildContext context, Json m) => ListTile(
      onTap: () => Navigator.of(context).push(_route(MilestoneDetailScreen(id: '${m['id']}'))),
      title: Text('${m['title']}', maxLines: 1, overflow: TextOverflow.ellipsis),
      subtitle: Text('Due ${formatDate(m['dueDate'])}'),
      trailing: Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.end, children: [
        Text(formatMoney((m['amount'] as num?) ?? 0), style: const TextStyle(fontWeight: FontWeight.w600)),
        const SizedBox(height: 4),
        chip(milestoneStatuses, m['status']),
      ]),
    );

class MilestoneDetailScreen extends StatelessWidget {
  const MilestoneDetailScreen({super.key, required this.id});
  final String id;

  @override
  Widget build(BuildContext context) => Consumer(builder: (context, ref, _) {
        return DetailPage(
          path: '$pBase/milestones/$id',
          titleOf: (d) => '${d['title']}',
          canEdit: (d) => d['status'] != 'invoiced' && ref.read(meOrEmptyProvider).can('projects.milestones.manage'),
          edit: (d) => milestoneForm(ref, existing: d),
          body: (context, d) => DetailBody(children: [
            DetailHeader(title: '${d['title']}', subtitle: '${d['projectName']}'),
            Align(alignment: Alignment.centerLeft, child: chip(milestoneStatuses, d['status'])),
            const SizedBox(height: 8),
            InfoCard(rows: [
              InfoRow('Amount', formatMoney((d['amount'] as num?) ?? 0), bold: true),
              InfoRow('Due', formatDate(d['dueDate'])),
              InfoRow('Achieved', d['achievedDate'] == null ? null : formatDate(d['achievedDate'])),
            ]),
          ]),
          actions: [
            ActionSpec(
              label: 'Mark achieved',
              icon: Icons.flag,
              permission: 'projects.milestones.manage',
              visible: (d) => d['status'] == 'pending',
              confirm: 'Mark this milestone achieved today? It can then be billed.',
              successMessage: 'Milestone achieved',
              run: (ref, d, _) => ref.read(apiProvider).post('$pBase/milestones/$id/achieve', body: const {}),
            ),
            ActionSpec(
              label: 'Delete',
              icon: Icons.delete_outline,
              permission: 'projects.milestones.manage',
              destructive: true,
              visible: (d) => d['status'] != 'invoiced',
              confirm: 'Delete this milestone?',
              successMessage: 'Deleted',
              run: (ref, d, _) async {
                await ref.read(apiProvider).delete('$pBase/milestones/$id');
                if (context.mounted) Navigator.of(context).pop();
                return null;
              },
            ),
          ],
        );
      });
}

// ---------------------------------------------------------------- timesheets

/// Quick time entry: today, the signed-in user, the project; hours and a note are all that is left to type.
FormSpec timesheetForm(WidgetRef ref, {Json? existing, String? projectId, String? projectName, String? taskId, String? taskTitle}) {
  final me = ref.read(meOrEmptyProvider);
  final editing = existing != null;
  return FormSpec(
    title: editing ? 'Edit time entry' : 'Log time',
    initial: {
      if (editing) ...{
        'projectId': existing['projectId'],
        '__label_projectId': existing['projectName'],
        'taskId': existing['taskId'],
        '__label_taskId': existing['taskTitle'],
        'date': existing['date'],
        'workerName': existing['workerName'],
        'hours': existing['hours']?.toString(),
        'hourlyRate': existing['hourlyRate']?.toString(),
        'description': existing['description'],
        'isBillable': existing['isBillable'],
      } else ...{
        if (projectId != null) ...{'projectId': projectId, '__label_projectId': projectName},
        if (taskId != null) ...{'taskId': taskId, '__label_taskId': taskTitle},
        'workerName': me.fullName,
      },
    },
    fields: [
      Field.lookup('projectId', 'Project', path: '$pBase/projects', subtitleKey: 'number', required: true),
      Field.lookup('taskId', 'Task (optional)', path: '$pBase/tasks', labelKey: 'title', subtitleKey: 'number'),
      Field.date('date', 'Date', required: true, initial: 'today'),
      Field.decimal('hours', 'Hours', required: true, min: 0.1, max: 24, decimals: 2, hint: 'for example 1.5'),
      Field.text('description', 'What was done', maxLength: 500),
      Field.text('workerName', 'Worker', required: true, maxLength: 100),
      Field.money('hourlyRate', 'Hourly rate', required: true, initial: '0'),
      Field.toggle('isBillable', 'Billable', initial: true),
    ],
    submit: (ref, body, raw) {
      final api = ref.read(apiProvider);
      return editing ? api.put('$pBase/timesheets/${existing['id']}', body: body) : api.post('$pBase/timesheets', body: body);
    },
  );
}

Widget timesheetTile(BuildContext context, Json t) => ListTile(
      onTap: () => Navigator.of(context).push(_route(TimesheetDetailScreen(id: '${t['id']}'))),
      title: Text('${t['hours']} h · ${t['workerName']}', maxLines: 1, overflow: TextOverflow.ellipsis),
      subtitle: Text('${formatDate(t['date'])}${(t['projectName'] ?? '').toString().isEmpty ? '' : ' · ${t['projectName']}'}${(t['description'] ?? '').toString().isEmpty ? '' : '\n${t['description']}'}'),
      isThreeLine: (t['description'] ?? '').toString().isNotEmpty,
      trailing: Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.end, children: [
        Text(formatMoney((t['totalAmount'] as num?) ?? 0), style: const TextStyle(fontWeight: FontWeight.w600)),
        const SizedBox(height: 4),
        StatusChip(t['isBilled'] == true ? 'Billed' : (t['isBillable'] == true ? 'Unbilled' : 'Non-billable'), color: t['isBilled'] == true ? Colors.green.shade700 : Colors.blueGrey),
      ]),
    );

class TimesheetsScreen extends StatelessWidget {
  const TimesheetsScreen({super.key});
  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Timesheets',
        path: '$pBase/timesheets',
        searchHint: 'Search by worker',
        emptyText: 'No time entries yet.',
        itemBuilder: timesheetTile,
        filterKey: 'isBilled',
        filters: const [Option('false', 'Unbilled'), Option('true', 'Billed')],
        create: (ref) => timesheetForm(ref),
        createPermission: 'projects.timesheets.manage',
      );
}

class TimesheetDetailScreen extends StatelessWidget {
  const TimesheetDetailScreen({super.key, required this.id});
  final String id;

  @override
  Widget build(BuildContext context) => Consumer(builder: (context, ref, _) {
        return DetailPage(
          path: '$pBase/timesheets/$id',
          titleOf: (d) => '${d['hours']} h',
          canEdit: (d) => d['isBilled'] != true && ref.read(meOrEmptyProvider).can('projects.timesheets.manage'),
          edit: (d) => timesheetForm(ref, existing: d),
          body: (context, d) => DetailBody(children: [
            DetailHeader(title: '${d['workerName']}', subtitle: '${d['projectName']}'),
            InfoCard(rows: [
              InfoRow('Date', formatDate(d['date'])),
              InfoRow('Hours', '${d['hours']}'),
              InfoRow('Rate', formatMoney((d['hourlyRate'] as num?) ?? 0)),
              InfoRow('Amount', formatMoney((d['totalAmount'] as num?) ?? 0), bold: true),
              InfoRow('Task', text(d['taskTitle'])),
              InfoRow('Billable', d['isBillable'] == true ? 'Yes' : 'No'),
              InfoRow('Billed', d['isBilled'] == true ? 'Yes' : 'No'),
            ]),
            if ((d['description'] ?? '').toString().isNotEmpty) Text(d['description'].toString()),
          ]),
          actions: [
            ActionSpec(
              label: 'Delete',
              icon: Icons.delete_outline,
              permission: 'projects.timesheets.manage',
              destructive: true,
              visible: (d) => d['isBilled'] != true,
              confirm: 'Delete this time entry?',
              successMessage: 'Deleted',
              run: (ref, d, _) async {
                await ref.read(apiProvider).delete('$pBase/timesheets/$id');
                if (context.mounted) Navigator.of(context).pop();
                return null;
              },
            ),
          ],
        );
      });
}

// ---------------------------------------------------------------- billing

Widget billingTile(BuildContext context, Json b) {
  final hasWork = ((b['totalUnbilledAmount'] as num?) ?? 0) > 0;
  return ListTile(
    onTap: () => Navigator.of(context).push(_route(BillingDetailScreen(data: b))),
    title: Text('${b['projectName']}', maxLines: 1, overflow: TextOverflow.ellipsis),
    subtitle: Text('${b['projectNumber']} · ${b['unbilledHours']} h unbilled · ${b['achievedMilestonesCount']} milestones'),
    trailing: Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.end, children: [
      Text(formatMoney((b['totalUnbilledAmount'] as num?) ?? 0), style: const TextStyle(fontWeight: FontWeight.w600)),
      const SizedBox(height: 4),
      StatusChip(hasWork ? 'Ready to bill' : 'Nothing to bill', color: hasWork ? Colors.blue.shade700 : Colors.blueGrey),
    ]),
  );
}

class BillingScreen extends StatelessWidget {
  const BillingScreen({super.key});
  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(title: const Text('Project billing')),
        body: PagedListView(path: '$pBase/billing/overview', itemBuilder: billingTile, searchHint: 'Search projects', emptyText: 'Nothing to bill.'),
      );
}

/// Bills a project: the backend needs a customer and creates the invoice; its errors are shown as they come.
class BillingDetailScreen extends ConsumerStatefulWidget {
  const BillingDetailScreen({super.key, required this.data});
  final Json data;

  @override
  ConsumerState<BillingDetailScreen> createState() => _BillingDetailState();
}

class _BillingDetailState extends ConsumerState<BillingDetailScreen> {
  bool _busy = false;
  String? _error;
  Json? _result;

  Future<void> _bill() async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        content: const Text('Create an invoice for all unbilled time and achieved milestones of this project?'),
        actions: [TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('Cancel')), FilledButton(onPressed: () => Navigator.pop(ctx, true), child: const Text('Bill'))],
      ),
    );
    if (ok != true) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final r = await ref.read(apiProvider).post('$pBase/billing/bill', body: {'projectId': widget.data['projectId']});
      ref.read(dataVersionProvider.notifier).bump();
      if (mounted) setState(() => _result = r as Json);
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final b = widget.data;
    final me = ref.watch(meOrEmptyProvider);
    final r = _result;
    return Scaffold(
      appBar: AppBar(title: Text('${b['projectNumber']}')),
      body: DetailBody(children: [
        DetailHeader(title: '${b['projectName']}', subtitle: label(billingTypes, b['billingType'])),
        InfoCard(title: 'Ready to invoice', rows: [
          InfoRow('Unbilled hours', '${b['unbilledHours']}'),
          InfoRow('Time amount', formatMoney((b['unbilledHoursAmount'] as num?) ?? 0)),
          InfoRow('Achieved milestones', '${b['achievedMilestonesCount']}'),
          InfoRow('Milestone amount', formatMoney((b['achievedMilestonesAmount'] as num?) ?? 0)),
          InfoRow('Total', formatMoney((b['totalUnbilledAmount'] as num?) ?? 0), bold: true),
        ]),
        if (_error != null) Card(color: Theme.of(context).colorScheme.errorContainer, child: Padding(padding: const EdgeInsets.all(12), child: Text(_error!))),
        if (r != null)
          InfoCard(title: 'Billed', rows: [
            InfoRow('Invoice', text(r['invoiceNumber'])),
            InfoRow('Amount', formatMoney((r['totalAmountBilled'] as num?) ?? 0), bold: true),
            InfoRow('Timesheets', '${r['billedTimesheetsCount']}'),
            InfoRow('Milestones', '${r['billedMilestonesCount']}'),
          ]),
      ]),
      bottomNavigationBar: r == null && me.can('projects.billing.manage')
          ? SafeArea(
              child: Padding(
                padding: const EdgeInsets.fromLTRB(12, 6, 12, 10),
                child: FilledButton.icon(onPressed: _busy ? null : _bill, icon: const Icon(Icons.request_quote_outlined), label: const Text('Create invoice')),
              ),
            )
          : null,
    );
  }
}
