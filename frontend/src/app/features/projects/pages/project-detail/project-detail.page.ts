import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { map } from 'rxjs';
import { PartiesApi } from '@core/api/parties.api';
import { ProjectsExtraApi } from '@core/api/projects-extra.api';
import { ProjectDto, ProjectInput, ProjectsApi, ProjectStatus } from '@core/api/projects.api';
import { toIsoDate } from '@core/format/dates';
import { doneText } from '@core/format/done-text';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { SessionStore } from '@core/session/session.store';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { MasterDataState } from '@shared/utils/master-data-state';
import { LogTimeComponent } from '../../components/log-time/log-time.component';
import { ProjectControlComponent } from '../../components/project-control/project-control.component';
import { ProjectMilestonesComponent } from '../../components/project-milestones/project-milestones.component';
import { ProjectMoneyComponent } from '../../components/project-money/project-money.component';
import { ProjectNotesComponent } from '../../components/project-notes/project-notes.component';
import { ProjectOverviewComponent } from '../../components/project-overview/project-overview.component';
import { ProjectTagComponent } from '../../components/project-tag/project-tag.component';
import { ProjectTeamComponent } from '../../components/project-team/project-team.component';
import { ProjectTimeComponent } from '../../components/project-time/project-time.component';
import { ProjectWorkComponent } from '../../components/project-work/project-work.component';
import { TaskDrawerComponent } from '../../components/task-drawer/task-drawer.component';
import { injectProjectUsers, userOptions } from '../../projects-lookups';
import { mergeProjectInput } from '../../projects-mappers';
import { BILLING_TYPES, errorText, filterOptions, PRJ_STYLES, PROJECT_ACTIONS } from '../../projects-ui';

interface TabDef { key: string; label: string; permission?: string | string[]; }
const TABS: TabDef[] = [
  { key: 'overview', label: 'Overview' },
  { key: 'tasks', label: 'Tasks', permission: 'projects.tasks.read' },
  { key: 'milestones', label: 'Milestones', permission: 'projects.milestones.read' },
  { key: 'time', label: 'Time and expenses', permission: ['projects.timesheets.read', 'projects.expenses.read'] },
  { key: 'money', label: 'Billing and budget', permission: 'projects.billing.read' },
  { key: 'team', label: 'Team' },
  { key: 'risks', label: 'Risks and changes', permission: ['projects.risks.read', 'projects.changeorders.read'] },
  { key: 'notes', label: 'Discussion and files' },
];

/** One page for one project: how it is doing at the top, and everything about it in tabs. The tab is in the address, so it can be linked and survives a reload. */
@Component({
  selector: 'erp-project-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, DecimalPipe, FormsModule, RouterModule, NzButtonModule, NzDropDownModule, NzIconModule, NzInputModule, NzModalModule, RecordDrawerComponent, ProjectTagComponent, LogTimeComponent, TaskDrawerComponent,
    ProjectOverviewComponent, ProjectWorkComponent, ProjectMilestonesComponent, ProjectTimeComponent, ProjectMoneyComponent, ProjectTeamComponent, ProjectControlComponent, ProjectNotesComponent],
  templateUrl: './project-detail.page.html',
  styles: [PRJ_STYLES + `
    :host { display:block; }
    .head { display:flex; justify-content:space-between; align-items:flex-start; gap:16px; flex-wrap:wrap; margin-bottom:14px; }
    .back { font-size:12.5px; color:var(--erp-color-text-secondary,#64748b); display:inline-flex; gap:5px; align-items:center; text-decoration:none; } .back:hover { color:var(--erp-color-primary,#10955d); }
    .title-row { display:flex; align-items:center; gap:10px; flex-wrap:wrap; margin-top:4px; } h2 { font-size:21px; font-weight:700; margin:0; color:var(--erp-color-text-primary,#0f172a); } .code { font-family:ui-monospace,monospace; font-size:12px; color:var(--erp-color-text-secondary,#64748b); } .flag { font-size:11.5px; font-weight:600; border-radius:4px; padding:1px 7px; background:var(--erp-color-bg-subtle,#eef2f6); color:var(--erp-color-text-secondary,#475569); }
    .meta { display:flex; gap:16px; flex-wrap:wrap; margin-top:7px; font-size:13px; color:var(--erp-color-text-secondary,#64748b); } .meta span[nz-icon] { margin-inline-end:4px; }
    .actions { display:flex; gap:8px; flex-wrap:wrap; }
    .kpis { display:grid; grid-template-columns:repeat(auto-fit,minmax(170px,1fr)); gap:10px; margin-bottom:14px; }
    .kpi { background:var(--erp-color-bg-surface,#fff); border:1px solid var(--erp-color-border-subtle,#e2e8f0); border-radius:12px; padding:12px 14px; display:block; text-decoration:none; } .kpi.link:hover { border-color:var(--erp-color-primary,#10955d); }
    .k-label { font-size:12px; font-weight:600; color:var(--erp-color-text-secondary,#64748b); } .k-value { font-size:20px; font-weight:700; color:var(--erp-color-text-primary,#0f172a); margin:2px 0 6px; font-variant-numeric:tabular-nums; } .k-sub { font-size:11.5px; color:var(--erp-color-text-secondary,#64748b); margin-top:5px; }
    .tabs { display:flex; gap:2px; border-bottom:1px solid var(--erp-color-border-subtle,#e2e8f0); margin-bottom:14px; overflow-x:auto; scrollbar-width:thin; }
    .tabs a { padding:9px 13px; white-space:nowrap; border-bottom:2px solid transparent; color:var(--erp-color-text-secondary,#475569); text-decoration:none; font-size:13.5px; } .tabs a:hover { color:var(--erp-color-text-primary,#0f172a); } .tabs a.on { color:var(--erp-color-primary,#10955d); border-bottom-color:var(--erp-color-primary,#10955d); font-weight:600; }
    .form { display:flex; flex-direction:column; gap:10px; } .form label { display:flex; flex-direction:column; gap:4px; font-size:13px; } .form .check { flex-direction:row; align-items:center; gap:6px; }
    @media (max-width:720px) { h2 { font-size:18px; } .actions { width:100%; } }
  `],
})
export class ProjectDetailPage {
  readonly id = input.required<string>();
  /** From ?tab= in the address. */
  readonly tab = input<string | null>(null);

