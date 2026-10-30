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
import 'order_screens.dart';
import 'sales_common.dart';

const _path = '/api/v1/m/sales/quotes';

const quoteStatuses = [
  Option('draft', 'Draft'),
  Option('sent', 'Sent'),
  Option('accepted', 'Accepted'),
  Option('declined', 'Declined'),
  Option('expired', 'Expired'),
  Option('converted', 'Converted'),
];

FormSpec quoteForm(WidgetRef ref, {Json? existing}) {
  final me = ref.read(meOrEmptyProvider);
  final editing = existing != null;
  final entity = me.defaultEntity;
  final initial = <String, dynamic>{
    'legalEntityId': existing?['legalEntityId'] ?? entity?.id,
    'currency': existing?['currency'] ?? entity?.baseCurrency,
    if (editing) ...{
      'customerPartyId': existing['customerPartyId'],
      if (existing['customerPartyId'] != null) '__label_customerPartyId': existing['customerName'],
      'customerName': existing['customerName'],
      'customerEmail': existing['customerEmail'],
      'customerPhone': existing['customerPhone'],
      'quoteDate': existing['quoteDate'],
      'validUntil': existing['validUntil'],
      'notes': existing['notes'],
      'terms': existing['terms'],
      'lines': linesToForm(existing['lines'] as List<dynamic>),
    },
  };
  final fields = <Field>[
    Field.select('legalEntityId', 'Legal entity', options: [for (final e in me.legalEntities) Option(e.id, e.name)], required: true, initial: entity?.id),
    Field.lookup('customerPartyId', 'Customer (optional)', path: '/api/v1/parties', query: const {'role': 'Customer'}, subtitleKey: 'code', copy: const {'customerName': 'name', 'customerEmail': 'email', 'customerPhone': 'phone'}),
    Field.text('customerName', 'Customer name', required: true, maxLength: 200),
    Field.text('customerEmail', 'Email', maxLength: 150, keyboard: TextInputType.emailAddress),
    Field.text('customerPhone', 'Phone', maxLength: 50, keyboard: TextInputType.phone),
    Field.date('quoteDate', 'Quote date', required: true, initial: 'today'),
    Field.date('validUntil', 'Valid until'),
    Field.text('currency', 'Currency', required: true, maxLength: 10),
    Field.lines('lines', 'Lines', fields: documentLineFields()),
    Field.multiline('notes', 'Notes', maxLength: 2000),
    Field.multiline('terms', 'Terms', maxLength: 2000),
  ];
  return FormSpec(
    title: editing ? 'Edit ${existing['number']}' : 'New quote',
    initial: initial,
    fields: fields,
    summary: formTotal,
    submit: (ref, body, raw) {
      final api = ref.read(apiProvider);
      final b = completeLines(body);
      return editing ? api.put('$_path/${existing['id']}', body: b) : api.post(_path, body: b);
    },
  );
}

Widget quoteTile(BuildContext context, Json q) {
  final status = (q['status'] ?? '').toString();
  return ListTile(
    onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => QuoteDetailScreen(id: q['id'].toString()))),
    title: Text('${q['number']}', maxLines: 1, overflow: TextOverflow.ellipsis),
    subtitle: Text('${q['customerName'] ?? ''}\nValid until ${formatDate(q['validUntil'])}'),
    isThreeLine: true,
    trailing: Column(
      mainAxisAlignment: MainAxisAlignment.center,
      crossAxisAlignment: CrossAxisAlignment.end,
      children: [
        Text(money(q, 'totalAmount', currencyKey: 'currency'), style: const TextStyle(fontWeight: FontWeight.w600)),
        const SizedBox(height: 4),
        StatusChip(humanize(status), color: StatusChip.forStatus(status)),
      ],
    ),
  );
}

class QuotesScreen extends StatelessWidget {
  const QuotesScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Quotes',
        path: _path,
        searchHint: 'Search quotes',
        emptyText: 'No quotes found.',
        itemBuilder: quoteTile,
        filters: quoteStatuses,
        create: (ref) => quoteForm(ref),
        createPermission: 'sales.quotes.manage',
      );
}

