import 'package:flutter/material.dart';

import '../../kit/registry.dart';
import 'order_screens.dart';
import 'receipt_screens.dart';
import 'requisition_screens.dart';

/// Screens of this area for the More menu. `purchasing.analytics` is a report page and is not built on the phone.
final List<MobileScreen> purchasingScreens = <MobileScreen>[
  MobileScreen(id: 'purchasing-orders', title: 'Purchase orders', menuIds: const ['purchasing.orders'], icon: Icons.shopping_bag_outlined, builder: (_) => const OrdersScreen()),
  MobileScreen(id: 'purchasing-requisitions', title: 'Purchase requests', menuIds: const ['purchasing.requisitions'], icon: Icons.note_add_outlined, builder: (_) => const RequisitionsScreen()),
  MobileScreen(id: 'purchasing-receipts', title: 'Goods receipts', menuIds: const ['purchasing.receipts'], icon: Icons.move_to_inbox_outlined, builder: (_) => const ReceiptsScreen()),
];
