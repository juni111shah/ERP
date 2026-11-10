import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { of, switchMap } from 'rxjs';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzTableModule } from 'ng-zorro-antd/table';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import {
  BomSummaryDto,
  CostRollupDto,
  CostVarianceDto,
  ManufacturingBomsApi,
  ManufacturingCostingApi,
} from '@core/api/manufacturing.api';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';

@Component({
  selector: 'erp-manufacturing-costing-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, FormsModule, NzCardModule, NzFormModule, NzSelectModule, NzSkeletonModule, NzTableModule, StatCardComponent, StatePanelComponent, StatusTagComponent],
  template: `
    @if (bomsFailed()) {
      <erp-state-panel kind="error" title="The bills of materials could not be loaded" hint="Check your connection and try again." actionLabel="Try again" (action)="loadBoms()" />
    } @else if (bomsLoading()) {
      <nz-skeleton [nzActive]="true"></nz-skeleton>
    } @else if (boms().length === 0) {
      <erp-state-panel title="No bills of materials yet" hint="Add a bill of materials to see what a product should cost and what it really costs." icon="calculator" actionLabel="Go to bills of materials" (action)="goToBoms()" />
    } @else {
      <div class="picker">
        <label for="costing-bom">Bill of materials</label>
        <nz-select nzId="costing-bom" nzShowSearch [ngModel]="selectedBomId()" (ngModelChange)="selectBom($event)" class="picker-select" data-testid="costing-bom">
          @for (b of boms(); track b.id) {
            <nz-option [nzValue]="b.id" [nzLabel]="b.code + ' - ' + b.name + (b.isActive ? '' : ' (inactive)')"></nz-option>
          }
        </nz-select>
      </div>

      @if (costFailed()) {
        <erp-state-panel kind="error" title="The cost of this product could not be worked out" hint="Check your connection and try again." actionLabel="Try again" (action)="loadCost()" />
      } @else if (costLoading() && !rollup()) {
        <nz-skeleton [nzActive]="true"></nz-skeleton>
      } @else if (rollup(); as r) {
        <h3 class="section-title">Standard cost of a batch of {{ r.outputQuantity | number:'1.0-4' }} {{ batchUom() }}</h3>
        <div class="erp-stat-grid erp-stat-grid--auto" data-testid="costing-rollup">
          <erp-stat-card label="Materials" [value]="money(r.materialCost)" [tone]="1" />
          <erp-stat-card label="Labor" [value]="money(r.laborCost)" [tone]="2" />
          <erp-stat-card label="Overhead" [value]="money(r.overheadCost)" [tone]="3" />
          <erp-stat-card label="Standard cost per unit" [value]="money(r.totalStandardUnitCost)" [caption]="r.producedItemName" [tone]="0" />
        </div>

        <nz-card nzTitle="Actual against standard, per unit" class="section" data-testid="costing-variance">
          @if (variance(); as v) {
            @if (v.completedWorkOrdersCount > 0 && v.actualAverageUnitCost > 0) {
              <dl class="facts">
                <div><dt>Standard</dt><dd>{{ money(v.standardUnitCost) }}</dd></div>
                <div><dt>Actual average</dt><dd>{{ money(v.actualAverageUnitCost) }}</dd></div>
                <div>
                  <dt>Difference</dt>
                  <dd>{{ v.varianceAmount > 0 ? '+' : '' }}{{ money(v.varianceAmount) }} ({{ v.variancePercentage > 0 ? '+' : '' }}{{ v.variancePercentage | number:'1.1-1' }}%)</dd>
                </div>
                <div><dt>Result</dt><dd><erp-status [value]="varianceTone()" [label]="varianceLabel()" /></dd></div>
              </dl>
              <p class="note">From {{ v.completedWorkOrdersCount }} completed work {{ v.completedWorkOrdersCount === 1 ? 'order' : 'orders' }} for {{ v.itemName }}. The standard comes from the active bill of materials of this product.</p>
            } @else if (v.completedWorkOrdersCount > 0) {
              <p class="note">{{ v.completedWorkOrdersCount }} completed work {{ v.completedWorkOrdersCount === 1 ? 'order' : 'orders' }} for {{ v.itemName }} have no actual cost recorded, so there is nothing to compare.</p>
            } @else {
              <p class="note">No completed work orders for {{ v.itemName }} yet, so there is no actual cost to compare.</p>
            }
          } @else {
            <p class="note">No completed work orders for this product yet, so there is no actual cost to compare.</p>
          }
        </nz-card>

        <nz-card nzTitle="Components" class="section">
          @if (r.componentBreakdown.length > 0) {
            <nz-table #t [nzData]="r.componentBreakdown" [nzShowPagination]="false" [nzFrontPagination]="false" nzSize="middle">
              <thead>
                <tr>
                  <th>Component</th>
                  <th nzAlign="right">Quantity</th>
                  <th nzAlign="right">Unit cost</th>
                  <th nzAlign="right">Cost with scrap</th>
                  <th nzAlign="right">Share of materials</th>
                </tr>
              </thead>
              <tbody>
                @for (c of t.data; track c.componentItemId) {
                  <tr>
                    <td><strong>{{ c.componentItemName }}</strong><div class="code">{{ c.componentItemCode }}</div></td>
                    <td nzAlign="right" class="num">{{ c.quantityRequired | number:'1.0-4' }}</td>
                    <td nzAlign="right" class="num">{{ money(c.unitCost) }}</td>
                    <td nzAlign="right" class="num">{{ money(c.totalCost) }}</td>
                    <td nzAlign="right" class="num">{{ c.costSharePercentage | number:'1.1-1' }}%</td>
                  </tr>
                }
              </tbody>
            </nz-table>
          } @else {
            <p class="note">This bill of materials has no components yet.</p>
          }
        </nz-card>
      } @else {
        <erp-state-panel title="This bill of materials was not found" hint="It may have been deleted. Choose another one." />
      }
    }
  `,
  styles: [`
    .picker { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; margin-block-end: 16px; }
    .picker label { font-weight: 500; }
    .picker-select { width: 420px; max-width: 100%; }
    .section-title { margin: 0 0 12px; font-size: 14px; font-weight: 600; color: var(--erp-color-text-muted); }
    .section { margin-block-start: 16px; }
    .facts { display: grid; grid-template-columns: repeat(auto-fit, minmax(170px, 1fr)); gap: 16px; margin: 0; }
    .facts dt { color: var(--erp-color-text-muted); font-size: 13px; }
    .facts dd { margin: 2px 0 0; font-size: 16px; font-weight: 600; font-variant-numeric: tabular-nums; }
    .note { margin: 12px 0 0; color: var(--erp-color-text-muted); }
    nz-card .note:first-child { margin-block-start: 0; }
    .code { font-family: var(--erp-font-family-mono); font-size: 12px; color: var(--erp-color-text-muted); }
    .num { font-variant-numeric: tabular-nums; white-space: nowrap; }
  `],
})
export class ManufacturingCostingPageComponent implements OnInit {
  private readonly bomsApi = inject(ManufacturingBomsApi);
  private readonly costingApi = inject(ManufacturingCostingApi);
  private readonly router = inject(Router);
  private readonly decimal = new DecimalPipe('en-US');
  readonly currency = inject(WorkspaceCurrencyService).currency;

