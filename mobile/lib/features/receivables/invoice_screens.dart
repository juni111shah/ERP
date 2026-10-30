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

// REFERENCE IMPLEMENTATION. Every other area follows this shape:
//   1. a ListScreen (path, tile, status chips, create form + permission),
//   2. a FormSpec builder shared by create and edit,
//   3. a DetailPage (body from InfoCards, ActionSpecs for the state changes the API offers),
//   4. one MobileScreen per server menu entry in the area's screens.dart.

const _invoicesPath = '/api/v1/finance/invoices';

const invoiceStatuses = [Option('draft', 'Draft'), Option('issued', 'Issued'), Option('partlyPaid', 'Partly paid'), Option('paid', 'Paid'), Option('voided', 'Voided')];

/// Create (existing == null) or edit a draft invoice or credit note.
FormSpec invoiceForm(WidgetRef ref, {Json? existing}) {
  final me = ref.read(meOrEmptyProvider);
  final editing = existing != null;
  final initial = <String, dynamic>{
    'legalEntityId': existing?['legalEntityId'] ?? me.defaultEntity?.id,
    if (editing) ...{
      'partyId': existing['partyId'],
      '__label_partyId': existing['partyName'],
      'invoiceDate': existing['invoiceDate'],
      'dueDate': existing['dueDate'],
      'notes': existing['notes'],
      'isCreditNote': existing['isCreditNote'],
      'lines': [
        for (final l in (existing['lines'] as List<dynamic>))
          {
            'itemId': (l as Json)['itemId'],
            'description': l['description'],
            'quantity': l['quantity'],
            'price': l['price'],
            'taxCodeId': l['taxCodeId'],
            if (l['taxCodeId'] != null) '__label_taxCodeId': 'Selected tax code',
          },
      ],
    },
  };
  final fields = <Field>[
    Field.select('legalEntityId', 'Legal entity', options: [for (final e in me.legalEntities) Option(e.id, e.name)], required: true, initial: me.defaultEntity?.id),
    Field.lookup('partyId', 'Customer', path: '/api/v1/parties', query: const {'role': 'Customer'}, subtitleKey: 'code', required: true),
    Field.date('invoiceDate', 'Invoice date', required: true, initial: 'today'),
    Field.date('dueDate', 'Due date (leave empty for the customer terms)'),
    if (!editing) Field.toggle('isCreditNote', 'This is a credit note'),
    Field.lines('lines', 'Lines', fields: [
      Field.lookup('itemId', 'Item (optional)', path: '/api/v1/items', subtitleKey: 'sku', copy: const {'description': 'name', 'price': 'salesPrice'}),
      Field.text('description', 'Description', required: true, maxLength: 400),
      Field.decimal('quantity', 'Quantity', required: true, min: 0.0001, initial: '1'),
      Field.money('price', 'Unit price', required: true),
      Field.lookup('taxCodeId', 'Tax code (optional)', path: '/api/v1/finance/tax-codes', subtitleKey: 'code'),
    ]),
    Field.multiline('notes', 'Notes', maxLength: 1000),
  ];
  return FormSpec(
    title: editing ? 'Edit ${existing['number']}' : 'New invoice',
    initial: initial,
    fields: fields,
    summary: (context, raw) {
      final net = [for (final l in (raw['lines'] as List<dynamic>)) (toNum((l as Json)['quantity']) ?? 0) * (toNum(l['price']) ?? 0)].fold<num>(0, (a, b) => a + b);
      return Padding(padding: const EdgeInsets.only(top: 4), child: InfoRow('Net before tax', formatMoney(net), bold: true));
    },
    submit: (ref, body, raw) async {
      final api = ref.read(apiProvider);
      return editing ? api.put('$_invoicesPath/${existing['id']}', body: body) : api.post(_invoicesPath, body: body);
    },
  );
}

Widget invoiceTile(BuildContext context, Json i) {
  final status = (i['status'] ?? '').toString();
  final credit = i['isCreditNote'] == true;
  return ListTile(
    onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => InvoiceDetailScreen(id: i['id'].toString()))),
    title: Text('${credit ? 'Credit note ' : ''}${i['number']}', maxLines: 1, overflow: TextOverflow.ellipsis),
    subtitle: Text('${i['partyName'] ?? ''}\nDue ${formatDate(i['dueDate'])}'),
    isThreeLine: true,
    trailing: Column(
      mainAxisAlignment: MainAxisAlignment.center,
      crossAxisAlignment: CrossAxisAlignment.end,
      children: [
        Text(money(i, 'total'), style: const TextStyle(fontWeight: FontWeight.w600)),
        const SizedBox(height: 4),
        StatusChip(humanize(status), color: StatusChip.forStatus(status)),
      ],
    ),
  );
}

