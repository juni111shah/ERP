import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
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
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { LegalEntitiesApi } from '../../core/api/admin.api';
import { createPagedList } from '../../core/api/paged-list';
import { PartiesApi, PartyDto } from '../parties.page';
import {
  EngagementDto,
  EngagementInput,
  EngagementStatus,
  EngagementSummaryDto,
  EngagementType,
  ServicesEngagementsApi
} from '../../core/api/services.api';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';

@Component({
  selector: 'erp-engagements-list-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    NzCardModule,
    NzButtonModule,
    NzIconModule,
    NzTagModule,
    NzBadgeModule,
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
  ],
  template: `
    <erp-paged-table
      [list]="list"
      [columns]="columns">
      <button toolbar *erpHasPermission="'services.engagements.manage'" nz-button nzType="primary" (click)="openCreateDrawer()" data-testid="engagement-new">
        <span nz-icon nzType="plus"></span> New Engagement
      </button>
          <ng-template #cell let-row let-column="column">
            <ng-container [ngSwitch]="column.key">

              <ng-container *ngSwitchCase="'code'">
                <span class="font-mono font-semibold text-primary">{{ row.engagementCode }}</span>
              </ng-container>

              <ng-container *ngSwitchCase="'title'">
                <div>
                  <div class="font-medium text-slate-800 dark:text-slate-100">{{ row.title }}</div>
                  <div class="text-xs text-secondary">{{ row.clientName }}</div>
                </div>
              </ng-container>

              <ng-container *ngSwitchCase="'type'">
                <nz-tag [nzColor]="getTypeColor(row.type)">{{ row.type }}</nz-tag>
              </ng-container>

              <ng-container *ngSwitchCase="'hours'">
                <div style="min-width: 140px;">
                  <div class="text-xs flex justify-between mb-1">
                    <span>{{ row.consumedHours }} hrs</span>
                    <span class="text-secondary">of {{ row.budgetedHours }} hrs</span>
                  </div>
                  <nz-progress
                    [nzPercent]="getHoursPercent(row)"
                    [nzStatus]="row.consumedHours > row.budgetedHours && row.budgetedHours > 0 ? 'exception' : 'active'"
                    [nzShowInfo]="false"
                    [nzStrokeWidth]="6">
                  </nz-progress>
                </div>
              </ng-container>

              <ng-container *ngSwitchCase="'contractValue'">
                <span class="font-medium font-mono">{{ row.contractValue | number:'1.2-2' }}</span>
              </ng-container>

              <ng-container *ngSwitchCase="'status'">
                <nz-badge [nzStatus]="getStatusBadge(row.status)" [nzText]="row.status"></nz-badge>
              </ng-container>

              <ng-container *ngSwitchCase="'actions'">
                <div class="table-actions">
                  <button aria-label="Update Status" *erpHasPermission="'services.engagements.manage'" nz-button nzType="text" nzSize="small" (click)="openStatusModal(row)" title="Update Status">
                    <span nz-icon nzType="edit"></span>
                  </button>
                  <button aria-label="View Details" nz-button nzType="text" nzSize="small" (click)="viewDetails(row)" title="View Details">
                    <span nz-icon nzType="eye"></span>
                  </button>
                </div>
              </ng-container>

            </ng-container>
          </ng-template>
        </erp-paged-table>

      <!-- Create Engagement Drawer -->
      <nz-drawer
        [nzVisible]="drawerVisible()"
        nzTitle="Create Service Engagement"
        [nzWidth]="520"
        (nzOnClose)="drawerVisible.set(false)">
        <ng-container *nzDrawerContent>
          <form nz-form [formGroup]="form" (ngSubmit)="submitEngagement()" nzLayout="vertical">
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
              <nz-form-label nzRequired>Engagement Title</nz-form-label>
              <nz-form-control nzErrorTip="Enter title">
                <input nz-input formControlName="title" placeholder="e.g. Enterprise Cloud Architecture Overhaul" />
              </nz-form-control>
            </nz-form-item>

            <div class="grid grid-cols-2 gap-4">
              <nz-form-item>
                <nz-form-label nzRequired>Client / Customer</nz-form-label>
                <nz-form-control nzErrorTip="Select client">
                  <nz-select formControlName="clientId" nzShowSearch nzPlaceHolder="Select Client" (ngModelChange)="onClientSelected($event)">
                    @for (c of clients(); track c.id) {
                      <nz-option [nzValue]="c.id" [nzLabel]="c.name + ' (' + c.code + ')'"></nz-option>
                    }
                  </nz-select>
                </nz-form-control>
              </nz-form-item>

              <nz-form-item>
                <nz-form-label nzRequired>Billing Model</nz-form-label>
                <nz-form-control>
                  <nz-select formControlName="type">
                    <nz-option nzValue="FixedPrice" nzLabel="Fixed Price"></nz-option>
                    <nz-option nzValue="TimeAndMaterials" nzLabel="Time & Materials (T&M)"></nz-option>
                    <nz-option nzValue="Retainer" nzLabel="Retainer Agreement"></nz-option>
                  </nz-select>
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="grid grid-cols-2 gap-4">
              <nz-form-item>
                <nz-form-label nzRequired>Contract Value</nz-form-label>
                <nz-form-control>
                  <nz-input-number formControlName="contractValue" [nzMin]="0" [nzStep]="1000" class="w-full"></nz-input-number>
                </nz-form-control>
              </nz-form-item>

              <nz-form-item>
                <nz-form-label nzRequired>Budgeted Hours</nz-form-label>
                <nz-form-control>
                  <nz-input-number formControlName="budgetedHours" [nzMin]="0" [nzStep]="10" class="w-full"></nz-input-number>
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="grid grid-cols-2 gap-4">
              <nz-form-item>
                <nz-form-label nzRequired>Start Date</nz-form-label>
                <nz-form-control>
                  <nz-date-picker formControlName="startDate" class="w-full"></nz-date-picker>
                </nz-form-control>
              </nz-form-item>

              <nz-form-item>
                <nz-form-label>Target End Date</nz-form-label>
                <nz-form-control>
                  <nz-date-picker formControlName="targetEndDate" class="w-full"></nz-date-picker>
                </nz-form-control>
              </nz-form-item>
            </div>

            <nz-form-item>
              <nz-form-label>Lead Consultant / Partner</nz-form-label>
              <nz-form-control>
                <input nz-input formControlName="leadConsultantName" placeholder="Managing Principal or Lead Architect" />
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-label>Scope Description</nz-form-label>
              <nz-form-control>
                <textarea nz-input formControlName="description" rows="3" placeholder="Engagement objectives, key milestones and deliverables..."></textarea>
              </nz-form-control>
            </nz-form-item>

            <div class="drawer-footer">
              <button nz-button type="button" (click)="drawerVisible.set(false)">Cancel</button>
              <button nz-button nzType="primary" [nzLoading]="isSubmitting()" type="submit" [disabled]="form.invalid">
                Create Engagement
              </button>
            </div>
          </form>
        </ng-container>
      </nz-drawer>

      <!-- Status Modal -->
      <nz-modal
        [nzVisible]="statusModalVisible()"
        nzTitle="Update Engagement Lifecycle Status"
        (nzOnCancel)="statusModalVisible.set(false)"
        (nzOnOk)="saveStatus()">
        <ng-container *nzModalContent>
          <div class="py-2">
            <p class="mb-4">Select the new delivery status for <strong>{{ selectedEngagement()?.engagementCode }}</strong>:</p>
            <nz-select [(ngModel)]="newStatus" class="w-full">
              <nz-option nzValue="Draft" nzLabel="Draft"></nz-option>
              <nz-option nzValue="Active" nzLabel="Active (In Delivery)"></nz-option>
              <nz-option nzValue="OnHold" nzLabel="On Hold (Paused)"></nz-option>
              <nz-option nzValue="Completed" nzLabel="Completed (Finalized)"></nz-option>
              <nz-option nzValue="Cancelled" nzLabel="Cancelled"></nz-option>
            </nz-select>
          </div>
        </ng-container>
      </nz-modal>

      <!-- Details Drawer -->
      <nz-drawer
        [nzVisible]="detailsVisible()"
        nzTitle="Engagement Overview"
        [nzWidth]="480"
        (nzOnClose)="detailsVisible.set(false)">
        <ng-container *nzDrawerContent>
          @if (selectedDetails(); as eng) {
            <div class="space-y-4">
              <div>
                <nz-tag [nzColor]="getTypeColor(eng.type)">{{ eng.type }}</nz-tag>
                <nz-badge [nzStatus]="getStatusBadge(eng.status)" [nzText]="eng.status" class="ml-2"></nz-badge>
              </div>
              <h3 class="text-lg font-semibold">{{ eng.title }}</h3>
              <p class="text-sm text-secondary">{{ eng.description || 'No detailed scope recorded.' }}</p>

              <nz-card nzSize="small" nzTitle="Engagement Financials & Hours">
                <div class="grid grid-cols-2 gap-2 text-sm">
                  <div>Contract Value:</div>
                  <div class="font-mono font-medium">{{ eng.contractValue | number:'1.2-2' }}</div>
                  <div>Budgeted Hours:</div>
                  <div class="font-mono font-medium">{{ eng.budgetedHours }} hrs</div>
                  <div>Consumed Hours:</div>
                  <div class="font-mono font-medium">{{ eng.consumedHours }} hrs</div>
                  <div>Burned Cost:</div>
                  <div class="font-mono font-medium">{{ eng.consumedCost | number:'1.2-2' }}</div>
                </div>
              </nz-card>

              <nz-card nzSize="small" nzTitle="Delivery Schedule">
                <div class="grid grid-cols-2 gap-2 text-sm">
                  <div>Start Date:</div>
                  <div>{{ eng.startDate }}</div>
                  <div>Target End:</div>
                  <div>{{ eng.targetEndDate || 'Ongoing' }}</div>
                  <div>Lead Consultant:</div>
                  <div>{{ eng.leadConsultantName || 'Not Assigned' }}</div>
                </div>
              </nz-card>
            </div>
          }
        </ng-container>
      </nz-drawer>
  `,
  styles: [`
    .text-secondary { color: rgba(0, 0, 0, 0.45); }
    .table-actions { display: flex; gap: 4px; }
    .drawer-footer { display: flex; justify-content: flex-end; gap: 8px; margin-top: 24px; padding-top: 16px; border-top: 1px solid #f0f0f0; }
  `]
})
export class EngagementsListPage implements OnInit {
  private readonly api = inject(ServicesEngagementsApi);
  private readonly legalEntitiesApi = inject(LegalEntitiesApi);
  private readonly partiesApi = inject(PartiesApi);
  private readonly fb = inject(FormBuilder);
  private readonly msg = inject(NzMessageService);

