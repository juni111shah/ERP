import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzBadgeModule } from 'ng-zorro-antd/badge';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { LegalEntitiesApi } from '../../core/api/admin.api';
import {
  BomSummaryDto,
  ManufacturingBomsApi,
  ManufacturingWorkOrdersApi,
  WorkOrderDto
} from '../../core/api/manufacturing.api';
import { createPagedList } from '../../core/api/paged-list';
import { WarehousesApi } from '../../modules/inventory/inventory.api';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';

@Component({
  selector: 'erp-work-orders-page',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    NzCardModule,
    NzButtonModule,
    NzIconModule,
    NzTagModule,
    NzBadgeModule,
    NzModalModule,
    NzDrawerModule,
    NzFormModule,
    NzInputModule,
    NzInputNumberModule,
    NzDatePickerModule,
    NzSelectModule,
    PagedTableComponent
  ],
  template: `
    <div class="page-container">
      <div class="page-header">
        <div>
          <h2>Manufacturing Work Orders</h2>
          <p class="text-secondary">Schedule production runs, release shop-floor jobs, track progress, and record actual yields</p>
        </div>
        <button nz-button nzType="primary" (click)="openCreateDrawer()">
          <span nz-icon nzType="plus"></span> New Work Order
        </button>
      </div>

      <nz-card [nzBordered]="false" class="mb-4">
        <erp-paged-table
          #table
          [list]="list"
          [columns]="columns">
          <ng-template #cell let-row let-column="column">
            <ng-container [ngSwitch]="column.key">
              <span *ngSwitchCase="'workOrderNumber'" class="mono-code font-bold">{{ row.workOrderNumber }}</span>
              <span *ngSwitchCase="'bomCode'">
                <nz-tag nzColor="blue">{{ row.bomCode }}</nz-tag>
              </span>
              <span *ngSwitchCase="'producedItemName'">
                <span>{{ row.producedItemName }}</span>
              </span>
              <span *ngSwitchCase="'plannedQuantity'">
                <span>{{ row.plannedQuantity }} {{ row.uom }}</span>
              </span>
              <span *ngSwitchCase="'completedQuantity'">
                <span class="text-success font-medium">{{ row.completedQuantity }}</span>
              </span>
              <span *ngSwitchCase="'status'">
                <nz-tag [nzColor]="row.status === 'Completed' ? 'success' : row.status === 'InProgress' ? 'processing' : row.status === 'Released' ? 'warning' : 'default'">
                  {{ row.status }}
                </nz-tag>
              </span>
              <span *ngSwitchCase="'dueDate'">
                <span>{{ row.dueDate || '-' }}</span>
              </span>
              <span *ngSwitchCase="'totalPlannedCost'">
                <span>\${{ row.totalPlannedCost | number:'1.2-2' }}</span>
              </span>
              <span *ngSwitchCase="'actualTotalCost'">
                <strong>\${{ row.actualTotalCost | number:'1.2-2' }}</strong>
              </span>
              <span *ngSwitchCase="'actions'">
                <div class="table-actions">
                  <button
                    *ngIf="row.status === 'Planned'"
                    nz-button nzType="link" nzSize="small"
                    (click)="releaseOrder(row)">
                    <span nz-icon nzType="play-circle"></span> Release
                  </button>
                  <button
                    *ngIf="row.status === 'Released' || row.status === 'InProgress'"
                    nz-button nzType="link" nzSize="small" class="text-success"
                    (click)="openCompleteModal(row)">
                    <span nz-icon nzType="check-circle"></span> Complete
                  </button>
                  <button
                    *ngIf="row.status !== 'Completed' && row.status !== 'Cancelled'"
                    nz-button nzType="link" nzDanger nzSize="small"
                    (click)="cancelOrder(row)">
                    <span nz-icon nzType="close-circle"></span> Cancel
                  </button>
                </div>
              </span>
            </ng-container>
          </ng-template>
        </erp-paged-table>
      </nz-card>

      <!-- Create Work Order Drawer -->
      <nz-drawer
        [nzVisible]="createVisible()"
        [nzWidth]="620"
        nzTitle="Schedule Production Work Order"
        (nzOnClose)="createVisible.set(false)">
        <ng-container *nzDrawerContent>
          <form [formGroup]="woForm" nz-form nzLayout="vertical" (ngSubmit)="saveWorkOrder()">
            <nz-form-item>
              <nz-form-label nzRequired>Legal Entity</nz-form-label>
              <nz-form-control nzErrorTip="Please select a legal entity">
                <nz-select formControlName="legalEntityId" nzPlaceHolder="Select Entity" (ngModelChange)="onEntityChange()">
                  <nz-option *ngFor="let e of legalEntities()" [nzValue]="e.id" [nzLabel]="e.name"></nz-option>
                </nz-select>
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-label nzRequired>Bill of Materials (BOM Recipe)</nz-form-label>
              <nz-form-control nzErrorTip="Please select a BOM">
                <nz-select formControlName="bomId" nzPlaceHolder="Select BOM Recipe">
                  <nz-option *ngFor="let b of availableBoms()" [nzValue]="b.id" [nzLabel]="b.code + ' - ' + b.name"></nz-option>
                </nz-select>
              </nz-form-control>
            </nz-form-item>

            <div class="grid grid-cols-2 gap-4">
              <nz-form-item>
                <nz-form-label nzRequired>Planned Production Quantity</nz-form-label>
                <nz-form-control nzErrorTip="Valid quantity required">
                  <nz-input-number formControlName="plannedQuantity" [nzMin]="1" class="w-full"></nz-input-number>
                </nz-form-control>
              </nz-form-item>

              <nz-form-item>
                <nz-form-label nzRequired>Target Warehouse</nz-form-label>
                <nz-form-control nzErrorTip="Select warehouse">
                  <nz-select formControlName="warehouseId" nzPlaceHolder="Select Warehouse">
                    <nz-option *ngFor="let w of warehouses()" [nzValue]="w.id" [nzLabel]="w.name"></nz-option>
                  </nz-select>
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="grid grid-cols-2 gap-4">
              <nz-form-item>
                <nz-form-label nzRequired>Planned Start Date</nz-form-label>
                <nz-form-control>
                  <nz-date-picker formControlName="startDate" class="w-full"></nz-date-picker>
                </nz-form-control>
              </nz-form-item>

              <nz-form-item>
                <nz-form-label nzRequired>Target Due Date</nz-form-label>
                <nz-form-control>
                  <nz-date-picker formControlName="dueDate" class="w-full"></nz-date-picker>
                </nz-form-control>
              </nz-form-item>
            </div>

            <nz-form-item>
              <nz-form-label>Production Notes & Instructions</nz-form-label>
              <nz-form-control>
                <textarea nz-input formControlName="notes" rows="3" placeholder="Batch routing guidelines, quality tolerances..."></textarea>
              </nz-form-control>
            </nz-form-item>

            <div class="drawer-footer">
              <button nz-button type="button" (click)="createVisible.set(false)">Cancel</button>
              <button nz-button nzType="primary" type="submit" [nzLoading]="isSubmitting()">Dispatch Order</button>
            </div>
          </form>
        </ng-container>
      </nz-drawer>

      <!-- Complete Work Order Modal -->
      <nz-modal
        [nzVisible]="completeModalVisible()"
        nzTitle="Record Work Order Completion"
        (nzOnCancel)="completeModalVisible.set(false)"
        (nzOnOk)="submitCompleteOrder()">
        <ng-container *nzModalContent>
          <form [formGroup]="completeForm" nz-form nzLayout="vertical">
            <div *ngIf="selectedOrder() as order" class="mb-4 order-summary-box">
              <p><strong>Order:</strong> {{ order.workOrderNumber }}</p>
              <p><strong>Planned Qty:</strong> {{ order.plannedQuantity }} {{ order.uom }}</p>
              <p><strong>Planned Cost:</strong> \${{ order.totalPlannedCost | number:'1.2-2' }}</p>
            </div>

            <nz-form-item>
              <nz-form-label nzRequired>Good Finished Units</nz-form-label>
              <nz-form-control>
                <nz-input-number formControlName="completedQuantity" [nzMin]="0" class="w-full"></nz-input-number>
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-label>Scrapped Units</nz-form-label>
              <nz-form-control>
                <nz-input-number formControlName="scrappedQuantity" [nzMin]="0" class="w-full"></nz-input-number>
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-label nzRequired>Actual Total Production Cost (\$)</nz-form-label>
              <nz-form-control>
                <nz-input-number formControlName="actualTotalCost" [nzMin]="0" class="w-full"></nz-input-number>
              </nz-form-control>
            </nz-form-item>
          </form>
        </ng-container>
      </nz-modal>
    </div>
  `,
  styles: [`
    .page-container { padding: 1.5rem; }
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1.5rem; }
    .page-header h2 { margin: 0; font-size: 1.5rem; font-weight: 600; }
    .text-secondary { color: var(--erp-text-secondary, #64748b); }
    .text-success { color: #10b981; }
    .order-summary-box { background: var(--erp-bg-secondary, #f8fafc); padding: 0.75rem; border-radius: 6px; }
    .order-summary-box p { margin: 0 0 0.25rem 0; font-size: 0.9rem; }
    .drawer-footer { display: flex; justify-content: flex-end; gap: 0.75rem; margin-top: 2rem; }
    .grid { display: grid; }
    .grid-cols-2 { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    .gap-4 { gap: 1rem; }
    .w-full { width: 100%; }
    .mb-4 { margin-bottom: 1rem; }
  `]
})
export class WorkOrdersPageComponent implements OnInit {
  private readonly api = inject(ManufacturingWorkOrdersApi);
  private readonly bomsApi = inject(ManufacturingBomsApi);
  private readonly warehousesApi = inject(WarehousesApi);
  private readonly legalEntitiesApi = inject(LegalEntitiesApi);
  private readonly message = inject(NzMessageService);
  private readonly fb = inject(FormBuilder);

