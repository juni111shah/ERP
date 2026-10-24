import { CommonModule, CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
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
import { NzTagModule } from 'ng-zorro-antd/tag';
import {
  CashMovementType,
  PosSessionSummaryDto,
  PosSessionsApi,
} from '../../core/api/pos.api';
import { createPagedList } from '../../core/api/paged-list';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';

@Component({
  selector: 'erp-pos-sessions',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    CurrencyPipe,
    DatePipe,
    DecimalPipe,
    FormsModule,
    RouterModule,
    NzGridModule,
    NzCardModule,
    NzButtonModule,
    NzIconModule,
    NzTagModule,
    NzModalModule,
    NzInputModule,
    NzInputNumberModule,
    NzSelectModule,
    NzSpaceModule,
    NzAlertModule,
    PagedTableComponent,
  ],
  template: `
    <div class="sessions-page-container">
      <div class="page-header">
        <div>
          <h1 class="erp-page-title">Shifts & Cash Drawers</h1>
          <p class="erp-page-subtitle">Monitor cashier shifts, cash drawer floats, safe drops and drawer reconciliations.</p>
        </div>
        <div class="header-actions">
          <button nz-button nzType="default" (click)="openDrawerTxModal()">
            <span nz-icon nzType="swap"></span>Record Drawer Float / Drop
          </button>
          <a routerLink="/pos/terminal" nz-button nzType="primary">
            <span nz-icon nzType="shop"></span>Open Terminal
          </a>
        </div>
      </div>

      <nz-card class="table-card" [nzBodyStyle]="{ padding: '0' }">
        <erp-paged-table [list]="list" [columns]="columns">
          <ng-template #cell let-session let-column="column">
            @if (column.key === 'registerName') {
              <strong>{{ session.registerName }}</strong>
            } @else if (column.key === 'cashierName') {
              {{ session.cashierName }}
            } @else if (column.key === 'openedAt') {
              {{ session.openedAt | date:'medium' }}
            } @else if (column.key === 'closedAt') {
              {{ session.closedAt ? (session.closedAt | date:'medium') : '—' }}
            } @else if (column.key === 'openingCash') {
              AED {{ session.openingCash | number:'1.2-2' }}
            } @else if (column.key === 'totalSales') {
              <strong>AED {{ session.totalSales | number:'1.2-2' }}</strong>
            } @else if (column.key === 'expectedCash') {
              AED {{ session.expectedCash | number:'1.2-2' }}
            } @else if (column.key === 'actualCash') {
              {{ session.status === 'Closed' ? ('AED ' + (session.actualCash | number:'1.2-2')) : '—' }}
            } @else if (column.key === 'cashDifference') {
              @if (session.status === 'Closed') {
                <span [style.color]="session.cashDifference < 0 ? '#dc2626' : (session.cashDifference > 0 ? '#16a34a' : '#64748b')" style="font-weight: 600;">
                  AED {{ session.cashDifference | number:'1.2-2' }}
                </span>
              } @else {
                <span style="color: #94a3b8;">Pending Close</span>
              }
            } @else if (column.key === 'status') {
              <nz-tag [nzColor]="session.status === 'Open' ? 'green' : 'default'">
                {{ session.status }}
              </nz-tag>
            }
          </ng-template>
        </erp-paged-table>
      </nz-card>

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
              <label style="font-weight: 600; display: block; margin-bottom: 4px;">Target Open Shift Session</label>
              <nz-select [(ngModel)]="selectedSessionId" style="width: 100%;">
                @for (s of openSessions(); track s.id) {
                  <nz-option [nzValue]="s.id" [nzLabel]="s.registerName + ' (' + s.cashierName + ')'"></nz-option>
                }
              </nz-select>
            </div>

            <div>
              <label style="font-weight: 600; display: block; margin-bottom: 4px;">Transaction Type</label>
              <nz-select [(ngModel)]="txType" style="width: 100%;">
                <nz-option nzValue="FloatIn" nzLabel="Float In (Add change to drawer)"></nz-option>
                <nz-option nzValue="DropOut" nzLabel="Safe Drop (Transfer cash to safe)"></nz-option>
                <nz-option nzValue="PayIn" nzLabel="Cash Pay-In (Miscellaneous inflow)"></nz-option>
                <nz-option nzValue="PayOut" nzLabel="Cash Pay-Out (Expense from drawer)"></nz-option>
              </nz-select>
            </div>

            <div>
              <label style="font-weight: 600; display: block; margin-bottom: 4px;">Amount (AED)</label>
              <nz-input-number [(ngModel)]="txAmount" [nzMin]="1" style="width: 100%; font-size: 16px;"></nz-input-number>
            </div>

            <div>
              <label style="font-weight: 600; display: block; margin-bottom: 4px;">Reason / Memo</label>
              <input nz-input [(ngModel)]="txReason" placeholder="e.g. Midday safe drop to main vault" />
            </div>
          </div>
        </ng-container>
      </nz-modal>
    </div>
  `,
  styles: [`
    .sessions-page-container { padding: 24px; max-width: 1400px; margin: 0 auto; }
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
    .erp-page-title { font-size: 24px; font-weight: 700; color: #0f172a; margin: 0; }
    .erp-page-subtitle { color: #64748b; font-size: 14px; margin: 4px 0 0 0; }
    .header-actions { display: flex; gap: 8px; }
    .table-card { border-radius: 12px; border: 1px solid #e2e8f0; }
  `],
})
export class PosSessionsPage {
  private readonly api = inject(PosSessionsApi);
  private readonly message = inject(NzMessageService);

