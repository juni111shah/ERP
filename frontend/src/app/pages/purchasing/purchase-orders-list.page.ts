import { CommonModule, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { LegalEntitiesApi } from '../../core/api/admin.api';
import { createPagedList } from '../../core/api/paged-list';
import { PurchaseOrderInput, PurchaseOrderSummaryDto, PurchaseOrdersApi, PurchasingKpiApi, PurchasingKpiDto } from '../../core/api/purchasing.api';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';

@Component({
  selector: 'erp-purchase-orders-list',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    DecimalPipe,
    ReactiveFormsModule,
    RouterModule,
    NzGridModule,
    NzCardModule,
    NzTagModule,
    NzButtonModule,
    NzIconModule,
    NzToolTipModule,
    NzDrawerModule,
    NzFormModule,
    NzInputModule,
    NzInputNumberModule,
    NzSelectModule,
    NzDatePickerModule,
    NzTableModule,
    NzPopconfirmModule,
    PagedTableComponent,
    HasPermissionDirective,
  ],
  template: `
    <div class="purchasing-container">
      <div class="page-header">
        <div>
          <h1 class="erp-page-title">Purchase Orders</h1>
          <p class="erp-page-subtitle">Procure goods and services, track supplier deliveries and manage vendor bills.</p>
        </div>
        <div class="header-actions">
          <button *erpHasPermission="'purchasing.orders.manage'" nz-button nzType="primary" (click)="openCreateDrawer()" data-testid="po-new">
            <span nz-icon nzType="plus"></span>New Purchase Order
          </button>
        </div>
      </div>

      <!-- KPI Stat Cards -->
      <div nz-row [nzGutter]="[16, 16]" class="kpi-row">
        <div nz-col [nzXs]="24" [nzSm]="12" [nzMd]="6">
          <nz-card class="stat-card">
            <div class="stat-header">
              <span class="stat-label">Open POs</span>
              <span nz-icon nzType="shopping" class="stat-icon icon-po"></span>
            </div>
            <div class="stat-value">{{ kpis()?.openOrdersCount ?? 0 }}</div>
            <div class="stat-sub">orders in procurement</div>
          </nz-card>
        </div>
        <div nz-col [nzXs]="24" [nzSm]="12" [nzMd]="6">
          <nz-card class="stat-card">
            <div class="stat-header">
              <span class="stat-label">Procurement Backlog</span>
              <span nz-icon nzType="clock-circle" class="stat-icon icon-backlog"></span>
            </div>
            <div class="stat-value">{{ kpis()?.openOrdersAmount ?? 0 | number:'1.0-0' }}</div>
            <div class="stat-sub">unreceived commitment</div>
          </nz-card>
        </div>
        <div nz-col [nzXs]="24" [nzSm]="12" [nzMd]="6">
          <nz-card class="stat-card">
            <div class="stat-header">
              <span class="stat-label">Pending Receiving</span>
              <span nz-icon nzType="download" class="stat-icon icon-receipt"></span>
            </div>
            <div class="stat-value">{{ kpis()?.pendingReceiptsCount ?? 0 }}</div>
            <div class="stat-sub">orders awaiting GRN</div>
          </nz-card>
        </div>
        <div nz-col [nzXs]="24" [nzSm]="12" [nzMd]="6">
          <nz-card class="stat-card">
            <div class="stat-header">
              <span class="stat-label">Total Spend</span>
              <span nz-icon nzType="dollar-circle" class="stat-icon icon-spend"></span>
            </div>
            <div class="stat-value">{{ kpis()?.totalSpend ?? 0 | number:'1.0-0' }}</div>
            <div class="stat-sub">lifetime procurement</div>
          </nz-card>
        </div>
      </div>

      <!-- Purchase Orders Table -->
      <nz-card class="table-card" [nzBodyStyle]="{ padding: '0' }">
        <erp-paged-table
          [list]="list"
          [columns]="columns"
          searchPlaceholder="Search PO number, vendor, quote ref...">
          
          <ng-template #cell let-row let-column="column">
            @if (column.key === 'number') {
              <a [routerLink]="['/purchasing/orders', row.id]" class="po-link">
                <strong>{{ row.number }}</strong>
              </a>
            } @else if (column.key === 'vendorName') {
              <div class="vendor-cell">
                <span class="vendor-name">{{ row.vendorName }}</span>
                @if (row.vendorReference) {
                  <span class="vendor-ref">Ref: {{ row.vendorReference }}</span>
                }
              </div>
            } @else if (column.key === 'totalAmount') {
              <strong>{{ row.currency }} {{ row.totalAmount | number:'1.2-2' }}</strong>
            } @else if (column.key === 'status') {
              <nz-tag [nzColor]="statusColor(row.status)">{{ row.status }}</nz-tag>
            } @else if (column.key === 'invoiced') {
              @if (row.invoicedBillId) {
                <nz-tag nzColor="green"><span nz-icon nzType="check"></span> Billed</nz-tag>
              } @else {
                <nz-tag nzColor="default">Pending</nz-tag>
              }
            } @else if (column.key === 'actions') {
              <div class="action-buttons">
                <a [routerLink]="['/purchasing/orders', row.id]" nz-button nzType="link" nzSize="small">
                  Manage
                </a>
                <button *erpHasPermission="'purchasing.orders.manage'" nz-button nzType="link" nzDanger nzSize="small"
                        nz-popconfirm nzPopconfirmTitle="Delete this purchase order?" (nzOnConfirm)="deleteOrder(row.id)">
                  Delete
                </button>
              </div>
            }
          </ng-template>
        </erp-paged-table>
      </nz-card>

      <!-- Create Purchase Order Drawer -->
      <nz-drawer
        [nzVisible]="drawerVisible()"
        nzTitle="New Purchase Order"
        [nzWidth]="850"
        (nzOnClose)="drawerVisible.set(false)">
        <ng-container *nzDrawerContent>
          <form nz-form [formGroup]="form" nzLayout="vertical" (ngSubmit)="submitForm()">
            <div nz-row [nzGutter]="16">
              <div nz-col [nzSpan]="12">
                <nz-form-item>
                  <nz-form-label nzRequired>Legal Entity</nz-form-label>
                  <nz-form-control nzErrorTip="Please select a legal entity">
                    <nz-select formControlName="legalEntityId" nzPlaceHolder="Select Entity">
                      @for (e of entities(); track e.id) {
                        <nz-option [nzValue]="e.id" [nzLabel]="e.name"></nz-option>
                      }
                    </nz-select>
                  </nz-form-control>
                </nz-form-item>
              </div>
              <div nz-col [nzSpan]="12">
                <nz-form-item>
                  <nz-form-label>PO Number (Auto if blank)</nz-form-label>
                  <nz-form-control>
                    <input nz-input formControlName="number" placeholder="PO-2026-0001" />
                  </nz-form-control>
                </nz-form-item>
              </div>
            </div>

            <div nz-row [nzGutter]="16">
              <div nz-col [nzSpan]="12">
                <nz-form-item>
                  <nz-form-label nzRequired>Vendor Name</nz-form-label>
                  <nz-form-control nzErrorTip="Vendor name is required">
                    <input nz-input formControlName="vendorName" placeholder="Supplier or Vendor Name" />
                  </nz-form-control>
                </nz-form-item>
              </div>
              <div nz-col [nzSpan]="12">
                <nz-form-item>
                  <nz-form-label>Vendor Quote / Reference</nz-form-label>
                  <nz-form-control>
                    <input nz-input formControlName="vendorReference" placeholder="e.g. V-QUOT-9821" />
                  </nz-form-control>
                </nz-form-item>
              </div>
            </div>

            <div nz-row [nzGutter]="16">
              <div nz-col [nzSpan]="8">
                <nz-form-item>
                  <nz-form-label nzRequired>Order Date</nz-form-label>
                  <nz-form-control>
                    <nz-date-picker formControlName="orderDate" style="width: 100%;"></nz-date-picker>
                  </nz-form-control>
                </nz-form-item>
              </div>
              <div nz-col [nzSpan]="8">
                <nz-form-item>
                  <nz-form-label>Expected Delivery Date</nz-form-label>
                  <nz-form-control>
                    <nz-date-picker formControlName="expectedDeliveryDate" style="width: 100%;"></nz-date-picker>
                  </nz-form-control>
                </nz-form-item>
              </div>
              <div nz-col [nzSpan]="8">
                <nz-form-item>
                  <nz-form-label nzRequired>Currency</nz-form-label>
                  <nz-form-control>
                    <input nz-input formControlName="currency" placeholder="AED" />
                  </nz-form-control>
                </nz-form-item>
              </div>
            </div>

            <!-- Lines Section -->
            <div class="lines-header">
              <h3>Line Items</h3>
              <button nz-button nzType="dashed" nzSize="small" type="button" (click)="addLine()">
                <span nz-icon nzType="plus"></span>Add Item
              </button>
            </div>

            <div formArrayName="lines" class="lines-table-container">
              <table class="lines-table">
                <thead>
                  <tr>
                    <th style="width: 120px;">Item Code</th>
                    <th>Description</th>
                    <th style="width: 80px;">Qty</th>
                    <th style="width: 100px;">Price</th>
                    <th style="width: 75px;">Disc %</th>
                    <th style="width: 75px;">Tax %</th>
                    <th style="width: 100px;">Total</th>
                    <th style="width: 40px;"></th>
                  </tr>
                </thead>
                <tbody>
                  @for (ctrl of linesArray.controls; track $index) {
                    <tr [formGroupName]="$index">
                      <td><input nz-input formControlName="itemCode" placeholder="Code" /></td>
                      <td><input nz-input formControlName="description" placeholder="Item description" /></td>
                      <td><nz-input-number formControlName="quantity" [nzMin]="1" [nzStep]="1" style="width: 100%;"></nz-input-number></td>
                      <td><nz-input-number formControlName="unitPrice" [nzMin]="0" style="width: 100%;"></nz-input-number></td>
                      <td><nz-input-number formControlName="discountPercent" [nzMin]="0" [nzMax]="100" style="width: 100%;"></nz-input-number></td>
                      <td><nz-input-number formControlName="taxRate" [nzMin]="0" [nzMax]="100" style="width: 100%;"></nz-input-number></td>
                      <td class="line-calc">{{ calculateLineTotal(ctrl.value) | number:'1.2-2' }}</td>
                      <td>
                        @if (linesArray.length > 1) {
                          <a (click)="removeLine($index)" style="color: #ef4444;"><span nz-icon nzType="delete"></span></a>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>

            <div class="po-summary-panel">
              <div class="summary-line">
                <span>Subtotal:</span>
                <span>{{ calculatedTotals().subTotal | number:'1.2-2' }}</span>
              </div>
              <div class="summary-line">
                <span>Total Tax:</span>
                <span>{{ calculatedTotals().taxTotal | number:'1.2-2' }}</span>
              </div>
              <div class="summary-line total-highlight">
                <span>Total Amount:</span>
                <span>{{ form.get('currency')?.value }} {{ calculatedTotals().total | number:'1.2-2' }}</span>
              </div>
            </div>

            <div nz-row [nzGutter]="16" style="margin-top: 16px;">
              <div nz-col [nzSpan]="12">
                <nz-form-item>
                  <nz-form-label>Delivery / Receiving Address</nz-form-label>
                  <nz-form-control>
                    <textarea nz-input formControlName="deliveryAddress" rows="2" placeholder="Warehouse address"></textarea>
                  </nz-form-control>
                </nz-form-item>
              </div>
              <div nz-col [nzSpan]="12">
                <nz-form-item>
                  <nz-form-label>Payment Terms</nz-form-label>
                  <nz-form-control>
                    <textarea nz-input formControlName="paymentTerms" rows="2" placeholder="e.g. Net 30, payment on delivery"></textarea>
                  </nz-form-control>
                </nz-form-item>
              </div>
            </div>

            <div class="drawer-footer">
              <button nz-button type="button" (click)="drawerVisible.set(false)">Cancel</button>
              <button nz-button nzType="primary" type="submit" [nzLoading]="isSubmitting()">Create Purchase Order</button>
            </div>
          </form>
        </ng-container>
      </nz-drawer>
    </div>
  `,
  styles: [`
    .purchasing-container { padding: 24px; max-width: 1400px; margin: 0 auto; }
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
    .erp-page-title { font-size: 24px; font-weight: 700; color: #0f172a; margin: 0; }
    .erp-page-subtitle { color: #64748b; font-size: 14px; margin: 4px 0 0 0; }
    .kpi-row { margin-bottom: 24px; }
    .stat-card {
      border-radius: 12px;
      border: 1px solid #e2e8f0;
      box-shadow: 0 1px 3px rgba(0,0,0,0.02);
      transition: all 0.2s;
    }
    .stat-card:hover { transform: translateY(-2px); box-shadow: 0 8px 16px -4px rgba(0,0,0,0.06); }
    .stat-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px; }
    .stat-label { font-size: 13px; font-weight: 600; color: #64748b; text-transform: uppercase; letter-spacing: 0.5px; }
    .stat-icon { font-size: 18px; padding: 6px; border-radius: 8px; }
    .icon-po { color: #2563eb; background: #eff6ff; }
    .icon-backlog { color: #d97706; background: #fffbeb; }
    .icon-receipt { color: #8b5cf6; background: #f5f3ff; }
    .icon-spend { color: #10955d; background: #ecfdf5; }
    .stat-value { font-size: 26px; font-weight: 700; color: #0f172a; line-height: 1.2; }
    .stat-sub { font-size: 12px; color: #94a3b8; margin-top: 4px; }
    .table-card { border-radius: 12px; border: 1px solid #e2e8f0; overflow: hidden; }
    .po-link { color: #10955d; font-weight: 600; }
    .po-link:hover { text-decoration: underline; color: #0c7347; }
    .vendor-cell { display: flex; flex-direction: column; }
    .vendor-name { font-weight: 600; color: #1e293b; }
    .vendor-ref { font-size: 12px; color: #64748b; }
    .action-buttons { display: flex; gap: 4px; align-items: center; }
    .lines-header { display: flex; justify-content: space-between; align-items: center; margin: 16px 0 8px; border-top: 1px solid #e2e8f0; padding-top: 16px; }
    .lines-header h3 { margin: 0; font-size: 15px; font-weight: 600; color: #1e293b; }
    .lines-table { width: 100%; border-collapse: collapse; font-size: 13px; }
    .lines-table th { background: #f8fafc; padding: 8px; text-align: left; font-weight: 600; color: #475569; border: 1px solid #e2e8f0; }
    .lines-table td { padding: 4px; border: 1px solid #e2e8f0; vertical-align: middle; }
    .line-calc { text-align: right; padding-right: 8px; font-weight: 600; color: #0f172a; }
    .po-summary-panel { margin-top: 16px; margin-left: auto; width: 280px; background: #f8fafc; border-radius: 8px; padding: 12px; border: 1px solid #e2e8f0; }
    .summary-line { display: flex; justify-content: space-between; margin-bottom: 6px; font-size: 13px; color: #475569; }
    .total-highlight { border-top: 1px solid #cbd5e1; padding-top: 6px; margin-top: 6px; font-weight: 700; font-size: 15px; color: #0f172a; }
    .drawer-footer { display: flex; justify-content: flex-end; gap: 8px; margin-top: 24px; border-top: 1px solid #f1f5f9; padding-top: 16px; }
  `],
})
export class PurchaseOrdersListPage {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly ordersApi = inject(PurchaseOrdersApi);
  private readonly kpiApi = inject(PurchasingKpiApi);
  private readonly entitiesApi = inject(LegalEntitiesApi);

