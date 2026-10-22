import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/access.guards';

/** Lazy routes of the HR module, mounted under /hr (see app.routes.ts). */
export const HR_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'employees' },
  { path: 'employees', canMatch: [permissionGuard('hr.employees.read')], loadComponent: () => import('./employees.page').then((m) => m.EmployeesPage) },
  { path: 'attendance', canMatch: [permissionGuard('hr.attendance.read')], loadComponent: () => import('./attendance.page').then((m) => m.AttendancePage) },
  { path: 'payroll', canMatch: [permissionGuard('hr.payroll.read')], loadComponent: () => import('./payroll.page').then((m) => m.PayrollPage) },
  { path: 'payroll/:id', canMatch: [permissionGuard('hr.payroll.read')], loadComponent: () => import('./payroll-detail.page').then((m) => m.PayrollDetailPage) },
];
