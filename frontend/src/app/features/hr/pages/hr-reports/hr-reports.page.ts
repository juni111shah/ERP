import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { GratuityReport, HrOccasions, HrReportsApi, HrSummaryReport } from '@core/api/hr-people.api';
import { MONTHS } from '@core/api/hr.api';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';

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
  private readonly api = inject(HrReportsApi);
  readonly summary = signal<HrSummaryReport | null>(null);
  readonly occasions = signal<HrOccasions | null>(null);
  readonly gratuity = signal<GratuityReport | null>(null);
  readonly summaryFailed = signal(false);
  readonly occasionsFailed = signal(false);
  readonly gratuityFailed = signal(false);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  constructor() {
    this.loadSummary();
    this.loadOccasions();
    this.api.gratuity(new Date().toISOString().slice(0, 10)).subscribe({ next: (g) => this.gratuity.set(g), error: () => this.gratuityFailed.set(true) });
  }
  loadSummary(): void { this.summaryFailed.set(false); this.api.summary().subscribe({ next: (s) => this.summary.set(s), error: () => this.summaryFailed.set(true) }); }
  loadOccasions(): void { this.occasionsFailed.set(false); this.api.occasions(30).subscribe({ next: (o) => this.occasions.set(o), error: () => this.occasionsFailed.set(true) }); }
  month(m: number): string { return MONTHS[m - 1]; }
}
