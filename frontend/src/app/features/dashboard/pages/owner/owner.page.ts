import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzTableModule } from 'ng-zorro-antd/table';
import { DashboardChart } from '@core/api/admin.api';
import { OwnerDashboardApi, OwnerDashboardDto, OwnerSection, OwnerTable } from '@core/api/setup.api';
import { displayDate } from '@core/format/display';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { SessionStore } from '@core/session/session.store';
import { ApexChartComponent } from '@shared/components/apex-chart/apex-chart.component';
import { RangeControlComponent } from '@shared/components/range-control/range-control.component';
import { StatCardComponent, StatFormat } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { isEmptySection, isNumericColumn, ownerCellText, ownerStatFormat, sectionChart } from '../../dashboard-owner';
import { DashboardRange, initialRange } from '../../dashboard-range';

/** The owner's view of the business: profit, cash, what is owed, what is due, branches, budget and the business's own measures. */
@Component({
  selector: 'erp-owner',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, NzCardModule, NzSelectModule, NzSkeletonModule, NzTableModule, ApexChartComponent, RangeControlComponent, StatCardComponent, StatePanelComponent],
  styleUrl: './owner.page.scss',
  templateUrl: './owner.page.html',
})
export class OwnerPage {
  private readonly ownerApi = inject(OwnerDashboardApi);
  private readonly session = inject(SessionStore);
  private readonly workspaceCurrency = inject(WorkspaceCurrencyService);
  private readonly route = inject(ActivatedRoute);

  readonly range = signal<DashboardRange>(initialRange(this.route.snapshot.queryParamMap));
  readonly entityId = signal<string | null>(this.session.currentEntityId());
  readonly data = signal<OwnerDashboardDto | null>(null);
  readonly loading = signal(false);
  readonly failed = signal(false);
  readonly entities = computed(() => this.session.me()?.legalEntities ?? []);
  readonly allEntities = computed(() => this.session.me()?.allLegalEntities ?? false);
  /** The currency of the figures: the one the server found for the chosen entity, else the workspace's. */
  readonly currencyCode = computed(() => this.data()?.currency || this.workspaceCurrency.currency());

  constructor() { this.load(); }

  load(): void {
    this.loading.set(true);
    this.failed.set(false);
    const { from, to } = this.range();
    this.ownerApi.get({ from, to, legalEntityId: this.entityId() }).subscribe({
      next: (d) => { this.loading.set(false); this.data.set(d); },
      error: () => { this.loading.set(false); this.failed.set(true); },
    });
  }

  setRange(r: DashboardRange): void { this.range.set(r); this.load(); }
  setEntity(id: string | null): void { this.entityId.set(id); this.load(); }

  date(v: string): string { return displayDate(v); }
  format(f: string): StatFormat { return ownerStatFormat(f); }

  /** A section with no figures, no table rows and no chart values: the page says so instead of showing nothing. */
  isEmpty(s: OwnerSection): boolean { return isEmptySection(s); }
  chartOf(s: OwnerSection): DashboardChart | null { return sectionChart(s); }
  isNumeric(t: OwnerTable, column: number): boolean { return isNumericColumn(t, column); }
  cellText(t: OwnerTable, column: number, cell: string): string { return ownerCellText(t, column, cell); }
}
