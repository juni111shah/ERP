import { CreateProjectButtonComponent } from '@shared/components/create-project-button/create-project-button.component';
import { problemMessage } from '@core/http/problem-details';
import { CdkDrag, CdkDragDrop, CdkDropList, DragDropModule } from '@angular/cdk/drag-drop';
import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSkeletonModule } from 'ng-zorro-antd/skeleton';
import { LegalEntitiesApi } from '@core/api/admin.api';
import {
  CrmOpportunitiesApi,
  OpportunityStage,
  OpportunitySummaryDto,
  PipelineStageSummaryDto
} from '@core/api/crm.api';
import { displayLabel } from '@core/format/display';
import { createPagedList } from '@core/api/paged-list';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { SessionStore } from '@core/session/session.store';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';

const STAGES: OpportunityStage[] = ['qualification', 'proposal', 'negotiation', 'closedWon', 'closedLost'];
/** The same moves the server allows (CrmTransitions.Opportunity). Won and lost go through the close form, which asks for the final value and reason. */
const STAGE_MOVES: Record<OpportunityStage, OpportunityStage[]> = {
  qualification: ['proposal', 'closedLost'],
  proposal: ['negotiation', 'qualification', 'closedLost'],
  negotiation: ['closedWon', 'proposal', 'closedLost'],
  closedWon: [],
  closedLost: ['qualification'],
};
/** The win probability each open stage starts with. */
const STAGE_PROBABILITY: Partial<Record<OpportunityStage, number>> = { qualification: 20, proposal: 50, negotiation: 80 };
const BOARD_LIMIT = 200;
const VIEW_KEY = 'erp.crm.pipeline.view';

/** The sales pipeline: a board to see where every deal stands, and a table for the detail. */
@Component({
  selector: 'erp-opportunities-pipeline-page',
  standalone: true,
  imports: [
    CommonModule,
    CreateProjectButtonComponent,
    DragDropModule,
    ErpDatePipe,
    FormsModule,
    ReactiveFormsModule,
    NzButtonModule,
    NzIconModule,
    NzModalModule,
    NzDrawerModule,
    NzFormModule,
    NzInputModule,
    NzInputNumberModule,
    NzDatePickerModule,
    NzSelectModule,
    NzSkeletonModule,
    NzDropDownModule,
    NzRadioModule,
    PagedTableComponent,
    StatCardComponent,
    StatePanelComponent,
    StatusTagComponent,
    HasPermissionDirective,
  ],
  templateUrl: './opportunities-pipeline.page.html',
  styleUrl: './opportunities-pipeline.page.scss'
})
export class OpportunitiesPipelinePageComponent implements OnInit {
  private readonly api = inject(CrmOpportunitiesApi);
  private readonly legalEntitiesApi = inject(LegalEntitiesApi);
  private readonly session = inject(SessionStore);
  private readonly currencyService = inject(WorkspaceCurrencyService);
  private readonly message = inject(NzMessageService);
  private readonly fb = inject(FormBuilder);

  readonly stages = STAGES;
  readonly currency = this.currencyService.currency;
  readonly list = createPagedList<OpportunitySummaryDto>((q: any) => this.api.list(q));
  readonly entities = signal<{ id: string; name: string }[]>([]);
  readonly stageSummaries = signal<PipelineStageSummaryDto[]>([]);

  readonly view = signal<'board' | 'table'>(localStorage.getItem(VIEW_KEY) === 'table' ? 'table' : 'board');
  readonly deals = signal<OpportunitySummaryDto[]>([]);
  readonly boardLoading = signal(false);
  readonly boardFailed = signal(false);
  readonly boardTruncated = signal(false);

  readonly openCount = computed(() => this.stageSummaries().filter((s) => !this.isClosed(s.stage)).reduce((sum, s) => sum + s.count, 0));
  readonly openValue = computed(() => this.stageSummaries().filter((s) => !this.isClosed(s.stage)).reduce((sum, s) => sum + s.totalExpectedRevenue, 0));
  readonly closedCount = computed(() => (this.stageSummary('closedWon')?.count ?? 0) + (this.stageSummary('closedLost')?.count ?? 0));
  readonly winRate = computed(() => (this.closedCount() ? Math.round(((this.stageSummary('closedWon')?.count ?? 0) / this.closedCount()) * 1000) / 10 : 0));

