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
import 'purchasing_common.dart';
import 'receipt_screens.dart';



String orderStatusLabel(String s) => s == 'partiallyReceived' ? 'Partly received' : humanize(s);

num _lineNet(Json l) {
  final q = toNum(l['quantity']) ?? 0, p = toNum(l['unitPrice']) ?? 0, d = toNum(l['discountPercent']) ?? 0, t = toNum(l['taxRate']) ?? 0;
  final after = q * p * (1 - d / 100);
  return after * (1 + t / 100);
}

/// Create (existing == null) or edit a draft/submitted purchase order.
FormSpec orderForm(WidgetRef ref, {Json? existing}) {
  final me = ref.read(meOrEmptyProvider);
  final editing = existing != null;
  final entityId = existing?['legalEntityId'] ?? me.defaultEntity?.id;
  return FormSpec(
    title: editing ? 'Edit ${existing['number']}' : 'New purchase order',
    initial: {
      'legalEntityId': entityId,
      if (editing) ...{
        'vendorPartyId': existing['vendorPartyId'],
        '__label_vendorPartyId': existing['vendorName'],
        'vendorReference': existing['vendorReference'],
        'orderDate': existing['orderDate'],
        'expectedDeliveryDate': existing['expectedDeliveryDate'],
        'paymentTerms': existing['paymentTerms'],
        'deliveryAddress': existing['deliveryAddress'],
        'notes': existing['notes'],
        'lines': [
          for (final l in (existing['lines'] as List<dynamic>))
            {
              'itemId': (l as Json)['itemId'],
              if (l['itemId'] != null) '__label_itemId': l['itemCode'] ?? 'Selected item',
              'itemCode': l['itemCode'],
              'description': l['description'],
              'quantity': l['quantity'],
              'unitOfMeasure': l['unitOfMeasure'],
              'unitPrice': l['unitPrice'],
              'discountPercent': l['discountPercent'],
              'taxRate': l['taxRate'],
            },
        ],
      },
    },
    fields: [
      Field.select('legalEntityId', 'Legal entity', options: [for (final e in me.legalEntities) Option(e.id, e.name)], required: true, initial: entityId, visibleWhen: (_) => !editing),
      Field.lookup('vendorPartyId', 'Vendor', path: '/api/v1/parties', query: const {'role': 'Vendor'}, subtitleKey: 'code', required: true),
      Field.text('vendorReference', 'Vendor reference', maxLength: 100),
      Field.date('orderDate', 'Order date', required: true, initial: 'today'),
      Field.date('expectedDeliveryDate', 'Expected delivery', validator: (v, all) {
        final order = all['orderDate']?.toString() ?? '';
        return v != null && order.isNotEmpty && v.toString().compareTo(order) < 0 ? 'Cannot be before the order date' : null;
      }),
      Field.text('paymentTerms', 'Payment terms', maxLength: 200),
      Field.multiline('deliveryAddress', 'Delivery address', maxLength: 500),
      Field.lines('lines', 'Lines', fields: [
        Field.lookup('itemId', 'Item (optional)', path: '/api/v1/items', subtitleKey: 'sku', copy: const {'description': 'name', 'itemCode': 'sku', 'unitPrice': 'purchasePrice', 'unitOfMeasure': 'uom'}),
        Field.text('description', 'Description', required: true, maxLength: 500),
        Field.decimal('quantity', 'Quantity', required: true, min: 0.0001, initial: '1'),
        Field.text('unitOfMeasure', 'Unit', initial: 'EA', maxLength: 20),
        Field.decimal('unitPrice', 'Unit price', required: true, min: 0, decimals: 4),
        Field.decimal('discountPercent', 'Discount %', min: 0, max: 100, decimals: 2, initial: '0'),
        Field.decimal('taxRate', 'Tax %', min: 0, max: 100, decimals: 2, initial: '0'),
      ]),
      Field.multiline('notes', 'Notes', maxLength: 2000),
    ],
    summary: (context, raw) {
      final total = [for (final l in (raw['lines'] as List<dynamic>)) _lineNet(l as Json)].fold<num>(0, (a, b) => a + b);
      return Padding(padding: const EdgeInsets.only(top: 4), child: InfoRow('Total incl. tax', formatMoney(total), bold: true));
    },
    submit: (ref, body, raw) {
      final api = ref.read(apiProvider);
      final entity = me.legalEntities.where((e) => e.id == (body['legalEntityId'] ?? entityId)).firstOrNull;
      final full = {
        ...body,
        'legalEntityId': body['legalEntityId'] ?? entityId,
        'vendorName': raw['__label_vendorPartyId'] ?? existing?['vendorName'] ?? '',
        'currency': entity?.baseCurrency ?? existing?['currency'] ?? me.baseCurrency,
        'lines': withLineDefaults(body['lines'] as List<dynamic>, raw['lines'] as List<dynamic>, extra: const {'discountPercent': 0, 'taxRate': 0}),
      };
      return editing ? api.put('$ordersPath/${existing['id']}', body: full) : api.post(ordersPath, body: full);
    },
  );
}

