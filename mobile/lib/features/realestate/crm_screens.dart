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
import 'common.dart';
import 'package:erp_mobile/theme/tokens.dart';

const _listingsPath = '$rePath/listings';
const _leadsPath = '$rePath/leads';
const _dealsPath = '$rePath/deals';

// ---- Listings ---------------------------------------------------------------------------------------------------------------------

const listingKinds = [Option('rent', 'For rent'), Option('sale', 'For sale')];
const listingStatuses = [Option('draft', 'Draft'), Option('published', 'Published'), Option('closed', 'Closed')];

FormSpec listingForm(WidgetRef ref, {Json? existing}) {
  final editing = existing != null;
  return FormSpec(
    title: editing ? 'Edit listing' : 'New listing',
    initial: editing ? {for (final k in ['propertyId', 'unitId', 'kind', 'title', 'price', 'channels', 'description']) k: existing[k]} : const {},
    fields: [
      if (!editing) propertyLookup(),
      unitLookup(),
      Field.select('kind', 'Kind', options: listingKinds, required: true, initial: 'rent'),
      Field.text('title', 'Title', required: true, maxLength: 200),
      Field.money('price', 'Price', required: true),
      Field.text('channels', 'Channels (Bayut, Website...)', maxLength: 200),
      Field.multiline('description', 'Description', maxLength: 2000),
    ],
    submit: (ref, body, raw) async {
      final api = ref.read(apiProvider);
      if (editing) body['propertyId'] = existing['propertyId'];
      return editing ? api.put('$_listingsPath/${existing['id']}', body: body) : api.post(_listingsPath, body: body);
    },
  );
}

Widget listingTile(BuildContext context, Json l) => reTile(
      context,
      title: '${l['title']}',
      subtitle: '${humanize(l['kind']?.toString())}${(l['channels'] ?? '').toString().isEmpty ? '' : ' · ${l['channels']}'}',
      trailing: formatMoney((l['price'] as num?) ?? 0),
      status: l['status'],
      onTap: () => reGo(context, ListingDetailScreen(id: l['id'].toString())),
    );

class ListingsScreen extends StatelessWidget {
  const ListingsScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Listings',
        path: _listingsPath,
        searchHint: 'Search listings',
        emptyText: 'No listings found.',
        itemBuilder: listingTile,
        filters: listingStatuses,
        create: (ref) => listingForm(ref),
        createPermission: 'realestate.crm.manage',
      );
}

class ListingDetailScreen extends StatelessWidget {
  const ListingDetailScreen({super.key, required this.id});

  final String id;

  static ActionSpec _status(String label, IconData icon, String to, bool Function(Json) visible, {String? confirm}) => ActionSpec(
        label: label,
        icon: icon,
        permission: 'realestate.crm.manage',
        visible: visible,
        confirm: confirm,
        run: (ref, d, _) => ref.read(apiProvider).post('$_listingsPath/${d['id']}/status', body: {'status': to}),
      );

  @override
  Widget build(BuildContext context) {
    return Consumer(builder: (context, ref, _) {
      return DetailPage(
        path: '$_listingsPath/$id',
        titleOf: (d) => '${d['title']}',
        canEdit: (d) => d['status'] != 'closed' && reCan(ref, 'realestate.crm.manage'),
        edit: (d) => listingForm(ref, existing: d),
        body: (context, d) => DetailBody(children: [
          DetailHeader(title: '${d['title']}', subtitle: humanize(d['kind']?.toString()), status: d['status']?.toString()),
          InfoCard(rows: [
            InfoRow('Price', formatMoney((d['price'] as num?) ?? 0), bold: true),
            InfoRow('Channels', text(d['channels'])),
            InfoRow('Listed on', d['listedOn'] == null ? null : formatDate(d['listedOn'])),
            InfoRow('Closed on', d['closedOn'] == null ? null : formatDate(d['closedOn'])),
          ]),
          if ((d['description'] ?? '').toString().isNotEmpty) Text(d['description'].toString()),
        ]),
        actions: [
          _status('Publish', Icons.publish, 'published', (d) => d['status'] == 'draft'),
          _status('Close', Icons.check_circle_outline, 'closed', (d) => d['status'] == 'published', confirm: 'Close this listing (let or sold)?'),
          _status('Unpublish', Icons.unpublished_outlined, 'draft', (d) => d['status'] == 'published'),
          _status('Reopen as draft', Icons.replay, 'draft', (d) => d['status'] == 'closed'),
        ],
      );
    });
  }
}

// ---- Leads ------------------------------------------------------------------------------------------------------------------------

const leadStages = [Option('new', 'New'), Option('contacted', 'Contacted'), Option('viewing', 'Viewing'), Option('negotiating', 'Negotiating'), Option('won', 'Won'), Option('lost', 'Lost')];

