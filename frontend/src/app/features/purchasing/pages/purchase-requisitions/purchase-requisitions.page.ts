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
  template: `
    <erp-paged-table
      [list]="list"
      [columns]="columns"
      searchPlaceholder="Search requisition code, requester, department..."
      emptyText="No requisitions yet. Create one to request items."
      exportFilename="purchase-requisitions"
      (rowClick)="viewItems($event)">
      <button toolbar *erpHasPermission="'purchasing.requisitions.manage'" nz-button nzType="primary" (click)="openCreateDrawer()">
        <span nz-icon nzType="plus"></span>New Requisition
      </button>
          
          <ng-template #cell let-row let-column="column" let-value="value">
            @if (column.key === 'number') {
              <strong class="nowrap">{{ row.number }}</strong>
            } @else if (column.key === 'requesterName') {
              <div class="requester-cell">
                <span class="requester-name">{{ row.requesterName }}</span>
                @if (row.department) {
                  <span class="dept-text">{{ row.department }}</span>
                }
              </div>
            } @else if (column.key === 'estimatedTotal') {
              <strong class="amount">{{ currency() }} {{ row.estimatedTotal | number:'1.2-2' }}</strong>
            } @else if (column.key === 'status') {
              <erp-status [value]="row.status" />@if (row.status === 'pendingApproval') { <erp-approval-state documentType="purchase_requisition" [documentId]="row.id" /> }
            } @else if (column.key === 'actions') {
              <div class="action-buttons" (click)="$event.stopPropagation()">
                @if (row.status === 'draft') {
                  <button *erpHasPermission="'purchasing.requisitions.manage'" nz-button nzType="link" nzSize="small" (click)="setStatus(row, 'pendingApproval')">
                    Submit
                  </button>
                }
                @if (row.status === 'draft' || row.status === 'pendingApproval') {
                  <button *erpHasPermission="'purchasing.requisitions.manage'" nz-button nzType="link" nzSize="small" (click)="setStatus(row, 'approved')">
                    Approve
                  </button>
                  <button *erpHasPermission="'purchasing.requisitions.manage'" nz-button nzType="link" nzDanger nzSize="small"
                          nz-popconfirm nzPopconfirmTitle="Reject this requisition?" (nzOnConfirm)="setStatus(row, 'rejected')">
                    Reject
                  </button>
                }
                @if (row.status === 'rejected') {
                  <button *erpHasPermission="'purchasing.requisitions.manage'" nz-button nzType="link" nzSize="small" (click)="setStatus(row, 'draft')">
                    Back to Draft
                  </button>
                }
                @if (row.status === 'approved' && !row.convertedPurchaseOrderId) {
                  <button *erpHasPermission="'purchasing.orders.manage'" nz-button nzType="link" nzSize="small" (click)="openConvertModal(row)">
                    Create PO
                  </button>
                }
                @if (row.convertedPurchaseOrderId) {
                  <a [routerLink]="['/purchasing/orders', row.convertedPurchaseOrderId]" nz-button nzType="link" nzSize="small">
                    View PO
                  </a>
                }
                @if (row.status === 'draft') {
                  <button *erpHasPermission="'purchasing.requisitions.manage'" nz-button nzType="link" nzDanger nzSize="small"
                          nz-popconfirm nzPopconfirmTitle="Delete this requisition?" (nzOnConfirm)="deleteRequisition(row)">
                    Delete
                  </button>
                }
              </div>
            } @else {
              {{ value }}
            }
          </ng-template>
        </erp-paged-table>

      <!-- Create Drawer -->
      <nz-drawer
        [nzVisible]="drawerVisible()"
        nzTitle="New Purchase Requisition"
        [nzWidth]="800"
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
                  <nz-form-label>Requisition Code</nz-form-label>
                  <nz-form-control>
                    <input nz-input formControlName="number" placeholder="PR-2026-0001" />
                  </nz-form-control>
                </nz-form-item>
              </div>
            </div>

            <div nz-row [nzGutter]="16">
              <div nz-col [nzSpan]="12">
                <nz-form-item>
                  <nz-form-label nzRequired>Requester Name</nz-form-label>
                  <nz-form-control nzErrorTip="Requester name is required">
                    <input nz-input formControlName="requesterName" placeholder="Employee Name" />
                  </nz-form-control>
                </nz-form-item>
              </div>
              <div nz-col [nzSpan]="12">
                <nz-form-item>
                  <nz-form-label>Department</nz-form-label>
                  <nz-form-control>
                    <input nz-input formControlName="department" placeholder="e.g. IT, Operations, Marketing" />
                  </nz-form-control>
                </nz-form-item>
              </div>
            </div>

            <div nz-row [nzGutter]="16">
              <div nz-col [nzSpan]="12">
                <nz-form-item>
                  <nz-form-label nzRequired>Requisition Date</nz-form-label>
                  <nz-form-control>
                    <nz-date-picker formControlName="requisitionDate" style="width: 100%;"></nz-date-picker>
                  </nz-form-control>
                </nz-form-item>
              </div>
              <div nz-col [nzSpan]="12">
                <nz-form-item>
                  <nz-form-label>Required By Date</nz-form-label>
                  <nz-form-control>
                    <nz-date-picker formControlName="requiredDate" style="width: 100%;"></nz-date-picker>
                  </nz-form-control>
                </nz-form-item>
              </div>
            </div>

            <div class="lines-header">
              <h3>Requested Items</h3>
              <button nz-button nzType="dashed" nzSize="small" type="button" (click)="addLine()">
                <span nz-icon nzType="plus"></span>Add Item
              </button>
            </div>

            <div formArrayName="lines" class="lines-table-container">
              <table class="lines-table">
                <thead>
                  <tr>
                    <th style="width: 200px;">Item</th>
                    <th>Description</th>
                    <th style="width: 90px;">Qty</th>
                    <th style="width: 120px;">Est. Unit Price</th>
                    <th style="width: 120px;">Total</th>
                    <th style="width: 40px;"></th>
                  </tr>
                </thead>
                <tbody>
                  @for (ctrl of linesArray.controls; track $index) {
                    <tr [formGroupName]="$index">
                      <td>
                        <nz-select formControlName="itemId" nzShowSearch nzAllowClear nzPlaceHolder="Choose or leave empty" aria-label="Item" style="width: 100%;" (ngModelChange)="onItemSelected($index, $event)">
                          @for (it of items(); track it.id) {
                            <nz-option [nzValue]="it.id" [nzLabel]="it.sku + ' - ' + it.name"></nz-option>
                          }
                        </nz-select>
                      </td>
                      <td><input aria-label="Item description" nz-input formControlName="description" placeholder="Item description" /></td>
                      <td><nz-input-number formControlName="quantity" [nzMin]="1" style="width: 100%;"></nz-input-number></td>
                      <td><nz-input-number formControlName="estimatedUnitPrice" [nzMin]="0" style="width: 100%;"></nz-input-number></td>
                      <td class="line-calc">{{ (ctrl.value.quantity || 0) * (ctrl.value.estimatedUnitPrice || 0) | number:'1.2-2' }}</td>
                      <td>
                        @if (linesArray.length > 1) {
                          <button nz-button nzType="text" nzDanger nzSize="small" type="button" aria-label="Remove line" nz-tooltip nzTooltipTitle="Remove line" (click)="removeLine($index)"><span nz-icon nzType="delete"></span></button>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>

            <div nz-form-item style="margin-top: 16px;">
              <nz-form-label>Business Justification / Reason</nz-form-label>
              <nz-form-control>
                <textarea aria-label="Why are these items needed?" nz-input formControlName="reason" rows="2" placeholder="Why are these items needed?"></textarea>
              </nz-form-control>
            </div>

            <div class="drawer-footer">
              <button nz-button type="button" (click)="drawerVisible.set(false)">Cancel</button>
              <button nz-button nzType="primary" type="submit" [nzLoading]="isSubmitting()">Submit Requisition</button>
            </div>
          </form>
        </ng-container>
      </nz-drawer>

      <!-- Convert to PO Modal -->
      <nz-modal
        [(nzVisible)]="convertModalVisible"
        nzTitle="Create Purchase Order"
        nzOkText="Create PO"
        [nzOkDisabled]="!vendorId()"
        [nzOkLoading]="converting()"
        (nzOnCancel)="convertModalVisible.set(false)"
        (nzOnOk)="submitConvertToOrder()">
        <ng-container *nzModalContent>
          <label class="field-label" for="req-vendor">Vendor</label>
          <nz-select nzId="req-vendor" nzShowSearch nzPlaceHolder="Choose a vendor" style="width: 100%;" [ngModel]="vendorId()" (ngModelChange)="vendorId.set($event)" [ngModelOptions]="{ standalone: true }" data-testid="req-vendor">
            @for (v of vendors(); track v.id) {
              <nz-option [nzValue]="v.id" [nzLabel]="v.name"></nz-option>
            }
          </nz-select>
          @if (!vendors().length) {
            <p class="hint">No vendors found. Add one under Parties first.</p>
          }
        </ng-container>
      </nz-modal>

      <!-- Requested items -->
      <nz-modal
        [(nzVisible)]="itemsModalVisible"
        [nzTitle]="'Requisition ' + (detail()?.number ?? '')"
        [nzWidth]="720"
        [nzFooter]="null"
        (nzOnCancel)="itemsModalVisible.set(false)">
        <ng-container *nzModalContent>
          @if (detail(); as d) {
            <div class="req-info">
              <span><strong>Requester:</strong> {{ d.requesterName }}@if (d.department) { ({{ d.department }}) }</span>
              <span><strong>Required by:</strong> {{ (d.requiredDate | erpDate) || '-' }}</span>
            </div>
            @if (d.reason) { <p class="req-reason"><strong>Reason:</strong> {{ d.reason }}</p> }
            <nz-table #itemRows [nzData]="d.lines" [nzShowPagination]="false" nzSize="small">
              <thead>
                <tr>
                  <th>Code</th>
                  <th>Description</th>
                  <th nzAlign="right">Qty</th>
                  <th nzAlign="right">Est. Unit Price</th>
                  <th nzAlign="right">Total</th>
                </tr>
              </thead>
              <tbody>
                @for (line of itemRows.data; track line.id) {
                  <tr>
                    <td class="nowrap">{{ line.itemCode || '-' }}</td>
                    <td>{{ line.description }}</td>
                    <td nzAlign="right" class="nowrap">{{ line.quantity | number:'1.0-2' }} {{ line.unitOfMeasure }}</td>
                    <td nzAlign="right">{{ line.estimatedUnitPrice | number:'1.2-2' }}</td>
                    <td nzAlign="right"><strong>{{ line.lineTotal | number:'1.2-2' }}</strong></td>
                  </tr>
                }
              </tbody>
            </nz-table>
          }
        </ng-container>
      </nz-modal>
  `,
  styles: [`
    .nowrap { white-space: nowrap; }
    .amount { font-variant-numeric: tabular-nums; white-space: nowrap; }
    .field-label { display: block; margin-bottom: 8px; font-weight: 600; }
    .hint { margin: 8px 0 0; color: var(--erp-color-text-muted); }
    .req-info { display: flex; flex-wrap: wrap; gap: 8px 24px; margin-bottom: 8px; }
    .req-reason { margin: 0 0 12px; }
    .requester-cell { display: flex; flex-direction: column; }
    .requester-name { font-weight: 600; color: var(--erp-color-text-primary); }
    .dept-text { font-size: 12px; color: var(--erp-color-text-muted); }
    .action-buttons { display: flex; gap: 4px; align-items: center; }
    .lines-header { display: flex; justify-content: space-between; align-items: center; margin: 16px 0 8px; border-top: 1px solid var(--erp-color-border-subtle); padding-top: 16px; }
    .lines-header h3 { margin: 0; font-size: 15px; font-weight: 600; color: var(--erp-color-text-primary); }
    .lines-table { width: 100%; border-collapse: collapse; font-size: 13px; }
    .lines-table th { background: var(--erp-color-bg-subtle); padding: 8px; text-align: left; font-weight: 600; color: var(--erp-color-text-secondary); border: 1px solid var(--erp-color-border-subtle); }
    .lines-table td { padding: 4px; border: 1px solid var(--erp-color-border-subtle); vertical-align: middle; }
    .line-calc { text-align: end; padding-inline-end: 8px; font-weight: 600; color: var(--erp-color-text-primary); }
    .drawer-footer { display: flex; justify-content: flex-end; gap: 8px; margin-top: 24px; border-top: 1px solid var(--erp-color-border-subtle); padding-top: 16px; }
  `],
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