Widget orderTile(BuildContext context, Json o) {
  final status = (o['status'] ?? '').toString();
  return ListTile(
    onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => OrderDetailScreen(id: o['id'].toString()))),
    title: Text('${o['number']}', maxLines: 1, overflow: TextOverflow.ellipsis),
    subtitle: Text('${o['vendorName'] ?? ''}\n${formatDate(o['orderDate'])} · ${o['lineCount']} line${o['lineCount'] == 1 ? '' : 's'}'),
    isThreeLine: true,
    trailing: Column(mainAxisAlignment: MainAxisAlignment.center, crossAxisAlignment: CrossAxisAlignment.end, children: [
      Text(money(o, 'totalAmount', currencyKey: 'currency'), style: const TextStyle(fontWeight: FontWeight.w600)),
      const SizedBox(height: 4),
      purchasingStatusChip(status, orderStatusLabel(status)),
    ]),
  );
}

class OrdersScreen extends StatelessWidget {
  const OrdersScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Purchase orders',
        path: ordersPath,
        searchHint: 'Search orders',
        emptyText: 'No purchase orders found.',
        itemBuilder: orderTile,
        filters: orderStatuses,
        create: (ref) => orderForm(ref),
        createPermission: 'purchasing.orders.manage',
      );
}

/// A status change the API offers (PUT .../status), shown only from the states the backend allows it.
ActionSpec orderStatusAction(String path, String label, IconData icon, String target, Set<String> from, {bool destructive = false, String? confirm}) => ActionSpec(
      label: label,
      icon: icon,
      permission: 'purchasing.orders.manage',
      destructive: destructive,
      visible: (d) => from.contains(d['status']),
      confirm: confirm,
      successMessage: '$label done',
      run: (ref, d, _) => ref.read(apiProvider).put('$path/status', body: {'status': target}),
    );

