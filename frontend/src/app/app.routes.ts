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
      { path: 'approvals', loadComponent: () => import('./pages/admin/approvals.page').then((m) => m.ApprovalsPage) },
      { path: 'catalog/items', canMatch: [permissionGuard('core.items.read')], loadComponent: () => import('./pages/admin/items.page').then((m) => m.ItemsPage) },
      { path: 'finance/accounts', canMatch: [permissionGuard('finance.accounts.read')], loadComponent: () => import('./pages/admin/accounts.page').then((m) => m.AccountsPage) },
      { path: 'finance/tax-codes', canMatch: [permissionGuard('finance.tax.read')], loadComponent: () => import('./pages/admin/tax-codes.page').then((m) => m.TaxCodesPage) },
      { path: 'admin/legal-entities', canMatch: [permissionGuard('core.entities.read')], loadComponent: () => import('./pages/admin/legal-entities.page').then((m) => m.LegalEntitiesPage) },
      { path: 'admin/business-units', canMatch: [permissionGuard('core.businessunits.read')], loadComponent: () => import('./pages/admin/business-units.page').then((m) => m.BusinessUnitsPage) },
      { path: 'admin/users', canMatch: [permissionGuard('core.users.read')], loadComponent: () => import('./pages/admin/users.page').then((m) => m.UsersPage) },
      { path: 'admin/roles', canMatch: [permissionGuard('core.roles.read')], loadComponent: () => import('./pages/admin/roles.page').then((m) => m.RolesPage) },
      { path: 'admin/settings', canMatch: [permissionGuard('core.settings.read')], loadComponent: () => import('./pages/admin/settings.page').then((m) => m.SettingsPage) },
      { path: 'admin/audit', canMatch: [permissionGuard('core.audit.read')], loadComponent: () => import('./pages/admin/audit.page').then((m) => m.AuditPage) },
      { path: 'finance', children: [{ path: '**', ...placeholder('Finance') }] },
      { path: 'admin', children: [{ path: '**', ...placeholder('Administration') }] },
      { path: 'inventory', canMatch: [moduleEnabledGuard('inventory')], children: [{ path: '**', ...placeholder('Inventory') }] },
      { path: 'real-estate', canMatch: [moduleEnabledGuard('realestate')], loadChildren: () => import('./modules/realestate/realestate.routes').then((m) => m.REAL_ESTATE_ROUTES) },
      { path: 'forbidden', loadComponent: () => import('./pages/placeholder.page').then((m) => m.PlaceholderPage), data: { title: 'You do not have access to this page' } },
      { path: '**', ...placeholder('Page not found') },
    ],
  },
  { path: '**', redirectTo: 'auth/login' },
];
