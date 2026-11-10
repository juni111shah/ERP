import { CommonModule, DecimalPipe } from '@angular/common';
import { problemMessage } from '@core/http/problem-details';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDescriptionsModule } from 'ng-zorro-antd/descriptions';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { SessionStore } from '@core/session/session.store';
import { ConvertToBillInput, GoodsReceiptInput, GoodsReceiptsApi, PurchaseOrderDto, PurchaseOrdersApi, PurchaseOrderStatus } from '@core/api/purchasing.api';
import { WarehouseRow, WarehousesApi } from '@core/api/inventory.api';
import { ApprovalStateComponent } from '@shared/components/approval-state/approval-state.component';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { displayLabel } from '@core/format/display';

@Component({
  selector: 'erp-purchase-order-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ApprovalStateComponent, NzSelectModule, ErpDatePipe, StatePanelComponent, StatusTagComponent, NzSkeletonModule, NzToolTipModule,
    CommonModule,
    DecimalPipe,
    FormsModule,
    ReactiveFormsModule,
    RouterModule,
    NzGridModule,
    NzCardModule,
    NzButtonModule,
    NzIconModule,
    NzTableModule,
    NzDescriptionsModule,
    NzProgressModule,
    NzModalModule,
    NzFormModule,
    NzInputModule,
    NzInputNumberModule,
    NzDatePickerModule,
    NzPopconfirmModule,
    HasPermissionDirective,
  ],
  templateUrl: './purchase-order-detail.page.html',
  styleUrl: './purchase-order-detail.page.scss',
})
export class PurchaseOrderDetailPage {
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);
  private readonly ordersApi = inject(PurchaseOrdersApi);
  private readonly receiptsApi = inject(GoodsReceiptsApi);
  private readonly message = inject(NzMessageService);
  private readonly session = inject(SessionStore);

  readonly order = signal<PurchaseOrderDto | null>(null);
  readonly failed = signal(false);
  readonly saving = signal(false);
  private orderId: string | null = null;
  readonly receiptModalVisible = signal(false);
  readonly billModalVisible = signal(false);
  readonly billDraft = signal<{ lineId: string; description: string; ordered: number; received: number; billed: number; orderPrice: number; quantity: number; price: number }[]>([]);
  readonly allowUnreceived = signal(false);
  readonly billReference = signal('');
  readonly warehouses = signal<WarehouseRow[]>([]);

  readonly receiptForm: FormGroup = this.fb.group({
    receivedBy: ['', Validators.required],
    deliveryNoteNumber: [''],
    warehouseId: [null as string | null],
    lines: this.fb.array([]),
  });

  get receiptLinesArray(): FormArray {
    return this.receiptForm.get('lines') as FormArray;
  }

  /** Something has been received and not all of it has gone back. */
  readonly hasReceived = () => (this.order()?.lines ?? []).some((l) => l.receivedQuantity - (l.returnedQuantity ?? 0) > 0);

  readonly receivingPercentage = () => {
    const o = this.order();
    if (!o || o.lines.length === 0) return 0;
    const totalOrdered = o.lines.reduce((acc, l) => acc + l.quantity, 0);
    const totalReceived = o.lines.reduce((acc, l) => acc + l.receivedQuantity, 0);
    if (totalOrdered <= 0) return 0;
    return Math.min(100, Math.round((totalReceived / totalOrdered) * 100));
  };

  constructor() {
    // The warehouse picker is a convenience: without the inventory permission the entity's default warehouse is used.
    inject(WarehousesApi).list({ page: 1, pageSize: 100, sort: 'code' }).subscribe({ next: (r) => this.warehouses.set(r.items.filter((w) => w.isActive)), error: () => undefined });
    this.route.paramMap.subscribe((params) => {
      const id = params.get('id');
      if (id) this.loadOrder(id);
    });
  }

  reload(): void {
    if (this.orderId) this.loadOrder(this.orderId);
  }

  loadOrder(id: string): void {
    this.orderId = id;
    this.failed.set(false);
    this.ordersApi.get(id).subscribe({
      next: (o) => this.order.set(o),
      error: () => this.failed.set(true),
    });
  }

  changeStatus(status: PurchaseOrderStatus): void {
    const o = this.order();
    if (!o) return;
    this.ordersApi.setStatus(o.id, status).subscribe({
      next: (updated) => {
        this.order.set(updated);
        // With an approval rule the order stays submitted until someone else approves it.
        if (status === 'approved' && updated.status !== 'approved') this.message.info(`${o.number} was sent for approval. It becomes approved when the approver decides.`);
        else this.message.success(`${o.number} is now ${displayLabel(status).toLowerCase()}.`);
      },
      error: (err) => this.message.error(problemMessage(err, 'Failed to update status')),
    });
  }

  openReceiptModal(): void {
    const o = this.order();
    if (!o) return;
    this.receiptForm.patchValue({ receivedBy: this.session.me()?.user?.fullName ?? '', deliveryNoteNumber: '' });
    this.receiptLinesArray.clear();
    for (const l of o.lines) {
      const remaining = Math.max(0, l.quantity - l.receivedQuantity);
      this.receiptLinesArray.push(
        this.fb.group({
          purchaseOrderLineId: [l.id],
          itemId: [l.itemId],
          itemCode: [l.itemCode],
          description: [l.description],
          orderedQuantity: [l.quantity],
          alreadyReceived: [l.receivedQuantity],
          quantityReceived: [remaining, [Validators.required, Validators.min(0)]],
          unitOfMeasure: [l.unitOfMeasure],
        })
      );
    }
    this.receiptModalVisible.set(true);
  }

  saveReceipt(): void {
    const o = this.order();
    if (!o || this.receiptForm.invalid) return;

    const raw = this.receiptForm.value;
    // The API refuses a line with nothing received, so lines left at zero are not sent.
    const receivedLines = raw.lines.filter((l: { quantityReceived: number }) => l.quantityReceived > 0);
    if (receivedLines.length === 0) {
      this.message.warning('Enter a quantity for at least one item.');
      return;
    }
    const input: GoodsReceiptInput = {
      legalEntityId: o.legalEntityId,
      purchaseOrderId: o.id,
      receivedBy: raw.receivedBy,
      receiptDate: new Date().toISOString().split('T')[0],
      deliveryNoteNumber: raw.deliveryNoteNumber || null,
      warehouseId: raw.warehouseId || null,
      lines: receivedLines.map((l: any) => ({
        purchaseOrderLineId: l.purchaseOrderLineId,
        itemId: l.itemId,
        itemCode: l.itemCode,
        description: l.description,
        quantityReceived: l.quantityReceived,
        unitOfMeasure: l.unitOfMeasure,
      })),
    };

    this.saving.set(true);
    this.receiptsApi.create(input).subscribe({
      next: () => {
        this.saving.set(false);
        this.message.success(`Goods receipt recorded against ${o.number}.`);
        this.receiptModalVisible.set(false);
        this.loadOrder(o.id);
      },
      error: (err) => {
        this.saving.set(false);
        this.message.error(problemMessage(err, 'Failed to record receipt'));
      },
    });
  }

  /** More can be billed: something is not yet billed and the order is past approval. */
  canBill(): boolean {
    const o = this.order();
    return !!o && (o.status === 'approved' || o.status === 'partiallyReceived' || o.status === 'received' || o.status === 'billed') && o.lines.some((l) => l.billedQuantity < l.quantity);
  }

  openBillModal(): void {
    const o = this.order();
    if (!o) return;
    this.billDraft.set(o.lines.map((l) => ({
      lineId: l.id, description: l.description, ordered: l.quantity, received: l.receivedQuantity, billed: l.billedQuantity, orderPrice: l.unitPrice,
      // What is billable: received and not yet billed. Nothing is offered for goods that have not arrived unless that is chosen below.
      quantity: Math.max(0, l.receivedQuantity - l.billedQuantity), price: l.unitPrice,
    })));
    this.allowUnreceived.set(false);
    this.billReference.set('');
    this.billModalVisible.set(true);
  }

  setBill(i: number, key: 'quantity' | 'price', value: number | null): void {
    this.billDraft.update((rows) => rows.map((r, n) => (n === i ? { ...r, [key]: value ?? 0 } : r)));
  }

  /** The request body: only lines with a quantity, only prices that differ from the order. */
  billRequest(): ConvertToBillInput {
    const rows = this.billDraft();
    const overrides: Record<string, number> = {};
    for (const r of rows) if (r.quantity > 0 && r.price !== r.orderPrice) overrides[r.lineId] = r.price;
    const body: ConvertToBillInput = { lines: rows.filter((r) => r.quantity > 0).map((r) => ({ lineId: r.lineId, quantity: r.quantity })), allowUnreceived: this.allowUnreceived() };
    if (Object.keys(overrides).length) body.unitPriceOverrides = overrides;
    if (this.billReference().trim()) body.vendorReference = this.billReference().trim();
    return body;
  }

  saveBill(): void {
    const o = this.order();
    if (!o) return;
    const body = this.billRequest();
    if (!body.lines?.length && !this.allowUnreceived()) {
      this.message.warning('Enter a quantity to bill for at least one item, or choose to bill goods not yet received.');
      return;
    }
    this.saving.set(true);
    this.ordersApi.convertToBill(o.id, body).subscribe({
      next: () => {
        this.saving.set(false);
        this.billModalVisible.set(false);
        this.message.success(`Vendor bill created from order ${o.number}`);
        this.loadOrder(o.id);
      },
      error: (err) => {
        this.saving.set(false);
        this.message.error(problemMessage(err, 'Failed to generate vendor bill'));
      },
    });
  }

}
