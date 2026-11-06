import { CommonModule } from '@angular/common';
import { problemMessage } from '../../core/http/problem-details';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzBadgeModule } from 'ng-zorro-antd/badge';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { LegalEntitiesApi } from '../../core/api/admin.api';
import {
  CrmLeadsApi,
  LeadStatus,
  LeadSummaryDto
} from '../../core/api/crm.api';
import { createPagedList } from '../../core/api/paged-list';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { StatusTagComponent } from '../../shared/status-tag.component';
import { displayLabel } from '../../core/format/display';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { ErpFormModule } from '../../core/forms/erp-form.module';
import { showFormErrors } from '../../core/forms/form-errors';

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
    ErpFormModule,
    NzInputModule,
    NzInputNumberModule,
    NzDatePickerModule,
    NzSelectModule,
    PagedTableComponent,
    HasPermissionDirective,
  ],
  template: `
    <erp-paged-table
      #table
      [list]="list"
      [columns]="columns"
      searchPlaceholder="Search name, company, email or phone"
      [clickable]="false"
      emptyText="No leads yet. Add one to start your pipeline."
      exportFilename="leads">
        <button toolbar *erpHasPermission="'crm.leads.manage'" nz-button nzType="primary" (click)="openCreateDrawer()" data-testid="lead-new">
          <span nz-icon nzType="user-add"></span> Add New Lead
        </button>
          <ng-template #cell let-row let-column="column" let-value="value">
            @switch (column.key) {
              @case ('leadNumber') { <span class="num-cell">{{ row.leadNumber }}</span> }
              @case ('fullName') {
                <div class="stack">
                  <a (click)="viewDetail(row)">{{ row.fullName }}</a>
                  @if (row.companyName) { <small class="text-secondary">{{ row.companyName }}</small> }
                </div>
              }
              @case ('email') {
                <div class="stack">
                  <small>{{ row.email || '-' }}</small>
                  @if (row.phone) { <small class="text-secondary nowrap">{{ row.phone }}</small> }
                </div>
              }
              @case ('source') { {{ value }} }
              @case ('status') { <erp-status [value]="row.status" /> }
              @case ('estimatedValue') { <strong class="amount">{{ currency() }} {{ row.estimatedValue | number:'1.0-0' }}</strong> }
              @case ('assignedUserName') { {{ row.assignedUserName || 'Unassigned' }} }
              @case ('actions') {
                <div class="table-actions">
                  @if (canConvert(row)) {
                    <button
                      *erpHasPermission="'crm.leads.manage'"
                      nz-button nzType="link" nzSize="small"
                      (click)="openConvertModal(row)" data-testid="lead-convert">
                      <span nz-icon nzType="arrow-right"></span> Convert
                    </button>
                  }
                  @if (row.status !== 'converted') {
                    <button
                      *erpHasPermission="'crm.leads.manage'"
                      nz-button nzType="link" nzSize="small" nz-dropdown nzTrigger="click" [nzDropdownMenu]="statusMenu" data-testid="lead-status">
                      Status <span nz-icon nzType="down"></span>
                    </button>
                    <nz-dropdown-menu #statusMenu="nzDropdownMenu">
                      <ul nz-menu>
                        @for (s of statusMoves(row); track s) {
                          <li nz-menu-item (click)="setStatus(row, s)">{{ label(s) }}</li>
                        }
                      </ul>
                    </nz-dropdown-menu>
                  }
                  <button nz-button nzType="link" nzSize="small" (click)="viewDetail(row)">
                    <span nz-icon nzType="eye"></span> Details
                  </button>
                </div>
              }
              @default { {{ value }} }
            }
          </ng-template>
        </erp-paged-table>

      <!-- Create Lead Drawer -->
      <nz-drawer
        [nzVisible]="createVisible()"
        [nzWidth]="620"
        nzTitle="New Lead"
        (nzOnClose)="createVisible.set(false)">
        <ng-container *nzDrawerContent>
          <form [formGroup]="leadForm" nz-form nzLayout="vertical" (ngSubmit)="saveLead()">
            <nz-form-item>
              <nz-form-label nzRequired>Legal Entity</nz-form-label>
              <nz-form-control nzErrorTip="Please select an entity">
                <nz-select formControlName="legalEntityId" nzPlaceHolder="Select Legal Entity">
                  <nz-option *ngFor="let ent of entities()" [nzValue]="ent.id" [nzLabel]="ent.name"></nz-option>
                </nz-select>
              </nz-form-control>
            </nz-form-item>

            <div class="grid grid-cols-2 gap-4">
              <nz-form-item>
                <nz-form-label nzRequired>Contact Full Name</nz-form-label>
                <nz-form-control nzErrorTip="Name is required">
                  <input nz-input formControlName="fullName" placeholder="Full name" />
                </nz-form-control>
              </nz-form-item>

              <nz-form-item>
                <nz-form-label>Company / Organization</nz-form-label>
                <nz-form-control>
                  <input nz-input formControlName="companyName" placeholder="Company name" />
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="grid grid-cols-2 gap-4">
              <nz-form-item>
                <nz-form-label>Email Address</nz-form-label>
                <nz-form-control nzErrorTip="Enter a valid email address.">
                  <input nz-input formControlName="email" type="email" placeholder="name@company.com" />
                </nz-form-control>
              </nz-form-item>

              <nz-form-item>
                <nz-form-label>Phone / WhatsApp</nz-form-label>
                <nz-form-control>
                  <input nz-input formControlName="phone" placeholder="Phone number" />
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="grid grid-cols-2 gap-4">
              <nz-form-item>
                <nz-form-label>Lead Source</nz-form-label>
                <nz-form-control>
                  <nz-select formControlName="source">
                    <nz-option nzValue="website" nzLabel="Website Inbound"></nz-option>
                    <nz-option nzValue="referral" nzLabel="Client Referral"></nz-option>
                    <nz-option nzValue="tradeShow" nzLabel="Trade Show / Exhibition"></nz-option>
                    <nz-option nzValue="coldCall" nzLabel="Outbound Outreach"></nz-option>
                    <nz-option nzValue="campaign" nzLabel="Marketing Campaign"></nz-option>
                    <nz-option nzValue="other" nzLabel="Other Channel"></nz-option>
                  </nz-select>
                </nz-form-control>
              </nz-form-item>

              <nz-form-item>
                <nz-form-label>Estimated Value ({{ currency() }})</nz-form-label>
                <nz-form-control>
                  <nz-input-number formControlName="estimatedValue" [nzMin]="0" class="w-full"></nz-input-number>
                </nz-form-control>
              </nz-form-item>
            </div>

            <nz-form-item>
              <nz-form-label>Notes</nz-form-label>
              <nz-form-control>
                <textarea nz-input formControlName="notes" rows="3" placeholder="What they need, budget, timing..."></textarea>
              </nz-form-control>
            </nz-form-item>

            <div class="drawer-footer">
              <button nz-button type="button" (click)="createVisible.set(false)">Cancel</button>
              <button nz-button nzType="primary" type="submit" [nzLoading]="isSubmitting()">Save Lead</button>
            </div>
          </form>
        </ng-container>
      </nz-drawer>

      <!-- Convert to Deal Modal -->
      <nz-modal
        [nzVisible]="convertModalVisible()"
        nzTitle="Convert Lead to Deal"
        [nzOkLoading]="isConverting()"
        (nzOnCancel)="convertModalVisible.set(false)"
        (nzOnOk)="submitConvertLead()">
        <ng-container *nzModalContent>
          <form [formGroup]="convertForm" nz-form nzLayout="vertical">
            <div *ngIf="selectedLead() as lead" class="mb-4 convert-summary-card">
              <p><strong>Lead:</strong> {{ lead.fullName }}@if (lead.companyName) { ({{ lead.companyName }}) }</p>
              <p><strong>Estimated:</strong> {{ currency() }} {{ lead.estimatedValue | number:'1.0-0' }}</p>
            </div>

            <nz-form-item>
              <nz-form-label nzRequired>Deal Title</nz-form-label>
              <nz-form-control nzErrorTip="Deal title is required">
                <input nz-input formControlName="opportunityTitle" placeholder="e.g. Annual supply contract" />
              </nz-form-control>
            </nz-form-item>

            <div class="grid grid-cols-2 gap-4">
              <nz-form-item>
                <nz-form-label nzRequired>Expected Value ({{ currency() }})</nz-form-label>
                <nz-form-control>
                  <nz-input-number formControlName="expectedRevenue" [nzMin]="0" class="w-full"></nz-input-number>
                </nz-form-control>
              </nz-form-item>

              <nz-form-item>
                <nz-form-label>Target Close Date</nz-form-label>
                <nz-form-control>
                  <nz-date-picker formControlName="expectedCloseDate" class="w-full"></nz-date-picker>
                </nz-form-control>
              </nz-form-item>
            </div>
          </form>
        </ng-container>
      </nz-modal>

      <!-- Detail Drawer -->
      <nz-drawer
        [nzVisible]="detailVisible()"
        [nzWidth]="550"
        nzTitle="Lead Details"
        (nzOnClose)="detailVisible.set(false)">
        <ng-container *nzDrawerContent>
          <div *ngIf="selectedLead() as lead" class="lead-detail-box">
            <div class="detail-header">
              <h3>{{ lead.fullName }}</h3>
              <p class="text-secondary">{{ leadSubtitle(lead) }}</p>
            </div>

            <div class="grid grid-cols-2 gap-4 mt-4">
              <div>
                <span class="label">Lead Number:</span>
                <p class="mono-code font-bold">{{ lead.leadNumber }}</p>
              </div>
              <div>
                <span class="label">Current Status:</span>
                <p><erp-status [value]="lead.status" /></p>
              </div>
              <div>
                <span class="label">Email:</span>
                <p>{{ lead.email || 'N/A' }}</p>
              </div>
              <div>
                <span class="label">Phone:</span>
                <p>{{ lead.phone || 'N/A' }}</p>
              </div>
              <div>
                <span class="label">Source:</span>
                <p>{{ label(lead.source) }}</p>
              </div>
              <div>
                <span class="label">Estimated Deal:</span>
                <p class="text-primary font-bold">{{ currency() }} {{ lead.estimatedValue | number:'1.0-0' }}</p>
              </div>
            </div>

            <div class="mt-4 notes-card">
              <span class="label">Notes:</span>
              <p class="mt-1">{{ lead.notes || 'No notes yet.' }}</p>
            </div>
          </div>
        </ng-container>
      </nz-drawer>
  `,
  styles: [`
    .text-secondary { color: var(--erp-color-text-secondary); }
    .text-primary { color: var(--erp-color-primary-text); }
    .num-cell { white-space: nowrap; font-weight: 600; color: var(--erp-color-primary-text); }
    .stack { display: flex; flex-direction: column; min-width: 0; }
    .nowrap { white-space: nowrap; }
    .amount { font-variant-numeric: tabular-nums; white-space: nowrap; }
    .table-actions { display: flex; justify-content: flex-end; align-items: center; gap: 4px; white-space: nowrap; }
    .mono-code { font-family: monospace; color: var(--erp-color-primary-text); }
    .font-bold { font-weight: 600; }
    .convert-summary-card { background: var(--erp-color-bg-subtle); padding: 0.75rem; border-radius: 6px; }
    .convert-summary-card p { margin: 0 0 0.25rem 0; font-size: 0.9rem; }
    .drawer-footer { display: flex; justify-content: flex-end; gap: 0.75rem; margin-top: 2rem; }
    .lead-detail-box .detail-header h3 { margin: 0; }
    .label { font-size: 0.75rem; color: var(--erp-color-text-muted); text-transform: uppercase; }
    .notes-card { background: var(--erp-color-bg-subtle); padding: 0.75rem; border-radius: 6px; }
    .grid { display: grid; }
    .grid-cols-2 { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    .gap-4 { gap: 1rem; }
    .w-full { width: 100%; }
    .mb-4 { margin-bottom: 1rem; }
    .mt-4 { margin-top: 1rem; }
    .mt-1 { margin-top: 0.25rem; }
  `]
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
      showFormErrors(this.leadForm);
      return;
    }
    this.isSubmitting.set(true);
    const v = this.leadForm.value;

    this.api.create(v).subscribe({
      next: () => {
        this.message.success('Lead added.');
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
    if (this.convertForm.invalid) { showFormErrors(this.convertForm); return; }
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
        this.message.success('Lead converted. The deal is in the pipeline.');
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
