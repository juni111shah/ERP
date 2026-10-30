import 'package:flutter/material.dart';

import '../../kit/registry.dart';
import 'property_screens.dart';

/// Screens of this area for the More menu.
final List<MobileScreen> realestateScreens = <MobileScreen>[
  MobileScreen(id: 're-properties', title: 'Properties', menuIds: const ['realestate.properties'], icon: Icons.apartment, builder: (_) => const PropertiesScreen()),
  MobileScreen(id: 're-units', title: 'Units', menuIds: const ['realestate.units'], icon: Icons.meeting_room_outlined, builder: (_) => const UnitsScreen()),
  MobileScreen(id: 're-unittypes', title: 'Unit types', menuIds: const ['realestate.unittypes'], icon: Icons.category_outlined, builder: (_) => const UnitTypesScreen()),
];
