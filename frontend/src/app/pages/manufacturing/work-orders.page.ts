import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { LegalEntitiesApi } from '../../core/api/admin.api';
import {
  BomSummaryDto,
  ManufacturingBomsApi,
  ManufacturingWorkOrdersApi,
  WorkOrderSummaryDto,
} from '../../core/api/manufacturing.api';
import { createPagedList } from '../../core/api/paged-list';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { problemMessage } from '../../core/http/problem-details';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { SessionStore } from '../../core/session/session.store';
import { WarehouseRow, WarehousesApi } from '../../modules/inventory/inventory.api';
import { ErpDatePipe } from '../../shared/display.pipes';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { askReason } from '../../shared/reason-prompt';
import { StatusTagComponent } from '../../shared/status-tag.component';
import { ErpFormModule } from '../../core/forms/erp-form.module';
import { showFormErrors } from '../../core/forms/form-errors';

@Component({
  selector: 'erp-work-orders-page',
  standalone: true,
  imports: [
    DecimalPipe,
    ErpDatePipe,
    ReactiveFormsModule,
    NzButtonModule,
    NzIconModule,
    NzModalModule,
    NzDrawerModule,
    ErpFormModule,
    NzInputModule,
    NzInputNumberModule,
    NzDatePickerModule,
    NzPopconfirmModule,
    NzSelectModule,
    NzToolTipModule,
    PagedTableComponent,
    HasPermissionDirective,
    StatusTagComponent,
  ],
  template: `
    <erp-paged-table [list]="list" [columns]="columns" [clickable]="false" searchPlaceholder="Search by number" emptyText="No work orders yet. Schedule one to start production.">
      <button toolbar *erpHasPermission="'manufacturing.workorders.manage'" nz-button nzType="primary" (click)="openCreateDrawer()" data-testid="work-order-new">
        <span nz-icon nzType="plus"></span> New Work Order
      </button>
      <ng-template #cell let-row let-column="column" let-value="value">
        @switch (column.key) {
          @case ('workOrderNumber') { <span class="code strong">{{ row.workOrderNumber }}</span> }
          @case ('producedItemName') {
            <div>{{ row.producedItemName }}</div>
            <div class="sub">BOM <span class="code">{{ row.bomCode }}</span></div>
          }
          @case ('plannedQuantity') { <span class="num">{{ row.plannedQuantity | number:'1.0-4' }} {{ row.uom }}</span> }
          @case ('completedQuantity') { <span class="num">{{ row.completedQuantity | number:'1.0-4' }}</span> }
          @case ('status') { <erp-status [value]="row.status" /> }
          @case ('dueDate') { <span class="nowrap">{{ row.dueDate | erpDate }}</span> }
          @case ('estimatedTotalCost') { <span class="num">{{ currency() }} {{ row.estimatedTotalCost | number:'1.2-2' }}</span> }
          @case ('actualTotalCost') {
            @if (row.status === 'completed') {
              <span class="num strong">{{ currency() }} {{ row.actualTotalCost | number:'1.2-2' }}</span>
            } @else {
              <span class="muted" aria-label="Not known until completed">&mdash;</span>
            }
          }
          @case ('actions') {
            <div class="table-actions">
              <a [href]="'/print/job-card/' + row.id" target="_blank" nz-button nzType="link" nzSize="small" nz-tooltip nzTooltipTitle="Print job card" aria-label="Print job card">
                <span nz-icon nzType="printer"></span>
              </a>
              @if (row.status === 'planned') {
                <button *erpHasPermission="'manufacturing.workorders.manage'" nz-button nzType="link" nzSize="small"
                        nz-popconfirm nzPopconfirmTitle="Release this work order to production?" (nzOnConfirm)="releaseOrder(row)">
                  <span nz-icon nzType="play-circle"></span> Release
                </button>
              }
              @if (row.status === 'inProgress') {
                <button *erpHasPermission="'manufacturing.workorders.manage'" nz-button nzType="link" nzSize="small" (click)="openCompleteModal(row)">
                  <span nz-icon nzType="check-circle"></span> Complete
                </button>
              }
              @if (canCancel(row)) {
                <button *erpHasPermission="'manufacturing.workorders.manage'" nz-button nzType="link" nzDanger nzSize="small" (click)="cancelOrder(row)">
                  <span nz-icon nzType="close-circle"></span> Cancel
                </button>
              }
            </div>
          }
          @default { {{ value }} }
        }
      </ng-template>
    </erp-paged-table>

    <!-- Create Work Order Drawer -->
    <nz-drawer [nzVisible]="createVisible()" [nzWidth]="620" nzTitle="New work order" (nzOnClose)="createVisible.set(false)">
      <ng-container *nzDrawerContent>
        <form [formGroup]="woForm" nz-form nzLayout="vertical" (ngSubmit)="saveWorkOrder()">
          <nz-form-item>
            <nz-form-label nzRequired>Legal entity</nz-form-label>
            <nz-form-control nzErrorTip="Choose a legal entity">
              <nz-select formControlName="legalEntityId" nzPlaceHolder="Choose a legal entity" (ngModelChange)="onEntityChange($event)">
                @for (e of legalEntities(); track e.id) { <nz-option [nzValue]="e.id" [nzLabel]="e.name"></nz-option> }
              </nz-select>
            </nz-form-control>
          </nz-form-item>

          <nz-form-item>
            <nz-form-label nzRequired>Bill of materials</nz-form-label>
            <nz-form-control nzErrorTip="Choose a bill of materials">
              <nz-select formControlName="bomId" nzShowSearch nzPlaceHolder="Choose a bill of materials" [nzNotFoundContent]="'No active bill of materials in this legal entity'">
                @for (b of entityBoms(); track b.id) { <nz-option [nzValue]="b.id" [nzLabel]="b.code + ' - ' + b.name"></nz-option> }
              </nz-select>
            </nz-form-control>
          </nz-form-item>

          <div class="row-2">
            <nz-form-item>
              <nz-form-label nzRequired>Planned quantity</nz-form-label>
              <nz-form-control nzErrorTip="Enter a quantity above zero">
                <nz-input-number formControlName="plannedQuantity" [nzMin]="1" class="full"></nz-input-number>
              </nz-form-control>
            </nz-form-item>
            <nz-form-item>
              <nz-form-label nzRequired>Warehouse</nz-form-label>
              <nz-form-control nzErrorTip="Choose a warehouse">
                <nz-select formControlName="warehouseId" nzPlaceHolder="Choose a warehouse">
                  @for (w of entityWarehouses(); track w.id) { <nz-option [nzValue]="w.id" [nzLabel]="w.name"></nz-option> }
                </nz-select>
              </nz-form-control>
            </nz-form-item>
          </div>

          <div class="row-2">
            <nz-form-item>
              <nz-form-label nzRequired>Start date</nz-form-label>
              <nz-form-control nzErrorTip="Choose a start date">
                <nz-date-picker formControlName="startDate" class="full"></nz-date-picker>
              </nz-form-control>
            </nz-form-item>
            <nz-form-item>
              <nz-form-label nzRequired>Due date</nz-form-label>
              <nz-form-control [nzErrorTip]="woForm.get('dueDate')?.hasError('beforeStart') ? 'The due date cannot be before the start date' : 'Choose a due date'">
                <nz-date-picker formControlName="dueDate" class="full"></nz-date-picker>
              </nz-form-control>
            </nz-form-item>
          </div>

          <nz-form-item>
            <nz-form-label>Notes</nz-form-label>
            <nz-form-control>
              <textarea nz-input formControlName="notes" rows="3" placeholder="Routing guidelines, quality tolerances"></textarea>
            </nz-form-control>
          </nz-form-item>

          <div class="drawer-footer">
            <button nz-button type="button" (click)="createVisible.set(false)">Cancel</button>
            <button nz-button nzType="primary" type="submit" [nzLoading]="isSubmitting()">Save work order</button>
          </div>
        </form>
      </ng-container>
    </nz-drawer>

    <!-- Complete Work Order Modal -->
    <nz-modal
      [nzVisible]="completeModalVisible()"
      [nzTitle]="'Complete ' + (selectedOrder()?.workOrderNumber ?? '')"
      nzOkText="Complete"
      [nzOkLoading]="isCompleting()"
      (nzOnCancel)="completeModalVisible.set(false)"
      (nzOnOk)="submitCompleteOrder()">
      <ng-container *nzModalContent>
        <form [formGroup]="completeForm" nz-form nzLayout="vertical">
          @if (selectedOrder(); as order) {
            <p class="intro">Planned {{ order.plannedQuantity | number:'1.0-4' }} {{ order.uom }}. Completing receives the finished goods into stock and consumes the components. The actual cost is worked out from the stock used and the labor and overhead on the bill of materials.</p>
          }
          <div class="row-2">
            <nz-form-item>
              <nz-form-label nzRequired>Quantity produced</nz-form-label>
              <nz-form-control nzErrorTip="Enter zero or more">
                <nz-input-number formControlName="completedQuantity" [nzMin]="0" class="full"></nz-input-number>
              </nz-form-control>
            </nz-form-item>
            <nz-form-item>
              <nz-form-label>Quantity scrapped</nz-form-label>
              <nz-form-control>
                <nz-input-number formControlName="scrappedQuantity" [nzMin]="0" class="full"></nz-input-number>
              </nz-form-control>
            </nz-form-item>
          </div>
          <nz-form-item>
            <nz-form-label>Notes</nz-form-label>
            <nz-form-control>
              <textarea nz-input formControlName="notes" rows="2"></textarea>
            </nz-form-control>
          </nz-form-item>
        </form>
      </ng-container>
    </nz-modal>
  `,
  styles: [`
    .code { font-family: var(--erp-font-family-mono); white-space: nowrap; }
    .strong { font-weight: 600; }
    .sub { font-size: 12px; color: var(--erp-color-text-muted); }
    .muted { color: var(--erp-color-text-muted); }
    .nowrap { white-space: nowrap; }
    .num { font-variant-numeric: tabular-nums; white-space: nowrap; }
    .table-actions { display: flex; justify-content: flex-end; gap: 4px; }
    .intro { margin: 0 0 16px; color: var(--erp-color-text-muted); }
    .drawer-footer { display: flex; justify-content: flex-end; gap: 12px; margin-block-start: 24px; }
    .row-2 { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 16px; }
    .full { width: 100%; }
  `],
})
export class WorkOrdersPageComponent implements OnInit {
  private readonly api = inject(ManufacturingWorkOrdersApi);
  private readonly bomsApi = inject(ManufacturingBomsApi);
  private readonly warehousesApi = inject(WarehousesApi);
  private readonly legalEntitiesApi = inject(LegalEntitiesApi);
  private readonly session = inject(SessionStore);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly fb = inject(FormBuilder);

  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly list = createPagedList<WorkOrderSummaryDto>((q) => this.api.list(q), { sort: '-createdAt' });
  readonly legalEntities = signal<{ id: string; name: string }[]>([]);
  private readonly warehouses = signal<WarehouseRow[]>([]);
  private readonly boms = signal<BomSummaryDto[]>([]);
  private readonly entityId = signal<string | null>(null);
  readonly entityBoms = computed(() => this.boms().filter((b) => b.isActive && b.legalEntityId === this.entityId()));
  readonly entityWarehouses = computed(() => this.warehouses().filter((w) => w.isActive && w.legalEntityId === this.entityId()));

