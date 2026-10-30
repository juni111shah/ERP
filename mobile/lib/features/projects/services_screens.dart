import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/format.dart';
import '../../core/me.dart';
import '../../core/session.dart';
import '../../kit/detail_page.dart';
import '../../kit/field.dart';
import '../../kit/form_page.dart';
import '../crm/client_filtered_list.dart';
import '../../kit/widgets.dart';
import '../common/paged_list_view.dart';

const sBase = '/api/v1/m/services';

const engagementTypes = [Option('fixedPrice', 'Fixed price'), Option('timeAndMaterials', 'Time & materials'), Option('retainer', 'Retainer')];
const engagementStatuses = [Option('draft', 'Draft'), Option('active', 'Active'), Option('onHold', 'On hold'), Option('completed', 'Completed'), Option('cancelled', 'Cancelled')];
const retainerCycles = [Option('monthly', 'Monthly'), Option('quarterly', 'Quarterly'), Option('annual', 'Annual')];
const retainerStatuses = [Option('active', 'Active'), Option('paused', 'Paused'), Option('terminated', 'Terminated')];
const serviceMilestoneStatuses = [Option('planned', 'Planned'), Option('readyToInvoice', 'Ready to invoice'), Option('invoiced', 'Invoiced'), Option('paid', 'Paid')];

const engagementMoves = <String, List<String>>{
  'draft': ['active', 'cancelled'],
  'active': ['onHold', 'completed', 'cancelled'],
  'onHold': ['active', 'cancelled'],
  'completed': <String>[],
  'cancelled': <String>[],
};
const retainerMoves = <String, List<String>>{
  'active': ['paused', 'terminated'],
  'paused': ['active', 'terminated'],
  'terminated': <String>[],
};

String _label(List<Option> options, Object? value) {
  for (final o in options) {
    if (o.value == value) return o.label;
  }
  return humanize(value?.toString());
}

Color _color(String s) => switch (s) {
      'active' || 'readyToInvoice' => Colors.blue.shade700,
      'completed' || 'paid' => Colors.green.shade700,
      'cancelled' || 'terminated' => Colors.red.shade700,
      'onHold' || 'paused' || 'invoiced' => Colors.orange.shade800,
      _ => Colors.blueGrey,
    };

Widget _chip(List<Option> options, Object? v) => StatusChip(_label(options, v), color: _color('$v'));

Route<T> _route<T>(Widget w) => MaterialPageRoute<T>(builder: (_) => w);

List<Option> _entities(Me me) => [for (final e in me.legalEntities) Option(e.id, e.name)];

// ---------------------------------------------------------------- engagements

FormSpec engagementForm(WidgetRef ref) {
  final me = ref.read(meOrEmptyProvider);
  return FormSpec(
    title: 'New engagement',
    fields: [
      Field.select('legalEntityId', 'Legal entity', options: _entities(me), required: true, initial: me.defaultEntity?.id),
      Field.text('title', 'Title', required: true, maxLength: 200),
      Field.lookup('clientId', 'Client', path: '/api/v1/parties', query: const {'role': 'Customer'}, subtitleKey: 'code', required: true, copy: const {'clientName': 'name'}),
      Field.text('clientName', 'Client name', required: true, maxLength: 150),
      Field.select('type', 'Type', options: engagementTypes, required: true, initial: 'fixedPrice'),
      Field.money('contractValue', 'Contract value', required: true, initial: '0'),
      Field.decimal('budgetedHours', 'Budgeted hours', required: true, min: 0, initial: '0'),
      Field.date('startDate', 'Start date', required: true, initial: 'today'),
      Field.date('targetEndDate', 'Target end date', validator: (v, all) => v != null && all['startDate'] != null && '$v'.compareTo('${all['startDate']}') < 0 ? 'Before the start date' : null),
      Field.text('leadConsultantName', 'Lead consultant', maxLength: 150),
      Field.multiline('description', 'Description', maxLength: 2000),
    ],
    submit: (ref, body, raw) => ref.read(apiProvider).post('$sBase/engagements', body: body),
  );
}

