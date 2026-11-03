import 'package:flutter/material.dart';

import '../../kit/registry.dart';
import 'attendance_screens.dart';
import 'leave_screens.dart';
import 'setup_screens.dart';

/// Screens of this area for the More menu. Add a MobileScreen per server menu entry you build.
final List<MobileScreen> hrScreens = <MobileScreen>[
  MobileScreen(id: 'hr-leave', title: 'Leave requests', menuIds: const ['hr.leave'], icon: Icons.beach_access_outlined, builder: (_) => const LeaveScreen()),
  MobileScreen(id: 'hr-leave-balances', title: 'Leave balances', menuIds: const ['hr.leavebalances'], icon: Icons.bar_chart, builder: (_) => const LeaveBalancesScreen()),
  MobileScreen(id: 'hr-attendance', title: 'Attendance', menuIds: const ['hr.attendance'], icon: Icons.fact_check_outlined, builder: (_) => const AttendanceScreen()),
  MobileScreen(id: 'hr-leave-types', title: 'Leave types', menuIds: const ['hr.leavetypes'], icon: Icons.category_outlined, builder: (_) => const LeaveTypesScreen()),
  MobileScreen(id: 'hr-holidays', title: 'Public holidays', menuIds: const ['hr.holidays'], icon: Icons.event_outlined, builder: (_) => const HolidaysScreen()),
];
