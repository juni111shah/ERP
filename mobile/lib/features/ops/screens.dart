import 'package:flutter/material.dart';

import '../../kit/registry.dart';
import 'pos_screens.dart';

/// Screens of this area for the More menu: POS, Inventory, Manufacturing.
final List<MobileScreen> opsScreens = <MobileScreen>[
  MobileScreen(id: 'pos.terminal', title: 'POS Terminal', menuIds: const ['pos.terminal'], icon: Icons.point_of_sale, builder: (_) => const PosTerminalScreen()),
  MobileScreen(id: 'pos.sessions', title: 'Shifts & Drawers', menuIds: const ['pos.sessions'], icon: Icons.schedule, builder: (_) => const SessionsScreen()),
  MobileScreen(id: 'pos.orders', title: 'Counter Orders', menuIds: const ['pos.orders'], icon: Icons.shopping_cart_outlined, builder: (_) => const OrdersScreen()),
  MobileScreen(id: 'pos.registers', title: 'Registers', menuIds: const ['pos.registers'], icon: Icons.desktop_windows_outlined, builder: (_) => const RegistersScreen()),
  MobileScreen(id: 'pos.analytics', title: 'POS Analytics', menuIds: const ['pos.analytics'], icon: Icons.bar_chart, builder: (_) => const PosAnalyticsScreen()),
];
