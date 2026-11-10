import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { catchError, of } from 'rxjs';
import { MilestoneDto, ProjectDto, ProjectMilestonesApi, ProjectTasksApi, TaskDto } from '@core/api/projects.api';
import { AcceptanceDto, ActivityDto, AllocationDto, BudgetLineDto, CommentDto, EXPENSE_CATEGORIES, MemberDto, ProjectInvoice, ProjectsExtraApi, RiskDto, UserOption } from '@core/api/projects-extra.api';
import { toIsoDate } from '@core/custom-fields/custom-field-form';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { SessionStore } from '@core/session/session.store';
import { AttachmentsComponent } from '@shared/components/attachments/attachments.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { MasterDataState } from '@shared/utils/master-data-state';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { BillModalComponent } from './components/bill-modal/bill-modal.component';
import { ProjectBoardPage } from './pages/project-board/project-board.page';
import { ProjectChangeOrdersPage } from './pages/project-change-orders/project-change-orders.page';
import { ProjectExpensesPage } from './pages/project-expenses/project-expenses.page';
import { ProjectGanttComponent } from './components/project-gantt/project-gantt.component';
import { ProjectRisksPage } from './pages/project-risks/project-risks.page';
import { ProjectTasksPage } from './pages/project-tasks/project-tasks.page';
import { ProjectTimesheetsPage } from './pages/project-timesheets/project-timesheets.page';
import { errorText, PRJ_STYLES, ProjectTagComponent } from './projects-ui';
import { TaskDrawerComponent } from './components/task-drawer/task-drawer.component';
import { ErpLabelPipe, ErpDatePipe } from '@shared/pipes/display.pipes';
import { RowActionsComponent } from '@shared/components/row-actions/row-actions.component';

const today = (): string => toIsoDate(new Date());
const SUBTABS = `
  .seg { display:inline-flex; border:1px solid var(--erp-color-border-subtle,#e2e8f0); border-radius:8px; overflow:hidden; margin-bottom:12px; }
  .seg button { background:var(--erp-color-bg-surface,#fff); border:0; padding:6px 14px; cursor:pointer; color:var(--erp-color-text-secondary,#475569); font-size:13px; } .seg button.on { background:var(--erp-color-primary,#10955d); color:#fff; }
  .plain { width:100%; border-collapse:collapse; font-size:13px; } .plain th { text-align:start; font-weight:500; color:var(--erp-color-text-secondary,#64748b); padding:6px 8px; border-bottom:1px solid var(--erp-color-border-subtle,#e2e8f0); } .plain td { padding:7px 8px; border-bottom:1px solid var(--erp-color-border-subtle,#f1f5f9); }
`;