  readonly createVisible = signal(false);
  readonly closeModalVisible = signal(false);
  readonly isSubmitting = signal(false);
  readonly isClosing = signal(false);
  readonly selectedOpportunity = signal<OpportunitySummaryDto | null>(null);
  readonly canWin = computed(() => this.selectedOpportunity()?.stage === 'negotiation');

  oppForm!: FormGroup;
  closeForm!: FormGroup;

  // The CRM list endpoints accept no sort or filter fields yet, so the columns offer none (the header filters work on the loaded page).
  readonly columns: TableColumn<OpportunitySummaryDto>[] = [
    { key: 'opportunityNumber', label: 'Deal #', sortField: 'opportunityNumber', width: '140px' },
    { key: 'title', label: 'Deal / Customer', sortField: 'title', filterValue: (r) => `${r.title} ${r.customerName}` },
    { key: 'customerName', label: 'Customer', hidden: true },
    { key: 'stage', label: 'Stage', sortField: 'stage', filterField: 'stage', filterOptions: ['qualification', 'proposal', 'negotiation', 'closedWon', 'closedLost'], width: '120px' },
    { key: 'probabilityPercent', label: 'Prob.', sortField: 'probabilityPercent', width: '80px', align: 'right', filter: false },
    { key: 'expectedRevenue', label: 'Expected Value', sortField: 'expectedRevenue', width: '140px', align: 'right', filter: false },
    { key: 'expectedCloseDate', label: 'Target Close', sortField: 'expectedCloseDate', width: '120px' },
    { key: 'assignedUserName', label: 'Owner', width: '130px', hidden: true },
    { key: 'actions', label: 'Actions', align: 'right', width: '210px' }
  ];

  /** A card can be dropped only where the stage flow allows, so the column shows that before the drop. */
  readonly canEnter = (drag: CdkDrag<OpportunitySummaryDto>, drop: CdkDropList<OpportunityStage>): boolean =>
    drag.data.stage === drop.data || this.movesOf(drag.data).includes(drop.data);

  ngOnInit(): void {
    this.initForms();
    this.loadEntities();
    this.reloadAll();
  }

  canManage(): boolean { return this.session.hasPermission('crm.opportunities.manage'); }
  deals_(n: number, kind = ''): string { return `${n} ${kind ? kind + ' ' : ''}${n === 1 ? 'deal' : 'deals'}`; }
  stageName(stage: string): string { return displayLabel(stage); }
  movesOf(d: OpportunitySummaryDto): OpportunityStage[] { return STAGE_MOVES[d.stage] ?? []; }
  /** Moves that do not ask for an outcome. Winning and losing use Close Deal. */
  openMoves(d: OpportunitySummaryDto): OpportunityStage[] { return this.movesOf(d).filter((s) => !this.isClosed(s)); }
  stageSummary(stage: OpportunityStage): PipelineStageSummaryDto | undefined { return this.stageSummaries().find((s) => s.stage === stage); }
  stageValue(stage: OpportunityStage): number { return this.stageSummary(stage)?.totalExpectedRevenue ?? this.inStage(stage).reduce((sum, d) => sum + d.expectedRevenue, 0); }
  inStage(stage: OpportunityStage): OpportunitySummaryDto[] { return this.deals().filter((d) => d.stage === stage); }
  private isClosed(stage: OpportunityStage): boolean { return stage === 'closedWon' || stage === 'closedLost'; }

  setView(view: 'board' | 'table'): void {
    this.view.set(view);
    try { localStorage.setItem(VIEW_KEY, view); } catch { /* storage unavailable */ }
  }

