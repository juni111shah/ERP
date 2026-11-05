import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../../core/format.dart';
import '../../core/session.dart';
import '../../kit/data_version.dart';
import '../../kit/detail_page.dart';
import '../../kit/field.dart';
import '../../kit/form_page.dart';
import '../../kit/list_screen.dart';
import '../../kit/widgets.dart';
import 'common.dart';
import 'maintenance_screens.dart';
import 'package:erp_mobile/theme/tokens.dart';
import '../../kit/skeleton.dart';
import '../../kit/state_view.dart';

const _leasesPath = '$rePath/leases';

const leaseStatuses = [
  Option('draft', 'Draft'),
  Option('active', 'Active'),
  Option('holdingOver', 'Holding over'),
  Option('ended', 'Ended'),
  Option('renewed', 'Renewed'),
  Option('terminated', 'Terminated'),
  Option('cancelled', 'Cancelled'),
];

// ---- Form -------------------------------------------------------------------------------------------------------------------------

FormSpec leaseForm(WidgetRef ref, {Json? existing}) {
  final editing = existing != null;
  return FormSpec(
    title: editing ? 'Edit lease ${existing['number'] ?? ''}'.trim() : 'New lease',
    initial: editing
        ? {
            for (final k in ['unitId', 'lesseePartyId', 'startDate', 'endDate', 'annualRent', 'paymentFrequency', 'securityDepositAmount', 'graceDays', 'noticePeriodDays', 'notes']) k: existing[k],
            '__label_unitId': 'Unit ${existing['unitNumber']}',
            '__label_lesseePartyId': existing['tenantName'],
          }
        : const {},
    fields: [
      unitLookup(required: true, label: 'Unit (vacant or reserved)'),
      tenantLookup(),
      Field.date('startDate', 'Start date', required: true, initial: 'today'),
      Field.date('endDate', 'End date (empty = one year)'),
      Field.money('annualRent', 'Annual rent', required: true),
      Field.integer('paymentFrequency', 'Rent instalments per year (1, 2, 4, 12)', required: true, min: 1, max: 12, initial: '4'),
      Field.money('securityDepositAmount', 'Security deposit', initial: '0'),
      Field.integer('graceDays', 'Grace days', min: 0, initial: '0'),
      Field.integer('noticePeriodDays', 'Notice period (days)', min: 0, initial: '90'),
      Field.multiline('notes', 'Notes', maxLength: 1000),
      if (!editing) Field.toggle('activateNow', 'Activate right away'),
    ],
    submit: (ref, body, raw) async {
      final api = ref.read(apiProvider);
      return editing ? api.put('$_leasesPath/${existing['id']}', body: body) : api.post(_leasesPath, body: body);
    },
  );
}

// ---- List -------------------------------------------------------------------------------------------------------------------------

Widget leaseTile(BuildContext context, Json l) {
  final days = l['daysToExpiry'];
  final expiry = days is int && l['status'] == 'active' ? (days < 0 ? 'expired ${-days} days ago' : 'ends in $days days') : 'ends ${formatDate(l['endDate'])}';
  return reTile(
    context,
    title: '${l['tenantName']}',
    subtitle: '${l['propertyName'] ?? ''} · Unit ${l['unitNumber']}\n${l['number'] ?? 'Draft'} · $expiry',
    trailing: formatMoney((l['annualRent'] as num?) ?? 0),
    status: l['status'],
    onTap: () => reGo(context, LeaseDetailScreen(id: l['id'].toString())),
  );
}

class LeasesScreen extends StatelessWidget {
  const LeasesScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ListScreen(
      title: 'Leases',
      path: _leasesPath,
      searchHint: 'Search tenant, unit or number',
      emptyText: 'No leases found.',
      itemBuilder: leaseTile,
      filters: leaseStatuses,
      create: (ref) => leaseForm(ref),
      createPermission: 'realestate.leases.create',
    );
  }
}

