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
  template: `
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search register or cashier" emptyText="No shifts yet. Open one from the terminal." exportFilename="pos-shifts" (rowClick)="open($event)">
      <div toolbar class="toolbar">
        <button *erpHasPermission="'pos.sessions.manage'" nz-button type="button" (click)="startCash()" data-testid="pos-drawer-tx"><span nz-icon nzType="swap"></span>Cash in/out</button>
        <a *erpHasPermission="'pos.orders.create'" routerLink="/pos/terminal" nz-button nzType="primary" data-testid="pos-open-terminal"><span nz-icon nzType="shop"></span>Open Terminal</a>
      </div>
      <ng-template #cell let-session let-column="column" let-value="value">
        @if (column.key === 'registerName') {
          <strong>{{ session.registerName }}</strong>
        } @else if (column.key === 'openedAt') {
          <span class="nowrap">{{ session.openedAt | erpDate }}</span>
        } @else if (column.key === 'closedAt') {
          <span class="nowrap">{{ (session.closedAt | erpDate) || '-' }}</span>
        } @else if (column.key === 'openingCash') {
          <span class="amount">{{ session.openingCash | number:'1.2-2' }}</span>
        } @else if (column.key === 'totalSales') {
          <strong class="amount">{{ session.totalSales | number:'1.2-2' }}</strong>
        } @else if (column.key === 'totalReturns') {
          <span class="amount">{{ session.totalReturns | number:'1.2-2' }}</span>
        } @else if (column.key === 'expectedCash') {
          <span class="amount">{{ session.expectedCash | number:'1.2-2' }}</span>
        } @else if (column.key === 'actualCash') {
          <span class="amount">{{ session.status === 'closed' ? (session.actualCash | number:'1.2-2') : '-' }}</span>
        } @else if (column.key === 'cashDifference') {
          @if (session.status === 'closed') {
            <span class="amount variance" [class.short]="session.cashDifference < 0" [class.over]="session.cashDifference > 0">{{ session.cashDifference | number:'1.2-2' }}</span>
          } @else {
            <span class="muted">Open</span>
          }
        } @else if (column.key === 'status') {
          <erp-status [value]="session.status" />
        } @else {
          {{ value }}
        }
      </ng-template>
    </erp-paged-table>

    <nz-drawer [nzVisible]="drawerOpen()" [nzTitle]="detailTitle()" [nzWidth]="520" (nzOnClose)="drawerOpen.set(false)" [nzFooter]="drawerFooter">
      <ng-container *nzDrawerContent>
        @if (reportFailed()) {
          <nz-alert nzType="error" nzShowIcon nzMessage="The shift report could not be loaded."></nz-alert>
        } @else if (report(); as r) {
          <erp-pos-shift-report [report]="r" [currency]="currency()" />
        } @else {
          <nz-skeleton [nzActive]="true" [nzParagraph]="{ rows: 10 }" />
        }
      </ng-container>
      <ng-template #drawerFooter>
        @if (report(); as r) {
          <div class="toolbar">
            <a nz-button [href]="'/print/pos-shift/' + r.session.id" target="_blank" rel="noopener" data-testid="pos-shift-print"><span nz-icon nzType="printer"></span>Print</a>
            @if (r.session.status === 'open') {
              <button *erpHasPermission="'pos.sessions.manage'" nz-button type="button" (click)="startCashFor(r)" data-testid="pos-shift-cash"><span nz-icon nzType="swap"></span>Cash in/out</button>
            }
          </div>
        }
      </ng-template>
    </nz-drawer>

    <erp-pos-cash-dialog [open]="cashOpen()" [shifts]="cashShifts()" (closed)="cashOpen.set(false)" (recorded)="cashRecorded()" />
  `,
  styles: [`
    .toolbar { display: flex; align-items: center; gap: 8px; }
    .nowrap { white-space: nowrap; }
    .amount { font-variant-numeric: tabular-nums; white-space: nowrap; }
    .muted { color: var(--erp-color-text-muted); }
    .variance { font-weight: 600; color: var(--erp-color-text-muted); }
    .variance.short { color: var(--erp-color-error-text); }
    .variance.over { color: var(--erp-color-success-text); }
  `],
})
export class PosSessionsPage {
  private readonly api = inject(PosSessionsApi);
  private readonly currencyService = inject(WorkspaceCurrencyService);

  readonly currency = this.currencyService.currency;
  readonly list = createPagedList<PosSessionSummaryDto>((q: any) => this.api.list(q), { sort: '-openedAt' });

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
    this.api.report(id).subscribe({
      next: (r) => this.report.set(r),
      error: () => this.reportFailed.set(true),
    });
  }

  startCash(): void {
    this.api.list({ page: 1, pageSize: 50, filters: [{ field: 'status', op: 'eq', value: 'open' }] }).subscribe((res) => {
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
