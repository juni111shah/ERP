import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { catchError, of } from 'rxjs';
import { ProjectsApi } from '@core/api/projects.api';
import { ProjectsExtraApi, RiskDto, UserOption } from '@core/api/projects-extra.api';
import { toIsoDate } from '@core/custom-fields/custom-field-form';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { MasterDataState } from '@shared/utils/master-data-state';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { errorText, filterOptions, PRJ_STYLES, ProjectTagComponent, RISK_KINDS, RISK_SEVERITIES, RISK_STATUSES } from '../../projects-ui';
import { ErpDatePipe } from '@shared/pipes/display.pipes';

/** Risks (things that might go wrong) and issues (things that have), for all projects or for one. */
@Component({
  selector: 'erp-project-risks',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, FormsModule, RouterModule, NzButtonModule, NzIconModule, NzModalModule, NzSelectModule, NzTableModule, RecordDrawerComponent, HasPermissionDirective, ProjectTagComponent],
  template: `
    <div class="prj-toolbar">
      <div class="prj-chips" role="group" aria-label="Filter by status">
        @for (f of statusFilters; track f.value) { <button type="button" class="prj-chip" [class.on]="statusFilter() === f.value" (click)="statusFilter.set(f.value)">{{ f.label }}</button> }
      </div>
      <nz-select [ngModel]="severity()" (ngModelChange)="severity.set($event)" nzAllowClear nzPlaceHolder="Any severity" style="width:150px" aria-label="Severity">
        @for (s of severities; track s.value) { <nz-option [nzValue]="s.value" [nzLabel]="s.label"></nz-option> }
      </nz-select>
      <span class="prj-spacer"></span>
      <button *erpHasPermission="'projects.risks.manage'" nz-button nzType="primary" (click)="state.create()" data-testid="risk-new"><span nz-icon nzType="plus"></span> Log a risk or issue</button>
    </div>
    <div class="prj-table-wrap">
      <nz-table [nzData]="visible()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" [nzNoResult]="' '" data-testid="risk-table">
        <thead><tr><th>Kind</th><th>Title</th>@if (!projectId()) { <th>Project</th> }<th>Severity</th><th>Status</th><th>Owner</th><th>Resolve by</th><th></th></tr></thead>
        <tbody>
          @for (r of visible(); track r.id) {
            <tr>
              <td><erp-prj-tag kind="riskKind" [value]="r.kind" /></td>
              <td><span class="prj-strong">{{ r.title }}</span>@if (r.notes) { <div class="prj-muted">{{ r.notes }}</div> }</td>
              @if (!projectId()) { <td><a [routerLink]="['/projects', r.projectId]" [queryParams]="{ tab: 'risks' }" class="prj-link">{{ projectName(r.projectId) }}</a></td> }
              <td><erp-prj-tag kind="severity" [value]="r.severity" /></td><td><erp-prj-tag kind="risk" [value]="r.status" /></td>
              <td>{{ r.ownerName }}</td><td [class.prj-danger]="overdue(r)">{{ r.dueDate | erpDate }}</td>
              <td class="prj-num"><ng-container *erpHasPermission="'projects.risks.manage'"><button nz-button nzSize="small" (click)="state.edit(r)" [attr.data-testid]="'risk-edit-' + r.id">Edit</button> <button nz-button nzSize="small" nzDanger (click)="remove(r)">Delete</button></ng-container></td>
            </tr>
          }
        </tbody>
      </nz-table>
      @if (!visible().length) { <div class="prj-empty">{{ rows().length ? 'Nothing matches this filter.' : 'Nothing logged yet.' }}</div> }
    </div>
    <erp-record-drawer [title]="state.editing() ? 'Edit risk or issue' : 'Log a risk or issue'" [open]="state.open()" [fields]="fields()" [value]="$any(state.editing())"
                       [saving]="state.saving()" [errors]="state.errors()" testId="risk-form" (submitted)="state.submit($event)" (closed)="state.close()" />
  `,
  styles: [PRJ_STYLES],
})
export class ProjectRisksPage {
  /** Set by the project page, or by ?projectId= in the address. */
  readonly projectId = input<string | null>(null);