// ---- Detail -----------------------------------------------------------------------------------------------------------------------

FormSpec _post(String title, String path, List<Field> fields, {String submit = 'Save', Map<String, dynamic> initial = const {}, Map<String, dynamic> extra = const {}}) => FormSpec(
      title: title,
      submitLabel: submit,
      initial: initial,
      fields: fields,
      submit: (ref, body, raw) => ref.read(apiProvider).post(path, body: {...body, ...extra}),
    );

class LeaseDetailScreen extends StatelessWidget {
  const LeaseDetailScreen({super.key, required this.id});

  final String id;

  static bool _occupying(Json d) => d['status'] == 'active' || d['status'] == 'holdingOver';
  static bool _closed(Json d) => const {'ended', 'terminated', 'renewed'}.contains(d['status']);

  @override
  Widget build(BuildContext context) {
    return Consumer(builder: (context, ref, _) {
      return DetailPage(
        path: '$_leasesPath/$id',
        titleOf: (d) => (d['number'] ?? 'Draft lease').toString(),
        canEdit: (d) => d['status'] == 'draft' && reCan(ref, 'realestate.leases.edit'),
        edit: (d) => leaseForm(ref, existing: d),
        body: (context, d) => _body(context, ref, d),
        actions: [
          ActionSpec(
            label: 'Activate',
            icon: Icons.check_circle_outline,
            permission: 'realestate.leases.activate',
            visible: (d) => d['status'] == 'draft',
            confirm: 'Activate this lease? The rent schedule becomes live and the unit is occupied.',
            successMessage: 'Lease activated',
            run: (ref, d, _) => ref.read(apiProvider).post('$_leasesPath/$id/activate'),
          ),
          ActionSpec(
            label: 'Renew',
            icon: Icons.autorenew,
            permission: 'realestate.leases.renew',
            visible: _occupying,
            form: (d) => _post('Renew lease', '$_leasesPath/$id/renew', [
              Field.date('endDate', 'New end date (empty = one more year)'),
              Field.money('annualRent', 'New annual rent (empty = same)'),
              Field.integer('paymentFrequency', 'Instalments per year (empty = same)', min: 1, max: 12),
              Field.money('securityDepositAmount', 'Security deposit (empty = same)'),
              Field.multiline('notes', 'Notes', maxLength: 1000),
            ], submit: 'Create renewal'),
            run: (ref, d, _) async => null,
          ),
          ActionSpec(
            label: 'Confirm move-out',
            icon: Icons.logout,
            permission: 'realestate.leases.terminate',
            visible: (d) => d['status'] == 'holdingOver',
            confirm: 'Confirm the tenant has moved out? The lease ends and the unit becomes vacant.',
            run: (ref, d, _) => ref.read(apiProvider).post('$_leasesPath/$id/move-out'),
          ),
          ActionSpec(
            label: 'Credit notes',
            icon: Icons.receipt_long_outlined,
            permission: 'realestate.leases.terminate',
            visible: (d) => d['status'] == 'terminated',
            confirm: 'Raise credit notes for the invoiced rent after the termination date?',
            run: (ref, d, _) => ref.read(apiProvider).post('$_leasesPath/$id/termination-credits'),
          ),
          ActionSpec(
            label: 'Return or keep deposit',
            icon: Icons.account_balance_wallet_outlined,
            permission: 'realestate.deposits.manage',
            visible: _closed,
            form: (d) => _post(
              'Return or keep the security deposit',
              '$_leasesPath/$id/deposit/settle',
              [
                Field.date('date', 'Date', required: true, initial: 'today'),
                Field.money('damages', 'Damages charged', initial: '0'),
                Field.text('damagesDescription', 'Damages description', maxLength: 300),
                Field.money('refund', 'Refund to tenant', initial: '0'),
                Field.lookup('refundBankAccountId', 'Refund from account', path: '/api/v1/finance/accounts', subtitleKey: 'code', visibleWhen: (v) => (toNum(v['refund']) ?? 0) > 0),
              ],
              submit: 'Close out',
            ),
            run: (ref, d, _) async => null,
          ),
          ActionSpec(
            label: 'Receive deposit',
            icon: Icons.savings_outlined,
            permission: 'realestate.deposits.manage',
            visible: (d) => d['status'] != 'cancelled' && ((d['securityDepositAmount'] as num?) ?? 0) > 0,
            form: (d) => _post(
              'Receive security deposit',
              '$_leasesPath/$id/deposit',
              [
                bankLookup(),
                Field.date('date', 'Date', required: true, initial: 'today'),
                Field.money('amount', 'Amount', required: true, min: 0.01, initial: d['securityDepositAmount']),
              ],
              submit: 'Record',
            ),
            run: (ref, d, _) async => null,
          ),
          ActionSpec(
            label: 'Holding over',
            icon: Icons.hourglass_bottom,
            permission: 'realestate.leases.edit',
            visible: (d) => d['status'] == 'active',
            confirm: 'Flag this lease as holding over (tenant stays past the contract)?',
            run: (ref, d, _) => ref.read(apiProvider).post('$_leasesPath/$id/holding-over', body: {'holdingOver': true}),
          ),
          ActionSpec(
            label: 'Terminate',
            icon: Icons.gavel,
            permission: 'realestate.leases.terminate',
            destructive: true,
            visible: _occupying,
            form: (d) => _post(
              'Terminate lease',
              '$_leasesPath/$id/terminate',
              [Field.date('date', 'Termination date', required: true, initial: 'today'), Field.text('reason', 'Reason', required: true, maxLength: 300)],
              submit: 'Terminate',
            ),
            run: (ref, d, _) async => null,
          ),
          ActionSpec(
            label: 'Cancel lease',
            icon: Icons.cancel_outlined,
            permission: 'realestate.leases.edit',
            destructive: true,
            visible: (d) => d['status'] == 'draft',
            confirm: 'Cancel this draft lease?',
            run: (ref, d, _) => ref.read(apiProvider).post('$_leasesPath/$id/cancel'),
          ),
        ],
      );
    });
  }

