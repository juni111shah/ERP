import { CommonModule, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { catchError, of } from 'rxjs';
import { LegalEntitiesApi } from '../../core/api/admin.api';
import { PartiesApi, PartyDto } from '../parties.page';
import { createPagedList } from '../../core/api/paged-list';
import { ProjectBillingType, ProjectInput, ProjectKpiDto, ProjectsApi, ProjectStatus, ProjectSummaryDto } from '../../core/api/projects.api';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';

@Component({
  selector: 'erp-projects-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    DecimalPipe,
    RouterModule,
    NzGridModule,
    NzCardModule,
    NzTagModule,
    NzButtonModule,
    NzIconModule,
    NzToolTipModule,
    PagedTableComponent,
    RecordDrawerComponent,
    HasPermissionDirective,
  ],
  template: `
    <div class="projects-container">
      <div class="page-header">
        <div>
          <h1 class="erp-page-title">Projects</h1>
          <p class="erp-page-subtitle">Track project lifecycles, milestones, tasks, timesheets and project billing.</p>
        </div>
        <div class="header-actions">
          <button toolbar *erpHasPermission="'projects.projects.manage'" nz-button nzType="primary" (click)="state.create()" data-testid="project-new">
            <span nz-icon nzType="plus"></span>New Project
          </button>
        </div>
      </div>

      <!-- KPI Stat Cards -->
      <div nz-row [nzGutter]="[16, 16]" class="kpi-row">
        <div nz-col [nzXs]="24" [nzSm]="12" [nzMd]="6">
          <nz-card class="stat-card">
            <div class="stat-header">
              <span class="stat-label">Active Projects</span>
              <span nz-icon nzType="project" class="stat-icon icon-active"></span>
            </div>
            <div class="stat-value">{{ kpis()?.activeProjects ?? 0 }}</div>
            <div class="stat-sub">of {{ kpis()?.totalProjects ?? 0 }} total projects</div>
          </nz-card>
        </div>
        <div nz-col [nzXs]="24" [nzSm]="12" [nzMd]="6">
          <nz-card class="stat-card">
            <div class="stat-header">
              <span class="stat-label">Completed</span>
              <span nz-icon nzType="check-circle" class="stat-icon icon-completed"></span>
            </div>
            <div class="stat-value">{{ kpis()?.completedProjects ?? 0 }}</div>
            <div class="stat-sub">delivered successfully</div>
          </nz-card>
        </div>
        <div nz-col [nzXs]="24" [nzSm]="12" [nzMd]="6">
          <nz-card class="stat-card">
            <div class="stat-header">
              <span class="stat-label">Total Budget</span>
              <span nz-icon nzType="dollar-circle" class="stat-icon icon-budget"></span>
            </div>
            <div class="stat-value">AED {{ (kpis()?.totalBudget ?? 0) | number: '1.0-0' }}</div>
            <div class="stat-sub">across all projects</div>
          </nz-card>
        </div>
        <div nz-col [nzXs]="24" [nzSm]="12" [nzMd]="6">
          <nz-card class="stat-card">
            <div class="stat-header">
              <span class="stat-label">Unbilled Work</span>
              <span nz-icon nzType="clock-circle" class="stat-icon icon-hours"></span>
            </div>
            <div class="stat-value">AED {{ (kpis()?.unbilledAmount ?? 0) | number: '1.0-0' }}</div>
            <div class="stat-sub">{{ (kpis()?.totalLoggedHours ?? 0) | number: '1.1-1' }}h logged total</div>
          </nz-card>
        </div>
      </div>

      <!-- Main Paged Table -->
      <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search project code, name, or customer..." emptyText="No projects found." (rowClick)="openDetail($event)">
        <ng-template #cell let-row let-column="column" let-value="value">
          @switch (column.key) {
            @case ('number') {
              <span class="code-pill">{{ row.number }}</span>
            }
            @case ('name') {
              <div class="project-name-cell">
                <a [routerLink]="['/projects', row.id]" class="project-link" (click)="$event.stopPropagation()">{{ row.name }}</a>
                @if (row.customerName) {
                  <span class="project-customer">{{ row.customerName }}</span>
                }
              </div>
            }
            @case ('status') {
              <nz-tag [nzColor]="statusColor(row.status)">{{ row.status }}</nz-tag>
            }
            @case ('billingType') {
              <span class="billing-type-badge">{{ formatBillingType(row.billingType) }}</span>
            }
            @case ('budgetAmount') {
              <span class="budget-cell">AED {{ row.budgetAmount | number: '1.2-2' }}</span>
            }
            @case ('dates') {
              <span class="dates-cell">{{ row.startDate || '—' }} to {{ row.endDate || '—' }}</span>
            }
            @case ('actions') {
              <div class="actions-cell" (click)="$event.stopPropagation()">
                <button nz-button nzType="text" nzSize="small" [routerLink]="['/projects', row.id]" nzTooltipTitle="View Project Details">
                  <span nz-icon nzType="eye"></span>
                </button>
                <button *erpHasPermission="'projects.projects.manage'" nz-button nzType="text" nzSize="small" (click)="state.edit(row)" nzTooltipTitle="Edit Project">
                  <span nz-icon nzType="edit"></span>
                </button>
              </div>
            }
            @default {
              {{ value }}
            }
          }
        </ng-template>
      </erp-paged-table>

      <erp-record-drawer [title]="state.editing() ? 'Edit Project' : 'New Project'" [open]="state.open()" [fields]="fields()" [value]="$any(state.editing())" customFieldEntityType="project"
                         [saving]="state.saving()" [errors]="state.errors()" testId="project-form" (submitted)="state.submit($event)" (closed)="state.close()" />
    </div>
  `,
  styles: [`
    .projects-container {
      display: flex;
      flex-direction: column;
      gap: 20px;
    }
    .page-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      flex-wrap: wrap;
      gap: 16px;
    }
    .erp-page-title {
      font-size: 24px;
      font-weight: 700;
      color: #0f172a;
      margin: 0;
    }
    .erp-page-subtitle {
      font-size: 13.5px;
      color: #64748b;
      margin: 4px 0 0 0;
    }
    .kpi-row {
      margin-bottom: 4px;
    }
    .stat-card {
      border-radius: 12px;
      border: 1px solid #e2e8f0;
      box-shadow: 0 1px 3px rgba(0,0,0,0.04);
      transition: all 0.2s;
    }
    .stat-card:hover {
      box-shadow: 0 4px 12px rgba(0,0,0,0.06);
      transform: translateY(-1px);
    }
    .stat-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 8px;
    }
    .stat-label {
      font-size: 13px;
      font-weight: 600;
      color: #64748b;
    }
    .stat-icon {
      font-size: 18px;
      padding: 6px;
      border-radius: 8px;
    }
    .icon-active { color: #10955d; background: #e6f7ef; }
    .icon-completed { color: #2563eb; background: #eff6ff; }
    .icon-budget { color: #d97706; background: #fef3c7; }
    .icon-hours { color: #7c3aed; background: #f5f3ff; }
    .stat-value {
      font-size: 22px;
      font-weight: 700;
      color: #0f172a;
      line-height: 1.2;
    }
    .stat-sub {
      font-size: 12px;
      color: #94a3b8;
      margin-top: 4px;
    }
    .project-name-cell {
      display: flex;
      flex-direction: column;
    }
    .project-link {
      font-weight: 600;
      color: #0f172a;
      text-decoration: none;
    }
    .project-link:hover {
      color: #10955d;
      text-decoration: underline;
    }
    .project-customer {
      font-size: 12px;
      color: #64748b;
    }
    .budget-cell {
      font-weight: 600;
      color: #0f172a;
    }
    .dates-cell {
      font-size: 12.5px;
      color: #475569;
    }
    .billing-type-badge {
      font-size: 12px;
      padding: 2px 8px;
      background: #f1f5f9;
      border-radius: 6px;
      color: #334155;
    }
    .actions-cell {
      display: flex;
      gap: 4px;
    }
  `],
})
export class ProjectsListPage {
  private readonly api = inject(ProjectsApi);
  private readonly router = inject(Router);
  private readonly entities = toSignal(inject(LegalEntitiesApi).list({ page: 1, pageSize: 100 }).pipe(catchError(() => of({ items: [] }))), { initialValue: { items: [] } });
  private readonly parties = toSignal(inject(PartiesApi).list({ page: 1, pageSize: 200, extra: { type: 'customer' } }).pipe(catchError(() => of({ items: [] as PartyDto[] }))), { initialValue: { items: [] as PartyDto[] } });

