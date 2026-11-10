import { doneText } from '@core/format/done-text';
import { CommonModule } from '@angular/common';
import { problemMessage } from '@core/http/problem-details';
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
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { LegalEntitiesApi } from '@core/api/admin.api';
import {
  CrmLeadsApi,
  LeadStatus,
  LeadSummaryDto
} from '@core/api/crm.api';
import { createPagedList } from '@core/api/paged-list';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { displayLabel } from '@core/format/display';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';

/** A lead becomes Converted only through the convert action. */
const STATUSES: LeadStatus[] = ['new', 'contacted', 'qualified', 'unqualified'];

@Component({
  selector: 'erp-leads-list-page',
  standalone: true,
  imports: [StatusTagComponent, NzDropDownModule,
    CommonModule,
    ReactiveFormsModule,
    NzCardModule,
    NzButtonModule,
    NzIconModule,
    NzBadgeModule,
    NzModalModule,
    NzDrawerModule,
    NzFormModule,
    NzInputModule,
    NzInputNumberModule,
    NzDatePickerModule,
    NzSelectModule,
    PagedTableComponent,
    HasPermissionDirective,
  ],
  templateUrl: './leads-list.page.html',
  styleUrl: './leads-list.page.scss'
})
export class LeadsListPageComponent implements OnInit {
  private readonly api = inject(CrmLeadsApi);
  private readonly legalEntitiesApi = inject(LegalEntitiesApi);
  private readonly currencyService = inject(WorkspaceCurrencyService);
  private readonly message = inject(NzMessageService);
  private readonly fb = inject(FormBuilder);

  readonly currency = this.currencyService.currency;
  readonly list = createPagedList<LeadSummaryDto>((q: any) => this.api.list(q));
  readonly entities = signal<{ id: string; name: string }[]>([]);

  readonly createVisible = signal(false);
  readonly convertModalVisible = signal(false);
  readonly detailVisible = signal(false);
  readonly isSubmitting = signal(false);
  readonly isConverting = signal(false);
  readonly selectedLead = signal<LeadSummaryDto | null>(null);

  leadForm!: FormGroup;
  convertForm!: FormGroup;

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
    this.initForms();
    this.loadEntities();
  }

  private initForms(): void {
    this.leadForm = this.fb.group({
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

    this.convertForm = this.fb.group({
      opportunityTitle: ['', [Validators.required]],
      expectedRevenue: [0, [Validators.required, Validators.min(0)]],
      expectedCloseDate: [new Date(Date.now() + 30 * 86400000)]
    });
  }

  private loadEntities(): void {
    this.legalEntitiesApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: res => {
        if (res.items) {
          this.entities.set(res.items.map(e => ({ id: e.id, name: e.name })));
          if (res.items.length > 0) {
            this.leadForm.patchValue({ legalEntityId: res.items[0].id });
          }
        }
      }
    });
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
      expectedCloseDate: new Date(Date.now() + 30 * 86400000)
    });
    this.convertModalVisible.set(true);
  }

  saveLead(): void {
    if (this.leadForm.invalid) {
      Object.values(this.leadForm.controls).forEach(c => { c.markAsDirty(); c.updateValueAndValidity({ onlySelf: true }); });
      return;
    }
    this.isSubmitting.set(true);
    const v = this.leadForm.value;

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
    const v = this.convertForm.value;
    const closeDate = v.expectedCloseDate ? new Date(v.expectedCloseDate).toISOString().slice(0, 10) : undefined;

    this.api.convert(lead.id, {
      opportunityTitle: v.opportunityTitle,
      expectedRevenue: v.expectedRevenue,
      expectedCloseDate: closeDate
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
