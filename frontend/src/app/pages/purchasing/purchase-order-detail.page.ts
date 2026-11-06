import { CommonModule, DecimalPipe } from '@angular/common';
import { showFormErrors } from '../../core/forms/form-errors';
import { problemMessage } from '../../core/http/problem-details';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDescriptionsModule } from 'ng-zorro-antd/descriptions';
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
import { SessionStore } from '../../core/session/session.store';
import { GoodsReceiptInput, GoodsReceiptsApi, PurchaseOrderDto, PurchaseOrdersApi, PurchaseOrderStatus } from '../../core/api/purchasing.api';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { ErpDatePipe } from '../../shared/display.pipes';
import { StatePanelComponent } from '../../shared/state-panel.component';
import { StatusTagComponent } from '../../shared/status-tag.component';
import { displayLabel } from '../../core/format/display';
import { ErpFormModule } from '../../core/forms/erp-form.module';

@Component({
  selector: 'erp-purchase-order-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, StatePanelComponent, StatusTagComponent, NzSkeletonModule, NzToolTipModule,
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
    ErpFormModule,
    NzInputModule,
    NzInputNumberModule,
    NzDatePickerModule,
    NzPopconfirmModule,
    HasPermissionDirective,
  ],
  template: `
    <div class="po-detail-container">
      @if (order(); as o) {
        <div class="header-nav">
          <a routerLink="/purchasing/orders" class="back-link">
            <span nz-icon nzType="arrow-left"></span> Back to Purchase Orders
          </a>
        </div>

        <div class="page-header">
          <div class="title-section">
            <div class="number-tag">
              <h1 class="po-number">{{ o.number }}</h1>
              <erp-status [value]="o.status" />
            </div>
            <p class="vendor-subtitle">Vendor / Supplier: <strong>{{ o.vendorName }}</strong></p>
          </div>

          <div class="header-actions">
            @if (o.status === 'draft') {
              <button *erpHasPermission="'purchasing.orders.manage'" nz-button (click)="changeStatus('submitted')" data-testid="po-submit">
                <span nz-icon nzType="send"></span>Submit
              </button>
            }
            @if (o.status === 'submitted') {
              <button *erpHasPermission="'purchasing.orders.manage'" nz-button (click)="changeStatus('draft')" data-testid="po-back-to-draft">
                <span nz-icon nzType="undo"></span>Back to Draft
              </button>
            }
            @if (o.status === 'draft' || o.status === 'submitted') {
              <button *erpHasPermission="'purchasing.orders.manage'" nz-button nzType="primary" (click)="changeStatus('approved')">
                <span nz-icon nzType="check"></span>Approve Order
              </button>
            }
            @if (o.status === 'approved' || o.status === 'partiallyReceived') {
              <button *erpHasPermission="'purchasing.receipts.manage'" nz-button (click)="openReceiptModal()">
                <span nz-icon nzType="download"></span>Receive Goods (GRN)
              </button>
            }
            @if (!o.invoicedBillId && (o.status === 'approved' || o.status === 'partiallyReceived' || o.status === 'received')) {
              <span nz-tooltip [nzTooltipTitle]="o.vendorPartyId ? null : 'Link this order to a vendor record to bill it.'">
                <button *erpHasPermission="'purchasing.orders.manage'" nz-button nzType="primary" [disabled]="!o.vendorPartyId" (click)="convertToBill()" data-testid="po-bill">
                  <span nz-icon nzType="file-done"></span>Generate Vendor Bill
                </button>
              </span>
            }
            @if (o.invoicedBillId) {
              <a [routerLink]="['/finance/bills', o.invoicedBillId]" nz-button nzType="dashed">
                <span nz-icon nzType="file-text"></span>View Vendor Bill
              </a>
            }
            <a [href]="'/print/purchase-order/' + o.id" target="_blank" nz-button data-testid="po-print">
              <span nz-icon nzType="printer"></span>Print / PDF
            </a>
            @if (o.status === 'draft' || o.status === 'submitted' || o.status === 'approved') {
              <button *erpHasPermission="'purchasing.orders.manage'" nz-button nzDanger
                      nz-popconfirm nzPopconfirmTitle="Cancel this purchase order?" (nzOnConfirm)="changeStatus('cancelled')">
                Cancel
              </button>
            }
          </div>
        </div>

        <!-- Information Cards -->
        <div nz-row [nzGutter]="16" style="margin-bottom: 24px;">
          <div nz-col [nzXs]="24" [nzLg]="16">
            <nz-card nzTitle="Order Details" class="info-card">
              <nz-descriptions [nzColumn]="{ xxl: 3, xl: 3, lg: 2, md: 2, sm: 1, xs: 1 }">
                <nz-descriptions-item nzTitle="Vendor">{{ o.vendorName }}</nz-descriptions-item>
                <nz-descriptions-item nzTitle="Vendor Ref / Quote">{{ o.vendorReference || '-' }}</nz-descriptions-item>
                <nz-descriptions-item nzTitle="Order Date">{{ o.orderDate | erpDate }}</nz-descriptions-item>
                <nz-descriptions-item nzTitle="Expected Delivery">{{ (o.expectedDeliveryDate | erpDate) || '-' }}</nz-descriptions-item>
                <nz-descriptions-item nzTitle="Currency">{{ o.currency }}</nz-descriptions-item>
                <nz-descriptions-item nzTitle="Payment Terms">{{ o.paymentTerms || '-' }}</nz-descriptions-item>
                <nz-descriptions-item nzTitle="Delivery Address" [nzSpan]="2">{{ o.deliveryAddress || '-' }}</nz-descriptions-item>
              </nz-descriptions>

              @if (o.notes) {
                <div class="note-box" style="margin-top: 16px;">
                  <strong>Special Instructions / Notes:</strong>
                  <p>{{ o.notes }}</p>
                </div>
              }
            </nz-card>
          </div>

          <div nz-col [nzXs]="24" [nzLg]="8">
            <nz-card nzTitle="Procurement Cost" class="summary-card">
              <div class="summary-row">
                <span class="label">Subtotal</span>
                <span class="value">{{ o.currency }} {{ o.subTotal | number:'1.2-2' }}</span>
              </div>
              <div class="summary-row">
                <span class="label">Discount</span>
                <span class="value" style="color: var(--erp-color-error-text);">-{{ o.currency }} {{ o.discountTotal | number:'1.2-2' }}</span>
              </div>
              <div class="summary-row">
                <span class="label">Tax</span>
                <span class="value">{{ o.currency }} {{ o.taxTotal | number:'1.2-2' }}</span>
              </div>
              <div class="summary-row total-row">
                <span class="label">Order Total</span>
                <span class="value">{{ o.currency }} {{ o.totalAmount | number:'1.2-2' }}</span>
              </div>

              <!-- Receiving Meter -->
              <div class="receiving-meter">
                <div class="meter-label">
                  <span>Goods Received</span>
                  <span><strong>{{ receivingPercentage() }}%</strong></span>
                </div>
                <nz-progress [nzPercent]="receivingPercentage()" [nzStatus]="receivingPercentage() === 100 ? 'success' : 'active'"></nz-progress>
              </div>
            </nz-card>
          </div>
        </div>

        <!-- Line Items Table -->
        <nz-card nzTitle="Purchased Items & Receiving Status" class="items-card" [nzBodyStyle]="{ padding: '0' }">
          <nz-table #itemsTable [nzData]="o.lines" [nzShowPagination]="false">
            <thead>
              <tr>
                <th style="width: 140px;">Item Code</th>
                <th>Description</th>
                <th style="width: 100px; text-align: right;">Ordered</th>
                <th style="width: 100px; text-align: right;">Received</th>
                <th style="width: 100px; text-align: right;">Billed</th>
                <th style="width: 120px; text-align: right;">Unit Price</th>
                <th style="width: 90px; text-align: right;">Disc %</th>
                <th style="width: 90px; text-align: right;">Tax %</th>
                <th style="width: 140px; text-align: right;">Line Total</th>
              </tr>
            </thead>
            <tbody>
              @for (item of itemsTable.data; track item.id) {
                <tr>
                  <td><strong>{{ item.itemCode || '-' }}</strong></td>
                  <td>{{ item.description }}</td>
                  <td style="text-align: right;">{{ item.quantity | number:'1.0-2' }} {{ item.unitOfMeasure }}</td>
                  <td style="text-align: right;">
                    <span [style.color]="item.receivedQuantity >= item.quantity ? 'var(--erp-color-primary)' : 'var(--erp-color-warning-text, #d97706)'">
                      {{ item.receivedQuantity | number:'1.0-2' }}
                    </span>
                  </td>
                  <td style="text-align: right;">{{ item.billedQuantity | number:'1.0-2' }}</td>
                  <td style="text-align: right;">{{ item.unitPrice | number:'1.2-2' }}</td>
                  <td style="text-align: right;">{{ item.discountPercent }}%</td>
                  <td style="text-align: right;">{{ item.taxRate }}%</td>
                  <td style="text-align: right; font-weight: 600;">{{ item.lineTotal | number:'1.2-2' }}</td>
                </tr>
              }
            </tbody>
          </nz-table>
        </nz-card>

        <!-- Goods Receipt Modal -->
        <nz-modal
          [(nzVisible)]="receiptModalVisible"
          nzTitle="Receive Goods"
          nzOkText="Record Receipt"
          [nzWidth]="750"
          [nzOkLoading]="saving()"
          (nzOnCancel)="receiptModalVisible.set(false)"
          (nzOnOk)="saveReceipt()">
          <ng-container *nzModalContent>
            <form nz-form [formGroup]="receiptForm" nzLayout="vertical">
              <div nz-row [nzGutter]="16">
                <div nz-col [nzSpan]="12">
                  <nz-form-item>
                    <nz-form-label nzRequired>Received By</nz-form-label>
                    <nz-form-control>
                      <input nz-input formControlName="receivedBy" placeholder="Receiver full name" />
                    </nz-form-control>
                  </nz-form-item>
                </div>
                <div nz-col [nzSpan]="12">
                  <nz-form-item>
                    <nz-form-label>Delivery Note / Waybill #</nz-form-label>
                    <nz-form-control>
                      <input nz-input formControlName="deliveryNoteNumber" placeholder="e.g. DN-8812" />
                    </nz-form-control>
                  </nz-form-item>
                </div>
              </div>

              <div class="receipt-lines-table-container">
                <table class="receipt-lines-table">
                  <thead>
                    <tr>
                      <th>Item Description</th>
                      <th style="width: 80px;">Ordered</th>
                      <th style="width: 90px;">Received</th>
                      <th style="width: 130px;">Receiving Now</th>
                    </tr>
                  </thead>
                  <tbody formArrayName="lines">
                    @for (ctrl of receiptLinesArray.controls; track $index) {
                      <tr [formGroupName]="$index">
                        <td><strong>{{ ctrl.get('description')?.value }}</strong></td>
                        <td>{{ ctrl.get('orderedQuantity')?.value }}</td>
                        <td>{{ ctrl.get('alreadyReceived')?.value }}</td>
                        <td>
                          <nz-input-number
                            formControlName="quantityReceived"
                            [attr.aria-label]="'Quantity receiving now for ' + ctrl.get('description')?.value"
                            [nzMin]="0"
                            style="width: 100%;">
                          </nz-input-number>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </form>
          </ng-container>
        </nz-modal>
      } @else if (failed()) {
        <erp-state-panel kind="error" title="This purchase order could not be loaded." hint="Check your connection and try again." actionLabel="Try again" (action)="reload()" />
      } @else {
        <nz-skeleton [nzActive]="true" [nzParagraph]="{ rows: 8 }" />
      }
    </div>
  `,
  styles: [`
    .po-detail-container { max-width: 1400px; margin: 0 auto; }
    .header-nav { margin-bottom: 12px; }
    .back-link { color: var(--erp-color-text-muted); font-size: 14px; display: inline-flex; align-items: center; gap: 6px; }
    .back-link:hover { color: var(--erp-color-primary); }
    .page-header { display: flex; justify-content: space-between; align-items: flex-start; gap: 16px; margin-bottom: 24px; }
    .number-tag { display: flex; align-items: center; gap: 12px; }
    .po-number { font-size: 26px; font-weight: 700; color: var(--erp-color-text-primary); margin: 0; }
    .vendor-subtitle { color: var(--erp-color-text-muted); font-size: 14px; margin: 4px 0 0 0; }
    .header-actions { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; justify-content: flex-end; }
    .info-card, .summary-card, .items-card { border-radius: 12px; border: 1px solid var(--erp-color-border-subtle); }
    .note-box { background: var(--erp-color-bg-subtle); border-inline-start: 3px solid var(--erp-color-primary); padding: 10px 14px; border-start-end-radius: 6px; border-end-end-radius: 6px; font-size: 13px; }
    .note-box p { margin: 4px 0 0 0; color: var(--erp-color-text-secondary); }
    .summary-row { display: flex; justify-content: space-between; padding: 8px 0; border-bottom: 1px solid var(--erp-color-border-subtle); font-size: 14px; color: var(--erp-color-text-secondary); }
    .total-row { border-top: 2px solid var(--erp-color-border-subtle); border-bottom: none; margin-top: 8px; padding-top: 12px; font-size: 18px; font-weight: 700; color: var(--erp-color-text-primary); }
    .receiving-meter { margin-top: 20px; border-top: 1px solid var(--erp-color-border-subtle); padding-top: 16px; }
    .meter-label { display: flex; justify-content: space-between; font-size: 13px; color: var(--erp-color-text-secondary); margin-bottom: 6px; }
    .receipt-lines-table { width: 100%; border-collapse: collapse; font-size: 13px; margin-top: 12px; }
    .receipt-lines-table th { background: var(--erp-color-bg-subtle); padding: 8px; border: 1px solid var(--erp-color-border-subtle); text-align: left; }
    .receipt-lines-table td { padding: 6px; border: 1px solid var(--erp-color-border-subtle); vertical-align: middle; }
  `],
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

  readonly receiptForm: FormGroup = this.fb.group({
    receivedBy: ['', Validators.required],
    deliveryNoteNumber: [''],
    lines: this.fb.array([]),
  });

  get receiptLinesArray(): FormArray {
    return this.receiptForm.get('lines') as FormArray;
  }

  readonly receivingPercentage = () => {
    const o = this.order();
    if (!o || o.lines.length === 0) return 0;
    const totalOrdered = o.lines.reduce((acc, l) => acc + l.quantity, 0);
    const totalReceived = o.lines.reduce((acc, l) => acc + l.receivedQuantity, 0);
    if (totalOrdered <= 0) return 0;
    return Math.min(100, Math.round((totalReceived / totalOrdered) * 100));
  };

  constructor() {
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
        this.message.success(`Status changed to ${displayLabel(status).toLowerCase()}.`);
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
    if (!o) return;
    if (this.receiptForm.invalid) { showFormErrors(this.receiptForm); return; }

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
        this.message.success('Goods receipt recorded.');
        this.receiptModalVisible.set(false);
        this.loadOrder(o.id);
      },
      error: (err) => {
        this.saving.set(false);
        this.message.error(problemMessage(err, 'Failed to record receipt'));
      },
    });
  }

  convertToBill(): void {
    const o = this.order();
    if (!o) return;
    this.ordersApi.convertToBill(o.id).subscribe({
      next: () => {
        this.message.success('Vendor Bill generated successfully in Accounting');
        this.loadOrder(o.id);
      },
      error: (err) => this.message.error(problemMessage(err, 'Failed to generate vendor bill')),
    });
  }

}