FormSpec leadForm(WidgetRef ref, {Json? existing}) {
  final me = ref.read(meOrEmptyProvider);
  final editing = existing != null;
  return FormSpec(
    title: editing ? 'Edit ${existing['name']}' : 'New lead',
    initial: editing ? {for (final k in ['legalEntityId', 'name', 'phone', 'email', 'source', 'interest', 'agentPartyId', 'listingId', 'unitId', 'notes', 'followUpOn']) k: existing[k]} : const {},
    fields: [
      Field.select('legalEntityId', 'Legal entity', options: entityOptions(me), required: true, initial: me.defaultEntity?.id),
      Field.text('name', 'Name', required: true, maxLength: 150),
      Field.text('phone', 'Phone', maxLength: 50, keyboard: TextInputType.phone),
      Field.text('email', 'Email', maxLength: 150, keyboard: TextInputType.emailAddress),
      Field.text('source', 'Source (website, portal, referral...)', maxLength: 100),
      Field.text('interest', 'Interested in', maxLength: 300),
      Field.lookup('listingId', 'Listing', path: _listingsPath, labelKey: 'title'),
      unitLookup(),
      Field.lookup('agentPartyId', 'Agent', path: '/api/v1/parties', subtitleKey: 'code'),
      Field.date('followUpOn', 'Follow up on'),
      Field.multiline('notes', 'Notes', maxLength: 1000),
    ],
    submit: (ref, body, raw) async {
      final api = ref.read(apiProvider);
      return editing ? api.put('$_leadsPath/${existing['id']}', body: body) : api.post(_leadsPath, body: body);
    },
  );
}

Widget leadTile(BuildContext context, Json l) => reTile(
      context,
      title: '${l['name']}',
      subtitle: '${(l['interest'] ?? l['source'] ?? '')}${l['followUpOn'] == null ? '' : '\nFollow up ${formatDate(l['followUpOn'])}'}',
      status: l['stage'],
      onTap: () => reGo(context, LeadDetailScreen(id: l['id'].toString())),
    );

class LeadsScreen extends StatelessWidget {
  const LeadsScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Leads',
        path: _leadsPath,
        searchHint: 'Search leads',
        emptyText: 'No leads found.',
        itemBuilder: leadTile,
        filters: leadStages,
        filterKey: 'stage',
        create: (ref) => leadForm(ref),
        createPermission: 'realestate.crm.manage',
      );
}

class LeadDetailScreen extends StatelessWidget {
  const LeadDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) {
    return Consumer(builder: (context, ref, _) {
      return DetailPage(
        path: '$_leadsPath/$id',
        titleOf: (d) => '${d['name']}',
        canEdit: (d) => d['stage'] != 'won' && reCan(ref, 'realestate.crm.manage'),
        edit: (d) => leadForm(ref, existing: d),
        body: (context, d) => DetailBody(children: [
          DetailHeader(title: '${d['name']}', subtitle: text(d['interest']), status: d['stage']?.toString()),
          InfoCard(rows: [
            InfoRow('Phone', text(d['phone'])),
            InfoRow('Email', text(d['email'])),
            InfoRow('Source', text(d['source'])),
            InfoRow('Follow up', d['followUpOn'] == null ? null : formatDate(d['followUpOn'])),
            InfoRow('Lost because', text(d['lostReason'])),
          ]),
          if ((d['notes'] ?? '').toString().isNotEmpty) Text(d['notes'].toString()),
          if (d['dealId'] != null) ...[
            const SizedBox(height: ErpSpace.s3),
            OutlinedButton.icon(onPressed: () => reGo(context, DealDetailScreen(id: d['dealId'].toString())), icon: const Icon(Icons.handshake_outlined), label: const Text('Open the deal')),
          ],
        ]),
        actions: [
          ActionSpec(
            label: 'Move stage',
            icon: Icons.arrow_forward,
            permission: 'realestate.crm.manage',
            visible: (d) => const {'new', 'contacted', 'viewing', 'negotiating'}.contains(d['stage']),
            form: (d) => FormSpec(
              title: 'Move lead',
              fields: [
                Field.select('stage', 'New stage', options: [for (final o in leadStages) if (o.value != d['stage'] && o.value != 'won' && o.value != 'lost') o], required: true),
              ],
              submit: (ref, body, raw) => ref.read(apiProvider).post('$_leadsPath/$id/stage', body: body),
            ),
            run: (ref, d, _) async => null,
          ),
          ActionSpec(
            label: 'Convert to deal',
            icon: Icons.handshake_outlined,
            permission: 'realestate.crm.manage',
            visible: (d) => const {'new', 'contacted', 'viewing', 'negotiating'}.contains(d['stage']),
            form: (d) => FormSpec(
              title: 'Convert to deal',
              submitLabel: 'Convert',
              intro: 'Creates a brokerage deal and marks the lead as won. Leave the type and value empty to take them from the lead\'s listing.',
              fields: [
                Field.select('dealType', 'Deal type', options: const [Option('sale', 'Sale'), Option('lease', 'Lease')]),
                Field.money('dealValue', 'Deal value'),
                Field.decimal('commissionRatePercent', 'Commission %', min: 0, max: 100, decimals: 2),
              ],
              submit: (ref, body, raw) => ref.read(apiProvider).post('$_leadsPath/$id/convert', body: body),
            ),
            run: (ref, d, _) async => null,
          ),
          ActionSpec(
            label: 'Mark lost',
            icon: Icons.thumb_down_alt_outlined,
            permission: 'realestate.crm.manage',
            destructive: true,
            visible: (d) => const {'new', 'contacted', 'viewing', 'negotiating'}.contains(d['stage']),
            form: (d) => FormSpec(
              title: 'Mark lead as lost',
              submitLabel: 'Mark lost',
              fields: [Field.text('lostReason', 'Reason', required: true, maxLength: 300)],
              submit: (ref, body, raw) => ref.read(apiProvider).post('$_leadsPath/$id/stage', body: {'stage': 'lost', ...body}),
            ),
            run: (ref, d, _) async => null,
          ),
          ActionSpec(
            label: 'Reopen',
            icon: Icons.replay,
            permission: 'realestate.crm.manage',
            visible: (d) => d['stage'] == 'lost',
            run: (ref, d, _) => ref.read(apiProvider).post('$_leadsPath/$id/stage', body: {'stage': 'new'}),
          ),
        ],
      );
    });
  }
}

