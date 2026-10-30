import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../../core/format.dart';
import '../../core/session.dart';
import '../../kit/detail_page.dart';
import '../../kit/field.dart';
import '../../kit/form_page.dart';
import '../../kit/list_screen.dart';
import '../../kit/widgets.dart';
import 'common.dart';

const _woPath = '$rePath/work-orders';
const _pmPath = '$rePath/pm-schedules';
const _inspPath = '$rePath/inspections';

const workOrderStatuses = [
  Option('new', 'New'),
  Option('assigned', 'Assigned'),
  Option('inProgress', 'In progress'),
  Option('onHold', 'On hold'),
  Option('completed', 'Completed'),
  Option('closed', 'Closed'),
  Option('cancelled', 'Cancelled'),
];

const _woKeys = ['propertyId', 'unitId', 'leaseId', 'issue', 'priority', 'category', 'reportedOn', 'reportedBy', 'vendorPartyId', 'technician', 'scheduledFor', 'estimatedCost', 'details'];

// ---- Work orders ------------------------------------------------------------------------------------------------------------------

/// Create (existing == null) or edit an open work order. [propertyId]/[propertyName] pre-select a property.
FormSpec workOrderForm(WidgetRef ref, {Json? existing, String? propertyId, String? propertyName, bool assign = false}) {
  final editing = existing != null;
  return FormSpec(
    title: assign ? 'Assign work order' : (editing ? 'Edit ${existing['number'] ?? 'work order'}' : 'New work order'),
    submitLabel: assign ? 'Assign' : 'Save',
    initial: {
      'propertyId': ?propertyId,
      '__label_propertyId': ?propertyName,
      if (editing) ...{for (final k in _woKeys) k: existing[k]},
    },
    fields: [
      if (!editing && propertyId == null) propertyLookup(),
      if (!assign) ...[
        unitLookup(),
        Field.text('issue', 'Issue', required: true, maxLength: 300),
        Field.select('priority', 'Priority', options: rePriorities, required: true, initial: 'normal'),
        Field.text('category', 'Category (plumbing, electrical...)', maxLength: 100),
        Field.text('reportedBy', 'Reported by', maxLength: 150),
      ],
      Field.lookup('vendorPartyId', 'Vendor', path: '/api/v1/parties', query: const {'role': 'Vendor'}, subtitleKey: 'code'),
      Field.text('technician', 'Technician', maxLength: 150),
      Field.date('scheduledFor', 'Scheduled for'),
      if (!assign) ...[
        Field.money('estimatedCost', 'Estimated cost', initial: '0'),
        Field.multiline('details', 'Details', maxLength: 2000),
      ],
    ],
    submit: (ref, body, raw) async {
      final api = ref.read(apiProvider);
      if (editing) {
        // The API takes the whole input on update; fields that are not on the form keep their value.
        final full = {for (final k in _woKeys) if (existing[k] != null) k: existing[k], ...body};
        if (assign && body['vendorPartyId'] == null && (body['technician'] ?? '').toString().isEmpty && existing['vendorPartyId'] == null && (existing['technician'] ?? '').toString().isEmpty) {
          throw ApiException(409, 'Choose a vendor or enter a technician.');
        }
        final saved = await api.put('$_woPath/${existing['id']}', body: full);
        return assign ? api.post('$_woPath/${existing['id']}/status', body: {'status': 'assigned'}) : saved;
      }
      if (propertyId != null) body['propertyId'] = propertyId;
      return api.post(_woPath, body: body);
    },
  );
}

Widget workOrderTile(BuildContext context, Json w) => reTile(
      context,
      title: '${w['issue']}',
      subtitle: '${w['number'] ?? ''} · ${humanize(w['priority']?.toString())}${(w['category'] ?? '').toString().isEmpty ? '' : ' · ${w['category']}'}\nReported ${formatDate(w['reportedOn'])}',
      status: w['status'],
      onTap: () => reGo(context, WorkOrderDetailScreen(id: w['id'].toString())),
    );

class WorkOrdersScreen extends StatelessWidget {
  const WorkOrdersScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Work orders',
        path: _woPath,
        searchHint: 'Search work orders',
        emptyText: 'No work orders found.',
        itemBuilder: workOrderTile,
        filters: workOrderStatuses,
        create: (ref) => workOrderForm(ref),
        createPermission: 'realestate.workorders.create',
      );
}

FormSpec _statusForm(String id, String title, String status, List<Field> fields, {String submit = 'Save'}) => FormSpec(
      title: title,
      submitLabel: submit,
      fields: fields,
      submit: (ref, body, raw) => ref.read(apiProvider).post('$_woPath/$id/status', body: {'status': status, ...body}),
    );

