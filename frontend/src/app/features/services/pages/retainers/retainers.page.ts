import { doneText } from '@core/format/done-text';
import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { AbstractControl, FormBuilder, FormGroup, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
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
  RetainerCycle,
  RetainerStatus,
  RetainerSummaryDto,
  ServicesEngagementsApi,
  ServicesRetainersApi,
} from '@core/api/services.api';
import { toIsoDate } from '@core/format/dates';
import { problemMessage } from '@core/http/problem-details';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { ErpLabelPipe } from '@shared/pipes/display.pipes';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';

/** The end date field must not fall before the start date field of the same form. */
const notBeforeStart = (startKey: string) => (c: AbstractControl): ValidationErrors | null => {
  const start = c.parent?.get(startKey)?.value;
  return start && c.value && toIsoDate(new Date(c.value)) < toIsoDate(new Date(start)) ? { beforeStart: true } : null;
};

const CYCLE_PERIOD: Record<RetainerCycle, string> = { monthly: 'month', quarterly: 'quarter', annual: 'year' };

@Component({
  selector: 'erp-retainers-page',
  standalone: true,
  imports: [
    DecimalPipe,
    ErpLabelPipe,
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
    HasPermissionDirective,
    PagedTableComponent,
    StatusTagComponent,
  ],
  templateUrl: './retainers.page.html',
  styleUrl: './retainers.page.scss',
})
export class RetainersPage implements OnInit {
  private readonly api = inject(ServicesRetainersApi);
  private readonly engagementsApi = inject(ServicesEngagementsApi);
  private readonly fb = inject(FormBuilder);
  private readonly msg = inject(NzMessageService);
  readonly currency = inject(WorkspaceCurrencyService).currency;

  readonly list = createPagedList<RetainerSummaryDto>((q) => this.api.list(q));
  private readonly engagements = signal<EngagementSummaryDto[]>([]);
  readonly openEngagements = computed(() => this.engagements().filter((e) => e.status === 'draft' || e.status === 'active' || e.status === 'onHold'));
  private readonly engagementId = signal<string | null>(null);
  readonly selectedEngagement = computed(() => this.engagements().find((e) => e.id === this.engagementId()) ?? null);
  readonly drawerVisible = signal(false);
  readonly isSubmitting = signal(false);

  readonly columns: TableColumn<RetainerSummaryDto>[] = [
    { key: 'retainerNumber', label: 'Number', sortField: 'retainerNumber' },
    { key: 'title', label: 'Retainer and client', sortField: 'title' },
    { key: 'cycle', label: 'Cycle', sortField: 'cycle' },
    { key: 'recurringFee', label: 'Fee', sortField: 'recurringFee', align: 'right' },
    { key: 'includedHours', label: 'Hours included', align: 'right' },
    { key: 'status', label: 'Status', sortField: 'status', filterField: 'status', filterOptions: ['active', 'paused', 'terminated'] },
    { key: 'actions', label: 'Actions', align: 'right', filter: false, value: () => '' },
  ];

  readonly form: FormGroup = this.fb.group({
    engagementId: [null, [Validators.required]],
    title: ['', [Validators.required, Validators.maxLength(200)]],
    cycle: ['monthly' as RetainerCycle, [Validators.required]],
    recurringFee: [null, [Validators.required, Validators.min(0)]],
    includedHours: [null, [Validators.required, Validators.min(0)]],
    overageHourlyRate: [null, [Validators.required, Validators.min(0)]],
    startDate: [new Date(), [Validators.required]],
    endDate: [null, [notBeforeStart('startDate')]],
    terms: [''],
  });

  ngOnInit(): void {
    this.form.get('startDate')!.valueChanges.subscribe(() => this.form.get('endDate')!.updateValueAndValidity());
    this.engagementsApi.list({ page: 1, pageSize: 200 }).subscribe({
      next: (res) => this.engagements.set(res.items),
    });
    this.form.get('engagementId')!.valueChanges.subscribe((id) => this.engagementId.set(id));
  }

  period(cycle: RetainerCycle): string {
    return CYCLE_PERIOD[cycle];
  }

  openCreateDrawer(): void {
    this.form.reset({
      engagementId: null,
      title: '',
      cycle: 'monthly',
      recurringFee: null,
      includedHours: null,
      overageHourlyRate: null,
      startDate: new Date(),
      endDate: null,
      terms: '',
    });
    this.drawerVisible.set(true);
  }

  /** Raises a draft invoice for the current period: the fee, plus hours beyond the allowance. Asking again for the same period returns the same invoice. */
  invoicePeriod(row: RetainerSummaryDto): void {
    this.api.invoicePeriod(row.id).subscribe({
      next: (r) => {
        const note = r.unapprovedHours > 0 ? ` ${r.unapprovedHours} hours are not approved yet and are not counted.` : '';
        this.msg.success(r.alreadyInvoiced
          ? `This period was already invoiced as ${r.invoiceNumber}.`
          : `Draft invoice ${r.invoiceNumber} created for ${row.retainerNumber}, ${r.periodStart} to ${r.periodEnd}. Issue it from Invoices.${note}`);
        this.list.reload();
      },
      error: (err) => this.msg.error(problemMessage(err, 'The period could not be invoiced')),
    });
  }

  setStatus(row: RetainerSummaryDto, status: RetainerStatus): void {
    this.api.updateStatus(row.id, status).subscribe({
      next: () => {
        const done = status === 'paused' ? 'paused' : status === 'active' ? 'resumed' : 'terminated';
        this.msg.success(`${row.retainerNumber} ${done}`);
        this.list.reload();
      },
      error: (err) => this.msg.error(problemMessage(err, 'The retainer could not be updated')),
    });
  }

  submitRetainer(): void {
    const engagement = this.selectedEngagement();
    if (this.form.invalid || !engagement) return;

    this.isSubmitting.set(true);
    const val = this.form.value;
    this.api.create({
      legalEntityId: engagement.legalEntityId,
      engagementId: engagement.id,
      clientId: engagement.clientId,
      clientName: engagement.clientName,
      title: val.title,
      cycle: val.cycle,
      recurringFee: val.recurringFee,
      includedHours: val.includedHours,
      overageHourlyRate: val.overageHourlyRate,
      startDate: toIsoDate(new Date(val.startDate)),
      endDate: val.endDate ? toIsoDate(new Date(val.endDate)) : null,
      terms: val.terms || null,
    }).subscribe({
      next: () => {
        this.msg.success(doneText('created', 'Retainer', val.title));
        this.isSubmitting.set(false);
        this.drawerVisible.set(false);
        this.list.reload();
      },
      error: (err) => {
        this.msg.error(problemMessage(err, 'The retainer could not be saved'));
        this.isSubmitting.set(false);
      },
    });
  }
}
