import { doneText } from '@core/format/done-text';
import { DecimalPipe } from '@angular/common';
import { Router } from '@angular/router';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { LegalEntitiesApi } from '@core/api/admin.api';
import {
  BomSummaryDto,
  ManufacturingBomsApi,
  ManufacturingWorkOrdersApi,
  WorkOrderSummaryDto,
} from '@core/api/manufacturing.api';
import { createPagedList } from '@core/api/paged-list';
import { toIsoDate } from '@core/custom-fields/custom-field-form';
import { problemMessage } from '@core/http/problem-details';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { SessionStore } from '@core/session/session.store';
import { WarehouseRow, WarehousesApi } from '@core/api/inventory.api';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { askReason } from '@shared/utils/reason-prompt';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';

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
    NzFormModule,
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
  templateUrl: './work-orders.page.html',
  styleUrl: './work-orders.page.scss',
})
export class WorkOrdersPageComponent implements OnInit {
  private readonly api = inject(ManufacturingWorkOrdersApi);
  private readonly bomsApi = inject(ManufacturingBomsApi);
  private readonly warehousesApi = inject(WarehousesApi);
  private readonly legalEntitiesApi = inject(LegalEntitiesApi);
  private readonly session = inject(SessionStore);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly router = inject(Router);
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
    completionDate: [new Date() as Date | null],
    notes: [''],
  });

  readonly columns: TableColumn<WorkOrderSummaryDto>[] = [
    { key: 'workOrderNumber', label: 'Number', sortField: 'workOrderNumber', width: '160px' },
    { key: 'producedItemName', label: 'Product' },
    { key: 'plannedQuantity', label: 'Planned', align: 'right', value: (r) => `${r.plannedQuantity} ${r.uom}` },
    { key: 'completedQuantity', label: 'Produced', align: 'right' },
    { key: 'status', label: 'Status', filterField: 'status', filterOptions: ['draft', 'planned', 'inProgress', 'completed', 'cancelled', 'reversed'] },
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

  open(row: WorkOrderSummaryDto): void { void this.router.navigate(['/manufacturing/workorders', row.id]); }

  /** Cancelling is refused once something was produced. */
  readonly notFuture = (d: Date): boolean => d.getTime() > Date.now();

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
      Object.values(this.woForm.controls).forEach((c) => c.markAsDirty());
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
        this.message.success(doneText('created', 'Work order', this.boms().find((b) => b.id === v.bomId)?.name));
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
        this.message.success(doneText('released', 'Work order', row.workOrderNumber));
        this.list.reload();
      },
      error: (err) => this.message.error(problemMessage(err, 'The work order could not be released')),
    });
  }

  openCompleteModal(row: WorkOrderSummaryDto): void {
    this.selectedOrder.set(row);
    this.completeForm.reset({ completedQuantity: row.plannedQuantity, scrappedQuantity: 0, completionDate: new Date(), notes: '' });
    this.completeModalVisible.set(true);
  }

  submitCompleteOrder(): void {
    const order = this.selectedOrder();
    if (!order || this.completeForm.invalid) return;
    const v = this.completeForm.value;
    this.isCompleting.set(true);
    this.api.complete(order.id, {
      completedQuantity: v.completedQuantity,
      scrappedQuantity: v.scrappedQuantity || 0,
      actualTotalCost: order.estimatedTotalCost,
      completionDate: v.completionDate ? toIsoDate(v.completionDate) : null,
      notes: v.notes || null,
    }).subscribe({
      next: () => {
        this.message.success(doneText('completed', 'Work order', order.workOrderNumber));
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
          this.message.success(doneText('cancelled', 'Work order', row.workOrderNumber));
          this.list.reload();
        },
        error: (err) => this.message.error(problemMessage(err, 'The work order could not be cancelled')),
      });
    });
  }
}
