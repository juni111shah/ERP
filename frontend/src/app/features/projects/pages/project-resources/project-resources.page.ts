import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzTableModule } from 'ng-zorro-antd/table';
import { catchError, of } from 'rxjs';
import { ProjectsApi } from '@core/api/projects.api';
import { AllocationDto, ProjectsConfig, ProjectsExtraApi, RateDto, UserOption } from '@core/api/projects-extra.api';
import { toIsoDate } from '@core/custom-fields/custom-field-form';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { SessionStore } from '@core/session/session.store';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { MasterDataState } from '@shared/utils/master-data-state';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { errorText, PRJ_STYLES } from '../../projects-ui';
import { ErpDatePipe } from '@shared/pipes/display.pipes';

type Tab = 'capacity' | 'allocations' | 'rates';
const WEEKS = 12;

/** Who works on what and for how much: a capacity heatmap by week, planned allocations, and rate cards (bill and cost per hour). */
@Component({
  selector: 'erp-project-resources',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, DecimalPipe, RouterModule, NzButtonModule, NzIconModule, NzModalModule, NzTableModule, RecordDrawerComponent, HasPermissionDirective],
  template: `
    <nav class="tabs" role="tablist" aria-label="Resources">
      @for (t of tabs(); track t.key) { <button type="button" role="tab" [class.on]="tab() === t.key" [attr.aria-selected]="tab() === t.key" (click)="tab.set(t.key)" [attr.data-testid]="'res-tab-' + t.key">{{ t.label }}</button> }
    </nav>

    @switch (tab()) {
      @case ('capacity') {
        <div class="prj-toolbar">
          <span class="prj-muted">Planned hours a week against {{ capacity() | number: '1.0-0' }} hours of capacity. Red means more is planned than the person can do.</span>
          <span class="prj-spacer"></span>
          <button nz-button (click)="shift(-4)" aria-label="Earlier weeks"><span nz-icon nzType="left"></span></button>
          <button nz-button (click)="offset.set(0)">Now</button>
          <button nz-button (click)="shift(4)" aria-label="Later weeks"><span nz-icon nzType="right"></span></button>
        </div>
        <div class="prj-card prj-table-wrap heat-card">
          <table class="heat" data-testid="capacity-grid">
            <thead><tr><th class="who">Person</th>@for (w of weeks(); track w.key) { <th class="wk">{{ w.label }}</th> }</tr></thead>
            <tbody>
              @for (p of heat(); track p.userId) {
                <tr>
                  <td class="who">{{ p.name }}</td>
                  @for (c of p.cells; track c.key) {
                    <td class="wk"><span class="cell" [class.free]="!c.hours" [class.ok]="c.hours > 0 && c.ratio <= 0.8" [class.full]="c.ratio > 0.8 && c.ratio <= 1" [class.over]="c.ratio > 1" [title]="c.hint" [attr.data-testid]="c.ratio > 1 ? 'over-allocated' : null">{{ c.hours ? (c.hours | number: '1.0-0') : '' }}</span></td>
                  }
                </tr>
              } @empty { <tr><td [attr.colspan]="weeks().length + 1" class="prj-empty">Nobody is allocated yet. Add an allocation to plan who works on what.</td></tr> }
            </tbody>
          </table>
        </div>
      }
      @case ('allocations') {
        <div class="prj-toolbar">
          <span class="prj-muted">Hours a week a person is planned to give a project.</span><span class="prj-spacer"></span>
          <button *erpHasPermission="'projects.team.manage'" nz-button nzType="primary" (click)="allocState.create()" data-testid="alloc-new"><span nz-icon nzType="plus"></span> New allocation</button>
        </div>
        <div class="prj-card prj-table-wrap">
          <nz-table [nzData]="allocations()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="alloc-table">
            <thead><tr><th>Person</th><th>Project</th><th>From</th><th>To</th><th class="prj-num">Hours / week</th><th></th></tr></thead>
            <tbody>
              @for (a of allocations(); track a.id) {
                <tr><td>{{ a.userName }}</td><td><a class="prj-link" [routerLink]="['/projects', a.projectId]" [queryParams]="{ tab: 'team' }">{{ projectName(a.projectId) }}</a></td><td>{{ a.fromDate | erpDate }}</td><td>{{ a.toDate | erpDate }}</td><td class="prj-num">{{ a.hoursPerWeek | number: '1.0-1' }}</td>
                  <td class="prj-num"><ng-container *erpHasPermission="'projects.team.manage'"><button nz-button nzSize="small" (click)="allocState.edit(a)">Edit</button> <button nz-button nzSize="small" nzDanger (click)="removeAlloc(a)">Delete</button></ng-container></td></tr>
              } @empty { <tr><td colspan="6" class="prj-empty">Nobody is allocated yet.</td></tr> }
            </tbody>
          </nz-table>
        </div>
      }
      @case ('rates') {
        <div class="prj-toolbar">
          <span class="prj-muted">The most specific rate wins: a person on a project, then the person, then a role, then the project, then everyone.</span><span class="prj-spacer"></span>
          <button *erpHasPermission="'projects.rates.manage'" nz-button nzType="primary" (click)="rateState.create()" data-testid="rate-new"><span nz-icon nzType="plus"></span> New rate</button>
        </div>
        <div class="prj-card prj-table-wrap">
          <nz-table [nzData]="rates()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="rate-table">
            <thead><tr><th>Applies to</th><th>Project</th><th class="prj-num">Bill rate / hour</th><th class="prj-num">Cost rate / hour</th><th class="prj-num">Margin</th><th></th></tr></thead>
            <tbody>
              @for (r of rates(); track r.id) {
                <tr><td>{{ r.userName || r.role || 'Everyone' }}</td><td>{{ projectName(r.projectId) }}</td><td class="prj-num">{{ currency() }} {{ r.billRate | number: '1.2-2' }}</td><td class="prj-num">{{ currency() }} {{ r.costRate | number: '1.2-2' }}</td>
                  <td class="prj-num">{{ r.billRate > 0 ? ((r.billRate - r.costRate) / r.billRate * 100 | number: '1.0-0') + '%' : '' }}</td>
                  <td class="prj-num"><ng-container *erpHasPermission="'projects.rates.manage'"><button nz-button nzSize="small" (click)="rateState.edit(r)">Edit</button> <button nz-button nzSize="small" nzDanger (click)="removeRate(r)">Delete</button></ng-container></td></tr>
              } @empty { <tr><td colspan="6" class="prj-empty">No rate cards. Time uses the workspace default rate.</td></tr> }
            </tbody>
          </nz-table>
        </div>
      }
    }
    <erp-record-drawer [title]="rateState.editing() ? 'Edit rate' : 'New rate'" [open]="rateState.open()" [fields]="rateFields()" [value]="$any(rateState.editing())" [saving]="rateState.saving()" [errors]="rateState.errors()" testId="rate-form" (submitted)="rateState.submit($event)" (closed)="rateState.close()" />
    <erp-record-drawer [title]="allocState.editing() ? 'Edit allocation' : 'New allocation'" [open]="allocState.open()" [fields]="allocFields()" [value]="$any(allocState.editing())" [saving]="allocState.saving()" [errors]="allocState.errors()" testId="alloc-form" (submitted)="allocState.submit($event)" (closed)="allocState.close()" />
  `,
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
  private readonly projectsApi = inject(ProjectsApi);
  private readonly msg = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly session = inject(SessionStore);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly rates = signal<RateDto[]>([]);
  readonly allocations = signal<AllocationDto[]>([]);
  readonly offset = signal(0);
  private readonly config = toSignal(this.api.config().pipe(catchError(() => of(null as ProjectsConfig | null))), { initialValue: null });
  private readonly projects = toSignal(this.projectsApi.list({ page: 1, pageSize: 200, sort: 'name', filters: [{ field: 'isTemplate', op: 'eq', value: false }] }).pipe(catchError(() => of({ items: [] }))), { initialValue: { items: [] } });
  private readonly users = toSignal(this.api.users().pipe(catchError(() => of([] as UserOption[]))), { initialValue: [] as UserOption[] });

  readonly canTeam = computed(() => this.session.hasPermission('projects.team.manage') || this.session.hasPermission('projects.projects.read'));
  readonly canRates = computed(() => this.session.hasPermission('projects.rates.read'));
  readonly tabs = computed<{ key: Tab; label: string }[]>(() => [...(this.canTeam() ? [{ key: 'capacity' as Tab, label: 'Capacity' }, { key: 'allocations' as Tab, label: 'Allocations' }] : []), ...(this.canRates() ? [{ key: 'rates' as Tab, label: 'Rate cards' }] : [])]);
  readonly tab = signal<Tab>(this.session.hasPermission('projects.team.manage') || this.session.hasPermission('projects.projects.read') ? 'capacity' : 'rates');
  readonly capacity = computed(() => this.config()?.weeklyCapacityHours ?? 40);

  /** The weeks on screen, Monday (or the workspace's first day) first. */
  readonly weeks = computed(() => {
    const now = new Date(); const first = this.config()?.workDays?.includes(0) && !this.config()?.workDays?.includes(6) ? 0 : 1;
    const start = new Date(now.getFullYear(), now.getMonth(), now.getDate()); start.setDate(start.getDate() - ((start.getDay() - first + 7) % 7) + this.offset() * 7);
    return Array.from({ length: WEEKS }, (_, i) => {
      const from = new Date(start); from.setDate(from.getDate() + i * 7);
      const to = new Date(from); to.setDate(to.getDate() + 6);
      return { key: toIsoDate(from), from: toIsoDate(from), to: toIsoDate(to), label: from.toLocaleDateString(undefined, { day: 'numeric', month: 'short' }) };
    });
  });

  /** Everyone with an allocation, and what is planned for them each week. An allocation counts in a week by the share of its days that fall in it. */
  readonly heat = computed(() => {
    const capacity = this.capacity();
    const byUser = new Map<string, AllocationDto[]>();
    for (const a of this.allocations()) byUser.set(a.userId, [...(byUser.get(a.userId) ?? []), a]);
    return [...byUser.entries()].map(([userId, rows]) => ({
      userId, name: rows[0].userName,
      cells: this.weeks().map((w) => {
        const hits = rows.filter((a) => a.fromDate <= w.to && a.toDate >= w.from);
        const hours = hits.reduce((sum, a) => {
          const from = a.fromDate > w.from ? a.fromDate : w.from; const to = a.toDate < w.to ? a.toDate : w.to;
          const days = Math.round((new Date(to).getTime() - new Date(from).getTime()) / 864e5) + 1;
          return sum + (a.hoursPerWeek * Math.min(days, 7)) / 7;
        }, 0);
        return { key: w.key, hours, ratio: capacity ? hours / capacity : 0, hint: hits.map((a) => `${this.projectName(a.projectId)}: ${a.hoursPerWeek} h/week`).join('\n') };
      }),
    })).sort((a, b) => a.name.localeCompare(b.name));
  });

  readonly rateState = new MasterDataState<RateDto>({ save: (id, body) => this.api.saveRate(id, body as never), reload: () => this.load(), message: this.msg, entityName: 'Rate' });
  readonly allocState = new MasterDataState<AllocationDto>({
    save: (id, body) => {
      const b = { ...(body as Record<string, unknown>) };
      for (const k of ['fromDate', 'toDate']) if (b[k] instanceof Date) b[k] = toIsoDate(b[k] as Date);
      return this.api.saveAllocation(id, b as never);
    },
    reload: () => this.load(), message: this.msg, entityName: 'Allocation',
  });

  readonly rateFields = computed<FieldSpec[]>(() => [
    { key: 'projectId', label: 'Project (blank = every project)', type: 'select', options: this.projects().items.map((p) => ({ value: p.id, label: `${p.number} · ${p.name}` })) },
    { key: 'userId', label: 'Person', type: 'select', options: this.users().map((u) => ({ value: u.id, label: u.fullName })) },
    { key: 'role', label: 'Or a role', type: 'text', maxLength: 100, hint: 'For example Senior consultant. A rate is for a person or a role, not both.', hidden: (v) => !!v['userId'] },
    { key: 'billRate', label: `Bill rate per hour (${this.currency()})`, type: 'number', required: true, min: 0, precision: 2 },
    { key: 'costRate', label: `Cost rate per hour (${this.currency()})`, type: 'number', required: true, min: 0, precision: 2 },
  ]);
  readonly allocFields = computed<FieldSpec[]>(() => [
    { key: 'projectId', label: 'Project', type: 'select', required: true, options: this.projects().items.map((p) => ({ value: p.id, label: `${p.number} · ${p.name}` })) },
    { key: 'userId', label: 'Person', type: 'select', required: true, options: this.users().map((u) => ({ value: u.id, label: u.fullName })) },
    { key: 'fromDate', label: 'From', type: 'date', required: true, default: new Date() },
    { key: 'toDate', label: 'To', type: 'date', required: true, default: new Date(Date.now() + 30 * 864e5) },
    { key: 'hoursPerWeek', label: 'Hours per week', type: 'number', required: true, min: 1, max: 168, default: 20 },
  ]);

  constructor() { this.load(); }

  shift(weeks: number): void { this.offset.update((o) => o + weeks); }

  load(): void {
    if (this.canRates()) this.api.rates().pipe(catchError(() => of([] as RateDto[]))).subscribe((r) => this.rates.set(r));
    this.api.allocations().pipe(catchError(() => of([] as AllocationDto[]))).subscribe((r) => this.allocations.set(r));
  }
  projectName(id?: string | null): string { if (!id) return 'All projects'; const p = this.projects().items.find((x) => x.id === id); return p ? `${p.number} · ${p.name}` : 'Project'; }
  removeRate(r: RateDto): void {
    this.modal.confirm({ nzTitle: 'Delete this rate?', nzContent: 'New time will use the next most specific rate. Time already logged keeps its rate.', nzOkText: 'Delete', nzOkDanger: true, nzOnOk: () => this.api.deleteRate(r.id).subscribe({ next: () => this.load(), error: (e) => this.msg.error(errorText(e, 'Could not delete.')) }) });
  }
  removeAlloc(a: AllocationDto): void {
    this.modal.confirm({ nzTitle: 'Delete this allocation?', nzContent: `${a.userName}, ${a.fromDate} to ${a.toDate}`, nzOkText: 'Delete', nzOkDanger: true, nzOnOk: () => this.api.deleteAllocation(a.id).subscribe({ next: () => this.load(), error: (e) => this.msg.error(errorText(e, 'Could not delete.')) }) });
  }
}
