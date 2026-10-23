import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/guards/access.guards';

/** Lazy routes of the real estate module, mounted under /real-estate (see app.routes.ts). */
export const REAL_ESTATE_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'properties' },
  { path: 'properties', canMatch: [permissionGuard('realestate.properties.read')], loadComponent: () => import('./properties.page').then((m) => m.PropertiesPage) },
  { path: 'properties/:id', canMatch: [permissionGuard('realestate.properties.read')], loadComponent: () => import('./property-detail.page').then((m) => m.PropertyDetailPage) },
  { path: 'units', canMatch: [permissionGuard('realestate.units.read')], loadComponent: () => import('./units.page').then((m) => m.UnitsPage) },
  { path: 'unit-types', canMatch: [permissionGuard('realestate.unittypes.read')], loadComponent: () => import('./unit-types.page').then((m) => m.UnitTypesPage) },
  { path: 'leases', canMatch: [permissionGuard('realestate.leases.read')], loadComponent: () => import('./leases.page').then((m) => m.LeasesPage) },
  { path: 'leases/:id', canMatch: [permissionGuard('realestate.leases.read')], loadComponent: () => import('./lease-detail.page').then((m) => m.LeaseDetailPage) },
  { path: 'acquisitions', canMatch: [permissionGuard('realestate.acquisitions.read')], loadComponent: () => import('./acquisitions.page').then((m) => m.AcquisitionsPage) },
  { path: 'acquisitions/:id', canMatch: [permissionGuard('realestate.acquisitions.read')], loadComponent: () => import('./acquisition-detail.page').then((m) => m.AcquisitionDetailPage) },
  { path: 'billing', canMatch: [permissionGuard('realestate.billing.run')], loadComponent: () => import('./billing.page').then((m) => m.BillingPage) },
  { path: 'work-orders', canMatch: [permissionGuard('realestate.workorders.read')], loadComponent: () => import('./work-orders.page').then((m) => m.WorkOrdersPage) },
  { path: 'renewals', canMatch: [permissionGuard('realestate.leases.renew')], loadComponent: () => import('./renewals.page').then((m) => m.RenewalsPage) },
  { path: 'preventive-maintenance', canMatch: [permissionGuard('realestate.pm.read')], loadComponent: () => import('./pm-schedules.page').then((m) => m.PmSchedulesPage) },
  { path: 'inspections', canMatch: [permissionGuard('realestate.inspections.read')], loadComponent: () => import('./inspections.page').then((m) => m.InspectionsPage) },
  { path: 'listings', canMatch: [permissionGuard('realestate.crm.read')], loadComponent: () => import('./listings.page').then((m) => m.ListingsPage) },
  { path: 'leads', canMatch: [permissionGuard('realestate.crm.read')], loadComponent: () => import('./leads.page').then((m) => m.LeadsPage) },
  { path: 'deals', canMatch: [permissionGuard('realestate.deals.read')], loadComponent: () => import('./deals.page').then((m) => m.DealsPage) },
];
