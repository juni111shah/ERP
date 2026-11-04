import 'package:flutter/material.dart';

import '../../kit/registry.dart';
import 'attendance_screens.dart';
import 'employee_screens.dart';
import 'leave_screens.dart';
import 'life_screens.dart';
import 'payroll_screens.dart';
import 'people_screens.dart';
import 'self_service_screens.dart';
import 'review_screens.dart';
import 'setup_screens.dart';

/// Screens of this area for the More menu. Add a MobileScreen per server menu entry you build.
final List<MobileScreen> hrScreens = <MobileScreen>[
  MobileScreen(id: 'hr-daily-attendance', title: 'Daily attendance', menuIds: const ['hr.dailyattendance'], icon: Icons.event_available_outlined, builder: (_) => const DailyAttendanceScreen()),
  MobileScreen(id: 'hr-statutory', title: 'Statutory rules', menuIds: const ['hr.statutory'], icon: Icons.gavel_outlined, builder: (_) => const StatutoryRulesScreen()),
  MobileScreen(id: 'hr-recruitment', title: 'Recruitment', menuIds: const ['hr.recruitment'], icon: Icons.person_search_outlined, builder: (_) => const RecruitmentScreen()),
  MobileScreen(id: 'hr-checklists', title: 'Checklists', menuIds: const ['hr.checklists'], icon: Icons.checklist, builder: (_) => const ChecklistsScreen()),
  MobileScreen(id: 'hr-tasks', title: 'HR tasks', menuIds: const ['hr.tasks'], icon: Icons.task_alt, builder: (_) => const OpenTasksScreen()),
  MobileScreen(id: 'hr-my', title: 'My HR', menuIds: const ['hr.my'], icon: Icons.person_outline, builder: (_) => const MyHrScreen()),
  MobileScreen(id: 'hr-departments', title: 'Departments', menuIds: const ['hr.departments'], icon: Icons.apartment_outlined, builder: (_) => const DepartmentsScreen()),
  MobileScreen(id: 'hr-positions', title: 'Positions', menuIds: const ['hr.positions'], icon: Icons.badge_outlined, builder: (_) => const PositionsScreen()),
  MobileScreen(id: 'hr-orgchart', title: 'Organisation', menuIds: const ['hr.orgchart'], icon: Icons.account_tree_outlined, builder: (_) => const OrgChartScreen()),
  MobileScreen(id: 'hr-document-expiry', title: 'Document expiry', menuIds: const ['hr.documentexpiry'], icon: Icons.warning_amber_outlined, builder: (_) => const DocumentExpiryScreen()),
  MobileScreen(id: 'hr-loans', title: 'Loans and advances', menuIds: const ['hr.loans'], icon: Icons.request_quote_outlined, builder: (_) => const LoansScreen()),
  MobileScreen(id: 'hr-reports', title: 'HR reports', menuIds: const ['hr.reports'], icon: Icons.bar_chart, builder: (_) => const HrReportsScreen()),
  MobileScreen(id: 'hr-employees', title: 'Employees', menuIds: const ['hr.employees'], icon: Icons.groups_outlined, builder: (_) => const EmployeesScreen()),
  MobileScreen(id: 'hr-payroll', title: 'Payroll', menuIds: const ['hr.payroll'], icon: Icons.account_balance_wallet_outlined, builder: (_) => const PayrollScreen()),
  MobileScreen(id: 'hr-reviews', title: 'Performance reviews', menuIds: const ['hr.reviews'], icon: Icons.insights_outlined, builder: (_) => const ReviewsScreen()),
  MobileScreen(id: 'hr-review-cycles', title: 'Review cycles', menuIds: const ['hr.reviewcycles'], icon: Icons.event_repeat_outlined, builder: (_) => const ReviewCyclesScreen()),
  MobileScreen(id: 'hr-leave', title: 'Leave requests', menuIds: const ['hr.leave'], icon: Icons.beach_access_outlined, builder: (_) => const LeaveScreen()),
  MobileScreen(id: 'hr-leave-balances', title: 'Leave balances', menuIds: const ['hr.leavebalances'], icon: Icons.bar_chart, builder: (_) => const LeaveBalancesScreen()),
  MobileScreen(id: 'hr-attendance', title: 'Attendance', menuIds: const ['hr.attendance'], icon: Icons.fact_check_outlined, builder: (_) => const AttendanceScreen()),
  MobileScreen(id: 'hr-leave-types', title: 'Leave types', menuIds: const ['hr.leavetypes'], icon: Icons.category_outlined, builder: (_) => const LeaveTypesScreen()),
  MobileScreen(id: 'hr-holidays', title: 'Public holidays', menuIds: const ['hr.holidays'], icon: Icons.event_outlined, builder: (_) => const HolidaysScreen()),
];