  Widget _body(BuildContext context, WidgetRef ref, Json d) {
    final schedule = (d['schedule'] as List<dynamic>? ?? const []).cast<Json>();
    final canBill = reCan(ref, 'realestate.billing.run') && d['status'] == 'active';
    return DetailBody(children: [
      DetailHeader(title: '${d['tenantName']}', subtitle: 'Unit ${d['unitNumber']} · ${formatDate(d['startDate'])} to ${formatDate(d['endDate'])}', status: d['status']?.toString()),
      InfoCard(rows: [
        InfoRow('Annual rent', money(d, 'annualRent'), bold: true),
        InfoRow('Instalments per year', text(d['paymentFrequency'])),
        InfoRow('Security deposit', money(d, 'securityDepositAmount')),
        InfoRow('Days to expiry', d['status'] == 'active' ? text(d['daysToExpiry']) : null),
        InfoRow('Grace days', text(d['graceDays'])),
        InfoRow('Notice period', '${d['noticePeriodDays']} days'),
        InfoRow('VAT', humanize(d['suggestedVat']?.toString())),
        InfoRow('Terminated on', d['terminationDate'] == null ? null : formatDate(d['terminationDate'])),
        InfoRow('Reason', text(d['terminationReason'])),
        InfoRow('Holding over', d['holdingOverFlag'] == true ? 'Yes' : null),
      ]),
      if ((d['notes'] ?? '').toString().isNotEmpty) Padding(padding: const EdgeInsets.only(bottom: ErpSpace.s3), child: Text(d['notes'].toString())),
      Text('Rent schedule', style: Theme.of(context).textTheme.titleMedium),
      for (final i in schedule)
        ListTile(
          contentPadding: EdgeInsets.zero,
          dense: true,
          title: Text('#${i['sequence']} · due ${formatDate(i['dueDate'])}'),
          subtitle: Text('${formatDate(i['periodFrom'])} to ${formatDate(i['periodTo'])}'),
          trailing: Row(mainAxisSize: MainAxisSize.min, children: [
            Text(formatMoney((i['amount'] as num?) ?? 0)),
            const SizedBox(width: ErpSpace.s2),
            if (canBill && i['status'] == 'scheduled')
              TextButton(onPressed: () => invoiceInstalment(context, ref, i), child: const Text('Invoice'))
            else
              reChip(i['status']),
          ]),
        ),
      const SizedBox(height: ErpSpace.s2),
      Text('Security deposit', style: Theme.of(context).textTheme.titleMedium),
      RelatedList(
        path: '$_leasesPath/${d['id']}/deposit',
        pick: 'transactions',
        empty: 'No deposit movements yet.',
        tile: (c, t) => ListTile(
          dense: true,
          contentPadding: EdgeInsets.zero,
          title: Text(humanize(t['kind']?.toString())),
          subtitle: Text('${formatDate(t['date'])}${t['description'] == null ? '' : ' · ${t['description']}'}'),
          trailing: Text(formatMoney((t['amount'] as num?) ?? 0)),
        ),
      ),
      const SizedBox(height: ErpSpace.s2),
      Text('Work orders', style: Theme.of(context).textTheme.titleMedium),
      RelatedList(path: '$rePath/work-orders', query: {'filter': 'leaseId:eq:${d['id']}', 'pageSize': 10}, tile: workOrderTile, empty: 'No work orders for this lease.'),
    ]);
  }
}

