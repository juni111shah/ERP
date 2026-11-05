import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, viewChild } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { catchError, map, of } from 'rxjs';
import { ProjectDto, ProjectInput, ProjectsApi, ProjectStatus } from '../../core/api/projects.api';
import { ProjectsExtraApi, UserOption } from '../../core/api/projects-extra.api';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { SessionStore } from '../../core/session/session.store';
import { MasterDataState } from '../../shared/master-data-state';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { PartiesApi } from '../parties.page';
import { LogTimeComponent } from './log-time.component';
import { ProjectControlComponent, ProjectMilestonesComponent, ProjectMoneyComponent, ProjectNotesComponent, ProjectOverviewComponent, ProjectTeamComponent, ProjectTimeComponent, ProjectWorkComponent } from './project-tabs.components';
import { BILLING_TYPES, errorText, filterOptions, PRJ_STYLES, PROJECT_ACTIONS, ProjectTagComponent } from './projects-ui';
import { TaskDrawerComponent } from './task-drawer.component';

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
  imports: [DecimalPipe, FormsModule, RouterModule, NzButtonModule, NzDropDownModule, NzIconModule, NzInputModule, NzModalModule, RecordDrawerComponent, ProjectTagComponent, LogTimeComponent, TaskDrawerComponent,
    ProjectOverviewComponent, ProjectWorkComponent, ProjectMilestonesComponent, ProjectTimeComponent, ProjectMoneyComponent, ProjectTeamComponent, ProjectControlComponent, ProjectNotesComponent],
  template: `
    @if (project(); as p) {
      <header class="head" data-testid="project-head">
        <div class="title-block">
          <a routerLink="/projects/list" class="back"><span nz-icon nzType="arrow-left"></span> All projects</a>
          <div class="title-row">
            <h2 data-testid="project-title">{{ p.name }}</h2>
            <span class="code">{{ p.number }}</span>
            <erp-prj-tag kind="project" [value]="p.status" />
            <span data-testid="plan-health"><erp-prj-tag kind="health" [value]="p.health" /></span>
            @if (p.isTemplate) { <span class="flag">Template</span> } @if (p.isArchived) { <span class="flag">Archived</span> }
          </div>
          <div class="meta">
            <span><span nz-icon nzType="user"></span> {{ p.customerName || 'Internal project' }}</span>
            @if (p.managerName) { <span><span nz-icon nzType="team"></span> {{ p.managerName }}</span> }
            <span><span nz-icon nzType="calendar"></span> {{ p.startDate || 'No start date' }} to {{ p.endDate || 'no end date' }}</span>
            <span><erp-prj-tag kind="billing" [value]="p.billingType" /></span>
          </div>
        </div>
        <div class="actions">
          @if (!closed()) {
            @if (can('projects.timesheets.manage') && !p.isTemplate) { <button nz-button nzType="primary" (click)="logTime()?.open(p.id)" data-testid="log-time"><span nz-icon nzType="clock-circle"></span> Log time</button> }
            @if (can('projects.tasks.manage')) { <button nz-button (click)="taskDrawer()?.create({ projectId: p.id })" data-testid="add-task"><span nz-icon nzType="plus"></span> Add task</button> }
          }
          @if (can('projects.projects.manage')) {
            @for (s of nextStatuses(); track s) { <button nz-button (click)="changeStatus(s)" [attr.data-testid]="'status-' + s">{{ action(s) }}</button> }
            <button nz-button nz-dropdown [nzDropdownMenu]="more" nzTrigger="click" nzPlacement="bottomRight" aria-label="More actions" data-testid="project-more"><span nz-icon nzType="more"></span></button>
            <nz-dropdown-menu #more="nzDropdownMenu">
              <ul nz-menu>
                <li nz-menu-item (click)="edit()" data-testid="project-edit">Edit project</li>
                <li nz-menu-item (click)="cloneOpen.set(true)" data-testid="plan-clone">{{ p.isTemplate ? 'Create a project from this' : 'Clone' }}</li>
                @if (can('projects.billing.manage') && !p.isTemplate) { <li nz-menu-item (click)="postCosts()" data-testid="plan-post-costs" title="Move the cost of approved time and expenses into work in progress">Post costs to ledger</li> }
                <li nz-menu-item (click)="toggleArchive()" data-testid="plan-archive">{{ p.isArchived ? 'Restore' : 'Archive' }}</li>
                @if (canCancel()) { <li nz-menu-item nzDanger (click)="changeStatus('cancelled')" data-testid="status-cancelled">Cancel project</li> }
                <li nz-menu-item nzDanger (click)="remove()" data-testid="project-delete">Delete project</li>
              </ul>
            </nz-dropdown-menu>
          }
        </div>
      </header>

      <div class="kpis" data-testid="project-kpis">
        <div class="kpi">
          <div class="k-label">Progress</div><div class="k-value">{{ p.percentComplete | number: '1.0-0' }}%</div>
          <div class="prj-bar"><i [style.width.%]="p.percentComplete"></i></div>
          <div class="k-sub">{{ p.completedTasks }} of {{ p.totalTasks }} tasks done @if (p.overdueTasks) { · <span class="prj-danger">{{ p.overdueTasks }} overdue</span> }</div>
        </div>
        <div class="kpi">
          <div class="k-label">Budget used</div><div class="k-value" [class.prj-danger]="budgetUsed() > 100">{{ budgetUsed() | number: '1.0-0' }}%</div>
          <div class="prj-bar" [class.warn]="budgetUsed() > 85" [class.over]="budgetUsed() > 100"><i [style.width.%]="min100(budgetUsed())"></i></div>
          <div class="k-sub">{{ currency() }} {{ p.totalTimesheetCost | number: '1.0-0' }} of {{ p.effectiveBudget | number: '1.0-0' }}</div>
        </div>
        <div class="kpi">
          <div class="k-label">Hours</div><div class="k-value">{{ p.totalLoggedHours | number: '1.0-1' }}</div>
          <div class="prj-bar" [class.over]="hoursUsed() > 100"><i [style.width.%]="min100(hoursUsed())"></i></div>
          <div class="k-sub">@if (p.effectiveBudgetHours) { of {{ p.effectiveBudgetHours | number: '1.0-0' }} budgeted } @else { no hours budgeted }</div>
        </div>
        <div class="kpi">
          <div class="k-label">Milestones</div><div class="k-value">{{ p.achievedMilestones }} / {{ p.totalMilestones }}</div>
          <div class="prj-bar"><i [style.width.%]="p.totalMilestones ? (p.achievedMilestones / p.totalMilestones) * 100 : 0"></i></div>
          <div class="k-sub">reached</div>
        </div>
        <a class="kpi link" [routerLink]="[]" [queryParams]="{ tab: 'money' }" data-testid="kpi-unbilled">
          <div class="k-label">Ready to bill</div><div class="k-value">{{ currency() }} {{ p.unbilledAmount | number: '1.0-0' }}</div>
          <div class="k-sub">{{ currency() }} {{ p.billedAmount | number: '1.0-0' }} billed so far</div>
        </a>
      </div>

      <nav class="tabs" role="tablist" aria-label="Project sections" data-testid="project-tabs">
        @for (t of tabs(); track t.key) {
          <a role="tab" [routerLink]="[]" [queryParams]="{ tab: t.key }" [class.on]="activeTab() === t.key" [attr.aria-selected]="activeTab() === t.key" [attr.data-testid]="'tab-' + t.key">{{ t.label }}</a>
        }
      </nav>

      <div role="tabpanel">
        @switch (activeTab()) {
          @case ('overview') { <erp-project-overview [project]="p" (changed)="reload()" /> }
          @case ('tasks') { <erp-project-work [project]="p" (changed)="reload()" /> }
          @case ('milestones') { <erp-project-milestones [project]="p" (changed)="reload()" /> }
          @case ('time') { <erp-project-time [projectId]="p.id" /> }
          @case ('money') { <erp-project-money [project]="p" (changed)="reload()" /> }
          @case ('team') { <erp-project-team [projectId]="p.id" /> }
          @case ('risks') { <erp-project-control [projectId]="p.id" /> }
          @case ('notes') { <erp-project-notes [projectId]="p.id" /> }
        }
      </div>

      <erp-log-time (saved)="reload()" />
      <erp-task-drawer (changed)="reload()" />
      <erp-record-drawer title="Edit project" [open]="state.open()" [fields]="fields()" [value]="$any(state.editing())" customFieldEntityType="project" [saving]="state.saving()" [errors]="state.errors()" testId="project-form" (submitted)="state.submit($event)" (closed)="state.close()" />

      <nz-modal [nzVisible]="cloneOpen()" [nzTitle]="p.isTemplate ? 'New project from this template' : 'Clone project'" nzOkText="Create" (nzOnOk)="clone(p)" (nzOnCancel)="cloneOpen.set(false)">
        <ng-container *nzModalContent>
          <div class="form">
            <label>Name<input nz-input [(ngModel)]="cloneName" maxlength="200" data-testid="clone-name" /></label>
            <label>Start date<input nz-input type="date" [(ngModel)]="cloneStart" /></label>
            <label class="check"><input type="checkbox" [(ngModel)]="cloneTeam" /> Keep the team and task assignees</label>
            <label class="check"><input type="checkbox" [(ngModel)]="cloneTemplate" /> Save the copy as a template</label>
            <p class="prj-muted">Tasks, milestones, dependencies and project rate cards are copied, and dates move with the start date. Time, expenses and billing are not copied.</p>
          </div>
        </ng-container>
      </nz-modal>
    } @else if (failed()) {
      <div class="prj-card prj-empty" role="alert" data-testid="project-load-error">{{ failed() }} <a routerLink="/projects/list" class="prj-link">Back to projects</a></div>
    } @else {
      <div class="prj-empty">Loading the project</div>
    }
  `,
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
  private readonly users = toSignal(this.extra.users().pipe(catchError(() => of([] as UserOption[]))), { initialValue: [] as UserOption[] });
  cloneName = ''; cloneStart = toIsoDate(new Date()); cloneTeam = false; cloneTemplate = false;

  readonly tabs = computed(() => TABS.filter((t) => !t.permission || (Array.isArray(t.permission) ? t.permission : [t.permission]).some((x) => this.session.hasPermission(x))));
  readonly activeTab = computed(() => { const t = this.tab(); return t && this.tabs().some((x) => x.key === t) ? t : 'overview'; });
  readonly closed = computed(() => { const p = this.project(); return !p || p.status === 'completed' || p.status === 'cancelled' || !!p.isArchived; });
  readonly nextStatuses = computed(() => { const p = this.project(); return p && !p.isTemplate ? (p.nextStatuses ?? []).filter((s) => s !== 'cancelled') : []; });
  readonly canCancel = computed(() => (this.project()?.nextStatuses ?? []).includes('cancelled'));
  readonly budgetUsed = computed(() => { const p = this.project(); return p && p.effectiveBudget > 0 ? (p.totalTimesheetCost / p.effectiveBudget) * 100 : 0; });
  readonly hoursUsed = computed(() => { const p = this.project(); return p && p.effectiveBudgetHours > 0 ? (p.totalLoggedHours / p.effectiveBudgetHours) * 100 : 0; });

  readonly state: MasterDataState<ProjectDto> = new MasterDataState<ProjectDto>({
    save: (id, body) => {
      const p = this.project()!;
      const merged = { legalEntityId: p.legalEntityId, status: p.status, taxCodeId: p.taxCodeId ?? null, sourceType: p.sourceType ?? null, sourceId: p.sourceId ?? null, isTemplate: p.isTemplate, customFields: p.customFields ?? null, ...(body as Partial<ProjectInput>) } as ProjectInput;
      if (!merged.managerUserId) merged.managerName = p.managerUserId ? null : p.managerName ?? null;
      return this.projectsApi.update(id!, merged);
    },
    reload: () => this.reload(), message: inject(NzMessageService), entityName: 'Project',
  });
  readonly fields = computed<FieldSpec[]>(() => [
    { key: 'name', label: 'Project name', type: 'text', required: true, maxLength: 200 },
    { key: 'number', label: 'Project code', type: 'text', maxLength: 30 },
    { key: 'customerId', label: 'Customer', type: 'lookup', lookup: (term) => this.parties.list({ page: 1, pageSize: 20, search: term, extra: { role: 'Customer' } }).pipe(map((r) => r.items.map((c) => ({ value: c.id, label: c.name })))), currentLabel: (r) => (r['customerName'] as string) ?? undefined },
    { key: 'managerUserId', label: 'Project manager', type: 'select', options: this.users().map((u) => ({ value: u.id, label: u.fullName })), hint: this.project()?.managerName && !this.project()?.managerUserId ? `Typed name: ${this.project()!.managerName}. Choose a person to link an account.` : undefined },
    { key: 'billingType', label: 'Billing model', type: 'select', required: true, options: filterOptions(BILLING_TYPES) },
    { key: 'budgetAmount', label: `Budget or fixed fee (${this.currency()})`, type: 'number', min: 0 },
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
    const go = () => this.projectsApi.setStatus(p.id, status).subscribe({ next: (r) => { this.project.set(r); this.msg.success('Status changed'); }, error: (e) => this.msg.error(errorText(e, 'Could not change the status.')) });
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
    this.extra.archive(p.id, !p.isArchived).subscribe({ next: (r) => { this.project.set(r); this.msg.success(r.isArchived ? 'Archived' : 'Restored'); }, error: (e) => this.msg.error(errorText(e, 'Could not change it.')) });
  }

  remove(): void {
    const p = this.project()!;
    this.modal.confirm({
      nzTitle: `Delete ${p.number}?`, nzContent: `${p.name} and its tasks, milestones and time will be removed. A project with invoiced work cannot be deleted.`, nzOkText: 'Delete', nzOkDanger: true,
      nzOnOk: () => this.projectsApi.delete(p.id).subscribe({ next: () => { this.msg.success('Project deleted'); void this.router.navigate(['/projects/list']); }, error: (e) => this.msg.error(errorText(e, 'Could not delete the project.')) }),
    });
  }

  clone(p: ProjectDto): void {
    this.extra.clone(p.id, { name: this.cloneName.trim() || `${p.name} (copy)`, startDate: this.cloneStart || null, includeTeam: this.cloneTeam, asTemplate: this.cloneTemplate }).subscribe({
      next: (c) => { this.cloneOpen.set(false); this.msg.success(`Created ${c.number}`); void this.router.navigate(['/projects', c.id]); },
      error: (e) => this.msg.error(errorText(e, 'Could not clone.')),
    });
  }
}
