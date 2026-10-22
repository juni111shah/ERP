import { Routes } from '@angular/router';
import { authGuard, moduleEnabledGuard, permissionGuard } from './core/guards/access.guards';

const placeholder = (title: string) => ({ loadComponent: () => import('./pages/placeholder.page').then((m) => m.PlaceholderPage), data: { title } });

export const routes: Routes = [
  {
    path: 'auth',
    children: [
      { path: 'login', loadComponent: () => import('./pages/login.page').then((m) => m.LoginPage) },
      { path: 'forgot-password', loadComponent: () => import('./pages/forgot-password.page').then((m) => m.ForgotPasswordPage) },
      { path: 'reset-password', loadComponent: () => import('./pages/reset-password.page').then((m) => m.ResetPasswordPage) },
      { path: '', pathMatch: 'full', redirectTo: 'login' },
    ],
  },
  { path: 'onboarding', loadComponent: () => import('./pages/onboarding.page').then((m) => m.OnboardingPage) },
  {
    path: '',
    canMatch: [authGuard],
    loadComponent: () => import('./layout/shell.component').then((m) => m.ShellComponent),
    children: [
      { path: '', pathMatch: 'full', loadComponent: () => import('./pages/dashboard.page').then((m) => m.DashboardPage) },
      { path: 'parties', canMatch: [permissionGuard('core.parties.read')], loadComponent: () => import('./pages/parties.page').then((m) => m.PartiesPage) },
      { path: 'admin/custom-fields', canMatch: [permissionGuard('core.customfields.read')], loadComponent: () => import('./pages/custom-fields.page').then((m) => m.CustomFieldsPage) },
      { path: 'admin/modules', canMatch: [permissionGuard('core.settings.read')], loadComponent: () => import('./pages/modules.page').then((m) => m.ModulesPage) },
      { path: 'approvals', ...placeholder('Approvals') },
      { path: 'catalog/items', ...placeholder('Items') },
      { path: 'finance', children: [{ path: '**', ...placeholder('Finance') }] },
      { path: 'admin', children: [{ path: '**', ...placeholder('Administration') }] },
      { path: 'inventory', canMatch: [moduleEnabledGuard('inventory')], children: [{ path: '**', ...placeholder('Inventory') }] },
      { path: 'real-estate', canMatch: [moduleEnabledGuard('realestate')], children: [{ path: '**', ...placeholder('Real estate') }] },
      { path: 'forbidden', loadComponent: () => import('./pages/placeholder.page').then((m) => m.PlaceholderPage), data: { title: 'You do not have access to this page' } },
      { path: '**', ...placeholder('Page not found') },
    ],
  },
  { path: '**', redirectTo: 'auth/login' },
];
