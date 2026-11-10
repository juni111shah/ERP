import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzTableModule } from 'ng-zorro-antd/table';
import { LeaveApi, LeaveBalanceRow } from '@core/api/hr.api';

/** Leave balance per employee and leave type for a year: entitled + carried - taken, with requests still waiting. */
@Component({
  selector: 'erp-leave-balances',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, FormsModule, NzInputNumberModule, NzTableModule],
  templateUrl: './leave-balances.page.html',
})
export class LeaveBalancesPage {
  private readonly leaveApi = inject(LeaveApi);
  readonly year = signal(new Date().getFullYear());
  readonly rows = signal<LeaveBalanceRow[]>([]);
  readonly loading = signal(false);

  constructor() { this.load(); }

  setYear(y: number | null): void { if (y) { this.year.set(y); this.load(); } }

  private load(): void {
    this.loading.set(true);
    this.leaveApi.balances(this.year()).subscribe({ next: (r) => { this.rows.set(r); this.loading.set(false); }, error: () => this.loading.set(false) });
  }
}