Widget engagementTile(BuildContext context, Json e) => ListTile(
      onTap: () => Navigator.of(context).push(_route(EngagementDetailScreen(id: '${e['id']}'))),
      title: Text('${e['title']}', maxLines: 1, overflow: TextOverflow.ellipsis),
      subtitle: Text('${e['engagementCode']} · ${e['clientName']}'),
      trailing: Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.end, children: [
        Text(formatMoney((e['contractValue'] as num?) ?? 0), style: const TextStyle(fontWeight: FontWeight.w600)),
        const SizedBox(height: 4),
        _chip(engagementStatuses, e['status']),
      ]),
    );

class EngagementsScreen extends StatelessWidget {
  const EngagementsScreen({super.key});
  @override
  Widget build(BuildContext context) => ClientFilteredListScreen(
        title: 'Engagements',
        path: '$sBase/engagements',
        filterKey: 'status',
        searchHint: 'Search engagements',
        emptyText: 'No engagements found.',
        itemBuilder: engagementTile,
        filters: engagementStatuses,
        create: (ref) => engagementForm(ref),
        createPermission: 'services.engagements.manage',
      );
}

ActionSpec _engagementMove(String target, String text, IconData icon, {bool destructive = false, String? confirm}) => ActionSpec(
      label: text,
      icon: icon,
      permission: 'services.engagements.manage',
      destructive: destructive,
      confirm: confirm,
      visible: (d) => (engagementMoves[d['status']] ?? const []).contains(target),
      successMessage: 'Engagement ${_label(engagementStatuses, target).toLowerCase()}',
      run: (ref, d, _) => ref.read(apiProvider).post('$sBase/engagements/${d['id']}/status', body: {'id': d['id'], 'status': target}),
    );

class EngagementDetailScreen extends StatelessWidget {
  const EngagementDetailScreen({super.key, required this.id});
  final String id;

  @override
  Widget build(BuildContext context) => DetailPage(
        path: '$sBase/engagements/$id',
        titleOf: (d) => '${d['engagementCode']}',
        body: (context, d) => DetailBody(children: [
          DetailHeader(title: '${d['title']}', subtitle: '${d['clientName']}'),
          Align(alignment: Alignment.centerLeft, child: _chip(engagementStatuses, d['status'])),
          const SizedBox(height: 8),
          InfoCard(rows: [
            InfoRow('Type', _label(engagementTypes, d['type'])),
            InfoRow('Contract value', formatMoney((d['contractValue'] as num?) ?? 0), bold: true),
            InfoRow('Budgeted hours', '${d['budgetedHours'] ?? 0}'),
            InfoRow('Consumed hours', '${d['consumedHours'] ?? 0}'),
            InfoRow('Consumed cost', formatMoney((d['consumedCost'] as num?) ?? 0)),
            InfoRow('Start', formatDate(d['startDate'])),
            InfoRow('Target end', d['targetEndDate'] == null ? null : formatDate(d['targetEndDate'])),
            InfoRow('Ended', d['actualEndDate'] == null ? null : formatDate(d['actualEndDate'])),
            InfoRow('Lead consultant', text(d['leadConsultantName'])),
          ]),
          if ((d['description'] ?? '').toString().isNotEmpty) Text(d['description'].toString()),
        ]),
        actions: [
          _engagementMove('active', 'Activate', Icons.play_arrow),
          _engagementMove('onHold', 'Put on hold', Icons.pause),
          _engagementMove('completed', 'Complete', Icons.check_circle_outline, confirm: 'Complete this engagement? It cannot be reopened.'),
          _engagementMove('cancelled', 'Cancel', Icons.block, destructive: true, confirm: 'Cancel this engagement? It cannot be reopened.'),
        ],
      );
}

// ---------------------------------------------------------------- retainers

FormSpec retainerForm(WidgetRef ref) {
  final me = ref.read(meOrEmptyProvider);
  return FormSpec(
    title: 'New retainer',
    fields: [
      Field.select('legalEntityId', 'Legal entity', options: _entities(me), required: true, initial: me.defaultEntity?.id),
      Field.lookup('engagementId', 'Engagement', path: '$sBase/engagements',
        labelKey: 'title', subtitleKey: 'engagementCode', required: true, copy: const {'clientId': 'clientId', 'clientName': 'clientName'}),
      Field.lookup('clientId', 'Client', path: '/api/v1/parties', query: const {'role': 'Customer'}, subtitleKey: 'code', required: true, copy: const {'clientName': 'name'}),
      Field.text('clientName', 'Client name', required: true, maxLength: 150),
      Field.text('title', 'Title', required: true, maxLength: 200),
      Field.select('cycle', 'Billing cycle', options: retainerCycles, required: true, initial: 'monthly'),
      Field.money('recurringFee', 'Recurring fee', required: true),
      Field.decimal('includedHours', 'Included hours', required: true, min: 0, initial: '0'),
      Field.money('overageHourlyRate', 'Overage rate per hour', required: true, initial: '0'),
      Field.date('startDate', 'Start date', required: true, initial: 'today'),
      Field.date('endDate', 'End date'),
      Field.multiline('terms', 'Terms', maxLength: 2000),
    ],
    submit: (ref, body, raw) => ref.read(apiProvider).post('$sBase/retainers', body: body),
  );
}

