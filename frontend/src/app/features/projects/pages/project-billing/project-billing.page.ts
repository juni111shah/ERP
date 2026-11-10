import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal, viewChild } from '@angular/core';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTableModule } from 'ng-zorro-antd/table';
import { CsvExportService } from '@core/services/csv-export.service';
import { ProjectBillingApi, ProjectBillingSummaryDto } from '@core/api/projects.api';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { BillModalComponent } from '../../components/bill-modal/bill-modal.component';
import { errorText, PRJ_STYLES, ProjectTagComponent } from '../../projects-ui';

/** What is ready to bill across all projects. Billing a project opens the invoice builder, where nothing is created until it is confirmed. */
@Component({
  selector: 'erp-project-billing',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, RouterModule, NzCardModule, NzButtonModule, NzIconModule, NzTableModule, HasPermissionDirective, StatCardComponent, BillModalComponent, ProjectTagComponent],
  template: `
    <div class="erp-stat-grid erp-stat-grid--auto mb-4" data-testid="billing-stats-grid">
      <erp-stat-card label="Ready to bill" [value]="total()" format="money" [tone]="0" [caption]="'across ' + billableCount() + (billableCount() === 1 ? ' project' : ' projects')" />
      <erp-stat-card label="Time" [value]="sum('unbilledHoursAmount')" format="money" [tone]="1" [caption]="(sum('unbilledHours') | number: '1.1-1') + ' billable hours'" />
      <erp-stat-card label="Milestones" [value]="sum('achievedMilestonesAmount')" format="money" tone="success" [caption]="sum('achievedMilestonesCount') + ' reached'" />
      <erp-stat-card label="Expenses" [value]="sum('unbilledExpensesAmount')" format="money" [tone]="3" caption="to charge to customers" />
      <erp-stat-card label="Waiting for approval" [value]="sum('awaitingApprovalAmount')" format="money" tone="warning" caption="not billable yet" />
    </div>

    <div class="prj-toolbar">
      <span class="prj-spacer"></span>
      <button nz-button (click)="loadOverview()" [nzLoading]="loading()" data-testid="billing-refresh"><span nz-icon nzType="reload"></span> Refresh</button>
      <button nz-button (click)="export()" [disabled]="!items().length"><span nz-icon nzType="download"></span> Export CSV</button>
    </div>
    <nz-card>
      <div class="prj-table-wrap">
        <nz-table #bTable [nzData]="items()" [nzLoading]="loading()" nzSize="middle" [nzScroll]="{ x: '1180px' }" [nzPageSize]="25">
          <thead>
            <tr>
              <th>Project</th><th nzWidth="130px">Billing</th><th class="prj-num" nzWidth="90px">Hours</th><th class="prj-num" nzWidth="110px">Time</th><th class="prj-num" nzWidth="110px">Milestones</th>
              <th class="prj-num" nzWidth="100px">Expenses</th><th class="prj-num" nzWidth="110px">Awaiting approval</th><th class="prj-num" nzWidth="110px">Fee left</th><th class="prj-num" nzWidth="120px">Ready to bill</th><th nzWidth="110px" nzRight></th>
            </tr>
          </thead>
          <tbody>
            @for (row of bTable.data; track row.projectId) {
              <tr>
                <td><a [routerLink]="['/projects', row.projectId]" [queryParams]="{ tab: 'money' }" class="prj-link">{{ row.projectNumber }} · {{ row.projectName }}</a></td>
                <td><erp-prj-tag kind="billing" [value]="row.billingType" /></td>
                <td class="prj-num">{{ row.unbilledHours | number: '1.1-1' }}</td>
                <td class="prj-num">{{ row.unbilledHoursAmount | number: '1.2-2' }}</td>
                <td class="prj-num">{{ row.achievedMilestonesAmount | number: '1.2-2' }}</td>
                <td class="prj-num">{{ row.unbilledExpensesAmount | number: '1.2-2' }}</td>
                <td class="prj-num">{{ row.awaitingApprovalAmount ? (row.awaitingApprovalAmount | number: '1.2-2') : '' }}</td>
                <td class="prj-num">{{ row.billingType === 'fixedFee' ? (row.fixedFeeRemaining | number: '1.2-2') : '' }}</td>
                <td class="prj-num"><b>{{ row.totalUnbilledAmount | number: '1.2-2' }}</b></td>
                <td nzRight>
                  <button *erpHasPermission="'projects.billing.manage'" nz-button nzType="primary" nzSize="small" [disabled]="row.totalUnbilledAmount <= 0 && row.fixedFeeRemaining <= 0" (click)="bill()?.open(row.projectId, row.projectName)" [attr.data-testid]="'bill-' + row.projectNumber">
                    <span nz-icon nzType="dollar"></span> Bill
                  </button>
                </td>
              </tr>
            }
          </tbody>
        </nz-table>
      </div>
      <p class="prj-muted">Amounts are in {{ currency() }}.</p>
    </nz-card>
    <erp-bill-modal (billed)="loadOverview()" />
  `,
  styles: [PRJ_STYLES],
})
export class ProjectBillingPage implements OnInit {
  private readonly billingApi = inject(ProjectBillingApi);
  private readonly msg = inject(NzMessageService);
  private readonly csv = inject(CsvExportService);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly bill = viewChild(BillModalComponent);
  readonly items = signal<ProjectBillingSummaryDto[]>([]);
  readonly loading = signal(false);
  readonly total = computed(() => this.items().reduce((a, x) => a + x.totalUnbilledAmount, 0));
  readonly billableCount = computed(() => this.items().filter((x) => x.totalUnbilledAmount > 0).length);

  ngOnInit(): void { this.loadOverview(); }

  sum(key: 'unbilledHours' | 'unbilledHoursAmount' | 'achievedMilestonesCount' | 'achievedMilestonesAmount' | 'unbilledExpensesAmount' | 'awaitingApprovalAmount'): number {
    return this.items().reduce((a, x) => a + (x[key] ?? 0), 0);
  }

  loadOverview(): void {
    this.loading.set(true);
    this.billingApi.getOverview().subscribe({
      next: (data) => { this.items.set(data); this.loading.set(false); },
      error: (e) => { this.loading.set(false); this.msg.error(errorText(e, 'Could not load the billing overview.')); },
    });
  }

  export(): void {
    this.csv.exportToCsv(this.items() as unknown as Record<string, unknown>[], 'project-billing', [
      { field: 'projectNumber', header: 'Project' }, { field: 'projectName', header: 'Name' }, { field: 'billingType', header: 'Billing' }, { field: 'unbilledHours', header: 'Hours' }, { field: 'unbilledHoursAmount', header: 'Time' },
      { field: 'achievedMilestonesAmount', header: 'Milestones' }, { field: 'unbilledExpensesAmount', header: 'Expenses' }, { field: 'awaitingApprovalAmount', header: 'Awaiting approval' }, { field: 'fixedFeeRemaining', header: 'Fee left' }, { field: 'totalUnbilledAmount', header: 'Ready to bill' },
    ]);
  }
}
