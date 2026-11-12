import { DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
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
import { WarehouseRow, WarehousesApi } from '@core/api/inventory.api';
import { BomSummaryDto, ManufacturingBomsApi, ManufacturingWorkOrdersApi, WorkOrderSummaryDto } from '@core/api/manufacturing.api';
import { createPagedList } from '@core/api/paged-list';
import { doneText } from '@core/format/done-text';
import { problemMessage } from '@core/http/problem-details';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { SessionStore } from '@core/session/session.store';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { askReason } from '@shared/utils/reason-prompt';
import { ManufacturingLookups } from '../../manufacturing-lookups';
import {
  CompleteFormValue,
  WorkOrderFormValue,
  canCancelWorkOrder,
  dueBeforeStart,
  toCompleteInput,
  toWorkOrderInput,
} from '../../manufacturing-work-orders';

const DAY_MS = 86400000;

/** Work orders: the paged list, the new-work-order drawer, and releasing, completing or cancelling an order. */
@Component({
  selector: 'erp-work-orders-page',
  standalone: true,
  imports: [
    DecimalPipe,
    ReactiveFormsModule,
    NzButtonModule,
    NzDatePickerModule,
    NzDrawerModule,
    NzFormModule,
    NzIconModule,
    NzInputModule,
    NzInputNumberModule,
    NzModalModule,
    NzPopconfirmModule,
    NzSelectModule,
    NzToolTipModule,
    ErpDatePipe,
    HasPermissionDirective,
    PagedTableComponent,
    StatusTagComponent,
  ],
  providers: [ManufacturingLookups],
  templateUrl: './work-orders.page.html',
  styleUrl: './work-orders.page.scss',
})
export class WorkOrdersPageComponent implements OnInit {
  private readonly api = inject(ManufacturingWorkOrdersApi);
  private readonly bomsApi = inject(ManufacturingBomsApi);
  private readonly warehousesApi = inject(WarehousesApi);
  private readonly lookups = inject(ManufacturingLookups);
  private readonly session = inject(SessionStore);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly list = createPagedList<WorkOrderSummaryDto>((q) => this.api.list(q), { sort: '-createdAt' });
  readonly legalEntities = this.lookups.legalEntities;
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
    dueDate: [new Date(Date.now() + 7 * DAY_MS), [Validators.required]],
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
    this.lookups.loadEntities();
    this.warehousesApi.list({ page: 1, pageSize: 100 }).subscribe({
      next: (res) => this.warehouses.set(res.items),
    });
    this.bomsApi.list({ page: 1, pageSize: 200, sort: 'code' }).subscribe({
      next: (res) => this.boms.set(res.items),
    });
  }

  open(row: WorkOrderSummaryDto): void { void this.router.navigate(['/manufacturing/workorders', row.id]); }

  /** A completion date cannot be in the future. */
  readonly notFuture = (d: Date): boolean => d.getTime() > Date.now();

  /** Cancelling is refused once something was produced. */
  canCancel(row: WorkOrderSummaryDto): boolean {
    return canCancelWorkOrder(row);
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
    const entity = this.lookups.defaultEntity(this.session.currentEntityId());
    this.entityId.set(entity?.id ?? null);
    this.woForm.reset({
      legalEntityId: entity?.id ?? '',
      bomId: this.entityBoms()[0]?.id ?? null,
      plannedQuantity: 1,
      warehouseId: this.entityWarehouses()[0]?.id ?? null,
      startDate: new Date(),
      dueDate: new Date(Date.now() + 7 * DAY_MS),
      notes: '',
    });
    this.createVisible.set(true);
  }

  saveWorkOrder(): void {
    this.checkDueDate();
    if (this.woForm.invalid) {
      Object.values(this.woForm.controls).forEach((c) => c.markAsDirty());
      return;
    }
    this.isSubmitting.set(true);
    const v = this.woForm.value as WorkOrderFormValue;
    this.api.create(toWorkOrderInput(v)).subscribe({
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
    this.isCompleting.set(true);
    this.api.complete(order.id, toCompleteInput(this.completeForm.value as CompleteFormValue, order)).subscribe({
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

  /** A due date before the start date is an error on the due date; once fixed, the error is cleared. */
  private checkDueDate(): void {
    const dueControl = this.woForm.get('dueDate')!;
    const { startDate, dueDate } = this.woForm.value as WorkOrderFormValue;
    if (dueBeforeStart(startDate, dueDate)) {
      dueControl.setErrors({ beforeStart: true });
    } else if (dueControl.hasError('beforeStart')) {
      dueControl.updateValueAndValidity();
    }
  }
}
