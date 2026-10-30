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

const _base = '/api/v1/m/crm';

const leadStatuses = [Option('new', 'New'), Option('contacted', 'Contacted'), Option('qualified', 'Qualified'), Option('unqualified', 'Unqualified'), Option('converted', 'Converted')];
const leadSources = [
  Option('website', 'Website'),
  Option('referral', 'Referral'),
  Option('tradeShow', 'Trade show'),
  Option('coldCall', 'Cold call'),
  Option('campaign', 'Campaign'),
  Option('other', 'Other'),
];
const opportunityStages = [
  Option('qualification', 'Qualification'),
  Option('proposal', 'Proposal'),
  Option('negotiation', 'Negotiation'),
  Option('closedWon', 'Closed won'),
  Option('closedLost', 'Closed lost'),
];
const interactionTypes = [Option('call', 'Call'), Option('meeting', 'Meeting / visit'), Option('email', 'Email'), Option('note', 'Note'), Option('task', 'Task')];

/// Stages an opportunity may move to by hand (mirrors the backend rules; closing has its own action).
const stageMoves = <String, List<String>>{
  'qualification': ['proposal'],
  'proposal': ['negotiation', 'qualification'],
  'negotiation': ['proposal'],
  'closedWon': <String>[],
  'closedLost': ['qualification'],
};

String optionLabel(List<Option> options, Object? value) {
  for (final o in options) {
    if (o.value == value) return o.label;
  }
  return humanize(value?.toString());
}

Color stageColor(String s) => switch (s) {
      'closedWon' || 'qualified' || 'converted' => Colors.green.shade700,
      'closedLost' || 'unqualified' => Colors.red.shade700,
      'negotiation' || 'contacted' => Colors.orange.shade800,
      'proposal' => Colors.blue.shade700,
      _ => Colors.blueGrey,
    };

Widget statusChip(String s, List<Option> options) => StatusChip(optionLabel(options, s), color: stageColor(s));

List<Json> asItems(Object? r) => ((r is Map ? r['items'] : r) as List<dynamic>? ?? const []).cast<Json>();

// ---------------------------------------------------------------- leads

FormSpec leadForm(WidgetRef ref, {Json? existing}) {
  final me = ref.read(meOrEmptyProvider);
  final editing = existing != null;
  return FormSpec(
    title: editing ? 'Edit ${existing['leadNumber']}' : 'New lead',
    initial: {
      if (editing) ...{
        for (final k in const ['fullName', 'companyName', 'jobTitle', 'phone', 'email', 'source', 'notes']) k: existing[k],
        'estimatedValue': existing['estimatedValue']?.toString(),
      },
    },
    fields: [
      if (!editing) Field.select('legalEntityId', 'Legal entity', options: [for (final e in me.legalEntities) Option(e.id, e.name)], required: true, initial: me.defaultEntity?.id),
      Field.text('fullName', 'Full name', required: true, maxLength: 150),
      Field.text('companyName', 'Company', maxLength: 150),
      Field.text('jobTitle', 'Job title', maxLength: 100),
      Field.text('phone', 'Phone', keyboard: TextInputType.phone, maxLength: 50),
      Field.text('email', 'Email', keyboard: TextInputType.emailAddress, maxLength: 150),
      Field.select('source', 'Source', options: leadSources, required: true, initial: 'website'),
      Field.money('estimatedValue', 'Estimated value', initial: '0'),
      Field.multiline('notes', 'Notes', maxLength: 1000),
    ],
    submit: (ref, body, raw) async {
      final api = ref.read(apiProvider);
      if (!editing) return api.post('$_base/leads', body: body);
      return api.put('$_base/leads/${existing['id']}', body: {...body, 'id': existing['id'], 'status': existing['status']});
    },
  );
}

