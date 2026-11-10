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
import { createPagedList } from '@core/api/paged-list';
import { GoodsReceiptDto, GoodsReceiptSummaryDto, GoodsReceiptsApi } from '@core/api/purchasing.api';
import { doneText } from '@core/format/done-text';
import { problemMessage } from '@core/http/problem-details';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { ErpDatePipe } from '@shared/pipes/display.pipes';

/** Goods receipts: the paged list, a receipt's lines in a dialog, and deleting a receipt. */
@Component({
  selector: 'erp-goods-receipts',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    DecimalPipe,
    RouterModule,
    NzButtonModule,
    NzCardModule,
    NzIconModule,
    NzModalModule,
    NzPopconfirmModule,
    NzTableModule,
    ErpDatePipe,
    HasPermissionDirective,
    PagedTableComponent,
  ],
  templateUrl: './goods-receipts.page.html',
  styleUrl: './goods-receipts.page.scss',
})
export class GoodsReceiptsPage {
  private readonly receiptsApi = inject(GoodsReceiptsApi);
  private readonly message = inject(NzMessageService);

  readonly list = createPagedList((q) => this.receiptsApi.list(q), { sort: 'number' });
  readonly detailModalVisible = signal(false);
  readonly selectedReceipt = signal<GoodsReceiptDto | null>(null);

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
