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
import '../common/paged_list_view.dart';

const receiptsPath = '/api/v1/finance/receipts';

const receiptStatuses = [Option('issued', 'Posted'), Option('voided', 'Voided')];

/// Methods a receipt can use; cheques ("instrument") are recorded in the cheque register, not here.
const receiptMethods = [Option('cash', 'Cash'), Option('bankTransfer', 'Bank transfer'), Option('card', 'Card'), Option('other', 'Other')];

String? _dateCheck(Object? v, Values all) {
  final d = DateTime.tryParse(v?.toString() ?? '');
  if (d == null) return null;
  if (d.year < 2000 || d.isAfter(DateTime.now().add(const Duration(days: 366)))) return 'Enter a plausible date';
  return null;
}

num _allocated(Values all) => [for (final l in (all['allocations'] as List<dynamic>? ?? const [])) toNum((l as Map)['amount']) ?? 0].fold<num>(0, (a, b) => a + b);

/// Allocation lines (invoice + amount) used by the receipt form and the "allocate" action.
Field allocationLines({int minLines = 0}) => Field.lines('allocations', 'Allocate to invoices', minLines: minLines, addLabel: 'Add invoice', fields: [
      Field.lookup('invoiceId', 'Invoice', path: '/api/v1/finance/invoices', labelKey: 'number', subtitleKey: 'partyName', required: true),
      Field.money('amount', 'Amount', required: true, min: 0.01),
    ]);

/// New receipt. [partyId]/[partyName] and [invoice] (an invoice json) prefill it, as when recording a receipt from an invoice.
FormSpec receiptForm(WidgetRef ref, {String? partyId, String? partyName, Json? invoice}) {
  final me = ref.read(meOrEmptyProvider);
  final outstanding = invoice == null ? null : ((toNum(invoice['total']) ?? 0) - (toNum(invoice['paid']) ?? 0));
  final amountText = outstanding == null || outstanding <= 0 ? null : outstanding.toStringAsFixed(2);
  final fields = <Field>[
    Field.select('legalEntityId', 'Legal entity', options: [for (final e in me.legalEntities) Option(e.id, e.name)], required: true, initial: me.defaultEntity?.id),
    Field.lookup('partyId', 'Customer', path: '/api/v1/parties', query: const {'role': 'Customer'}, subtitleKey: 'code', required: true),
    Field.date('date', 'Date', required: true, initial: 'today', validator: _dateCheck),
    Field.select('method', 'Method', options: receiptMethods, required: true, initial: 'bankTransfer'),
    Field.lookup('bankAccountId', 'Bank or cash account', path: '/api/v1/finance/accounts', query: const {'filter': 'isBank:eq:true'}, subtitleKey: 'code', required: true),
    Field.money('amount', 'Amount', required: true, min: 0.01, validator: (v, all) => _allocated(all) > (toNum(v) ?? 0) ? 'Allocations exceed the receipt amount' : null),
    Field.text('reference', 'Reference', maxLength: 100),
    Field.toggle('autoAllocate', 'Allocate to oldest invoices automatically'),
    allocationLines(),
  ];
  return FormSpec(
    title: 'New receipt',
    initial: {
      'legalEntityId': invoice?['legalEntityId'] ?? me.defaultEntity?.id,
      'partyId': ?partyId,
      '__label_partyId': ?(partyId == null ? null : partyName),
      'amount': ?amountText,
      if (invoice != null)
        'allocations': [
          if (amountText != null) {'invoiceId': invoice['id'], '__label_invoiceId': invoice['number'], 'amount': amountText}
        ],
    },
    fields: fields,
    summary: (context, raw) {
      final left = (toNum(raw['amount']) ?? 0) - _allocated(raw);
      return Padding(padding: const EdgeInsets.only(top: 4), child: InfoRow('Left on account', formatMoney(left < 0 ? 0 : left), bold: true));
    },
    submit: (ref, body, raw) => ref.read(apiProvider).post(receiptsPath, body: body),
  );
}