  readonly createVisible = signal(false);
  readonly completeModalVisible = signal(false);
  readonly isSubmitting = signal(false);
  readonly isCompleting = signal(false);
  readonly selectedOrder = signal<WorkOrderSummaryDto | null>(null);

  readonly woForm: FormGroup = this.fb.group({
    legalEntityId: ['', [Validators.required]],
    bomId: [null, [Validators.required]],
    plannedQuantity: [1, [Validators.required, Validators.min(0.0001)]],
    warehouseId: [null, [Validators.required]],
    startDate: [new Date(), [Validators.required]],
    dueDate: [new Date(Date.now() + 7 * 86400000), [Validators.required]],
    notes: [''],
  });

  readonly completeForm: FormGroup = this.fb.group({
    completedQuantity: [0, [Validators.required, Validators.min(0)]],
    scrappedQuantity: [0],
    notes: [''],
  });

  readonly columns: TableColumn<WorkOrderSummaryDto>[] = [
    { key: 'workOrderNumber', label: 'Number', sortField: 'workOrderNumber', width: '160px' },
    { key: 'producedItemName', label: 'Product' },
    { key: 'plannedQuantity', label: 'Planned', align: 'right', value: (r) => `${r.plannedQuantity} ${r.uom}` },
    { key: 'completedQuantity', label: 'Produced', align: 'right' },
    { key: 'status', label: 'Status' },
    { key: 'dueDate', label: 'Due date' },
    { key: 'estimatedTotalCost', label: 'Estimated cost', align: 'right' },
    { key: 'actualTotalCost', label: 'Actual cost', align: 'right' },
    { key: 'actions', label: 'Actions', align: 'right', filter: false, width: '215px', value: () => '' },
  ];

