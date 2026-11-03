import 'package:flutter/material.dart';

import '../../kit/registry.dart';
import 'acquisition_screens.dart';
import 'crm_screens.dart';
import 'lease_screens.dart';
import 'maintenance_screens.dart';
import 'property_screens.dart';

/// Screens of this area for the More menu.
final List<MobileScreen> realestateScreens = <MobileScreen>[
  MobileScreen(id: 're-properties', title: 'Properties', menuIds: const ['realestate.properties'], icon: Icons.apartment, builder: (_) => const PropertiesScreen()),
  MobileScreen(id: 're-units', title: 'Units', menuIds: const ['realestate.units'], icon: Icons.meeting_room_outlined, builder: (_) => const UnitsScreen()),
  MobileScreen(id: 're-unittypes', title: 'Unit types', menuIds: const ['realestate.unittypes'], icon: Icons.category_outlined, builder: (_) => const UnitTypesScreen()),
  MobileScreen(id: 're-leases', title: 'Leases', menuIds: const ['realestate.leases'], icon: Icons.description_outlined, builder: (_) => const LeasesScreen()),
  MobileScreen(id: 're-billing', title: 'Rent billing', menuIds: const ['realestate.billing'], icon: Icons.payments_outlined, builder: (_) => const BillingScreen()),
  MobileScreen(id: 're-renewals', title: 'Lease renewals', menuIds: const ['realestate.renewals'], icon: Icons.autorenew, builder: (_) => const RenewalsScreen()),
  MobileScreen(id: 're-workorders', title: 'Work orders', menuIds: const ['realestate.workorders'], icon: Icons.build_outlined, builder: (_) => const WorkOrdersScreen()),
  MobileScreen(id: 're-pm', title: 'Preventive maintenance', menuIds: const ['realestate.pm'], icon: Icons.event_repeat, builder: (_) => const PreventiveMaintenanceScreen()),
  MobileScreen(id: 're-inspections', title: 'Inspections', menuIds: const ['realestate.inspections'], icon: Icons.fact_check_outlined, builder: (_) => const InspectionsScreen()),
  MobileScreen(id: 're-acquisitions', title: 'Acquisitions', menuIds: const ['realestate.acquisitions'], icon: Icons.account_balance_outlined, builder: (_) => const AcquisitionsScreen()),
  MobileScreen(id: 're-listings', title: 'Listings', menuIds: const ['realestate.listings'], icon: Icons.storefront_outlined, builder: (_) => const ListingsScreen()),
  MobileScreen(id: 're-leads', title: 'Leads', menuIds: const ['realestate.leads'], icon: Icons.group_outlined, builder: (_) => const LeadsScreen()),
  MobileScreen(id: 're-deals', title: 'Deals', menuIds: const ['realestate.deals'], icon: Icons.handshake_outlined, builder: (_) => const DealsScreen()),
];
