import { CommonModule, DecimalPipe } from '@angular/common';
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
import { NzTagModule } from 'ng-zorro-antd/tag';
import { LegalEntitiesApi } from '../../core/api/admin.api';
import { createPagedList } from '../../core/api/paged-list';
import { PurchaseRequisitionInput, PurchaseRequisitionStatus, PurchaseRequisitionsApi, RequisitionInput, RequisitionSummaryDto } from '../../core/api/purchasing.api';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';

@Component({
  selector: 'erp-purchase-requisitions',
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
      searchPlaceholder="Search requisition code, requester, department...">
      <button toolbar *erpHasPermission="'purchasing.requisitions.manage'" nz-button nzType="primary" (click)="openCreateDrawer()">
        <span nz-icon nzType="plus"></span>New Requisition
      </button>
          
          <ng-template #cell let-row let-column="column">
            @if (column.key === 'number') {
              <strong>{{ row.number }}</strong>
            } @else if (column.key === 'requesterName') {
              <div class="requester-cell">
                <span class="requester-name">{{ row.requesterName }}</span>
                @if (row.department) {
                  <span class="dept-text">{{ row.department }}</span>
                }
              </div>
            } @else if (column.key === 'estimatedTotal') {
              <strong>{{ currency() }} {{ row.estimatedTotal | number:'1.2-2' }}</strong>
            } @else if (column.key === 'status') {
              <nz-tag [nzColor]="statusColor(row.status)">{{ row.status }}</nz-tag>
            } @else if (column.key === 'actions') {
              <div class="action-buttons">
                @if (row.status === 'Draft' || row.status === 'PendingApproval') {
                  <button *erpHasPermission="'purchasing.requisitions.manage'" nz-button nzType="link" nzSize="small" (click)="approveRequisition(row.id)">
                    Approve
                  </button>
                }
                @if (row.status === 'Approved' && !row.convertedPurchaseOrderId) {
                  <button *erpHasPermission="'purchasing.orders.manage'" nz-button nzType="link" nzSize="small" (click)="openConvertModal(row)">
                    Create PO
                  </button>
                }
                @if (row.convertedPurchaseOrderId) {
                  <a [routerLink]="['/purchasing/orders', row.convertedPurchaseOrderId]" nz-button nzType="link" nzSize="small">
                    View PO
                  </a>
                }
                @if (row.status === 'Draft') {
                  <button *erpHasPermission="'purchasing.requisitions.manage'" nz-button nzType="link" nzDanger nzSize="small"
                          nz-popconfirm nzPopconfirmTitle="Delete this requisition?" (nzOnConfirm)="deleteRequisition(row.id)">
                    Delete
                  </button>
                }
              </div>
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
                    <th style="width: 120px;">Item Code</th>
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
                      <td><input aria-label="Code" nz-input formControlName="itemCode" placeholder="Code" /></td>
                      <td><input aria-label="Item description" nz-input formControlName="description" placeholder="Item description" /></td>
                      <td><nz-input-number formControlName="quantity" [nzMin]="1" style="width: 100%;"></nz-input-number></td>
                      <td><nz-input-number formControlName="estimatedUnitPrice" [nzMin]="0" style="width: 100%;"></nz-input-number></td>
                      <td class="line-calc">{{ (ctrl.value.quantity || 0) * (ctrl.value.estimatedUnitPrice || 0) | number:'1.2-2' }}</td>
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
        nzTitle="Convert Requisition to Purchase Order"
        (nzOnCancel)="convertModalVisible.set(false)"
        (nzOnOk)="submitConvertToOrder()">
        <ng-container *nzModalContent>
          <div style="margin-bottom: 16px;">
            <label style="display: block; margin-bottom: 8px; font-weight: 600;">Vendor / Supplier Name:</label>
            <input aria-label="Enter supplier name" nz-input [(ngModel)]="vendorNameInput" placeholder="Enter supplier name" />
          </div>
        </ng-container>
      </nz-modal>
  `,
  styles: [`
    .requester-cell { display: flex; flex-direction: column; }
    .requester-name { font-weight: 600; color: #1e293b; }
    .dept-text { font-size: 12px; color: #64748b; }
    .action-buttons { display: flex; gap: 4px; align-items: center; }
    .lines-header { display: flex; justify-content: space-between; align-items: center; margin: 16px 0 8px; border-top: 1px solid #e2e8f0; padding-top: 16px; }
    .lines-header h3 { margin: 0; font-size: 15px; font-weight: 600; color: #1e293b; }
    .lines-table { width: 100%; border-collapse: collapse; font-size: 13px; }
    .lines-table th { background: #f8fafc; padding: 8px; text-align: left; font-weight: 600; color: #475569; border: 1px solid #e2e8f0; }
    .lines-table td { padding: 4px; border: 1px solid #e2e8f0; vertical-align: middle; }
    .line-calc { text-align: right; padding-right: 8px; font-weight: 600; color: #0f172a; }
    .drawer-footer { display: flex; justify-content: flex-end; gap: 8px; margin-top: 24px; border-top: 1px solid #f1f5f9; padding-top: 16px; }
  `],
})
export class PurchaseRequisitionsPage {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly reqsApi = inject(PurchaseRequisitionsApi);
  private readonly entitiesApi = inject(LegalEntitiesApi);
  private readonly message = inject(NzMessageService);
  private readonly currencyService = inject(WorkspaceCurrencyService);

  readonly currency = this.currencyService.currency;
  readonly list = createPagedList((q) => this.reqsApi.list(q), { sort: 'number' });
  readonly entities = signal<{ id: string; name: string }[]>([]);
  readonly drawerVisible = signal(false);
  readonly isSubmitting = signal(false);

  readonly convertModalVisible = signal(false);
  readonly selectedReq = signal<RequisitionSummaryDto | null>(null);
  vendorNameInput = '';

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
    { key: 'requesterName', label: 'Requester / Dept', sortField: 'requesterName' },
    { key: 'requisitionDate', label: 'Req Date', sortField: 'requisitionDate', width: '120px' },
    { key: 'requiredDate', label: 'Required By', sortField: 'requiredDate', width: '120px' },
    { key: 'estimatedTotal', label: 'Est. Total', sortField: 'estimatedTotal', width: '140px' },
    { key: 'status', label: 'Status', sortField: 'status', width: '130px' },
    { key: 'actions', label: 'Actions', width: '160px' },
  ];

  constructor() {
    this.loadEntities();
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

  statusColor(status: string): string {
    switch (status) {
      case 'Approved': return 'blue';
      case 'Ordered': return 'green';
      case 'Rejected': return 'red';
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
        this.message.success(`Requisition ${created.number} submitted.`);
        this.list.reload();
      },
      error: (err) => {
        this.isSubmitting.set(false);
        this.message.error(err?.error?.message ?? 'Failed to create requisition');
      },
    });
  }

  approveRequisition(id: string): void {
    this.reqsApi.setStatus(id, 'Approved').subscribe({
      next: () => {
        this.message.success('Requisition approved');
        this.list.reload();
      },
      error: (err) => this.message.error(err?.error?.message ?? 'Failed to approve'),
    });
  }

  openConvertModal(req: RequisitionSummaryDto): void {
    this.selectedReq.set(req);
    this.vendorNameInput = '';
    this.convertModalVisible.set(true);
  }

  submitConvertToOrder(): void {
    const req = this.selectedReq();
    if (!req) return;
    this.reqsApi.convertToOrder(req.id, null, this.vendorNameInput).subscribe({
      next: (po) => {
        this.message.success(`Converted to Purchase Order ${po.number}`);
        this.convertModalVisible.set(false);
        this.list.reload();
        this.router.navigate(['/purchasing/orders', po.id]);
      },
      error: (err) => this.message.error(err?.error?.message ?? 'Failed to convert to order'),
    });
  }

  deleteRequisition(id: string): void {
    this.reqsApi.delete(id).subscribe({
      next: () => {
        this.message.success('Requisition deleted');
        this.list.reload();
      },
      error: (err) => this.message.error(err?.error?.message ?? 'Failed to delete'),
    });
  }
}