  readonly boms = signal<BomSummaryDto[]>([]);
  readonly bomsLoading = signal(true);
  readonly bomsFailed = signal(false);
  readonly selectedBomId = signal<string | null>(null);

  readonly rollup = signal<CostRollupDto | null>(null);
  readonly variance = signal<CostVarianceDto | null>(null);
  readonly costLoading = signal(false);
  readonly costFailed = signal(false);

  private readonly selectedBom = computed(() => this.boms().find((b) => b.id === this.selectedBomId()) ?? null);
  readonly batchUom = computed(() => this.selectedBom()?.uom ?? '');

  /** A dearer actual cost is unfavourable, a cheaper one favourable. */
  readonly varianceLabel = computed(() => {
    const amount = this.variance()?.varianceAmount ?? 0;
    return amount === 0 ? 'On standard' : amount > 0 ? 'Over standard' : 'Under standard';
  });
  readonly varianceTone = computed(() => {
    const amount = this.variance()?.varianceAmount ?? 0;
    return amount === 0 ? 'normal' : amount > 0 ? 'overbudget' : 'ontrack';
  });

  ngOnInit(): void {
    this.loadBoms();
  }

  money(value: number): string {
    return `${this.currency()} ${this.decimal.transform(value, '1.2-2')}`;
  }

  loadBoms(): void {
    this.bomsLoading.set(true);
    this.bomsFailed.set(false);
    this.bomsApi.list({ page: 1, pageSize: 200, sort: 'code' }).subscribe({
      next: (res) => {
        this.boms.set(res.items);
        this.bomsLoading.set(false);
        const first = res.items.find((b) => b.isActive) ?? res.items[0];
        if (first) this.selectBom(first.id);
      },
      error: () => {
        this.bomsFailed.set(true);
        this.bomsLoading.set(false);
      },
    });
  }

  selectBom(id: string): void {
    this.selectedBomId.set(id);
    this.rollup.set(null);
    this.variance.set(null);
    this.loadCost();
  }

  loadCost(): void {
    const bom = this.selectedBom();
    if (!bom) return;
    this.costLoading.set(true);
    this.costFailed.set(false);
    this.costingApi.rollup(bom.id).pipe(
      switchMap((rollup) => {
        if (this.selectedBomId() === bom.id) this.rollup.set(rollup);
        return rollup ? this.costingApi.variance(bom.legalEntityId, rollup.producedItemId) : of(null);
      }),
    ).subscribe({
      next: (variance) => {
        if (this.selectedBomId() !== bom.id) return;
        this.variance.set(variance);
        this.costLoading.set(false);
      },
      error: () => {
        if (this.selectedBomId() !== bom.id) return;
        this.costFailed.set(true);
        this.costLoading.set(false);
      },
    });
  }

  goToBoms(): void {
    void this.router.navigateByUrl('/manufacturing/boms');
  }
}
