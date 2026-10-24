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
import { NzSelectModule } from 'ng-zorro-antd/select';
import { LegalEntitiesApi } from '../../core/api/admin.api';
import { createPagedList } from '../../core/api/paged-list';
import {
  MilestoneInput,
  MilestoneStatus,
  MilestoneSummaryDto,
  ServicesMilestonesApi
} from '../../core/api/services.api';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';

@Component({
  selector: 'erp-milestone-billing-page',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    NzCardModule,
    NzButtonModule,
    NzIconModule,
    NzBadgeModule,
    NzDrawerModule,
    NzFormModule,
    NzInputModule,
    NzInputNumberModule,
    NzDatePickerModule,
    NzSelectModule,
    PagedTableComponent
  ],
  template: `
    <erp-paged-table
      [list]="list"
      [columns]="columns">
      <button toolbar nz-button nzType="primary" (click)="openCreateDrawer()" data-testid="milestone-new">
        <span nz-icon nzType="plus"></span> Add Milestone
      </button>
          <ng-template #cell let-row let-column="column">
            <ng-container [ngSwitch]="column.key">

              <ng-container *ngSwitchCase="'code'">
                <span class="font-mono font-semibold text-primary">{{ row.milestoneCode }}</span>
              </ng-container>

              <ng-container *ngSwitchCase="'title'">
                <div class="font-medium text-slate-800 dark:text-slate-100">{{ row.title }}</div>
              </ng-container>

              <ng-container *ngSwitchCase="'dueDate'">
                <span>{{ row.dueDate }}</span>
              </ng-container>

              <ng-container *ngSwitchCase="'amount'">
                <span class="font-mono font-semibold">{{ row.amount | number:'1.2-2' }}</span>
              </ng-container>

              <ng-container *ngSwitchCase="'status'">
                <nz-badge [nzStatus]="getStatusBadge(row.status)" [nzText]="row.status"></nz-badge>
              </ng-container>

              <ng-container *ngSwitchCase="'actions'">
                <div class="table-actions">
                  @if (row.status === 'Planned') {
                    <button nz-button nzType="text" nzSize="small" (click)="updateStatus(row, 'ReadyToInvoice')" title="Deliverable Approved (Ready to Invoice)">
                      <span nz-icon nzType="check-circle" class="text-green-600"></span>
                    </button>
                  }
                  @if (row.status === 'ReadyToInvoice') {
                    <button nz-button nzType="text" nzSize="small" (click)="updateStatus(row, 'Invoiced')" title="Mark as Invoiced">
                      <span nz-icon nzType="file-done" class="text-blue-600"></span>
                    </button>
                  }
                  @if (row.status === 'Invoiced') {
                    <button nz-button nzType="text" nzSize="small" (click)="updateStatus(row, 'Paid')" title="Mark as Paid">
                      <span nz-icon nzType="dollar-circle" class="text-emerald-600"></span>
                    </button>
                  }
                </div>
              </ng-container>

            </ng-container>
          </ng-template>
        </erp-paged-table>

      <!-- Create Milestone Drawer -->
      <nz-drawer
        [nzVisible]="drawerVisible()"
        nzTitle="Add Billing Milestone Deliverable"
        [nzWidth]="480"
        (nzOnClose)="drawerVisible.set(false)">
        <ng-container *nzDrawerContent>
          <form nz-form [formGroup]="form" (ngSubmit)="submitMilestone()" nzLayout="vertical">
            <nz-form-item>
              <nz-form-label nzRequired>Operating Entity</nz-form-label>
              <nz-form-control nzErrorTip="Select legal entity">
                <nz-select formControlName="legalEntityId" nzPlaceHolder="Select Legal Entity">
                  @for (entity of legalEntities(); track entity.id) {
                    <nz-option [nzValue]="entity.id" [nzLabel]="entity.name"></nz-option>
                  }
                </nz-select>
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-label nzRequired>Deliverable / Milestone Title</nz-form-label>
              <nz-form-control nzErrorTip="Enter title">
                <input nz-input formControlName="title" placeholder="e.g. Phase 1 Architecture Blueprint Sign-Off" />
              </nz-form-control>
            </nz-form-item>

            <div class="grid grid-cols-2 gap-4">
              <nz-form-item>
                <nz-form-label nzRequired>Milestone Amount</nz-form-label>
                <nz-form-control>
                  <nz-input-number formControlName="amount" [nzMin]="1" [nzStep]="1000" class="w-full"></nz-input-number>
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
              <nz-form-label>Acceptance Criteria & Notes</nz-form-label>
              <nz-form-control>
                <textarea nz-input formControlName="notes" rows="3" placeholder="Acceptance sign-off criteria, customer contacts..."></textarea>
              </nz-form-control>
            </nz-form-item>

            <div class="drawer-footer">
              <button nz-button type="button" (click)="drawerVisible.set(false)">Cancel</button>
              <button nz-button nzType="primary" [nzLoading]="isSubmitting()" type="submit" [disabled]="form.invalid">
                Save Milestone
              </button>
            </div>
          </form>
        </ng-container>
      </nz-drawer>
  `,
  styles: [`
    .text-secondary { color: rgba(0, 0, 0, 0.45); }
    .table-actions { display: flex; gap: 4px; }
    .drawer-footer { display: flex; justify-content: flex-end; gap: 8px; margin-top: 24px; padding-top: 16px; border-top: 1px solid #f0f0f0; }
  `]
})
export class MilestoneBillingPage implements OnInit {
  private readonly api = inject(ServicesMilestonesApi);
  private readonly legalEntitiesApi = inject(LegalEntitiesApi);
  private readonly fb = inject(FormBuilder);
  private readonly msg = inject(NzMessageService);

