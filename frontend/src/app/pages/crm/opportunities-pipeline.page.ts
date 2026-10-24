import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzBadgeModule } from 'ng-zorro-antd/badge';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { LegalEntitiesApi } from '../../core/api/admin.api';
import {
  CrmOpportunitiesApi,
  OpportunityStage,
  OpportunitySummaryDto,
  PipelineStageSummaryDto
} from '../../core/api/crm.api';
import { createPagedList } from '../../core/api/paged-list';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';

import { StatCardComponent, StatTone } from '../../shared/stat-card.component';

@Component({
  selector: 'erp-opportunities-pipeline-page',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    NzCardModule,
    NzGridModule,
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
    NzDropDownModule,
    NzRadioModule,
    PagedTableComponent,
    StatCardComponent,
  ],
  template: `
      <div class="erp-stat-grid erp-stat-grid--auto mb-4" data-testid="opp-stats">
        <erp-stat-card
          *ngFor="let s of stageSummaries()"
          [label]="s.stageName"
          [value]="s.totalExpectedRevenue"
          format="money"
          [caption]="s.count + ' in stage'"
          [tone]="getStageTone(s.stage)"
        />
      </div>

      <erp-paged-table
        #table
        [list]="list"
        [columns]="columns">
        <button toolbar nz-button nzType="primary" (click)="openCreateDrawer()" data-testid="opp-new">
          <span nz-icon nzType="plus"></span> New Opportunity
        </button>
          <ng-template #cell let-row let-column="column">
            <ng-container [ngSwitch]="column.key">
              <span *ngSwitchCase="'opportunityNumber'" class="mono-code font-bold">{{ row.opportunityNumber }}</span>
              <span *ngSwitchCase="'title'">
                <strong>{{ row.title }}</strong>
              </span>
              <span *ngSwitchCase="'customerName'">
                <span>{{ row.customerName }}</span>
              </span>
              <span *ngSwitchCase="'stage'">
                <nz-tag [nzColor]="getStageColor(row.stage)">{{ row.stage }}</nz-tag>
              </span>
              <span *ngSwitchCase="'probabilityPercent'">
                <span>{{ row.probabilityPercent }}%</span>
              </span>
              <span *ngSwitchCase="'expectedRevenue'">
                <strong class="text-primary">\${{ row.expectedRevenue | number:'1.0-0' }}</strong>
              </span>
              <span *ngSwitchCase="'expectedCloseDate'">
                <span>{{ row.expectedCloseDate || '-' }}</span>
              </span>
              <span *ngSwitchCase="'assignedUserName'">
                <span>{{ row.assignedUserName || 'Unassigned' }}</span>
              </span>
              <span *ngSwitchCase="'actions'">
                <div class="table-actions">
                  <button
                    *ngIf="row.stage !== 'ClosedWon' && row.stage !== 'ClosedLost'"
                    nz-button nzType="link" nzSize="small" nz-dropdown [nzDropdownMenu]="stageMenu">
                    <span nz-icon nzType="step-forward"></span> Advance
                  </button>
                  <nz-dropdown-menu #stageMenu="nzDropdownMenu">
                    <ul nz-menu>
                      <li nz-menu-item *ngIf="row.stage !== 'Qualification'" (click)="advanceStage(row, 'Qualification', 20)">Move to Qualification (20%)</li>
                      <li nz-menu-item *ngIf="row.stage !== 'Proposal'" (click)="advanceStage(row, 'Proposal', 50)">Move to Proposal (50%)</li>
                      <li nz-menu-item *ngIf="row.stage !== 'Negotiation'" (click)="advanceStage(row, 'Negotiation', 80)">Move to Negotiation (80%)</li>
                    </ul>
                  </nz-dropdown-menu>

                  <button
                    *ngIf="row.stage !== 'ClosedWon' && row.stage !== 'ClosedLost'"
                    nz-button nzType="link" nzSize="small" class="text-success"
                    (click)="openCloseModal(row)">
                    <span nz-icon nzType="check-circle"></span> Close Deal
                  </button>
                </div>
              </span>
            </ng-container>
          </ng-template>
        </erp-paged-table>

      <!-- Create Opportunity Drawer -->
      <nz-drawer
        [nzVisible]="createVisible()"
        [nzWidth]="620"
        nzTitle="Create Sales Deal Opportunity"
        (nzOnClose)="createVisible.set(false)">
        <ng-container *nzDrawerContent>
          <form [formGroup]="oppForm" nz-form nzLayout="vertical" (ngSubmit)="saveOpportunity()">
            <nz-form-item>
              <nz-form-label nzRequired>Workspace Legal Entity</nz-form-label>
              <nz-form-control nzErrorTip="Please select an entity">
                <nz-select formControlName="legalEntityId" nzPlaceHolder="Select Entity">
                  <nz-option *ngFor="let ent of entities()" [nzValue]="ent.id" [nzLabel]="ent.name"></nz-option>
                </nz-select>
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-label nzRequired>Opportunity / Deal Title</nz-form-label>
              <nz-form-control nzErrorTip="Title is required">
                <input nz-input formControlName="title" placeholder="e.g. 100-Seat License Upgrade & Deployment" />
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-label nzRequired>Customer / Account Name</nz-form-label>
              <nz-form-control nzErrorTip="Customer name is required">
                <input nz-input formControlName="customerName" placeholder="e.g. Emirates Logistics LLC" />
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
                <nz-form-label nzRequired>Pipeline Stage</nz-form-label>
                <nz-form-control>
                  <nz-select formControlName="stage" (ngModelChange)="onStageChange($event)">
                    <nz-option nzValue="Qualification" nzLabel="Qualification (20%)"></nz-option>
                    <nz-option nzValue="Proposal" nzLabel="Proposal (50%)"></nz-option>
                    <nz-option nzValue="Negotiation" nzLabel="Negotiation (80%)"></nz-option>
                  </nz-select>
                </nz-form-control>
              </nz-form-item>
            </div>

            <div class="grid grid-cols-2 gap-4">
              <nz-form-item>
                <nz-form-label>Win Probability (%)</nz-form-label>
                <nz-form-control>
                  <nz-input-number formControlName="probabilityPercent" [nzMin]="0" [nzMax]="100" class="w-full"></nz-input-number>
                </nz-form-control>
              </nz-form-item>

              <nz-form-item>
                <nz-form-label>Target Close Date</nz-form-label>
                <nz-form-control>
                  <nz-date-picker formControlName="expectedCloseDate" class="w-full"></nz-date-picker>
                </nz-form-control>
              </nz-form-item>
            </div>

            <nz-form-item>
              <nz-form-label>Deal Strategy & Stakeholder Notes</nz-form-label>
              <nz-form-control>
                <textarea nz-input formControlName="notes" rows="3" placeholder="Key decision-makers, competition, pricing leverage..."></textarea>
              </nz-form-control>
            </nz-form-item>

            <div class="drawer-footer">
              <button nz-button type="button" (click)="createVisible.set(false)">Cancel</button>
              <button nz-button nzType="primary" type="submit" [nzLoading]="isSubmitting()">Create Deal</button>
            </div>
          </form>
        </ng-container>
      </nz-drawer>

      <!-- Close Deal Modal -->
      <nz-modal
        [nzVisible]="closeModalVisible()"
        nzTitle="Close Opportunity"
        [nzOkLoading]="isClosing()"
        (nzOnCancel)="closeModalVisible.set(false)"
        (nzOnOk)="submitCloseDeal()">
        <ng-container *nzModalContent>
          <form [formGroup]="closeForm" nz-form nzLayout="vertical">
            <div *ngIf="selectedOpportunity() as opp" class="mb-4 close-summary-box">
              <p><strong>Deal:</strong> {{ opp.title }}</p>
              <p><strong>Customer:</strong> {{ opp.customerName }}</p>
              <p><strong>Pipeline Value:</strong> \${{ opp.expectedRevenue | number:'1.0-0' }}</p>
            </div>

            <nz-form-item>
              <nz-form-label nzRequired>Deal Outcome</nz-form-label>
              <nz-form-control>
                <nz-radio-group formControlName="isWon">
                  <label nz-radio [nzValue]="true">Closed Won (Won Deal!)</label>
                  <label nz-radio [nzValue]="false">Closed Lost</label>
                </nz-radio-group>
              </nz-form-control>
            </nz-form-item>

            <nz-form-item *ngIf="closeForm.value.isWon">
              <nz-form-label nzRequired>Final Won Contract Value (\$)</nz-form-label>
              <nz-form-control>
                <nz-input-number formControlName="actualRevenue" [nzMin]="0" class="w-full"></nz-input-number>
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-label>Close Reason / Win-Loss Learnings</nz-form-label>
              <nz-form-control>
                <textarea nz-input formControlName="closeReason" rows="2" placeholder="Why did we win or lose this deal?"></textarea>
              </nz-form-control>
            </nz-form-item>
          </form>
        </ng-container>
      </nz-modal>
  `,
  styles: [`
    .text-secondary { color: var(--erp-text-secondary, #64748b); }
    .text-primary { color: var(--erp-primary, #0ea5e9); }
    .text-success { color: #10b981; }
    .mono-code { font-family: monospace; color: #1890ff; }
    .font-bold { font-weight: 600; }
    .close-summary-box { background: var(--erp-bg-secondary, #f8fafc); padding: 0.75rem; border-radius: 6px; }
    .close-summary-box p { margin: 0 0 0.25rem 0; font-size: 0.9rem; }
    .drawer-footer { display: flex; justify-content: flex-end; gap: 0.75rem; margin-top: 2rem; }
    .grid { display: grid; }
    .grid-cols-2 { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    .gap-4 { gap: 1rem; }
    .w-full { width: 100%; }
    .mb-4 { margin-bottom: 1rem; }
    .mt-2 { margin-top: 0.5rem; }
  `]
})
export class OpportunitiesPipelinePageComponent implements OnInit {
  private readonly api = inject(CrmOpportunitiesApi);
  private readonly legalEntitiesApi = inject(LegalEntitiesApi);
  private readonly message = inject(NzMessageService);
  private readonly fb = inject(FormBuilder);

