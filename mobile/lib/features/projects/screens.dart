import 'package:flutter/material.dart';

import '../../kit/registry.dart';
import 'projects_extra_screens.dart';
import 'projects_screens.dart';
import 'services_screens.dart';

/// Projects and Services screens for the More menu.
final List<MobileScreen> projectsScreens = <MobileScreen>[
  MobileScreen(id: 'projects-projects', title: 'Projects', menuIds: const ['projects.projects'], icon: Icons.work_outline, builder: (_) => const ProjectsScreen()),
  MobileScreen(id: 'projects-tasks', title: 'Tasks', menuIds: const ['projects.tasks'], icon: Icons.checklist, builder: (_) => const TasksScreen()),
  MobileScreen(id: 'projects-timesheets', title: 'Timesheets', menuIds: const ['projects.timesheets'], icon: Icons.schedule, builder: (_) => const TimesheetsScreen()),
  MobileScreen(id: 'projects-billing', title: 'Project billing', menuIds: const ['projects.billing'], icon: Icons.request_quote_outlined, builder: (_) => const BillingScreen()),
  MobileScreen(id: 'projects-week', title: 'My week', menuIds: const ['projects.week'], icon: Icons.calendar_view_week_outlined, builder: (_) => const MyWeekScreen()),
  MobileScreen(id: 'projects-expenses', title: 'Expenses', menuIds: const ['projects.expenses'], icon: Icons.receipt_long_outlined, builder: (_) => const ExpensesScreen()),
  MobileScreen(id: 'projects-approvals', title: 'Timesheet approvals', menuIds: const ['projects.approvals'], icon: Icons.fact_check_outlined, builder: (_) => const ApprovalsScreen()),
  MobileScreen(id: 'projects-reports', title: 'Project reports', menuIds: const ['projects.reports'], icon: Icons.bar_chart_outlined, builder: (_) => const ProjectReportsScreen()),
  MobileScreen(id: 'projects-resources', title: 'Resources', menuIds: const ['projects.resources'], icon: Icons.groups_outlined, builder: (_) => const ResourcesScreen()),
  MobileScreen(id: 'projects-changeorders', title: 'Change orders', menuIds: const ['projects.changeorders'], icon: Icons.swap_horiz, builder: (_) => const ChangeOrdersScreen()),
  MobileScreen(id: 'projects-risks', title: 'Risks and issues', menuIds: const ['projects.risks'], icon: Icons.warning_amber_outlined, builder: (_) => const RisksScreen()),
  MobileScreen(id: 'services-engagements', title: 'Engagements', menuIds: const ['services.engagements'], icon: Icons.handshake_outlined, builder: (_) => const EngagementsScreen()),
  MobileScreen(id: 'services-retainers', title: 'Retainers', menuIds: const ['services.retainers'], icon: Icons.autorenew, builder: (_) => const RetainersScreen()),
  MobileScreen(id: 'services-milestones', title: 'Milestone billing', menuIds: const ['services.milestones'], icon: Icons.flag_outlined, builder: (_) => const ServiceMilestonesScreen()),
];
