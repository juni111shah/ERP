import 'package:flutter/material.dart';

import '../../kit/registry.dart';
import 'order_screens.dart';
import 'quote_screens.dart';

/// Screens of this area for the More menu (Sales, Retail, Distribution).
final List<MobileScreen> salesScreens = <MobileScreen>[
  MobileScreen(id: 'sales-quotes', title: 'Quotes', menuIds: const ['sales.quotes'], icon: Icons.description_outlined, builder: (_) => const QuotesScreen()),
  MobileScreen(id: 'sales-orders', title: 'Sales orders', menuIds: const ['sales.orders'], icon: Icons.shopping_cart_outlined, builder: (_) => const OrdersScreen()),
];
