import 'package:flutter/material.dart';

import '../../kit/registry.dart';
import 'projects_screens.dart';
import 'services_screens.dart';

/// Projects and Services screens for the More menu.
final List<MobileScreen> projectsScreens = <MobileScreen>[
  MobileScreen(id: 'projects-projects', title: 'Projects', menuIds: const ['projects.projects'], icon: Icons.work_outline, builder: (_) => const ProjectsScreen()),
  MobileScreen(id: 'projects-tasks', title: 'Tasks', menuIds: const ['projects.tasks'], icon: Icons.checklist, builder: (_) => const TasksScreen()),
  MobileScreen(id: 'projects-timesheets', title: 'Timesheets', menuIds: const ['projects.timesheets'], icon: Icons.schedule, builder: (_) => const TimesheetsScreen()),
  MobileScreen(id: 'projects-billing', title: 'Project billing', menuIds: const ['projects.billing'], icon: Icons.request_quote_outlined, builder: (_) => const BillingScreen()),
  MobileScreen(id: 'services-engagements', title: 'Engagements', menuIds: const ['services.engagements'], icon: Icons.handshake_outlined, builder: (_) => const EngagementsScreen()),
  MobileScreen(id: 'services-retainers', title: 'Retainers', menuIds: const ['services.retainers'], icon: Icons.autorenew, builder: (_) => const RetainersScreen()),
  MobileScreen(id: 'services-milestones', title: 'Milestone billing', menuIds: const ['services.milestones'], icon: Icons.flag_outlined, builder: (_) => const ServiceMilestonesScreen()),
];
