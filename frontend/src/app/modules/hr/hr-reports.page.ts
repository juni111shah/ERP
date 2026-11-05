import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { GratuityReport, HrOccasions, HrReportsApi, HrSummaryReport } from './hr-people.api';
import { MONTHS } from './hr.api';
import { ErpDatePipe } from '../../shared/display.pipes';
import { StatCardComponent } from '../../shared/stat-card.component';
import { StatePanelComponent } from '../../shared/state-panel.component';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';

/** HR reports: headcount and mix, joiners and leavers, payroll cost, leave and absence, birthdays and anniversaries, end-of-service liability. */
@Component({
  selector: 'erp-hr-reports',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, DecimalPipe, NzSkeletonModule, NzTableModule, NzTabsModule, StatCardComponent, StatePanelComponent],
  styles: [`h3 { margin: 18px 0 8px; } .num { text-align: end; white-space: nowrap; } .muted { color: var(--erp-color-text-secondary); } .sub { font-size: 12px; color: var(--erp-color-text-secondary); }`],
  template: `
    <h1 class="erp-page-title">HR reports</h1>
    <nz-tabs>
      <nz-tab nzTitle="Summary">
        @if (summaryFailed()) {
          <erp-state-panel kind="error" title="The report could not be loaded" hint="Check your connection and try again." actionLabel="Try again" (action)="loadSummary()" />
        } @else if (summary(); as s) {
          <div class="erp-stat-grid erp-stat-grid--auto" data-testid="hr-kpis">
            <erp-stat-card label="Headcount" [value]="s.headcount" format="number" [caption]="'as at ' + (s.to | erpDate)" [tone]="0" />
            <erp-stat-card label="Average service" [value]="(s.averageServiceYears | number: '1.1-1') + ' yrs'" [tone]="1" />
            <erp-stat-card label="Absence days" [value]="s.absenceDays" format="number" [caption]="(s.from | erpDate) + ' to ' + (s.to | erpDate)" [tone]="3" />
          </div>
          <div style="display:flex;gap:24px;flex-wrap:wrap">
            <div style="min-width:240px"><h3>By department</h3>@for (c of s.byDepartment; track c.key) { <div>{{ c.key || 'No department' }}: <b>{{ c.count }}</b></div> }</div>
            <div style="min-width:240px"><h3>By nationality</h3>@for (c of s.byNationality; track c.key) { <div>{{ c.key || 'Not set' }}: <b>{{ c.count }}</b></div> }</div>
            <div style="min-width:240px"><h3>By contract</h3>@for (c of s.byContractType; track c.key) { <div>{{ c.key || 'Not set' }}: <b>{{ c.count }}</b></div> }</div>
          </div>
          <h3>Joiners and leavers</h3>
          <nz-table [nzData]="s.movements" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small">
            <thead><tr><th>Month</th><th class="num">Joiners</th><th class="num">Leavers</th></tr></thead>
            <tbody>@for (m of s.movements; track m.year * 100 + m.month) { <tr><td>{{ month(m.month) }} {{ m.year }}</td><td class="num">{{ m.joiners }}</td><td class="num">{{ m.leavers }}</td></tr> }</tbody>
          </nz-table>
          @if (s.payrollCost) {
            <h3>Payroll cost</h3>
            <nz-table [nzData]="s.payrollCost" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="hr-cost">
              <thead><tr><th>Month</th><th class="num">Runs</th><th class="num">Employees</th><th class="num">Gross ({{ currency() }})</th><th class="num">Deductions ({{ currency() }})</th><th class="num">Net ({{ currency() }})</th></tr></thead>
              <tbody>@for (m of s.payrollCost; track m.year * 100 + m.month) { <tr><td>{{ month(m.month) }} {{ m.year }}</td><td class="num">{{ m.runs }}</td><td class="num">{{ m.employees }}</td><td class="num">{{ m.gross | number: '1.2-2' }}</td><td class="num">{{ m.deductions | number: '1.2-2' }}</td><td class="num">{{ m.net | number: '1.2-2' }}</td></tr> }</tbody>
            </nz-table>
          }
          <h3>Leave taken, by type (days)</h3>
          @for (l of s.leaveDaysByType; track l.leaveType) { <div>{{ l.leaveType }}: <b>{{ l.days }}</b></div> } @empty { <div class="muted">No leave in the period.</div> }
        } @else { <nz-skeleton [nzActive]="true" [nzParagraph]="{ rows: 8 }"></nz-skeleton> }
      </nz-tab>
      <nz-tab nzTitle="Birthdays and anniversaries">
        @if (occasions(); as o) {
          <h3>Birthdays (next {{ o.days }} days)</h3>
          @for (b of o.birthdays; track b.employeeId) { <div>{{ b.employee }}: {{ b.date | erpDate }} ({{ b.daysAway === 0 ? 'today' : 'in ' + b.daysAway + ' days' }})</div> } @empty { <div class="muted">No birthdays in the next {{ o.days }} days.</div> }
          <h3>Work anniversaries</h3>
          @for (a of o.anniversaries; track a.employeeId) { <div>{{ a.employee }}: {{ a.years }} years on {{ a.date | erpDate }}</div> } @empty { <div class="muted">No anniversaries in the next {{ o.days }} days.</div> }
        } @else if (occasionsFailed()) {
          <erp-state-panel kind="error" title="This list could not be loaded" hint="Check your connection and try again." actionLabel="Try again" (action)="loadOccasions()" />
        } @else { <nz-skeleton [nzActive]="true" [nzParagraph]="{ rows: 4 }"></nz-skeleton> }
      </nz-tab>
      <nz-tab nzTitle="End-of-service liability">
        @if (gratuity(); as g) {
          <p>Total liability <b data-testid="gratuity-total">{{ currency() }} {{ g.totalLiability | number: '1.2-2' }}</b> @if (g.rule) { <span class="muted">· {{ g.rule }}</span> }</p>
          <nz-table [nzData]="g.rows" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" nzNoResult="No employees to report">
            <thead><tr><th>Employee</th><th>Joined</th><th class="num">Basic ({{ currency() }})</th><th class="num">Service (yrs)</th><th class="num">Entitlement (days)</th><th class="num">Amount ({{ currency() }})</th></tr></thead>
            <tbody>@for (r of g.rows; track r.employeeId) { <tr><td>{{ r.employee }}<div class="sub">{{ r.number }}</div></td><td>{{ r.joined | erpDate }}</td><td class="num">{{ r.basicSalary | number: '1.2-2' }}</td><td class="num">{{ r.serviceYears }}</td><td class="num">{{ r.eligible ? r.entitlementDays : '—' }}</td><td class="num">{{ r.gratuity | number: '1.2-2' }}</td></tr> }</tbody>
          </nz-table>
        } @else if (gratuityFailed()) {
          <erp-state-panel title="This report is not available" hint="It needs access to pay figures, or the employer's country has no end-of-service rule." icon="lock" />
        } @else { <nz-skeleton [nzActive]="true" [nzParagraph]="{ rows: 4 }"></nz-skeleton> }
      </nz-tab>
    </nz-tabs>`,
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