class WorkOrderDetailScreen extends StatelessWidget {
  const WorkOrderDetailScreen({super.key, required this.id});

  final String id;

  static const _open = {'new', 'assigned', 'inProgress', 'onHold'};

  static ActionSpec _move(String label, IconData icon, String perm, String to, bool Function(Json) visible, {String? confirm, bool destructive = false}) => ActionSpec(
        label: label,
        icon: icon,
        permission: perm,
        destructive: destructive,
        visible: visible,
        confirm: confirm,
        run: (ref, d, _) => ref.read(apiProvider).post('$_woPath/${d['id']}/status', body: {'status': to}),
      );

  @override
  Widget build(BuildContext context) {
    return Consumer(builder: (context, ref, _) {
      return DetailPage(
        path: '$_woPath/$id',
        titleOf: (d) => (d['number'] ?? 'Work order').toString(),
        canEdit: (d) => _open.contains(d['status']) && reCan(ref, 'realestate.workorders.edit'),
        edit: (d) => workOrderForm(ref, existing: d),
        body: (context, d) => _body(context, d),
        actions: [
          ActionSpec(
            label: 'Assign',
            icon: Icons.person_add_alt_1_outlined,
            permission: 'realestate.workorders.assign',
            visible: (d) => d['status'] == 'new' || d['status'] == 'onHold',
            form: (d) => workOrderForm(ref, existing: d, assign: true),
            run: (ref, d, _) async => null,
          ),
          _move('Start work', Icons.play_arrow, 'realestate.workorders.edit', 'inProgress', (d) => const {'new', 'assigned', 'onHold'}.contains(d['status'])),
          _move('Put on hold', Icons.pause_circle_outline, 'realestate.workorders.edit', 'onHold', (d) => d['status'] == 'assigned' || d['status'] == 'inProgress'),
          ActionSpec(
            label: 'Complete',
            icon: Icons.task_alt,
            permission: 'realestate.workorders.complete',
            visible: (d) => d['status'] == 'inProgress',
            form: (d) => _statusForm(id, 'Complete work order', 'completed', [
              Field.money('actualCost', 'Actual cost', initial: d['actualCost'] ?? d['estimatedCost']),
              Field.multiline('resolutionNotes', 'What was done', maxLength: 2000),
            ], submit: 'Complete'),
            run: (ref, d, _) async => null,
          ),
          _move('Close', Icons.lock_outline, 'realestate.workorders.close', 'closed', (d) => d['status'] == 'completed', confirm: 'Close this work order? It can no longer be changed.'),
          _move('Reopen', Icons.replay, 'realestate.workorders.edit', 'inProgress', (d) => d['status'] == 'completed'),
          _move('Cancel', Icons.cancel_outlined, 'realestate.workorders.cancel', 'cancelled', (d) => _open.contains(d['status']), confirm: 'Cancel this work order?', destructive: true),
        ],
      );
    });
  }

  Widget _body(BuildContext context, Json d) => DetailBody(children: [
        DetailHeader(title: '${d['issue']}', subtitle: '${d['number'] ?? ''} · ${humanize(d['priority']?.toString())} priority', status: d['status']?.toString()),
        InfoCard(rows: [
          InfoRow('Category', text(d['category'])),
          InfoRow('Reported', '${formatDate(d['reportedOn'])}${(d['reportedBy'] ?? '').toString().isEmpty ? '' : ' by ${d['reportedBy']}'}'),
          InfoRow('Technician', text(d['technician'])),
          InfoRow('Scheduled for', d['scheduledFor'] == null ? null : formatDate(d['scheduledFor'])),
          InfoRow('Completed on', d['completedOn'] == null ? null : formatDate(d['completedOn'])),
          InfoRow('Estimated cost', formatMoney((d['estimatedCost'] as num?) ?? 0)),
          InfoRow('Actual cost', d['actualCost'] == null ? null : formatMoney(d['actualCost'] as num)),
          InfoRow('Cost borne by', d['chargeableTo'] == null ? null : humanize(d['chargeableTo'].toString())),
        ]),
        if ((d['details'] ?? '').toString().isNotEmpty) ...[Text('Details', style: Theme.of(context).textTheme.titleMedium), Text(d['details'].toString())],
        if ((d['resolutionNotes'] ?? '').toString().isNotEmpty) ...[
          const SizedBox(height: 8),
          Text('Resolution', style: Theme.of(context).textTheme.titleMedium),
          Text(d['resolutionNotes'].toString()),
        ],
      ]);
}

