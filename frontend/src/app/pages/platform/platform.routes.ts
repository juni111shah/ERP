import { Routes } from '@angular/router';

/** The founder's console. The guard and the layout sit on the parent route in app.routes.ts. */
export const PLATFORM_ROUTES: Routes = [
  { path: '', pathMatch: 'full', loadComponent: () => import('./platform-dashboard.page').then((m) => m.PlatformDashboardPage) },
  { path: 'clients', loadComponent: () => import('./platform-clients.page').then((m) => m.PlatformClientsPage) },
  { path: 'clients/:orgId', loadComponent: () => import('./platform-client-detail.page').then((m) => m.PlatformClientDetailPage) },
  { path: 'signups', loadComponent: () => import('./platform-signups.page').then((m) => m.PlatformSignupsPage) },
  { path: 'plans', loadComponent: () => import('./platform-plans.page').then((m) => m.PlatformPlansPage) },
  { path: 'invoices', loadComponent: () => import('./platform-invoices.page').then((m) => m.PlatformInvoicesPage) },
  { path: 'activity', loadComponent: () => import('./platform-activity.page').then((m) => m.PlatformActivityPage) },
  { path: 'team', loadComponent: () => import('./platform-team.page').then((m) => m.PlatformTeamPage) },
  { path: 'assistant', loadComponent: () => import('./platform-assistant.page').then((m) => m.PlatformAssistantPage) },
  { path: 'settings', loadComponent: () => import('./platform-settings.page').then((m) => m.PlatformSettingsPage) },
  { path: '**', redirectTo: '' },
];