/** What needs attention, what is coming up and who is involved. */
@Component({
  selector: 'erp-project-overview',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, DecimalPipe, DatePipe, RouterModule, NzIconModule, ProjectTagComponent, TaskDrawerComponent],
  template: `
    <div class="columns">
      <div class="col-main">
        @if (attention().length) {
          <section class="prj-card attention" data-testid="overview-attention">
            <h3><span nz-icon nzType="warning"></span> Needs attention</h3>
            <ul>@for (a of attention(); track a.text) { <li><a [routerLink]="[]" [queryParams]="{ tab: a.tab }" queryParamsHandling="merge">{{ a.text }}</a></li> }</ul>
          </section>
        }
        <section class="prj-card">
          <h3>Next up</h3>
          @for (t of nextTasks(); track t.id) {
            <div class="line" role="button" tabindex="0" (click)="drawer()?.open(t.id)" (keydown.enter)="drawer()?.open(t.id)">
              <span class="grow"><span class="prj-muted">{{ t.number }}</span> {{ t.title }}</span>
              <span class="prj-muted">{{ t.assignedTo }}</span><span [class.prj-danger]="late(t)" class="when">{{ t.dueDate | erpDate }}</span><erp-prj-tag kind="task" [value]="t.status" />
            </div>
          } @empty { <p class="prj-empty">No open tasks with a due date.</p> }
        </section>
        <section class="prj-card">
          <h3>Scope</h3>
          <p class="scope">{{ project().description || 'No description yet. Edit the project to add its scope.' }}</p>
          <dl class="facts">
            <div><dt>Customer</dt><dd>{{ project().customerName || 'Internal' }}</dd></div>
            <div><dt>Manager</dt><dd>{{ project().managerName || 'Not set' }}</dd></div>
            <div><dt>Billing</dt><dd><erp-prj-tag kind="billing" [value]="project().billingType" /></dd></div>
            <div><dt>Start</dt><dd>{{ (project().startDate | erpDate) || 'Not set' }}</dd></div>
            <div><dt>Target end</dt><dd>{{ (project().endDate | erpDate) || 'Not set' }}</dd></div>
            <div><dt>Budget</dt><dd>{{ currency() }} {{ project().budgetAmount | number: '1.0-0' }}@if (project().approvedChanges) { <span class="prj-muted"> + {{ project().approvedChanges | number: '1.0-0' }} from change orders</span> }</dd></div>
            @if (project().contractValue !== null && project().contractValue !== undefined) { <div><dt>Contract value</dt><dd data-testid="project-contract-value">{{ currency() }} {{ project().contractValue | number: '1.0-0' }}</dd></div> }
            @if (project().wipBalance !== undefined) { <div><dt>Work in progress</dt><dd data-testid="project-wip">{{ currency() }} {{ project().wipBalance | number: '1.2-2' }}<span class="prj-muted"> cost not yet released</span></dd></div> }
            @if (project().budgetHours) { <div><dt>Budgeted hours</dt><dd>{{ project().effectiveBudgetHours | number: '1.0-0' }}</dd></div> }
            @if (sourceLabel()) { <div><dt>Started from</dt><dd>{{ sourceLabel() }}</dd></div> }
          </dl>
        </section>
      </div>
      <div class="col-side">
        <section class="prj-card">
          <h3>Milestones</h3>
          @for (m of milestones(); track m.id) {
            <div class="line"><span class="grow">{{ m.title }}</span><span class="prj-muted when" [class.prj-danger]="m.status === 'pending' && m.dueDate < todayIso">{{ m.dueDate | erpDate }}</span><erp-prj-tag kind="milestone" [value]="m.status" /></div>
          } @empty { <p class="prj-empty">No milestones yet.</p> }
        </section>
        <section class="prj-card">
          <h3>Open risks and issues</h3>
          @for (r of risks(); track r.id) { <div class="line"><span class="grow">{{ r.title }}</span><erp-prj-tag kind="severity" [value]="r.severity" /></div> } @empty { <p class="prj-empty">Nothing open.</p> }
        </section>
        <section class="prj-card">
          <h3>Team</h3>
          <div class="team">@for (m of members(); track m.id) { <span class="person"><span class="who" [title]="m.name">{{ initials(m.name) }}</span> {{ m.name }}@if (m.role) { <span class="prj-muted"> · {{ m.role }}</span> }</span> } @empty { <p class="prj-empty">No team yet.</p> }</div>
        </section>
        <section class="prj-card">
          <h3>Latest discussion</h3>
          @for (c of comments(); track c.id) { <div class="comment"><b>{{ c.authorName }}</b> <span class="prj-muted">{{ c.createdAt | date: 'mediumDate' }}</span><p>{{ c.text }}</p></div> } @empty { <p class="prj-empty">No comments yet.</p> }
        </section>
      </div>
    </div>
    <erp-task-drawer (changed)="load(); changed.emit()" />
  `,
  styles: [PRJ_STYLES + `
    .columns { display:grid; grid-template-columns:minmax(0,3fr) minmax(0,2fr); gap:12px; align-items:start; } .col-main, .col-side { display:flex; flex-direction:column; gap:12px; min-width:0; } @media (max-width:1000px) { .columns { grid-template-columns:minmax(0,1fr); } }
    .attention { border-color:#f59e0b; background:var(--erp-color-bg-surface,#fffbeb); } .attention ul { margin:0; padding-inline-start:18px; } .attention a { color:var(--erp-color-text-primary,#92400e); }
    .line { display:flex; align-items:center; gap:10px; padding:6px 0; border-bottom:1px solid var(--erp-color-border-subtle,#f1f5f9); font-size:13px; } .line[role=button] { cursor:pointer; } .line[role=button]:hover { background:var(--erp-color-bg-subtle,#f8fafc); } .grow { flex:1; min-width:0; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; } .when { white-space:nowrap; font-size:12px; }
    .scope { white-space:pre-wrap; color:var(--erp-color-text-primary,#334155); font-size:13.5px; } .facts { display:grid; grid-template-columns:repeat(auto-fill,minmax(180px,1fr)); gap:10px; margin:0; } dt { font-size:12px; color:var(--erp-color-text-secondary,#64748b); } dd { margin:0; font-size:13.5px; font-weight:500; color:var(--erp-color-text-primary,#0f172a); }
    .team { display:flex; flex-direction:column; gap:6px; font-size:13px; } .who { display:inline-flex; width:24px; height:24px; border-radius:50%; align-items:center; justify-content:center; background:var(--erp-color-primary,#10955d); color:#fff; font-size:10.5px; font-weight:600; margin-inline-end:6px; }
    .comment { padding:6px 0; border-bottom:1px solid var(--erp-color-border-subtle,#f1f5f9); font-size:13px; } .comment p { margin:2px 0 0; white-space:pre-wrap; }
  `],
})
export class ProjectOverviewComponent {
  readonly project = input.required<ProjectDto>();
  readonly changed = output<void>();
  private readonly tasksApi = inject(ProjectTasksApi);
  private readonly milestonesApi = inject(ProjectMilestonesApi);
  private readonly extra = inject(ProjectsExtraApi);
  private readonly session = inject(SessionStore);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly drawer = viewChild(TaskDrawerComponent);
  readonly todayIso = today();
  readonly tasks = signal<TaskDto[]>([]);
  readonly milestones = signal<MilestoneDto[]>([]);
  readonly risks = signal<RiskDto[]>([]);
  readonly members = signal<MemberDto[]>([]);
  readonly comments = signal<CommentDto[]>([]);
  readonly acceptances = signal<AcceptanceDto[]>([]);

  readonly nextTasks = computed(() => this.tasks().filter((t) => t.dueDate && !['done', 'cancelled'].includes(t.status)).sort((a, b) => a.dueDate!.localeCompare(b.dueDate!)).slice(0, 6));
  readonly sourceLabel = computed(() => { const s = this.project().sourceType; return !s ? '' : s.startsWith('sales') ? 'A sales order' : s.startsWith('crm') ? 'An opportunity' : s; });
  readonly attention = computed(() => {
    const p = this.project(); const out: { text: string; tab: string }[] = [];
    if (p.overdueTasks) out.push({ text: `${p.overdueTasks} overdue ${p.overdueTasks === 1 ? 'task' : 'tasks'}`, tab: 'tasks' });
    if (p.effectiveBudget > 0 && p.totalTimesheetCost > p.effectiveBudget) out.push({ text: `Over budget by ${this.currency()} ${(p.totalTimesheetCost - p.effectiveBudget).toLocaleString('en-US', { maximumFractionDigits: 0 })}`, tab: 'money' });
    if (p.endDate && p.endDate < this.todayIso && p.openTasks) out.push({ text: `Past the target end date with ${p.openTasks} open ${p.openTasks === 1 ? 'task' : 'tasks'}`, tab: 'tasks' });
    if (p.unbilledAmount > 0) out.push({ text: `${this.currency()} ${p.unbilledAmount.toLocaleString('en-US', { maximumFractionDigits: 0 })} ready to bill`, tab: 'money' });
    const late = this.milestones().filter((m) => m.status === 'pending' && m.dueDate < this.todayIso).length;
    if (late) out.push({ text: `${late} ${late === 1 ? 'milestone is' : 'milestones are'} past due`, tab: 'milestones' });
    const declined = this.acceptances().filter((a) => a.status === 'declined').length;
    if (declined) out.push({ text: `The customer declined ${declined} sign-off${declined === 1 ? '' : 's'}`, tab: 'milestones' });
    return out;
  });

