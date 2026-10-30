import 'package:flutter/material.dart';

import '../../kit/registry.dart';
import 'invoice_screens.dart';

/// Screens of this area for the More menu. Add a MobileScreen per server menu entry you build.
final List<MobileScreen> receivablesScreens = <MobileScreen>[
  MobileScreen(id: 'invoices', title: 'Invoices', menuIds: const ['finance.invoices'], icon: Icons.receipt_long_outlined, builder: (_) => const InvoicesScreen()),
];
