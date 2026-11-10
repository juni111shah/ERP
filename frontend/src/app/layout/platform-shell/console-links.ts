/** One entry of the founder console's navigation; `badge` shows the count of registrations waiting. */
export interface ConsoleLink { path: string; label: string; icon: string; exact?: boolean; testId: string; badge?: boolean; }

/** The founder console's navigation, in menu order. */
export const CONSOLE_LINKS: readonly ConsoleLink[] = [
  { path: '/platform', label: 'Dashboard', icon: 'dashboard', exact: true, testId: 'platform-nav-dashboard' },
  { path: '/platform/clients', label: 'Clients', icon: 'team', testId: 'platform-nav-clients' },
  { path: '/platform/signups', label: 'Registrations', icon: 'user-add', testId: 'platform-nav-signups', badge: true },
  { path: '/platform/leads', label: 'Leads', icon: 'contacts', testId: 'platform-nav-leads' },
  { path: '/platform/plans', label: 'Plans and pricing', icon: 'tag', testId: 'platform-nav-plans' },
  { path: '/platform/invoices', label: 'Invoices', icon: 'file-text', testId: 'platform-nav-invoices' },
  { path: '/platform/activity', label: 'Activity', icon: 'history', testId: 'platform-nav-activity' },
  { path: '/platform/team', label: 'Team', icon: 'safety-certificate', testId: 'platform-nav-team' },
  { path: '/platform/assistant', label: 'AI assistant', icon: 'thunderbolt', testId: 'platform-nav-assistant' },
  { path: '/platform/settings', label: 'Settings', icon: 'setting', testId: 'platform-nav-settings' },
];