  constructor() { effect(() => { this.project().id; this.load(); }); }

  private can(p: string): boolean { return this.session.hasPermission(p); }
  load(): void {
    const id = this.project().id;
    const filters = [{ field: 'projectId', op: 'eq' as const, value: id }];
    if (this.can('projects.tasks.read')) this.tasksApi.list({ page: 1, pageSize: 200, sort: 'dueDate', filters }).pipe(catchError(() => of({ items: [] as TaskDto[] }))).subscribe((r) => this.tasks.set(r.items));
    if (this.can('projects.milestones.read')) {
      this.milestonesApi.list({ page: 1, pageSize: 50, sort: 'dueDate', filters }).pipe(catchError(() => of({ items: [] as MilestoneDto[] }))).subscribe((r) => this.milestones.set(r.items.filter((m) => m.status !== 'cancelled')));
      this.extra.acceptances(id).pipe(catchError(() => of([] as AcceptanceDto[]))).subscribe((r) => this.acceptances.set(r));
    }
    if (this.can('projects.risks.read')) this.extra.risks(id).pipe(catchError(() => of([] as RiskDto[]))).subscribe((r) => this.risks.set(r.filter((x) => x.status === 'open').slice(0, 5)));
    this.extra.members(id).pipe(catchError(() => of([] as MemberDto[]))).subscribe((m) => this.members.set(m));
    this.extra.comments('project', id).pipe(catchError(() => of([] as CommentDto[]))).subscribe((c) => this.comments.set(c.slice(-3).reverse()));
  }
  late(t: TaskDto): boolean { return !!t.dueDate && t.dueDate < this.todayIso; }
  initials(name: string): string { return name.split(/\s+/).map((n) => n[0]).slice(0, 2).join('').toUpperCase(); }
}

/** The tasks of a project as a list, a board or a timeline. */
@Component({
  selector: 'erp-project-work',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ProjectTasksPage, ProjectBoardPage, ProjectGanttComponent, TaskDrawerComponent],
  template: `
    <div class="seg" role="group" aria-label="View">
      @for (v of views; track v.key) { <button type="button" [class.on]="view() === v.key" (click)="setView(v.key)" [attr.data-testid]="'work-' + v.key">{{ v.label }}</button> }
    </div>
    @switch (view()) {
      @case ('list') { <erp-project-tasks [projectId]="project().id" /> }
      @case ('board') { <erp-project-board [projectId]="project().id" /> }
      @case ('timeline') { <erp-project-gantt [projectId]="project().id" (opened)="drawer()?.open($event)" (changed)="changed.emit()" /> }
    }
    <erp-task-drawer (changed)="reloadGantt(); changed.emit()" />
  `,
  styles: [SUBTABS],
})
export class ProjectWorkComponent {
  readonly project = input.required<ProjectDto>();
  readonly changed = output<void>();
  readonly drawer = viewChild(TaskDrawerComponent);
  readonly gantt = viewChild(ProjectGanttComponent);
  readonly views = [{ key: 'list', label: 'List' }, { key: 'board', label: 'Board' }, { key: 'timeline', label: 'Timeline' }];
  readonly view = signal(this.read());
  private read(): string { try { return localStorage.getItem('erp.projects.workview') ?? 'list'; } catch { return 'list'; } }
  setView(v: string): void { this.view.set(v); try { localStorage.setItem('erp.projects.workview', v); } catch { /* storage may be blocked */ } }
  reloadGantt(): void { this.gantt()?.load(); }
}