  readonly list = createPagedList<OpportunitySummaryDto>((q: any) => this.api.list(q));
  readonly entities = signal<{ id: string; name: string }[]>([]);
  readonly stageSummaries = signal<PipelineStageSummaryDto[]>([]);

  readonly createVisible = signal(false);
  readonly closeModalVisible = signal(false);
  readonly isSubmitting = signal(false);
  readonly isClosing = signal(false);
  readonly selectedOpportunity = signal<OpportunitySummaryDto | null>(null);

  oppForm!: FormGroup;
  closeForm!: FormGroup;

  readonly columns: TableColumn<OpportunitySummaryDto>[] = [
    { key: 'opportunityNumber', label: 'Deal #', sortField: 'opportunityNumber' },
    { key: 'title', label: 'Opportunity Title', sortField: 'title' },
    { key: 'customerName', label: 'Customer / Account', sortField: 'customerName' },
    { key: 'stage', label: 'Pipeline Stage', sortField: 'stage' },
    { key: 'probabilityPercent', label: 'Prob. %', sortField: 'probabilityPercent' },
    { key: 'expectedRevenue', label: 'Expected Value', sortField: 'expectedRevenue' },
    { key: 'expectedCloseDate', label: 'Target Close' },
    { key: 'assignedUserName', label: 'Deal Owner' },
    { key: 'actions', label: 'Actions', align: 'right' }
  ];