FormSpec convertLeadForm(Json lead) => FormSpec(
      title: 'Convert ${lead['leadNumber']}',
      intro: 'Creates a customer and an opportunity from this lead.',
      submitLabel: 'Convert',
      initial: {'opportunityTitle': 'Opportunity - ${lead['companyName'] ?? lead['fullName']}', 'expectedRevenue': lead['estimatedValue']?.toString()},
      fields: [
        Field.text('opportunityTitle', 'Opportunity title', required: true, maxLength: 200),
        Field.money('expectedRevenue', 'Expected revenue', required: true),
        Field.date('expectedCloseDate', 'Expected close date'),
      ],
      submit: (ref, body, raw) => ref.read(apiProvider).post('$_base/leads/${lead['id']}/convert', body: {...body, 'id': lead['id']}),
    );

Widget leadTile(BuildContext context, Json l) {
  final status = (l['status'] ?? '').toString();
  return ListTile(
    onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => LeadDetailScreen(id: l['id'].toString()))),
    title: Text('${l['fullName']}', maxLines: 1, overflow: TextOverflow.ellipsis),
    subtitle: Text('${l['leadNumber']} · ${l['companyName'] ?? optionLabel(leadSources, l['source'])}'),
    trailing: Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.end, children: [
      Text(formatMoney((l['estimatedValue'] as num?) ?? 0), style: const TextStyle(fontWeight: FontWeight.w600)),
      const SizedBox(height: 4),
      statusChip(status, leadStatuses),
    ]),
  );
}

class LeadsScreen extends StatelessWidget {
  const LeadsScreen({super.key});
  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Leads',
        path: '$_base/leads',
        searchHint: 'Search leads',
        emptyText: 'No leads found.',
        itemBuilder: leadTile,
        filters: leadStatuses,
        create: (ref) => leadForm(ref),
        createPermission: 'crm.leads.manage',
      );
}

Future<Object?> _setLeadStatus(WidgetRef ref, Json d, String status) => ref.read(apiProvider).put('$_base/leads/${d['id']}', body: {
      'id': d['id'],
      'fullName': d['fullName'],
      'companyName': d['companyName'],
      'jobTitle': d['jobTitle'],
      'email': d['email'],
      'phone': d['phone'],
      'source': d['source'],
      'status': status,
      'estimatedValue': d['estimatedValue'],
      'assignedUserId': d['assignedUserId'],
      'assignedUserName': d['assignedUserName'],
      'notes': d['notes'],
      'customFields': d['customFields'],
    });

class LeadDetailScreen extends StatelessWidget {
  const LeadDetailScreen({super.key, required this.id});
  final String id;

  @override
  Widget build(BuildContext context) => Consumer(builder: (context, ref, _) {
        return DetailPage(
          path: '$_base/leads/$id',
          titleOf: (d) => '${d['leadNumber']}',
          canEdit: (d) => d['status'] != 'converted' && ref.read(meOrEmptyProvider).can('crm.leads.manage'),
          edit: (d) => leadForm(ref, existing: d),
          body: (context, d) => DetailBody(children: [
            DetailHeader(title: '${d['fullName']}', subtitle: [d['jobTitle'], d['companyName']].where((e) => e != null && '$e'.isNotEmpty).join(' · ')),
            Align(alignment: Alignment.centerLeft, child: statusChip('${d['status']}', leadStatuses)),
            const SizedBox(height: 8),
            InfoCard(rows: [
              InfoRow('Phone', text(d['phone'])),
              InfoRow('Email', text(d['email'])),
              InfoRow('Source', optionLabel(leadSources, d['source'])),
              InfoRow('Estimated value', formatMoney((d['estimatedValue'] as num?) ?? 0), bold: true),
              InfoRow('Assigned to', text(d['assignedUserName'])),
              InfoRow('Converted', d['convertedAt'] == null ? null : formatDate(d['convertedAt'])),
            ]),
            if ((d['notes'] ?? '').toString().isNotEmpty) ...[Text('Notes', style: Theme.of(context).textTheme.titleMedium), Text(d['notes'].toString()), const SizedBox(height: 12)],
            ActivityList(kind: 'Lead', entityId: id),
          ]),
          actions: [
            ActionSpec(
              label: 'Contacted',
              icon: Icons.phone_in_talk_outlined,
              permission: 'crm.leads.manage',
              visible: (d) => d['status'] == 'new',
              successMessage: 'Marked as contacted',
              run: (ref, d, _) => _setLeadStatus(ref, d, 'contacted'),
            ),
            ActionSpec(
              label: 'Qualify',
              icon: Icons.verified_outlined,
              permission: 'crm.leads.manage',
              visible: (d) => const {'new', 'contacted', 'unqualified'}.contains(d['status']),
              confirm: 'Mark this lead as qualified?',
              successMessage: 'Lead qualified',
              run: (ref, d, _) => _setLeadStatus(ref, d, 'qualified'),
            ),
            ActionSpec(
              label: 'Unqualify',
              icon: Icons.thumb_down_outlined,
              permission: 'crm.leads.manage',
              destructive: true,
              visible: (d) => const {'new', 'contacted', 'qualified'}.contains(d['status']),
              confirm: 'Mark this lead as unqualified? It cannot be converted until requalified.',
              successMessage: 'Lead unqualified',
              run: (ref, d, _) => _setLeadStatus(ref, d, 'unqualified'),
            ),
            ActionSpec(
              label: 'Convert',
              icon: Icons.swap_horiz,
              permission: 'crm.leads.manage',
              visible: (d) => const {'new', 'contacted', 'qualified'}.contains(d['status']),
              run: (ref, d, _) async => null,
              form: convertLeadForm,
            ),
            ActionSpec(
              label: 'Log activity',
              icon: Icons.add_call,
              permission: 'crm.interactions.manage',
              run: (ref, d, _) async => null,
              form: (d) => interactionForm(ref, kind: 'Lead', entityId: id, entityLabel: '${d['fullName']}', legalEntityId: d['legalEntityId']?.toString()),
            ),
          ],
        );
      });
}

