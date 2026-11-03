import 'package:flutter/material.dart';

import '../../kit/detail_page.dart';
import '../../kit/field.dart';
import '../common/paged_list_view.dart';

const purchasingPath = '/api/v1/m/purchasing';
const ordersPath = '$purchasingPath/orders';

Color purchasingStatusColor(String status) => switch (status) {
      'approved' || 'received' || 'billed' => Colors.green.shade700,
      'submitted' || 'pendingApproval' || 'partiallyReceived' || 'ordered' => Colors.blue.shade700,
      'rejected' || 'cancelled' => Colors.red.shade700,
      'draft' => Colors.grey.shade700,
      _ => Colors.blueGrey,
    };

Widget purchasingStatusChip(String status, String label) => StatusChip(label, color: purchasingStatusColor(status));

const orderStatuses = [
  Option('draft', 'Draft'),
  Option('submitted', 'Submitted'),
  Option('approved', 'Approved'),
  Option('partiallyReceived', 'Partly received'),
  Option('received', 'Received'),
  Option('billed', 'Billed'),
  Option('cancelled', 'Cancelled'),
];

const requisitionStatuses = [
  Option('draft', 'Draft'),
  Option('pendingApproval', 'Pending approval'),
  Option('approved', 'Approved'),
  Option('rejected', 'Rejected'),
  Option('ordered', 'Ordered'),
];

/// The server needs a code and unit on every line; the line editor only asks for the unit, the code comes from the picked item.
List<Map<String, dynamic>> withLineDefaults(List<dynamic> bodyLines, List<dynamic> rawLines, {Map<String, dynamic> extra = const {}}) => [
      for (var i = 0; i < bodyLines.length; i++)
        {
          'itemCode': (i < rawLines.length ? (rawLines[i] as Map)['itemCode'] : null) ?? '',
          'unitOfMeasure': 'EA',
          ...extra,
          ...(bodyLines[i] as Map<String, dynamic>),
        },
    ];

/// Line values of an existing document for the line editor (keeps itemCode, which the editor has no box for).
List<Map<String, dynamic>> editableLines(Json d, List<String> keys) => [
      for (final l in (d['lines'] as List<dynamic>? ?? const [])) {for (final k in keys) k: (l as Json)[k]},
    ];
