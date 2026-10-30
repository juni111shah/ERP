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

const _acqPath = '$rePath/acquisitions';

const acquisitionStatuses = [Option('draft', 'Draft'), Option('completed', 'Completed'), Option('cancelled', 'Cancelled')];
const fundingTypes = [Option('bankMortgage', 'Bank mortgage'), Option('developerPlan', 'Developer plan'), Option('loan', 'Loan'), Option('other', 'Other')];

FormSpec acquisitionForm(WidgetRef ref, {Json? existing}) {
  final me = ref.read(meOrEmptyProvider);
  final editing = existing != null;
  return FormSpec(
    title: editing ? 'Edit ${existing['number'] ?? 'acquisition'}' : 'New acquisition',
    initial: editing
        ? {
            for (final k in ['legalEntityId', 'propertyId', 'sellerName', 'purchaseDate', 'contractReference', 'purchasePrice', 'downPayment', 'transferFees', 'otherCosts', 'payFromAccountId', 'notes']) k: existing[k],
            '__label_propertyId': existing['propertyName'],
            'funding': [
              for (final f in (existing['funding'] as List<dynamic>? ?? const []))
                {for (final k in ['type', 'lender', 'principal', 'annualRatePercent', 'termMonths', 'firstDueDate']) k: (f as Json)[k]},
            ],
          }
        : const {},
    fields: [
      Field.select('legalEntityId', 'Legal entity', options: entityOptions(me), required: true, initial: me.defaultEntity?.id),
      propertyLookup(),
      Field.text('sellerName', 'Seller', required: true, maxLength: 200),
      Field.date('purchaseDate', 'Purchase date', required: true, initial: 'today'),
      Field.text('contractReference', 'Contract reference', maxLength: 100),
      Field.money('purchasePrice', 'Purchase price', required: true, min: 0.01),
      Field.money('downPayment', 'Down payment', required: true, initial: '0'),
      Field.money('transferFees', 'Transfer fees', required: true, initial: '0'),
      Field.money('otherCosts', 'Other costs', required: true, initial: '0'),
      Field.lookup('payFromAccountId', 'Pay from account', path: '/api/v1/finance/accounts', subtitleKey: 'code'),
      Field.multiline('notes', 'Notes', maxLength: 1000),
      Field.lines('funding', 'Funding', minLines: 0, addLabel: 'Add funding source', fields: [
        Field.select('type', 'Type', options: fundingTypes, required: true, initial: 'bankMortgage'),
        Field.text('lender', 'Lender', required: true, maxLength: 200),
        Field.money('principal', 'Principal', required: true, min: 0.01),
        Field.decimal('annualRatePercent', 'Annual rate %', required: true, min: 0, max: 100, decimals: 3),
        Field.integer('termMonths', 'Term (months)', required: true, min: 1, max: 600),
        Field.date('firstDueDate', 'First instalment due', required: true),
      ]),
    ],
    submit: (ref, body, raw) async {
      final api = ref.read(apiProvider);
      return editing ? api.put('$_acqPath/${existing['id']}', body: body) : api.post(_acqPath, body: body);
    },
  );
}

Widget acquisitionTile(BuildContext context, Json a) => reTile(
      context,
      title: '${a['sellerName']}',
      subtitle: '${a['number'] ?? 'Draft'} · ${formatDate(a['purchaseDate'])}',
      trailing: money(a, 'purchasePrice'),
      status: a['status'],
      onTap: () => reGo(context, AcquisitionDetailScreen(id: a['id'].toString())),
    );

class AcquisitionsScreen extends StatelessWidget {
  const AcquisitionsScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Acquisitions',
        path: _acqPath,
        searchHint: 'Search acquisitions',
        emptyText: 'No acquisitions found.',
        itemBuilder: acquisitionTile,
        filters: acquisitionStatuses,
        create: (ref) => acquisitionForm(ref),
        createPermission: 'realestate.acquisitions.manage',
      );
}

