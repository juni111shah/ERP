import { ProjectStatus, ProjectTaskStatus } from '@core/api/projects.api';

/** One value of a status list: what the API sends, its name on screen and its tag colour. */
export interface StatusOption { value: string; label: string; color: string; }

/** Names and colours of every status in the Projects module, in one place. The values are what the API sends. */
export const PROJECT_STATUSES: StatusOption[] = [
  { value: 'planning', label: 'Planning', color: 'blue' }, { value: 'active', label: 'Active', color: 'green' }, { value: 'onHold', label: 'On hold', color: 'gold' },
  { value: 'completed', label: 'Completed', color: 'purple' }, { value: 'cancelled', label: 'Cancelled', color: 'red' },
];
export const BILLING_TYPES: StatusOption[] = [
  { value: 'timeAndMaterials', label: 'Time and materials', color: 'blue' }, { value: 'fixedFee', label: 'Fixed fee', color: 'purple' }, { value: 'nonBillable', label: 'Non-billable', color: 'default' },
];
export const TASK_STATUSES: StatusOption[] = [
  { value: 'todo', label: 'To do', color: 'default' }, { value: 'inProgress', label: 'In progress', color: 'blue' }, { value: 'review', label: 'In review', color: 'purple' },
  { value: 'blocked', label: 'Blocked', color: 'red' }, { value: 'done', label: 'Done', color: 'green' }, { value: 'cancelled', label: 'Cancelled', color: 'default' },
];
export const TASK_PRIORITIES: StatusOption[] = [
  { value: 'low', label: 'Low', color: 'default' }, { value: 'medium', label: 'Medium', color: 'blue' }, { value: 'high', label: 'High', color: 'orange' }, { value: 'urgent', label: 'Urgent', color: 'red' },
];
export const MILESTONE_STATUSES: StatusOption[] = [
  { value: 'pending', label: 'Pending', color: 'default' }, { value: 'achieved', label: 'Ready to bill', color: 'blue' }, { value: 'invoiced', label: 'Invoiced', color: 'green' }, { value: 'cancelled', label: 'Cancelled', color: 'default' },
];
export const APPROVAL_STATUSES: StatusOption[] = [
  { value: 'draft', label: 'Draft', color: 'default' }, { value: 'submitted', label: 'Waiting for approval', color: 'blue' }, { value: 'approved', label: 'Approved', color: 'green' }, { value: 'rejected', label: 'Rejected', color: 'red' },
];
export const CHANGE_STATUSES: StatusOption[] = [
  { value: 'draft', label: 'Draft', color: 'default' }, { value: 'approved', label: 'Approved', color: 'green' }, { value: 'rejected', label: 'Rejected', color: 'red' },
];
export const RISK_KINDS: StatusOption[] = [{ value: 'risk', label: 'Risk', color: 'gold' }, { value: 'issue', label: 'Issue', color: 'volcano' }];
export const RISK_SEVERITIES: StatusOption[] = [
  { value: 'low', label: 'Low', color: 'default' }, { value: 'medium', label: 'Medium', color: 'gold' }, { value: 'high', label: 'High', color: 'orange' }, { value: 'critical', label: 'Critical', color: 'red' },
];
export const RISK_STATUSES: StatusOption[] = [{ value: 'open', label: 'Open', color: 'red' }, { value: 'mitigated', label: 'Mitigated', color: 'blue' }, { value: 'closed', label: 'Closed', color: 'green' }];
export const ACCEPTANCE_STATUSES: StatusOption[] = [{ value: 'pending', label: 'Waiting for the customer', color: 'blue' }, { value: 'signed', label: 'Signed', color: 'green' }, { value: 'declined', label: 'Declined', color: 'red' }];
export const HEALTH: StatusOption[] = [{ value: 'onTrack', label: 'On track', color: 'green' }, { value: 'atRisk', label: 'At risk', color: 'orange' }, { value: 'offTrack', label: 'Off track', color: 'red' }, { value: 'closed', label: 'Closed', color: 'default' }];

const KINDS: Record<string, StatusOption[]> = {
  project: PROJECT_STATUSES, billing: BILLING_TYPES, task: TASK_STATUSES, priority: TASK_PRIORITIES, milestone: MILESTONE_STATUSES, approval: APPROVAL_STATUSES,
  change: CHANGE_STATUSES, riskKind: RISK_KINDS, severity: RISK_SEVERITIES, risk: RISK_STATUSES, acceptance: ACCEPTANCE_STATUSES, health: HEALTH,
};
export type StatusKind = 'project' | 'billing' | 'task' | 'priority' | 'milestone' | 'approval' | 'change' | 'riskKind' | 'severity' | 'risk' | 'acceptance' | 'health';

/** Case-insensitive, so a value from an older screen or API still finds its name. */
export function statusOption(kind: StatusKind, value: string | null | undefined): StatusOption {
  const v = (value ?? '').toLowerCase();
  return KINDS[kind].find((o) => o.value.toLowerCase() === v) ?? { value: value ?? '', label: value ?? '', color: 'default' };
}
/** The name of a status value. */
export const statusLabel = (kind: StatusKind, value: string | null | undefined): string => statusOption(kind, value).label;
/** A status list as filter or select options (value and name). */
export const filterOptions = (list: StatusOption[]): { value: string; label: string }[] => list.map((o) => ({ value: o.value, label: o.label }));