// ---- Deals ------------------------------------------------------------------------------------------------------------------------

const dealStages = [
  Option('lead', 'Lead'),
  Option('viewing', 'Viewing'),
  Option('negotiation', 'Negotiation'),
  Option('agreed', 'Agreed'),
  Option('invoiced', 'Invoiced'),
  Option('collected', 'Collected'),
  Option('agentPaid', 'Agent paid'),
  Option('lost', 'Lost'),
];
const _forward = {'lead': 'viewing', 'viewing': 'negotiation', 'negotiation': 'agreed'};

FormSpec dealForm(WidgetRef ref, {Json? existing}) {
  final me = ref.read(meOrEmptyProvider);
  final editing = existing != null;
  return FormSpec(
    title: editing ? 'Edit ${existing['number'] ?? 'deal'}' : 'New deal',
    initial: editing
        ? {
            for (final k in ['legalEntityId', 'dealType', 'clientName', 'dealDate', 'dealValue', 'commissionRatePercent', 'propertyId', 'unitReference', 'clientPartyId', 'agentPartyId', 'externalAgentName', 'agentSharePercent', 'teamLeadPartyId', 'teamLeadSharePercent', 'notes'])
              k: existing[k],
          }
        : const {},
    fields: [
      Field.select('legalEntityId', 'Legal entity', options: entityOptions(me), required: true, initial: me.defaultEntity?.id),
      Field.select('dealType', 'Deal type', options: const [Option('sale', 'Sale'), Option('lease', 'Lease')], required: true, initial: 'sale'),
      Field.text('clientName', 'Client name', required: true, maxLength: 200),
      Field.lookup('clientPartyId', 'Client record (needed to invoice the commission)', path: '/api/v1/parties', subtitleKey: 'code'),
      Field.date('dealDate', 'Deal date', required: true, initial: 'today'),
      Field.money('dealValue', 'Deal value', required: true),
      Field.decimal('commissionRatePercent', 'Commission %', required: true, min: 0, max: 100, decimals: 2),
      propertyLookup(required: false),
      Field.text('unitReference', 'Unit reference', maxLength: 100),
      Field.lookup('agentPartyId', 'Agent', path: '/api/v1/parties', subtitleKey: 'code'),
      Field.text('externalAgentName', 'External agent', maxLength: 150),
      Field.decimal('agentSharePercent', 'Agent share %', min: 0, max: 100, decimals: 2, initial: '0'),
      Field.lookup('teamLeadPartyId', 'Team lead', path: '/api/v1/parties', subtitleKey: 'code'),
      Field.decimal('teamLeadSharePercent', 'Team lead share %', min: 0, max: 100, decimals: 2, initial: '0'),
      Field.multiline('notes', 'Notes', maxLength: 1000),
    ],
    submit: (ref, body, raw) async {
      final api = ref.read(apiProvider);
      return editing ? api.put('$_dealsPath/${existing['id']}', body: body) : api.post(_dealsPath, body: body);
    },
  );
}

