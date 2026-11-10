import { CommonModule, DecimalPipe } from '@angular/common';
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
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { WarehouseRow, WarehousesApi } from '@core/api/inventory.api';
import { ConvertToBillInput, GoodsReceiptInput, GoodsReceiptsApi, PurchaseOrderDto, PurchaseOrderStatus, PurchaseOrdersApi } from '@core/api/purchasing.api';
import { toUtcIsoDate } from '@core/format/dates';
import { displayLabel } from '@core/format/display';
import { problemMessage } from '@core/http/problem-details';
import { SessionStore } from '@core/session/session.store';
import { ApprovalStateComponent } from '@shared/components/approval-state/approval-state.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { loadInto } from '@shared/utils/load-into';
import {
  BillDraftRow,
  ReceiptFormValue,
  canBill,
  hasReceived,
  newReceiptLine,
  receivingPercentage,
  toBillDraft,
  toBillRequest,
  toReceiptLineInputs,
} from '../../purchasing-order-progress';

/** One purchase order: its lines and progress, status moves, receiving goods and converting it to a vendor bill. */
@Component({
  selector: 'erp-purchase-order-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    DecimalPipe,
    FormsModule,
    ReactiveFormsModule,
    RouterModule,
    NzButtonModule,
    NzCardModule,
    NzDatePickerModule,
    NzDescriptionsModule,
    NzFormModule,
    NzGridModule,
    NzIconModule,
    NzInputModule,
    NzInputNumberModule,
    NzModalModule,
    NzPopconfirmModule,
    NzProgressModule,
    NzSelectModule,
    NzSkeletonModule,
    NzTableModule,
    NzToolTipModule,
    ApprovalStateComponent,
    ErpDatePipe,
    HasPermissionDirective,
    StatePanelComponent,
    StatusTagComponent,
  ],
  templateUrl: './purchase-order-detail.page.html',
  styleUrl: './purchase-order-detail.page.scss',
})
export class PurchaseOrderDetailPage {
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);
  private readonly ordersApi = inject(PurchaseOrdersApi);
  private readonly receiptsApi = inject(GoodsReceiptsApi);
  private readonly warehousesApi = inject(WarehousesApi);
  private readonly message = inject(NzMessageService);
  private readonly session = inject(SessionStore);

  readonly order = signal<PurchaseOrderDto | null>(null);
  readonly failed = signal(false);
  readonly saving = signal(false);
  private orderId: string | null = null;
  readonly receiptModalVisible = signal(false);
  readonly billModalVisible = signal(false);
  readonly billDraft = signal<BillDraftRow[]>([]);
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
  readonly hasReceived = () => hasReceived(this.order());

  readonly receivingPercentage = () => receivingPercentage(this.order());

  constructor() {
    // The warehouse picker is a convenience: without the inventory permission the entity's default warehouse is used.
    loadInto(this.warehouses, this.warehousesApi.list({ page: 1, pageSize: 100, sort: 'code' }), (r) => r.items.filter((w) => w.isActive));
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
    for (const l of o.lines) this.receiptLinesArray.push(newReceiptLine(this.fb, l));
    this.receiptModalVisible.set(true);
  }

  saveReceipt(): void {
    const o = this.order();
    if (!o || this.receiptForm.invalid) return;

    const raw = this.receiptForm.value as ReceiptFormValue;
    // The API refuses a line with nothing received, so lines left at zero are not sent.
    const lines = toReceiptLineInputs(raw.lines);
    if (lines.length === 0) {
      this.message.warning('Enter a quantity for at least one item.');
      return;
    }
    const input: GoodsReceiptInput = {
      legalEntityId: o.legalEntityId,
      purchaseOrderId: o.id,
      receivedBy: raw.receivedBy,
      receiptDate: toUtcIsoDate(new Date()),
      deliveryNoteNumber: raw.deliveryNoteNumber || null,
      warehouseId: raw.warehouseId || null,
      lines,
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
    return canBill(this.order());
  }

  openBillModal(): void {
    const o = this.order();
    if (!o) return;
    this.billDraft.set(toBillDraft(o.lines));
    this.allowUnreceived.set(false);
    this.billReference.set('');
    this.billModalVisible.set(true);
  }

  setBill(i: number, key: 'quantity' | 'price', value: number | null): void {
    this.billDraft.update((rows) => rows.map((r, n) => (n === i ? { ...r, [key]: value ?? 0 } : r)));
  }

  /** The request body: only lines with a quantity, only prices that differ from the order. */
  billRequest(): ConvertToBillInput {
    return toBillRequest(this.billDraft(), this.allowUnreceived(), this.billReference());
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
