import 'package:flutter/material.dart';

import '../../kit/registry.dart';
import 'crm_screens.dart';

/// Screens of this area for the More menu. crm.analytics is not built (dashboards only).
final List<MobileScreen> crmScreens = <MobileScreen>[
  MobileScreen(id: 'crm-leads', title: 'Leads & contacts', menuIds: const ['crm.leads'], icon: Icons.person_add_alt_outlined, builder: (_) => const LeadsScreen()),
  MobileScreen(id: 'crm-opportunities', title: 'Sales pipeline', menuIds: const ['crm.opportunities'], icon: Icons.filter_alt_outlined, builder: (_) => const OpportunitiesScreen()),
  MobileScreen(id: 'crm-interactions', title: 'Activity logs', menuIds: const ['crm.interactions'], icon: Icons.phone_outlined, builder: (_) => const InteractionsScreen()),
];