Widget dealTile(BuildContext context, Json d) => reTile(
      context,
      title: '${d['clientName']}',
      subtitle: '${d['number'] ?? ''} · ${humanize(d['dealType']?.toString())}\nCommission ${formatMoney((d['grossCommission'] as num?) ?? 0)}',
      trailing: money(d, 'dealValue'),
      status: d['stage'],
      onTap: () => reGo(context, DealDetailScreen(id: d['id'].toString())),
    );

class DealsScreen extends StatelessWidget {
  const DealsScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Deals',
        path: _dealsPath,
        searchHint: 'Search deals',
        emptyText: 'No deals found.',
        itemBuilder: dealTile,
        filters: dealStages,
        filterKey: 'stage',
        create: (ref) => dealForm(ref),
        createPermission: 'realestate.deals.create',
      );
}

class DealDetailScreen extends StatelessWidget {
  const DealDetailScreen({super.key, required this.id});

  final String id;

  static bool _editable(Json d) => d['stage'] != 'lost' && !const {'invoiced', 'collected', 'agentPaid'}.contains(d['stage']);

  @override
  Widget build(BuildContext context) {
    return Consumer(builder: (context, ref, _) {
      return DetailPage(
        path: '$_dealsPath/$id',
        titleOf: (d) => (d['number'] ?? 'Deal').toString(),
        canEdit: (d) => _editable(d) && reCan(ref, 'realestate.deals.edit'),
        edit: (d) => dealForm(ref, existing: d),
        body: (context, d) => DetailBody(children: [
          DetailHeader(title: '${d['clientName']}', subtitle: '${d['number'] ?? ''} · ${humanize(d['dealType']?.toString())} · ${formatDate(d['dealDate'])}', status: d['stage']?.toString()),
          InfoCard(rows: [
            InfoRow('Deal value', money(d, 'dealValue'), bold: true),
            InfoRow('Commission rate', '${d['commissionRatePercent']}%'),
            InfoRow('Gross commission', money(d, 'grossCommission'), bold: true),
            InfoRow('Agent (${d['agentSharePercent']}%)', money(d, 'agentCommission')),
            InfoRow('Team lead (${d['teamLeadSharePercent']}%)', money(d, 'teamLeadCommission')),
            InfoRow('Unit', text(d['unitReference'])),
            InfoRow('External agent', text(d['externalAgentName'])),
            InfoRow('Agent paid on', d['agentPaidOn'] == null ? null : formatDate(d['agentPaidOn'])),
            InfoRow('Lost because', text(d['lostReason'])),
          ]),
          if ((d['notes'] ?? '').toString().isNotEmpty) Text(d['notes'].toString()),
        ]),
        actions: [
          for (final e in _forward.entries)
            ActionSpec(
              label: 'Move to ${humanize(e.value)}',
              icon: Icons.arrow_forward,
              permission: 'realestate.deals.edit',
              visible: (d) => d['stage'] == e.key,
              run: (ref, d, _) => ref.read(apiProvider).post('$_dealsPath/$id/stage', body: {'stage': e.value}),
            ),
          ActionSpec(
            label: 'Invoice commission',
            icon: Icons.receipt_long_outlined,
            permission: 'realestate.deals.invoice',
            visible: (d) => d['stage'] == 'agreed',
            confirm: 'Raise the commission invoice for this deal?',
            successMessage: 'Commission invoice raised',
            run: (ref, d, _) => ref.read(apiProvider).post('$_dealsPath/$id/invoice-commission'),
          ),
          ActionSpec(
            label: 'Pay agent',
            icon: Icons.payments_outlined,
            permission: 'realestate.deals.payagent',
            visible: (d) => d['stage'] == 'collected',
            form: (d) => FormSpec(
              title: 'Pay agent commission',
              submitLabel: 'Pay',
              fields: [bankLookup(label: 'Pay from account'), Field.date('date', 'Date', required: true, initial: 'today')],
              submit: (ref, body, raw) => ref.read(apiProvider).post('$_dealsPath/$id/pay-agent', body: body),
            ),
            run: (ref, d, _) async => null,
          ),
          ActionSpec(
            label: 'Mark lost',
            icon: Icons.thumb_down_alt_outlined,
            permission: 'realestate.deals.lose',
            destructive: true,
            visible: (d) => const {'lead', 'viewing', 'negotiation', 'agreed'}.contains(d['stage']),
            reasonLabel: 'Why was the deal lost?',
            run: (ref, d, reason) => ref.read(apiProvider).post('$_dealsPath/$id/stage', body: {'stage': 'lost', 'lostReason': reason}),
          ),
        ],
      );
    });
  }
}