// ---------------------------------------------------------------- opportunities

FormSpec opportunityForm(WidgetRef ref) {
  final me = ref.read(meOrEmptyProvider);
  return FormSpec(
    title: 'New opportunity',
    fields: [
      Field.select('legalEntityId', 'Legal entity', options: [for (final e in me.legalEntities) Option(e.id, e.name)], required: true, initial: me.defaultEntity?.id),
      Field.text('title', 'Title', required: true, maxLength: 200),
      Field.lookup('customerPartyId', 'Customer (optional)', path: '/api/v1/parties', query: const {'role': 'Customer'}, subtitleKey: 'code', copy: const {'customerName': 'name'}),
      Field.text('customerName', 'Customer name', required: true, maxLength: 150),
      Field.select('stage', 'Stage', options: opportunityStages.take(3).toList(), required: true, initial: 'qualification'),
      Field.integer('probabilityPercent', 'Probability %', required: true, min: 0, max: 100, initial: '20'),
      Field.money('expectedRevenue', 'Expected revenue', required: true),
      Field.date('expectedCloseDate', 'Expected close date'),
      Field.multiline('notes', 'Notes', maxLength: 1000),
    ],
    submit: (ref, body, raw) => ref.read(apiProvider).post('$_base/opportunities', body: body),
  );
}

FormSpec closeOpportunityForm(Json d, {required bool won}) => FormSpec(
      title: won ? 'Close as won' : 'Close as lost',
      submitLabel: won ? 'Close won' : 'Close lost',
      initial: {'actualRevenue': won ? d['expectedRevenue']?.toString() : '0'},
      fields: [
        if (won) Field.money('actualRevenue', 'Actual revenue', required: true),
        Field.text('closeReason', won ? 'Notes (optional)' : 'Reason lost', required: !won, maxLength: 500),
      ],
      submit: (ref, body, raw) =>
          ref.read(apiProvider).post('$_base/opportunities/${d['id']}/close', body: {'id': d['id'], 'isWon': won, 'actualRevenue': won ? body['actualRevenue'] : 0, 'closeReason': body['closeReason']}),
    );

