import { DecimalPipe, NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { catchError, map, of } from 'rxjs';
import { LegalEntitiesApi } from '../../core/api/admin.api';
import { FilterCriterion } from '../../core/api/api.models';
import { createPagedList } from '../../core/api/paged-list';
import { ProjectDto, ProjectInput, ProjectKpiDto, ProjectsApi, ProjectSummaryDto } from '../../core/api/projects.api';
import { ProjectsExtraApi, UserOption } from '../../core/api/projects-extra.api';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { SessionStore } from '../../core/session/session.store';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { StatCardComponent } from '../../shared/stat-card.component';
import { PartiesApi } from '../parties.page';
import { BILLING_TYPES, errorText, filterOptions, PRJ_STYLES, PROJECT_ACTIONS, PROJECT_STATUSES, ProjectTagComponent } from './projects-ui';

/** An older project can name a manager without an account; the name is kept until a person is chosen. */
const typedManager = (p: ProjectSummaryDto | null): string | undefined => (p?.managerName && !p.managerUserId ? `Typed name: ${p.managerName}. Choose a person to link an account.` : undefined);

type View = 'live' | 'active' | 'planning' | 'onHold' | 'mine' | 'completed' | 'templates' | 'archived' | 'all';

/** The portfolio: how every project is doing at a glance, as a table or as cards, with quick views and starting a project from a template. */
@Component({
  selector: 'erp-projects-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, NgTemplateOutlet, FormsModule, RouterModule, NzButtonModule, NzDropDownModule, NzIconModule, NzInputModule, NzModalModule, NzSelectModule, NzToolTipModule, PagedTableComponent, RecordDrawerComponent, StatCardComponent, HasPermissionDirective, ProjectTagComponent],
  template: `
    <div class="erp-stat-grid erp-stat-grid--auto" data-testid="project-stats">
      <erp-stat-card label="Active projects" [value]="kpis()?.activeProjects ?? 0" format="number" [caption]="'of ' + (kpis()?.totalProjects ?? 0) + ' live projects'" [tone]="0" />
      <erp-stat-card label="Need attention" [value]="(kpis()?.atRiskProjects ?? 0) + (kpis()?.offTrackProjects ?? 0)" format="number" [caption]="(kpis()?.offTrackProjects ?? 0) + ' off track · ' + (kpis()?.overdueTasks ?? 0) + ' overdue tasks'" [tone]="(kpis()?.offTrackProjects ?? 0) > 0 ? 'danger' : 'warning'" />
      <erp-stat-card label="Budget" [value]="kpis()?.totalBudget ?? 0" format="money" [caption]="'cost so far ' + currency() + ' ' + ((kpis()?.totalCost ?? 0) | number: '1.0-0')" [tone]="3" />
      <erp-stat-card label="Ready to bill" [value]="kpis()?.unbilledAmount ?? 0" format="money" [caption]="((kpis()?.totalLoggedHours ?? 0) | number: '1.1-1') + ' h logged in total'" [tone]="4" link="/projects/billing" />
    </div>

    <div class="prj-toolbar views">
      <div class="prj-chips" role="group" aria-label="Which projects" data-testid="project-views">
        @for (v of views; track v.key) { <button type="button" class="prj-chip" [class.on]="view() === v.key" (click)="setView(v.key)" [attr.data-testid]="'view-' + v.key">{{ v.label }}</button> }
      </div>
      <span class="prj-spacer"></span>
      <div class="prj-chips" role="group" aria-label="Layout">
        <button type="button" class="prj-chip" [class.on]="layout() === 'table'" (click)="setLayout('table')" aria-label="Show as a table" data-testid="layout-table"><span nz-icon nzType="table"></span></button>
        <button type="button" class="prj-chip" [class.on]="layout() === 'cards'" (click)="setLayout('cards')" aria-label="Show as cards" data-testid="layout-cards"><span nz-icon nzType="appstore"></span></button>
      </div>
    </div>

    @if (layout() === 'cards') {
      <div class="prj-toolbar">
        <input nz-input placeholder="Search projects" aria-label="Search projects" style="max-width:280px" (input)="list.setSearch($any($event.target).value)" />
        <span class="prj-spacer"></span>
        <ng-container [ngTemplateOutlet]="newButton"></ng-container>
      </div>
      <div class="cards" data-testid="project-cards">
        @for (row of list.items(); track row.id) {
          <article class="pcard" tabindex="0" (click)="openDetail(row)" (keydown.enter)="openDetail(row)">
            <div class="ptop"><span class="code">{{ row.number }}</span> <erp-prj-tag kind="project" [value]="row.status" /> @if (row.health && row.health !== 'closed') { <erp-prj-tag kind="health" [value]="row.health" /> }</div>
            <h3>{{ row.name }}</h3>
            <p class="prj-muted">{{ row.customerName || 'Internal' }} @if (row.managerName) { · {{ row.managerName }} }</p>
            <div class="metric"><span>Progress</span><b>{{ row.percentComplete ?? 0 | number: '1.0-0' }}%</b></div>
            <div class="prj-bar"><i [style.width.%]="clamp(row.percentComplete ?? 0)"></i></div>
            <div class="metric"><span>Budget used</span><b [class.prj-danger]="used(row) > 100">{{ used(row) | number: '1.0-0' }}%</b></div>
            <div class="prj-bar" [class.warn]="used(row) > 85" [class.over]="used(row) > 100"><i [style.width.%]="clamp(used(row))"></i></div>
            <div class="foot prj-muted"><span>{{ row.openTasks ?? 0 }} open @if (row.overdueTasks) { · <span class="prj-danger">{{ row.overdueTasks }} overdue</span> }</span><span>{{ row.endDate || 'No end date' }}</span></div>
          </article>
        } @empty { <div class="prj-empty prj-card" style="grid-column:1/-1">{{ emptyText() }}</div> }
      </div>
    } @else {
      <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search project code, name or customer" [emptyText]="emptyText()" scrollX="1500px" exportFilename="projects" (rowClick)="openDetail($event)">
        <ng-container toolbar [ngTemplateOutlet]="newButton"></ng-container>
        <ng-template #cell let-row let-column="column" let-value="value">
          @switch (column.key) {
            @case ('number') { <span class="code">{{ row.number }}</span> }
            @case ('name') {
              <div class="name-cell">
                <a [routerLink]="['/projects', row.id]" class="prj-strong name" (click)="$event.stopPropagation()">{{ row.name }}</a>
                <span class="prj-muted">{{ row.customerName || 'Internal' }}</span>
              </div>
            }
            @case ('status') { <erp-prj-tag kind="project" [value]="row.status" /> @if (row.isTemplate) { <span class="prj-muted">Template</span> } @if (row.isArchived) { <span class="prj-muted">Archived</span> } }
            @case ('health') { @if (row.health && !row.isTemplate) { <erp-prj-tag kind="health" [value]="row.health" /> } }
            @case ('progress') {
              <div class="meter"><div class="prj-bar"><i [style.width.%]="clamp(row.percentComplete ?? 0)"></i></div><span>{{ row.percentComplete ?? 0 | number: '1.0-0' }}%</span></div>
              @if (row.overdueTasks) { <span class="prj-danger small">{{ row.overdueTasks }} overdue</span> }
            }
            @case ('budget') {
              <div class="meter"><div class="prj-bar" [class.warn]="used(row) > 85" [class.over]="used(row) > 100"><i [style.width.%]="clamp(used(row))"></i></div><span [class.prj-danger]="used(row) > 100">{{ used(row) | number: '1.0-0' }}%</span></div>
              <span class="prj-muted small">{{ currency() }} {{ row.cost ?? 0 | number: '1.0-0' }} of {{ row.effectiveBudget ?? row.budgetAmount | number: '1.0-0' }}</span>
            }
            @case ('unbilledAmount') { {{ row.unbilledAmount ? (currency() + ' ' + (row.unbilledAmount | number: '1.0-0')) : '' }} }
            @case ('billingType') { <erp-prj-tag kind="billing" [value]="row.billingType" /> }
            @case ('dates') { <span class="small">{{ row.startDate || '—' }} to {{ row.endDate || '—' }}</span> }
            @case ('actions') {
              <div class="acts" (click)="$event.stopPropagation()" (keydown.enter)="$event.stopPropagation()">
                <button *erpHasPermission="'projects.projects.manage'" aria-label="Edit project" nz-button nzType="text" nzSize="small" (click)="edit(row)" nzTooltipTitle="Edit" nz-tooltip [attr.data-testid]="'project-edit-' + row.number"><span nz-icon nzType="edit"></span></button>
                <button *erpHasPermission="'projects.projects.manage'" aria-label="More actions" nz-button nzType="text" nzSize="small" nz-dropdown [nzDropdownMenu]="more" nzTrigger="click" [attr.data-testid]="'project-more-' + row.number"><span nz-icon nzType="more"></span></button>
                <nz-dropdown-menu #more="nzDropdownMenu">
                  <ul nz-menu>
                    <li nz-menu-item (click)="startClone(row)">{{ row.isTemplate ? 'Create a project from this' : 'Clone' }}</li>
                    @if (!row.isTemplate) { @for (s of nextOf(row); track s) { <li nz-menu-item (click)="changeStatus(row, s)">{{ action(s) }}</li> } }
                    <li nz-menu-item (click)="toggleArchive(row)">{{ row.isArchived ? 'Restore' : 'Archive' }}</li>
                    <li nz-menu-item nzDanger (click)="remove(row)" [attr.data-testid]="'project-delete-' + row.number">Delete</li>
                  </ul>
                </nz-dropdown-menu>
              </div>
            }
            @default { {{ value }} }
          }
        </ng-template>
      </erp-paged-table>
    }

    <ng-template #newButton>
      <span *erpHasPermission="'projects.projects.manage'" class="new-group">
        <button nz-button nzType="primary" (click)="startNew()" data-testid="project-new"><span nz-icon nzType="plus"></span> New project</button>
        <button nz-button (click)="startFromTemplate()" [disabled]="!(kpis()?.templateCount)" data-testid="project-from-template" [title]="kpis()?.templateCount ? '' : 'No templates yet. Save a project as a template with Clone.'"><span nz-icon nzType="copy"></span> From a template</button>
      </span>
    </ng-template>

    <erp-record-drawer [title]="state.editing() ? 'Edit project' : 'New project'" [open]="state.open()" [fields]="fields()" [value]="$any(state.editing())" customFieldEntityType="project"
                       [saving]="state.saving()" [errors]="state.errors()" testId="project-form" (submitted)="state.submit($event)" (closed)="state.close()" />

    <nz-modal [nzVisible]="cloning() !== null" [nzTitle]="cloning()?.fromTemplate ? 'New project from a template' : 'Clone project'" nzOkText="Create" [nzOkLoading]="busy()" (nzOnOk)="clone()" (nzOnCancel)="cloning.set(null)">
      <ng-container *nzModalContent>
        <div class="form" data-testid="clone-form">
          @if (cloning()?.fromTemplate) {
            <label>Template
              <nz-select [(ngModel)]="cloneSource" (ngModelChange)="templatePicked($event)" nzShowSearch style="width:100%" aria-label="Template" data-testid="clone-template">
                @for (t of templates(); track t.id) { <nz-option [nzValue]="t.id" [nzLabel]="t.number + ' · ' + t.name"></nz-option> }
              </nz-select>
            </label>
          }
          <label>Name<input nz-input [(ngModel)]="cloneName" maxlength="200" data-testid="clone-name" /></label>
          <label>Start date<input nz-input type="date" [(ngModel)]="cloneStart" data-testid="clone-start" /></label>
          <label>Customer
            <nz-select [(ngModel)]="cloneCustomer" nzShowSearch nzServerSearch nzAllowClear (nzOnSearch)="searchCustomers($event)" nzPlaceHolder="Search customers" style="width:100%" aria-label="Customer">
              @for (c of customers(); track c.value) { <nz-option [nzValue]="c.value" [nzLabel]="c.label"></nz-option> }
            </nz-select>
          </label>
          <label class="check"><input type="checkbox" [(ngModel)]="cloneTeam" /> Keep the team and task assignees</label>
          @if (!cloning()?.fromTemplate) { <label class="check"><input type="checkbox" [(ngModel)]="cloneTemplate" /> Save the copy as a template</label> }
          <p class="prj-muted">Tasks, milestones, dependencies and project rate cards are copied, and dates move with the start date. Time, expenses and billing are not copied.</p>
        </div>
      </ng-container>
    </nz-modal>
  `,
  styles: [PRJ_STYLES + `
    .views { margin-top:16px; } .code { font-family:ui-monospace,monospace; font-size:12px; color:var(--erp-color-text-secondary,#475569); white-space:nowrap; }
    .name-cell { display:flex; flex-direction:column; min-width:0; } .name { text-decoration:none; } .name:hover { color:var(--erp-color-primary,#10955d); text-decoration:underline; }
    .meter { display:flex; align-items:center; gap:8px; } .meter .prj-bar { flex:1; min-width:50px; } .meter span { font-size:12px; font-variant-numeric:tabular-nums; } .small { font-size:12px; white-space:nowrap; }
    .acts { display:flex; gap:2px; justify-content:flex-end; } .new-group { display:inline-flex; gap:8px; }
    .cards { display:grid; grid-template-columns:repeat(auto-fill, minmax(280px, 1fr)); gap:12px; }
    .pcard { background:var(--erp-color-bg-surface,#fff); border:1px solid var(--erp-color-border-subtle,#e2e8f0); border-radius:12px; padding:14px; cursor:pointer; display:flex; flex-direction:column; gap:6px; } .pcard:hover { border-color:var(--erp-color-primary,#10955d); } .pcard:focus-visible { outline:2px solid var(--erp-color-primary,#10955d); }
    .ptop { display:flex; align-items:center; gap:6px; flex-wrap:wrap; } .pcard h3 { margin:2px 0 0; font-size:14.5px; font-weight:600; color:var(--erp-color-text-primary,#0f172a); } .pcard p { margin:0 0 4px; }
    .metric { display:flex; justify-content:space-between; font-size:12px; color:var(--erp-color-text-secondary,#475569); margin-top:4px; } .foot { display:flex; justify-content:space-between; margin-top:6px; font-size:12px; }
    .form { display:flex; flex-direction:column; gap:10px; } .form label { display:flex; flex-direction:column; gap:4px; font-size:13px; } .form .check { flex-direction:row; align-items:center; gap:6px; }
  `],
})
export class ProjectsListPage {
  private readonly api = inject(ProjectsApi);
  private readonly extra = inject(ProjectsExtraApi);
  private readonly parties = inject(PartiesApi);
  private readonly router = inject(Router);
  private readonly msg = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly session = inject(SessionStore);
  readonly currency = inject(WorkspaceCurrencyService).currency;

  private readonly entities = toSignal(inject(LegalEntitiesApi).list({ page: 1, pageSize: 100 }).pipe(catchError(() => of({ items: [] }))), { initialValue: { items: [] } });
  private readonly users = toSignal(this.extra.users().pipe(catchError(() => of([] as UserOption[]))), { initialValue: [] as UserOption[] });
  readonly kpis = signal<ProjectKpiDto | null>(null);

  readonly views: { key: View; label: string }[] = [
    { key: 'live', label: 'All live' }, { key: 'active', label: 'Active' }, { key: 'planning', label: 'Planning' }, { key: 'onHold', label: 'On hold' }, { key: 'mine', label: 'I manage' },
    { key: 'completed', label: 'Completed' }, { key: 'templates', label: 'Templates' }, { key: 'archived', label: 'Archived' },
  ];
  readonly view = signal<View>('live');
  readonly layout = signal<'table' | 'cards'>(this.readLayout());
  readonly emptyText = computed(() => (this.view() === 'templates' ? 'No templates yet. Clone a project and save the copy as a template.' : this.view() === 'archived' ? 'Nothing is archived.' : 'No projects here yet.'));

  readonly list = createPagedList((q) => this.api.list(q), { sort: 'number', filters: this.filtersFor('live') });

  readonly state: MasterDataState<ProjectSummaryDto> = new MasterDataState<ProjectSummaryDto>({
    save: (id, body) => this.saveProject(id, body as Partial<ProjectInput>),
    reload: () => this.refresh(),
    message: inject(NzMessageService),
    entityName: 'Project',
  });

  readonly columns: TableColumn<ProjectSummaryDto>[] = [
    { key: 'number', label: 'Code', sortField: 'number', width: '130px', filter: false },
    { key: 'name', label: 'Project', sortField: 'name', width: '300px', filter: false },
    { key: 'status', label: 'Status', sortField: 'status', width: '120px', filterField: 'status', filterOptions: filterOptions(PROJECT_STATUSES) },
    { key: 'health', label: 'Health', width: '100px', filter: false },
    { key: 'progress', label: 'Progress', width: '150px', filter: false },
    { key: 'budget', label: 'Budget used', width: '190px', filter: false },
    { key: 'unbilledAmount', label: 'Ready to bill', width: '120px', align: 'right', filter: false },
    { key: 'managerName', label: 'Manager', width: '130px', filter: false },
    { key: 'billingType', label: 'Billing', width: '140px', filterField: 'billingType', filterOptions: filterOptions(BILLING_TYPES), hidden: true },
    { key: 'dates', label: 'Timeline', width: '180px', filter: false },
    { key: 'actions', label: '', width: '84px' },
  ];

  readonly fields = computed<FieldSpec[]>(() => {
    const editing = !!this.state.editing();
    return [
      { key: 'name', label: 'Project name', type: 'text', required: true, maxLength: 200 },
      { key: 'number', label: 'Project code (blank creates one)', type: 'text', maxLength: 30 },
      { key: 'legalEntityId', label: 'Legal entity', type: 'select', required: true, options: this.entities().items.map((e) => ({ value: e.id, label: `${e.code} · ${e.name}` })), default: this.entities().items.length === 1 ? this.entities().items[0].id : undefined },
      { key: 'customerId', label: 'Customer', type: 'lookup', lookup: (term) => this.customerLookup(term), currentLabel: (r) => (r['customerName'] as string) ?? undefined },
      { key: 'managerUserId', label: 'Project manager', type: 'select', options: this.users().map((u) => ({ value: u.id, label: u.fullName })), hint: typedManager(this.state.editing()) },
      ...(editing ? [] : [{ key: 'status', label: 'Status', type: 'select' as const, required: true, options: filterOptions(PROJECT_STATUSES).slice(0, 2), default: 'planning' }]),
      { key: 'billingType', label: 'Billing model', type: 'select', required: true, options: filterOptions(BILLING_TYPES), default: 'timeAndMaterials' },
      { key: 'budgetAmount', label: `Budget or fixed fee (${this.currency()})`, type: 'number', min: 0, default: 0 },
      { key: 'budgetHours', label: 'Budgeted hours', type: 'number', min: 0, default: 0 },
      { key: 'startDate', label: 'Start date', type: 'date' },
      { key: 'endDate', label: 'Target end date', type: 'date' },
      { key: 'description', label: 'Description and scope', type: 'textarea', maxLength: 2000 },
    ];
  });

  readonly cloning = signal<{ fromTemplate: boolean; source: ProjectSummaryDto | null } | null>(null);
  readonly templates = signal<ProjectSummaryDto[]>([]);
  readonly customers = signal<{ value: string; label: string }[]>([]);
  readonly busy = signal(false);
  cloneSource: string | null = null; cloneName = ''; cloneStart = ''; cloneCustomer: string | null = null; cloneTeam = false; cloneTemplate = false;

  constructor() { this.reloadKpis(); }

  private readLayout(): 'table' | 'cards' { try { return localStorage.getItem('erp.projects.layout') === 'cards' ? 'cards' : 'table'; } catch { return 'table'; } }
  setLayout(l: 'table' | 'cards'): void { this.layout.set(l); try { localStorage.setItem('erp.projects.layout', l); } catch { /* storage may be blocked */ } }

  private filtersFor(v: View): FilterCriterion[] {
    const real: FilterCriterion[] = [{ field: 'isTemplate', op: 'eq', value: false }, { field: 'isArchived', op: 'eq', value: false }];
    switch (v) {
      case 'templates': return [{ field: 'isTemplate', op: 'eq', value: true }, { field: 'isArchived', op: 'eq', value: false }];
      case 'archived': return [{ field: 'isArchived', op: 'eq', value: true }];
      case 'mine': return [...real, { field: 'managerUserId', op: 'eq', value: this.me() ?? '' }];
      case 'active': case 'planning': case 'onHold': case 'completed': return [...real, { field: 'status', op: 'eq', value: v }];
      default: return real;
    }
  }
  private me(): string | null { return this.session?.me()?.user?.id ?? null; }
  setView(v: View): void { this.view.set(v); this.list.setFilters(this.filtersFor(v)); }

  clamp(n: number): number { return Math.max(0, Math.min(100, n)); }
  used(row: ProjectSummaryDto): number { const b = row.effectiveBudget ?? row.budgetAmount; return b > 0 ? ((row.cost ?? 0) / b) * 100 : 0; }
  action(s: ProjectSummaryDto['status']): string { return PROJECT_ACTIONS[s]; }
  nextOf(row: ProjectSummaryDto): ProjectSummaryDto['status'][] {
    const map: Record<string, ProjectSummaryDto['status'][]> = { planning: ['active', 'cancelled'], active: ['onHold', 'completed', 'cancelled'], onHold: ['active', 'cancelled'] };
    return map[row.status] ?? [];
  }

  openDetail(row: ProjectSummaryDto): void { void this.router.navigate(['/projects', row.id]); }

  startNew(): void { this.state.create(); }

  /** A row of the list has only some fields; load the whole project so the form shows, and saves, all of them. */
  edit(row: ProjectSummaryDto): void {
    this.api.get(row.id).subscribe({ next: (p) => this.state.edit(p), error: (e) => this.msg.error(errorText(e, 'Could not open the project.')) });
  }

  private saveProject(id: string | null, body: Partial<ProjectInput>) {
    const current = this.state.editing() as ProjectDto | null;
    if (!id || !current) return this.api.create(body as ProjectInput);
    const merged = { legalEntityId: current.legalEntityId, status: current.status, taxCodeId: current.taxCodeId ?? null, sourceType: current.sourceType ?? null, sourceId: current.sourceId ?? null, isTemplate: current.isTemplate, customFields: current.customFields ?? null, ...body } as ProjectInput;
    // the typed manager name goes with the chosen account; without one the old typed name stays
    if (!merged.managerUserId) merged.managerName = current.managerUserId ? null : current.managerName ?? null;
    return this.api.update(id, merged);
  }

  private refresh(): void { this.list.reload(); this.reloadKpis(); }
  private reloadKpis(): void { this.api.getKpis().pipe(catchError(() => of(null))).subscribe((k) => this.kpis.set(k)); }

  private customerLookup(term: string) {
    return this.parties.list({ page: 1, pageSize: 20, search: term, extra: { role: 'Customer' } }).pipe(map((r) => r.items.map((p) => ({ value: p.id, label: p.name }))));
  }
  searchCustomers(term: string): void { this.customerLookup(term).pipe(catchError(() => of([]))).subscribe((c) => this.customers.set(c)); }

  changeStatus(row: ProjectSummaryDto, status: ProjectSummaryDto['status']): void {
    const go = () => this.api.setStatus(row.id, status).subscribe({ next: () => { this.msg.success('Status changed'); this.refresh(); }, error: (e) => this.msg.error(errorText(e, 'Could not change the status.')) });
    if (status === 'cancelled' || status === 'completed') this.modal.confirm({ nzTitle: `${PROJECT_ACTIONS[status]}: ${row.name}?`, nzContent: 'A completed or cancelled project takes no more work and cannot be reopened.', nzOkText: PROJECT_ACTIONS[status], nzOkDanger: status === 'cancelled', nzOnOk: go });
    else go();
  }

  toggleArchive(row: ProjectSummaryDto): void {
    this.extra.archive(row.id, !row.isArchived).subscribe({ next: (r) => { this.msg.success(r.isArchived ? 'Archived' : 'Restored'); this.refresh(); }, error: (e) => this.msg.error(errorText(e, 'Could not change it.')) });
  }

  remove(row: ProjectSummaryDto): void {
    this.modal.confirm({
      nzTitle: `Delete ${row.number}?`, nzContent: `${row.name} and its tasks, milestones and time will be removed. A project with invoiced work cannot be deleted.`, nzOkText: 'Delete', nzOkDanger: true,
      nzOnOk: () => this.api.delete(row.id).subscribe({ next: () => { this.msg.success('Project deleted'); this.refresh(); }, error: (e) => this.msg.error(errorText(e, 'Could not delete the project.')) }),
    });
  }

  private resetClone(source: ProjectSummaryDto | null): void {
    this.cloneSource = source?.id ?? null; this.cloneName = source ? (source.isTemplate ? source.name.replace(/\s*\(template\)\s*$/i, '') : `${source.name} (copy)`) : '';
    this.cloneStart = toIsoDate(new Date()); this.cloneCustomer = null; this.cloneTeam = false; this.cloneTemplate = false;
    this.searchCustomers('');
  }
  startClone(row: ProjectSummaryDto): void { this.resetClone(row); this.cloning.set({ fromTemplate: !!row.isTemplate, source: row }); if (row.isTemplate) this.templates.set([row]); }
  startFromTemplate(): void {
    this.resetClone(null);
    this.api.list({ page: 1, pageSize: 200, sort: 'name', filters: [{ field: 'isTemplate', op: 'eq', value: true }, { field: 'isArchived', op: 'eq', value: false }] }).subscribe((r) => { this.templates.set(r.items); if (r.items[0]) this.templatePicked(r.items[0].id); });
    this.cloning.set({ fromTemplate: true, source: null });
  }
  templatePicked(id: string | null): void {
    const t = this.templates().find((x) => x.id === id); if (!t) return;
    this.cloneSource = t.id; this.cloneName = t.name.replace(/\s*\(template\)\s*$/i, '');
  }
  clone(): boolean {
    const id = this.cloneSource; const name = this.cloneName.trim();
    if (!id || !name) { this.msg.warning('Choose a template and a name.'); return false; }
    this.busy.set(true);
    this.extra.clone(id, { name, startDate: this.cloneStart || null, customerId: this.cloneCustomer, includeTeam: this.cloneTeam, asTemplate: this.cloneTemplate }).subscribe({
      next: (c) => { this.busy.set(false); this.cloning.set(null); this.msg.success(`Created ${c.number}`); void this.router.navigate(['/projects', c.id]); },
      error: (e) => { this.busy.set(false); this.msg.error(errorText(e, 'Could not create the copy.')); },
    });
    return false;
  }
}
