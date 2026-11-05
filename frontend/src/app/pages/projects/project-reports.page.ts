import { CommonModule, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { AgeingRow, BudgetRow, HoursRow, ProfitabilityRow, ProjectsExtraApi, UtilisationRow } from '../../core/api/projects-extra.api';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';

const HEALTH: Record<string, { label: string; color: string }> = {
  onTrack: { label: 'On track', color: 'green' }, atRisk: { label: 'At risk', color: 'orange' }, offTrack: { label: 'Off track', color: 'red' }, closed: { label: 'Closed', color: 'default' },
};

/** Project reports: profit, utilisation, budget and work in progress, unbilled ageing, hours. */
@Component({
  selector: 'erp-project-reports',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, DecimalPipe, FormsModule, NzButtonModule, NzDatePickerModule, NzSelectModule, NzTableModule, NzTabsModule, NzTagModule],
  template: `
    <h1 class="erp-page-title">Project reports</h1>
    <div class="filters">
      <nz-range-picker [(ngModel)]="range" aria-label="Period" data-testid="report-range"></nz-range-picker>
      <button nz-button nzType="primary" (click)="loadAll()" data-testid="report-run">Run</button>
    </div>
    <nz-tabs>
      <nz-tab nzTitle="Profitability">
        <nz-table [nzData]="profit()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="rep-profit">
          <thead><tr><th>Project</th><th>Hours</th><th class="n">Billed</th><th class="n">Unbilled</th><th class="n">Labour cost</th><th class="n">Expenses</th><th class="n">Margin</th><th class="n">Margin %</th></tr></thead>
          <tbody>
            @for (r of profit(); track r.projectId) {
              <tr><td>{{ r.number }} · {{ r.name }}</td><td>{{ r.hours | number: '1.1-1' }}</td><td class="n">{{ r.billed | number: '1.0-0' }}</td><td class="n">{{ r.unbilled | number: '1.0-0' }}</td><td class="n">{{ r.labourCost | number: '1.0-0' }}</td><td class="n">{{ r.expenseCost | number: '1.0-0' }}</td>
                <td class="n" [class.neg]="r.margin < 0">{{ r.margin | number: '1.0-0' }}</td><td class="n" [class.neg]="r.margin < 0">{{ r.marginPercent | number: '1.0-1' }}%</td></tr>
            } @empty { <tr><td colspan="8">No projects.</td></tr> }
          </tbody>
        </nz-table>
      </nz-tab>
      <nz-tab nzTitle="Utilisation">
        <nz-table [nzData]="util()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="rep-util">
          <thead><tr><th>Person</th><th class="n">Logged</th><th class="n">Billable</th><th class="n">Capacity</th><th class="n">Planned</th><th class="n">Utilisation</th><th class="n">Billable share</th></tr></thead>
          <tbody>
            @for (r of util(); track r.person) {
              <tr><td>{{ r.person }}</td><td class="n">{{ r.loggedHours | number: '1.1-1' }}h</td><td class="n">{{ r.billableHours | number: '1.1-1' }}h</td><td class="n">{{ r.capacityHours | number: '1.0-0' }}h</td><td class="n">{{ r.allocatedHours | number: '1.0-0' }}h</td><td class="n">{{ r.utilisationPercent | number: '1.0-0' }}%</td><td class="n">{{ r.billablePercent | number: '1.0-0' }}%</td></tr>
            } @empty { <tr><td colspan="7">No time logged in this period.</td></tr> }
          </tbody>
        </nz-table>
      </nz-tab>
      <nz-tab nzTitle="Budget and work in progress">
        <nz-table [nzData]="budget()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="rep-budget">
          <thead><tr><th>Project</th><th>Health</th><th class="n">Budget</th><th class="n">Cost so far</th><th class="n">Used</th><th class="n">Done</th><th class="n">Forecast cost</th><th class="n">Earned</th><th class="n">Billed</th><th class="n">WIP</th></tr></thead>
          <tbody>
            @for (r of budget(); track r.projectId) {
              <tr><td>{{ r.number }} · {{ r.name }}</td><td><nz-tag [nzColor]="health(r.health).color">{{ health(r.health).label }}</nz-tag></td><td class="n">{{ r.budget | number: '1.0-0' }}</td><td class="n">{{ r.cost | number: '1.0-0' }}</td><td class="n" [class.neg]="r.usedPercent > 100">{{ r.usedPercent | number: '1.0-0' }}%</td>
                <td class="n">{{ r.percentComplete | number: '1.0-0' }}%</td><td class="n" [class.neg]="r.budget > 0 && r.forecastVariance < 0">{{ r.forecastCost | number: '1.0-0' }}</td>
                <td class="n">{{ r.billingType === 'FixedFee' ? (r.earnedRevenue | number: '1.0-0') : '' }}</td><td class="n">{{ r.billed | number: '1.0-0' }}</td><td class="n">{{ r.billingType === 'FixedFee' ? (r.workInProgress | number: '1.0-0') : '' }}</td></tr>
            } @empty { <tr><td colspan="10">No projects.</td></tr> }
          </tbody>
        </nz-table>
        <p class="hint">WIP is the revenue earned by the share of work done (fixed-fee projects) minus what has been billed. A positive figure means work done but not yet billed.</p>
      </nz-tab>
      <nz-tab nzTitle="Unbilled ageing">
        <nz-table [nzData]="ageing()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="rep-ageing">
          <thead><tr><th>Project</th><th class="n">0-30 days</th><th class="n">31-60</th><th class="n">61-90</th><th class="n">Over 90</th><th class="n">Total</th></tr></thead>
          <tbody>
            @for (r of ageing(); track r.projectId) {
              <tr><td>{{ r.number }} · {{ r.name }}</td><td class="n">{{ r.current | number: '1.0-0' }}</td><td class="n">{{ r.days31To60 | number: '1.0-0' }}</td><td class="n">{{ r.days61To90 | number: '1.0-0' }}</td><td class="n" [class.neg]="r.over90 > 0">{{ r.over90 | number: '1.0-0' }}</td><td class="n"><b>{{ r.total | number: '1.0-0' }}</b></td></tr>
            } @empty { <tr><td colspan="6">Nothing is waiting to be billed.</td></tr> }
          </tbody>
        </nz-table>
      </nz-tab>
      <nz-tab nzTitle="Hours">
        <div class="filters">
          <nz-select [(ngModel)]="groupBy" (ngModelChange)="loadHours()" style="width:180px" aria-label="Group hours by" data-testid="hours-group">
            <nz-option nzValue="person" nzLabel="By person"></nz-option><nz-option nzValue="project" nzLabel="By project"></nz-option><nz-option nzValue="task" nzLabel="By task"></nz-option>
          </nz-select>
        </div>
        <nz-table [nzData]="hours()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="rep-hours">
          <thead><tr><th>Group</th><th class="n">Hours</th><th class="n">Billable</th><th class="n">Value</th><th class="n">Cost</th></tr></thead>
          <tbody>
            @for (r of hours(); track r.group) {
              <tr><td>{{ r.group }}</td><td class="n">{{ r.hours | number: '1.1-1' }}</td><td class="n">{{ r.billableHours | number: '1.1-1' }}</td><td class="n">{{ r.amount | number: '1.0-0' }}</td><td class="n">{{ r.cost | number: '1.0-0' }}</td></tr>
            } @empty { <tr><td colspan="5">No time logged in this period.</td></tr> }
          </tbody>
        </nz-table>
      </nz-tab>
    </nz-tabs>
    <p class="hint">Amounts are in {{ currency() }}.</p>
  `,
  styles: ['.filters { display:flex; gap:12px; margin-bottom:12px; align-items:center } .n { text-align:right } .neg { color:#cf1322 } .hint { color:#64748b; font-size:12.5px; margin-top:8px }'],
})
export class ProjectReportsPage {
  private readonly api = inject(ProjectsExtraApi);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  range: Date[] = [new Date(new Date().getFullYear(), new Date().getMonth(), 1), new Date()];
  groupBy = 'person';
  readonly profit = signal<ProfitabilityRow[]>([]);
  readonly util = signal<UtilisationRow[]>([]);
  readonly budget = signal<BudgetRow[]>([]);
  readonly ageing = signal<AgeingRow[]>([]);
  readonly hours = signal<HoursRow[]>([]);

  constructor() { this.loadAll(); }

  health(h: string): { label: string; color: string } { return HEALTH[h] ?? HEALTH['closed']; }

  private bounds(): { from: string; to: string } {
    const [a, b] = this.range.length === 2 ? this.range : [new Date(), new Date()];
    return { from: toIsoDate(a), to: toIsoDate(b) };
  }

  loadAll(): void {
    const { from, to } = this.bounds();
    this.api.profitability(from, to).subscribe((r) => this.profit.set(r));
    this.api.utilisation(from, to).subscribe((r) => this.util.set(r));
    this.api.budget().subscribe((r) => this.budget.set(r));
    this.api.ageing(to).subscribe((r) => this.ageing.set(r));
    this.loadHours();
  }

  loadHours(): void {
    const { from, to } = this.bounds();
    this.api.hours(from, to, this.groupBy).subscribe((r) => this.hours.set(r));
  }
}