// ---- Preventive maintenance -------------------------------------------------------------------------------------------------------

FormSpec pmForm(WidgetRef ref, {Json? existing}) {
  final editing = existing != null;
  return FormSpec(
    title: editing ? 'Edit ${existing['title']}' : 'New schedule',
    initial: editing ? {for (final k in ['propertyId', 'unitId', 'title', 'frequencyMonths', 'nextDue', 'priority', 'category', 'leadDays', 'estimatedCost', 'isActive', 'notes']) k: existing[k]} : const {},
    fields: [
      propertyLookup(),
      unitLookup(),
      Field.text('title', 'Title', required: true, maxLength: 200),
      Field.integer('frequencyMonths', 'Repeat every (months)', required: true, min: 1, max: 120, initial: '3'),
      Field.date('nextDue', 'Next due', required: true, initial: 'today'),
      Field.select('priority', 'Priority', options: rePriorities, initial: 'normal'),
      Field.text('category', 'Category', maxLength: 100),
      Field.integer('leadDays', 'Create the work order this many days before', min: 0, initial: '7'),
      Field.money('estimatedCost', 'Estimated cost', initial: '0'),
      Field.toggle('isActive', 'Active', initial: true),
      Field.multiline('notes', 'Notes', maxLength: 1000),
    ],
    submit: (ref, body, raw) async {
      final api = ref.read(apiProvider);
      return editing ? api.put('$_pmPath/${existing['id']}', body: body) : api.post(_pmPath, body: body);
    },
  );
}

class PreventiveMaintenanceScreen extends ConsumerWidget {
  const PreventiveMaintenanceScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final manage = reCan(ref, 'realestate.pm.manage');
    return Scaffold(
      appBar: AppBar(title: const Text('Preventive maintenance'), actions: [
        if (manage)
          IconButton(
            tooltip: 'Generate due work orders',
            icon: const Icon(Icons.playlist_add_check),
            onPressed: () async {
              final messenger = ScaffoldMessenger.of(context);
              try {
                final r = await ref.read(apiProvider).post('$_pmPath/run');
                messenger.showSnackBar(SnackBar(content: Text('${(r as Map)['workOrdersCreated'] ?? 0} work orders created')));
              } on ApiException catch (e) {
                messenger.showSnackBar(SnackBar(content: Text(e.message)));
              }
            },
          ),
      ]),
      body: ListScreen(
        title: 'Preventive maintenance',
        path: _pmPath,
        showAppBar: false,
        searchHint: 'Search schedules',
        emptyText: 'No schedules found.',
        itemBuilder: (context, s) => ListTile(
          title: Text('${s['title']}'),
          subtitle: Text('Every ${s['frequencyMonths']} months · next ${formatDate(s['nextDue'])}'),
          trailing: s['isActive'] == false ? reChip('inactive') : Text(humanize(s['priority']?.toString())),
          onTap: manage ? () => openForm<Object?>(context, pmForm(ref, existing: s)) : null,
        ),
        create: (ref) => pmForm(ref),
        createPermission: 'realestate.pm.manage',
      ),
    );
  }
}

// ---- Inspections ------------------------------------------------------------------------------------------------------------------

const inspectionKinds = [Option('moveIn', 'Move-in'), Option('moveOut', 'Move-out'), Option('routine', 'Routine')];
const _conditions = [Option('good', 'Good'), Option('fair', 'Fair'), Option('poor', 'Poor'), Option('damaged', 'Damaged')];

FormSpec inspectionForm(WidgetRef ref, {Json? existing}) {
  final editing = existing != null;
  return FormSpec(
    title: editing ? 'Edit inspection' : 'New inspection',
    initial: editing
        ? {
            'propertyId': existing['propertyId'],
            '__label_propertyId': existing['propertyName'],
            'unitId': existing['unitId'],
            if (existing['unitNumber'] != null) '__label_unitId': 'Unit ${existing['unitNumber']}',
            'leaseId': existing['leaseId'],
            'kind': existing['kind'],
            'inspectionDate': existing['inspectionDate'],
            'inspector': existing['inspector'],
            'notes': existing['notes'],
            'items': [
              for (final i in (existing['items'] as List<dynamic>? ?? const [])) {'area': (i as Json)['area'], 'condition': i['condition'], 'notes': i['notes'], 'repairCost': i['repairCost']},
            ],
          }
        : const {},
    fields: [
      propertyLookup(),
      unitLookup(),
      Field.select('kind', 'Kind', options: inspectionKinds, required: true, initial: 'routine'),
      Field.date('inspectionDate', 'Date', required: true, initial: 'today'),
      Field.text('inspector', 'Inspector', maxLength: 150),
      Field.multiline('notes', 'Notes', maxLength: 1000),
      Field.lines('items', 'Checklist', minLines: 0, addLabel: 'Add area', fields: [
        Field.text('area', 'Area (kitchen, bathroom...)', required: true, maxLength: 100),
        Field.select('condition', 'Condition', options: _conditions, required: true, initial: 'good'),
        Field.text('notes', 'Notes', maxLength: 300),
        Field.money('repairCost', 'Repair cost', initial: '0'),
      ]),
    ],
    submit: (ref, body, raw) async {
      final api = ref.read(apiProvider);
      body['items'] ??= <dynamic>[];
      if (editing && existing['leaseId'] != null) body['leaseId'] = existing['leaseId'];
      return editing ? api.put('$_inspPath/${existing['id']}', body: body) : api.post(_inspPath, body: body);
    },
  );
}

