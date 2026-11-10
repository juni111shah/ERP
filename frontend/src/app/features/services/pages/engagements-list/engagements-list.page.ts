import { doneText } from '@core/format/done-text';
import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { AbstractControl, FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { LegalEntitiesApi } from '@core/api/admin.api';
import { createPagedList } from '@core/api/paged-list';
import {
  EngagementDto,
  EngagementStatus,
  EngagementSummaryDto,
  EngagementType,
  ServicesEngagementsApi,
} from '@core/api/services.api';
import { toIsoDate } from '@core/custom-fields/custom-field-form';
import { problemMessage } from '@core/http/problem-details';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { SessionStore } from '@core/session/session.store';
import { ErpDatePipe, ErpLabelPipe } from '@shared/pipes/display.pipes';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { EngagementTimeComponent } from '../../components/engagement-time/engagement-time.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { PartiesApi, PartyDto } from '@features/parties/pages/parties/parties.page';

/** The moves the server allows from each status. */
const NEXT_STATUS: Record<EngagementStatus, EngagementStatus[]> = {
  draft: ['active', 'cancelled'],
  active: ['onHold', 'completed', 'cancelled'],
  onHold: ['active', 'cancelled'],
  completed: [],
  cancelled: [],
};

/** The end date field must not fall before the start date field of the same form. */
const notBeforeStart = (startKey: string) => (c: AbstractControl): ValidationErrors | null => {
  const start = c.parent?.get(startKey)?.value;
  return start && c.value && toIsoDate(new Date(c.value)) < toIsoDate(new Date(start)) ? { beforeStart: true } : null;
};

@Component({
  selector: 'erp-engagements-list-page',
  standalone: true,
  imports: [
    DecimalPipe,
    ErpLabelPipe,
    ErpDatePipe,
    FormsModule,
    ReactiveFormsModule,
    NzButtonModule,
    NzCardModule,
    NzIconModule,
    NzProgressModule,
    NzModalModule,
    NzDrawerModule,
    NzFormModule,
    NzInputModule,
    NzInputNumberModule,
    NzDatePickerModule,
    NzSelectModule,
    PagedTableComponent,
    HasPermissionDirective,
    StatusTagComponent,
    EngagementTimeComponent,
  ],
  templateUrl: './engagements-list.page.html',
  styleUrl: './engagements-list.page.scss',
})
export class EngagementsListPage implements OnInit {
  private readonly api = inject(ServicesEngagementsApi);
  private readonly legalEntitiesApi = inject(LegalEntitiesApi);
  private readonly partiesApi = inject(PartiesApi);
  private readonly session = inject(SessionStore);
  private readonly fb = inject(FormBuilder);
  private readonly msg = inject(NzMessageService);
  readonly currency = inject(WorkspaceCurrencyService).currency;

  readonly list = createPagedList<EngagementSummaryDto>((q) => this.api.list(q));

  readonly columns: TableColumn<EngagementSummaryDto>[] = [
    { key: 'engagementCode', label: 'Number', sortField: 'engagementCode' },
    { key: 'title', label: 'Engagement and client', sortField: 'title' },
    { key: 'type', label: 'Billing model', sortField: 'type' },
    { key: 'hours', label: 'Hours used', filter: false, value: (r) => `${r.consumedHours} of ${r.budgetedHours}` },
    { key: 'contractValue', label: 'Contract value', sortField: 'contractValue', align: 'right' },
    { key: 'status', label: 'Status', sortField: 'status', filterField: 'status', filterOptions: ['draft', 'active', 'onHold', 'completed', 'cancelled'] },
    { key: 'actions', label: 'Actions', align: 'right', filter: false, value: () => '' },
  ];

  readonly legalEntities = signal<{ id: string; name: string }[]>([]);
  readonly clients = signal<PartyDto[]>([]);
  readonly drawerVisible = signal(false);
  readonly detailsVisible = signal(false);
  readonly statusModalVisible = signal(false);
  readonly isSubmitting = signal(false);
  readonly isSavingStatus = signal(false);
  readonly selectedEngagement = signal<EngagementSummaryDto | null>(null);
  readonly selectedDetails = signal<EngagementDto | null>(null);
  readonly statusOptions = computed(() => this.nextStatuses(this.selectedEngagement()?.status));

  newStatus: EngagementStatus | null = null;

  readonly form: FormGroup = this.fb.group({
    legalEntityId: ['', [Validators.required]],
    title: ['', [Validators.required, Validators.maxLength(200)]],
    clientId: [null, [Validators.required]],
    clientName: ['', [Validators.required]],
    type: ['fixedPrice' as EngagementType, [Validators.required]],
    contractValue: [null, [Validators.required, Validators.min(0)]],
    budgetedHours: [null, [Validators.required, Validators.min(0)]],
    startDate: [new Date(), [Validators.required]],
    targetEndDate: [null, [notBeforeStart('startDate')]],
    leadConsultantName: [''],
    description: [''],
  });

  ngOnInit(): void {
    this.form.get('startDate')!.valueChanges.subscribe(() => this.form.get('targetEndDate')!.updateValueAndValidity());
    this.legalEntitiesApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: (res) => this.legalEntities.set(res.items.map((e) => ({ id: e.id, name: e.name }))),
    });
    this.partiesApi.list({ page: 1, pageSize: 200 }).subscribe({
      next: (res) => {
        const customers = res.items.filter((p) => !p.roles?.length || p.roles.some((r) => /customer|client/i.test(r)));
        this.clients.set(customers.length > 0 ? customers : res.items);
      },
    });
  }

  nextStatuses(status: EngagementStatus | undefined): EngagementStatus[] {
    return status ? NEXT_STATUS[status] : [];
  }

  onClientSelected(id: string): void {
    const c = this.clients().find((x) => x.id === id);
    if (c) this.form.patchValue({ clientName: c.name });
  }

  openCreateDrawer(): void {
    const entity = this.legalEntities().find((e) => e.id === this.session.currentEntityId()) ?? this.legalEntities()[0];
    this.form.reset({
      legalEntityId: entity?.id ?? '',
      title: '',
      clientId: null,
      clientName: '',
      type: 'fixedPrice',
      contractValue: null,
      budgetedHours: null,
      startDate: new Date(),
      targetEndDate: null,
      leadConsultantName: '',
      description: '',
    });
    this.drawerVisible.set(true);
  }

  getHoursPercent(row: EngagementSummaryDto): number {
    if (!row.budgetedHours) return 0;
    return Math.min(Math.round((row.consumedHours / row.budgetedHours) * 100), 100);
  }

  openStatusModal(row: EngagementSummaryDto): void {
    this.selectedEngagement.set(row);
    this.newStatus = null;
    this.statusModalVisible.set(true);
  }

  saveStatus(): void {
    const item = this.selectedEngagement();
    if (!item || !this.newStatus) return;

    this.isSavingStatus.set(true);
    this.api.updateStatus(item.id, this.newStatus).subscribe({
      next: () => {
        this.msg.success(`${item.engagementCode} is now ${String(this.newStatus).replace(/_/g, ' ')}`);
        this.isSavingStatus.set(false);
        this.statusModalVisible.set(false);
        this.list.reload();
      },
      error: (err) => {
        this.msg.error(problemMessage(err, 'The status could not be changed'));
        this.isSavingStatus.set(false);
      },
    });
  }

  viewDetails(row: EngagementSummaryDto): void {
    this.api.getEngagement(row.id).subscribe({
      next: (dto) => {
        this.selectedDetails.set(dto);
        this.detailsVisible.set(true);
      },
      error: (err) => this.msg.error(problemMessage(err, 'The engagement could not be loaded')),
    });
  }

  submitEngagement(): void {
    if (this.form.invalid) return;

    this.isSubmitting.set(true);
    const val = this.form.value;
    this.api.create({
      legalEntityId: val.legalEntityId,
      title: val.title,
      clientId: val.clientId,
      clientName: val.clientName,
      type: val.type,
      contractValue: val.contractValue,
      budgetedHours: val.budgetedHours,
      startDate: toIsoDate(new Date(val.startDate)),
      targetEndDate: val.targetEndDate ? toIsoDate(new Date(val.targetEndDate)) : null,
      leadConsultantName: val.leadConsultantName || null,
      description: val.description || null,
    }).subscribe({
      next: () => {
        this.msg.success(doneText('created', 'Engagement', val.title));
        this.isSubmitting.set(false);
        this.drawerVisible.set(false);
        this.list.reload();
      },
      error: (err) => {
        this.msg.error(problemMessage(err, 'The engagement could not be saved'));
        this.isSubmitting.set(false);
      },
    });
  }
}
