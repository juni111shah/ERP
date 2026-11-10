import { Routes } from '@angular/router';
import { permissionGuard } from '@core/guards/access.guards';

/** Lazy routes of the HR module, mounted under /hr (see app.routes.ts). */
export const HR_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'employees' },
  { path: 'employees', canMatch: [permissionGuard('hr.employees.read')], loadComponent: () => import('./pages/employees/employees.page').then((m) => m.EmployeesPage) },
  { path: 'my', loadComponent: () => import('./pages/my-hr/my-hr.page').then((m) => m.MyHrPage) },
  { path: 'departments', canMatch: [permissionGuard('hr.employees.read')], loadComponent: () => import('./pages/departments/departments.page').then((m) => m.DepartmentsPage) },
  { path: 'positions', canMatch: [permissionGuard('hr.employees.read')], loadComponent: () => import('./pages/positions/positions.page').then((m) => m.PositionsPage) },
  { path: 'org-chart', canMatch: [permissionGuard('hr.employees.read')], loadComponent: () => import('./pages/org-chart/org-chart.page').then((m) => m.OrgChartPage) },
  { path: 'documents-expiry', canMatch: [permissionGuard('hr.employees.read')], loadComponent: () => import('./pages/expiring-documents/expiring-documents.page').then((m) => m.ExpiringDocumentsPage) },
  { path: 'statutory-rules', canMatch: [permissionGuard('hr.payroll.read')], loadComponent: () => import('./pages/statutory-rules/statutory-rules.page').then((m) => m.StatutoryRulesPage) },
  { path: 'daily-attendance', canMatch: [permissionGuard('hr.attendance.read')], loadComponent: () => import('./pages/daily-attendance/daily-attendance.page').then((m) => m.DailyAttendancePage) },
  { path: 'shifts', canMatch: [permissionGuard('hr.attendance.read')], loadComponent: () => import('./pages/shifts/shifts.page').then((m) => m.ShiftsPage) },
  { path: 'checklists', canMatch: [permissionGuard('hr.employees.read')], loadComponent: () => import('./pages/checklists/checklists.page').then((m) => m.ChecklistsPage) },
  { path: 'tasks', canMatch: [permissionGuard('hr.employees.read')], loadComponent: () => import('./pages/open-tasks/open-tasks.page').then((m) => m.OpenTasksPage) },
  { path: 'recruitment', canMatch: [permissionGuard('hr.recruitment.read')], loadComponent: () => import('./pages/recruitment/recruitment.page').then((m) => m.RecruitmentPage) },
  { path: 'loans', canMatch: [permissionGuard('hr.payroll.read')], loadComponent: () => import('./pages/loans/loans.page').then((m) => m.LoansPage) },
  { path: 'reports', canMatch: [permissionGuard('hr.employees.read')], loadComponent: () => import('./pages/hr-reports/hr-reports.page').then((m) => m.HrReportsPage) },
  { path: 'attendance', canMatch: [permissionGuard('hr.attendance.read')], loadComponent: () => import('./pages/attendance/attendance.page').then((m) => m.AttendancePage) },
  { path: 'payroll', canMatch: [permissionGuard('hr.payroll.read')], loadComponent: () => import('./pages/payroll/payroll.page').then((m) => m.PayrollPage) },
  { path: 'reviews', canMatch: [permissionGuard('hr.reviews.read')], loadComponent: () => import('./pages/reviews/reviews.page').then((m) => m.ReviewsPage) },
  { path: 'review-cycles', canMatch: [permissionGuard('hr.reviews.read')], loadComponent: () => import('./pages/review-cycles/review-cycles.page').then((m) => m.ReviewCyclesPage) },
  { path: 'leave', canMatch: [permissionGuard('hr.leave.read')], loadComponent: () => import('./pages/leave/leave.page').then((m) => m.LeavePage) },
  { path: 'leave-balances', canMatch: [permissionGuard('hr.leave.read')], loadComponent: () => import('./pages/leave-balances/leave-balances.page').then((m) => m.LeaveBalancesPage) },
  { path: 'leave-types', canMatch: [permissionGuard('hr.leave.read')], loadComponent: () => import('./pages/leave-types/leave-types.page').then((m) => m.LeaveTypesPage) },
  { path: 'holidays', canMatch: [permissionGuard('hr.leave.read')], loadComponent: () => import('./pages/holidays/holidays.page').then((m) => m.HolidaysPage) },
  { path: 'payroll/:id', canMatch: [permissionGuard('hr.payroll.read')], loadComponent: () => import('./pages/payroll-detail/payroll-detail.page').then((m) => m.PayrollDetailPage) },
];