  ngOnInit(): void {
    this.initForms();
    this.loadEntities();
    this.loadStageSummaries();
  }

  private initForms(): void {
    this.oppForm = this.fb.group({
      legalEntityId: ['', [Validators.required]],
      title: ['', [Validators.required]],
      customerName: ['', [Validators.required]],
      stage: ['Qualification', [Validators.required]],
      probabilityPercent: [20, [Validators.required, Validators.min(0), Validators.max(100)]],
      expectedRevenue: [50000, [Validators.required, Validators.min(0)]],
      expectedCloseDate: [new Date(Date.now() + 45 * 86400000)],
      notes: ['']
    });

    this.closeForm = this.fb.group({
      isWon: [true, [Validators.required]],
      actualRevenue: [50000, [Validators.required, Validators.min(0)]],
      closeReason: ['']
    });
  }

  private loadEntities(): void {
    this.legalEntitiesApi.list({ page: 1, pageSize: 50 }).subscribe({
      next: res => {
        if (res.items) {
          this.entities.set(res.items.map(e => ({ id: e.id, name: e.name })));
          if (res.items.length > 0) {
            this.oppForm.patchValue({ legalEntityId: res.items[0].id });
          }
        }
      }
    });
  }

  loadStageSummaries(): void {
    this.api.getPipelineSummary().subscribe({
      next: res => this.stageSummaries.set(res),
      error: () => {}
    });
  }