/** Milestones with everything that can happen to one, and the customer's sign-offs. */
@Component({
  selector: 'erp-project-milestones',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, DatePipe, DecimalPipe, NzButtonModule, NzIconModule, RecordDrawerComponent, HasPermissionDirective, ProjectTagComponent],
  template: `
    <div class="prj-toolbar">
      <span class="prj-muted" data-testid="milestones-summary">{{ summary() }}</span>
      <span class="prj-spacer"></span>
      <button *erpHasPermission="'projects.milestones.manage'" nz-button nzType="primary" (click)="state.create()" data-testid="milestone-new"><span nz-icon nzType="plus"></span> Add milestone</button>
    </div>
    <div class="prj-card prj-table-wrap">
      <table class="plain" data-testid="milestones-table">
        <thead><tr><th>Milestone</th><th>Due</th><th class="prj-num">Amount</th><th>Status</th><th>Reached</th><th>Sign-off</th><th></th></tr></thead>
        <tbody>
          @for (m of rows(); track m.id) {
            <tr>
              <td class="prj-strong">{{ m.title }}</td><td [class.prj-danger]="m.status === 'pending' && m.dueDate < todayIso">{{ m.dueDate | erpDate }}</td><td class="prj-num">{{ currency() }} {{ m.amount | number: '1.2-2' }}</td>
              <td><erp-prj-tag kind="milestone" [value]="m.status" /></td><td>{{ (m.achievedDate | erpDate) || '' }}</td>
              <td>@if (signOf(m.id); as a) { <erp-prj-tag kind="acceptance" [value]="a.status" /> }</td>
              <td class="prj-num acts">
                <ng-container *erpHasPermission="'projects.milestones.manage'">
                  @if (m.status === 'pending') { <button nz-button nzSize="small" nzType="primary" (click)="achieve(m)" [attr.data-testid]="'milestone-achieve-' + $index"><span nz-icon nzType="check"></span> Mark achieved</button> }
                  @if (m.status === 'achieved') { <button nz-button nzSize="small" (click)="setStatus(m, 'pending')">Back to pending</button> }
                  @if (m.status === 'pending' && hasCustomer() && !waiting(m.id)) { <button nz-button nzSize="small" (click)="askSignOff(m)" [attr.data-testid]="'milestone-signoff-' + $index">Ask to sign off</button> }
                  @if (m.status === 'pending' || m.status === 'achieved') { <button nz-button nzSize="small" (click)="state.edit(m)" [attr.data-testid]="'milestone-edit-' + $index">Edit</button> }
                  @if (m.status === 'pending' || m.status === 'achieved') { <button nz-button nzSize="small" (click)="setStatus(m, 'cancelled')">Cancel</button> }
                  @if (m.status === 'cancelled') { <button nz-button nzSize="small" (click)="setStatus(m, 'pending')">Restore</button> }
                  @if (m.status !== 'invoiced') { <button nz-button nzSize="small" nzDanger nzType="text" (click)="remove(m)" aria-label="Delete milestone"><span nz-icon nzType="delete"></span></button> }
                </ng-container>
              </td>
            </tr>
          } @empty { <tr><td colspan="7" class="prj-empty">No milestones yet. Add the points where work is delivered or billed.</td></tr> }
        </tbody>
      </table>
    </div>

    <div class="prj-card signoffs" data-testid="signoffs">
      <div class="prj-toolbar">
        <h3 style="margin:0">Customer sign-offs</h3><span class="prj-spacer"></span>
        @if (hasCustomer()) { <button *erpHasPermission="'projects.milestones.manage'" nz-button nzSize="small" (click)="askProjectSignOff()" data-testid="plan-signoff"><span nz-icon nzType="edit"></span> Ask to sign off the whole project</button> }
      </div>
      @if (!hasCustomer()) { <p class="prj-muted">Add a customer to the project to ask them to sign off in their portal.</p> }
      <div class="prj-table-wrap">
        <table class="plain">
          <thead><tr><th>What</th><th>Asked</th><th>Status</th><th>Answered by</th><th>When</th><th>Evidence</th></tr></thead>
          <tbody>
            @for (a of acceptances(); track a.id) {
              <tr><td>{{ a.title }}@if (a.comment) { <div class="prj-muted">{{ a.comment }}</div> }</td><td>{{ a.requestedAt | date: 'mediumDate' }}</td><td><erp-prj-tag kind="acceptance" [value]="a.status" /></td><td>{{ a.signedByName }}</td><td>{{ a.signedAt | date: 'medium' }}</td>
                <td class="prj-muted evidence" [title]="a.evidenceHash || ''">@if (a.evidenceHash) { {{ a.drawnSignature ? 'Drawn signature' : 'Typed name' }}, {{ a.signedIp }}, {{ a.evidenceHash.slice(0, 10) }}… }</td></tr>
            } @empty { <tr><td colspan="6" class="prj-empty">No sign-offs asked for yet.</td></tr> }
          </tbody>
        </table>
      </div>
    </div>
    <erp-record-drawer [title]="state.editing() ? 'Edit milestone' : 'Add milestone'" [open]="state.open()" [fields]="fields()" [value]="$any(state.editing())" [saving]="state.saving()" [errors]="state.errors()" testId="milestone-form" (submitted)="state.submit($event)" (closed)="state.close()" />
  `,
  styles: [PRJ_STYLES + SUBTABS + '.signoffs { margin-top:12px; } .acts { white-space:nowrap; } .acts button { margin-inline-start:4px; } .evidence { max-width:260px; font-size:11.5px; }'],
})
export class ProjectMilestonesComponent {
  readonly project = input.required<ProjectDto>();
  readonly changed = output<void>();
  private readonly api = inject(ProjectMilestonesApi);
  private readonly extra = inject(ProjectsExtraApi);
  private readonly msg = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly todayIso = today();
  readonly rows = signal<MilestoneDto[]>([]);
  readonly acceptances = signal<AcceptanceDto[]>([]);
  readonly hasCustomer = computed(() => !!this.project().customerId);
  readonly summary = computed(() => {
    const live = this.rows().filter((m) => m.status !== 'cancelled'); const total = live.reduce((a, m) => a + m.amount, 0);
    const invoiced = live.filter((m) => m.status === 'invoiced').reduce((a, m) => a + m.amount, 0);
    const fee = this.project().billingType === 'fixedFee' ? ` of the ${this.currency()} ${this.project().effectiveBudget.toLocaleString('en-US')} fee` : '';
    return `${this.currency()} ${total.toLocaleString('en-US')}${fee} in milestones · ${this.currency()} ${invoiced.toLocaleString('en-US')} invoiced`;
  });