  readonly list = createPagedList((q) => this.ordersApi.list(q), { sort: 'number' });
  readonly kpis = signal<PurchasingKpiDto | null>(null);
  readonly entities = signal<{ id: string; name: string }[]>([]);
  readonly drawerVisible = signal(false);
  readonly isSubmitting = signal(false);

  readonly form: FormGroup = this.fb.group({
    legalEntityId: ['', Validators.required],
    number: [''],
    vendorName: ['', Validators.required],
    vendorReference: [''],
    orderDate: [new Date(), Validators.required],
    expectedDeliveryDate: [null],
    currency: ['AED', Validators.required],
    paymentTerms: ['Net 30'],
    deliveryAddress: [''],
    notes: [''],
    lines: this.fb.array([]),
  });

  get linesArray(): FormArray {
    return this.form.get('lines') as FormArray;
  }

  readonly columns: TableColumn<PurchaseOrderSummaryDto>[] = [
    { key: 'number', label: 'PO #', sortField: 'number', width: '130px' },
    { key: 'vendorName', label: 'Vendor / Supplier', sortField: 'vendorName' },
    { key: 'orderDate', label: 'Order Date', sortField: 'orderDate', width: '120px' },
    { key: 'expectedDeliveryDate', label: 'Expected Due', sortField: 'expectedDeliveryDate', width: '120px' },
    { key: 'totalAmount', label: 'Total Amount', sortField: 'totalAmount', width: '150px' },
    { key: 'status', label: 'Status', sortField: 'status', width: '140px' },
    { key: 'invoiced', label: 'Bill', width: '110px' },
    { key: 'actions', label: 'Actions', width: '130px' },
  ];

