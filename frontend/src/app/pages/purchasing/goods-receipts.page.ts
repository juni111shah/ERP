import { doneText } from '../../core/format/done-text';
import { CommonModule, DecimalPipe } from '@angular/common';
import { problemMessage } from '../../core/http/problem-details';
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
import { ErpDatePipe } from '../../shared/display.pipes';

@Component({
  selector: 'erp-goods-receipts',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, 
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
      searchPlaceholder="Search GRN number, PO number, vendor..."
      emptyText="No goods received yet. Receive goods from an approved purchase order."
      exportFilename="goods-receipts"
      [clickable]="false">
          
          <ng-template #cell let-row let-column="column">
            @if (column.key === 'number') {
              <strong class="nowrap">{{ row.number }}</strong>
            } @else if (column.key === 'purchaseOrderNumber') {
              <a [routerLink]="['/purchasing/orders', row.purchaseOrderId]" class="po-link">
                {{ row.purchaseOrderNumber }}
              </a>
            } @else if (column.key === 'vendorName') {
              <span class="vendor-name">{{ row.vendorName }}</span>
            } @else if (column.key === 'receiptDate') {
              <span>{{ row.receiptDate | erpDate }}</span>
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
                  <span nz-icon nzType="printer"></span> Print
                </a>
                <button *erpHasPermission="'purchasing.receipts.manage'" nz-button nzType="link" nzDanger nzSize="small"
                        nz-popconfirm nzPopconfirmTitle="Delete this receipt?" (nzOnConfirm)="deleteReceipt(row)">
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
              <p><strong>PO Reference:</strong> {{ grn.purchaseOrderNumber }}</p>
              <p><strong>Supplier:</strong> {{ grn.vendorName }}</p>
              <p><strong>Received On:</strong> {{ grn.receiptDate | erpDate }} by {{ grn.receivedBy }}</p>
            </div>

            <nz-table #linesTable [nzData]="grn.lines" [nzShowPagination]="false">
              <thead>
                <tr>
                  <th>Code</th>
                  <th>Description</th>
                  <th style="width: 120px; text-align: end;">Qty Received</th>
                </tr>
              </thead>
              <tbody>
                @for (item of linesTable.data; track item.id) {
                  <tr>
                    <td><strong>{{ item.itemCode || '-' }}</strong></td>
                    <td>{{ item.description }}</td>
                    <td style="text-align: end; font-weight: 600; color: var(--erp-color-primary);">
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
    .nowrap { white-space: nowrap; }
    .po-link { color: var(--erp-color-primary); font-weight: 600; white-space: nowrap; }
    .vendor-name { display: block; width: 200px; white-space: normal; font-weight: 600; color: var(--erp-color-text-primary); }
    .action-buttons { display: flex; gap: 4px; align-items: center; }
    .modal-info { display: grid; grid-template-columns: 1fr 1fr; gap: 8px; margin-bottom: 16px; font-size: 13.5px; }
    .modal-info p { margin: 0; color: var(--erp-color-text-secondary); }
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
    { key: 'purchaseOrderNumber', label: 'Purchase Order #', width: '150px' },
    { key: 'vendorName', label: 'Vendor / Supplier', sortField: 'vendorName' },
    { key: 'receiptDate', label: 'Receipt Date', sortField: 'receiptDate', width: '120px', filterField: 'receiptDate' },
    { key: 'receivedBy', label: 'Received By', width: '130px' },
    { key: 'deliveryNoteNumber', label: 'Delivery Note', width: '120px' },
    { key: 'actions', label: 'Actions', width: '240px' },
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

  deleteReceipt(row: { id: string; number?: string | null }): void {
    this.receiptsApi.delete(row.id).subscribe({
      next: () => {
        this.message.success(doneText('deleted', 'Goods receipt', row.number));
        this.list.reload();
      },
      error: (err) => this.message.error(problemMessage(err, 'Failed to delete receipt')),
    });
  }
}