  readonly list = createPagedList<EngagementSummaryDto>((q) => this.api.list(q), {
    pageSize: 15,
    sort: 'createdAt:desc'
  });

  readonly columns: TableColumn<EngagementSummaryDto>[] = [
    { key: 'code', label: 'Code', sortField: 'engagementCode', width: '120px' },
    { key: 'title', label: 'Engagement & Client', sortField: 'title' },
    { key: 'type', label: 'Billing Model', sortField: 'type', width: '150px' },
    { key: 'hours', label: 'Hours Burn', width: '180px' },
    { key: 'contractValue', label: 'Contract Value', sortField: 'contractValue', align: 'right', width: '140px' },
    { key: 'status', label: 'Status', sortField: 'status', width: '130px' },
    { key: 'actions', label: 'Actions', width: '90px' }
  ];

  readonly legalEntities = signal<{ id: string; name: string }[]>([]);
  readonly clients = signal<PartyDto[]>([]);
  readonly drawerVisible = signal(false);
  readonly detailsVisible = signal(false);
  readonly statusModalVisible = signal(false);
  readonly isSubmitting = signal(false);
  readonly selectedEngagement = signal<EngagementSummaryDto | null>(null);
  readonly selectedDetails = signal<EngagementDto | null>(null);

  newStatus: EngagementStatus = 'Active';

