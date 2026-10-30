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
import 'common.dart';

const billsPath = '$finPath/bills';

/// Create (existing == null) or edit a draft vendor bill.
FormSpec billForm(WidgetRef ref, {Json? existing}) {
  final editing = existing != null;
  final initial = <String, dynamic>{
    'legalEntityId': existing?['legalEntityId'] ?? ref.read(meOrEmptyProvider).defaultEntity?.id,
    if (editing) ...{
      'partyId': existing['partyId'],
      '__label_partyId': existing['partyName'],
      'vendorReference': existing['vendorReference'],
      'billDate': existing['billDate'],
      'dueDate': existing['dueDate'],
      'notes': existing['notes'],
      'lines': [
        for (final l in (existing['lines'] as List<dynamic>))
          {
            'description': (l as Json)['description'],
            'quantity': l['quantity'],
            'price': l['price'],
            'accountId': l['accountId'],
            if (l['accountId'] != null) '__label_accountId': 'Selected account',
            'taxCodeId': l['taxCodeId'],
            if (l['taxCodeId'] != null) '__label_taxCodeId': 'Selected tax code',
          },
      ],
    },
  };
  final fields = <Field>[
    entityField(ref),
    vendorField(),
    Field.text('vendorReference', "Vendor's invoice number", maxLength: 100),
    Field.date('billDate', 'Bill date', required: true, initial: 'today', validator: plausibleDate),
    Field.date('dueDate', 'Due date (empty = vendor terms)', validator: notBefore('billDate', 'The due date cannot be before the bill date')),
    Field.lines('lines', 'Lines', fields: [
      Field.lookup('itemId', 'Item (optional)', path: '/api/v1/items', subtitleKey: 'sku', copy: const {'description': 'name', 'price': 'purchasePrice'}),
      Field.text('description', 'Description', required: true, maxLength: 400),
      Field.decimal('quantity', 'Quantity', required: true, min: 0.0001, initial: '1'),
      Field.decimal('price', 'Unit price', required: true, min: 0, decimals: 4),
      accountField('accountId', 'Expense account'),
      taxCodeField(),
    ]),
    Field.multiline('notes', 'Notes', maxLength: 1000),
  ];
  return FormSpec(
    title: editing ? 'Edit ${existing['number']}' : 'New bill',
    initial: initial,
    fields: fields,
    summary: (context, raw) {
      final net = [for (final l in (raw['lines'] as List<dynamic>)) lineTotal(l as Map)].fold<num>(0, (a, b) => a + b);
      return Padding(padding: const EdgeInsets.only(top: 4), child: InfoRow('Net before tax', formatMoney(net), bold: true));
    },
    submit: (ref, body, raw) async {
      final api = ref.read(apiProvider);
      return editing ? api.put('$billsPath/${existing['id']}', body: body) : api.post(billsPath, body: body);
    },
  );
}

Widget billTile(BuildContext context, Json b) {
  final status = (b['status'] ?? '').toString();
  final ref = (b['vendorReference'] ?? '').toString();
  return ListTile(
    onTap: () => pushPage(context, BillDetailScreen(id: b['id'].toString())),
    title: Text('${b['number']}${ref.isEmpty ? '' : ' · $ref'}', maxLines: 1, overflow: TextOverflow.ellipsis),
    subtitle: Text('${b['partyName'] ?? ''}\nDue ${formatDate(b['dueDate'])}'),
    isThreeLine: true,
    trailing: Column(
      mainAxisAlignment: MainAxisAlignment.center,
      crossAxisAlignment: CrossAxisAlignment.end,
      children: [
        Text(money(b, 'total'), style: const TextStyle(fontWeight: FontWeight.w600)),
        const SizedBox(height: 4),
        StatusChip(docStatusLabel(status), color: StatusChip.forStatus(status)),
      ],
    ),
  );
}