class InvoicesScreen extends StatelessWidget {
  const InvoicesScreen({super.key, this.showAppBar = true});

  final bool showAppBar;

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Invoices',
        path: _invoicesPath,
        searchHint: 'Search invoices',
        emptyText: 'No invoices found.',
        itemBuilder: invoiceTile,
        filters: invoiceStatuses,
        create: (ref) => invoiceForm(ref),
        createPermission: 'finance.invoices.write',
        showAppBar: showAppBar,
      );
}

class InvoiceDetailScreen extends StatelessWidget {
  const InvoiceDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) {
    return Consumer(builder: (context, ref, _) {
      return DetailPage(
        path: '$_invoicesPath/$id',
        titleOf: (d) => '${d['isCreditNote'] == true ? 'Credit note ' : ''}${d['number']}',
        canEdit: (d) => d['status'] == 'draft' && ref.read(meOrEmptyProvider).can('finance.invoices.write'),
        edit: (d) => invoiceForm(ref, existing: d),
        body: (context, d) => _body(context, d),
        actions: [
          ActionSpec(
            label: 'Issue',
            icon: Icons.check_circle_outline,
            permission: 'finance.invoices.issue',
            visible: (d) => d['status'] == 'draft',
            confirm: 'Issue this document? It gets its number and is posted to the ledger.',
            successMessage: 'Issued',
            run: (ref, d, _) => ref.read(apiProvider).post('$_invoicesPath/$id/issue'),
          ),
          ActionSpec(
            label: 'Credit note',
            icon: Icons.assignment_return_outlined,
            permission: 'finance.invoices.credit',
            visible: (d) => d['isCreditNote'] != true && const {'issued', 'partlyPaid', 'paid'}.contains(d['status']),
            confirm: 'Create a draft credit note for the full amount? You can edit it before issuing.',
            successMessage: 'Draft credit note created',
            run: (ref, d, _) => ref.read(apiProvider).post('$_invoicesPath/$id/credit-note', body: const {}),
          ),
          ActionSpec(
            label: 'Void',
            icon: Icons.block,
            permission: 'finance.invoices.void',
            destructive: true,
            visible: (d) => d['status'] == 'issued',
            reasonLabel: 'Why is this invoice being voided?',
            successMessage: 'Voided',
            run: (ref, d, reason) => ref.read(apiProvider).post('$_invoicesPath/$id/void', body: {'reason': reason}),
          ),
          ActionSpec(
            label: 'Delete',
            icon: Icons.delete_outline,
            permission: 'finance.invoices.write',
            destructive: true,
            visible: (d) => d['status'] == 'draft',
            confirm: 'Delete this draft?',
            successMessage: 'Deleted',
            run: (ref, d, _) async {
              await ref.read(apiProvider).delete('$_invoicesPath/$id');
              if (context.mounted) Navigator.of(context).pop();
              return null;
            },
          ),
        ],
      );
    });
  }

  Widget _body(BuildContext context, Json d) {
    final lines = (d['lines'] as List<dynamic>? ?? const []).cast<Json>();
    final outstanding = ((d['total'] as num?) ?? 0) - ((d['paid'] as num?) ?? 0);
    return DetailBody(children: [
      DetailHeader(title: d['partyName']?.toString() ?? '', subtitle: 'Invoice date ${formatDate(d['invoiceDate'])} · Due ${formatDate(d['dueDate'])}', status: d['status']?.toString()),
      InfoCard(rows: [
        InfoRow('Net', money(d, 'net')),
        InfoRow('Tax', money(d, 'tax')),
        InfoRow('Total', money(d, 'total'), bold: true),
        InfoRow('Paid', money(d, 'paid')),
        InfoRow('Outstanding', '${d['currencyCode'] ?? ''} ${formatMoney(outstanding)}'.trim(), bold: true),
      ]),
      Text('Lines', style: Theme.of(context).textTheme.titleMedium),
      for (final l in lines)
        ListTile(
          contentPadding: EdgeInsets.zero,
          title: Text('${l['description']}'),
          subtitle: Text('${formatMoney((l['quantity'] as num?) ?? 0)} × ${formatMoney((l['price'] as num?) ?? 0)}'),
          // Quantity x price (+ tax), not the stored line net: demo data has lines whose net was never filled in.
          trailing: Text(formatMoney(((l['quantity'] as num?) ?? 0) * ((l['price'] as num?) ?? 0) + ((l['taxAmount'] as num?) ?? 0))),
        ),
      if ((d['notes'] ?? '').toString().isNotEmpty) ...[const SizedBox(height: 8), Text('Notes', style: Theme.of(context).textTheme.titleMedium), Text(d['notes'].toString())],
    ]);
  }
}