  getStageColor(stage: OpportunityStage): string {
    switch (stage) {
      case 'Qualification': return 'blue';
      case 'Proposal': return 'cyan';
      case 'Negotiation': return 'orange';
      case 'ClosedWon': return 'success';
      case 'ClosedLost': return 'error';
      default: return 'default';
    }
  }

  getStageBadgeColor(stage: OpportunityStage): string {
    switch (stage) {
      case 'Qualification': return '#1890ff';
      case 'Proposal': return '#13c2c2';
      case 'Negotiation': return '#fa8c16';
      case 'ClosedWon': return '#52c41a';
      case 'ClosedLost': return '#ff4d4f';
      default: return '#8c8c8c';
    }
  }

  getStageTone(stage: OpportunityStage | string): StatTone {
    switch (stage) {
      case 'Qualification': return 1;
      case 'Proposal': return 2;
      case 'Negotiation': return 'warning';
      case 'ClosedWon': return 'success';
      case 'ClosedLost': return 'danger';
      default: return 'neutral';
    }
  }

  onStageChange(stage: OpportunityStage): void {
    let prob = 20;
    if (stage === 'Proposal') prob = 50;
    else if (stage === 'Negotiation') prob = 80;
    this.oppForm.patchValue({ probabilityPercent: prob });
  }

  openCreateDrawer(): void {
    this.oppForm.reset({
      legalEntityId: this.entities()[0]?.id || '',
      stage: 'Qualification',
      probabilityPercent: 20,
      expectedRevenue: 50000,
      expectedCloseDate: new Date(Date.now() + 45 * 86400000)
    });
    this.createVisible.set(true);
  }

  advanceStage(row: OpportunitySummaryDto, nextStage: OpportunityStage, prob: number): void {
    this.api.updateStage(row.id, nextStage, prob).subscribe({
      next: () => {
        this.message.success(`Deal moved to ${nextStage}`);
        this.list.reload();
        this.loadStageSummaries();
      },
      error: () => this.message.error('Failed to update stage')
    });
  }

  openCloseModal(row: OpportunitySummaryDto): void {
    this.selectedOpportunity.set(row);
    this.closeForm.patchValue({
      isWon: true,
      actualRevenue: row.expectedRevenue,
      closeReason: 'Value proposal accepted and contract signed.'
    });
    this.closeModalVisible.set(true);
  }

  saveOpportunity(): void {
    if (this.oppForm.invalid) {
      Object.values(this.oppForm.controls).forEach(c => c.markAsDirty());
      return;
    }
    this.isSubmitting.set(true);
    const v = this.oppForm.value;
    const closeDate = v.expectedCloseDate ? new Date(v.expectedCloseDate).toISOString().slice(0, 10) : undefined;

    this.api.create({
      legalEntityId: v.legalEntityId,
      title: v.title,
      customerName: v.customerName,
      stage: v.stage,
      probabilityPercent: v.probabilityPercent,
      expectedRevenue: v.expectedRevenue,
      expectedCloseDate: closeDate,
      notes: v.notes
    }).subscribe({
      next: () => {
        this.message.success('Deal opportunity added to pipeline');
        this.createVisible.set(false);
        this.isSubmitting.set(false);
        this.list.reload();
        this.loadStageSummaries();
      },
      error: () => {
        this.message.error('Failed to create opportunity');
        this.isSubmitting.set(false);
      }
    });
  }

  submitCloseDeal(): void {
    if (this.closeForm.invalid) return;
    const opp = this.selectedOpportunity();
    if (!opp) return;

    this.isClosing.set(true);
    const v = this.closeForm.value;

    this.api.closeDeal(opp.id, v.isWon, v.actualRevenue, v.closeReason).subscribe({
      next: () => {
        this.message.success(v.isWon ? 'Congratulations! Deal closed won!' : 'Deal closed lost recorded.');
        this.closeModalVisible.set(false);
        this.isClosing.set(false);
        this.list.reload();
        this.loadStageSummaries();
      },
      error: () => {
        this.message.error('Failed to close deal');
        this.isClosing.set(false);
      }
    });
  }
}