  private readonly api = inject(ProjectsExtraApi);
  private readonly projectsApi = inject(ProjectsApi);
  private readonly msg = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  readonly severities = RISK_SEVERITIES;
  readonly statusFilters = [{ value: 'active', label: 'Still open' }, { value: 'all', label: 'All' }, { value: 'closed', label: 'Closed' }];
  readonly statusFilter = signal('active');
  readonly severity = signal<string | null>(null);
  readonly rows = signal<RiskDto[]>([]);
  readonly visible = computed(() => {
    const status = this.statusFilter(); const sev = this.severity();
    return this.rows().filter((r) => (status === 'all' || (status === 'closed' ? r.status === 'closed' : r.status !== 'closed')) && (!sev || r.severity === sev));
  });
  private readonly projects = toSignal(this.projectsApi.list({ page: 1, pageSize: 200, sort: 'name', filters: [{ field: 'isTemplate', op: 'eq', value: false }] }).pipe(catchError(() => of({ items: [] }))), { initialValue: { items: [] } });
  private readonly users = toSignal(this.api.users().pipe(catchError(() => of([] as UserOption[]))), { initialValue: [] as UserOption[] });

  readonly state: MasterDataState<RiskDto> = new MasterDataState<RiskDto>({
    save: (id, body) => {
      const b = { ...(body as Record<string, unknown>) };
      if (b['dueDate'] instanceof Date) b['dueDate'] = toIsoDate(b['dueDate']);
      b['projectId'] = this.projectId() ?? b['projectId'] ?? this.state.editing()?.projectId;
      return this.api.saveRisk(id, b as never);
    },
    reload: () => this.load(), message: this.msg, entityName: 'Risk',
  });
  readonly fields = computed<FieldSpec[]>(() => [
    ...(this.projectId() ? [] : [{ key: 'projectId', label: 'Project', type: 'select' as const, required: true, readonlyOnEdit: true, options: this.projects().items.map((p) => ({ value: p.id, label: `${p.number} · ${p.name}` })) }]),
    { key: 'kind', label: 'Kind', type: 'select', required: true, default: 'risk', options: [{ value: 'risk', label: 'Risk (might happen)' }, { value: 'issue', label: 'Issue (has happened)' }] },
    { key: 'title', label: 'Title', type: 'text', required: true, maxLength: 200 },
    { key: 'notes', label: 'Details and plan', type: 'textarea', maxLength: 2000 },
    { key: 'severity', label: 'Severity', type: 'select', required: true, default: 'medium', options: filterOptions(RISK_SEVERITIES) },
    { key: 'status', label: 'Status', type: 'select', required: true, default: 'open', options: filterOptions(RISK_STATUSES) },
    { key: 'ownerUserId', label: 'Owner', type: 'select', options: this.users().map((u) => ({ value: u.id, label: u.fullName })) },
    { key: 'dueDate', label: 'Resolve by', type: 'date' },
  ]);
  readonly kinds = RISK_KINDS;

  constructor() { effect(() => { this.projectId(); this.load(); }); }

  load(): void { this.api.risks(this.projectId()).subscribe({ next: (r) => this.rows.set(r), error: (e) => this.msg.error(errorText(e, 'Could not load the risks.')) }); }
  overdue(r: RiskDto): boolean { return r.status === 'open' && !!r.dueDate && r.dueDate < toIsoDate(new Date()); }
  projectName(id: string): string { const p = this.projects().items.find((x) => x.id === id); return p ? `${p.number} · ${p.name}` : 'Open project'; }
  remove(r: RiskDto): void {
    this.modal.confirm({ nzTitle: 'Delete this entry?', nzContent: r.title, nzOkText: 'Delete', nzOkDanger: true, nzOnOk: () => this.api.deleteRisk(r.id).subscribe({ next: () => this.load(), error: (e) => this.msg.error(errorText(e, 'Could not delete.')) }) });
  }
}
