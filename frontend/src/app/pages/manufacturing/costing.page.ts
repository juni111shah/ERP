import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzBadgeModule } from 'ng-zorro-antd/badge';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzStatisticModule } from 'ng-zorro-antd/statistic';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { SessionStore } from '../../core/session/session.store';
import {
  BomSummaryDto,
  CostRollupDto,
  ManufacturingBomsApi,
  ManufacturingCostingApi,
  VarianceAnalysisDto
} from '../../core/api/manufacturing.api';

@Component({
  selector: 'erp-manufacturing-costing-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzCardModule,
    NzTabsModule,
    NzButtonModule,
    NzIconModule,
    NzSelectModule,
    NzTableModule,
    NzTagModule,
    NzBadgeModule,
    NzDividerModule,
    NzGridModule,
    NzStatisticModule
  ],
  template: `
    <div class="page-container">
      <div class="page-header">
        <div>
          <h2>Product Costing & Variance Analysis</h2>
          <p class="text-secondary">Perform standard BOM cost rollups, inspect raw material breakdowns, and analyze shop-floor production variances</p>
        </div>
      </div>

      <nz-tabset>
        <!-- Standard Cost Rollup Tab -->
        <nz-tab nzTitle="Standard BOM Cost Rollup">
          <nz-card [nzBordered]="false" class="mb-4">
            <div class="rollup-selector">
              <label class="font-medium mr-3">Select BOM Recipe:</label>
              <nz-select
                [(ngModel)]="selectedBomId"
                (ngModelChange)="onSelectBom($event)"
                nzPlaceHolder="Choose BOM recipe..."
                class="bom-dropdown">
                <nz-option *ngFor="let b of availableBoms()" [nzValue]="b.id" [nzLabel]="b.code + ' - ' + b.name"></nz-option>
              </nz-select>
              <button nz-button nzType="primary" (click)="calculateRollup()" [nzLoading]="isCalculating()">
                <span nz-icon nzType="calculator"></span> Rollup Cost
              </button>
            </div>
          </nz-card>

          <div *ngIf="rollupData() as rollup" class="rollup-results">
            <div nz-row [nzGutter]="16" class="mb-4">
              <div nz-col [nzSpan]="6">
                <nz-card [nzBordered]="false" class="stat-card">
                  <nz-statistic [nzTitle]="'Raw Material Cost'" [nzValue]="(rollup.componentCost | number:'1.2-2') || '0.00'" [nzPrefix]="currency()"></nz-statistic>
                </nz-card>
              </div>
              <div nz-col [nzSpan]="6">
                <nz-card [nzBordered]="false" class="stat-card">
                  <nz-statistic [nzTitle]="'Labor & Overhead'" [nzValue]="(rollup.laborAndOverheadCost | number:'1.2-2') || '0.00'" [nzPrefix]="currency()"></nz-statistic>
                </nz-card>
              </div>
              <div nz-col [nzSpan]="6">
                <nz-card [nzBordered]="false" class="stat-card">
                  <nz-statistic [nzTitle]="'Total Batch Standard Cost'" [nzValue]="(rollup.totalStandardCost | number:'1.2-2') || '0.00'" [nzPrefix]="currency()"></nz-statistic>
                </nz-card>
              </div>
              <div nz-col [nzSpan]="6">
                <nz-card [nzBordered]="false" class="stat-card highlight">
                  <nz-statistic [nzTitle]="'Calculated Cost / Unit'" [nzValue]="(rollup.calculatedStandardUnitCost | number:'1.2-2') || '0.00'" [nzPrefix]="currency()"></nz-statistic>
                </nz-card>
              </div>
            </div>

            <nz-card [nzBordered]="false" nzTitle="Cost Rollup Component Breakdown">
              <nz-table #breakTable [nzData]="rollup.breakdown" [nzShowPagination]="false" nzSize="middle">
                <thead>
                  <tr>
                    <th>Component Item</th>
                    <th>Qty Required</th>
                    <th>Unit Cost</th>
                    <th>Line Standard Cost</th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let item of breakTable.data">
                    <td>
                      <div><strong>{{ item.componentItemName }}</strong></div>
                      <small class="text-secondary">{{ item.componentItemCode }}</small>
                    </td>
                    <td>{{ item.quantityPerUnit }} {{ item.unitOfMeasure }}</td>
                    <td>{{ currency() }} {{ item.unitCost | number:'1.2-2' }}</td>
                    <td><strong>{{ currency() }} {{ item.extendedCost | number:'1.2-2' }}</strong></td>
                  </tr>
                </tbody>
              </nz-table>
            </nz-card>
          </div>
        </nz-tab>

        <!-- Variance Analysis Tab -->
        <nz-tab nzTitle="Production Cost Variances">
          <nz-card [nzBordered]="false">
            <nz-table #varTable [nzData]="variances()" [nzShowPagination]="false" nzSize="middle">
              <thead>
                <tr>
                  <th>Work Order #</th>
                  <th>Produced Item</th>
                  <th>Completed Qty</th>
                  <th>Scrap Qty</th>
                  <th>Planned Cost</th>
                  <th>Actual Cost</th>
                  <th>Variance ({{ currency() }})</th>
                  <th>Variance (%)</th>
                  <th>Evaluation</th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let row of varTable.data">
                  <td><strong>{{ row.orderNumber }}</strong></td>
                  <td>{{ row.outputItemName }} ({{ row.outputItemCode }})</td>
                  <td>{{ row.completedQuantity }}</td>
                  <td>
                    <span [class.text-danger]="row.scrappedQuantity > 0">{{ row.scrappedQuantity }}</span>
                  </td>
                  <td>{{ currency() }} {{ row.totalPlannedCost | number:'1.2-2' }}</td>
                  <td>{{ currency() }} {{ row.actualTotalCost | number:'1.2-2' }}</td>
                  <td>
                    <span [class.text-success]="row.isFavorable" [class.text-danger]="!row.isFavorable">
                      {{ row.isFavorable ? '-' : '+' }}{{ currency() }} {{ (row.varianceAmount < 0 ? -row.varianceAmount : row.varianceAmount) | number:'1.2-2' }}
                    </span>
                  </td>
                  <td>{{ row.variancePercent | number:'1.1-1' }}%</td>
                  <td>
                    <nz-tag [nzColor]="row.isFavorable ? 'success' : 'error'">
                      {{ row.isFavorable ? 'Favorable' : 'Unfavorable' }}
                    </nz-tag>
                  </td>
                </tr>
                <tr *ngIf="variances().length === 0">
                  <td colspan="9" class="text-center py-4 text-secondary">No closed production work orders found for variance analysis.</td>
                </tr>
              </tbody>
            </nz-table>
          </nz-card>
        </nz-tab>
      </nz-tabset>
    </div>
  `,
  styles: [`
    .page-container { padding: 1.5rem; }
    .page-header { margin-bottom: 1.5rem; }
    .page-header h2 { margin: 0; font-size: 1.5rem; font-weight: 600; }
    .text-secondary { color: var(--erp-text-secondary, #64748b); }
    .text-success { color: #10b981; }
    .text-danger { color: #ef4444; }
    .rollup-selector { display: flex; align-items: center; gap: 0.75rem; }
    .bom-dropdown { width: 340px; }
    .stat-card { background: var(--erp-bg-secondary, #f8fafc); border-radius: 8px; }
    .stat-card.highlight { border: 1px solid var(--erp-primary, #0ea5e9); background: rgba(14, 165, 233, 0.05); }
    .mr-3 { margin-right: 0.75rem; }
    .mb-4 { margin-bottom: 1rem; }
    .text-center { text-align: center; }
    .py-4 { padding-top: 1rem; padding-bottom: 1rem; }
  `]
})
export class ManufacturingCostingPageComponent implements OnInit {
  private readonly bomsApi = inject(ManufacturingBomsApi);
  private readonly costingApi = inject(ManufacturingCostingApi);
  private readonly sessionStore = inject(SessionStore);
  private readonly message = inject(NzMessageService);