class InspectionsScreen extends StatelessWidget {
  const InspectionsScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Inspections',
        path: _inspPath,
        searchHint: 'Search inspections',
        emptyText: 'No inspections found.',
        filters: const [Option('draft', 'Draft'), Option('completed', 'Completed')],
        itemBuilder: (context, i) => reTile(
          context,
          title: '${humanize(i['kind']?.toString())} inspection',
          subtitle: '${formatDate(i['inspectionDate'])}${(i['inspector'] ?? '').toString().isEmpty ? '' : ' · ${i['inspector']}'}',
          trailing: ((i['damageCost'] as num?) ?? 0) > 0 ? formatMoney(i['damageCost'] as num) : null,
          status: i['status'],
          onTap: () => reGo(context, InspectionDetailScreen(id: i['id'].toString())),
        ),
        create: (ref) => inspectionForm(ref),
        createPermission: 'realestate.inspections.manage',
      );
}

class InspectionDetailScreen extends StatelessWidget {
  const InspectionDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) {
    return Consumer(builder: (context, ref, _) {
      return DetailPage(
        path: '$_inspPath/$id',
        titleOf: (d) => '${humanize(d['kind']?.toString())} inspection',
        canEdit: (d) => d['status'] == 'draft' && reCan(ref, 'realestate.inspections.manage'),
        edit: (d) => inspectionForm(ref, existing: d),
        body: (context, d) {
          final items = (d['items'] as List<dynamic>? ?? const []).cast<Json>();
          return DetailBody(children: [
            DetailHeader(title: '${d['propertyName']}${d['unitNumber'] == null ? '' : ' · Unit ${d['unitNumber']}'}', subtitle: '${formatDate(d['inspectionDate'])}${(d['inspector'] ?? '').toString().isEmpty ? '' : ' · ${d['inspector']}'}', status: d['status']?.toString()),
            InfoCard(rows: [InfoRow('Damage / repair cost', formatMoney((d['damageCost'] as num?) ?? 0), bold: true)]),
            if ((d['notes'] ?? '').toString().isNotEmpty) Padding(padding: const EdgeInsets.only(bottom: 12), child: Text(d['notes'].toString())),
            Text('Checklist', style: Theme.of(context).textTheme.titleMedium),
            if (items.isEmpty) const Text('No areas recorded.'),
            for (final i in items)
              ListTile(
                contentPadding: EdgeInsets.zero,
                title: Text('${i['area']}'),
                subtitle: (i['notes'] ?? '').toString().isEmpty ? null : Text('${i['notes']}'),
                trailing: Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.end, children: [
                  reChip(i['condition']),
                  if (((i['repairCost'] as num?) ?? 0) > 0) Text(formatMoney(i['repairCost'] as num)),
                  if (i['workOrderId'] != null) const Text('Work order raised', style: TextStyle(fontSize: 11)),
                ]),
              ),
          ]);
        },
        actions: [
          ActionSpec(
            label: 'Complete',
            icon: Icons.task_alt,
            permission: 'realestate.inspections.manage',
            visible: (d) => d['status'] == 'draft',
            form: (d) => FormSpec(
              title: 'Complete inspection',
              submitLabel: 'Complete',
              intro: 'A completed inspection can no longer be edited.',
              fields: [Field.toggle('raiseWorkOrders', 'Raise work orders for areas that need repair')],
              submit: (ref, body, raw) => ref.read(apiProvider).post('$_inspPath/$id/complete', body: body),
            ),
            run: (ref, d, _) async => null,
          ),
        ],
      );
    });
  }
}