  readonly list = createPagedList<MilestoneSummaryDto>((q) => this.api.list(q), {
    pageSize: 15,
    sort: 'createdAt:desc'
  });

  readonly columns: TableColumn<MilestoneSummaryDto>[] = [
    { key: 'code', label: 'Milestone #', sortField: 'milestoneCode', width: '130px' },
    { key: 'title', label: 'Deliverable Title', sortField: 'title' },
    { key: 'dueDate', label: 'Due Date', sortField: 'dueDate', width: '130px' },
    { key: 'amount', label: 'Amount', sortField: 'amount', align: 'right', width: '140px' },
    { key: 'status', label: 'Billing Status', sortField: 'status', width: '150px' },
    { key: 'actions', label: 'Actions', width: '90px' }
  ];

  readonly legalEntities = signal<{ id: string; name: string }[]>([]);
  readonly drawerVisible = signal(false);
  readonly isSubmitting = signal(false);

  form: FormGroup = this.fb.group({
    legalEntityId: ['', [Validators.required]],
    engagementId: ['00000000-0000-0000-0000-000000000001'],
    title: ['', [Validators.required, Validators.maxLength(200)]],
    amount: [15000, [Validators.required, Validators.min(1)]],
    dueDate: [new Date(), [Validators.required]],
    notes: ['']
  });

  ngOnInit(): void {
    this.legalEntitiesApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: res => {
        const items = res.items || [];
        this.legalEntities.set(items.map(e => ({ id: e.id, name: e.name })));
        if (items.length > 0) {
          this.form.patchValue({ legalEntityId: items[0].id });
        }
      }
    });
  }

  openCreateDrawer(): void {
    this.drawerVisible.set(true);
  }

  getStatusBadge(status: MilestoneStatus): 'default' | 'warning' | 'processing' | 'success' {
    switch (status) {
      case 'Planned': return 'default';
      case 'ReadyToInvoice': return 'warning';
      case 'Invoiced': return 'processing';
      case 'Paid': return 'success';
      default: return 'default';
    }
  }

  updateStatus(row: MilestoneSummaryDto, newStatus: MilestoneStatus): void {
    this.api.updateStatus(row.id, newStatus).subscribe({
      next: () => {
        this.msg.success(`Milestone transitioned to ${newStatus}`);
        this.list.reload();
      },
      error: () => this.msg.error('Failed to update milestone status')
    });
  }

  submitMilestone(): void {
    if (this.form.invalid) return;

    this.isSubmitting.set(true);
    const val = this.form.value;
    const input: MilestoneInput = {
      legalEntityId: val.legalEntityId,
      engagementId: val.engagementId,
      title: val.title,
      amount: val.amount,
      dueDate: val.dueDate instanceof Date ? val.dueDate.toISOString().substring(0, 10) : val.dueDate,
      notes: val.notes || null
    };

    this.api.create(input).subscribe({
      next: () => {
        this.msg.success('Billing milestone saved');
        this.isSubmitting.set(false);
        this.drawerVisible.set(false);
        this.list.reload();
      },
      error: () => {
        this.msg.error('Failed to save milestone');
        this.isSubmitting.set(false);
      }
    });
  }
}
