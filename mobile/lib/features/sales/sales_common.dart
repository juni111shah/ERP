import 'package:flutter/material.dart';

import '../../core/format.dart';
import '../../kit/detail_page.dart';
import '../../kit/field.dart';
import '../../kit/widgets.dart';

/// Line inputs shared by quotes and sales orders (same shape in the API).
List<Field> documentLineFields() => [
      Field.lookup('itemId', 'Item (optional)', path: '/api/v1/items', subtitleKey: 'sku', copy: const {'description': 'name', 'itemCode': 'sku', 'unitPrice': 'salesPrice'}),
      Field.text('description', 'Description', required: true, maxLength: 500),
      Field.decimal('quantity', 'Quantity', required: true, min: 0.0001, initial: '1'),
      Field.text('unitOfMeasure', 'Unit', maxLength: 20, initial: 'EA'),
      Field.money('unitPrice', 'Unit price', required: true),
      Field.decimal('discountPercent', 'Discount %', min: 0, max: 100, decimals: 2, initial: '0'),
      Field.decimal('taxRate', 'Tax %', min: 0, max: 100, decimals: 2, initial: '0'),
    ];

/// Lines as the form holds them, from a loaded document.
List<Map<String, dynamic>> linesToForm(List<dynamic> lines) => [
      for (final l in lines)
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
    ];

/// The API reads these as non-null strings/numbers; the kit drops empty values, so fill them back in.
Json completeLines(Json body) {
  final lines = [
    for (final l in (body['lines'] as List<dynamic>? ?? const []))
      {
        ...(l as Map<String, dynamic>),
        'itemCode': (l['itemCode'] ?? '').toString(),
        'unitOfMeasure': l['unitOfMeasure'] ?? 'EA',
        'discountPercent': l['discountPercent'] ?? 0,
        'taxRate': l['taxRate'] ?? 0,
      },
  ];
  return {...body, 'lines': lines};
}

num lineTotal(Json l) {
  final base = (toNum(l['quantity']) ?? 0) * (toNum(l['unitPrice']) ?? 0);
  final afterDiscount = base * (1 - (toNum(l['discountPercent']) ?? 0) / 100);
  return afterDiscount * (1 + (toNum(l['taxRate']) ?? 0) / 100);
}

Widget formTotal(BuildContext context, Map<String, dynamic> raw) {
  final total = [for (final l in (raw['lines'] as List<dynamic>)) lineTotal(l as Json)].fold<num>(0, (a, b) => a + b);
  return Padding(padding: const EdgeInsets.only(top: 4), child: InfoRow('Total incl. tax', formatMoney(total), bold: true));
}

Widget documentLines(BuildContext context, List<dynamic> lines, {bool delivered = false}) {
  return Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
    Text('Lines', style: Theme.of(context).textTheme.titleMedium),
    for (final raw in lines)
      Builder(builder: (_) {
        final l = raw as Json;
        return ListTile(
          contentPadding: EdgeInsets.zero,
          title: Text('${l['description']}'),
          subtitle: Text('${formatMoney((l['quantity'] as num?) ?? 0)} ${l['unitOfMeasure'] ?? ''} × ${formatMoney((l['unitPrice'] as num?) ?? 0)}'
              '${delivered ? '\nDelivered ${formatMoney((l['deliveredQuantity'] as num?) ?? 0)}' : ''}'),
          isThreeLine: delivered,
          trailing: Text(formatMoney((l['lineTotal'] as num?) ?? 0)),
        );
      }),
  ]);
}