Widget opportunityTile(BuildContext context, Json o) {
  final stage = (o['stage'] ?? '').toString();
  return ListTile(
    onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => OpportunityDetailScreen(id: o['id'].toString()))),
    title: Text('${o['title']}', maxLines: 1, overflow: TextOverflow.ellipsis),
    subtitle: Text('${o['customerName']}\n${o['probabilityPercent']}%${o['expectedCloseDate'] == null ? '' : ' · close ${formatDate(o['expectedCloseDate'])}'}'),
    isThreeLine: true,
    trailing: Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.end, children: [
      Text(formatMoney((o['expectedRevenue'] as num?) ?? 0), style: const TextStyle(fontWeight: FontWeight.w600)),
      const SizedBox(height: 4),
      statusChip(stage, opportunityStages),
    ]),
  );
}

class OpportunitiesScreen extends StatelessWidget {
  const OpportunitiesScreen({super.key});
  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Sales pipeline',
        path: '$_base/opportunities',
        filterKey: 'stage',
        searchHint: 'Search opportunities',
        emptyText: 'No opportunities found.',
        itemBuilder: opportunityTile,
        filters: opportunityStages,
        create: (ref) => opportunityForm(ref),
        createPermission: 'crm.opportunities.manage',
      );
}

ActionSpec _stageAction(String target) => ActionSpec(
      label: target == 'qualification' ? 'Back to qualification' : 'Move to ${optionLabel(opportunityStages, target)}',
      icon: Icons.arrow_forward,
      permission: 'crm.opportunities.manage',
      visible: (d) => (stageMoves[d['stage']] ?? const []).contains(target),
      successMessage: 'Moved to ${optionLabel(opportunityStages, target)}',
      run: (ref, d, _) => ref.read(apiProvider).post('$_base/opportunities/${d['id']}/stage', body: {'id': d['id'], 'stage': target, 'probabilityPercent': d['probabilityPercent'] ?? 0}),
    );

class OpportunityDetailScreen extends StatelessWidget {
  const OpportunityDetailScreen({super.key, required this.id});
  final String id;

  @override
  Widget build(BuildContext context) => Consumer(builder: (context, ref, _) {
        return DetailPage(
          path: '$_base/opportunities/$id',
          titleOf: (d) => '${d['opportunityNumber']}',
          body: (context, d) => DetailBody(children: [
            DetailHeader(title: '${d['title']}', subtitle: '${d['customerName']}'),
            Align(alignment: Alignment.centerLeft, child: statusChip('${d['stage']}', opportunityStages)),
            const SizedBox(height: 8),
            InfoCard(rows: [
              InfoRow('Probability', '${d['probabilityPercent']}%'),
              InfoRow('Expected revenue', formatMoney((d['expectedRevenue'] as num?) ?? 0), bold: true),
              InfoRow('Actual revenue', d['stage'] == 'closedWon' ? formatMoney((d['actualRevenue'] as num?) ?? 0) : null),
              InfoRow('Expected close', d['expectedCloseDate'] == null ? null : formatDate(d['expectedCloseDate'])),
              InfoRow('Closed on', d['actualCloseDate'] == null ? null : formatDate(d['actualCloseDate'])),
              InfoRow('Close reason', text(d['closeReason'])),
              InfoRow('Assigned to', text(d['assignedUserName'])),
            ]),
            if ((d['notes'] ?? '').toString().isNotEmpty) ...[Text('Notes', style: Theme.of(context).textTheme.titleMedium), Text(d['notes'].toString()), const SizedBox(height: 12)],
            ActivityList(kind: 'Opportunity', entityId: id),
          ]),
          actions: [
            _stageAction('proposal'),
            _stageAction('negotiation'),
            _stageAction('qualification'),
            ActionSpec(
              label: 'Close won',
              icon: Icons.emoji_events_outlined,
              permission: 'crm.opportunities.manage',
              visible: (d) => d['stage'] == 'negotiation',
              run: (ref, d, _) async => null,
              form: (d) => closeOpportunityForm(d, won: true),
            ),
            ActionSpec(
              label: 'Close lost',
              icon: Icons.cancel_outlined,
              permission: 'crm.opportunities.manage',
              destructive: true,
              visible: (d) => const {'qualification', 'proposal', 'negotiation'}.contains(d['stage']),
              run: (ref, d, _) async => null,
              form: (d) => closeOpportunityForm(d, won: false),
            ),
            ActionSpec(
              label: 'Log activity',
              icon: Icons.add_call,
              permission: 'crm.interactions.manage',
              run: (ref, d, _) async => null,
              form: (d) => interactionForm(ref, kind: 'Opportunity', entityId: id, entityLabel: '${d['title']}', legalEntityId: d['legalEntityId']?.toString()),
            ),
          ],
        );
      });
}

