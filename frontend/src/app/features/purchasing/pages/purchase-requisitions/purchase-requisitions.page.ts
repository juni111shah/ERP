import { doneText } from '@core/format/done-text';
import { CommonModule, DecimalPipe } from '@angular/common';
import { problemMessage } from '@core/http/problem-details';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
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
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { ItemDto, ItemsApi, LegalEntitiesApi } from '@core/api/admin.api';
import { displayLabel } from '@core/format/display';
import { createPagedList } from '@core/api/paged-list';
import { PurchaseRequisitionStatus, PurchaseRequisitionsApi, RequisitionDto, RequisitionInput, RequisitionSummaryDto } from '@core/api/purchasing.api';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { ApprovalStateComponent } from '@shared/components/approval-state/approval-state.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { PartiesApi } from '@features/parties/pages/parties/parties.page';
import { ErpDatePipe } from '@shared/pipes/display.pipes';

@Component({
  selector: 'erp-purchase-requisitions',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ApprovalStateComponent, StatusTagComponent, ErpDatePipe, NzToolTipModule,
    CommonModule,
    DecimalPipe,
    FormsModule,
    ReactiveFormsModule,
    RouterModule,
    NzGridModule,
    NzCardModule,
    NzButtonModule,
    NzIconModule,
    NzDrawerModule,
    NzModalModule,
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
  templateUrl: './purchase-requisitions.page.html',
  styleUrl: './purchase-requisitions.page.scss',
})
export class PurchaseRequisitionsPage {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly reqsApi = inject(PurchaseRequisitionsApi);
  private readonly entitiesApi = inject(LegalEntitiesApi);
  private readonly message = inject(NzMessageService);
  private readonly currencyService = inject(WorkspaceCurrencyService);
  private readonly partiesApi = inject(PartiesApi);
  private readonly itemsApi = inject(ItemsApi);

  readonly currency = this.currencyService.currency;
  readonly list = createPagedList((q) => this.reqsApi.list(q), { sort: 'number' });
  readonly entities = signal<{ id: string; name: string }[]>([]);
  readonly drawerVisible = signal(false);
  readonly isSubmitting = signal(false);

  readonly convertModalVisible = signal(false);
  readonly selectedReq = signal<RequisitionSummaryDto | null>(null);
  readonly items = signal<ItemDto[]>([]);
  readonly vendors = signal<{ id: string; name: string }[]>([]);
  readonly vendorId = signal<string | null>(null);
  readonly converting = signal(false);
  readonly itemsModalVisible = signal(false);
  readonly detail = signal<RequisitionDto | null>(null);

  readonly form: FormGroup = this.fb.group({
    legalEntityId: ['', Validators.required],
    number: [''],
    requesterName: ['', Validators.required],
    department: [''],
    requisitionDate: [new Date(), Validators.required],
    requiredDate: [null],
    reason: [''],
    lines: this.fb.array([]),
  });

  get linesArray(): FormArray {
    return this.form.get('lines') as FormArray;
  }

  readonly columns: TableColumn<RequisitionSummaryDto>[] = [
    { key: 'number', label: 'Requisition #', sortField: 'number', width: '140px' },
    { key: 'requesterName', label: 'Requester / Dept' },
    { key: 'requisitionDate', label: 'Req Date', sortField: 'requisitionDate', width: '120px', filterField: 'requisitionDate' },
    { key: 'requiredDate', label: 'Required By', sortField: 'requiredDate', width: '120px', filterField: 'requiredDate' },
    { key: 'estimatedTotal', label: 'Est. Total', sortField: 'estimatedTotal', width: '150px', align: 'right', filter: false },
    { key: 'status', label: 'Status', width: '130px', filterField: 'status', filterOptions: ['draft', 'pendingApproval', 'approved', 'rejected', 'ordered'] },
    { key: 'actions', label: 'Actions', width: '220px' },
  ];

  constructor() {
    this.loadEntities();
    this.loadVendors();
    this.itemsApi.list({ page: 1, pageSize: 200, filters: [{ field: 'isActive', op: 'eq', value: true }] }).subscribe({
      next: (res) => this.items.set(res.items),
      error: () => {},
    });
  }

