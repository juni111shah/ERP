import { doneText } from '../../core/format/done-text';
import { DecimalPipe } from '@angular/common';
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
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { createPagedList } from '../../core/api/paged-list';
import {
  EngagementSummaryDto,
  MilestoneStatus,
  MilestoneSummaryDto,
  ServicesEngagementsApi,
  ServicesMilestonesApi,
} from '../../core/api/services.api';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { problemMessage } from '../../core/http/problem-details';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { ErpDatePipe } from '../../shared/display.pipes';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { StatusTagComponent } from '../../shared/status-tag.component';

@Component({
  selector: 'erp-milestone-billing-page',
  standalone: true,
  imports: [
    DecimalPipe,
    ErpDatePipe,
    ReactiveFormsModule,
    NzButtonModule,
    NzIconModule,
    NzDrawerModule,
    NzFormModule,
    NzInputModule,
    NzInputNumberModule,
    NzDatePickerModule,
    NzPopconfirmModule,
    NzSelectModule,
    PagedTableComponent,
    HasPermissionDirective,
    StatusTagComponent,
  ],
  template: `
    <!-- The server allows no search or sorting on milestones yet, so neither is offered. -->
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search number or title" [clickable]="false" emptyText="No milestones yet. Add one to bill an engagement in stages.">
      <button toolbar *erpHasPermission="'services.milestones.manage'" nz-button nzType="primary" (click)="openCreateDrawer()" data-testid="milestone-new">
        <span nz-icon nzType="plus"></span> Add Milestone
      </button>
      <ng-template #cell let-row let-column="column" let-value="value">
        @switch (column.key) {
          @case ('milestoneCode') { <span class="code strong">{{ row.milestoneCode }}</span> }
          @case ('title') {
            <div>{{ row.title }}</div>
            @if (engagementOf(row); as eng) {
              <div class="sub"><span class="code">{{ eng.engagementCode }}</span> &middot; {{ eng.title }} &middot; {{ eng.clientName }}</div>
            }
          }
          @case ('dueDate') { <span class="nowrap">{{ row.dueDate | erpDate }}</span> }
          @case ('amount') { <span class="num strong">{{ currency() }} {{ row.amount | number:'1.2-2' }}</span> }
          @case ('status') { <erp-status [value]="row.status" /> }
          @case ('actions') {
            <div class="table-actions">
              @if (row.status === 'planned') {
                <button *erpHasPermission="'services.milestones.manage'" nz-button nzType="link" nzSize="small" (click)="updateStatus(row, 'readyToInvoice')">
                  <span nz-icon nzType="check-circle"></span> Ready to invoice
                </button>
              }
              @if (row.status === 'readyToInvoice') {
                <button *erpHasPermission="'services.milestones.manage'" nz-button nzType="link" nzSize="small"
                        nz-popconfirm [nzPopconfirmTitle]="'Create an invoice for ' + row.milestoneCode + '?'" (nzOnConfirm)="invoiceMilestone(row)">
                  <span nz-icon nzType="file-text"></span> Create invoice
                </button>
                <button *erpHasPermission="'services.milestones.manage'" nz-button nzType="link" nzSize="small" (click)="updateStatus(row, 'planned')">
                  <span nz-icon nzType="rollback"></span> Back to planned
                </button>
              }
            </div>
          }
          @default { {{ value }} }
        }
      </ng-template>
    </erp-paged-table>

    <!-- Create Milestone Drawer -->
    <nz-drawer [nzVisible]="drawerVisible()" nzTitle="Add milestone" [nzWidth]="480" (nzOnClose)="drawerVisible.set(false)">
      <ng-container *nzDrawerContent>
        <form nz-form [formGroup]="form" (ngSubmit)="submitMilestone()" nzLayout="vertical">
          <nz-form-item>
            <nz-form-label nzRequired>Engagement</nz-form-label>
            <nz-form-control nzErrorTip="Choose an engagement">
              <nz-select formControlName="engagementId" nzShowSearch nzPlaceHolder="Choose an engagement" nzNotFoundContent="No open engagements. Create one first.">
                @for (eng of openEngagements(); track eng.id) {
                  <nz-option [nzValue]="eng.id" [nzLabel]="eng.engagementCode + ' - ' + eng.title + ' (' + eng.clientName + ')'"></nz-option>
                }
              </nz-select>
            </nz-form-control>
          </nz-form-item>

          <nz-form-item>
            <nz-form-label nzRequired>Title</nz-form-label>
            <nz-form-control nzErrorTip="Enter a title">
              <input nz-input formControlName="title" placeholder="e.g. Phase 1 sign-off" />
            </nz-form-control>
          </nz-form-item>

          <div class="row-2">
            <nz-form-item>
              <nz-form-label nzRequired>Amount ({{ currency() }})</nz-form-label>
              <nz-form-control nzErrorTip="Enter an amount above zero">
                <nz-input-number formControlName="amount" [nzMin]="0.01" [nzStep]="1000" class="full"></nz-input-number>
              </nz-form-control>
            </nz-form-item>
            <nz-form-item>
              <nz-form-label nzRequired>Due date</nz-form-label>
              <nz-form-control nzErrorTip="Choose a due date">
                <nz-date-picker formControlName="dueDate" class="full"></nz-date-picker>
              </nz-form-control>
            </nz-form-item>
          </div>

          <nz-form-item>
            <nz-form-label>Notes</nz-form-label>
            <nz-form-control>
              <textarea nz-input formControlName="notes" rows="3" placeholder="Acceptance criteria, customer contacts"></textarea>
            </nz-form-control>
          </nz-form-item>

          <div class="drawer-footer">
            <button nz-button type="button" (click)="drawerVisible.set(false)">Cancel</button>
            <button nz-button nzType="primary" [nzLoading]="isSubmitting()" type="submit" [disabled]="form.invalid">Save</button>
          </div>
        </form>
      </ng-container>
    </nz-drawer>
  `,
  styles: [`
    .code { font-family: var(--erp-font-family-mono); white-space: nowrap; }
    .strong { font-weight: 600; }
    .sub { font-size: 12px; color: var(--erp-color-text-muted); }
    .nowrap { white-space: nowrap; }
    .num { font-variant-numeric: tabular-nums; white-space: nowrap; }
    .table-actions { display: flex; justify-content: flex-end; gap: 4px; }
    .row-2 { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 16px; }
    .full { width: 100%; }
    .drawer-footer { display: flex; justify-content: flex-end; gap: 8px; margin-block-start: 24px; }
  `],
})
export class MilestoneBillingPage implements OnInit {
  private readonly api = inject(ServicesMilestonesApi);
  private readonly engagementsApi = inject(ServicesEngagementsApi);
  private readonly fb = inject(FormBuilder);
  private readonly msg = inject(NzMessageService);
  readonly currency = inject(WorkspaceCurrencyService).currency;

