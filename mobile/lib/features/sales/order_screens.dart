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
import '../receivables/invoice_screens.dart';
import 'sales_common.dart';

const _path = '/api/v1/m/sales/orders';

const orderStatuses = [
  Option('draft', 'Draft'),
  Option('confirmed', 'Confirmed'),
  Option('processing', 'Processing'),
  Option('delivered', 'Delivered'),
  Option('invoiced', 'Invoiced'),
  Option('cancelled', 'Cancelled'),
];

/// The API only lets a draft, or a confirmed order with nothing delivered yet, be edited.
bool orderEditable(Json d) {
  if (d['status'] == 'draft') return true;
  if (d['status'] != 'confirmed') return false;
  return (d['lines'] as List<dynamic>? ?? const []).every((l) => ((l as Json)['deliveredQuantity'] as num? ?? 0) == 0);
}

FormSpec orderForm(WidgetRef ref, {Json? existing}) {
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
      'customerReference': existing['customerReference'],
      'orderDate': existing['orderDate'],
      'expectedDeliveryDate': existing['expectedDeliveryDate'],
      'paymentTerms': existing['paymentTerms'],
      'shippingAddress': existing['shippingAddress'],
      'notes': existing['notes'],
      'lines': linesToForm(existing['lines'] as List<dynamic>),
    },
  };
  final fields = <Field>[
    Field.select('legalEntityId', 'Legal entity', options: [for (final e in me.legalEntities) Option(e.id, e.name)], required: true, initial: entity?.id),
    Field.lookup('customerPartyId', 'Customer', path: '/api/v1/parties', query: const {'role': 'Customer'}, subtitleKey: 'code', copy: const {'customerName': 'name'}),
    Field.text('customerName', 'Customer name', required: true, maxLength: 200),
    Field.text('customerReference', 'Customer reference', maxLength: 100),
    Field.date('orderDate', 'Order date', required: true, initial: 'today'),
    Field.date('expectedDeliveryDate', 'Expected delivery'),
    Field.text('currency', 'Currency', required: true, maxLength: 10),
    Field.text('paymentTerms', 'Payment terms', maxLength: 200),
    Field.multiline('shippingAddress', 'Shipping address', maxLength: 500),
    Field.lines('lines', 'Lines', fields: documentLineFields()),
    Field.multiline('notes', 'Notes', maxLength: 2000),
  ];
  return FormSpec(
    title: editing ? 'Edit ${existing['number']}' : 'New sales order',
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

/// Records how much of each line has been delivered (cumulative, never lower than before).
FormSpec fulfillmentForm(Json order) {
  final lines = (order['lines'] as List<dynamic>).cast<Json>();
  return FormSpec(
    title: 'Record delivery',
    intro: 'Enter the total delivered so far for each line.',
    initial: {for (var i = 0; i < lines.length; i++) 'del_$i': lines[i]['deliveredQuantity']},
    fields: [
      for (var i = 0; i < lines.length; i++)
        Field.decimal('del_$i', '${lines[i]['description']} (ordered ${formatMoney((lines[i]['quantity'] as num?) ?? 0)})',
            required: true,
            min: (lines[i]['deliveredQuantity'] as num?) ?? 0,
            max: (lines[i]['quantity'] as num?) ?? 0),
    ],
    submit: (ref, body, raw) => ref.read(apiProvider).put('$_path/${order['id']}/fulfillment', body: {
          'lines': [
            for (var i = 0; i < lines.length; i++) {'lineId': lines[i]['id'], 'deliveredQuantity': body['del_$i']},
          ],
        }),
  );
}

Widget orderTile(BuildContext context, Json o) {
  final status = (o['status'] ?? '').toString();
  return ListTile(
    onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => OrderDetailScreen(id: o['id'].toString()))),
    title: Text('${o['number']}', maxLines: 1, overflow: TextOverflow.ellipsis),
    subtitle: Text('${o['customerName'] ?? ''}\nOrdered ${formatDate(o['orderDate'])}'),
    isThreeLine: true,
    trailing: Column(
      mainAxisAlignment: MainAxisAlignment.center,
      crossAxisAlignment: CrossAxisAlignment.end,
      children: [
        Text(money(o, 'totalAmount', currencyKey: 'currency'), style: const TextStyle(fontWeight: FontWeight.w600)),
        const SizedBox(height: 4),
        StatusChip(humanize(status), color: StatusChip.forStatus(status)),
      ],
    ),
  );
}

class OrdersScreen extends StatelessWidget {
  const OrdersScreen({super.key});