class OrderDetailScreen extends StatelessWidget {
  const OrderDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) => Consumer(builder: (context, ref, _) {
        final path = '$ordersPath/$id';
        return DetailPage(
          path: path,
          titleOf: (d) => '${d['number']}',
          canEdit: (d) => const {'draft', 'submitted'}.contains(d['status']) && ref.read(meOrEmptyProvider).can('purchasing.orders.manage'),
          edit: (d) => orderForm(ref, existing: d),
          body: (context, d) => orderBody(context, d),
          actions: [
            orderStatusAction(path, 'Submit', Icons.send_outlined, 'submitted', {'draft'}, confirm: 'Submit this order for approval?'),
            orderStatusAction(path, 'Approve', Icons.check_circle_outline, 'approved', {'draft', 'submitted'}, confirm: 'Approve this order? It can then be received.'),
            orderStatusAction(path, 'Back to draft', Icons.undo, 'draft', {'submitted'}),
            ActionSpec(
              label: 'Receive goods',
              icon: Icons.inventory_2_outlined,
              permission: 'purchasing.receipts.manage',
              visible: (d) => const {'approved', 'partiallyReceived'}.contains(d['status']),
              form: (d) => receiptForm(ref, order: d),
              run: (ref, d, _) async => null,
            ),
            orderStatusAction(path, 'Mark received', Icons.done_all, 'received', {'partiallyReceived'}, confirm: 'Mark the order as fully received?'),
            ActionSpec(
              label: 'Convert to bill',
              icon: Icons.receipt_long_outlined,
              permission: 'purchasing.orders.manage',
              visible: (d) => const {'approved', 'partiallyReceived', 'received'}.contains(d['status']),
              confirm: 'Create a vendor bill from this order? Quantities received are billed at the order price.',
              successMessage: 'Vendor bill created',
              run: (ref, d, _) => ref.read(apiProvider).post('$path/convert-to-bill'),
            ),
            orderStatusAction(path, 'Cancel order', Icons.block, 'cancelled', {'draft', 'submitted', 'approved'}, destructive: true, confirm: 'Cancel this purchase order?'),
            ActionSpec(
              label: 'Delete',
              icon: Icons.delete_outline,
              permission: 'purchasing.orders.manage',
              destructive: true,
              visible: (d) => const {'draft', 'cancelled'}.contains(d['status']),
              confirm: 'Delete this order?',
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

Widget orderBody(BuildContext context, Json d) {
  final lines = (d['lines'] as List<dynamic>? ?? const []).cast<Json>();
  final status = '${d['status']}';
  return DetailBody(children: [
    DetailHeader(title: d['vendorName']?.toString() ?? '', subtitle: 'Ordered ${formatDate(d['orderDate'])}${d['expectedDeliveryDate'] != null ? ' · Delivery ${formatDate(d['expectedDeliveryDate'])}' : ''}'),
    Align(alignment: Alignment.centerLeft, child: Padding(padding: const EdgeInsets.only(bottom: 12), child: purchasingStatusChip(status, orderStatusLabel(status)))),
    InfoCard(rows: [
      InfoRow('Sub-total', money(d, 'subTotal', currencyKey: 'currency')),
      InfoRow('Discount', money(d, 'discountTotal', currencyKey: 'currency')),
      InfoRow('Tax', money(d, 'taxTotal', currencyKey: 'currency')),
      InfoRow('Total', money(d, 'totalAmount', currencyKey: 'currency'), bold: true),
      InfoRow('Vendor reference', text(d['vendorReference'])),
      InfoRow('Payment terms', text(d['paymentTerms'])),
      InfoRow('Delivery address', text(d['deliveryAddress'])),
      InfoRow('Vendor bill', d['invoicedBillId'] != null ? 'Created' : null),
    ]),
    Text('Lines', style: Theme.of(context).textTheme.titleMedium),
    for (final l in lines)
      ListTile(
        contentPadding: EdgeInsets.zero,
        title: Text('${l['description']}'),
        subtitle: Text('${formatMoney((l['quantity'] as num?) ?? 0)} ${l['unitOfMeasure'] ?? ''} × ${formatMoney((l['unitPrice'] as num?) ?? 0)}\nReceived ${formatMoney((l['receivedQuantity'] as num?) ?? 0)} · Billed ${formatMoney((l['billedQuantity'] as num?) ?? 0)}'),
        isThreeLine: true,
        trailing: Text(formatMoney((l['lineTotal'] as num?) ?? 0)),
      ),
    if ((d['notes'] ?? '').toString().isNotEmpty) ...[const SizedBox(height: 8), Text('Notes', style: Theme.of(context).textTheme.titleMedium), Text(d['notes'].toString())],
  ]);
}
