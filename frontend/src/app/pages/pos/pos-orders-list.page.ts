import { CommonModule, DecimalPipe } from '@angular/common';
import { problemMessage } from '../../core/http/problem-details';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { catchError, forkJoin, of } from 'rxjs';
import {
  PosOrderDto,
  PosOrdersApi,
  PosOrderSummaryDto,
  PosRegisterDto,
  PosRegistersApi,
} from '../../core/api/pos.api';
import { createPagedList } from '../../core/api/paged-list';
import { SessionStore } from '../../core/session/session.store';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { ThermalReceiptComponent, ThermalReceiptData } from '../../shared/thermal-receipt.component';
import { ErpDatePipe } from '../../shared/display.pipes';
import { StatusTagComponent } from '../../shared/status-tag.component';
import { displayLabel } from '../../core/format/display';

@Component({
  selector: 'erp-pos-orders-list',
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
    NzDividerModule,
    PagedTableComponent,
    HasPermissionDirective,
    ThermalReceiptComponent,
  ],
  template: `
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search order number, customer, cashier..." emptyText="No counter sales yet. Open the terminal to make one." exportFilename="pos-orders" [clickable]="false">
      <a toolbar *erpHasPermission="'pos.orders.create'" routerLink="/pos/terminal" nz-button nzType="primary" data-testid="pos-open-terminal">
        <span nz-icon nzType="shop"></span>Open Terminal
      </a>
          <ng-template #cell let-ord let-column="column" let-value="value">
            @if (column.key === 'number') {
              <a (click)="viewOrder(ord)" class="num-cell">
                {{ ord.number }}
              </a>
            } @else if (column.key === 'customerName') {
              {{ ord.customerName }}
            } @else if (column.key === 'cashierName') {
              {{ ord.cashierName || '-' }}
            } @else if (column.key === 'orderDate') {
              <span class="nowrap">{{ ord.orderDate | erpDate }}</span>
            } @else if (column.key === 'lineCount') {
              {{ ord.lineCount }} {{ ord.lineCount === 1 ? 'item' : 'items' }}
            } @else if (column.key === 'totalAmount') {
              <strong class="amount">{{ currency() }} {{ ord.totalAmount | number:'1.2-2' }}</strong>
            } @else if (column.key === 'status') {
              <erp-status [value]="ord.status" />
            } @else if (column.key === 'actions') {
              <button nz-button nzType="link" nzSize="small" (click)="viewOrder(ord)">
                Details
              </button>
              <a [href]="'/print/pos-receipt/' + ord.id" target="_blank" nz-button nzType="link" nzSize="small">
                Receipt
              </a>
              @if (ord.status === 'completed') {
                <button *erpHasPermission="'pos.orders.void'" nz-button nzType="link" nzDanger nzSize="small" (click)="openVoidModal(ord)">
                  Void
                </button>
              }
            } @else {
              {{ value }}
            }
          </ng-template>
        </erp-paged-table>

      <!-- View Order Modal -->
      <nz-modal
        [(nzVisible)]="viewModalVisible"
        [nzTitle]="'POS Receipt ' + (selectedOrder()?.number || '')"
        [nzFooter]="viewFooterTpl"
        [nzWidth]="420"
        (nzOnCancel)="viewModalVisible = false"
      >
        <ng-container *nzModalContent>
          @if (thermalReceiptData(); as rc) {
            <erp-thermal-receipt [receipt]="rc" />
          }
        </ng-container>
        <ng-template #viewFooterTpl>
          @if (selectedOrder(); as o) {
            <a [href]="'/print/pos-receipt/' + o.id" target="_blank" nz-button nzType="primary">
              <span nz-icon nzType="printer"></span> Print Receipt
            </a>
          }
          <button nz-button (click)="viewModalVisible = false">Close</button>
        </ng-template>
      </nz-modal>

      <!-- Void Order Modal -->
      <nz-modal
        [(nzVisible)]="voidModalVisible"
        nzTitle="Void Order"
        nzOkText="Void Order"
        nzOkDanger
        (nzOnCancel)="voidModalVisible = false"
        (nzOnOk)="submitVoid()"
      >
        <ng-container *nzModalContent>
          <div style="display: flex; flex-direction: column; gap: 12px;">
            <p>Are you sure you want to void order <strong>{{ orderToVoid?.number }}</strong> for {{ currency() }} {{ orderToVoid?.totalAmount | number:'1.2-2' }}?</p>
            <div>
              <label style="font-weight: 600; display: block; margin-bottom: 4px;">Reason for Void</label>
              <input aria-label="Reason for void" nz-input [(ngModel)]="voidReason" placeholder="e.g. Rung up by mistake" />
            </div>
          </div>
        </ng-container>
      </nz-modal>
  `,
  styles: [`
    .num-cell { font-weight: 600; white-space: nowrap; }
    .nowrap { white-space: nowrap; }
    .amount { font-variant-numeric: tabular-nums; white-space: nowrap; }
  `],
})
export class PosOrdersListPage {
  private readonly api = inject(PosOrdersApi);
  private readonly registersApi = inject(PosRegistersApi);
  private readonly sessionStore = inject(SessionStore);
  private readonly currencyService = inject(WorkspaceCurrencyService);
  private readonly message = inject(NzMessageService);

