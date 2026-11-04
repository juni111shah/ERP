import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/access.guards';

/** Lazy routes of the HR module, mounted under /hr (see app.routes.ts). */
export const HR_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'employees' },
  { path: 'employees', canMatch: [permissionGuard('hr.employees.read')], loadComponent: () => import('./employees.page').then((m) => m.EmployeesPage) },
  { path: 'my', loadComponent: () => import('./my-hr.page').then((m) => m.MyHrPage) },
  { path: 'departments', canMatch: [permissionGuard('hr.employees.read')], loadComponent: () => import('./departments.page').then((m) => m.DepartmentsPage) },
  { path: 'positions', canMatch: [permissionGuard('hr.employees.read')], loadComponent: () => import('./positions.page').then((m) => m.PositionsPage) },
  { path: 'org-chart', canMatch: [permissionGuard('hr.employees.read')], loadComponent: () => import('./org-chart.page').then((m) => m.OrgChartPage) },
  { path: 'documents-expiry', canMatch: [permissionGuard('hr.employees.read')], loadComponent: () => import('./expiring-documents.page').then((m) => m.ExpiringDocumentsPage) },
  { path: 'statutory-rules', canMatch: [permissionGuard('hr.payroll.read')], loadComponent: () => import('./statutory-rules.page').then((m) => m.StatutoryRulesPage) },
  { path: 'daily-attendance', canMatch: [permissionGuard('hr.attendance.read')], loadComponent: () => import('./daily-attendance.page').then((m) => m.DailyAttendancePage) },
  { path: 'checklists', canMatch: [permissionGuard('hr.employees.read')], loadComponent: () => import('./checklists.page').then((m) => m.ChecklistsPage) },
  { path: 'tasks', canMatch: [permissionGuard('hr.employees.read')], loadComponent: () => import('./open-tasks.page').then((m) => m.OpenTasksPage) },
  { path: 'recruitment', canMatch: [permissionGuard('hr.recruitment.read')], loadComponent: () => import('./recruitment.page').then((m) => m.RecruitmentPage) },
  { path: 'loans', canMatch: [permissionGuard('hr.payroll.read')], loadComponent: () => import('./loans.page').then((m) => m.LoansPage) },
  { path: 'reports', canMatch: [permissionGuard('hr.employees.read')], loadComponent: () => import('./hr-reports.page').then((m) => m.HrReportsPage) },
  { path: 'attendance', canMatch: [permissionGuard('hr.attendance.read')], loadComponent: () => import('./attendance.page').then((m) => m.AttendancePage) },
  { path: 'payroll', canMatch: [permissionGuard('hr.payroll.read')], loadComponent: () => import('./payroll.page').then((m) => m.PayrollPage) },
  { path: 'reviews', canMatch: [permissionGuard('hr.reviews.read')], loadComponent: () => import('./reviews.page').then((m) => m.ReviewsPage) },
  { path: 'review-cycles', canMatch: [permissionGuard('hr.reviews.read')], loadComponent: () => import('./review-cycles.page').then((m) => m.ReviewCyclesPage) },
  { path: 'leave', canMatch: [permissionGuard('hr.leave.read')], loadComponent: () => import('./leave.page').then((m) => m.LeavePage) },
  { path: 'leave-balances', canMatch: [permissionGuard('hr.leave.read')], loadComponent: () => import('./leave-balances.page').then((m) => m.LeaveBalancesPage) },
  { path: 'leave-types', canMatch: [permissionGuard('hr.leave.read')], loadComponent: () => import('./leave-types.page').then((m) => m.LeaveTypesPage) },
  { path: 'holidays', canMatch: [permissionGuard('hr.leave.read')], loadComponent: () => import('./holidays.page').then((m) => m.HolidaysPage) },
  { path: 'payroll/:id', canMatch: [permissionGuard('hr.payroll.read')], loadComponent: () => import('./payroll-detail.page').then((m) => m.PayrollDetailPage) },
];
