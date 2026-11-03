import 'package:flutter/material.dart';

import '../../kit/registry.dart';
import 'inventory_screens.dart';
import 'manufacturing_screens.dart';
import 'pos_screens.dart';

/// Screens of this area for the More menu: POS, Inventory, Manufacturing.
final List<MobileScreen> opsScreens = <MobileScreen>[
  MobileScreen(id: 'pos.terminal', title: 'POS Terminal', menuIds: const ['pos.terminal'], icon: Icons.point_of_sale, builder: (_) => const PosTerminalScreen()),
  MobileScreen(id: 'pos.sessions', title: 'Shifts & Drawers', menuIds: const ['pos.sessions'], icon: Icons.schedule, builder: (_) => const SessionsScreen()),
  MobileScreen(id: 'pos.orders', title: 'Counter Orders', menuIds: const ['pos.orders'], icon: Icons.shopping_cart_outlined, builder: (_) => const OrdersScreen()),
  MobileScreen(id: 'pos.registers', title: 'Registers', menuIds: const ['pos.registers'], icon: Icons.desktop_windows_outlined, builder: (_) => const RegistersScreen()),
  MobileScreen(id: 'pos.analytics', title: 'POS Analytics', menuIds: const ['pos.analytics'], icon: Icons.bar_chart, builder: (_) => const PosAnalyticsScreen()),
  MobileScreen(id: 'inventory.stock', title: 'Stock on hand', menuIds: const ['inventory.stock'], icon: Icons.inventory_2_outlined, builder: (_) => const StockScreen()),
  MobileScreen(id: 'inventory.movements', title: 'Stock movements', menuIds: const ['inventory.movements'], icon: Icons.swap_horiz, builder: (_) => const MovementsScreen()),
  MobileScreen(id: 'inventory.counts', title: 'Stock counts', menuIds: const ['inventory.counts'], icon: Icons.fact_check_outlined, builder: (_) => const CountsScreen()),
  MobileScreen(id: 'inventory.warehouses', title: 'Warehouses', menuIds: const ['inventory.warehouses'], icon: Icons.warehouse_outlined, builder: (_) => const WarehousesScreen()),
  MobileScreen(id: 'manufacturing.boms', title: 'Bills of materials', menuIds: const ['manufacturing.boms'], icon: Icons.account_tree_outlined, builder: (_) => const BomsScreen()),
  MobileScreen(id: 'manufacturing.workorders', title: 'Work orders', menuIds: const ['manufacturing.workorders'], icon: Icons.build_outlined, builder: (_) => const WorkOrdersScreen()),
  MobileScreen(id: 'manufacturing.costing', title: 'Product costing', menuIds: const ['manufacturing.costing'], icon: Icons.calculate_outlined, builder: (_) => const CostingScreen()),
  MobileScreen(id: 'manufacturing.analytics', title: 'Manufacturing analytics', menuIds: const ['manufacturing.analytics'], icon: Icons.pie_chart_outline, builder: (_) => const MfgAnalyticsScreen()),
];
