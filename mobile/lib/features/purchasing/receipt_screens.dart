import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api_client.dart';
import '../../core/format.dart';
import '../../core/me.dart';
import '../../core/session.dart';
import '../../kit/detail_page.dart';
import '../../kit/field.dart';
import '../../kit/field_view.dart';
import '../../kit/form_page.dart';
import '../../kit/list_screen.dart';
import '../../kit/widgets.dart';
import 'purchasing_common.dart';

const receiptsPath = '$purchasingPath/receipts';

/// Receive goods against an approved / partly received [order] (the order's detail JSON). Each line starts at what is still open.
FormSpec receiptForm(WidgetRef ref, {required Json order}) {
  final me = ref.read(meOrEmptyProvider);
  final lines = <Map<String, dynamic>>[
    for (final l in (order['lines'] as List<dynamic>))
      if (((toNum((l as Json)['quantity']) ?? 0) - (toNum(l['receivedQuantity']) ?? 0)) > 0)
        {
          'purchaseOrderLineId': l['id'],
          'description': '${l['description']} (ordered ${formatMoney(toNum(l['quantity']) ?? 0)}, received ${formatMoney(toNum(l['receivedQuantity']) ?? 0)})',
          'quantityReceived': '${(toNum(l['quantity']) ?? 0) - (toNum(l['receivedQuantity']) ?? 0)}',
        },
  ];
  return FormSpec(
    title: 'Receive ${order['number']}',
    intro: 'Enter what arrived. The server limits each line to what is still open (plus the allowed tolerance).',
    initial: {'receivedBy': me.fullName, 'lines': lines},
    fields: [
      Field.text('receivedBy', 'Received by', required: true, maxLength: 150),
      Field.date('receiptDate', 'Receipt date', required: true, initial: 'today'),
      Field.text('deliveryNoteNumber', 'Delivery note number', maxLength: 100),
      Field.lines('lines', 'Lines', addLabel: 'Add', fields: [
        Field.text('description', 'Item', readOnly: true, validator: (v, line) => line['purchaseOrderLineId'] == null ? 'Remove this line: use the order lines' : null),
        Field.decimal('quantityReceived', 'Quantity received', required: true, min: 0.0001),
      ]),
      Field.multiline('notes', 'Notes', maxLength: 2000),
    ],
    submit: (ref, body, raw) {
      final rawLines = raw['lines'] as List<dynamic>;
      final bodyLines = body['lines'] as List<dynamic>;
      final sent = {
        ...body,
        'legalEntityId': order['legalEntityId'],
        'purchaseOrderId': order['id'],
        'lines': [
          for (var i = 0; i < bodyLines.length; i++)
            {
              'purchaseOrderLineId': (rawLines[i] as Map)['purchaseOrderLineId'],
              'itemCode': '',
              'unitOfMeasure': 'EA',
              ...(bodyLines[i] as Map<String, dynamic>),
            },
        ],
      };
      return ref.read(apiProvider).post(receiptsPath, body: sent);
    },
  );
}

Widget receiptTile(BuildContext context, Json g) => ListTile(
      onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => ReceiptDetailScreen(id: g['id'].toString()))),
      title: Text('${g['number']}', maxLines: 1, overflow: TextOverflow.ellipsis),
      subtitle: Text('${g['vendorName'] ?? ''}\nOrder ${g['purchaseOrderNumber']} · ${formatDate(g['receiptDate'])}'),
      isThreeLine: true,
      trailing: Text('${g['lineCount']} line${g['lineCount'] == 1 ? '' : 's'}'),
    );

/// The receipts list. New receipts start from an order: pick one that can be received, then fill in the quantities.
class ReceiptsScreen extends ConsumerWidget {
  const ReceiptsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final canCreate = ref.watch(meOrEmptyProvider).can('purchasing.receipts.manage');
    return Scaffold(
      appBar: AppBar(title: const Text('Goods receipts')),
      floatingActionButton: canCreate
          ? FloatingActionButton.extended(
              onPressed: () => startReceipt(context, ref),
              icon: const Icon(Icons.add),
              label: const Text('New'),
            )
          : null,
      body: ListScreen(
        title: 'Goods receipts',
        path: receiptsPath,
        searchHint: 'Search receipts',
        emptyText: 'No goods receipts found.',
        itemBuilder: receiptTile,
        showAppBar: false,
      ),
    );
  }
}

Future<void> startReceipt(BuildContext context, WidgetRef ref) async {
  final picked = await showLookupSheet(context, ref,
      path: ordersPath, title: 'Order to receive', labelKey: 'number', subtitleKey: 'vendorName', query: const {'filter': 'status:in:approved|partiallyReceived'});
  if (picked == null || !context.mounted) return;
  final messenger = ScaffoldMessenger.of(context);
  try {
    final order = await ref.read(apiProvider).get('$ordersPath/${picked['id']}') as Json;
    if (!context.mounted) return;
    await openForm<Object?>(context, receiptForm(ref, order: order));
  } on ApiException catch (e) {
    messenger.showSnackBar(SnackBar(content: Text(e.message)));
  }
}

class ReceiptDetailScreen extends StatelessWidget {
  const ReceiptDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) => Consumer(builder: (context, ref, _) {
        final path = '$receiptsPath/$id';
        return DetailPage(
          path: path,
          titleOf: (d) => '${d['number']}',
          body: (context, d) => receiptBody(context, d),
          actions: [
            ActionSpec(
              label: 'Delete',
              icon: Icons.delete_outline,
              permission: 'purchasing.receipts.manage',
              destructive: true,
              confirm: 'Delete this receipt? The goods are taken off the order again.',
              successMessage: 'Deleted',
              run: (ref, d, _) async {
                await ref.read(apiProvider).delete(path);
                if (context.mounted) Navigator.of(context).pop();
                return null;
              },
            ),
          ],
        );
      });
}

Widget receiptBody(BuildContext context, Json d) {
  final lines = (d['lines'] as List<dynamic>? ?? const []).cast<Json>();
  return DetailBody(children: [
    DetailHeader(title: d['vendorName']?.toString() ?? '', subtitle: 'Order ${d['purchaseOrderNumber']} · ${formatDate(d['receiptDate'])}'),
    InfoCard(rows: [
      InfoRow('Received by', text(d['receivedBy'])),
      InfoRow('Delivery note', text(d['deliveryNoteNumber'])),
      InfoRow('Notes', text(d['notes'])),
    ]),
    Text('Lines', style: Theme.of(context).textTheme.titleMedium),
    for (final l in lines)
      ListTile(
        contentPadding: EdgeInsets.zero,
        title: Text('${l['description']}'),
        subtitle: Text('${l['itemCode'] ?? ''}'),
        trailing: Text('${formatMoney((l['quantityReceived'] as num?) ?? 0)} ${l['unitOfMeasure'] ?? ''}'),
      ),
  ]);
}
