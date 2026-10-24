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
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { LegalEntitiesApi } from '../../core/api/admin.api';
import {
  CrmLeadsApi,
  LeadDto,
  LeadSource,
  LeadStatus,
  LeadSummaryDto
} from '../../core/api/crm.api';
import { createPagedList } from '../../core/api/paged-list';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';

@Component({
  selector: 'erp-leads-list-page',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    NzCardModule,
    NzButtonModule,
    NzIconModule,
    NzTagModule,
    NzBadgeModule,
    NzModalModule,
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
      #table
      [list]="list"
      [columns]="columns">
        <button toolbar nz-button nzType="primary" (click)="openCreateDrawer()" data-testid="lead-new">
          <span nz-icon nzType="user-add"></span> Add New Lead
        </button>
          <ng-template #cell let-row let-column="column">
            <ng-container [ngSwitch]="column.key">
              <span *ngSwitchCase="'leadNumber'" class="mono-code font-bold">{{ row.leadNumber }}</span>
              <span *ngSwitchCase="'fullName'">
                <a (click)="viewDetail(row)">{{ row.fullName }}</a>
              </span>
              <span *ngSwitchCase="'companyName'">
                <span>{{ row.companyName || 'Individual' }}</span>
              </span>
              <span *ngSwitchCase="'email'">
                <small>{{ row.email || '-' }}</small>
              </span>
              <span *ngSwitchCase="'phone'">
                <small>{{ row.phone || '-' }}</small>
              </span>
              <span *ngSwitchCase="'source'">
                <nz-tag>{{ row.source }}</nz-tag>
              </span>
              <span *ngSwitchCase="'status'">
                <nz-tag [nzColor]="getStatusColor(row.status)">{{ row.status }}</nz-tag>
              </span>
              <span *ngSwitchCase="'estimatedValue'">
                <strong>\${{ row.estimatedValue | number:'1.0-0' }}</strong>
              </span>
              <span *ngSwitchCase="'assignedUserName'">
                <span>{{ row.assignedUserName || 'Unassigned' }}</span>
              </span>
              <span *ngSwitchCase="'actions'">
                <div class="table-actions">
                  <button
                    *ngIf="row.status !== 'Converted'"
                    nz-button nzType="link" nzSize="small" class="text-primary"
                    (click)="openConvertModal(row)">
                    <span nz-icon nzType="arrow-right"></span> Convert to Deal
                  </button>
                  <button nz-button nzType="link" nzSize="small" (click)="viewDetail(row)">
                    <span nz-icon nzType="eye"></span> Details
                  </button>
                </div>
              </span>
            </ng-container>
          </ng-template>
        </erp-paged-table>

      <!-- Create Lead Drawer -->
      <nz-drawer
        [nzVisible]="createVisible()"
        [nzWidth]="620"
        nzTitle="Acquire New Lead / Prospect"
        (nzOnClose)="createVisible.set(false)">
        <ng-container *nzDrawerContent>
          <form [formGroup]="leadForm" nz-form nzLayout="vertical" (ngSubmit)="saveLead()">
            <nz-form-item>
              <nz-form-label nzRequired>Workspace Legal Entity</nz-form-label>
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
                  <input nz-input formControlName="fullName" placeholder="e.g. Michael Scott" />
                </nz-form-control>
              </nz-form-item>

              <nz-form-item>
                <nz-form-label>Company / Organization</nz-form-label>
                <nz-form-control>
                  <input nz-input formControlName="companyName" placeholder="e.g. Dunder Mifflin" />
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="grid grid-cols-2 gap-4">
              <nz-form-item>
                <nz-form-label>Email Address</nz-form-label>
                <nz-form-control>
                  <input nz-input formControlName="email" type="email" placeholder="e.g. michael@dunder.com" />
                </nz-form-control>
              </nz-form-item>

              <nz-form-item>
                <nz-form-label>Phone / WhatsApp</nz-form-label>
                <nz-form-control>
                  <input nz-input formControlName="phone" placeholder="+971 50 123 4567" />
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="grid grid-cols-2 gap-4">
              <nz-form-item>
                <nz-form-label>Lead Source</nz-form-label>
                <nz-form-control>
                  <nz-select formControlName="source">
                    <nz-option nzValue="Website" nzLabel="Website Inbound"></nz-option>
                    <nz-option nzValue="Referral" nzLabel="Client Referral"></nz-option>
                    <nz-option nzValue="TradeShow" nzLabel="Trade Show / Exhibition"></nz-option>
                    <nz-option nzValue="ColdCall" nzLabel="Outbound Outreach"></nz-option>
                    <nz-option nzValue="Campaign" nzLabel="Marketing Campaign"></nz-option>
                    <nz-option nzValue="Other" nzLabel="Other Channel"></nz-option>
                  </nz-select>
                </nz-form-control>
              </nz-form-item>

              <nz-form-item>
                <nz-form-label>Estimated Deal Value (\$)</nz-form-label>
                <nz-form-control>
                  <nz-input-number formControlName="estimatedValue" [nzMin]="0" class="w-full"></nz-input-number>
                </nz-form-control>
              </nz-form-item>
            </div>

            <nz-form-item>
              <nz-form-label>Discovery Notes & Requirements</nz-form-label>
              <nz-form-control>
                <textarea nz-input formControlName="notes" rows="3" placeholder="Client business scale, pain points, timeline expectations..."></textarea>
              </nz-form-control>
            </nz-form-item>

            <div class="drawer-footer">
              <button nz-button type="button" (click)="createVisible.set(false)">Cancel</button>
              <button nz-button nzType="primary" type="submit" [nzLoading]="isSubmitting()">Save Prospect</button>
            </div>
          </form>
        </ng-container>
      </nz-drawer>

      <!-- Convert to Deal Modal -->
      <nz-modal
        [nzVisible]="convertModalVisible()"
        nzTitle="Convert Lead to Pipeline Deal"
        [nzOkLoading]="isConverting()"
        (nzOnCancel)="convertModalVisible.set(false)"
        (nzOnOk)="submitConvertLead()">
        <ng-container *nzModalContent>
          <form [formGroup]="convertForm" nz-form nzLayout="vertical">
            <div *ngIf="selectedLead() as lead" class="mb-4 convert-summary-card">
              <p><strong>Prospect:</strong> {{ lead.fullName }} ({{ lead.companyName || 'Individual' }})</p>
              <p><strong>Estimated:</strong> \${{ lead.estimatedValue | number:'1.0-0' }}</p>
            </div>

            <nz-form-item>
              <nz-form-label nzRequired>Opportunity / Deal Title</nz-form-label>
              <nz-form-control nzErrorTip="Deal title is required">
                <input nz-input formControlName="opportunityTitle" placeholder="e.g. Enterprise Cloud ERP Implementation" />
              </nz-form-control>
            </nz-form-item>

            <div class="grid grid-cols-2 gap-4">
              <nz-form-item>
                <nz-form-label nzRequired>Expected Deal Value (\$)</nz-form-label>
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
        nzTitle="Prospect Profile"
        (nzOnClose)="detailVisible.set(false)">
        <ng-container *nzDrawerContent>
          <div *ngIf="selectedLead() as lead" class="lead-detail-box">
            <div class="detail-header">
              <h3>{{ lead.fullName }}</h3>
              <p class="text-secondary">{{ lead.companyName || 'Individual Prospect' }} · {{ lead.jobTitle || 'Decision Maker' }}</p>
            </div>

            <div class="grid grid-cols-2 gap-4 mt-4">
              <div>
                <span class="label">Lead Number:</span>
                <p class="mono-code font-bold">{{ lead.leadNumber }}</p>
              </div>
              <div>
                <span class="label">Current Status:</span>
                <p><nz-tag [nzColor]="getStatusColor(lead.status)">{{ lead.status }}</nz-tag></p>
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
                <p><nz-tag>{{ lead.source }}</nz-tag></p>
              </div>
              <div>
                <span class="label">Estimated Deal:</span>
                <p class="text-primary font-bold">\${{ lead.estimatedValue | number:'1.0-0' }}</p>
              </div>
            </div>

            <div class="mt-4 notes-card">
              <span class="label">Discovery & Background Notes:</span>
              <p class="mt-1">{{ lead.notes || 'No discovery notes recorded.' }}</p>
            </div>
          </div>
        </ng-container>
      </nz-drawer>
  `,
  styles: [`
    .text-secondary { color: var(--erp-text-secondary, #64748b); }
    .text-primary { color: var(--erp-primary, #0ea5e9); }
    .mono-code { font-family: monospace; color: #1890ff; }
    .font-bold { font-weight: 600; }
    .convert-summary-card { background: var(--erp-bg-secondary, #f8fafc); padding: 0.75rem; border-radius: 6px; }
    .convert-summary-card p { margin: 0 0 0.25rem 0; font-size: 0.9rem; }
    .drawer-footer { display: flex; justify-content: flex-end; gap: 0.75rem; margin-top: 2rem; }
    .lead-detail-box .detail-header h3 { margin: 0; }
    .label { font-size: 0.75rem; color: #64748b; text-transform: uppercase; }
    .notes-card { background: var(--erp-bg-secondary, #f8fafc); padding: 0.75rem; border-radius: 6px; }
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
  private readonly message = inject(NzMessageService);
  private readonly fb = inject(FormBuilder);

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

  readonly columns: TableColumn<LeadSummaryDto>[] = [
    { key: 'leadNumber', label: 'Lead #', sortField: 'leadNumber' },
    { key: 'fullName', label: 'Contact Name', sortField: 'fullName' },
    { key: 'companyName', label: 'Company / Org', sortField: 'companyName' },
    { key: 'email', label: 'Email' },
    { key: 'phone', label: 'Phone' },
    { key: 'source', label: 'Source' },
    { key: 'status', label: 'Status', sortField: 'status' },
    { key: 'estimatedValue', label: 'Est. Value', sortField: 'estimatedValue' },
    { key: 'assignedUserName', label: 'Owner' },
    { key: 'actions', label: 'Actions', align: 'right' }
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
      email: [''],
      phone: [''],
      source: ['Website'],
      estimatedValue: [10000, [Validators.required, Validators.min(0)]],
      notes: ['']
    });

    this.convertForm = this.fb.group({
      opportunityTitle: ['', [Validators.required]],
      expectedRevenue: [10000, [Validators.required, Validators.min(0)]],
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

  getStatusColor(status: LeadStatus): string {
    switch (status) {
      case 'New': return 'blue';
      case 'Contacted': return 'cyan';
      case 'Qualified': return 'purple';
      case 'Converted': return 'success';
      case 'Unqualified': return 'default';
      default: return 'default';
    }
  }

  openCreateDrawer(): void {
    this.leadForm.reset({
      legalEntityId: this.entities()[0]?.id || '',
      source: 'Website',
      estimatedValue: 10000
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
      expectedRevenue: row.estimatedValue || 15000,
      expectedCloseDate: new Date(Date.now() + 30 * 86400000)
    });
    this.convertModalVisible.set(true);
  }

  saveLead(): void {
    if (this.leadForm.invalid) {
      Object.values(this.leadForm.controls).forEach(c => c.markAsDirty());
      return;
    }
    this.isSubmitting.set(true);
    const v = this.leadForm.value;

    this.api.create(v).subscribe({
      next: () => {
        this.message.success('Prospect lead acquired');
        this.createVisible.set(false);
        this.isSubmitting.set(false);
        this.list.reload();
      },
      error: () => {
        this.message.error('Failed to create lead');
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
        this.message.success('Lead converted to sales pipeline opportunity!');
        this.convertModalVisible.set(false);
        this.isConverting.set(false);
        this.list.reload();
      },
      error: () => {
        this.message.error('Failed to convert lead');
        this.isConverting.set(false);
      }
    });
  }
}