  readonly currency = computed(() => this.sessionStore.me()?.workspace?.baseCurrency || '$');
  readonly availableBoms = signal<BomSummaryDto[]>([]);
  readonly rollupData = signal<CostRollupDto | null>(null);
  readonly variances = signal<VarianceAnalysisDto[]>([]);
  readonly isCalculating = signal(false);

  selectedBomId = '';

  ngOnInit(): void {
    this.loadBoms();
    this.loadVariances();
  }

  private loadBoms(): void {
    this.bomsApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: res => {
        if (res.items) {
          this.availableBoms.set(res.items);
          if (res.items.length > 0) {
            this.selectedBomId = res.items[0].id;
            this.calculateRollup();
          }
        }
      }
    });
  }

  private loadVariances(): void {
    this.costingApi.variances().subscribe({
      next: res => {
        if (res.data) {
          this.variances.set(res.data);
        }
      }
    });
  }

  onSelectBom(id: string): void {
    this.selectedBomId = id;
    this.calculateRollup();
  }

  calculateRollup(): void {
    if (!this.selectedBomId) return;
    this.isCalculating.set(true);
    this.costingApi.rollup(this.selectedBomId).subscribe({
      next: res => {
        this.rollupData.set(res.data);
        this.isCalculating.set(false);
      },
      error: () => {
        this.message.error('Failed to calculate BOM cost rollup');
        this.isCalculating.set(false);
      }
    });
  }
}