  form: FormGroup = this.fb.group({
    legalEntityId: ['', [Validators.required]],
    title: ['', [Validators.required, Validators.maxLength(200)]],
    clientId: [null, [Validators.required]],
    clientName: ['', [Validators.required]],
    type: ['FixedPrice' as EngagementType, [Validators.required]],
    contractValue: [25000, [Validators.required, Validators.min(0)]],
    budgetedHours: [120, [Validators.required, Validators.min(0)]],
    startDate: [new Date(), [Validators.required]],
    targetEndDate: [null],
    leadConsultantName: [''],
    description: ['']
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

    this.partiesApi.list({ page: 1, pageSize: 200 }).subscribe({
      next: res => {
        const custs = res.items.filter(p => !p.roles || p.roles.length === 0 || p.roles.includes('Customer') || p.roles.some(r => r.toLowerCase().includes('client')));
        this.clients.set(custs.length > 0 ? custs : res.items);
      },
      error: () => {}
    });
  }

  onClientSelected(id: string): void {
    const c = this.clients().find(x => x.id === id);
    if (c) {
      this.form.patchValue({ clientId: c.id, clientName: c.name });
    }
  }

  openCreateDrawer(): void {
    this.form.reset({
      legalEntityId: this.legalEntities()[0]?.id || '',
      title: '',
      clientId: null,
      clientName: '',
      type: 'FixedPrice',
      contractValue: 25000,
      budgetedHours: 120,
      startDate: new Date(),
      targetEndDate: null,
      leadConsultantName: '',
      description: ''
    });
    this.drawerVisible.set(true);
  }

  getTypeColor(type: EngagementType): string {
    switch (type) {
      case 'FixedPrice': return 'geekblue';
      case 'TimeAndMaterials': return 'cyan';
      case 'Retainer': return 'purple';
      default: return 'default';
    }
  }

  getStatusBadge(status: EngagementStatus): 'success' | 'processing' | 'default' | 'error' | 'warning' {
    switch (status) {
      case 'Active': return 'success';
      case 'Draft': return 'default';
      case 'Completed': return 'processing';
      case 'OnHold': return 'warning';
      case 'Cancelled': return 'error';
      default: return 'default';
    }
  }

  getHoursPercent(row: EngagementSummaryDto): number {
    if (!row.budgetedHours || row.budgetedHours === 0) return 0;
    return Math.min(Math.round((row.consumedHours / row.budgetedHours) * 100), 100);
  }

  openStatusModal(row: EngagementSummaryDto): void {
    this.selectedEngagement.set(row);
    this.newStatus = row.status;
    this.statusModalVisible.set(true);
  }

  saveStatus(): void {
    const item = this.selectedEngagement();
    if (!item) return;

    this.api.updateStatus(item.id, this.newStatus).subscribe({
      next: () => {
        this.msg.success('Engagement status updated');
        this.statusModalVisible.set(false);
        this.list.reload();
      },
      error: () => this.msg.error('Failed to update status')
    });
  }

  viewDetails(row: EngagementSummaryDto): void {
    this.api.getEngagement(row.id).subscribe({
      next: (dto: EngagementDto) => {
        this.selectedDetails.set(dto);
        this.detailsVisible.set(true);
      },
      error: () => this.msg.error('Failed to load engagement details')
    });
  }

  submitEngagement(): void {
    if (this.form.invalid) return;

    this.isSubmitting.set(true);
    const val = this.form.value;
    const input: EngagementInput = {
      legalEntityId: val.legalEntityId,
      title: val.title,
      clientId: val.clientId,
      clientName: val.clientName,
      type: val.type,
      contractValue: val.contractValue,
      budgetedHours: val.budgetedHours,
      startDate: val.startDate instanceof Date ? val.startDate.toISOString().substring(0, 10) : val.startDate,
      targetEndDate: val.targetEndDate instanceof Date ? val.targetEndDate.toISOString().substring(0, 10) : null,
      leadConsultantName: val.leadConsultantName || null,
      description: val.description || null
    };

    this.api.create(input).subscribe({
      next: () => {
        this.msg.success('Engagement created successfully');
        this.isSubmitting.set(false);
        this.drawerVisible.set(false);
        this.list.reload();
      },
      error: () => {
        this.msg.error('Failed to create engagement');
        this.isSubmitting.set(false);
      }
    });
  }
}
