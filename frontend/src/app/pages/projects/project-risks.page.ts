import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalService } from 'ng-zorro-antd/modal';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { catchError, of } from 'rxjs';
import { ProjectsApi } from '../../core/api/projects.api';
import { ProjectsExtraApi, RiskDto, SEVERITIES, UserOption } from '../../core/api/projects-extra.api';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';

const SEVERITY_COLORS: Record<string, string> = { Low: 'default', Medium: 'gold', High: 'orange', Critical: 'red' };
const STATUS_COLORS: Record<string, string> = { Open: 'red', Mitigated: 'blue', Closed: 'green' };

/** Risks (things that might go wrong) and issues (things that have) for each project. */
@Component({
  selector: 'erp-project-risks',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, RouterModule, NzButtonModule, NzIconModule, NzTableModule, NzTagModule, RecordDrawerComponent, HasPermissionDirective],
  template: `
    <h1 class="erp-page-title">Risks and issues</h1>
    <div style="margin-bottom:12px">
      <button *erpHasPermission="'projects.risks.manage'" nz-button nzType="primary" (click)="state.create()" data-testid="risk-new"><span nz-icon nzType="plus"></span> Log a risk or issue</button>
    </div>
    <nz-table [nzData]="rows()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="risk-table">
      <thead><tr><th>Kind</th><th>Title</th><th>Project</th><th>Severity</th><th>Status</th><th>Owner</th><th>Due</th><th></th></tr></thead>
      <tbody>
        @for (r of rows(); track r.id) {
          <tr>
            <td>{{ r.kind }}</td><td>{{ r.title }}</td><td><a [routerLink]="['/projects', r.projectId]">{{ projectName(r.projectId) }}</a></td>
            <td><nz-tag [nzColor]="sevColor(r.severity)">{{ r.severity }}</nz-tag></td><td><nz-tag [nzColor]="statusColor(r.status)">{{ r.status }}</nz-tag></td>
            <td>{{ r.ownerName }}</td><td [style.color]="overdue(r) ? '#cf1322' : ''">{{ r.dueDate }}</td>
            <td *erpHasPermission="'projects.risks.manage'"><button nz-button nzSize="small" (click)="state.edit(r)" [attr.data-testid]="'risk-edit-' + r.id">Edit</button> <button nz-button nzSize="small" nzDanger (click)="remove(r)">Delete</button></td>
          </tr>
        } @empty { <tr><td colspan="8">Nothing logged.</td></tr> }
      </tbody>
    </nz-table>
    <erp-record-drawer [title]="state.editing() ? 'Edit risk or issue' : 'Log a risk or issue'" [open]="state.open()" [fields]="fields()" [value]="$any(state.editing())"
                       [saving]="state.saving()" [errors]="state.errors()" testId="risk-form" (submitted)="state.submit($event)" (closed)="state.close()" />
  `,
})
export class ProjectRisksPage {
  private readonly api = inject(ProjectsExtraApi);
  private readonly projectsApi = inject(ProjectsApi);
  private readonly msg = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  readonly rows = signal<RiskDto[]>([]);
  private readonly projects = toSignal(this.projectsApi.list({ page: 1, pageSize: 200 }).pipe(catchError(() => of({ items: [] }))), { initialValue: { items: [] } });
  private readonly users = toSignal(this.api.users().pipe(catchError(() => of([] as UserOption[]))), { initialValue: [] as UserOption[] });

  readonly state = new MasterDataState<RiskDto>({
    save: (id, body) => {
      const b = { ...(body as Record<string, unknown>) };
      if (b['dueDate'] instanceof Date) b['dueDate'] = toIsoDate(b['dueDate']);
      return this.api.saveRisk(id, b as never);
    },
    reload: () => this.load(), message: this.msg, entityName: 'Risk',
  });
  readonly fields = computed<FieldSpec[]>(() => [
    { key: 'projectId', label: 'Project', type: 'select', required: true, readonlyOnEdit: true, options: this.projects().items.map((p) => ({ value: p.id, label: `${p.number} · ${p.name}` })) },
    { key: 'kind', label: 'Kind', type: 'select', required: true, default: 'Risk', options: [{ value: 'Risk', label: 'Risk (might happen)' }, { value: 'Issue', label: 'Issue (has happened)' }] },
    { key: 'title', label: 'Title', type: 'text', required: true, maxLength: 200 },
    { key: 'notes', label: 'Details and plan', type: 'textarea', maxLength: 2000 },
    { key: 'severity', label: 'Severity', type: 'select', required: true, default: 'Medium', options: SEVERITIES },
    { key: 'status', label: 'Status', type: 'select', required: true, default: 'Open', options: [{ value: 'Open', label: 'Open' }, { value: 'Mitigated', label: 'Mitigated' }, { value: 'Closed', label: 'Closed' }] },
    { key: 'ownerUserId', label: 'Owner', type: 'select', options: this.users().map((u) => ({ value: u.id, label: u.fullName })) },
    { key: 'dueDate', label: 'Resolve by', type: 'date' },
  ]);

  constructor() { this.load(); }

  load(): void { this.api.risks().subscribe((r) => this.rows.set(r)); }
  sevColor(s: string): string { return SEVERITY_COLORS[s] ?? 'default'; }
  statusColor(s: string): string { return STATUS_COLORS[s] ?? 'default'; }
  overdue(r: RiskDto): boolean { return r.status === 'Open' && !!r.dueDate && r.dueDate < toIsoDate(new Date()); }
  projectName(id: string): string { const p = this.projects().items.find((x) => x.id === id); return p ? `${p.number} · ${p.name}` : 'Project'; }
  remove(r: RiskDto): void {
    this.modal.confirm({ nzTitle: 'Delete this entry?', nzOkText: 'Delete', nzOkDanger: true, nzOnOk: () => this.api.deleteRisk(r.id).subscribe({ next: () => this.load(), error: (e) => this.msg.error(e?.error?.detail ?? 'Could not delete.') }) });
  }
}
