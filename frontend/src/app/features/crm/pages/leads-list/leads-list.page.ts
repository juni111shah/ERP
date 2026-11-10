import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzBadgeModule } from 'ng-zorro-antd/badge';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { CrmLeadsApi, LeadInput, LeadStatus, LeadSummaryDto } from '@core/api/crm.api';
import { createPagedList } from '@core/api/paged-list';
import { toUtcIsoDate } from '@core/format/dates';
import { displayLabel } from '@core/format/display';
import { doneText } from '@core/format/done-text';
import { problemMessage } from '@core/http/problem-details';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { revealErrors } from '@shared/forms/form-validation';
import { CrmLookups, daysFromNow } from '../../crm-lookups';

/** A lead becomes Converted only through the convert action. */
const STATUSES: LeadStatus[] = ['new', 'contacted', 'qualified', 'unqualified'];

/** The lead form's raw value, as the API takes it. */
type LeadFormValue = Pick<LeadInput, 'legalEntityId' | 'fullName' | 'companyName' | 'jobTitle' | 'email' | 'phone' | 'source' | 'estimatedValue' | 'notes'>;

/** The convert-lead form's raw value. */
interface ConvertLeadFormValue {
  opportunityTitle: string;
  expectedRevenue: number;
  expectedCloseDate: Date | null;
}

/** Leads: the paged list, the new-lead drawer, status moves and converting a lead into a deal. */
@Component({
  selector: 'erp-leads-list-page',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    NzBadgeModule,
    NzButtonModule,
    NzCardModule,
    NzDatePickerModule,
    NzDrawerModule,
    NzDropDownModule,
    NzFormModule,
    NzIconModule,
    NzInputModule,
    NzInputNumberModule,
    NzModalModule,
    NzSelectModule,
    HasPermissionDirective,
    PagedTableComponent,
    StatusTagComponent,
  ],
  providers: [CrmLookups],
  templateUrl: './leads-list.page.html',
  styleUrl: './leads-list.page.scss'
})
export class LeadsListPageComponent implements OnInit {
  private readonly api = inject(CrmLeadsApi);
  private readonly lookups = inject(CrmLookups);
  private readonly currencyService = inject(WorkspaceCurrencyService);
  private readonly message = inject(NzMessageService);
  private readonly fb = inject(FormBuilder);

  readonly currency = this.currencyService.currency;
  readonly list = createPagedList<LeadSummaryDto>((q) => this.api.list(q));
  readonly entities = this.lookups.entities;

  readonly createVisible = signal(false);
  readonly convertModalVisible = signal(false);
  readonly detailVisible = signal(false);
  readonly isSubmitting = signal(false);
  readonly isConverting = signal(false);
  readonly selectedLead = signal<LeadSummaryDto | null>(null);

  readonly leadForm: FormGroup = this.fb.group({
    legalEntityId: ['', [Validators.required]],
    fullName: ['', [Validators.required]],
    companyName: [''],
    jobTitle: [''],
    email: ['', [Validators.email]],
    phone: [''],
    source: ['website'],
    estimatedValue: [0, [Validators.required, Validators.min(0)]],
    notes: ['']
  });

  readonly convertForm: FormGroup = this.fb.group({
    opportunityTitle: ['', [Validators.required]],
    expectedRevenue: [0, [Validators.required, Validators.min(0)]],
    expectedCloseDate: [daysFromNow(30)]
  });

