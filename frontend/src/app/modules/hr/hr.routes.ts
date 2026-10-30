import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/access.guards';

/** Lazy routes of the HR module, mounted under /hr (see app.routes.ts). */
export const HR_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'employees' },
  { path: 'employees', canMatch: [permissionGuard('hr.employees.read')], loadComponent: () => import('./employees.page').then((m) => m.EmployeesPage) },
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