  readonly state: MasterDataState<MilestoneDto> = new MasterDataState<MilestoneDto>({
    save: (id, body) => {
      const b: Record<string, unknown> = { ...(body as Record<string, unknown>), projectId: this.project().id, status: this.state.editing()?.status ?? 'pending', achievedDate: this.state.editing()?.achievedDate ?? null };
      if (b['dueDate'] instanceof Date) b['dueDate'] = toIsoDate(b['dueDate'] as Date);
      return id ? this.api.update(id, b as never) : this.api.create(b as never);
    },
    reload: () => { this.load(); this.changed.emit(); }, message: inject(NzMessageService), entityName: 'Milestone',
  });
  readonly fields = computed<FieldSpec[]>(() => [
    { key: 'title', label: 'Milestone', type: 'text', required: true, maxLength: 200 },
    { key: 'dueDate', label: 'Target date', type: 'date', required: true },
    { key: 'amount', label: `Amount to bill (${this.currency()})`, type: 'number', required: true, min: 0, default: 0, precision: 2 },
  ]);

  constructor() { effect(() => { this.project().id; this.load(); }); }

  load(): void {
    const id = this.project().id;
    this.api.list({ page: 1, pageSize: 100, sort: 'dueDate', filters: [{ field: 'projectId', op: 'eq', value: id }] }).subscribe({ next: (r) => this.rows.set(r.items), error: (e) => this.msg.error(errorText(e, 'Could not load the milestones.')) });
    this.extra.acceptances(id).pipe(catchError(() => of([] as AcceptanceDto[]))).subscribe((r) => this.acceptances.set(r));
  }
  signOf(milestoneId: string): AcceptanceDto | undefined { return this.acceptances().find((a) => a.milestoneId === milestoneId); }
  waiting(milestoneId: string): boolean { return this.acceptances().some((a) => a.milestoneId === milestoneId && a.status === 'pending'); }
  achieve(m: MilestoneDto): void { this.api.achieve(m.id).subscribe({ next: () => { this.msg.success(`Milestone ${m.title} achieved. It is now ready to bill.`); this.load(); this.changed.emit(); }, error: (e) => this.msg.error(errorText(e, 'Could not mark it achieved.')) }); }
  setStatus(m: MilestoneDto, status: MilestoneDto['status']): void {
    this.api.update(m.id, { projectId: m.projectId, title: m.title, dueDate: m.dueDate, amount: m.amount, status, achievedDate: status === 'achieved' ? m.achievedDate : null }).subscribe({ next: () => { this.load(); this.changed.emit(); }, error: (e) => this.msg.error(errorText(e, 'Could not change the milestone.')) });
  }
  remove(m: MilestoneDto): void { this.modal.confirm({ nzTitle: 'Delete this milestone?', nzContent: m.title, nzOkText: 'Delete', nzOkDanger: true, nzOnOk: () => this.api.delete(m.id).subscribe({ next: () => { this.load(); this.changed.emit(); }, error: (e) => this.msg.error(errorText(e, 'Could not delete.')) }) }); }
  askSignOff(m: MilestoneDto): void { this.request(m.id, `Sign-off of ${m.title}`); }
  askProjectSignOff(): void { this.request(null, `Sign-off of ${this.project().name}`); }
  private request(milestoneId: string | null, title: string): void {
    this.extra.requestAcceptance({ projectId: this.project().id, milestoneId, title }).subscribe({ next: () => { this.msg.success(`The customer can sign ${title} in their portal`); this.load(); }, error: (e) => this.msg.error(errorText(e, 'Could not ask for the sign-off.')) });
  }
}

/** Time and expenses of a project. */
@Component({
  selector: 'erp-project-time',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ProjectTimesheetsPage, ProjectExpensesPage],
  template: `
    <div class="seg" role="group" aria-label="View">
      @if (canTime()) { <button type="button" [class.on]="view() === 'time'" (click)="view.set('time')" data-testid="time-tab">Time</button> }
      @if (canCost()) { <button type="button" [class.on]="view() === 'expenses'" (click)="view.set('expenses')" data-testid="expenses-tab">Expenses</button> }
    </div>
    @if (view() === 'time') { <erp-project-timesheets [projectId]="projectId()" /> } @else { <erp-project-expenses [projectId]="projectId()" /> }
  `,
  styles: [SUBTABS],
})
export class ProjectTimeComponent {
  readonly projectId = input.required<string>();
  private readonly session = inject(SessionStore);
  readonly canTime = computed(() => this.session.hasPermission('projects.timesheets.read'));
  readonly canCost = computed(() => this.session.hasPermission('projects.expenses.read'));
  readonly view = signal<'time' | 'expenses'>(this.session.hasPermission('projects.timesheets.read') ? 'time' : 'expenses');
}