  readonly list = createPagedList<WorkOrderDto>((q: any) => this.api.list(q));
  readonly legalEntities = signal<{ id: string; name: string }[]>([]);
  readonly warehouses = signal<{ id: string; name: string }[]>([]);
  readonly availableBoms = signal<BomSummaryDto[]>([]);

  readonly createVisible = signal(false);
  readonly completeModalVisible = signal(false);
  readonly isSubmitting = signal(false);
  readonly selectedOrder = signal<WorkOrderDto | null>(null);

  woForm!: FormGroup;
  completeForm!: FormGroup;

  readonly columns: TableColumn<WorkOrderDto>[] = [
    { key: 'workOrderNumber', label: 'WO Number', sortField: 'workOrderNumber' },
    { key: 'bomCode', label: 'BOM' },
    { key: 'producedItemName', label: 'Product Produced', sortField: 'producedItemName' },
    { key: 'plannedQuantity', label: 'Planned Qty' },
    { key: 'completedQuantity', label: 'Completed Qty' },
    { key: 'status', label: 'Status', sortField: 'status' },
    { key: 'dueDate', label: 'Target Due Date' },
    { key: 'totalPlannedCost', label: 'Planned Cost' },
    { key: 'actualTotalCost', label: 'Actual Cost' },
    { key: 'actions', label: 'Actions', align: 'right' }
  ];