ActionSpec _quoteStatus(String id, String label, IconData icon, String target, Set<String> from, {bool destructive = false, String? message}) => ActionSpec(
      label: label,
      icon: icon,
      permission: 'sales.quotes.manage',
      destructive: destructive,
      visible: (d) => from.contains(d['status']),
      confirm: message,
      successMessage: '$label done',
      run: (ref, d, _) => ref.read(apiProvider).put('$_path/$id/status', body: {'status': target}),
    );

class QuoteDetailScreen extends StatelessWidget {
  const QuoteDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) {
    return Consumer(builder: (context, ref, _) {
      return DetailPage(
        path: '$_path/$id',
        titleOf: (d) => '${d['number']}',
        canEdit: (d) => d['status'] != 'converted' && ref.read(meOrEmptyProvider).can('sales.quotes.manage'),
        edit: (d) => quoteForm(ref, existing: d),
        body: _body,
        actions: [
          _quoteStatus(id, 'Send', Icons.send_outlined, 'sent', {'draft'}),
          _quoteStatus(id, 'Accept', Icons.thumb_up_outlined, 'accepted', {'draft', 'sent'}),
          _quoteStatus(id, 'Decline', Icons.thumb_down_outlined, 'declined', {'draft', 'sent'}, destructive: true, message: 'Mark this quote as declined?'),
          _quoteStatus(id, 'Expire', Icons.timer_off_outlined, 'expired', {'sent'}, destructive: true, message: 'Mark this quote as expired?'),
          _quoteStatus(id, 'Back to draft', Icons.undo, 'draft', {'declined', 'expired'}),
          ActionSpec(
            label: 'Convert to order',
            icon: Icons.shopping_cart_checkout,
            permission: 'sales.orders.manage',
            visible: (d) => d['status'] == 'accepted',
            confirm: 'Create a sales order from this quote?',
            successMessage: 'Sales order created',
            run: (ref, d, _) => ref.read(apiProvider).post('$_path/$id/convert-to-order'),
          ),
          ActionSpec(
            label: 'Delete',
            icon: Icons.delete_outline,
            permission: 'sales.quotes.manage',
            destructive: true,
            visible: (d) => d['status'] != 'converted',
            confirm: 'Delete this quote?',
            successMessage: 'Deleted',
            run: (ref, d, _) async {
              await ref.read(apiProvider).delete('$_path/$id');
              if (context.mounted) Navigator.of(context).pop();
              return null;
            },
          ),
        ],
      );
    });
  }

  Widget _body(BuildContext context, Json d) {
    return DetailBody(children: [
      DetailHeader(title: d['customerName']?.toString() ?? '', subtitle: 'Quote date ${formatDate(d['quoteDate'])} · Valid until ${formatDate(d['validUntil'])}', status: d['status']?.toString()),
      InfoCard(rows: [
        InfoRow('Email', d['customerEmail']?.toString()),
        InfoRow('Phone', d['customerPhone']?.toString()),
        InfoRow('Subtotal', money(d, 'subTotal', currencyKey: 'currency')),
        InfoRow('Discount', money(d, 'discountTotal', currencyKey: 'currency')),
        InfoRow('Tax', money(d, 'taxTotal', currencyKey: 'currency')),
        InfoRow('Total', money(d, 'totalAmount', currencyKey: 'currency'), bold: true),
      ]),
      if (d['convertedSalesOrderId'] != null)
        ListTile(
          contentPadding: EdgeInsets.zero,
          leading: const Icon(Icons.shopping_cart_outlined),
          title: const Text('Open the sales order'),
          trailing: const Icon(Icons.chevron_right),
          onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => OrderDetailScreen(id: d['convertedSalesOrderId'].toString()))),
        ),
      documentLines(context, d['lines'] as List<dynamic>? ?? const []),
      if ((d['notes'] ?? '').toString().isNotEmpty) ...[const SizedBox(height: 8), Text('Notes', style: Theme.of(context).textTheme.titleMedium), Text(d['notes'].toString())],
      if ((d['terms'] ?? '').toString().isNotEmpty) ...[const SizedBox(height: 8), Text('Terms', style: Theme.of(context).textTheme.titleMedium), Text(d['terms'].toString())],
    ]);
  }
}