class AcquisitionDetailScreen extends StatelessWidget {
  const AcquisitionDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) {
    return Consumer(builder: (context, ref, _) {
      return DetailPage(
        path: '$_acqPath/$id',
        titleOf: (d) => (d['number'] ?? 'Acquisition').toString(),
        canEdit: (d) => d['status'] == 'draft' && reCan(ref, 'realestate.acquisitions.manage'),
        edit: (d) => acquisitionForm(ref, existing: d),
        body: (context, d) => _body(context, ref, d),
        actions: [
          ActionSpec(
            label: 'Complete',
            icon: Icons.check_circle_outline,
            permission: 'realestate.acquisitions.complete',
            visible: (d) => d['status'] == 'draft',
            confirm: 'Complete this acquisition? The property is capitalised, the payment is posted and the financing is fixed.',
            successMessage: 'Acquisition completed',
            run: (ref, d, _) => ref.read(apiProvider).post('$_acqPath/$id/complete'),
          ),
          ActionSpec(
            label: 'Cancel',
            icon: Icons.cancel_outlined,
            permission: 'realestate.acquisitions.manage',
            destructive: true,
            visible: (d) => d['status'] == 'draft',
            confirm: 'Cancel this draft acquisition?',
            run: (ref, d, _) => ref.read(apiProvider).post('$_acqPath/$id/cancel'),
          ),
        ],
      );
    });
  }

  FormSpec _payForm(Json inst) => FormSpec(
        title: 'Pay instalment #${inst['sequence']}',
        submitLabel: 'Pay ${formatMoney((inst['total'] as num?) ?? 0)}',
        fields: [bankLookup(label: 'Pay from account'), Field.date('date', 'Payment date', required: true, initial: 'today')],
        submit: (ref, body, raw) => ref.read(apiProvider).post('$_acqPath/instalments/${inst['id']}/pay', body: body),
      );

  Widget _body(BuildContext context, WidgetRef ref, Json d) {
    final funding = (d['funding'] as List<dynamic>? ?? const []).cast<Json>();
    final canPay = reCan(ref, 'realestate.funding.pay') && d['status'] == 'completed';
    return DetailBody(children: [
      DetailHeader(title: '${d['propertyName'] ?? 'Acquisition'}', subtitle: 'From ${d['sellerName']} · ${formatDate(d['purchaseDate'])}', status: d['status']?.toString()),
      InfoCard(rows: [
        InfoRow('Purchase price', money(d, 'purchasePrice'), bold: true),
        InfoRow('Down payment', money(d, 'downPayment')),
        InfoRow('Transfer fees', money(d, 'transferFees')),
        InfoRow('Other costs', money(d, 'otherCosts')),
        InfoRow('Total cost', money(d, 'totalCost'), bold: true),
        InfoRow('Cash at completion', money(d, 'cashAtCompletion')),
        InfoRow('Financed', money(d, 'financed')),
        InfoRow('Not yet funded', ((d['unfunded'] as num?) ?? 0) > 0 ? money(d, 'unfunded') : null),
        InfoRow('Contract', text(d['contractReference'])),
      ]),
      for (final f in funding) ...[
        Text('${humanize(f['type']?.toString())} · ${f['lender']}', style: Theme.of(context).textTheme.titleMedium),
        InfoCard(rows: [
          InfoRow('Principal', formatMoney((f['principal'] as num?) ?? 0)),
          InfoRow('Rate / term', '${f['annualRatePercent']}% · ${f['termMonths']} months'),
          InfoRow('Monthly payment', formatMoney((f['monthlyPayment'] as num?) ?? 0)),
          InfoRow('Paid', '${f['paid']} of ${f['instalmentCount']}'),
          InfoRow('Outstanding', formatMoney((f['outstanding'] as num?) ?? 0), bold: true),
          InfoRow('Next due', f['nextDue'] == null ? null : formatDate(f['nextDue'])),
        ]),
        for (final i in (f['instalments'] as List<dynamic>? ?? const []).cast<Json>().where((i) => i['status'] != 'paid').take(6))
          ListTile(
            dense: true,
            contentPadding: EdgeInsets.zero,
            title: Text('#${i['sequence']} · due ${formatDate(i['dueDate'])}'),
            subtitle: Text('Principal ${formatMoney((i['principal'] as num?) ?? 0)} · interest ${formatMoney((i['interest'] as num?) ?? 0)}'),
            trailing: Row(mainAxisSize: MainAxisSize.min, children: [
              Text(formatMoney((i['total'] as num?) ?? 0)),
              if (canPay) TextButton(onPressed: () => openForm<Object?>(context, _payForm(i)), child: const Text('Pay')),
            ]),
          ),
      ],
      if ((d['notes'] ?? '').toString().isNotEmpty) Text(d['notes'].toString()),
    ]);
  }
}
