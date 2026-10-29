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
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { GoodsReceiptInput, GoodsReceiptsApi, PurchaseOrderDto, PurchaseOrdersApi, PurchaseOrderStatus } from '../../core/api/purchasing.api';
import { HasPermissionDirective } from '../../shared/has-permission.directive';

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
    NzGridModule,
    NzCardModule,
    NzTagModule,
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
              <nz-tag [nzColor]="statusColor(o.status)">{{ o.status }}</nz-tag>
              @if (o.invoicedBillId) {
                <nz-tag nzColor="green"><span nz-icon nzType="check"></span> Billed</nz-tag>
              }
            </div>
            <p class="vendor-subtitle">Vendor / Supplier: <strong>{{ o.vendorName }}</strong></p>
          </div>

          <div class="header-actions">
            @if (o.status === 'Draft' || o.status === 'Submitted') {
              <button *erpHasPermission="'purchasing.orders.manage'" nz-button nzType="primary" (click)="changeStatus('Approved')">
                <span nz-icon nzType="check"></span>Approve Order
              </button>
            }
            @if (o.status === 'Approved' || o.status === 'PartiallyReceived') {
              <button *erpHasPermission="'purchasing.receipts.manage'" nz-button (click)="openReceiptModal()">
                <span nz-icon nzType="download"></span>Receive Goods (GRN)
              </button>
            }
            @if (!o.invoicedBillId && (o.status === 'Approved' || o.status === 'PartiallyReceived' || o.status === 'Received')) {
              <button *erpHasPermission="'purchasing.orders.manage'" nz-button nzType="primary" (click)="convertToBill()">
                <span nz-icon nzType="file-done"></span>Generate Vendor Bill
              </button>
            }
            @if (o.invoicedBillId) {
              <a [routerLink]="['/finance/bills', o.invoicedBillId]" nz-button nzType="dashed">
                <span nz-icon nzType="file-text"></span>View Vendor Bill
              </a>
            }
            <a [href]="'/print/purchase-order/' + o.id" target="_blank" nz-button data-testid="po-print">
              <span nz-icon nzType="printer"></span>Print / PDF
            </a>
            @if (o.status === 'Draft' || o.status === 'Submitted' || o.status === 'Approved') {
              <button *erpHasPermission="'purchasing.orders.manage'" nz-button nzDanger
                      nz-popconfirm nzPopconfirmTitle="Cancel this purchase order?" (nzOnConfirm)="changeStatus('Cancelled')">
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
                <nz-descriptions-item nzTitle="Order Date">{{ o.orderDate }}</nz-descriptions-item>
                <nz-descriptions-item nzTitle="Expected Delivery">{{ o.expectedDeliveryDate || 'Standard' }}</nz-descriptions-item>
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
                <span class="value" style="color: #ef4444;">-{{ o.currency }} {{ o.discountTotal | number:'1.2-2' }}</span>
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
                    <span [style.color]="item.receivedQuantity >= item.quantity ? '#10955d' : '#d97706'">
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
          nzTitle="Record Goods Receipt Note (GRN)"
          [nzWidth]="750"
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
                      <th style="width: 90px;">Already Recv</th>
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
                            [nzMin]="0"
                            [nzMax]="ctrl.get('orderedQuantity')?.value"
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
      }
    </div>
  `,
  styles: [`
    .po-detail-container { padding: 24px; max-width: 1400px; margin: 0 auto; }
    .header-nav { margin-bottom: 12px; }
    .back-link { color: #64748b; font-size: 14px; display: inline-flex; align-items: center; gap: 6px; }
    .back-link:hover { color: #10955d; }
    .page-header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 24px; }
    .number-tag { display: flex; align-items: center; gap: 12px; }
    .po-number { font-size: 26px; font-weight: 700; color: #0f172a; margin: 0; }
    .vendor-subtitle { color: #64748b; font-size: 14px; margin: 4px 0 0 0; }
    .header-actions { display: flex; gap: 8px; align-items: center; }
    .info-card, .summary-card, .items-card { border-radius: 12px; border: 1px solid #e2e8f0; }
    .note-box { background: #f8fafc; border-left: 3px solid #10955d; padding: 10px 14px; border-radius: 0 6px 6px 0; font-size: 13px; }
    .note-box p { margin: 4px 0 0 0; color: #475569; }
    .summary-row { display: flex; justify-content: space-between; padding: 8px 0; border-bottom: 1px solid #f1f5f9; font-size: 14px; color: #475569; }
    .total-row { border-top: 2px solid #e2e8f0; border-bottom: none; margin-top: 8px; padding-top: 12px; font-size: 18px; font-weight: 700; color: #0f172a; }
    .receiving-meter { margin-top: 20px; border-top: 1px solid #f1f5f9; padding-top: 16px; }
    .meter-label { display: flex; justify-content: space-between; font-size: 13px; color: #475569; margin-bottom: 6px; }
    .receipt-lines-table { width: 100%; border-collapse: collapse; font-size: 13px; margin-top: 12px; }
    .receipt-lines-table th { background: #f8fafc; padding: 8px; border: 1px solid #e2e8f0; text-align: left; }
    .receipt-lines-table td { padding: 6px; border: 1px solid #e2e8f0; vertical-align: middle; }
  `],
})
export class PurchaseOrderDetailPage {
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);
  private readonly ordersApi = inject(PurchaseOrdersApi);
  private readonly receiptsApi = inject(GoodsReceiptsApi);
  private readonly message = inject(NzMessageService);

  readonly order = signal<PurchaseOrderDto | null>(null);
  readonly receiptModalVisible = signal(false);

  readonly receiptForm: FormGroup = this.fb.group({
    receivedBy: ['Store Manager', Validators.required],
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

  loadOrder(id: string): void {
    this.ordersApi.get(id).subscribe({
      next: (o) => this.order.set(o),
      error: () => this.message.error('Failed to load purchase order'),
    });
  }

  changeStatus(status: PurchaseOrderStatus): void {
    const o = this.order();
    if (!o) return;
    this.ordersApi.setStatus(o.id, status).subscribe({
      next: (updated) => {
        this.order.set(updated);
        this.message.success(`Status updated to ${status}`);
      },
      error: (err) => this.message.error(err?.error?.message ?? 'Failed to update status'),
    });
  }

  openReceiptModal(): void {
    const o = this.order();
    if (!o) return;
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
    const input: GoodsReceiptInput = {
      legalEntityId: o.legalEntityId,
      purchaseOrderId: o.id,
      receivedBy: raw.receivedBy,
      receiptDate: new Date().toISOString().split('T')[0],
      deliveryNoteNumber: raw.deliveryNoteNumber || null,
      lines: raw.lines.map((l: any) => ({
        purchaseOrderLineId: l.purchaseOrderLineId,
        itemId: l.itemId,
        itemCode: l.itemCode,
        description: l.description,
        quantityReceived: l.quantityReceived,
        unitOfMeasure: l.unitOfMeasure,
      })),
    };

    this.receiptsApi.create(input).subscribe({
      next: () => {
        this.message.success('Goods Receipt Note (GRN) created successfully');
        this.receiptModalVisible.set(false);
        this.loadOrder(o.id);
      },
      error: (err) => this.message.error(err?.error?.message ?? 'Failed to record receipt'),
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
      error: (err) => this.message.error(err?.error?.message ?? 'Failed to generate vendor bill'),
    });
  }

  statusColor(status: string): string {
    switch (status) {
      case 'Approved': return 'blue';
      case 'PartiallyReceived': return 'orange';
      case 'Received': return 'cyan';
      case 'Billed': return 'green';
      case 'Cancelled': return 'red';
      default: return 'default';
    }
  }
}
