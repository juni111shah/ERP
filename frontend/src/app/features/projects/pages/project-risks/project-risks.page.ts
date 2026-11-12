import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { ProjectsExtraApi, RiskDto, RiskInput } from '@core/api/projects-extra.api';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { MasterDataState } from '@shared/utils/master-data-state';
import { ProjectTagComponent } from '../../components/project-tag/project-tag.component';
import { isoToday } from '../../projects-format';
import { injectProjectUsers, injectRealProjects, projectNameIn, projectOptions, userOptions } from '../../projects-lookups';
import { withIsoDates } from '../../projects-mappers';
import { errorText, filterOptions, PRJ_STYLES, RISK_KINDS, RISK_SEVERITIES, RISK_STATUSES } from '../../projects-ui';

/** Risks (things that might go wrong) and issues (things that have), for all projects or for one. */
@Component({
  selector: 'erp-project-risks',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, FormsModule, RouterModule, NzButtonModule, NzIconModule, NzModalModule, NzSelectModule, NzTableModule, RecordDrawerComponent, HasPermissionDirective, ProjectTagComponent],
  templateUrl: './project-risks.page.html',
  styles: [PRJ_STYLES],
})
export class ProjectRisksPage {
  /** Set by the project page, or by ?projectId= in the address. */
  readonly projectId = input<string | null>(null);

  private readonly api = inject(ProjectsExtraApi);
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
  private readonly projects = injectRealProjects();
  private readonly users = injectProjectUsers();

  readonly state: MasterDataState<RiskDto> = new MasterDataState<RiskDto>({
    save: (id, body) => {
      const b = withIsoDates(body, 'dueDate');
      b['projectId'] = this.projectId() ?? b['projectId'] ?? this.state.editing()?.projectId;
      return this.api.saveRisk(id, b as unknown as RiskInput);
    },
    reload: () => this.load(), message: this.msg, entityName: 'Risk',
  });
  readonly fields = computed<FieldSpec[]>(() => [
    ...(this.projectId() ? [] : [{ key: 'projectId', label: 'Project', type: 'select' as const, required: true, readonlyOnEdit: true, options: projectOptions(this.projects().items) }]),
    { key: 'kind', label: 'Kind', type: 'select', required: true, default: 'risk', options: [{ value: 'risk', label: 'Risk (might happen)' }, { value: 'issue', label: 'Issue (has happened)' }] },
    { key: 'title', label: 'Title', type: 'text', required: true, maxLength: 200 },
    { key: 'notes', label: 'Details and plan', type: 'textarea', maxLength: 2000 },
    { key: 'severity', label: 'Severity', type: 'select', required: true, default: 'medium', options: filterOptions(RISK_SEVERITIES) },
    { key: 'status', label: 'Status', type: 'select', required: true, default: 'open', options: filterOptions(RISK_STATUSES) },
    { key: 'ownerUserId', label: 'Owner', type: 'select', options: userOptions(this.users()) },
    { key: 'dueDate', label: 'Resolve by', type: 'date' },
  ]);
  readonly kinds = RISK_KINDS;

  constructor() { effect(() => { this.projectId(); this.load(); }); }

  load(): void { this.api.risks(this.projectId()).subscribe({ next: (r) => this.rows.set(r), error: (e) => this.msg.error(errorText(e, 'Could not load the risks.')) }); }
  overdue(r: RiskDto): boolean { return r.status === 'open' && !!r.dueDate && r.dueDate < isoToday(); }
  projectName(id: string): string { return projectNameIn(this.projects().items, id, 'Open project'); }
  remove(r: RiskDto): void {
    this.modal.confirm({ nzTitle: 'Delete this entry?', nzContent: r.title, nzOkText: 'Delete', nzOkDanger: true, nzOnOk: () => this.api.deleteRisk(r.id).subscribe({ next: () => this.load(), error: (e) => this.msg.error(errorText(e, 'Could not delete.')) }) });
  }
}