/** Where a task can go from each status. The same map as the server (ProjectsTransitions), so screens only offer moves that will work. */
export const TASK_MOVES: Record<ProjectTaskStatus, ProjectTaskStatus[]> = {
  todo: ['inProgress', 'blocked', 'cancelled'], inProgress: ['review', 'todo', 'blocked', 'cancelled'], review: ['done', 'inProgress', 'blocked', 'cancelled'],
  done: ['inProgress'], blocked: ['todo', 'inProgress', 'cancelled'], cancelled: ['todo'],
};
/** The statuses a task can be set to from its current one, the current one included. */
export const taskOptions =(current: ProjectTaskStatus): StatusOption[] => TASK_STATUSES.filter((o) => o.value === current || TASK_MOVES[current]?.includes(o.value as ProjectTaskStatus));

/** A project's status action in words, for the buttons that move it on. */
export const PROJECT_ACTIONS: Record<ProjectStatus, string> = { planning: 'Back to planning', active: 'Start', onHold: 'Put on hold', completed: 'Mark complete', cancelled: 'Cancel project' };

/** The message a refused request carries. */
export function errorText(e: unknown, fallback: string): string {
  const err = (e as { error?: { detail?: string; message?: string; errors?: Record<string, string[]> } })?.error;
  const field = err?.errors ? Object.values(err.errors)[0]?.[0] : undefined;
  return err?.detail ?? field ?? err?.message ?? fallback;
}

/** Shared look of the Projects screens. Uses the theme variables so light, dark and tenant colours all work. */
export const PRJ_STYLES = `
  .prj-toolbar { display:flex; gap:10px; align-items:center; flex-wrap:wrap; margin-bottom:12px; }
  .prj-spacer { flex:1; }
  .prj-card { background: var(--erp-card-bg); border: 1px solid var(--erp-card-border); box-shadow: var(--erp-card-shadow); border-radius:12px; padding:16px; }
  .prj-card h3 { font-size:14px; font-weight:600; margin:0 0 10px; color:var(--erp-color-text-primary,#0f172a); }
  .prj-muted { color:var(--erp-color-text-secondary,#64748b); font-size:12.5px; }
  .prj-link { font-weight:600; color:var(--erp-color-primary,#10955d); text-decoration:none; } .prj-link:hover { text-decoration:underline; }
  .prj-strong { font-weight:600; color:var(--erp-color-text-primary,#0f172a); }
  .prj-danger { color:#cf1322; } .prj-num { text-align:end; white-space:nowrap; font-variant-numeric:tabular-nums; }
  .prj-empty { color:var(--erp-color-text-secondary,#64748b); text-align:center; padding:24px 8px; font-size:13px; }
  .prj-chips { display:flex; gap:6px; flex-wrap:wrap; }
  .prj-chip { border:1px solid var(--erp-color-border-subtle,#e2e8f0); background:var(--erp-color-bg-surface,#fff); color:var(--erp-color-text-secondary,#475569); border-radius:999px; padding:3px 12px; font-size:12.5px; cursor:pointer; }
  .prj-chip:hover { border-color:var(--erp-color-primary,#10955d); }
  .prj-chip.on { background:var(--erp-color-primary,#10955d); border-color:var(--erp-color-primary,#10955d); color:#fff; }
  .prj-table-wrap { overflow-x:auto; }
  .prj-bar { height:6px; border-radius:4px; background:var(--erp-color-bg-subtle,#eef2f6); overflow:hidden; }
  .prj-bar > i { display:block; height:100%; background:var(--erp-color-primary,#10955d); border-radius:4px; } .prj-bar.warn > i { background:#f59e0b; } .prj-bar.over > i { background:#ef4444; }
  @media (max-width: 720px) { .prj-toolbar { align-items:stretch; } .prj-spacer { display:none; } }
`;

/** The segmented sub-view buttons and plain tables inside the project page's tabs. */
export const PRJ_SUBTABS = `
  .seg { display:inline-flex; border:1px solid var(--erp-color-border-subtle,#e2e8f0); border-radius:8px; overflow:hidden; margin-bottom:12px; }
  .seg button { background:var(--erp-color-bg-surface,#fff); border:0; padding:6px 14px; cursor:pointer; color:var(--erp-color-text-secondary,#475569); font-size:13px; } .seg button.on { background:var(--erp-color-primary,#10955d); color:#fff; }
  .plain { width:100%; border-collapse:collapse; font-size:13px; } .plain th { text-align:start; font-weight:500; color:var(--erp-color-text-secondary,#64748b); padding:6px 8px; border-bottom:1px solid var(--erp-color-border-subtle,#e2e8f0); } .plain td { padding:7px 8px; border-bottom:1px solid var(--erp-color-border-subtle,#f1f5f9); }
`;
