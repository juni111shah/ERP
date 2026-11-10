import { doneText } from '@core/format/done-text';
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
import { createPagedList } from '@core/api/paged-list';
import {
  EngagementSummaryDto,
  MilestoneStatus,
  MilestoneSummaryDto,
  ServicesEngagementsApi,
  ServicesMilestonesApi,
} from '@core/api/services.api';
import { toIsoDate } from '@core/format/dates';
import { problemMessage } from '@core/http/problem-details';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';

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
  templateUrl: './milestone-billing.page.html',
  styleUrl: './milestone-billing.page.scss',
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