Widget retainerTile(BuildContext context, Json r) => ListTile(
      onTap: () => Navigator.of(context).push(_route(RetainerDetailScreen(id: '${r['id']}'))),
      title: Text('${r['title']}', maxLines: 1, overflow: TextOverflow.ellipsis),
      subtitle: Text('${r['retainerNumber']} · ${r['clientName']}\n${_label(retainerCycles, r['cycle'])}'),
      isThreeLine: true,
      trailing: Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.end, children: [
        Text(formatMoney((r['recurringFee'] as num?) ?? 0), style: const TextStyle(fontWeight: FontWeight.w600)),
        const SizedBox(height: 4),
        _chip(retainerStatuses, r['status']),
      ]),
    );

class RetainersScreen extends StatelessWidget {
  const RetainersScreen({super.key});
  @override
  Widget build(BuildContext context) => ClientFilteredListScreen(
        title: 'Retainers',
        path: '$sBase/retainers',
        filterKey: 'status',
        searchHint: 'Search retainers',
        emptyText: 'No retainers found.',
        itemBuilder: retainerTile,
        filters: retainerStatuses,
        create: (ref) => retainerForm(ref),
        createPermission: 'services.retainers.manage',
      );
}

ActionSpec _retainerMove(String target, String text, IconData icon, {bool destructive = false, String? confirm}) => ActionSpec(
      label: text,
      icon: icon,
      permission: 'services.retainers.manage',
      destructive: destructive,
      confirm: confirm,
      visible: (d) => (retainerMoves[d['status']] ?? const []).contains(target),
      successMessage: 'Retainer ${_label(retainerStatuses, target).toLowerCase()}',
      run: (ref, d, _) => ref.read(apiProvider).post('$sBase/retainers/${d['id']}/status', body: {'id': d['id'], 'status': target}),
    );

class RetainerDetailScreen extends StatelessWidget {
  const RetainerDetailScreen({super.key, required this.id});
  final String id;

  @override
  Widget build(BuildContext context) => DetailPage(
        path: '$sBase/retainers/$id',
        titleOf: (d) => '${d['retainerNumber']}',
        body: (context, d) => DetailBody(children: [
          DetailHeader(title: '${d['title']}', subtitle: '${d['clientName']}'),
          Align(alignment: Alignment.centerLeft, child: _chip(retainerStatuses, d['status'])),
          const SizedBox(height: 8),
          InfoCard(rows: [
            InfoRow('Cycle', _label(retainerCycles, d['cycle'])),
            InfoRow('Recurring fee', formatMoney((d['recurringFee'] as num?) ?? 0), bold: true),
            InfoRow('Included hours', '${d['includedHours'] ?? 0}'),
            InfoRow('Overage rate', formatMoney((d['overageHourlyRate'] as num?) ?? 0)),
            InfoRow('Start', formatDate(d['startDate'])),
            InfoRow('End', d['endDate'] == null ? null : formatDate(d['endDate'])),
          ]),
          if ((d['terms'] ?? '').toString().isNotEmpty) ...[Text('Terms', style: Theme.of(context).textTheme.titleMedium), Text(d['terms'].toString())],
        ]),
        actions: [
          _retainerMove('active', 'Resume', Icons.play_arrow),
          _retainerMove('paused', 'Pause', Icons.pause),
          _retainerMove('terminated', 'Terminate', Icons.block, destructive: true, confirm: 'Terminate this retainer? It cannot be restarted.'),
        ],
      );
}

// ---------------------------------------------------------------- service milestones

