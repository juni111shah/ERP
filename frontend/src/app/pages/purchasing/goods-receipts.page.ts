import { CommonModule, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzTableModule } from 'ng-zorro-antd/table';
import { createPagedList } from '../../core/api/paged-list';
import { GoodsReceiptDto, GoodsReceiptSummaryDto, GoodsReceiptsApi } from '../../core/api/purchasing.api';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';

@Component({
  selector: 'erp-goods-receipts',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    DecimalPipe,
    RouterModule,
    NzCardModule,
    NzButtonModule,
    NzIconModule,
    NzModalModule,
    NzTableModule,
    NzPopconfirmModule,
    PagedTableComponent,
    HasPermissionDirective,
  ],
  template: `
    <erp-paged-table
      [list]="list"
      [columns]="columns"
      searchPlaceholder="Search GRN number, PO number, vendor...">
          
          <ng-template #cell let-row let-column="column">
            @if (column.key === 'number') {
              <strong>{{ row.number }}</strong>
            } @else if (column.key === 'purchaseOrderNumber') {
              <a [routerLink]="['/purchasing/orders', row.purchaseOrderId]" class="po-link">
                {{ row.purchaseOrderNumber }}
              </a>
            } @else if (column.key === 'vendorName') {
              <span class="vendor-name">{{ row.vendorName }}</span>
            } @else if (column.key === 'receiptDate') {
              <span>{{ row.receiptDate }}</span>
            } @else if (column.key === 'receivedBy') {
              <span>{{ row.receivedBy }}</span>
            } @else if (column.key === 'deliveryNoteNumber') {
              <span>{{ row.deliveryNoteNumber || '—' }}</span>
            } @else if (column.key === 'actions') {
              <div class="action-buttons">
                <button nz-button nzType="link" nzSize="small" (click)="viewDetail(row.id)">
                  View Items
                </button>
                <a [href]="'/print/grn/' + row.id" target="_blank" nz-button nzType="link" nzSize="small">
                  <span nz-icon nzType="printer"></span> Print GRN
                </a>
                <button *erpHasPermission="'purchasing.receipts.manage'" nz-button nzType="link" nzDanger nzSize="small"
                        nz-popconfirm nzPopconfirmTitle="Delete this receipt?" (nzOnConfirm)="deleteReceipt(row.id)">
                  Delete
                </button>
              </div>
            }
          </ng-template>
        </erp-paged-table>

      <!-- View Detail Modal -->
      <nz-modal
        [(nzVisible)]="detailModalVisible"
        nzTitle="Goods Receipt Note Details"
        [nzWidth]="700"
        [nzFooter]="null"
        (nzOnCancel)="detailModalVisible.set(false)">
        <ng-container *nzModalContent>
          @if (selectedReceipt(); as grn) {
            <div class="modal-info">
              <p><strong>GRN Number:</strong> {{ grn.number }}</p>
              <p><strong>PO Reference:</strong> {{ grn.PurchaseOrderNumber || grn.purchaseOrderNumber }}</p>
              <p><strong>Supplier:</strong> {{ grn.vendorName }}</p>
              <p><strong>Received On:</strong> {{ grn.receiptDate }} by {{ grn.receivedBy }}</p>
            </div>

            <nz-table #linesTable [nzData]="grn.lines" [nzShowPagination]="false">
              <thead>
                <tr>
                  <th>Code</th>
                  <th>Description</th>
                  <th style="width: 120px; text-align: right;">Qty Received</th>
                </tr>
              </thead>
              <tbody>
                @for (item of linesTable.data; track item.id) {
                  <tr>
                    <td><strong>{{ item.itemCode || '-' }}</strong></td>
                    <td>{{ item.description }}</td>
                    <td style="text-align: right; font-weight: 600; color: #10955d;">
                      {{ item.quantityReceived | number:'1.0-2' }} {{ item.unitOfMeasure }}
                    </td>
                  </tr>
                }
              </tbody>
            </nz-table>

            <div style="display: flex; justify-content: flex-end; gap: 8px; margin-top: 20px;">
              <a [href]="'/print/grn/' + grn.id" target="_blank" nz-button nzType="primary">
                <span nz-icon nzType="printer"></span> Print GRN Note
              </a>
              <button nz-button (click)="detailModalVisible.set(false)">Close</button>
            </div>
          }
        </ng-container>
      </nz-modal>
  `,
  styles: [`
    .po-link { color: #10955d; font-weight: 600; }
    .vendor-name { font-weight: 600; color: #1e293b; }
    .action-buttons { display: flex; gap: 4px; align-items: center; }
    .modal-info { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; margin-bottom: 16px; font-size: 13.5px; }
    .modal-info p { margin: 0; color: #475569; }
  `],
})
export class GoodsReceiptsPage {
  private readonly receiptsApi = inject(GoodsReceiptsApi);
  private readonly message = inject(NzMessageService);

  readonly list = createPagedList((q) => this.receiptsApi.list(q), { sort: 'number' });
  readonly detailModalVisible = signal(false);
  readonly selectedReceipt = signal<any | null>(null);

  readonly columns: TableColumn<GoodsReceiptSummaryDto>[] = [
    { key: 'number', label: 'GRN #', sortField: 'number', width: '130px' },
    { key: 'purchaseOrderNumber', label: 'Purchase Order #', sortField: 'purchaseOrderNumber', width: '150px' },
    { key: 'vendorName', label: 'Vendor / Supplier', sortField: 'vendorName' },
    { key: 'receiptDate', label: 'Receipt Date', sortField: 'receiptDate', width: '120px' },
    { key: 'receivedBy', label: 'Received By', sortField: 'receivedBy', width: '150px' },
    { key: 'deliveryNoteNumber', label: 'Delivery Note #', width: '140px' },
    { key: 'actions', label: 'Actions', width: '150px' },
  ];

  viewDetail(id: string): void {
    this.receiptsApi.get(id).subscribe({
      next: (grn) => {
        this.selectedReceipt.set(grn);
        this.detailModalVisible.set(true);
      },
      error: () => this.message.error('Failed to load receipt details'),
    });
  }

  deleteReceipt(id: string): void {
    this.receiptsApi.delete(id).subscribe({
      next: () => {
        this.message.success('Goods receipt deleted');
        this.list.reload();
      },
      error: (err) => this.message.error(err?.error?.message ?? 'Failed to delete receipt'),
    });
  }
}