Future<void> invoiceInstalment(BuildContext context, WidgetRef ref, Json instalment) async {
  final messenger = ScaffoldMessenger.of(context);
  final ok = await showDialog<bool>(
    context: context,
    builder: (ctx) => AlertDialog(
      content: Text('Invoice instalment #${instalment['sequence']} (${formatMoney((instalment['amount'] as num?) ?? 0)}) now?'),
      actions: [TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('Cancel')), FilledButton(onPressed: () => Navigator.pop(ctx, true), child: const Text('Invoice'))],
    ),
  );
  if (ok != true) return;
  try {
    await ref.read(apiProvider).post('$rePath/billing/instalments/${instalment['id']}/invoice');
    ref.read(dataVersionProvider.notifier).bump();
    messenger.showSnackBar(const SnackBar(content: Text('Invoice raised')));
  } on ApiException catch (e) {
    messenger.showSnackBar(SnackBar(content: Text(e.message), duration: const Duration(seconds: 6)));
  }
}

// ---- Rent billing -----------------------------------------------------------------------------------------------------------------

class BillingScreen extends ConsumerStatefulWidget {
  const BillingScreen({super.key});

  @override
  ConsumerState<BillingScreen> createState() => _BillingScreenState();
}

class _BillingScreenState extends ConsumerState<BillingScreen> {
  Json? _result;

  FormSpec _spec() => FormSpec(
        title: 'Run rent billing',
        submitLabel: 'Run billing',
        intro: 'Invoices every scheduled rent instalment of an active lease that has fallen due by the date below.',
        fields: [Field.date('asOf', 'Bill instalments due up to', required: true, initial: 'today')],
        submit: (ref, body, raw) => ref.read(apiProvider).post('$rePath/billing/run', body: body),
      );

