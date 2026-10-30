import { CommonModule, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { ProjectBillingApi, ProjectBillingSummaryDto } from '../../core/api/projects.api';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { HasPermissionDirective } from '../../shared/has-permission.directive';

import { StatCardComponent } from '../../shared/stat-card.component';

@Component({
  selector: 'erp-project-billing',
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
    NzTableModule,
    NzModalModule,
    HasPermissionDirective,
    StatCardComponent,
  ],
  template: `
    <!-- Stat KPI Cards -->
    <div class="erp-stat-grid erp-stat-grid--auto mb-4" data-testid="billing-stats-grid">
      <erp-stat-card
        label="Total Unbilled Balance"
        [value]="totalUnbilled()"
        format="money"
        [tone]="0"
        [caption]="'ready to bill across ' + items().length + ' projects'"
      />
      <erp-stat-card
        label="Unbilled Work Hours"
        [value]="totalHoursAmount()"
        format="money"
        [tone]="1"
        [caption]="(totalHours() | number: '1.1-1') + ' billable hours logged'"
      />
      <erp-stat-card
        label="Achieved Milestones"
        [value]="totalMilestonesAmount()"
        format="money"
        tone="success"
        [caption]="totalMilestones() + ' fixed-fee milestones achieved'"
      />
    </div>

    <!-- Main Billing Worksheet Table -->
    <div style="display:flex;justify-content:flex-end;margin-bottom:12px">
      <button nz-button (click)="loadOverview()" [nzLoading]="loading()" data-testid="billing-refresh">
        <span nz-icon nzType="reload"></span> Refresh
      </button>
    </div>
    <nz-card class="table-card">
        <nz-table #bTable [nzData]="items()" [nzLoading]="loading()" nzSize="middle">
          <thead>
            <tr>
              <th>Project Code</th>
              <th>Project Name</th>
              <th>Billing Model</th>
              <th>Unbilled Hours</th>
              <th>Hours Amount</th>
              <th>Achieved Milestones</th>
              <th>Milestones Amount</th>
              <th>Total Unbilled</th>
              <th>Action</th>
            </tr>
          </thead>
          <tbody>
            @for (row of bTable.data; track row.projectId) {
              <tr>
                <td><span class="code-pill">{{ row.projectNumber }}</span></td>
                <td>
                  <a [routerLink]="['/projects', row.projectId]" class="project-link">{{ row.projectName }}</a>
                </td>
                <td><span class="billing-type-badge">{{ formatBillingType(row.billingType) }}</span></td>
                <td>{{ row.unbilledHours | number: '1.1-1' }}h</td>
                <td>{{ currency() }} {{ row.unbilledHoursAmount | number: '1.2-2' }}</td>
                <td>{{ row.achievedMilestonesCount }}</td>
                <td>{{ currency() }} {{ row.achievedMilestonesAmount | number: '1.2-2' }}</td>
                <td><b class="total-unbilled-val">{{ currency() }} {{ row.totalUnbilledAmount | number: '1.2-2' }}</b></td>
                <td>
                  <button *erpHasPermission="'projects.billing.manage'" nz-button nzType="primary" nzSize="small" [disabled]="row.totalUnbilledAmount <= 0" (click)="confirmBill(row)">
                    <span nz-icon nzType="dollar"></span> Bill Work
                  </button>
                </td>
              </tr>
            }
          </tbody>
        </nz-table>
      </nz-card>
  `,
  styles: [`
    .table-card {
      border-radius: 12px;
      border: 1px solid #e2e8f0;
      box-shadow: 0 1px 3px rgba(0,0,0,0.04);
    }
    .stat-label {
      font-size: 13px;
      font-weight: 600;
      color: #64748b;
    }
    .stat-value {
      font-size: 22px;
      font-weight: 700;
      color: #0f172a;
      margin: 6px 0 2px 0;
    }
    .stat-sub {
      font-size: 12px;
      color: #94a3b8;
    }
    .table-card {
      border-radius: 12px;
      border: 1px solid #e2e8f0;
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
    .billing-type-badge {
      font-size: 12px;
      padding: 2px 8px;
      background: #f1f5f9;
      border-radius: 6px;
      color: #334155;
    }
    .total-unbilled-val {
      color: #10955d;
      font-weight: 700;
    }
  `],
})
export class ProjectBillingPage implements OnInit {
  private readonly billingApi = inject(ProjectBillingApi);
  private readonly modal = inject(NzModalService);
  private readonly msg = inject(NzMessageService);
  private readonly currencyService = inject(WorkspaceCurrencyService);

  readonly currency = this.currencyService.currency;
  readonly items = signal<ProjectBillingSummaryDto[]>([]);
  readonly loading = signal(false);

  readonly totalUnbilled = signal(0);
  readonly totalHours = signal(0);
  readonly totalHoursAmount = signal(0);
  readonly totalMilestones = signal(0);
  readonly totalMilestonesAmount = signal(0);

  ngOnInit(): void {
    this.loadOverview();
  }

  loadOverview(): void {
    this.loading.set(true);
    this.billingApi.getOverview().subscribe({
      next: (data) => {
        this.items.set(data);
        this.totalUnbilled.set(data.reduce((acc, x) => acc + x.totalUnbilledAmount, 0));
        this.totalHours.set(data.reduce((acc, x) => acc + x.unbilledHours, 0));
        this.totalHoursAmount.set(data.reduce((acc, x) => acc + x.unbilledHoursAmount, 0));
        this.totalMilestones.set(data.reduce((acc, x) => acc + x.achievedMilestonesCount, 0));
        this.totalMilestonesAmount.set(data.reduce((acc, x) => acc + x.achievedMilestonesAmount, 0));
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  formatBillingType(type: string): string {
    switch (type) {
      case 'TimeAndMaterials': return 'Time & Materials';
      case 'FixedFee': return 'Fixed Fee';
      case 'NonBillable': return 'Non-Billable';
      default: return type;
    }
  }

  confirmBill(row: ProjectBillingSummaryDto): void {
    this.modal.confirm({
      nzTitle: `Bill Work for ${row.projectName}?`,
      nzContent: `This will mark ${row.unbilledHours} hours (${this.currency()} ${row.unbilledHoursAmount.toFixed(2)}) and ${row.achievedMilestonesCount} milestones (${this.currency()} ${row.achievedMilestonesAmount.toFixed(2)}) as billed. Total: ${this.currency()} ${row.totalUnbilledAmount.toFixed(2)}.`,
      nzOkText: 'Generate Billing',
      nzOkType: 'primary',
      nzOnOk: () => {
        this.billingApi.bill({ projectId: row.projectId }).subscribe({
          next: (res) => {
            if (res.invoiceNumber) {
              this.msg.success(`Generated Invoice ${res.invoiceNumber} (${this.currency()} ${res.totalAmountBilled.toFixed(2)}) for ${row.projectName}`);
            } else {
              this.msg.success(`Successfully billed ${this.currency()} ${res.totalAmountBilled.toFixed(2)} for ${row.projectName}`);
            }
            this.loadOverview();
          },
        });
      },
    });
  }
}
