import { CommonModule, DecimalPipe } from '@angular/common';
import { problemMessage } from '../../core/http/problem-details';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpaceModule } from 'ng-zorro-antd/space';
import {
  CashMovementType,
  PosSessionSummaryDto,
  PosSessionsApi,
} from '../../core/api/pos.api';
import { createPagedList } from '../../core/api/paged-list';
import { SessionStore } from '../../core/session/session.store';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { ErpDatePipe } from '../../shared/display.pipes';
import { StatusTagComponent } from '../../shared/status-tag.component';

@Component({
  selector: 'erp-pos-sessions',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, StatusTagComponent,
    CommonModule,
    DecimalPipe,
    FormsModule,
    RouterModule,
    NzGridModule,
    NzCardModule,
    NzButtonModule,
    NzIconModule,
    NzModalModule,
    NzInputModule,
    NzInputNumberModule,
    NzSelectModule,
    NzSpaceModule,
    NzAlertModule,
    PagedTableComponent,
    HasPermissionDirective,
  ],
  template: `
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search register or cashier..." emptyText="No shifts yet. Open one from the terminal." exportFilename="pos-shifts" [clickable]="false">
      <div toolbar style="display:flex;align-items:center;gap:8px">
        <button *erpHasPermission="'pos.sessions.manage'" nz-button nzType="default" (click)="openDrawerTxModal()" data-testid="pos-drawer-tx">
          <span nz-icon nzType="swap"></span>Record Drawer Float / Drop
        </button>
        <a *erpHasPermission="'pos.sessions.manage'" routerLink="/pos/terminal" nz-button nzType="primary" data-testid="pos-open-terminal">
          <span nz-icon nzType="shop"></span>Open Terminal
        </a>
      </div>
          <ng-template #cell let-session let-column="column" let-value="value">
            @if (column.key === 'registerName') {
              <strong>{{ session.registerName }}</strong>
            } @else if (column.key === 'cashierName') {
              {{ session.cashierName }}
            } @else if (column.key === 'openedAt') {
              <span class="nowrap">{{ session.openedAt | erpDate }}</span>
            } @else if (column.key === 'closedAt') {
              <span class="nowrap">{{ (session.closedAt | erpDate) || '-' }}</span>
            } @else if (column.key === 'openingCash') {
              <span class="amount">{{ currency() }} {{ session.openingCash | number:'1.2-2' }}</span>
            } @else if (column.key === 'totalSales') {
              <strong class="amount">{{ currency() }} {{ session.totalSales | number:'1.2-2' }}</strong>
            } @else if (column.key === 'expectedCash') {
              <span class="amount">{{ currency() }} {{ session.expectedCash | number:'1.2-2' }}</span>
            } @else if (column.key === 'actualCash') {
              <span class="amount">{{ session.status === 'closed' ? (currency() + ' ' + (session.actualCash | number:'1.2-2')) : '-' }}</span>
            } @else if (column.key === 'cashDifference') {
              @if (session.status === 'closed') {
                <span class="amount variance" [class.short]="session.cashDifference < 0" [class.over]="session.cashDifference > 0">
                  {{ currency() }} {{ session.cashDifference | number:'1.2-2' }}
                </span>
              } @else {
                <span class="muted">Shift open</span>
              }
            } @else if (column.key === 'status') {
              <erp-status [value]="session.status" />
            } @else {
              {{ value }}
            }
          </ng-template>
        </erp-paged-table>

      <!-- Record Drawer Transaction Modal -->
      <nz-modal
        [(nzVisible)]="drawerTxModalVisible"
        nzTitle="Record Cash Drawer Transaction"
        (nzOnCancel)="drawerTxModalVisible = false"
        (nzOnOk)="submitDrawerTx()"
      >
        <ng-container *nzModalContent>
          <div style="display: flex; flex-direction: column; gap: 14px;">
            <div>
              <label style="font-weight: 600; display: block; margin-bottom: 4px;">Open Shift</label>
              @if (!openSessions().length) {
                <nz-alert nzType="warning" nzShowIcon nzMessage="No shift is open. Open one from the terminal first." style="margin-bottom: 8px;"></nz-alert>
              }
              <nz-select [(ngModel)]="selectedSessionId" style="width: 100%;" aria-label="Open shift" nzPlaceHolder="Choose a shift">
                @for (s of openSessions(); track s.id) {
                  <nz-option [nzValue]="s.id" [nzLabel]="s.registerName + ' (' + s.cashierName + ')'"></nz-option>
                }
              </nz-select>
            </div>

            <div>
              <label style="font-weight: 600; display: block; margin-bottom: 4px;">Transaction Type</label>
              <nz-select [(ngModel)]="txType" style="width: 100%;" aria-label="Transaction type">
                <nz-option nzValue="floatIn" nzLabel="Float In (Add change to drawer)"></nz-option>
                <nz-option nzValue="dropOut" nzLabel="Safe Drop (Transfer cash to safe)"></nz-option>
                <nz-option nzValue="payIn" nzLabel="Cash Pay-In (Miscellaneous inflow)"></nz-option>
                <nz-option nzValue="payOut" nzLabel="Cash Pay-Out (Expense from drawer)"></nz-option>
              </nz-select>
            </div>

            <div>
              <label style="font-weight: 600; display: block; margin-bottom: 4px;">Amount ({{ currency() }})</label>
              <nz-input-number [(ngModel)]="txAmount" [nzMin]="1" aria-label="Amount" style="width: 100%; font-size: 16px;"></nz-input-number>
            </div>

            <div>
              <label style="font-weight: 600; display: block; margin-bottom: 4px;">Reason / Memo</label>
              <input aria-label="Reason" nz-input [(ngModel)]="txReason" placeholder="e.g. Midday safe drop to main vault" />
            </div>
          </div>
        </ng-container>
      </nz-modal>
  `,
  styles: [`
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
  private readonly sessionStore = inject(SessionStore);
  private readonly currencyService = inject(WorkspaceCurrencyService);
  private readonly message = inject(NzMessageService);

  readonly currency = this.currencyService.currency;
  readonly list = createPagedList<PosSessionSummaryDto>((q: any) => this.api.list(q), { sort: '-openedAt' });
  readonly openSessions = signal<PosSessionSummaryDto[]>([]);

  drawerTxModalVisible = false;
  selectedSessionId = '';
  txType: CashMovementType = 'dropOut';
  txAmount = 100;
  txReason = '';

  readonly columns: TableColumn<PosSessionSummaryDto>[] = [
    { key: 'registerName', label: 'Register', sortField: 'registerName' },
    { key: 'cashierName', label: 'Cashier', sortField: 'cashierName' },
    { key: 'openedAt', label: 'Opened At', sortField: 'openedAt', filterField: 'openedAt' },
    { key: 'closedAt', label: 'Closed At', sortField: 'closedAt' },
    { key: 'openingCash', label: 'Opening Float', align: 'right', filter: false },
    { key: 'totalSales', label: 'Total Sales', align: 'right', filter: false },
    { key: 'expectedCash', label: 'Expected Cash', align: 'right', filter: false },
    { key: 'actualCash', label: 'Actual Cash', align: 'right', filter: false },
    { key: 'cashDifference', label: 'Variance', align: 'right', filter: false },
    { key: 'status', label: 'Status', filterField: 'status', filterOptions: ['open', 'closed'] },
  ];

  constructor() {
    this.refreshOpenSessions();
  }

  refreshOpenSessions(): void {
    this.api.list({ page: 1, pageSize: 50, filters: [{ field: 'status', op: 'eq', value: 'open' }] }).subscribe({
      next: (res) => {
        const opens = res.items;
        this.openSessions.set(opens);
        if (opens.length > 0 && !this.selectedSessionId) {
          this.selectedSessionId = opens[0].id;
        }
      },
    });
  }

  openDrawerTxModal(): void {
    this.refreshOpenSessions();
    this.drawerTxModalVisible = true;
  }

  submitDrawerTx(): void {
    if (!this.selectedSessionId) {
      this.message.warning('Choose an open shift.');
      return;
    }
    if (!this.txReason.trim()) {
      this.message.warning('Enter a reason for this cash movement.');
      return;
    }

    const user = this.sessionStore.me()?.user;
    if (!user?.id) {
      this.message.error('User authentication is missing.');
      return;
    }
    this.api
      .recordDrawerTransaction(this.selectedSessionId, {
        type: this.txType,
        amount: this.txAmount,
        reason: this.txReason,
      })
      .subscribe({
        next: () => {
          this.drawerTxModalVisible = false;
          this.txReason = '';
          this.list.reload();
          this.message.success('Cash drawer transaction recorded.');
        },
        error: (err) => this.message.error(problemMessage(err, 'Transaction recording failed.')),
      });
  }
}
