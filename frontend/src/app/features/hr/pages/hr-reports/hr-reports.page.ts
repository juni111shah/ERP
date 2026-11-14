import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { GratuityReport, HrOccasions, HrReportsApi, HrSummaryReport } from '@core/api/hr-people.api';
import { toUtcIsoDate } from '@core/format/dates';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { SessionStore } from '@core/session/session.store';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { monthName } from '../../hr-format';

/** HR reports: headcount and mix, joiners and leavers, payroll cost, leave and absence, birthdays and anniversaries, end-of-service liability. */
@Component({
  selector: 'erp-hr-reports',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, DecimalPipe, NzSkeletonModule, NzTableModule, NzTabsModule, StatCardComponent, StatePanelComponent],
  styleUrl: './hr-reports.page.scss',
  templateUrl: './hr-reports.page.html',
})
export class HrReportsPage {
  private readonly reportsApi = inject(HrReportsApi);
  readonly summary = signal<HrSummaryReport | null>(null);
  readonly occasions = signal<HrOccasions | null>(null);
  readonly gratuity = signal<GratuityReport | null>(null);
  readonly summaryFailed = signal(false);
  readonly occasionsFailed = signal(false);
  readonly gratuityFailed = signal(false);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  private readonly session = inject(SessionStore);
  /** The end-of-service liability report needs finance.reports.read; without it the tab is not shown. */
  readonly canSeeLiability = computed(() => this.session.hasPermission('finance.reports.read'));

  constructor() {
    this.loadSummary();
    this.loadOccasions();
    effect(() => { if (this.canSeeLiability()) untracked(() => this.loadGratuity()); });
  }

  loadGratuity(): void { this.gratuityFailed.set(false); this.reportsApi.gratuity(toUtcIsoDate(new Date())).subscribe({ next: (g) => this.gratuity.set(g), error: () => this.gratuityFailed.set(true) }); }

  loadSummary(): void { this.summaryFailed.set(false); this.reportsApi.summary().subscribe({ next: (s) => this.summary.set(s), error: () => this.summaryFailed.set(true) }); }
  loadOccasions(): void { this.occasionsFailed.set(false); this.reportsApi.occasions(30).subscribe({ next: (o) => this.occasions.set(o), error: () => this.occasionsFailed.set(true) }); }
  month(m: number): string { return monthName(m); }
}