  readonly calculatedTotals = computed(() => {
    let sub = 0;
    let tax = 0;
    const lines = this.linesArray.value as { quantity: number; unitPrice: number; discountPercent: number; taxRate: number }[];
    for (const l of lines) {
      const base = (l.quantity || 0) * (l.unitPrice || 0);
      const disc = base * ((l.discountPercent || 0) / 100);
      const afterDisc = base - disc;
      const taxAmt = afterDisc * ((l.taxRate || 0) / 100);
      sub += afterDisc;
      tax += taxAmt;
    }
    return { subTotal: sub, taxTotal: tax, total: sub + tax };
  });

  constructor() {
    this.loadKpis();
    this.loadEntities();
  }

  loadKpis(): void {
    this.kpiApi.getSummary().subscribe({
      next: (k) => this.kpis.set(k),
      error: () => {},
    });
  }

  loadEntities(): void {
    this.entitiesApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: (res) => this.entities.set(res.items.map((x) => ({ id: x.id, name: x.name }))),
      error: () => {},
    });
  }

  openCreateDrawer(): void {
    this.form.reset({
      legalEntityId: this.entities()[0]?.id ?? '',
      number: '',
      vendorName: '',
      vendorReference: '',
      orderDate: new Date(),
      expectedDeliveryDate: null,
      currency: 'AED',
      paymentTerms: 'Net 30',
      deliveryAddress: 'Central Warehouse',
      notes: '',
    });
    this.linesArray.clear();
    this.addLine();
    this.drawerVisible.set(true);
  }

  addLine(): void {
    this.linesArray.push(
      this.fb.group({
        itemCode: [''],
        description: ['', Validators.required],
        quantity: [1, [Validators.required, Validators.min(0.01)]],
        unitOfMeasure: ['EA'],
        unitPrice: [0, [Validators.required, Validators.min(0)]],
        discountPercent: [0, [Validators.min(0), Validators.max(100)]],
        taxRate: [5, [Validators.min(0), Validators.max(100)]],
      })
    );
  }

  removeLine(index: number): void {
    this.linesArray.removeAt(index);
  }

  calculateLineTotal(val: { quantity: number; unitPrice: number; discountPercent: number; taxRate: number }): number {
    const base = (val.quantity || 0) * (val.unitPrice || 0);
    const disc = base * ((val.discountPercent || 0) / 100);
    const afterDisc = base - disc;
    const tax = afterDisc * ((val.taxRate || 0) / 100);
    return afterDisc + tax;
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

  submitForm(): void {
    if (this.form.invalid) {
      Object.values(this.form.controls).forEach((c) => {
        c.markAsDirty();
        c.updateValueAndValidity({ onlySelf: true });
      });
      return;
    }

    this.isSubmitting.set(true);
    const raw = this.form.value;
    const input: PurchaseOrderInput = {
      legalEntityId: raw.legalEntityId,
      number: raw.number || null,
      vendorName: raw.vendorName,
      vendorReference: raw.vendorReference || null,
      orderDate: this.formatDate(raw.orderDate),
      expectedDeliveryDate: raw.expectedDeliveryDate ? this.formatDate(raw.expectedDeliveryDate) : null,
      currency: raw.currency,
      paymentTerms: raw.paymentTerms || null,
      deliveryAddress: raw.deliveryAddress || null,
      notes: raw.notes || null,
      lines: raw.lines,
    };

    this.ordersApi.create(input).subscribe({
      next: (created) => {
        this.isSubmitting.set(false);
        this.drawerVisible.set(false);
        this.message.success(`Purchase Order ${created.number} created.`);
        this.list.reload();
        this.loadKpis();
        this.router.navigate(['/purchasing/orders', created.id]);
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.message.error(err?.error?.message ?? 'Failed to create purchase order');
      },
    });
  }

  deleteOrder(id: string): void {
    this.ordersApi.delete(id).subscribe({
      next: () => {
        this.message.success('Purchase order deleted');
        this.list.reload();
        this.loadKpis();
      },
      error: (err) => this.message.error(err?.error?.message ?? 'Failed to delete order'),
    });
  }

  private formatDate(d: Date | string): string {
    const date = new Date(d);
    return date.toISOString().split('T')[0];
  }
}
