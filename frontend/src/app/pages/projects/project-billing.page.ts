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
import { FormsModule } from '@angular/forms';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { ProjectBillingApi, ProjectBillingSummaryDto, ProjectTimesheetsApi } from '../../core/api/projects.api';
import { BillPreviewDto, ProjectExpensesApi, ProjectsExtraApi } from '../../core/api/projects-extra.api';
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
    FormsModule,
    NzInputModule,
    NzInputNumberModule,
    NzSelectModule,
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
              <th>Expenses</th>
              <th>Waiting approval</th>
              <th>Fixed fee left</th>
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
                <td>{{ currency() }} {{ row.unbilledExpensesAmount | number: '1.2-2' }}</td>
                <td>{{ row.awaitingApprovalAmount ? currency() + ' ' + (row.awaitingApprovalAmount | number: '1.2-2') : '' }}</td>
                <td>{{ row.billingType === 'FixedFee' ? currency() + ' ' + (row.fixedFeeRemaining | number: '1.2-2') : '' }}</td>
                <td><b class="total-unbilled-val">{{ currency() }} {{ row.totalUnbilledAmount | number: '1.2-2' }}</b></td>
                <td>
                  <button *erpHasPermission="'projects.billing.manage'" nz-button nzType="primary" nzSize="small" [disabled]="row.totalUnbilledAmount <= 0" (click)="openBill(row)" [attr.data-testid]="'bill-' + row.projectNumber">
                    <span nz-icon nzType="dollar"></span> Bill Work
                  </button>
                </td>
              </tr>
            }
          </tbody>
        </nz-table>
      </nz-card>

      <nz-modal [nzVisible]="billing() !== null" [nzTitle]="'Bill ' + (billing()?.projectName ?? '')" nzWidth="720" (nzOnCancel)="billing.set(null)" [nzFooter]="footer">
        <ng-container *nzModalContent>
          <div style="display:flex;gap:12px;flex-wrap:wrap;margin-bottom:12px" data-testid="bill-options">
            <label>Group the invoice by
              <nz-select [(ngModel)]="grouping" (ngModelChange)="preview()" style="width:170px;display:block" aria-label="Group by">
                <nz-option nzValue="detailed" nzLabel="Each entry"></nz-option>
                <nz-option nzValue="person" nzLabel="Person"></nz-option>
                <nz-option nzValue="task" nzLabel="Task"></nz-option>
                <nz-option nzValue="project" nzLabel="One line"></nz-option>
              </nz-select>
            </label>
            <label>Discount %
              <nz-input-number [(ngModel)]="discount" [nzMin]="0" [nzMax]="100" (ngModelChange)="preview()" style="display:block" aria-label="Discount percent"></nz-input-number>
            </label>
            @if (billing()?.billingType === 'FixedFee') {
              <label>Bill a share of the fixed fee (%)
                <nz-input-number [(ngModel)]="progress" [nzMin]="0" [nzMax]="100" (ngModelChange)="preview()" style="display:block" aria-label="Progress percent" data-testid="bill-progress"></nz-input-number>
              </label>
            }
            <label style="flex:1">Note on the invoice
              <input nz-input [(ngModel)]="notes" maxlength="500" aria-label="Invoice note" />
            </label>
          </div>
          @if (previewError()) { <p style="color:#cf1322">{{ previewError() }}</p> }
          @if (previewData(); as pv) {
            <nz-table [nzData]="pv.lines" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="bill-preview">
              <thead><tr><th>Line</th><th style="text-align:right">Quantity</th><th style="text-align:right">Price</th><th style="text-align:right">Amount</th></tr></thead>
              <tbody>
                @for (l of pv.lines; track $index) { <tr><td>{{ l.description }}</td><td style="text-align:right">{{ l.quantity | number: '1.0-2' }}</td><td style="text-align:right">{{ l.price | number: '1.2-2' }}</td><td style="text-align:right">{{ l.amount | number: '1.2-2' }}</td></tr> }
              </tbody>
            </nz-table>
            <p style="text-align:right;margin-top:8px">Subtotal {{ currency() }} {{ pv.subtotal | number: '1.2-2' }}
              @if (pv.discount > 0) { · discount {{ pv.discount | number: '1.2-2' }} }
              · <b>Total before tax {{ currency() }} {{ pv.total | number: '1.2-2' }}</b></p>
          }
        </ng-container>
      </nz-modal>
      <ng-template #footer>
        <button nz-button (click)="writeOff()" nzDanger [disabled]="!previewData()" data-testid="bill-writeoff" title="Do not charge for the unbilled time and expenses of this project">Write off unbilled time</button>
        <button nz-button (click)="billing.set(null)">Cancel</button>
        <button nz-button nzType="primary" (click)="generate()" [disabled]="!previewData()" [nzLoading]="busy()" data-testid="bill-generate">Generate invoice</button>
      </ng-template>
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

  private readonly extra = inject(ProjectsExtraApi);
  readonly billing = signal<ProjectBillingSummaryDto | null>(null);
  readonly previewData = signal<BillPreviewDto | null>(null);
  readonly previewError = signal<string | null>(null);
  readonly busy = signal(false);
  grouping = 'detailed';
  discount = 0;
  progress: number | null = null;
  notes = '';

  openBill(row: ProjectBillingSummaryDto): void {
    this.grouping = 'detailed'; this.discount = 0; this.progress = null; this.notes = '';
    this.billing.set(row);
    this.preview();
  }

  private input() {
    return { projectId: this.billing()!.projectId, grouping: this.grouping, discountPercent: this.discount || 0, progressPercent: this.progress || null, notes: this.notes || null };
  }

  preview(): void {
    if (!this.billing()) return;
    this.previewError.set(null);
    this.extra.previewBill(this.input()).subscribe({
      next: (r) => this.previewData.set(r),
      error: (e) => { this.previewData.set(null); this.previewError.set(e?.error?.detail ?? e?.error?.message ?? 'Nothing to bill.'); },
    });
  }

  generate(): void {
    const row = this.billing(); if (!row) return;
    this.busy.set(true);
    this.extra.bill(this.input()).subscribe({
      next: (res) => {
        this.busy.set(false); this.billing.set(null);
        this.msg.success(res.invoiceNumber ? `Generated invoice ${res.invoiceNumber} (${this.currency()} ${res.totalAmountBilled.toFixed(2)}) for ${row.projectName}` : 'Billed');
        this.loadOverview();
      },
      error: (e) => { this.busy.set(false); this.msg.error(e?.error?.detail ?? e?.error?.message ?? 'Could not generate the invoice.'); },
    });
  }

  writeOff(): void {
    const row = this.billing(); if (!row) return;
    this.modal.confirm({
      nzTitle: `Write off the unbilled work of ${row.projectName}?`,
      nzContent: 'The unbilled time and expenses will not be charged. This cannot be undone from here.',
      nzOkText: 'Write off', nzOkDanger: true,
      nzOnOk: () => {
        const filters = [{ field: 'projectId', op: 'eq' as const, value: row.projectId }, { field: 'isBilled', op: 'eq' as const, value: false }];
        this.timeApi.list({ page: 1, pageSize: 500, filters }).subscribe((t) => {
          const ids = t.items.filter((x) => x.isBillable && !x.writtenOff).map((x) => x.id);
          this.costApi.list({ page: 1, pageSize: 500, filters }).subscribe((e) => {
            const costIds = e.items.filter((x) => x.isBillable && !x.writtenOff).map((x) => x.id);
            const fail = (er: { error?: { detail?: string } }) => this.msg.error(er?.error?.detail ?? 'Could not write off.');
            const done = () => { this.msg.success('Unbilled work written off'); this.billing.set(null); this.loadOverview(); };
            const writeCosts = () => (costIds.length ? this.costApi.writeOff(costIds).subscribe({ next: done, error: fail }) : done());
            if (ids.length) this.extra.writeOffTime(ids).subscribe({ next: writeCosts, error: fail }); else writeCosts();
          });
        });
      },
    });
  }

  private readonly timeApi = inject(ProjectTimesheetsApi);
  private readonly costApi = inject(ProjectExpensesApi);

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