/** The money of a project: what can be billed, what was invoiced, and the budget by category against what was spent. */
@Component({
  selector: 'erp-project-money',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpLabelPipe, ErpDatePipe, DecimalPipe, FormsModule, RouterModule, NzButtonModule, NzIconModule, NzInputNumberModule, NzSelectModule, HasPermissionDirective, BillModalComponent],
  template: `
    <div class="tiles">
      <div class="prj-card tile"><span class="prj-muted">Billed so far</span><b>{{ currency() }} {{ project().billedAmount | number: '1.0-0' }}</b></div>
      <div class="prj-card tile"><span class="prj-muted">Ready to bill</span><b data-testid="money-unbilled">{{ currency() }} {{ project().unbilledAmount | number: '1.0-0' }}</b>
        @if (canBill()) { <button nz-button nzType="primary" nzSize="small" [disabled]="project().isTemplate || project().billingType === 'nonBillable'" (click)="bill()?.open(project().id, project().name)" data-testid="money-bill"><span nz-icon nzType="dollar"></span> Bill work</button> }
      </div>
      <div class="prj-card tile"><span class="prj-muted">Cost so far</span><b>{{ currency() }} {{ project().totalTimesheetCost | number: '1.0-0' }}</b><span class="prj-muted small">labour {{ project().labourCost | number: '1.0-0' }} · expenses {{ project().expenseCost | number: '1.0-0' }}</span></div>
      <div class="prj-card tile"><span class="prj-muted">Margin so far</span><b [class.prj-danger]="margin() < 0">{{ currency() }} {{ margin() | number: '1.0-0' }}</b><span class="prj-muted small">billed and ready to bill, less cost</span></div>
    </div>

    <div class="prj-card block" data-testid="budget-lines">
      <div class="prj-toolbar"><h3 style="margin:0">Budget by category</h3><span class="prj-spacer"></span><span class="prj-muted">Total budget {{ currency() }} {{ project().effectiveBudget | number: '1.0-0' }} · planned here {{ currency() }} {{ plannedTotal() | number: '1.0-0' }}</span></div>
      <div class="prj-table-wrap">
        <table class="plain">
          <thead><tr><th>Category</th><th class="prj-num">Budget</th><th class="prj-num">Spent</th><th style="width:180px">Used</th><th class="prj-num">Left</th><th></th></tr></thead>
          <tbody>
            @for (l of lines(); track l.category) {
              <tr>
                <td class="prj-strong">{{ l.category | erpLabel }}@if (!l.id) { <span class="prj-muted"> · no budget line</span> }@if (l.hours) { <div class="prj-muted">{{ l.actualHours | number: '1.0-1' }} of {{ l.hours | number: '1.0-0' }} h</div> }</td>
                <td class="prj-num">{{ l.amount | number: '1.0-0' }}</td><td class="prj-num">{{ l.actual | number: '1.0-0' }}</td>
                <td><div class="prj-bar" [class.warn]="share(l) > 85" [class.over]="share(l) > 100 || !l.id"><i [style.width.%]="min100(share(l))"></i></div></td>
                <td class="prj-num" [class.prj-danger]="l.amount - l.actual < 0">{{ l.amount - l.actual | number: '1.0-0' }}</td>
                <td class="prj-num"><ng-container *erpHasPermission="'projects.projects.manage'">@if (l.id) { <button nz-button nzSize="small" nzType="text" nzDanger (click)="removeLine(l)" aria-label="Delete budget line"><span nz-icon nzType="delete"></span></button> }</ng-container></td>
              </tr>
            } @empty { <tr><td colspan="6" class="prj-empty">No budget by category yet. Split the budget below to track each kind of cost.</td></tr> }
          </tbody>
        </table>
      </div>
      <div class="prj-toolbar add" *erpHasPermission="'projects.projects.manage'">
        <nz-select [(ngModel)]="newCategory" nzShowSearch nzPlaceHolder="Category" style="width:190px" aria-label="Budget category" data-testid="budget-category">
          @for (c of categories; track c) { <nz-option [nzValue]="c" [nzLabel]="c"></nz-option> }
        </nz-select>
        <nz-input-number [(ngModel)]="newAmount" [nzMin]="0" nzPlaceHolder="Amount" aria-label="Budget amount" data-testid="budget-amount"></nz-input-number>
        @if (newCategory === 'Labour') { <nz-input-number [(ngModel)]="newHours" [nzMin]="0" nzPlaceHolder="Hours" aria-label="Budget hours"></nz-input-number> }
        <button nz-button [disabled]="!newCategory" (click)="saveLine()" data-testid="budget-save">Save budget line</button>
      </div>
    </div>

    <div class="prj-card block" data-testid="project-invoices">
      <h3>Invoices</h3>
      <div class="prj-table-wrap">
        <table class="plain">
          <thead><tr><th>Number</th><th>Date</th><th>Due</th><th>Status</th><th class="prj-num">Total</th><th class="prj-num">Paid</th></tr></thead>
          <tbody>
            @for (i of invoices(); track i.id) {
              <tr><td><a class="prj-link" [routerLink]="['/finance/invoices', i.id]">{{ i.number }}</a></td><td>{{ i.date | erpDate }}</td><td>{{ i.dueDate | erpDate }}</td><td>{{ label(i.status) }}</td><td class="prj-num">{{ i.total | number: '1.2-2' }}</td><td class="prj-num">{{ i.paid | number: '1.2-2' }}</td></tr>
            } @empty { <tr><td colspan="6" class="prj-empty">Nothing invoiced yet.</td></tr> }
          </tbody>
        </table>
      </div>
    </div>
    <erp-bill-modal (billed)="load(); changed.emit()" />
  `,
  styles: [PRJ_STYLES + SUBTABS + `
    .tiles { display:grid; grid-template-columns:repeat(auto-fit,minmax(200px,1fr)); gap:12px; margin-bottom:12px; } .tile { display:flex; flex-direction:column; gap:4px; align-items:flex-start; } .tile b { font-size:18px; color:var(--erp-color-text-primary,#0f172a); } .small { font-size:11.5px; }
    .block { margin-bottom:12px; } .add { margin:10px 0 0; }
  `],
})
export class ProjectMoneyComponent {
  readonly project = input.required<ProjectDto>();
  readonly changed = output<void>();
  private readonly extra = inject(ProjectsExtraApi);
  private readonly msg = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly session = inject(SessionStore);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly bill = viewChild(BillModalComponent);
  readonly lines = signal<BudgetLineDto[]>([]);
  readonly invoices = signal<ProjectInvoice[]>([]);
  readonly categories = ['Labour', ...EXPENSE_CATEGORIES.map((c) => c.value)];
  readonly margin = computed(() => this.project().billedAmount + this.project().unbilledAmount - this.project().totalTimesheetCost);
  readonly plannedTotal = computed(() => this.lines().reduce((a, l) => a + l.amount, 0));
  newCategory: string | null = null; newAmount: number | null = null; newHours: number | null = null;

  constructor() { effect(() => { this.project().id; this.load(); }); }

