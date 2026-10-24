import { CommonModule, CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
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
import { NzTagModule } from 'ng-zorro-antd/tag';
import {
  PosOrderDto,
  PosOrdersApi,
  PosOrderSummaryDto,
} from '../../core/api/pos.api';
import { createPagedList } from '../../core/api/paged-list';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';

@Component({
  selector: 'erp-pos-orders-list',
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
    NzDividerModule,
    PagedTableComponent,
  ],
  template: `
    <erp-paged-table [list]="list" [columns]="columns">
      <a toolbar routerLink="/pos/terminal" nz-button nzType="primary" data-testid="pos-open-terminal">
        <span nz-icon nzType="shop"></span>Open Terminal
      </a>
          <ng-template #cell let-ord let-column="column">
            @if (column.key === 'number') {
              <a (click)="viewOrder(ord.id)" style="font-weight: 700; color: #2563eb;">
                {{ ord.number }}
              </a>
            } @else if (column.key === 'customerName') {
              {{ ord.customerName }}
            } @else if (column.key === 'cashierName') {
              {{ ord.cashierName }}
            } @else if (column.key === 'orderDate') {
              {{ ord.orderDate | date:'medium' }}
            } @else if (column.key === 'lineCount') {
              {{ ord.lineCount }} items
            } @else if (column.key === 'totalAmount') {
              <strong>AED {{ ord.totalAmount | number:'1.2-2' }}</strong>
            } @else if (column.key === 'status') {
              <nz-tag [nzColor]="ord.status === 'Completed' ? 'green' : 'error'">
                {{ ord.status }}
              </nz-tag>
            } @else if (column.key === 'actions') {
              <button nz-button nzType="link" nzSize="small" (click)="viewOrder(ord.id)">
                Details
              </button>
              @if (ord.status === 'Completed') {
                <button nz-button nzType="link" nzDanger nzSize="small" (click)="openVoidModal(ord)">
                  Void
                </button>
              }
            }
          </ng-template>
        </erp-paged-table>

      <!-- View Order Modal -->
      <nz-modal
        [(nzVisible)]="viewModalVisible"
        [nzTitle]="'Order ' + (selectedOrder()?.number || '')"
        [nzFooter]="viewFooterTpl"
        (nzOnCancel)="viewModalVisible = false"
      >
        <ng-container *nzModalContent>
          @if (selectedOrder(); as o) {
            <div style="display: flex; flex-direction: column; gap: 12px;">
              <div style="display: flex; justify-content: space-between;">
                <div>
                  <span style="color: #64748b; font-size: 12px;">Customer:</span>
                  <div style="font-weight: 600;">{{ o.customerName }}</div>
                </div>
                <div style="text-align: right;">
                  <span style="color: #64748b; font-size: 12px;">Cashier:</span>
                  <div style="font-weight: 600;">{{ o.cashierName }}</div>
                </div>
              </div>

              <nz-divider style="margin: 6px 0;"></nz-divider>

              <div class="line-items-box">
                @for (l of o.lines; track l.id) {
                  <div style="display: flex; justify-content: space-between; font-size: 13px; margin-bottom: 6px;">
                    <span>{{ l.itemCode }} · {{ l.description }} (x{{ l.quantity }})</span>
                    <strong>AED {{ l.lineTotal | number:'1.2-2' }}</strong>
                  </div>
                }
              </div>

              <nz-divider style="margin: 6px 0;"></nz-divider>

              <div style="display: flex; justify-content: space-between; font-size: 16px; font-weight: 700;">
                <span>Total Amount:</span>
                <span style="color: #2563eb;">AED {{ o.totalAmount | number:'1.2-2' }}</span>
              </div>

              @if (o.notes) {
                <div style="background: #f8fafc; padding: 8px 12px; border-radius: 6px; font-size: 12px; color: #475569;">
                  {{ o.notes }}
                </div>
              }
            </div>
          }
        </ng-container>
        <ng-template #viewFooterTpl>
          <button nz-button (click)="viewModalVisible = false">Close</button>
        </ng-template>
      </nz-modal>

      <!-- Void Order Modal -->
      <nz-modal
        [(nzVisible)]="voidModalVisible"
        nzTitle="Void POS Sales Order"
        (nzOnCancel)="voidModalVisible = false"
        (nzOnOk)="submitVoid()"
      >
        <ng-container *nzModalContent>
          <div style="display: flex; flex-direction: column; gap: 12px;">
            <p>Are you sure you want to void order <strong>{{ orderToVoid?.number }}</strong> for AED {{ orderToVoid?.totalAmount | number:'1.2-2' }}?</p>
            <div>
              <label style="font-weight: 600; display: block; margin-bottom: 4px;">Reason for Void</label>
              <input nz-input [(ngModel)]="voidReason" placeholder="e.g. Accidental ring-up / customer return" />
            </div>
          </div>
        </ng-container>
      </nz-modal>
  `,
  styles: [`
    .line-items-box { max-height: 200px; overflow-y: auto; }
  `],
})
export class PosOrdersListPage {
  private readonly api = inject(PosOrdersApi);
  private readonly message = inject(NzMessageService);

  readonly list = createPagedList<PosOrderSummaryDto>((q: any) => this.api.list(q));

  readonly columns: TableColumn<PosOrderSummaryDto>[] = [
    { key: 'number', label: 'Order #', sortField: 'number' },
    { key: 'customerName', label: 'Customer', sortField: 'customerName' },
    { key: 'cashierName', label: 'Cashier', sortField: 'cashierName' },
    { key: 'orderDate', label: 'Date', sortField: 'orderDate' },
    { key: 'lineCount', label: 'Lines', align: 'right' },
    { key: 'totalAmount', label: 'Total Amount', sortField: 'totalAmount', align: 'right' },
    { key: 'status', label: 'Status', sortField: 'status' },
    { key: 'actions', label: 'Actions' },
  ];

  viewModalVisible = false;
  readonly selectedOrder = signal<PosOrderDto | null>(null);

  voidModalVisible = false;
  orderToVoid: PosOrderSummaryDto | null = null;
  voidReason = '';

  viewOrder(id: string): void {
    this.api.get(id).subscribe({
      next: (ord) => {
        this.selectedOrder.set(ord);
        this.viewModalVisible = true;
      },
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
      this.message.warning('Please provide a reason to void the transaction.');
      return;
    }

    this.api.voidOrder(this.orderToVoid.id, this.voidReason).subscribe({
      next: () => {
        this.voidModalVisible = false;
        this.list.reload();
        this.message.success(`Order ${this.orderToVoid?.number} voided.`);
      },
      error: (err) => this.message.error(err?.error?.message || 'Failed to void order.'),
    });
  }
}