  readonly list = createPagedList<MilestoneSummaryDto>((q) => this.api.list(q));
  private readonly engagements = signal<EngagementSummaryDto[]>([]);
  private readonly engagementsById = computed(() => new Map(this.engagements().map((e) => [e.id, e])));
  readonly openEngagements = computed(() => this.engagements().filter((e) => e.status === 'draft' || e.status === 'active' || e.status === 'onHold'));
  readonly drawerVisible = signal(false);
  readonly isSubmitting = signal(false);

  readonly columns: TableColumn<MilestoneSummaryDto>[] = [
    { key: 'milestoneCode', label: 'Number', sortField: 'milestoneCode' },
    { key: 'title', label: 'Milestone and engagement', sortField: 'title' },
    { key: 'engagement', label: 'Engagement', hidden: true, value: (r) => this.engagementOf(r)?.title ?? '' },
    { key: 'dueDate', label: 'Due date', sortField: 'dueDate' },
    { key: 'amount', label: 'Amount', sortField: 'amount', align: 'right' },
    { key: 'status', label: 'Status', sortField: 'status', filterField: 'status', filterOptions: ['planned', 'readyToInvoice', 'invoiced', 'paid'] },
    { key: 'actions', label: 'Actions', align: 'right', filter: false, value: () => '' },
  ];

  readonly form: FormGroup = this.fb.group({
    engagementId: [null, [Validators.required]],
    title: ['', [Validators.required, Validators.maxLength(200)]],
    amount: [null, [Validators.required, Validators.min(0.01)]],
    dueDate: [new Date(), [Validators.required]],
    notes: [''],
  });

  ngOnInit(): void {
    this.engagementsApi.list({ page: 1, pageSize: 200 }).subscribe({
      next: (res) => this.engagements.set(res.items),
    });
  }

  engagementOf(row: MilestoneSummaryDto): EngagementSummaryDto | undefined {
    return this.engagementsById().get(row.engagementId);
  }

  openCreateDrawer(): void {
    this.form.reset({ engagementId: null, title: '', amount: null, dueDate: new Date(), notes: '' });
    this.drawerVisible.set(true);
  }

  updateStatus(row: MilestoneSummaryDto, newStatus: MilestoneStatus): void {
    this.api.updateStatus(row.id, newStatus).subscribe({
      next: () => {
        this.msg.success(newStatus === 'planned' ? `${row.milestoneCode} moved back to planned` : `${row.milestoneCode} is ready to invoice`);
        this.list.reload();
      },
      error: (err) => this.msg.error(problemMessage(err, 'The milestone could not be updated')),
    });
  }

  invoiceMilestone(row: MilestoneSummaryDto): void {
    this.api.invoice(row.id).subscribe({
      next: () => {
        this.msg.success(`Invoice created for ${row.milestoneCode}`);
        this.list.reload();
      },
      error: (err) => this.msg.error(problemMessage(err, 'The invoice could not be created')),
    });
  }

  submitMilestone(): void {
    if (this.form.invalid) return;
    const val = this.form.value;
    const engagement = this.engagementsById().get(val.engagementId);
    if (!engagement) return;

    this.isSubmitting.set(true);
    this.api.create({
      legalEntityId: engagement.legalEntityId,
      engagementId: engagement.id,
      title: val.title,
      amount: val.amount,
      dueDate: toIsoDate(new Date(val.dueDate)),
      notes: val.notes || null,
    }).subscribe({
      next: () => {
        this.msg.success(doneText('created', 'Milestone', val.title));
        this.isSubmitting.set(false);
        this.drawerVisible.set(false);
        this.list.reload();
      },
      error: (err) => {
        this.msg.error(problemMessage(err, 'The milestone could not be saved'));
        this.isSubmitting.set(false);
      },
    });
  }
}
