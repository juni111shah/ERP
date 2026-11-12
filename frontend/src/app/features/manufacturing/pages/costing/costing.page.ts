import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { of, switchMap } from 'rxjs';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzTableModule } from 'ng-zorro-antd/table';
import { BomSummaryDto, CostRollupDto, CostVarianceDto, ManufacturingBomsApi, ManufacturingCostingApi } from '@core/api/manufacturing.api';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { varianceLabel, varianceTone } from '../../manufacturing-costing';

/** Costing: a BOM's standard cost roll-up and how actual production cost compares with it. */
@Component({
  selector: 'erp-manufacturing-costing-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, FormsModule, NzCardModule, NzFormModule, NzSelectModule, NzSkeletonModule, NzTableModule, StatCardComponent, StatePanelComponent, StatusTagComponent],
  templateUrl: './costing.page.html',
  styleUrl: './costing.page.scss',
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
  readonly varianceLabel = computed(() => varianceLabel(this.variance()?.varianceAmount ?? 0));
  readonly varianceTone = computed(() => varianceTone(this.variance()?.varianceAmount ?? 0));

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
