import 'package:flutter/material.dart';

import '../features/crm/screens.dart';
import '../features/dashboard/dashboard_page.dart';
import '../features/hr/screens.dart';
import '../features/ops/screens.dart';
import '../features/payables/screens.dart';
import '../features/projects/screens.dart';
import '../features/purchasing/screens.dart';
import '../features/realestate/screens.dart';
import '../features/receivables/screens.dart';
import '../features/sales/screens.dart';
import '../kit/registry.dart';

/// Every screen the app has, by area. The More screen matches these to the server menu by id.
final List<MobileScreen> allScreens = <MobileScreen>[
  MobileScreen(
    id: 'overview',
    title: 'Overview',
    menuIds: const ['core.overview'],
    icon: Icons.dashboard_outlined,
    builder: (_) => Scaffold(appBar: AppBar(title: const Text('Overview')), body: const DashboardPage()),
  ),
  ...receivablesScreens,
  ...payablesScreens,
  ...salesScreens,
  ...opsScreens,
  ...purchasingScreens,
  ...hrScreens,
  ...crmScreens,
  ...projectsScreens,
  ...realestateScreens,
];