FormSpec allocateForm(Json receipt) => FormSpec(
      title: 'Allocate ${receipt['number']}',
      intro: 'Unallocated: ${money(receipt, 'unallocated')}',
      fields: [allocationLines(minLines: 1)],
      submit: (ref, body, raw) => ref.read(apiProvider).post('$receiptsPath/${receipt['id']}/allocate', body: {'allocations': body['allocations']}),
    );

Widget receiptTile(BuildContext context, Json r) {
  final status = (r['status'] ?? '').toString();
  final unallocated = (r['unallocated'] as num?) ?? 0;
  return ListTile(
    onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => ReceiptDetailScreen(id: r['id'].toString()))),
    title: Text('${r['number']}', maxLines: 1, overflow: TextOverflow.ellipsis),
    subtitle: Text('${r['partyName'] ?? ''}\n${formatDate(r['date'])} · ${humanize(r['method']?.toString())}${unallocated > 0 ? ' · ${formatMoney(unallocated)} unallocated' : ''}'),
    isThreeLine: true,
    trailing: Column(
      mainAxisAlignment: MainAxisAlignment.center,
      crossAxisAlignment: CrossAxisAlignment.end,
      children: [
        Text(money(r, 'amount'), style: const TextStyle(fontWeight: FontWeight.w600)),
        const SizedBox(height: 4),
        StatusChip(humanize(status == 'issued' ? 'posted' : status), color: StatusChip.forStatus(status)),
      ],
    ),
  );
}

class ReceiptsScreen extends StatelessWidget {
  const ReceiptsScreen({super.key, this.showAppBar = true});

  final bool showAppBar;

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Receipts',
        path: receiptsPath,
        searchHint: 'Search receipts',
        emptyText: 'No receipts found.',
        itemBuilder: receiptTile,
        filters: receiptStatuses,
        create: (ref) => receiptForm(ref),
        createPermission: 'finance.receipts.write',
        showAppBar: showAppBar,
      );
}

class ReceiptDetailScreen extends StatelessWidget {
  const ReceiptDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) => DetailPage(
        path: '$receiptsPath/$id',
        titleOf: (d) => '${d['number']}',
        body: _body,
        actions: [
          ActionSpec(
            label: 'Allocate',
            icon: Icons.link,
            permission: 'finance.receipts.write',
            visible: (d) => d['status'] == 'issued' && ((d['unallocated'] as num?) ?? 0) > 0,
            form: allocateForm,
            run: (ref, d, _) async => null,
          ),
          ActionSpec(
            label: 'Void',
            icon: Icons.block,
            permission: 'finance.receipts.write',
            destructive: true,
            visible: (d) => d['status'] == 'issued',
            reasonLabel: 'Why is this receipt being voided?',
            successMessage: 'Voided',
            run: (ref, d, reason) => ref.read(apiProvider).post('$receiptsPath/$id/void', body: {'reason': reason}),
          ),
        ],
      );

  Widget _body(BuildContext context, Json d) {
    final allocations = (d['allocations'] as List<dynamic>? ?? const []).cast<Json>();
    return DetailBody(children: [
      DetailHeader(title: d['partyName']?.toString() ?? '', subtitle: '${formatDate(d['date'])} · ${humanize(d['method']?.toString())}', status: d['status'] == 'issued' ? 'posted' : d['status']?.toString()),
      InfoCard(rows: [
        InfoRow('Amount', money(d, 'amount'), bold: true),
        InfoRow('Unallocated', money(d, 'unallocated')),
        InfoRow('Reference', text(d['reference'])),
        InfoRow('Cheque', text(d['chequeNumber'])),
        InfoRow('Void reason', text(d['voidReason'])),
      ]),
      Text('Allocations', style: Theme.of(context).textTheme.titleMedium),
      if (allocations.isEmpty) const Padding(padding: EdgeInsets.symmetric(vertical: 8), child: Text('Not allocated to any invoice.')),
      for (final a in allocations)
        ListTile(
          contentPadding: EdgeInsets.zero,
          title: Text(a['invoiceNumber']?.toString() ?? a['label']?.toString() ?? ''),
          trailing: Text(formatMoney((a['amount'] as num?) ?? 0)),
        ),
    ]);
  }
}