  ngOnInit(): void {
    this.legalEntitiesApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: (res) => this.legalEntities.set(res.items.map((e) => ({ id: e.id, name: e.name }))),
    });
    this.warehousesApi.list({ page: 1, pageSize: 100 }).subscribe({
      next: (res) => this.warehouses.set(res.items),
    });
    this.bomsApi.list({ page: 1, pageSize: 200, sort: 'code' }).subscribe({
      next: (res) => this.boms.set(res.items),
    });
  }

  /** Cancelling is refused once something was produced. */
  canCancel(row: WorkOrderSummaryDto): boolean {
    return (row.status === 'draft' || row.status === 'planned' || row.status === 'inProgress') && row.completedQuantity === 0;
  }

  onEntityChange(id: string): void {
    this.entityId.set(id);
    if (!this.entityBoms().some((b) => b.id === this.woForm.value.bomId)) {
      this.woForm.patchValue({ bomId: this.entityBoms()[0]?.id ?? null });
    }
    if (!this.entityWarehouses().some((w) => w.id === this.woForm.value.warehouseId)) {
      this.woForm.patchValue({ warehouseId: this.entityWarehouses()[0]?.id ?? null });
    }
  }

  openCreateDrawer(): void {
    const entity = this.legalEntities().find((e) => e.id === this.session.currentEntityId()) ?? this.legalEntities()[0];
    this.entityId.set(entity?.id ?? null);
    this.woForm.reset({
      legalEntityId: entity?.id ?? '',
      bomId: this.entityBoms()[0]?.id ?? null,
      plannedQuantity: 1,
      warehouseId: this.entityWarehouses()[0]?.id ?? null,
      startDate: new Date(),
      dueDate: new Date(Date.now() + 7 * 86400000),
      notes: '',
    });
    this.createVisible.set(true);
  }

  saveWorkOrder(): void {
    const dueControl = this.woForm.get('dueDate')!;
    const { startDate, dueDate } = this.woForm.value;
    if (startDate && dueDate && toIsoDate(new Date(dueDate)) < toIsoDate(new Date(startDate))) {
      dueControl.setErrors({ beforeStart: true });
    } else if (dueControl.hasError('beforeStart')) {
      dueControl.updateValueAndValidity();
    }
    if (this.woForm.invalid) {
      showFormErrors(this.woForm);
      return;
    }
    this.isSubmitting.set(true);
    const v = this.woForm.value;
    this.api.create({
      legalEntityId: v.legalEntityId,
      bomId: v.bomId,
      plannedQuantity: v.plannedQuantity,
      warehouseId: v.warehouseId,
      startDate: toIsoDate(new Date(v.startDate)),
      dueDate: toIsoDate(new Date(v.dueDate)),
      notes: v.notes || null,
    }).subscribe({
      next: () => {
        this.message.success('Work order saved');
        this.createVisible.set(false);
        this.isSubmitting.set(false);
        this.list.reload();
      },
      error: (err) => {
        this.message.error(problemMessage(err, 'The work order could not be saved'));
        this.isSubmitting.set(false);
      },
    });
  }

  releaseOrder(row: WorkOrderSummaryDto): void {
    this.api.release(row.id).subscribe({
      next: () => {
        this.message.success('Work order released');
        this.list.reload();
      },
      error: (err) => this.message.error(problemMessage(err, 'The work order could not be released')),
    });
  }

  openCompleteModal(row: WorkOrderSummaryDto): void {
    this.selectedOrder.set(row);
    this.completeForm.reset({ completedQuantity: row.plannedQuantity, scrappedQuantity: 0, notes: '' });
    this.completeModalVisible.set(true);
  }

  submitCompleteOrder(): void {
    const order = this.selectedOrder();
    if (!order) return;
    if (this.completeForm.invalid) { showFormErrors(this.completeForm); return; }
    const v = this.completeForm.value;
    this.isCompleting.set(true);
    this.api.complete(order.id, {
      completedQuantity: v.completedQuantity,
      scrappedQuantity: v.scrappedQuantity || 0,
      actualTotalCost: order.estimatedTotalCost,
      notes: v.notes || null,
    }).subscribe({
      next: () => {
        this.message.success('Work order completed');
        this.isCompleting.set(false);
        this.completeModalVisible.set(false);
        this.list.reload();
      },
      error: (err) => {
        this.message.error(problemMessage(err, 'The work order could not be completed'));
        this.isCompleting.set(false);
      },
    });
  }

  cancelOrder(row: WorkOrderSummaryDto): void {
    void askReason(this.modal, `Cancel ${row.workOrderNumber}?`, 'Cancel work order', 'This cannot be undone.', false).then((reason) => {
      if (reason === null) return;
      this.api.cancel(row.id, reason).subscribe({
        next: () => {
          this.message.success('Work order cancelled');
          this.list.reload();
        },
        error: (err) => this.message.error(problemMessage(err, 'The work order could not be cancelled')),
      });
    });
  }
}