// ---------------------------------------------------------------- interactions

/// Log a call / visit / note. With [kind] and [entityId] it is bound to that record; otherwise the user picks one.
FormSpec interactionForm(WidgetRef ref, {String? kind, String? entityId, String? entityLabel, String? legalEntityId}) {
  final me = ref.read(meOrEmptyProvider);
  final fixed = kind != null && entityId != null;
  return FormSpec(
    title: 'Log activity',
    intro: fixed ? 'For ${kind.toLowerCase()} $entityLabel' : null,
    submitLabel: 'Log',
    fields: [
      if (!fixed) ...[
        Field.select('relatedEntityKind', 'Related to', options: const [Option('Lead', 'Lead'), Option('Opportunity', 'Opportunity'), Option('Customer', 'Customer')], required: true, initial: 'Lead'),
        Field.lookup('leadId', 'Lead', path: '$_base/leads', labelKey: 'fullName', subtitleKey: 'leadNumber', visibleWhen: (v) => v['relatedEntityKind'] == 'Lead'),
        Field.lookup('oppId', 'Opportunity', path: '$_base/opportunities', labelKey: 'title', subtitleKey: 'opportunityNumber', visibleWhen: (v) => v['relatedEntityKind'] == 'Opportunity'),
        Field.lookup('partyId', 'Customer', path: '/api/v1/parties', query: const {'role': 'Customer'}, subtitleKey: 'code', visibleWhen: (v) => v['relatedEntityKind'] == 'Customer'),
        if (legalEntityId == null) Field.select('legalEntityId', 'Legal entity', options: [for (final e in me.legalEntities) Option(e.id, e.name)], required: true, initial: me.defaultEntity?.id),
      ],
      Field.select('type', 'Type', options: interactionTypes, required: true, initial: 'call'),
      Field.text('subject', 'Subject', required: true, maxLength: 200),
      Field.multiline('details', 'Details', maxLength: 2000),
      Field.date('followUpDate', 'Follow-up date'),
      Field.toggle('isResolved', 'Done (no follow-up needed)', initial: true),
    ],
    submit: (ref, body, raw) {
      final map = <String, dynamic>{...body};
      map['legalEntityId'] = legalEntityId ?? body['legalEntityId'] ?? me.defaultEntity?.id;
      if (fixed) {
        map['relatedEntityKind'] = kind;
        map['relatedEntityId'] = entityId;
      } else {
        final k = body['relatedEntityKind'];
        final target = k == 'Lead' ? body['leadId'] : (k == 'Opportunity' ? body['oppId'] : body['partyId']);
        if (target == null) throw ApiException(422, 'Choose the ${k.toString().toLowerCase()} this activity is about.');
        map['relatedEntityId'] = target;
        map
          ..remove('leadId')
          ..remove('oppId')
          ..remove('partyId');
      }
      return ref.read(apiProvider).post('$_base/interactions', body: map);
    },
  );
}

Widget interactionTile(BuildContext context, Json i) {
  final resolved = i['isResolved'] == true;
  return ListTile(
    leading: Icon(switch (i['type']) { 'call' => Icons.call, 'meeting' => Icons.groups_outlined, 'email' => Icons.email_outlined, 'task' => Icons.task_alt, _ => Icons.sticky_note_2_outlined }),
    title: Text('${i['subject']}', maxLines: 1, overflow: TextOverflow.ellipsis),
    subtitle: Text('${optionLabel(interactionTypes, i['type'])} · ${i['relatedEntityKind']} · ${formatDate(i['interactionDate'])}'),
    trailing: StatusChip(resolved ? 'Done' : 'Open', color: resolved ? Colors.green.shade700 : Colors.orange.shade800),
    onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => InteractionDetailScreen(data: i))),
  );
}