FormSpec serviceMilestoneForm(WidgetRef ref) {
  final me = ref.read(meOrEmptyProvider);
  return FormSpec(
    title: 'New billing milestone',
    fields: [
      Field.select('legalEntityId', 'Legal entity', options: _entities(me), required: true, initial: me.defaultEntity?.id),
      Field.lookup('engagementId', 'Engagement', path: '$sBase/engagements',
        labelKey: 'title', subtitleKey: 'engagementCode', required: true),
      Field.text('title', 'Title', required: true, maxLength: 200),
      Field.money('amount', 'Amount', required: true),
      Field.date('dueDate', 'Due date', required: true),
      Field.multiline('notes', 'Notes', maxLength: 1000),
    ],
    submit: (ref, body, raw) => ref.read(apiProvider).post('$sBase/milestones', body: body),
  );
}

Widget serviceMilestoneTile(BuildContext context, Json m) => ListTile(
      onTap: () => Navigator.of(context).push(_route(ServiceMilestoneDetailScreen(id: '${m['id']}'))),
      title: Text('${m['title']}', maxLines: 1, overflow: TextOverflow.ellipsis),
      subtitle: Text('${m['milestoneCode']} · due ${formatDate(m['dueDate'])}'),
      trailing: Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.end, children: [
        Text(formatMoney((m['amount'] as num?) ?? 0), style: const TextStyle(fontWeight: FontWeight.w600)),
        const SizedBox(height: 4),
        _chip(serviceMilestoneStatuses, m['status']),
      ]),
    );

class ServiceMilestonesScreen extends StatelessWidget {
  const ServiceMilestonesScreen({super.key});
  @override
  Widget build(BuildContext context) => ClientFilteredListScreen(
        title: 'Milestone billing',
        path: '$sBase/milestones',
        filterKey: 'status',
        searchHint: 'Search milestones',
        emptyText: 'No milestones found.',
        itemBuilder: serviceMilestoneTile,
        filters: serviceMilestoneStatuses,
        create: (ref) => serviceMilestoneForm(ref),
        createPermission: 'services.milestones.manage',
      );
}

ActionSpec _milestoneMove(String target, String text, IconData icon) => ActionSpec(
      label: text,
      icon: icon,
      permission: 'services.milestones.manage',
      // Invoiced and Paid are set by the system only (invoicing and payments), so only planned / ready are offered.
      visible: (d) => (d['status'] == 'planned' && target == 'readyToInvoice') || (d['status'] == 'readyToInvoice' && target == 'planned'),
      successMessage: _label(serviceMilestoneStatuses, target),
      run: (ref, d, _) => ref.read(apiProvider).post('$sBase/milestones/${d['id']}/status', body: {'id': d['id'], 'status': target}),
    );

class ServiceMilestoneDetailScreen extends StatelessWidget {
  const ServiceMilestoneDetailScreen({super.key, required this.id});
  final String id;

  @override
  Widget build(BuildContext context) => DetailPage(
        path: '$sBase/milestones/$id',
        titleOf: (d) => '${d['milestoneCode']}',
        body: (context, d) => DetailBody(children: [
          DetailHeader(title: '${d['title']}', subtitle: 'Due ${formatDate(d['dueDate'])}'),
          Align(alignment: Alignment.centerLeft, child: _chip(serviceMilestoneStatuses, d['status'])),
          const SizedBox(height: 8),
          InfoCard(rows: [
            InfoRow('Amount', formatMoney((d['amount'] as num?) ?? 0), bold: true),
            InfoRow('Completed', d['completionDate'] == null ? null : formatDate(d['completionDate'])),
            InfoRow('Payment', d['status'] == 'paid' ? 'Paid (from the invoice)' : (d['status'] == 'invoiced' ? 'Awaiting payment' : null)),
          ]),
          if ((d['notes'] ?? '').toString().isNotEmpty) Text(d['notes'].toString()),
        ]),
        actions: [
          _milestoneMove('readyToInvoice', 'Ready to invoice', Icons.flag),
          _milestoneMove('planned', 'Back to planned', Icons.undo),
          ActionSpec(
            label: 'Invoice',
            icon: Icons.receipt_long_outlined,
            permission: 'services.milestones.manage',
            visible: (d) => d['status'] == 'readyToInvoice',
            confirm: 'Create the invoice for this milestone?',
            successMessage: 'Invoice created',
            run: (ref, d, _) => ref.read(apiProvider).post('$sBase/milestones/$id/invoice'),
          ),
        ],
      );
}