  readonly kpis = toSignal(this.api.getKpis().pipe(catchError(() => of(null))), { initialValue: null });
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'number' });

  readonly state = new MasterDataState<ProjectSummaryDto>({
    save: (id, body: any) => (id ? this.api.update(id, body as ProjectInput) : this.api.create(body as ProjectInput)),
    reload: () => {
      this.list.reload();
      this.reloadKpis();
    },
    message: inject(NzMessageService),
    entityName: 'Project',
  });

  readonly columns: TableColumn<ProjectSummaryDto>[] = [
    { key: 'number', label: 'Code', sortField: 'number', width: '130px' },
    { key: 'name', label: 'Project Name', sortField: 'name' },
    { key: 'status', label: 'Status', sortField: 'status', width: '120px' },
    { key: 'billingType', label: 'Billing Type', width: '160px' },
    { key: 'budgetAmount', label: 'Budget', sortField: 'budgetAmount', width: '150px' },
    { key: 'dates', label: 'Timeline', width: '190px' },
    { key: 'actions', label: '', width: '90px' },
  ];

  readonly fields = computed<FieldSpec[]>(() => [
    { key: 'name', label: 'Project Name', type: 'text', required: true, maxLength: 200 },
    { key: 'number', label: 'Project Code (leave blank for auto-generate)', type: 'text', maxLength: 30 },
    {
      key: 'legalEntityId',
      label: 'Legal Entity',
      type: 'select',
      required: true,
      options: this.entities().items.map((e) => ({ value: e.id, label: `${e.code} · ${e.name}` })),
    },
    {
      key: 'customerId',
      label: 'Client / Customer',
      type: 'select',
      options: this.parties().items.map((p) => ({ value: p.id, label: p.name })),
    },
    { key: 'managerName', label: 'Project Manager', type: 'text', maxLength: 150 },
    {
      key: 'status',
      label: 'Status',
      type: 'select',
      required: true,
      options: [
        { value: 'Planning', label: 'Planning' },
        { value: 'Active', label: 'Active' },
        { value: 'OnHold', label: 'On Hold' },
        { value: 'Completed', label: 'Completed' },
        { value: 'Cancelled', label: 'Cancelled' },
      ],
      default: 'Planning',
    },
    {
      key: 'billingType',
      label: 'Billing Model',
      type: 'select',
      required: true,
      options: [
        { value: 'TimeAndMaterials', label: 'Time & Materials' },
        { value: 'FixedFee', label: 'Fixed Fee (Milestone-based)' },
        { value: 'NonBillable', label: 'Non-Billable (Internal)' },
      ],
      default: 'TimeAndMaterials',
    },
    { key: 'budgetAmount', label: 'Total Budget (AED)', type: 'number', min: 0, default: 0 },
    { key: 'startDate', label: 'Start Date', type: 'date' },
    { key: 'endDate', label: 'Target Completion Date', type: 'date' },
    { key: 'description', label: 'Description & Scope', type: 'textarea', maxLength: 2000 },
  ]);

  statusColor(status: ProjectStatus): string {
    switch (status) {
      case 'Active': return 'green';
      case 'Planning': return 'blue';
      case 'OnHold': return 'warning';
      case 'Completed': return 'purple';
      case 'Cancelled': return 'error';
      default: return 'default';
    }
  }

  formatBillingType(type: ProjectBillingType): string {
    switch (type) {
      case 'TimeAndMaterials': return 'Time & Materials';
      case 'FixedFee': return 'Fixed Fee';
      case 'NonBillable': return 'Non-Billable';
      default: return type;
    }
  }

  openDetail(row: ProjectSummaryDto): void {
    void this.router.navigate(['/projects', row.id]);
  }

  private reloadKpis(): void {
    this.api.getKpis().subscribe((k) => {
      // Updates KPI state
    });
  }
}
