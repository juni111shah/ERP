import 'package:flutter/material.dart';

import '../../kit/registry.dart';
import 'bill_screens.dart';
import 'payment_screens.dart';

/// Screens of this area for the More menu.
final List<MobileScreen> payablesScreens = <MobileScreen>[
  MobileScreen(id: 'bills', title: 'Bills', menuIds: const ['finance.bills'], icon: Icons.description_outlined, builder: (_) => const BillsScreen()),
  MobileScreen(id: 'payments', title: 'Payments', menuIds: const ['finance.payments'], icon: Icons.payments_outlined, builder: (_) => const PaymentsScreen()),
];