  private readonly projectsApi = inject(ProjectsApi);
  private readonly extra = inject(ProjectsExtraApi);
  private readonly parties = inject(PartiesApi);
  private readonly msg = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly session = inject(SessionStore);
  private readonly router = inject(Router);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly logTime = viewChild(LogTimeComponent);
  readonly taskDrawer = viewChild(TaskDrawerComponent);

  readonly project = signal<ProjectDto | null>(null);
  readonly failed = signal<string | null>(null);
  readonly cloneOpen = signal(false);
  private readonly users = injectProjectUsers();
  cloneName = ''; cloneStart = toIsoDate(new Date()); cloneTeam = false; cloneTemplate = false;

  readonly tabs = computed(() => TABS.filter((t) => !t.permission || (Array.isArray(t.permission) ? t.permission : [t.permission]).some((x) => this.session.hasPermission(x))));
  readonly activeTab = computed(() => { const t = this.tab(); return t && this.tabs().some((x) => x.key === t) ? t : 'overview'; });
  readonly closed = computed(() => { const p = this.project(); return !p || p.status === 'completed' || p.status === 'cancelled' || !!p.isArchived; });
  readonly nextStatuses = computed(() => { const p = this.project(); return p && !p.isTemplate ? (p.nextStatuses ?? []).filter((s) => s !== 'cancelled') : []; });
  readonly canCancel = computed(() => (this.project()?.nextStatuses ?? []).includes('cancelled'));
  readonly budgetUsed = computed(() => { const p = this.project(); return p && p.effectiveBudget > 0 ? (p.totalTimesheetCost / p.effectiveBudget) * 100 : 0; });
  readonly hoursUsed = computed(() => { const p = this.project(); return p && p.effectiveBudgetHours > 0 ? (p.totalLoggedHours / p.effectiveBudgetHours) * 100 : 0; });