  /** A line linked to an item keeps the link on the purchase order, so receiving it moves stock. */
  onItemSelected(index: number, itemId: string | null): void {
    const it = this.items().find((x) => x.id === itemId);
    const line = this.linesArray.at(index);
    if (!it) {
      line.patchValue({ itemCode: '' });
      return;
    }
    line.patchValue({
      itemCode: it.sku,
      description: it.name,
      unitOfMeasure: it.uom || 'EA',
      estimatedUnitPrice: it.purchasePrice ?? line.value.estimatedUnitPrice ?? 0,
    });
  }

  private loadVendors(): void {
    this.partiesApi.list({ page: 1, pageSize: 200 }).subscribe({
      next: (res) => {
        const vendors = res.items.filter((p) => p.roles?.some((r) => r.toLowerCase() === 'vendor' || r.toLowerCase() === 'supplier'));
        this.vendors.set(vendors.map((p) => ({ id: p.id, name: p.name })));
      },
      error: () => {},
    });
  }

  viewItems(row: RequisitionSummaryDto): void {
    this.reqsApi.get(row.id).subscribe({
      next: (d) => {
        this.detail.set(d);
        this.itemsModalVisible.set(true);
      },
      error: () => this.message.error('Could not load the requested items.'),
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
      requesterName: '',
      department: '',
      requisitionDate: new Date(),
      requiredDate: null,
      reason: '',
    });
    this.linesArray.clear();
    this.addLine();
    this.drawerVisible.set(true);
  }

  addLine(): void {
    this.linesArray.push(
      this.fb.group({
        itemId: [null],
        itemCode: [''],
        description: ['', Validators.required],
        quantity: [1, [Validators.required, Validators.min(0.01)]],
        unitOfMeasure: ['EA'],
        estimatedUnitPrice: [0, [Validators.required, Validators.min(0)]],
      })
    );
  }

  removeLine(index: number): void {
    this.linesArray.removeAt(index);
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
    const input: RequisitionInput = {
      legalEntityId: raw.legalEntityId,
      number: raw.number || null,
      requesterName: raw.requesterName,
      department: raw.department || null,
      requisitionDate: new Date(raw.requisitionDate).toISOString().split('T')[0],
      requiredDate: raw.requiredDate ? new Date(raw.requiredDate).toISOString().split('T')[0] : null,
      reason: raw.reason || null,
      lines: raw.lines,
    };

    this.reqsApi.create(input).subscribe({
      next: (created) => {
        this.isSubmitting.set(false);
        this.drawerVisible.set(false);
        this.message.success(doneText('submitted', 'Requisition', created.number));
        this.list.reload();
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.message.error(problemMessage(err, 'Failed to create requisition'));
      },
    });
  }

  setStatus(row: RequisitionSummaryDto, status: PurchaseRequisitionStatus): void {
    this.reqsApi.setStatus(row.id, status).subscribe({
      next: () => {
        this.message.success(`${row.number} is now ${displayLabel(status).toLowerCase()}.`);
        this.list.reload();
      },
      error: (err) => this.message.error(problemMessage(err, 'Could not change the status.')),
    });
  }

  openConvertModal(req: RequisitionSummaryDto): void {
    this.selectedReq.set(req);
    this.vendorId.set(null);
    this.convertModalVisible.set(true);
  }

  submitConvertToOrder(): void {
    const req = this.selectedReq();
    const vendor = this.vendors().find((v) => v.id === this.vendorId());
    if (!req || !vendor) return;
    this.converting.set(true);
    this.reqsApi.convertToOrder(req.id, vendor.id, vendor.name).subscribe({
      next: (po) => {
        this.converting.set(false);
        this.message.success(`${req.number} converted to purchase order ${po.number}`);
        this.convertModalVisible.set(false);
        this.list.reload();
        this.router.navigate(['/purchasing/orders', po.id]);
      },
      error: (err) => {
        this.converting.set(false);
        this.message.error(problemMessage(err, 'Failed to convert to order'));
      },
    });
  }

  deleteRequisition(row: { id: string; number?: string | null }): void {
    this.reqsApi.delete(row.id).subscribe({
      next: () => {
        this.message.success(doneText('deleted', 'Requisition', row.number));
        this.list.reload();
      },
      error: (err) => this.message.error(problemMessage(err, 'Failed to delete')),
    });
  }
}