  @override
  Widget build(BuildContext context) {
    final r = _result;
    return Scaffold(
      appBar: AppBar(title: const Text('Rent billing')),
      body: ListView(padding: const EdgeInsets.all(ErpSpace.s4), children: [
        const Text('Raise rent invoices for all instalments that are due. Single instalments can also be invoiced from the lease.'),
        const SizedBox(height: ErpSpace.s4),
        if (r != null)
          InfoCard(title: 'Last run', rows: [InfoRow('Invoices raised', '${r['invoiced']}', bold: true), InfoRow('Total billed', formatMoney((r['totalBilled'] as num?) ?? 0))]),
        if (reCan(ref, 'realestate.billing.run'))
          FilledButton.icon(
            icon: const Icon(Icons.play_arrow),
            label: const Text('Run billing'),
            onPressed: () async {
              final res = await openForm<Object?>(context, _spec());
              if (res is Map && mounted) setState(() => _result = res.cast<String, dynamic>());
            },
          ),
      ]),
    );
  }
}

// ---- Renewals ---------------------------------------------------------------------------------------------------------------------

class RenewalsScreen extends ConsumerStatefulWidget {
  const RenewalsScreen({super.key});

  @override
  ConsumerState<RenewalsScreen> createState() => _RenewalsScreenState();
}

class _RenewalsScreenState extends ConsumerState<RenewalsScreen> {
  Json? _data;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final d = await ref.read(apiProvider).get('$rePath/renewals');
      if (mounted) setState(() => _data = (d as Map).cast<String, dynamic>());
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    }
  }

  FormSpec draftForm(Json row) => FormSpec(
        title: 'Draft renewal',
        submitLabel: 'Create draft',
        intro: 'Creates a draft renewal for ${row['tenant']} (unit ${row['unit']}). Current rent ${formatMoney((row['annualRent'] as num?) ?? 0)}.',
        fields: [Field.decimal('upliftPercent', 'Rent increase %', required: true, min: -50, max: 100, decimals: 2, initial: '0')],
        submit: (ref, body, raw) async {
          final res = await ref.read(apiProvider).post('$rePath/renewals/drafts', body: {'leaseIds': [row['leaseId']], 'upliftPercent': body['upliftPercent']});
          final first = (res as List<dynamic>).isEmpty ? null : (res.first as Map);
          if (first != null && first['error'] != null) throw ApiException(409, first['error'].toString());
          return res;
        },
      );

  @override
  Widget build(BuildContext context) {
    ref.listen(dataVersionProvider, (_, _) => _load());
    final d = _data;
    final rows = (d?['rows'] as List<dynamic>? ?? const []).cast<Json>();
    return Scaffold(
      appBar: AppBar(title: const Text('Lease renewals')),
      body: d == null
          ? (_error == null ? const ErpSkeleton.list() : ErpStateView.error(message: _error!, onRetry: _load))
          : RefreshIndicator(
              onRefresh: _load,
              child: ListView(children: [
                Padding(
                  padding: const EdgeInsets.all(ErpSpace.s4),
                  child: Text('Leases ending within ${d['withinDays']} days: ${d['due']} due, ${d['draft']} draft renewals, ${d['renewed']} renewed.'),
                ),
                if (rows.isEmpty) const Padding(padding: EdgeInsets.all(ErpSpace.s4), child: Text('No leases are due for renewal.')),
                for (final r in rows)
                  ListTile(
                    onTap: () => reGo(context, LeaseDetailScreen(id: r['leaseId'].toString())),
                    title: Text('${r['tenant']}'),
                    subtitle: Text('${r['property']} · Unit ${r['unit']}\nEnds ${formatDate(r['endDate'])} (${r['daysLeft']} days) · rent ${formatMoney((r['annualRent'] as num?) ?? 0)}'
                        '${r['marketVariancePercent'] == null ? '' : ' · ${formatMoney(r['marketVariancePercent'] as num)}% vs market'}'),
                    isThreeLine: true,
                    trailing: r['state'] == 'due' && reCan(ref, 'realestate.leases.renew')
                        ? TextButton(onPressed: () => openForm<Object?>(context, draftForm(r)), child: const Text('Draft'))
                        : reChip(r['state']),
                  ),
              ]),
            ),
    );
  }
}