class InteractionsScreen extends StatelessWidget {
  const InteractionsScreen({super.key});
  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Activity logs',
        path: '$_base/interactions',
        filterKey: 'type',
        searchHint: 'Search activities',
        emptyText: 'No activities logged.',
        itemBuilder: interactionTile,
        filters: interactionTypes,
        create: (ref) => interactionForm(ref),
        createPermission: 'crm.interactions.manage',
      );
}

/// The API has no single-interaction read, so the screen shows the list row it was opened with.
class InteractionDetailScreen extends ConsumerStatefulWidget {
  const InteractionDetailScreen({super.key, required this.data});
  final Json data;

  @override
  ConsumerState<InteractionDetailScreen> createState() => _InteractionDetailState();
}

class _InteractionDetailState extends ConsumerState<InteractionDetailScreen> {
  late bool _resolved = widget.data['isResolved'] == true;
  bool _busy = false;

  Future<void> _resolve() async {
    final messenger = ScaffoldMessenger.of(context);
    setState(() => _busy = true);
    try {
      await ref.read(apiProvider).post('$_base/interactions/${widget.data['id']}/resolve');
      ref.read(dataVersionProvider.notifier).bump();
      messenger.showSnackBar(const SnackBar(content: Text('Marked as done')));
      if (mounted) setState(() => _resolved = true);
    } on ApiException catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(e.message)));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final i = widget.data;
    final me = ref.watch(meOrEmptyProvider);
    return Scaffold(
      appBar: AppBar(title: const Text('Activity')),
      body: DetailBody(children: [
        DetailHeader(title: '${i['subject']}', subtitle: optionLabel(interactionTypes, i['type'])),
        InfoCard(rows: [
          InfoRow('Related to', '${i['relatedEntityKind']}'),
          InfoRow('Date', formatDate(i['interactionDate'])),
          InfoRow('By', text(i['performedByName'])),
          InfoRow('Follow-up', i['followUpDate'] == null ? null : formatDate(i['followUpDate'])),
          InfoRow('Status', _resolved ? 'Done' : 'Open', bold: true),
        ]),
        if ((i['details'] ?? '').toString().isNotEmpty) Text(i['details'].toString()),
      ]),
      bottomNavigationBar: !_resolved && me.can('crm.interactions.manage')
          ? SafeArea(
              child: Padding(
                padding: const EdgeInsets.fromLTRB(12, 6, 12, 10),
                child: FilledButton.icon(onPressed: _busy ? null : _resolve, icon: const Icon(Icons.check), label: const Text('Mark done')),
              ),
            )
          : null,
    );
  }
}

/// The activity history of a lead / opportunity / customer, shown inside detail screens.
class ActivityList extends ConsumerStatefulWidget {
  const ActivityList({super.key, required this.kind, required this.entityId});
  final String kind;
  final String entityId;

  @override
  ConsumerState<ActivityList> createState() => _ActivityListState();
}

class _ActivityListState extends ConsumerState<ActivityList> {
  List<Json>? _items;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final r = await ref.read(apiProvider).get('$_base/interactions/by-entity', query: {'kind': widget.kind, 'id': widget.entityId});
      if (mounted) setState(() => _items = asItems(r));
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    }
  }

  @override
  Widget build(BuildContext context) {
    ref.listen(dataVersionProvider, (_, _) => _load());
    final items = _items;
    return Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
      Text('Activity', style: Theme.of(context).textTheme.titleMedium),
      if (_error != null)
        Text(_error!)
      else if (items == null)
        const Padding(padding: EdgeInsets.all(8), child: LinearProgressIndicator())
      else if (items.isEmpty)
        const Text('No activity logged yet.'),
      for (final i in items ?? const <Json>[])
        ListTile(
          contentPadding: EdgeInsets.zero,
          dense: true,
          title: Text('${i['subject']}'),
          subtitle: Text('${optionLabel(interactionTypes, i['type'])} · ${formatDate(i['interactionDate'])}'),
        ),
    ]);
  }
}
