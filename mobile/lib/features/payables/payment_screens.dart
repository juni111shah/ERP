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
import '../common/paged_list_view.dart';
import 'bill_screens.dart' show billsPath;
import 'common.dart';

const paymentsPath = '$finPath/payments';

const paymentMethods = [
  Option('cash', 'Cash'),
  Option('bankTransfer', 'Bank transfer'),
  Option('card', 'Card'),
  Option('instrument', 'Cheque'),
  Option('other', 'Other'),
];

const _paymentStatuses = [Option('issued', 'Posted'), Option('voided', 'Voided')];

String methodLabel(Object? m) {
  for (final o in paymentMethods) {
    if (o.value == m) return o.label;
  }
  return humanize(m?.toString());
}

Field _billLookup() => Field.lookup('billId', 'Bill', path: billsPath, labelKey: 'number', subtitleKey: 'partyName', required: true);

num allocationTotal(Object? lines) => [for (final l in (lines as List<dynamic>? ?? const [])) toNum((l as Map)['amount']) ?? 0].fold<num>(0, (a, b) => a + b);

/// Create a vendor payment (optionally a cheque, optionally allocated to bills).
FormSpec paymentForm(WidgetRef ref) {
  final fields = <Field>[
    entityField(ref),
    vendorField(),
    Field.date('date', 'Payment date', required: true, initial: 'today', validator: plausibleDate),
    Field.select('method', 'Method', options: paymentMethods, required: true, initial: 'bankTransfer'),
    accountField('bankAccountId', 'Paid from (bank or cash account)', bankOnly: true),
    Field.money('amount', 'Amount', required: true, min: 0.01),
    Field.text('chequeNo', 'Cheque number', required: true, maxLength: 50, visibleWhen: (v) => v['method'] == 'instrument'),
    Field.date('chequeDate', 'Cheque date', visibleWhen: (v) => v['method'] == 'instrument', validator: plausibleDate),
    Field.text('bankName', 'Cheque bank', maxLength: 100, visibleWhen: (v) => v['method'] == 'instrument'),
    Field.text('reference', 'Reference', maxLength: 100),
    Field.lines(
      'allocations',
      'Allocate to bills (optional)',
      minLines: 0,
      addLabel: 'Add bill',
      fields: [_billLookup(), Field.money('amount', 'Amount', required: true, min: 0.01)],
      visibleWhen: null,
    ),
  ];
  return FormSpec(
    title: 'New payment',
    fields: fields,
    summary: (context, raw) {
      final alloc = allocationTotal(raw['allocations']);
      final amount = toNum(raw['amount']) ?? 0;
      return Column(children: [
        InfoRow('Allocated to bills', formatMoney(alloc)),
        InfoRow('Left on account', formatMoney(amount - alloc), bold: true),
        if (alloc > amount) Text('Allocations exceed the payment amount.', style: TextStyle(color: Theme.of(context).colorScheme.error)),
      ]);
    },
    submit: (ref, body, raw) async {
      if (allocationTotal(raw['allocations']) > (toNum(raw['amount']) ?? 0)) {
        throw ApiException(422, 'Allocations exceed the payment amount.');
      }
      if ((body['allocations'] as List).isEmpty) body.remove('allocations');
      return ref.read(apiProvider).post(paymentsPath, body: body);
    },
  );
}

/// Allocate (more of) an existing payment to bills.
FormSpec allocateForm(Json payment) => FormSpec(
      title: 'Allocate ${payment['number']}',
      intro: 'Unallocated: ${formatMoney((payment['unallocated'] as num?) ?? 0)}',
      fields: [
        Field.lines('allocations', 'Bills', addLabel: 'Add bill', fields: [_billLookup(), Field.money('amount', 'Amount', required: true, min: 0.01)]),
      ],
      submit: (ref, body, raw) async {
        if (allocationTotal(raw['allocations']) > ((payment['unallocated'] as num?) ?? 0)) {
          throw ApiException(422, 'Allocations exceed the unallocated amount.');
        }
        return ref.read(apiProvider).post('$paymentsPath/${payment['id']}/allocate', body: body);
      },
    );