  readonly state: MasterDataState<ProjectDto> = new MasterDataState<ProjectDto>({
    save: (id, body) => this.projectsApi.update(id!, mergeProjectInput(this.project()!, body as Partial<ProjectInput>)),
    reload: () => this.reload(), message: inject(NzMessageService), entityName: 'Project',
  });
  readonly fields = computed<FieldSpec[]>(() => [
    { key: 'name', label: 'Project name', type: 'text', required: true, maxLength: 200 },
    { key: 'number', label: 'Project code', type: 'text', maxLength: 30 },
    { key: 'customerId', label: 'Customer', type: 'lookup', lookup: (term) => this.parties.list({ page: 1, pageSize: 20, search: term, extra: { role: 'Customer' } }).pipe(map((r) => r.items.map((c) => ({ value: c.id, label: c.name })))), currentLabel: (r) => (r['customerName'] as string) ?? undefined },
    { key: 'managerUserId', label: 'Project manager', type: 'select', options: userOptions(this.users()), hint: this.project()?.managerName && !this.project()?.managerUserId ? `Typed name: ${this.project()!.managerName}. Choose a person to link an account.` : undefined },
    { key: 'billingType', label: 'Billing model', type: 'select', required: true, options: filterOptions(BILLING_TYPES) },
    { key: 'budgetAmount', label: `Budget or fixed fee (${this.currency()})`, type: 'number', min: 0 },
    { key: 'contractValue', label: `Contract value (${this.currency()})`, type: 'number', min: 0, hint: 'What the customer agreed to pay, when it differs from the cost budget. Left empty, the budget is the fee.' },
    { key: 'budgetHours', label: 'Budgeted hours', type: 'number', min: 0 },
    { key: 'startDate', label: 'Start date', type: 'date' },
    { key: 'endDate', label: 'Target end date', type: 'date' },
    { key: 'description', label: 'Description and scope', type: 'textarea', maxLength: 2000 },
  ]);

  constructor() { effect(() => { this.id(); this.reload(); }); }

  can(permission: string): boolean { return this.session.hasPermission(permission); }
  action(s: ProjectStatus): string { return PROJECT_ACTIONS[s]; }
  min100(n: number): number { return Math.max(0, Math.min(100, n)); }

  reload(): void {
    this.projectsApi.get(this.id()).subscribe({
      next: (p) => { this.project.set(p); this.failed.set(null); if (!this.cloneName) this.cloneName = `${p.name} (copy)`; },
      error: (e) => this.failed.set(errorText(e, 'This project could not be opened.')),
    });
  }

  edit(): void { const p = this.project(); if (p) this.state.edit(p); }

  changeStatus(status: ProjectStatus): void {
    const p = this.project()!;
    const go = () => this.projectsApi.setStatus(p.id, status).subscribe({ next: (r) => { this.project.set(r); this.msg.success(`${p.number} is now ${String(status).replace(/_/g, ' ')}`); }, error: (e) => this.msg.error(errorText(e, 'Could not change the status.')) });
    if (status === 'completed' || status === 'cancelled') this.modal.confirm({ nzTitle: `${PROJECT_ACTIONS[status]}: ${p.name}?`, nzContent: 'A completed or cancelled project takes no more work and cannot be reopened.', nzOkText: PROJECT_ACTIONS[status], nzOkDanger: status === 'cancelled', nzOnOk: go });
    else go();
  }

  postCosts(): void {
    const p = this.project()!;
    this.extra.postCosts(p.id).subscribe({
      next: (r) => this.msg.success(r.entries ? `Posted ${r.entries} entries (${this.currency()} ${r.amount.toFixed(2)}) to work in progress in journal ${r.journalNumber}` : 'Nothing new to post'),
      error: (e) => this.msg.error(errorText(e, 'Could not post the costs.')),
    });
  }

  toggleArchive(): void {
    const p = this.project()!;
    this.extra.archive(p.id, !p.isArchived).subscribe({ next: (r) => { this.project.set(r); this.msg.success(doneText(r.isArchived ? 'archived' : 'restored', 'Project', r.number)); }, error: (e) => this.msg.error(errorText(e, 'Could not change it.')) });
  }

  remove(): void {
    const p = this.project()!;
    this.modal.confirm({
      nzTitle: `Delete ${p.number}?`, nzContent: `${p.name} and its tasks, milestones and time will be removed. A project with invoiced work cannot be deleted.`, nzOkText: 'Delete', nzOkDanger: true,
      nzOnOk: () => this.projectsApi.delete(p.id).subscribe({ next: () => { this.msg.success(doneText('deleted', 'Project', p.name)); void this.router.navigate(['/projects/list']); }, error: (e) => this.msg.error(errorText(e, 'Could not delete the project.')) }),
    });
  }

  clone(p: ProjectDto): void {
    this.extra.clone(p.id, { name: this.cloneName.trim() || `${p.name} (copy)`, startDate: this.cloneStart || null, includeTeam: this.cloneTeam, asTemplate: this.cloneTemplate }).subscribe({
      next: (c) => { this.cloneOpen.set(false); this.msg.success(doneText('created', 'Project', c.number)); void this.router.navigate(['/projects', c.id]); },
      error: (e) => this.msg.error(errorText(e, 'Could not clone.')),
    });
  }
}