class BillsScreen extends StatelessWidget {
  const BillsScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Bills',
        path: billsPath,
        searchHint: 'Search bills',
        emptyText: 'No bills found.',
        itemBuilder: billTile,
        filters: docStatuses,
        create: (ref) => billForm(ref),
        createPermission: 'finance.bills.write',
      );
}

bool _editable(Json d) => d['status'] == 'draft' || d['status'] == 'pendingApproval';

class BillDetailScreen extends StatelessWidget {
  const BillDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) {
    return Consumer(builder: (context, ref, _) {
      return DetailPage(
        path: '$billsPath/$id',
        titleOf: (d) => d['number'].toString(),
        canEdit: (d) => _editable(d) && ref.read(meOrEmptyProvider).can('finance.bills.write'),
        edit: (d) => billForm(ref, existing: d),
        body: billBody,
        actions: [
          ActionSpec(
            label: 'Post',
            icon: Icons.check_circle_outline,
            permission: 'finance.bills.post',
            visible: _editable,
            confirm: 'Post this bill to the ledger? If an approval rule applies it is sent for approval first.',
            successMessage: 'Bill posted',
            run: (ref, d, _) => ref.read(apiProvider).post('$billsPath/$id/post'),
          ),
          ActionSpec(
            label: 'Void',
            icon: Icons.block,
            permission: 'finance.bills.void',
            destructive: true,
            visible: (d) => d['status'] == 'issued',
            reasonLabel: 'Why is this bill being voided?',
            successMessage: 'Voided',
            run: (ref, d, reason) => ref.read(apiProvider).post('$billsPath/$id/void', body: {'reason': reason}),
          ),
          ActionSpec(
            label: 'Delete',
            icon: Icons.delete_outline,
            permission: 'finance.bills.write',
            destructive: true,
            visible: _editable,
            confirm: 'Delete this draft bill?',
            successMessage: 'Deleted',
            run: (ref, d, _) async {
              await ref.read(apiProvider).delete('$billsPath/$id');
              if (context.mounted) Navigator.of(context).pop();
              return null;
            },
          ),
        ],
      );
    });
  }
}

Widget billBody(BuildContext context, Json d) {
  final lines = (d['lines'] as List<dynamic>? ?? const []).cast<Json>();
  final outstanding = ((d['total'] as num?) ?? 0) - ((d['paid'] as num?) ?? 0);
  return DetailBody(children: [
    DetailHeader(title: d['partyName']?.toString() ?? '', subtitle: 'Bill date ${formatDate(d['billDate'])} · Due ${formatDate(d['dueDate'])}', status: d['status']?.toString()),
    InfoCard(rows: [
      InfoRow('Vendor reference', d['vendorReference']?.toString()),
      InfoRow('Net', money(d, 'net')),
      InfoRow('Tax', money(d, 'tax')),
      InfoRow('Total', money(d, 'total'), bold: true),
      InfoRow('Paid', money(d, 'paid')),
      InfoRow('Outstanding', '${d['currencyCode'] ?? ''} ${formatMoney(outstanding)}'.trim(), bold: true),
      InfoRow('Void reason', d['voidReason']?.toString()),
    ]),
    Text('Lines', style: Theme.of(context).textTheme.titleMedium),
    for (final l in lines)
      ListTile(
        contentPadding: EdgeInsets.zero,
        title: Text('${l['description']}'),
        subtitle: Text('${formatMoney((l['quantity'] as num?) ?? 0)} × ${formatMoney((l['price'] as num?) ?? 0)}'),
        // Quantity x price (+ tax): demo data has lines whose net was never filled in.
        trailing: Text(formatMoney(lineTotal(l) + ((l['taxAmount'] as num?) ?? 0))),
      ),
    if ((d['notes'] ?? '').toString().isNotEmpty) ...[const SizedBox(height: 8), Text('Notes', style: Theme.of(context).textTheme.titleMedium), Text(d['notes'].toString())],
  ]);
}
