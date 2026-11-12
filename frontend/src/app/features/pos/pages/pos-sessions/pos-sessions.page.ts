import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterModule } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { createPagedList } from '@core/api/paged-list';
import { PosSessionSummaryDto, PosSessionsApi, PosShiftReportDto } from '@core/api/pos.api';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { CashDialogShift, PosCashDialogComponent } from '../../components/pos-cash-dialog/pos-cash-dialog.component';
import { PosShiftReportComponent } from '../../components/pos-shift-report/pos-shift-report.component';

/** Every shift with its cash result. A row opens the shift's full report. */
@Component({
  selector: 'erp-pos-sessions',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, StatusTagComponent, DecimalPipe, RouterModule, NzAlertModule, NzButtonModule, NzDrawerModule, NzIconModule, NzSkeletonModule, PagedTableComponent, HasPermissionDirective, PosCashDialogComponent, PosShiftReportComponent],
  templateUrl: './pos-sessions.page.html',
  styleUrl: './pos-sessions.page.scss',
})
export class PosSessionsPage {
  private readonly sessionsApi = inject(PosSessionsApi);
  private readonly currencyService = inject(WorkspaceCurrencyService);

  readonly currency = this.currencyService.currency;
  readonly list = createPagedList<PosSessionSummaryDto>((q) => this.sessionsApi.list(q), { sort: '-openedAt' });

  readonly columns: TableColumn<PosSessionSummaryDto>[] = [
    { key: 'registerName', label: 'Register', sortField: 'registerName' },
    { key: 'cashierName', label: 'Cashier', sortField: 'cashierName' },
    { key: 'openedAt', label: 'Opened', sortField: 'openedAt', filterField: 'openedAt' },
    { key: 'closedAt', label: 'Closed', sortField: 'closedAt' },
    { key: 'openingCash', label: 'Float', align: 'right', filter: false },
    { key: 'totalSales', label: 'Sales', align: 'right', filter: false },
    { key: 'totalReturns', label: 'Returns & voids', align: 'right', filter: false },
    { key: 'expectedCash', label: 'Expected', align: 'right', filter: false },
    { key: 'actualCash', label: 'Counted', align: 'right', filter: false },
    { key: 'cashDifference', label: 'Over/short', align: 'right', filter: false },
    { key: 'status', label: 'Status', filterField: 'status', filterOptions: ['open', 'closed'] },
  ];

  readonly drawerOpen = signal(false);
  readonly report = signal<PosShiftReportDto | null>(null);
  readonly reportFailed = signal(false);
  private readonly detailId = signal<string | null>(null);
  readonly detailTitle = computed(() => this.report()?.session.registerName ?? 'Shift');

  readonly cashOpen = signal(false);
  readonly cashShifts = signal<CashDialogShift[]>([]);

  open(s: PosSessionSummaryDto): void {
    this.detailId.set(s.id);
    this.drawerOpen.set(true);
    this.loadReport(s.id);
  }

  private loadReport(id: string): void {
    this.report.set(null);
    this.reportFailed.set(false);
    this.sessionsApi.report(id).subscribe({
      next: (r) => this.report.set(r),
      error: () => this.reportFailed.set(true),
    });
  }

  startCash(): void {
    this.sessionsApi.list({ page: 1, pageSize: 50, filters: [{ field: 'status', op: 'eq', value: 'open' }] }).subscribe((res) => {
      this.cashShifts.set(res.items.map((s) => ({ id: s.id, label: `${s.registerName} (${s.cashierName})`, expectedCash: s.expectedCash })));
      this.cashOpen.set(true);
    });
  }

  startCashFor(r: PosShiftReportDto): void {
    this.cashShifts.set([{ id: r.session.id, label: r.session.registerName, expectedCash: r.session.expectedCash }]);
    this.cashOpen.set(true);
  }

  cashRecorded(): void {
    this.cashOpen.set(false);
    this.list.reload();
    const id = this.detailId();
    if (this.drawerOpen() && id) this.loadReport(id);
  }
}
