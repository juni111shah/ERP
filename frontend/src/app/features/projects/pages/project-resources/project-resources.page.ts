import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzTableModule } from 'ng-zorro-antd/table';
import { AllocationDto, AllocationInput, ProjectsExtraApi, RateDto, RateInput } from '@core/api/projects-extra.api';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { SessionStore } from '@core/session/session.store';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { loadOrFallback } from '@shared/utils/load-or-fallback';
import { MasterDataState } from '@shared/utils/master-data-state';
import { injectProjectsConfig, injectProjectUsers, injectRealProjects, projectNameIn, projectOptions, userOptions } from '../../projects-lookups';
import { withIsoDates } from '../../projects-mappers';
import { errorText, PRJ_STYLES } from '../../projects-ui';
import { capacityHeat, planWeeks } from '../../projects-view';
import { firstWeekDay } from '../../projects-week';

type Tab = 'capacity' | 'allocations' | 'rates';
const WEEKS = 12;

/** Who works on what and for how much: a capacity heatmap by week, planned allocations, and rate cards (bill and cost per hour). */
@Component({
  selector: 'erp-project-resources',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, DecimalPipe, RouterModule, NzButtonModule, NzIconModule, NzModalModule, NzTableModule, RecordDrawerComponent, HasPermissionDirective],
  templateUrl: './project-resources.page.html',
  styles: [PRJ_STYLES + `
    .tabs { display:flex; gap:4px; border-bottom:1px solid var(--erp-color-border-subtle,#e2e8f0); margin-bottom:12px; overflow-x:auto; }
    .tabs button { background:none; border:0; border-bottom:2px solid transparent; padding:8px 12px; cursor:pointer; color:var(--erp-color-text-secondary,#475569); white-space:nowrap; } .tabs button.on { color:var(--erp-color-primary,#10955d); border-bottom-color:var(--erp-color-primary,#10955d); font-weight:600; }
    .heat-card { padding:0; } .heat { border-collapse:collapse; width:100%; min-width:820px; font-size:12.5px; } .heat th { font-weight:600; color:var(--erp-color-text-secondary,#475569); padding:8px 4px; border-bottom:1px solid var(--erp-color-border-subtle,#e2e8f0); } .heat td { padding:3px; border-bottom:1px solid var(--erp-color-border-subtle,#f1f5f9); }
    .who { text-align:start; padding-inline-start:12px !important; min-width:160px; white-space:nowrap; } .wk { text-align:center; min-width:52px; }
    .cell { display:block; border-radius:5px; height:26px; line-height:26px; } .cell.free { background:var(--erp-color-bg-subtle,#f1f5f9); } .cell.ok { background:#bbf7d0; color:#14532d; } .cell.full { background:#fde68a; color:#713f12; } .cell.over { background:#fca5a5; color:#7f1d1d; font-weight:700; }
  `],
})
export class ProjectResourcesPage {
  private readonly api = inject(ProjectsExtraApi);
  private readonly msg = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly session = inject(SessionStore);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly rates = signal<RateDto[]>([]);
  readonly allocations = signal<AllocationDto[]>([]);
  readonly offset = signal(0);
  private readonly config = injectProjectsConfig();
  private readonly projects = injectRealProjects();
  private readonly users = injectProjectUsers();

  readonly canTeam = computed(() => this.session.hasPermission('projects.team.manage') || this.session.hasPermission('projects.projects.read'));
  readonly canRates = computed(() => this.session.hasPermission('projects.rates.read'));
  readonly tabs = computed<{ key: Tab; label: string }[]>(() => [...(this.canTeam() ? [{ key: 'capacity' as Tab, label: 'Capacity' }, { key: 'allocations' as Tab, label: 'Allocations' }] : []), ...(this.canRates() ? [{ key: 'rates' as Tab, label: 'Rate cards' }] : [])]);
  readonly tab = signal<Tab>(this.session.hasPermission('projects.team.manage') || this.session.hasPermission('projects.projects.read') ? 'capacity' : 'rates');
  readonly capacity = computed(() => this.config()?.weeklyCapacityHours ?? 40);

  /** The weeks on screen, Monday (or the workspace's first day) first. */
  readonly weeks = computed(() => planWeeks(firstWeekDay(this.config()?.workDays), this.offset(), WEEKS));

  /** Everyone with an allocation, and what is planned for them each week. An allocation counts in a week by the share of its days that fall in it. */
  readonly heat = computed(() => capacityHeat(this.allocations(), this.weeks(), this.capacity(), (id) => this.projectName(id)));

  readonly rateState = new MasterDataState<RateDto>({ save: (id, body) => this.api.saveRate(id, body as RateInput), reload: () => this.load(), message: this.msg, entityName: 'Rate' });
  readonly allocState = new MasterDataState<AllocationDto>({
    save: (id, body) => this.api.saveAllocation(id, withIsoDates(body, 'fromDate', 'toDate') as unknown as AllocationInput),
    reload: () => this.load(), message: this.msg, entityName: 'Allocation',
  });

  readonly rateFields = computed<FieldSpec[]>(() => [
    { key: 'projectId', label: 'Project (blank = every project)', type: 'select', options: projectOptions(this.projects().items) },
    { key: 'userId', label: 'Person', type: 'select', options: userOptions(this.users()) },
    { key: 'role', label: 'Or a role', type: 'text', maxLength: 100, hint: 'For example Senior consultant. A rate is for a person or a role, not both.', hidden: (v) => !!v['userId'] },
    { key: 'billRate', label: `Bill rate per hour (${this.currency()})`, type: 'number', required: true, min: 0, precision: 2 },
    { key: 'costRate', label: `Cost rate per hour (${this.currency()})`, type: 'number', required: true, min: 0, precision: 2 },
  ]);
  readonly allocFields = computed<FieldSpec[]>(() => [
    { key: 'projectId', label: 'Project', type: 'select', required: true, options: projectOptions(this.projects().items) },
    { key: 'userId', label: 'Person', type: 'select', required: true, options: userOptions(this.users()) },
    { key: 'fromDate', label: 'From', type: 'date', required: true, default: new Date() },
    { key: 'toDate', label: 'To', type: 'date', required: true, default: new Date(Date.now() + 30 * 864e5) },
    { key: 'hoursPerWeek', label: 'Hours per week', type: 'number', required: true, min: 1, max: 168, default: 20 },
  ]);

  constructor() { this.load(); }

  shift(weeks: number): void { this.offset.update((o) => o + weeks); }

  load(): void {
    if (this.canRates()) loadOrFallback(this.rates, this.api.rates(), []);
    loadOrFallback(this.allocations, this.api.allocations(), []);
  }
  projectName(id?: string | null): string { return id ? projectNameIn(this.projects().items, id, 'Project') : 'All projects'; }
  removeRate(r: RateDto): void {
    this.modal.confirm({ nzTitle: 'Delete this rate?', nzContent: 'New time will use the next most specific rate. Time already logged keeps its rate.', nzOkText: 'Delete', nzOkDanger: true, nzOnOk: () => this.api.deleteRate(r.id).subscribe({ next: () => this.load(), error: (e) => this.msg.error(errorText(e, 'Could not delete.')) }) });
  }
  removeAlloc(a: AllocationDto): void {
    this.modal.confirm({ nzTitle: 'Delete this allocation?', nzContent: `${a.userName}, ${a.fromDate} to ${a.toDate}`, nzOkText: 'Delete', nzOkDanger: true, nzOnOk: () => this.api.deleteAllocation(a.id).subscribe({ next: () => this.load(), error: (e) => this.msg.error(errorText(e, 'Could not delete.')) }) });
  }
}