  readonly currency = this.currencyService.currency;
  readonly list = createPagedList<PosOrderSummaryDto>((q: any) => this.api.list(q), { sort: '-orderDate' });

  readonly columns: TableColumn<PosOrderSummaryDto>[] = [
    { key: 'number', label: 'Order #', sortField: 'number' },
    { key: 'customerName', label: 'Customer', sortField: 'customerName' },
    { key: 'cashierName', label: 'Cashier' },
    { key: 'orderDate', label: 'Date', sortField: 'orderDate', filterField: 'orderDate' },
    { key: 'lineCount', label: 'Lines', align: 'right', filter: false },
    { key: 'totalAmount', label: 'Total Amount', sortField: 'totalAmount', align: 'right', filter: false },
    { key: 'status', label: 'Status', filterField: 'status', filterOptions: ['completed', 'parked', 'voided'] },
    { key: 'actions', label: 'Actions' },
  ];

  viewModalVisible = false;
  readonly selectedOrder = signal<PosOrderDto | null>(null);
  private readonly selectedRegister = signal<PosRegisterDto | null>(null);

  readonly thermalReceiptData = computed<ThermalReceiptData | null>(() => {
    const o = this.selectedOrder();
    if (!o) return null;
    return {
      organizationName: this.sessionStore.me()?.workspace?.name,
      organizationNameAr: this.sessionStore.me()?.workspace?.name,
      branchName: this.selectedRegister()?.name,
      headerNote: this.selectedRegister()?.receiptHeader,
      footerNote: this.selectedRegister()?.receiptFooter,
      title: 'Tax Invoice / Cash Receipt',
      titleAr: 'فاتورة ضريبية / إيصال مبيعات',
      receiptNumber: o.number,
      date: o.orderDate,
      status: o.status,
      cashierName: o.cashierName,
      customerName: o.customerName,
      currency: this.currency(),
      lines: (o.lines || []).map((l) => ({
        code: l.itemCode,
        description: l.description,
        quantity: l.quantity,
        unitPrice: l.unitPrice,
        lineTotal: l.lineTotal,
      })),
      subTotal: o.subTotal,
      taxTotal: o.taxTotal,
      totalAmount: o.totalAmount,
      payments: (o.payments || []).map((p) => ({
        method: p.method,
        amount: p.amount,
        changeAmount: p.changeAmount,
      })),
      notes: o.notes,
    };
  });

  voidModalVisible = false;
  orderToVoid: PosOrderSummaryDto | null = null;
  voidReason = '';

  viewOrder(summary: PosOrderSummaryDto): void {
    // The receipt still shows if the register's wording cannot be loaded.
    forkJoin({
      order: this.api.get(summary.id),
      register: this.registersApi.get(summary.registerId).pipe(catchError(() => of(null))),
    }).subscribe({
      next: ({ order, register }) => {
        this.viewModalVisible = true;
        this.selectedRegister.set(register);
        this.selectedOrder.set(order);
      },
      error: () => this.message.error('Could not load this order.'),
    });
  }

  openVoidModal(ord: PosOrderSummaryDto): void {
    this.orderToVoid = ord;
    this.voidReason = '';
    this.voidModalVisible = true;
  }

  submitVoid(): void {
    if (!this.orderToVoid) return;
    if (!this.voidReason.trim()) {
      this.message.warning('Enter a reason to void this order.');
      return;
    }

    this.api.voidOrder(this.orderToVoid.id, this.voidReason).subscribe({
      next: () => {
        this.voidModalVisible = false;
        this.list.reload();
        this.message.success(`Order ${this.orderToVoid?.number} voided.`);
      },
      error: (err) => this.message.error(problemMessage(err, 'Failed to void order.')),
    });
  }
}
