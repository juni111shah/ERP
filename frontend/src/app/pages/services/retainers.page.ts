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
import { NzTagModule } from 'ng-zorro-antd/tag';
import { LegalEntitiesApi } from '../../core/api/admin.api';
import { createPagedList } from '../../core/api/paged-list';
import { PartiesApi, PartyDto } from '../parties.page';
import {
  EngagementSummaryDto,
  RetainerCycle,
  RetainerInput,
  RetainerStatus,
  RetainerSummaryDto,
  ServicesEngagementsApi,
  ServicesRetainersApi
} from '../../core/api/services.api';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';

@Component({
  selector: 'erp-retainers-page',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    NzCardModule,
    NzButtonModule,
    NzIconModule,
    NzTagModule,
    NzBadgeModule,
    NzDrawerModule,
    NzFormModule,
    NzInputModule,
    NzInputNumberModule,
    NzDatePickerModule,
    NzSelectModule,
    HasPermissionDirective,
    PagedTableComponent
  ],
  template: `
    <erp-paged-table
      [list]="list"
      [columns]="columns">
      <button toolbar nz-button nzType="primary" *erpHasPermission="'services.retainers.manage'" (click)="openCreateDrawer()" data-testid="retainer-new">
        <span nz-icon nzType="plus"></span> New Retainer Contract
      </button>
          <ng-template #cell let-row let-column="column">
            <ng-container [ngSwitch]="column.key">

              <ng-container *ngSwitchCase="'number'">
                <span class="font-mono font-semibold text-primary">{{ row.retainerNumber }}</span>
              </ng-container>

              <ng-container *ngSwitchCase="'title'">
                <div>
                  <div class="font-medium text-slate-800 dark:text-slate-100">{{ row.title }}</div>
                  <div class="text-xs text-secondary">{{ row.clientName }}</div>
                </div>
              </ng-container>

              <ng-container *ngSwitchCase="'cycle'">
                <nz-tag [nzColor]="getCycleColor(row.cycle)">{{ row.cycle }}</nz-tag>
              </ng-container>

              <ng-container *ngSwitchCase="'fee'">
                <span class="font-mono font-semibold">{{ row.recurringFee | number:'1.2-2' }}</span>
                <span class="text-xs text-secondary ml-1">/ {{ row.cycle }}</span>
              </ng-container>

              <ng-container *ngSwitchCase="'hours'">
                <span class="font-medium">{{ row.includedHours }} hrs</span>
                <span class="text-xs text-secondary ml-1">included</span>
              </ng-container>

              <ng-container *ngSwitchCase="'status'">
                <nz-badge [nzStatus]="getStatusBadge(row.status)" [nzText]="row.status"></nz-badge>
              </ng-container>

              <ng-container *ngSwitchCase="'actions'">
                <div class="table-actions" *erpHasPermission="'services.retainers.manage'">
                  @if (row.status === 'Active') {
                    <button nz-button nzType="text" nzSize="small" (click)="toggleStatus(row, 'Paused')" title="Pause Retainer">
                      <span nz-icon nzType="pause-circle"></span>
                    </button>
                  } @else {
                    <button nz-button nzType="text" nzSize="small" (click)="toggleStatus(row, 'Active')" title="Activate Retainer">
                      <span nz-icon nzType="play-circle"></span>
                    </button>
                  }
                </div>
              </ng-container>

            </ng-container>
          </ng-template>
        </erp-paged-table>

      <!-- Create Retainer Drawer -->
      <nz-drawer
        [nzVisible]="drawerVisible()"
        nzTitle="New Client Retainer Contract"
        [nzWidth]="500"
        (nzOnClose)="drawerVisible.set(false)">
        <ng-container *nzDrawerContent>
          <form nz-form [formGroup]="form" (ngSubmit)="submitRetainer()" nzLayout="vertical">
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
              <nz-form-label nzRequired>Retainer Agreement Title</nz-form-label>
              <nz-form-control nzErrorTip="Enter title">
                <input nz-input formControlName="title" placeholder="e.g. Executive Architecture Retainer" />
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
                <nz-form-label>Service Engagement</nz-form-label>
                <nz-form-control>
                  <nz-select formControlName="engagementId" nzShowSearch nzAllowClear nzPlaceHolder="Optional linked engagement">
                    @for (eng of engagements(); track eng.id) {
                      <nz-option [nzValue]="eng.id" [nzLabel]="eng.engagementCode + ' - ' + eng.title"></nz-option>
                    }
                  </nz-select>
                </nz-form-control>
              </nz-form-item>
            </div>

            <nz-form-item>
              <nz-form-label nzRequired>Billing Cycle</nz-form-label>
              <nz-form-control>
                <nz-select formControlName="cycle">
                  <nz-option nzValue="Monthly" nzLabel="Monthly"></nz-option>
                  <nz-option nzValue="Quarterly" nzLabel="Quarterly"></nz-option>
                  <nz-option nzValue="Annual" nzLabel="Annual"></nz-option>
                </nz-select>
              </nz-form-control>
            </nz-form-item>

            <div class="grid grid-cols-3 gap-3">
              <nz-form-item>
                <nz-form-label nzRequired>Recurring Fee</nz-form-label>
                <nz-form-control>
                  <nz-input-number formControlName="recurringFee" [nzMin]="0" [nzStep]="500" class="w-full"></nz-input-number>
                </nz-form-control>
              </nz-form-item>

              <nz-form-item>
                <nz-form-label nzRequired>Hours Included</nz-form-label>
                <nz-form-control>
                  <nz-input-number formControlName="includedHours" [nzMin]="0" [nzStep]="5" class="w-full"></nz-input-number>
                </nz-form-control>
              </nz-form-item>

              <nz-form-item>
                <nz-form-label nzRequired>Overage / Hr</nz-form-label>
                <nz-form-control>
                  <nz-input-number formControlName="overageHourlyRate" [nzMin]="0" [nzStep]="25" class="w-full"></nz-input-number>
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
                <nz-form-label>Expiration / Renewal Date</nz-form-label>
                <nz-form-control>
                  <nz-date-picker formControlName="endDate" class="w-full"></nz-date-picker>
                </nz-form-control>
              </nz-form-item>
            </div>

            <nz-form-item>
              <nz-form-label>Service Level Agreement / Terms</nz-form-label>
              <nz-form-control>
                <textarea nz-input formControlName="terms" rows="3" placeholder="Guaranteed turnaround, unused hours carry-over policy..."></textarea>
              </nz-form-control>
            </nz-form-item>

            <div class="drawer-footer">
              <button nz-button type="button" (click)="drawerVisible.set(false)">Cancel</button>
              <button nz-button nzType="primary" [nzLoading]="isSubmitting()" type="submit" [disabled]="form.invalid">
                Create Retainer
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
export class RetainersPage implements OnInit {
  private readonly api = inject(ServicesRetainersApi);
  private readonly legalEntitiesApi = inject(LegalEntitiesApi);
  private readonly partiesApi = inject(PartiesApi);
  private readonly engagementsApi = inject(ServicesEngagementsApi);
  private readonly fb = inject(FormBuilder);
  private readonly msg = inject(NzMessageService);

  readonly list = createPagedList<RetainerSummaryDto>((q) => this.api.list(q), {
    pageSize: 15,
    sort: 'createdAt:desc'
  });

  readonly columns: TableColumn<RetainerSummaryDto>[] = [
    { key: 'number', label: 'Retainer #', sortField: 'retainerNumber', width: '130px' },
    { key: 'title', label: 'Agreement & Client', sortField: 'title' },
    { key: 'cycle', label: 'Cycle', sortField: 'cycle', width: '110px' },
    { key: 'fee', label: 'Recurring Fee', sortField: 'recurringFee', align: 'right', width: '150px' },
    { key: 'hours', label: 'Included Quota', width: '140px' },
    { key: 'status', label: 'Status', sortField: 'status', width: '120px' },
    { key: 'actions', label: 'Actions', width: '80px' }
  ];

  readonly legalEntities = signal<{ id: string; name: string }[]>([]);
  readonly clients = signal<PartyDto[]>([]);
  readonly engagements = signal<EngagementSummaryDto[]>([]);
  readonly drawerVisible = signal(false);
  readonly isSubmitting = signal(false);

  form: FormGroup = this.fb.group({
    legalEntityId: ['', [Validators.required]],
    engagementId: [null],
    clientId: [null, [Validators.required]],
    clientName: ['', [Validators.required]],
    title: ['', [Validators.required, Validators.maxLength(200)]],
    cycle: ['Monthly' as RetainerCycle, [Validators.required]],
    recurringFee: [5000, [Validators.required, Validators.min(0)]],
    includedHours: [20, [Validators.required, Validators.min(0)]],
    overageHourlyRate: [250, [Validators.required, Validators.min(0)]],
    startDate: [new Date(), [Validators.required]],
    endDate: [null],
    terms: ['']
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

    this.engagementsApi.list({ page: 1, pageSize: 100 }).subscribe({
      next: res => this.engagements.set(res.items || []),
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
      engagementId: null,
      clientId: null,
      clientName: '',
      title: '',
      cycle: 'Monthly',
      recurringFee: 5000,
      includedHours: 20,
      overageHourlyRate: 250,
      startDate: new Date(),
      endDate: null,
      terms: ''
    });
    this.drawerVisible.set(true);
  }

  getCycleColor(cycle: RetainerCycle): string {
    switch (cycle) {
      case 'Monthly': return 'blue';
      case 'Quarterly': return 'purple';
      case 'Annual': return 'gold';
      default: return 'default';
    }
  }

  getStatusBadge(status: RetainerStatus): 'success' | 'warning' | 'error' | 'default' {
    switch (status) {
      case 'Active': return 'success';
      case 'Paused': return 'warning';
      case 'Terminated': return 'error';
      default: return 'default';
    }
  }

  toggleStatus(row: RetainerSummaryDto, newStatus: RetainerStatus): void {
    this.api.updateStatus(row.id, newStatus).subscribe({
      next: () => {
        this.msg.success(`Retainer status set to ${newStatus}`);
        this.list.reload();
      },
      error: () => this.msg.error('Failed to change retainer status')
    });
  }

  submitRetainer(): void {
    if (this.form.invalid) return;

    this.isSubmitting.set(true);
    const val = this.form.value;
    const input: RetainerInput = {
      legalEntityId: val.legalEntityId,
      engagementId: val.engagementId,
      clientId: val.clientId,
      clientName: val.clientName,
      title: val.title,
      cycle: val.cycle,
      recurringFee: val.recurringFee,
      includedHours: val.includedHours,
      overageHourlyRate: val.overageHourlyRate,
      startDate: val.startDate instanceof Date ? val.startDate.toISOString().substring(0, 10) : val.startDate,
      endDate: val.endDate instanceof Date ? val.endDate.toISOString().substring(0, 10) : null,
      terms: val.terms || null
    };

    this.api.create(input).subscribe({
      next: () => {
        this.msg.success('Retainer contract created successfully');
        this.isSubmitting.set(false);
        this.drawerVisible.set(false);
        this.list.reload();
      },
      error: () => {
        this.msg.error('Failed to create retainer');
        this.isSubmitting.set(false);
      }
    });
  }
}
