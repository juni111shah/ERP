import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzTableModule } from 'ng-zorro-antd/table';
import { LeaveApi, LeaveBalanceRow } from './hr.api';

/** Leave balance per employee and leave type for a year: entitled + carried - taken, with requests still waiting. */
@Component({
  selector: 'erp-leave-balances',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, FormsModule, NzInputNumberModule, NzTableModule],
  template: `
    <h1 class="erp-page-title">Leave balances</h1>
    <div style="display:flex;gap:8px;align-items:center;margin-bottom:12px"><span>Year</span>
      <nz-input-number [ngModel]="year()" (ngModelChange)="setYear($event)" [nzMin]="2000" [nzMax]="2100" [nzPrecision]="0" data-testid="balance-year"></nz-input-number></div>
    <nz-table [nzData]="rows()" nzSize="small" [nzShowPagination]="false" [nzLoading]="loading()" data-testid="balance-table">
      <thead><tr><th>Employee</th><th>Leave type</th><th style="text-align:end">Entitled</th><th style="text-align:end">Carried</th><th style="text-align:end">Taken</th><th style="text-align:end">Pending</th><th style="text-align:end">Remaining</th></tr></thead>
      <tbody>
        @for (r of rows(); track r.employeeId + r.leaveTypeId) {
          <tr><td>{{ r.employeeName }}<div style="font-size:12px;opacity:.7">{{ r.employeeNumber }}</div></td><td>{{ r.leaveTypeName }}</td>
            <td style="text-align:end"><bdi dir="ltr">{{ r.entitled | number: '1.0-2' }}</bdi></td><td style="text-align:end"><bdi dir="ltr">{{ r.carried | number: '1.0-2' }}</bdi></td>
            <td style="text-align:end"><bdi dir="ltr">{{ r.taken | number: '1.0-2' }}</bdi></td><td style="text-align:end"><bdi dir="ltr">{{ r.pending | number: '1.0-2' }}</bdi></td>
            <td style="text-align:end"><b><bdi dir="ltr">{{ r.remaining | number: '1.0-2' }}</bdi></b></td></tr>
        } @empty { <tr><td colspan="7">No leave balances for this year yet.</td></tr> }
      </tbody>
    </nz-table>`,
})
export class LeaveBalancesPage {
  private readonly api = inject(LeaveApi);
  readonly year = signal(new Date().getFullYear());
  readonly rows = signal<LeaveBalanceRow[]>([]);
  readonly loading = signal(false);

  constructor() { this.load(); }

  setYear(y: number | null): void { if (y) { this.year.set(y); this.load(); } }

  private load(): void {
    this.loading.set(true);
    this.api.balances(this.year()).subscribe({ next: (r) => { this.rows.set(r); this.loading.set(false); }, error: () => this.loading.set(false) });
  }
}
