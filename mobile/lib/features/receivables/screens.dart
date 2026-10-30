import 'package:flutter/material.dart';

import '../../kit/registry.dart';
import 'invoice_screens.dart';
import 'party_screens.dart';
import 'receipt_screens.dart';
import 'report_screens.dart';

/// Screens of this area for the More menu. Add a MobileScreen per server menu entry you build.
final List<MobileScreen> receivablesScreens = <MobileScreen>[
  MobileScreen(id: 'invoices', title: 'Invoices', menuIds: const ['finance.invoices'], icon: Icons.receipt_long_outlined, builder: (_) => const InvoicesScreen()),
  MobileScreen(id: 'receipts', title: 'Receipts', menuIds: const ['finance.receipts'], icon: Icons.payments_outlined, builder: (_) => const ReceiptsScreen()),
  MobileScreen(id: 'customers', title: 'Customers', menuIds: const ['sales.customers'], icon: Icons.people_outline, builder: (_) => const PartiesScreen(title: 'Customers', role: 'Customer')),
  MobileScreen(id: 'vendors', title: 'Vendors', menuIds: const ['purchases.vendors'], icon: Icons.local_shipping_outlined, builder: (_) => const PartiesScreen(title: 'Vendors', role: 'Vendor')),
  MobileScreen(id: 'parties', title: 'All parties', menuIds: const ['core.parties'], icon: Icons.groups_outlined, builder: (_) => const PartiesScreen(title: 'All parties')),
  MobileScreen(id: 'partygroups', title: 'Party groups', menuIds: const ['core.partygroups'], icon: Icons.workspaces_outline, builder: (_) => const PartyGroupsScreen()),
  MobileScreen(id: 'items', title: 'Items', menuIds: const ['core.items'], icon: Icons.inventory_2_outlined, builder: (_) => const ItemsScreen()),
  MobileScreen(id: 'ar-ageing', title: 'Receivables ageing', menuIds: const ['finance.ar-ageing'], icon: Icons.hourglass_bottom, builder: (_) => const ArAgeingScreen()),
];
