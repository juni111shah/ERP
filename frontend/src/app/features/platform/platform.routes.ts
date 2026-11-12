import { Routes } from '@angular/router';

/** The founder's console. The guard and the layout sit on the parent route in app.routes.ts. */
export const PLATFORM_ROUTES: Routes = [
  { path: '', pathMatch: 'full', loadComponent: () => import('./pages/platform-dashboard/platform-dashboard.page').then((m) => m.PlatformDashboardPage) },
  { path: 'clients', loadComponent: () => import('./pages/platform-clients/platform-clients.page').then((m) => m.PlatformClientsPage) },
  { path: 'clients/:orgId', loadComponent: () => import('./pages/platform-client-detail/platform-client-detail.page').then((m) => m.PlatformClientDetailPage) },
  { path: 'signups', loadComponent: () => import('./pages/platform-signups/platform-signups.page').then((m) => m.PlatformSignupsPage) },
  { path: 'leads', loadComponent: () => import('./pages/platform-leads/platform-leads.page').then((m) => m.PlatformLeadsPage) },
  { path: 'plans', loadComponent: () => import('./pages/platform-plans/platform-plans.page').then((m) => m.PlatformPlansPage) },
  { path: 'invoices', loadComponent: () => import('./pages/platform-invoices/platform-invoices.page').then((m) => m.PlatformInvoicesPage) },
  { path: 'activity', loadComponent: () => import('./pages/platform-activity/platform-activity.page').then((m) => m.PlatformActivityPage) },
  { path: 'team', loadComponent: () => import('./pages/platform-team/platform-team.page').then((m) => m.PlatformTeamPage) },
  { path: 'assistant', loadComponent: () => import('./pages/platform-assistant/platform-assistant.page').then((m) => m.PlatformAssistantPage) },
  { path: 'settings', loadComponent: () => import('./pages/platform-settings/platform-settings.page').then((m) => m.PlatformSettingsPage) },
  { path: '**', redirectTo: '' },
];