  canBill(): boolean { return this.session.hasPermission('projects.billing.manage'); }
  load(): void {
    const id = this.project().id;
    this.extra.budgetLines(id).pipe(catchError(() => of([] as BudgetLineDto[]))).subscribe((r) => this.lines.set(r));
    if (this.session.hasPermission('projects.billing.read')) this.extra.projectInvoices(id).pipe(catchError(() => of([] as ProjectInvoice[]))).subscribe((r) => this.invoices.set(r));
  }
  label(status: string): string { return status ? status[0].toUpperCase() + status.slice(1) : ''; }
  share(l: BudgetLineDto): number { return l.amount > 0 ? (l.actual / l.amount) * 100 : (l.actual > 0 ? 101 : 0); }
  min100(n: number): number { return Math.min(100, n); }
  saveLine(): void {
    if (!this.newCategory) return;
    const existing = this.lines().find((l) => l.id && l.category.toLowerCase() === this.newCategory!.toLowerCase());
    this.extra.saveBudgetLine(existing?.id ?? null, { projectId: this.project().id, category: this.newCategory, amount: this.newAmount ?? 0, hours: this.newHours ?? 0 }).subscribe({
      next: () => { this.newCategory = null; this.newAmount = null; this.newHours = null; this.load(); },
      error: (e) => this.msg.error(errorText(e, 'Could not save the budget line.')),
    });
  }
  removeLine(l: BudgetLineDto): void {
    this.modal.confirm({ nzTitle: `Delete the ${l.category} budget line?`, nzOkText: 'Delete', nzOkDanger: true, nzOnOk: () => this.extra.deleteBudgetLine(l.id!).subscribe({ next: () => this.load(), error: (e) => this.msg.error(errorText(e, 'Could not delete.')) }) });
  }
}

/** Who is on the project and how much of their time is planned for it. */
@Component({
  selector: 'erp-project-team',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RowActionsComponent, DecimalPipe, FormsModule, RouterModule, NzButtonModule, NzInputModule, NzSelectModule, HasPermissionDirective],
  template: `
    <div class="prj-card">
      <h3>Team</h3>
      <div class="prj-table-wrap">
        <table class="plain" data-testid="team">
          <thead><tr><th>Person</th><th>Role</th><th class="prj-num">Planned hours / week</th><th></th></tr></thead>
          <tbody>
            @for (m of members(); track m.id) {
              <tr><td class="prj-strong">{{ m.name }}</td><td>{{ m.role }}</td><td class="prj-num">{{ plannedFor(m.userId) | number: '1.0-1' }}</td>
                <td class="erp-cell-actions prj-num"><erp-row-actions><button *erpHasPermission="'projects.team.manage'" nz-button nzSize="small" nzDanger (click)="remove(m)">Remove</button></erp-row-actions></td></tr>
            } @empty { <tr><td colspan="4" class="prj-empty">No team members yet.</td></tr> }
          </tbody>
        </table>
      </div>
      <div class="prj-toolbar add" *erpHasPermission="'projects.team.manage'">
        <nz-select [(ngModel)]="newMember" nzShowSearch nzPlaceHolder="Add a person" style="width:240px" aria-label="Person to add" data-testid="member-user">
          @for (u of available(); track u.id) { <nz-option [nzValue]="u.id" [nzLabel]="u.fullName"></nz-option> }
        </nz-select>
        <input nz-input [(ngModel)]="newRole" placeholder="Role" maxlength="100" style="width:200px" aria-label="Role" />
        <button nz-button nzType="primary" [disabled]="!newMember" (click)="add()" data-testid="member-add">Add</button>
        <a nz-button routerLink="/projects/resources">Plan allocations</a>
      </div>
    </div>
  `,
  styles: [PRJ_STYLES + SUBTABS + '.add { margin:12px 0 0; }'],
})
export class ProjectTeamComponent {
  readonly projectId = input.required<string>();
  private readonly api = inject(ProjectsExtraApi);
  private readonly msg = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  readonly members = signal<MemberDto[]>([]);
  readonly allocations = signal<AllocationDto[]>([]);
  readonly users = signal<UserOption[]>([]);
  readonly available = computed(() => this.users().filter((u) => !this.members().some((m) => m.userId === u.id)));
  newMember: string | null = null; newRole = '';

  constructor() {
    this.api.users().pipe(catchError(() => of([] as UserOption[]))).subscribe((u) => this.users.set(u));
    effect(() => { this.projectId(); this.load(); });
  }
  load(): void {
    this.api.members(this.projectId()).subscribe({ next: (m) => this.members.set(m), error: (e) => this.msg.error(errorText(e, 'Could not load the team.')) });
    this.api.allocations(this.projectId()).pipe(catchError(() => of([] as AllocationDto[]))).subscribe((a) => this.allocations.set(a));
  }
  plannedFor(userId: string): number { const now = today(); return this.allocations().filter((a) => a.userId === userId && a.fromDate <= now && a.toDate >= now).reduce((s, a) => s + a.hoursPerWeek, 0); }
  add(): void { if (!this.newMember) return; this.api.addMember(this.projectId(), this.newMember, this.newRole || null).subscribe({ next: () => { this.newMember = null; this.newRole = ''; this.load(); }, error: (e) => this.msg.error(errorText(e, 'Could not add.')) }); }
  remove(m: MemberDto): void { this.modal.confirm({ nzTitle: `Remove ${m.name} from the team?`, nzOkText: 'Remove', nzOkDanger: true, nzOnOk: () => this.api.removeMember(m.id).subscribe({ next: () => this.load(), error: (e) => this.msg.error(errorText(e, 'Could not remove.')) }) }); }
}

