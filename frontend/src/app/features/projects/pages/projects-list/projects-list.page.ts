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
import { Observable, catchError, map, of } from 'rxjs';
import { LegalEntitiesApi } from '@core/api/admin.api';
import { FilterCriterion } from '@core/api/api.models';
import { createPagedList } from '@core/api/paged-list';
import { PartiesApi } from '@core/api/parties.api';
import { ProjectsExtraApi } from '@core/api/projects-extra.api';
import { ProjectDto, ProjectInput, ProjectKpiDto, ProjectsApi, ProjectSummaryDto } from '@core/api/projects.api';
import { toIsoDate } from '@core/format/dates';
import { doneText } from '@core/format/done-text';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { SessionStore } from '@core/session/session.store';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { loadOrFallback } from '@shared/utils/load-or-fallback';
import { MasterDataState } from '@shared/utils/master-data-state';
import { ProjectTagComponent } from '../../components/project-tag/project-tag.component';
import { injectProjectUsers, userOptions } from '../../projects-lookups';
import { mergeProjectInput } from '../../projects-mappers';
import { BILLING_TYPES, errorText, filterOptions, PRJ_STYLES, PROJECT_ACTIONS, PROJECT_STATUSES } from '../../projects-ui';

/** An older project can name a manager without an account; the name is kept until a person is chosen. */
const typedManager = (p: ProjectSummaryDto | null): string | undefined => (p?.managerName && !p.managerUserId ? `Typed name: ${p.managerName}. Choose a person to link an account.` : undefined);

type View = 'live' | 'active' | 'planning' | 'onHold' | 'mine' | 'completed' | 'templates' | 'archived' | 'all';

/** The portfolio: how every project is doing at a glance, as a table or as cards, with quick views and starting a project from a template. */
@Component({
  selector: 'erp-projects-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, DecimalPipe, NgTemplateOutlet, FormsModule, RouterModule, NzButtonModule, NzDropDownModule, NzIconModule, NzInputModule, NzModalModule, NzSelectModule, NzToolTipModule, PagedTableComponent, RecordDrawerComponent, StatCardComponent, HasPermissionDirective, ProjectTagComponent],
  templateUrl: './projects-list.page.html',
  styles: [PRJ_STYLES + `
    .views { margin-top:16px; } .code { font-family:ui-monospace,monospace; font-size:12px; color:var(--erp-color-text-secondary,#475569); white-space:nowrap; }
    .name-cell { display:flex; flex-direction:column; min-width:0; } .name { text-decoration:none; } .name:hover { color:var(--erp-color-primary,#10955d); text-decoration:underline; }
    .meter { display:flex; align-items:center; gap:8px; } .meter .prj-bar { flex:1; min-width:50px; } .meter span { font-size:12px; font-variant-numeric:tabular-nums; } .small { font-size:12px; white-space:nowrap; }
    .acts { display:flex; gap:2px; justify-content:flex-end; } .new-group { display:inline-flex; gap:8px; }
    .cards { display:grid; grid-template-columns:repeat(auto-fill, minmax(280px, 1fr)); gap:12px; }
    .pcard { background: var(--erp-card-bg); border: 1px solid var(--erp-card-border); box-shadow: var(--erp-card-shadow); border-radius:12px; padding:14px; display:flex; flex-direction:column; gap:6px; } .pcard:focus-visible { outline:2px solid var(--erp-color-primary,#10955d); }
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
  private readonly users = injectProjectUsers();
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
      { key: 'managerUserId', label: 'Project manager', type: 'select', options: userOptions(this.users()), hint: typedManager(this.state.editing()) },
      ...(editing ? [] : [{ key: 'status', label: 'Status', type: 'select' as const, required: true, options: filterOptions(PROJECT_STATUSES).slice(0, 2), default: 'planning' }]),
      { key: 'billingType', label: 'Billing model', type: 'select', required: true, options: filterOptions(BILLING_TYPES), default: 'timeAndMaterials' },
      { key: 'budgetAmount', label: `Budget or fixed fee (${this.currency()})`, type: 'number', min: 0, default: 0 },
      { key: 'contractValue', label: `Contract value (${this.currency()})`, type: 'number', min: 0, hint: 'What the customer agreed to pay, when it differs from the cost budget. Left empty, the budget is the fee.' },
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

  /** Creates a project, or updates one over the fields of the saved project the form does not show. */
  private saveProject(id: string | null, body: Partial<ProjectInput>): Observable<ProjectSummaryDto> {
    const current = this.state.editing() as ProjectDto | null;
    if (!id || !current) return this.api.create(body as ProjectInput);
    return this.api.update(id, mergeProjectInput(current, body));
  }

  private refresh(): void { this.list.reload(); this.reloadKpis(); }
  private reloadKpis(): void { loadOrFallback(this.kpis, this.api.getKpis(), null); }

  private customerLookup(term: string): Observable<{ value: string; label: string }[]> {
    return this.parties.list({ page: 1, pageSize: 20, search: term, extra: { role: 'Customer' } }).pipe(map((r) => r.items.map((p) => ({ value: p.id, label: p.name }))));
  }
  searchCustomers(term: string): void { loadOrFallback(this.customers, this.customerLookup(term), []); }

  changeStatus(row: ProjectSummaryDto, status: ProjectSummaryDto['status']): void {
    const go = () => this.api.setStatus(row.id, status).subscribe({ next: () => { this.msg.success(`${row.number} is now ${String(status).replace(/_/g, ' ')}`); this.refresh(); }, error: (e) => this.msg.error(errorText(e, 'Could not change the status.')) });
    if (status === 'cancelled' || status === 'completed') this.modal.confirm({ nzTitle: `${PROJECT_ACTIONS[status]}: ${row.name}?`, nzContent: 'A completed or cancelled project takes no more work and cannot be reopened.', nzOkText: PROJECT_ACTIONS[status], nzOkDanger: status === 'cancelled', nzOnOk: go });
    else go();
  }

  toggleArchive(row: ProjectSummaryDto): void {
    this.extra.archive(row.id, !row.isArchived).subscribe({ next: (r) => { this.msg.success(doneText(r.isArchived ? 'archived' : 'restored', 'Project', row.number)); this.refresh(); }, error: (e) => this.msg.error(errorText(e, 'Could not change it.')) });
  }

  remove(row: ProjectSummaryDto): void {
    this.modal.confirm({
      nzTitle: `Delete ${row.number}?`, nzContent: `${row.name} and its tasks, milestones and time will be removed. A project with invoiced work cannot be deleted.`, nzOkText: 'Delete', nzOkDanger: true,
      nzOnOk: () => this.api.delete(row.id).subscribe({ next: () => { this.msg.success(doneText('deleted', 'Project', row.name)); this.refresh(); }, error: (e) => this.msg.error(errorText(e, 'Could not delete the project.')) }),
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
      next: (c) => { this.busy.set(false); this.cloning.set(null); this.msg.success(doneText('created', 'Project', c.number)); void this.router.navigate(['/projects', c.id]); },
      error: (e) => { this.busy.set(false); this.msg.error(errorText(e, 'Could not create the copy.')); },
    });
    return false;
  }
}
