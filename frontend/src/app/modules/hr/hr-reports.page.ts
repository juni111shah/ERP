import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { GratuityReport, HrOccasions, HrReportsApi, HrSummaryReport } from './hr-people.api';
import { MONTHS } from './hr.api';

/** HR reports: headcount and mix, joiners and leavers, payroll cost, leave and absence, birthdays and anniversaries, end-of-service liability. */
@Component({
  selector: 'erp-hr-reports',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, FormsModule, NzDatePickerModule, NzTableModule, NzTabsModule],
  styles: [`.kpis { display: flex; gap: 16px; flex-wrap: wrap; margin: 12px 0 20px; } .kpi { min-width: 150px; padding: 12px 16px; border: 1px solid var(--erp-color-border, #e2e8f0); border-radius: 8px; } .kpi b { font-size: 22px; display: block; } h3 { margin: 18px 0 8px; }`],
  template: `
    <h1 class="erp-page-title">HR reports</h1>
    <nz-tabs>
      <nz-tab nzTitle="Summary">
        @if (summary(); as s) {
          <div class="kpis" data-testid="hr-kpis">
            <div class="kpi"><small>Headcount</small><b>{{ s.headcount }}</b></div>
            <div class="kpi"><small>Average service</small><b>{{ s.averageServiceYears | number: '1.1-1' }} yrs</b></div>
            <div class="kpi"><small>Absence days</small><b>{{ s.absenceDays }}</b></div>
            <div class="kpi"><small>Period</small><b style="font-size:14px">{{ s.from }} → {{ s.to }}</b></div>
          </div>
          <div style="display:flex;gap:24px;flex-wrap:wrap">
            <div style="min-width:240px"><h3>By department</h3>@for (c of s.byDepartment; track c.key) { <div>{{ c.key || 'No department' }}: <b>{{ c.count }}</b></div> }</div>
            <div style="min-width:240px"><h3>By nationality</h3>@for (c of s.byNationality; track c.key) { <div>{{ c.key || 'Not set' }}: <b>{{ c.count }}</b></div> }</div>
            <div style="min-width:240px"><h3>By contract</h3>@for (c of s.byContractType; track c.key) { <div>{{ c.key || 'Not set' }}: <b>{{ c.count }}</b></div> }</div>
          </div>
          <h3>Joiners and leavers</h3>
          <nz-table [nzData]="s.movements" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small">
            <thead><tr><th>Month</th><th>Joiners</th><th>Leavers</th></tr></thead>
            <tbody>@for (m of s.movements; track m.year * 100 + m.month) { <tr><td>{{ month(m.month) }} {{ m.year }}</td><td>{{ m.joiners }}</td><td>{{ m.leavers }}</td></tr> }</tbody>
          </nz-table>
          @if (s.payrollCost) {
            <h3>Payroll cost</h3>
            <nz-table [nzData]="s.payrollCost" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="hr-cost">
              <thead><tr><th>Month</th><th>Runs</th><th>Employees</th><th>Gross</th><th>Deductions</th><th>Net</th></tr></thead>
              <tbody>@for (m of s.payrollCost; track m.year * 100 + m.month) { <tr><td>{{ month(m.month) }} {{ m.year }}</td><td>{{ m.runs }}</td><td>{{ m.employees }}</td><td>{{ m.gross | number: '1.2-2' }}</td><td>{{ m.deductions | number: '1.2-2' }}</td><td>{{ m.net | number: '1.2-2' }}</td></tr> }</tbody>
            </nz-table>
          }
          <h3>Leave taken, by type (days)</h3>
          @for (l of s.leaveDaysByType; track l.leaveType) { <div>{{ l.leaveType }}: <b>{{ l.days }}</b></div> } @empty { <div style="color:var(--erp-color-text-secondary)">No leave in the period.</div> }
        }
      </nz-tab>
      <nz-tab nzTitle="Birthdays and anniversaries">
        @if (occasions(); as o) {
          <h3>Birthdays (next {{ o.days }} days)</h3>
          @for (b of o.birthdays; track b.employeeId) { <div>{{ b.employee }} — {{ b.date }} ({{ b.daysAway === 0 ? 'today' : 'in ' + b.daysAway + ' days' }})</div> } @empty { <div style="color:var(--erp-color-text-secondary)">None.</div> }
          <h3>Work anniversaries</h3>
          @for (a of o.anniversaries; track a.employeeId) { <div>{{ a.employee }} — {{ a.years }} years on {{ a.date }}</div> } @empty { <div style="color:var(--erp-color-text-secondary)">None.</div> }
        }
      </nz-tab>
      <nz-tab nzTitle="End-of-service liability">
        @if (gratuity(); as g) {
          <p>Total liability <b data-testid="gratuity-total">{{ g.totalLiability | number: '1.2-2' }}</b> @if (g.rule) { <span style="color:var(--erp-color-text-secondary)">· {{ g.rule }}</span> }</p>
          <nz-table [nzData]="g.rows" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small">
            <thead><tr><th>Employee</th><th>Joined</th><th>Basic</th><th>Service (yrs)</th><th>Entitlement (days)</th><th>Amount</th></tr></thead>
            <tbody>@for (r of g.rows; track r.employeeId) { <tr><td>{{ r.employee }}<div style="font-size:12px;color:var(--erp-color-text-secondary)">{{ r.number }}</div></td><td>{{ r.joined }}</td><td>{{ r.basicSalary | number: '1.2-2' }}</td><td>{{ r.serviceYears }}</td><td>{{ r.eligible ? r.entitlementDays : '—' }}</td><td>{{ r.gratuity | number: '1.2-2' }}</td></tr> }</tbody>
          </nz-table>
        }
      </nz-tab>
    </nz-tabs>`,
})
export class HrReportsPage {
  private readonly api = inject(HrReportsApi);
  readonly summary = signal<HrSummaryReport | null>(null);
  readonly occasions = signal<HrOccasions | null>(null);
  readonly gratuity = signal<GratuityReport | null>(null);
  constructor() {
    this.api.summary().subscribe((s) => this.summary.set(s));
    this.api.occasions(30).subscribe((o) => this.occasions.set(o));
    this.api.gratuity(new Date().toISOString().slice(0, 10)).subscribe({ next: (g) => this.gratuity.set(g), error: () => this.gratuity.set(null) });
  }
  month(m: number): string { return MONTHS[m - 1]; }
}