  // The CRM list endpoints accept no sort, filter or search fields yet, so the table offers none of those from the server (the header filters work on the loaded page).
  readonly columns: TableColumn<LeadSummaryDto>[] = [
    { key: 'leadNumber', label: 'Lead #', sortField: 'leadNumber', width: '140px' },
    { key: 'fullName', label: 'Lead / Company', sortField: 'fullName', filterValue: (r) => `${r.fullName} ${r.companyName ?? ''}` },
    { key: 'companyName', label: 'Company', hidden: true },
    { key: 'email', label: 'Contact', filterValue: (r) => `${r.email ?? ''} ${r.phone ?? ''}` },
    { key: 'phone', label: 'Phone', hidden: true },
    { key: 'source', label: 'Source', sortField: 'source', width: '100px' },
    { key: 'status', label: 'Status', sortField: 'status', filterField: 'status', filterOptions: ['new', 'contacted', 'qualified', 'unqualified', 'converted'], width: '110px' },
    { key: 'estimatedValue', label: 'Est. Value', sortField: 'estimatedValue', width: '130px', align: 'right', filter: false },
    { key: 'assignedUserName', label: 'Owner', hidden: true },
    { key: 'actions', label: 'Actions', align: 'right', width: '250px' }
  ];

  ngOnInit(): void {
    this.lookups.loadEntities(this.leadForm);
  }

  label(value: string): string { return displayLabel(value); }
  /** The server refuses to convert an unqualified lead or one already converted. */
  canConvert(row: LeadSummaryDto): boolean { return row.status !== 'converted' && row.status !== 'unqualified'; }
  statusMoves(row: LeadSummaryDto): LeadStatus[] { return STATUSES.filter((s) => s !== row.status); }
  leadSubtitle(lead: LeadSummaryDto): string { return [lead.companyName, lead.jobTitle].filter(Boolean).join(' · '); }

  setStatus(row: LeadSummaryDto, status: LeadStatus): void {
    this.api.setStatus(row.id, status).subscribe({
      next: () => {
        this.message.success(`${row.leadNumber} is now ${displayLabel(status).toLowerCase()}.`);
        this.list.reload();
      },
      error: (err) => this.message.error(problemMessage(err, 'Could not change the status.')),
    });
  }

  openCreateDrawer(): void {
    this.leadForm.reset({
      legalEntityId: this.entities()[0]?.id || '',
      source: 'website',
      estimatedValue: 0
    });
    this.createVisible.set(true);
  }

  viewDetail(row: LeadSummaryDto): void {
    this.selectedLead.set(row);
    this.detailVisible.set(true);
  }

  openConvertModal(row: LeadSummaryDto): void {
    this.selectedLead.set(row);
    this.convertForm.patchValue({
      opportunityTitle: `${row.companyName || row.fullName} - Deal`,
      expectedRevenue: row.estimatedValue,
      expectedCloseDate: daysFromNow(30)
    });
    this.convertModalVisible.set(true);
  }

  saveLead(): void {
    if (this.leadForm.invalid) {
      revealErrors(this.leadForm, { onlySelf: true });
      return;
    }
    this.isSubmitting.set(true);
    const v = this.leadForm.value as LeadFormValue;

    this.api.create(v).subscribe({
      next: () => {
        this.message.success(doneText('added', 'Lead', v.fullName));
        this.createVisible.set(false);
        this.isSubmitting.set(false);
        this.list.reload();
      },
      error: (err) => {
        this.message.error(problemMessage(err, 'Failed to create lead'));
        this.isSubmitting.set(false);
      }
    });
  }

  submitConvertLead(): void {
    if (this.convertForm.invalid) return;
    const lead = this.selectedLead();
    if (!lead) return;

    this.isConverting.set(true);
    const v = this.convertForm.value as ConvertLeadFormValue;

    this.api.convert(lead.id, {
      opportunityTitle: v.opportunityTitle,
      expectedRevenue: v.expectedRevenue,
      expectedCloseDate: v.expectedCloseDate ? toUtcIsoDate(v.expectedCloseDate) : undefined
    }).subscribe({
      next: () => {
        this.message.success(`${lead.leadNumber} converted. Deal ${v.opportunityTitle} is in the pipeline.`);
        this.convertModalVisible.set(false);
        this.isConverting.set(false);
        this.list.reload();
      },
      error: (err) => {
        this.message.error(problemMessage(err, 'Failed to convert lead'));
        this.isConverting.set(false);
      }
    });
  }
}