  private initForms(): void {
    this.oppForm = this.fb.group({
      legalEntityId: ['', [Validators.required]],
      title: ['', [Validators.required]],
      customerName: ['', [Validators.required]],
      stage: ['qualification', [Validators.required]],
      probabilityPercent: [20, [Validators.required, Validators.min(0), Validators.max(100)]],
      expectedRevenue: [0, [Validators.required, Validators.min(0)]],
      expectedCloseDate: [null],
      notes: ['']
    });

    this.closeForm = this.fb.group({
      isWon: [false, [Validators.required]],
      actualRevenue: [0, [Validators.required, Validators.min(0)]],
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

  private reloadAll(): void {
    this.loadStageSummaries();
    this.loadBoard();
    this.list.reload();
  }

  loadStageSummaries(): void {
    this.api.getPipelineSummary().subscribe({
      next: res => this.stageSummaries.set(res),
      error: () => {}
    });
  }

  loadBoard(): void {
    this.boardLoading.set(true);
    this.boardFailed.set(false);
    this.api.list({ page: 1, pageSize: BOARD_LIMIT }).subscribe({
      next: (res) => {
        this.deals.set(res.items);
        this.boardTruncated.set(res.total > res.items.length);
        this.boardLoading.set(false);
      },
      error: () => {
        this.boardLoading.set(false);
        this.boardFailed.set(true);
      },
    });
  }

  drop(e: CdkDragDrop<OpportunityStage>): void {
    if (e.previousContainer === e.container) return;
    this.moveTo(e.item.data as OpportunitySummaryDto, e.container.data);
  }

  moveTo(d: OpportunitySummaryDto, stage: OpportunityStage | null): void {
    if (!stage || stage === d.stage) return;
    if (this.isClosed(stage)) {
      this.openCloseModal(d, stage === 'closedWon');
      return;
    }
    this.advanceStage(d, stage);
  }

  onStageChange(stage: OpportunityStage): void {
    this.oppForm.patchValue({ probabilityPercent: STAGE_PROBABILITY[stage] ?? 20 });
  }

  openCreateDrawer(): void {
    this.oppForm.reset({
      legalEntityId: this.entities()[0]?.id || '',
      stage: 'qualification',
      probabilityPercent: 20,
      expectedRevenue: 0,
      expectedCloseDate: null
    });
    this.createVisible.set(true);
  }

  advanceStage(row: OpportunitySummaryDto, nextStage: OpportunityStage): void {
    const before = this.deals();
    const probabilityPercent = STAGE_PROBABILITY[nextStage] ?? row.probabilityPercent;
    this.deals.set(before.map((d) => (d.id === row.id ? { ...d, stage: nextStage, probabilityPercent } : d)));   // show it at once, undo when the server refuses
    this.api.updateStage(row.id, nextStage, probabilityPercent).subscribe({
      next: () => {
        this.message.success(`${row.opportunityNumber} moved to ${this.stageName(nextStage).toLowerCase()}.`);
        this.list.reload();
        this.loadStageSummaries();
      },
      error: (err) => {
        this.deals.set(before);
        this.message.error(problemMessage(err, 'That move is not allowed.'));
      }
    });
  }

  openCloseModal(row: OpportunitySummaryDto, won?: boolean): void {
    this.selectedOpportunity.set(row);
    this.closeForm.reset({
      isWon: won ?? row.stage === 'negotiation',
      actualRevenue: row.expectedRevenue,
      closeReason: ''
    });
    this.closeModalVisible.set(true);
  }

  saveOpportunity(): void {
    if (this.oppForm.invalid) {
      Object.values(this.oppForm.controls).forEach(c => { c.markAsDirty(); c.updateValueAndValidity({ onlySelf: true }); });
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
        this.message.success(`Deal ${v.title} added to the pipeline.`);
        this.createVisible.set(false);
        this.isSubmitting.set(false);
        this.reloadAll();
      },
      error: (err) => {
        this.message.error(problemMessage(err, 'Failed to create opportunity'));
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

    this.api.closeDeal(opp.id, v.isWon, v.isWon ? v.actualRevenue : 0, v.closeReason).subscribe({
      next: () => {
        this.message.success(`${opp.opportunityNumber} closed as ${v.isWon ? 'won' : 'lost'}.`);
        this.closeModalVisible.set(false);
        this.isClosing.set(false);
        this.reloadAll();
      },
      error: (err) => {
        this.message.error(problemMessage(err, 'Failed to close deal'));
        this.isClosing.set(false);
      }
    });
  }
}