/** Discussion, files and what happened on the project. */
@Component({
  selector: 'erp-project-notes',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpLabelPipe, DatePipe, FormsModule, NzButtonModule, NzIconModule, NzInputModule, AttachmentsComponent],
  template: `
    <div class="seg" role="group" aria-label="View">
      @for (v of views; track v.key) { <button type="button" [class.on]="view() === v.key" (click)="view.set(v.key)" [attr.data-testid]="'notes-' + v.key">{{ v.label }}</button> }
    </div>
    @switch (view()) {
      @case ('discussion') {
        <div class="prj-card" data-testid="comments">
          @for (c of comments(); track c.id) {
            <div class="comment"><div class="who"><b>{{ c.authorName }}</b> <span class="prj-muted">{{ c.createdAt | date: 'medium' }}</span>
              @if (canDelete(c)) { <button nz-button nzSize="small" nzType="link" (click)="removeComment(c)">Delete</button> }</div><p>{{ c.text }}</p></div>
          } @empty { <p class="prj-empty">No comments yet. Start the conversation.</p> }
          <div class="row">
            <textarea nz-input rows="2" [(ngModel)]="newComment" maxlength="2000" placeholder="Write a comment" aria-label="Comment" data-testid="comment-text" style="flex:1"></textarea>
            <button nz-button nzType="primary" [disabled]="!newComment.trim()" (click)="addComment()" data-testid="comment-add">Post</button>
          </div>
        </div>
      }
      @case ('files') { <div class="prj-card"><erp-attachments ownerType="project" [ownerId]="projectId()" /></div> }
      @case ('activity') {
        <div class="prj-card" data-testid="activity">
          @for (a of activity(); track $index) {
            <div class="event"><span class="dot" [class.created]="a.action === 'Created'" [class.deleted]="a.action === 'Deleted'"></span>
              <div><span class="prj-strong">{{ a.who || 'System' }}</span> {{ verb(a) }} {{ a.kind | erpLabel }} <b>{{ a.title }}</b>@if (a.fields.length) { <span class="prj-muted"> ({{ fieldList(a) }})</span> }<div class="prj-muted">{{ a.at | date: 'medium' }}</div></div></div>
          } @empty { <p class="prj-empty">Nothing recorded yet.</p> }
        </div>
      }
    }
  `,
  styles: [PRJ_STYLES + SUBTABS + `
    .comment { border-bottom:1px solid var(--erp-color-border-subtle,#f1f5f9); padding:8px 0; } .comment p { margin:4px 0 0; white-space:pre-wrap; } .row { display:flex; gap:8px; margin-top:12px; align-items:flex-start; }
    .event { display:flex; gap:10px; padding:7px 0; font-size:13px; border-bottom:1px solid var(--erp-color-border-subtle,#f1f5f9); } .dot { width:9px; height:9px; border-radius:50%; background:#60a5fa; margin-top:5px; flex:none; } .dot.created { background:#10955d; } .dot.deleted { background:#ef4444; }
  `],
})
export class ProjectNotesComponent {
  readonly projectId = input.required<string>();
  private readonly api = inject(ProjectsExtraApi);
  private readonly msg = inject(NzMessageService);
  private readonly session = inject(SessionStore);
  readonly views = [{ key: 'discussion', label: 'Discussion' }, { key: 'files', label: 'Files' }, { key: 'activity', label: 'Activity' }];
  readonly view = signal('discussion');
  readonly comments = signal<CommentDto[]>([]);
  readonly activity = signal<ActivityDto[]>([]);
  newComment = '';

  constructor() { effect(() => { this.projectId(); this.load(); }); }
  load(): void {
    this.api.comments('project', this.projectId()).pipe(catchError(() => of([] as CommentDto[]))).subscribe((c) => this.comments.set(c));
    this.api.activity(this.projectId()).pipe(catchError(() => of([] as ActivityDto[]))).subscribe((a) => this.activity.set(a));
  }
  canDelete(c: CommentDto): boolean { return c.userId === this.session.me()?.user?.id || this.session.hasPermission('projects.projects.manage'); }
  addComment(): void { this.api.addComment('project', this.projectId(), this.newComment.trim()).subscribe({ next: () => { this.newComment = ''; this.load(); }, error: (e) => this.msg.error(errorText(e, 'Could not post.')) }); }
  removeComment(c: CommentDto): void { this.api.deleteComment(c.id).subscribe({ next: () => this.load(), error: (e) => this.msg.error(errorText(e, 'Could not delete.')) }); }
  verb(a: ActivityDto): string { return a.action === 'Created' ? 'added' : a.action === 'Deleted' ? 'removed' : 'changed'; }
  fieldList(a: ActivityDto): string { return a.fields.map((f) => f.replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase()).join(', '); }
}

/** Risks and change orders of a project. */
@Component({
  selector: 'erp-project-control',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ProjectRisksPage, ProjectChangeOrdersPage],
  template: `
    <div class="seg" role="group" aria-label="View">
      @if (canRisks()) { <button type="button" [class.on]="view() === 'risks'" (click)="view.set('risks')" data-testid="control-risks">Risks and issues</button> }
      @if (canChanges()) { <button type="button" [class.on]="view() === 'changes'" (click)="view.set('changes')" data-testid="control-changes">Change orders</button> }
    </div>
    @if (view() === 'risks') { <erp-project-risks [projectId]="projectId()" /> } @else { <erp-project-change-orders [projectId]="projectId()" /> }
  `,
  styles: [SUBTABS],
})
export class ProjectControlComponent {
  readonly projectId = input.required<string>();
  private readonly session = inject(SessionStore);
  readonly canRisks = computed(() => this.session.hasPermission('projects.risks.read'));
  readonly canChanges = computed(() => this.session.hasPermission('projects.changeorders.read'));
  readonly view = signal<'risks' | 'changes'>(this.session.hasPermission('projects.risks.read') ? 'risks' : 'changes');
}