  @override
  Widget build(BuildContext context) => ListScreen(
        title: 'Sales orders',
        path: _path,
        searchHint: 'Search orders',
        emptyText: 'No sales orders found.',
        itemBuilder: orderTile,
        filters: orderStatuses,
        create: (ref) => orderForm(ref),
        createPermission: 'sales.orders.manage',
      );
}

ActionSpec _orderStatus(String id, String label, IconData icon, String target, Set<String> from, {bool destructive = false, String? message}) => ActionSpec(
      label: label,
      icon: icon,
      permission: 'sales.orders.manage',
      destructive: destructive,
      visible: (d) => from.contains(d['status']),
      confirm: message,
      successMessage: '$label done',
      run: (ref, d, _) => ref.read(apiProvider).put('$_path/$id/status', body: {'status': target}),
    );

class OrderDetailScreen extends StatelessWidget {
  const OrderDetailScreen({super.key, required this.id});

  final String id;

  @override
  Widget build(BuildContext context) {
    return Consumer(builder: (context, ref, _) {
      return DetailPage(
        path: '$_path/$id',
        titleOf: (d) => '${d['number']}',
        canEdit: (d) => orderEditable(d) && ref.read(meOrEmptyProvider).can('sales.orders.manage'),
        edit: (d) => orderForm(ref, existing: d),
        body: _body,
        actions: [
          _orderStatus(id, 'Confirm', Icons.check_circle_outline, 'confirmed', {'draft'}),
          _orderStatus(id, 'Start processing', Icons.autorenew, 'processing', {'confirmed'}),
          ActionSpec(
            label: 'Record delivery',
            icon: Icons.local_shipping_outlined,
            permission: 'sales.orders.manage',
            visible: (d) => const {'confirmed', 'processing'}.contains(d['status']),
            form: fulfillmentForm,
            run: (ref, d, _) async => null,
          ),
          _orderStatus(id, 'Mark delivered', Icons.inventory_2_outlined, 'delivered', {'confirmed', 'processing'}),
          ActionSpec(
            label: 'Convert to invoice',
            icon: Icons.receipt_long_outlined,
            permission: 'sales.orders.manage',
            visible: (d) => const {'confirmed', 'processing', 'delivered'}.contains(d['status']) && d['invoicedInvoiceId'] == null,
            confirm: 'Create the invoice for this order? Undelivered stock is issued automatically.',
            successMessage: 'Invoice created',
            run: (ref, d, _) => ref.read(apiProvider).post('$_path/$id/convert-to-invoice'),
          ),
          _orderStatus(id, 'Cancel order', Icons.block, 'cancelled', {'draft', 'confirmed', 'processing'}, destructive: true, message: 'Cancel this sales order?'),
          ActionSpec(
            label: 'Delete',
            icon: Icons.delete_outline,
            permission: 'sales.orders.manage',
            destructive: true,
            visible: (d) => d['status'] != 'invoiced',
            confirm: 'Delete this sales order?',
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
      DetailHeader(title: d['customerName']?.toString() ?? '', subtitle: 'Ordered ${formatDate(d['orderDate'])} · Delivery ${formatDate(d['expectedDeliveryDate'])}', status: d['status']?.toString()),
      InfoCard(rows: [
        InfoRow('Customer reference', d['customerReference']?.toString()),
        InfoRow('From quote', d['quoteNumber']?.toString()),
        InfoRow('Payment terms', d['paymentTerms']?.toString()),
        InfoRow('Ship to', d['shippingAddress']?.toString()),
        InfoRow('Subtotal', money(d, 'subTotal', currencyKey: 'currency')),
        InfoRow('Discount', money(d, 'discountTotal', currencyKey: 'currency')),
        InfoRow('Tax', money(d, 'taxTotal', currencyKey: 'currency')),
        InfoRow('Total', money(d, 'totalAmount', currencyKey: 'currency'), bold: true),
      ]),
      if (d['invoicedInvoiceId'] != null)
        ListTile(
          contentPadding: EdgeInsets.zero,
          leading: const Icon(Icons.receipt_long_outlined),
          title: const Text('Open the invoice'),
          trailing: const Icon(Icons.chevron_right),
          onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => InvoiceDetailScreen(id: d['invoicedInvoiceId'].toString()))),
        ),
      documentLines(context, d['lines'] as List<dynamic>? ?? const [], delivered: true),
      if ((d['notes'] ?? '').toString().isNotEmpty) ...[const SizedBox(height: 8), Text('Notes', style: Theme.of(context).textTheme.titleMedium), Text(d['notes'].toString())],
    ]);
  }
}