  readonly list = createPagedList<PosSessionSummaryDto>((q: any) => this.api.list(q));
  readonly openSessions = signal<PosSessionSummaryDto[]>([]);

  drawerTxModalVisible = false;
  selectedSessionId = '';
  txType: CashMovementType = 'DropOut';
  txAmount = 100;
  txReason = '';

  readonly columns: TableColumn<PosSessionSummaryDto>[] = [
    { key: 'registerName', label: 'Register', sortField: 'registerName' },
    { key: 'cashierName', label: 'Cashier', sortField: 'cashierName' },
    { key: 'openedAt', label: 'Opened At', sortField: 'openedAt' },
    { key: 'closedAt', label: 'Closed At', sortField: 'closedAt' },
    { key: 'openingCash', label: 'Opening Float', align: 'right' },
    { key: 'totalSales', label: 'Total Sales', align: 'right' },
    { key: 'expectedCash', label: 'Expected Cash', align: 'right' },
    { key: 'actualCash', label: 'Actual Cash', align: 'right' },
    { key: 'cashDifference', label: 'Variance', align: 'right' },
    { key: 'status', label: 'Status', sortField: 'status' },
  ];

  constructor() {
    this.refreshOpenSessions();
  }

  refreshOpenSessions(): void {
    this.api.list({ page: 1, pageSize: 20 }).subscribe({
      next: (res: any) => {
        const opens = (res.items || []).filter((s: any) => s.status === 'Open');
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
      this.message.warning('Please select an active open shift session.');
      return;
    }
    if (!this.txReason.trim()) {
      this.message.warning('Please provide a reason for the drawer movement.');
      return;
    }

    this.api
      .recordDrawerTransaction(this.selectedSessionId, {
        type: this.txType,
        amount: this.txAmount,
        reason: this.txReason,
        performedByUserId: '00000000-0000-0000-0000-000000000001',
      })
      .subscribe({
        next: () => {
          this.drawerTxModalVisible = false;
          this.list.reload();
          this.message.success('Cash drawer transaction recorded.');
        },
        error: (err) => this.message.error(err?.error?.message || 'Transaction recording failed.'),
      });
  }
}