Widget paymentTile(BuildContext context, Json p) {
  final status = (p['status'] ?? '').toString();
  return ListTile(
    onTap: () => pushPage(context, PaymentDetailScreen(id: p['id'].toString())),
    title: Text('${p['number']}', maxLines: 1, overflow: TextOverflow.ellipsis),
    subtitle: Text('${p['partyName'] ?? ''}\n${formatDate(p['date'])} · ${methodLabel(p['method'])}'),
    isThreeLine: true,
    trailing: Column(
      mainAxisAlignment: MainAxisAlignment.center,
      crossAxisAlignment: CrossAxisAlignment.end,
      children: [
        Text(money(p, 'amount'), style: const TextStyle(fontWeight: FontWeight.w600)),
        const SizedBox(height: 4),
        StatusChip(docStatusLabel(status), color: StatusChip.forStatus(status)),
      ],
    ),
  );
}

class PaymentsScreen extends StatelessWidget {
  const PaymentsScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Payments',
        path: paymentsPath,
        searchHint: 'Search payments',
        emptyText: 'No payments found.',
        itemBuilder: paymentTile,
        filters: _paymentStatuses,
        create: (ref) => paymentForm(ref),
        createPermission: 'finance.payments.write',
      );
}

FormSpec clearChequeForm(Json p) => FormSpec(
      title: 'Clear cheque',
      intro: 'Cheque ${p['chequeNumber'] ?? ''} has cleared the bank on:',
      fields: [Field.date('date', 'Clearing date', required: true, initial: 'today', validator: plausibleDate)],
      submitLabel: 'Mark cleared',
      submit: (ref, body, raw) => ref.read(apiProvider).post('$finPath/cheques/${p['paymentInstrumentId']}/clear-issued', body: body),
    );

class PaymentDetailScreen extends StatelessWidget {
  const PaymentDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) {
    return DetailPage(
      path: '$paymentsPath/$id',
      titleOf: (d) => d['number'].toString(),
      body: paymentBody,
      actions: [
        ActionSpec(
          label: 'Allocate',
          icon: Icons.link,
          permission: 'finance.payments.write',
          visible: (d) => d['status'] != 'voided' && ((d['unallocated'] as num?) ?? 0) > 0,
          form: allocateForm,
          run: (ref, d, _) async => null,
        ),
        ActionSpec(
          label: 'Clear cheque',
          icon: Icons.account_balance,
          permission: 'finance.cheques.approve',
          visible: (d) => d['status'] != 'voided' && d['paymentInstrumentId'] != null && '${d['chequeStatus']}'.toLowerCase() == 'issued',
          form: clearChequeForm,
          run: (ref, d, _) async => null,
        ),
        ActionSpec(
          label: 'Void',
          icon: Icons.block,
          permission: 'finance.payments.write',
          destructive: true,
          visible: (d) => d['status'] != 'voided',
          reasonLabel: 'Why is this payment being voided?',
          successMessage: 'Voided',
          run: (ref, d, reason) => ref.read(apiProvider).post('$paymentsPath/$id/void', body: {'reason': reason}),
        ),
      ],
    );
  }
}

Widget paymentBody(BuildContext context, Json d) {
  final allocs = (d['allocations'] as List<dynamic>? ?? const []).cast<Json>();
  return DetailBody(children: [
    DetailHeader(title: d['partyName']?.toString() ?? '', subtitle: '${formatDate(d['date'])} · ${methodLabel(d['method'])}', status: d['status']?.toString()),
    InfoCard(rows: [
      InfoRow('Amount', money(d, 'amount'), bold: true),
      InfoRow('Unallocated', money(d, 'unallocated')),
      InfoRow('Reference', d['reference']?.toString()),
      InfoRow('Cheque number', d['chequeNumber']?.toString()),
      InfoRow('Cheque date', d['chequeDate'] == null ? null : formatDate(d['chequeDate'])),
      InfoRow('Cheque bank', d['chequeBankName']?.toString()),
      InfoRow('Cheque status', d['chequeStatus'] == null ? null : humanize(d['chequeStatus'].toString())),
      InfoRow('Void reason', d['voidReason']?.toString()),
    ]),
    Text('Allocated to bills', style: Theme.of(context).textTheme.titleMedium),
    if (allocs.isEmpty) const Padding(padding: EdgeInsets.symmetric(vertical: 8), child: Text('Not allocated to any bill.')),
    for (final a in allocs)
      ListTile(
        contentPadding: EdgeInsets.zero,
        title: Text((a['billNumber'] ?? '').toString().isEmpty ? (a['label'] ?? 'On account').toString() : a['billNumber'].toString()),
        trailing: Text(formatMoney((a['amount'] as num?) ?? 0)),
      ),
  ]);
}