  ngOnInit(): void {
    this.initForms();
    this.loadPrerequisites();
  }

  private initForms(): void {
    this.woForm = this.fb.group({
      legalEntityId: ['', [Validators.required]],
      bomId: ['', [Validators.required]],
      plannedQuantity: [10, [Validators.required, Validators.min(1)]],
      warehouseId: ['', [Validators.required]],
      startDate: [new Date(), [Validators.required]],
      dueDate: [new Date(Date.now() + 7 * 86400000), [Validators.required]],
      notes: ['']
    });

    this.completeForm = this.fb.group({
      completedQuantity: [0, [Validators.required, Validators.min(0)]],
      scrappedQuantity: [0],
      actualTotalCost: [0, [Validators.required, Validators.min(0)]]
    });
  }

  private loadPrerequisites(): void {
    this.legalEntitiesApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: res => {
        if (res.items) {
          this.legalEntities.set(res.items.map(e => ({ id: e.id, name: e.name })));
          if (res.items.length > 0) {
            this.woForm.patchValue({ legalEntityId: res.items[0].id });
          }
        }
      }
    });

    this.warehousesApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: res => {
        if (res.items) {
          this.warehouses.set(res.items.map(w => ({ id: w.id, name: w.name })));
          if (res.items.length > 0) {
            this.woForm.patchValue({ warehouseId: res.items[0].id });
          }
        }
      }
    });

    this.bomsApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: res => {
        if (res.items) {
          this.availableBoms.set(res.items);
          if (res.items.length > 0) {
            this.woForm.patchValue({ bomId: res.items[0].id });
          }
        }
      }
    });
  }

  onEntityChange(): void {
    // refresh list
  }

  openCreateDrawer(): void {
    this.createVisible.set(true);
  }

  saveWorkOrder(): void {
    if (this.woForm.invalid) {
      Object.values(this.woForm.controls).forEach(c => c.markAsDirty());
      return;
    }
    this.isSubmitting.set(true);
    const v = this.woForm.value;
    const start = v.startDate ? new Date(v.startDate).toISOString().slice(0, 10) : undefined;
    const due = v.dueDate ? new Date(v.dueDate).toISOString().slice(0, 10) : undefined;

    const payload = {
      legalEntityId: v.legalEntityId,
      bomId: v.bomId,
      plannedQuantity: v.plannedQuantity,
      warehouseId: v.warehouseId,
      startDate: start,
      dueDate: due,
      notes: v.notes
    };

    this.api.create(payload as any).subscribe({
      next: () => {
        this.message.success('Work order scheduled');
        this.createVisible.set(false);
        this.isSubmitting.set(false);
        this.list.reload();
      },
      error: () => {
        this.message.error('Failed to schedule work order');
        this.isSubmitting.set(false);
      }
    });
  }

  releaseOrder(row: WorkOrderDto): void {
    this.api.release(row.id).subscribe({
      next: () => {
        this.message.success('Work order released to shop floor');
        this.list.reload();
      },
      error: () => this.message.error('Failed to release work order')
    });
  }

  openCompleteModal(row: WorkOrderDto): void {
    this.selectedOrder.set(row);
    this.completeForm.patchValue({
      completedQuantity: row.plannedQuantity,
      scrappedQuantity: 0,
      actualTotalCost: row.totalPlannedCost || 100
    });
    this.completeModalVisible.set(true);
  }

  submitCompleteOrder(): void {
    if (this.completeForm.invalid) return;
    const order = this.selectedOrder();
    if (!order) return;

    const v = this.completeForm.value;
    this.api.complete(order.id, {
      completedQuantity: v.completedQuantity,
      scrappedQuantity: v.scrappedQuantity || 0,
      actualTotalCost: v.actualTotalCost
    }).subscribe({
      next: () => {
        this.message.success('Work order marked completed');
        this.completeModalVisible.set(false);
        this.list.reload();
      },
      error: () => this.message.error('Failed to complete work order')
    });
  }

  cancelOrder(row: WorkOrderDto): void {
    this.api.cancel(row.id).subscribe({
      next: () => {
        this.message.success('Work order cancelled');
        this.list.reload();
      },
      error: () => this.message.error('Failed to cancel work order')
    });
  }
}
