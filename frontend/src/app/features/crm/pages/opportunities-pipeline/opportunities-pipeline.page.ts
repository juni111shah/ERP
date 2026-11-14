import { CdkDrag, CdkDragDrop, CdkDropList, DragDropModule } from '@angular/cdk/drag-drop';
import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
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
import { CrmOpportunitiesApi, OpportunityStage, OpportunitySummaryDto, PipelineStageSummaryDto } from '@core/api/crm.api';
import { createPagedList } from '@core/api/paged-list';
import { toUtcIsoDate } from '@core/format/dates';
import { displayLabel } from '@core/format/display';
import { problemMessage } from '@core/http/problem-details';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { SessionStore } from '@core/session/session.store';
import { CreateProjectButtonComponent } from '@shared/components/create-project-button/create-project-button.component';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { revealErrors } from '@shared/forms/form-validation';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { loadInto } from '@shared/utils/load-into';
import { CrmLookups } from '../../crm-lookups';
import { STAGES, STAGE_MOVES, STAGE_PROBABILITY, dealsText, isClosedStage, pipelineTotals } from '../../crm-pipeline';

const BOARD_LIMIT = 200;
const VIEW_KEY = 'erp.crm.pipeline.view';

/** The new-deal form's raw value. */
interface OpportunityFormValue {
  legalEntityId: string;
  title: string;
  customerName: string;
  stage: OpportunityStage;
  probabilityPercent: number;
  expectedRevenue: number;
  expectedCloseDate: Date | null;
  notes: string;
}

/** The close-deal form's raw value. */
interface CloseDealFormValue {
  isWon: boolean;
  actualRevenue: number;
  closeReason: string;
}

/** The sales pipeline: a board to see where every deal stands, and a table for the detail. */
@Component({
  selector: 'erp-opportunities-pipeline-page',
  standalone: true,
  imports: [
    CommonModule,
    DragDropModule,
    FormsModule,
    ReactiveFormsModule,
    NzButtonModule,
    NzDatePickerModule,
    NzDrawerModule,
    NzDropDownModule,
    NzFormModule,
    NzIconModule,
    NzInputModule,
    NzInputNumberModule,
    NzModalModule,
    NzRadioModule,
    NzSelectModule,
    NzSkeletonModule,
    CreateProjectButtonComponent,
    ErpDatePipe,
    HasPermissionDirective,
    PagedTableComponent,
    StatCardComponent,
    StatePanelComponent,
    StatusTagComponent,
  ],
  providers: [CrmLookups],
  templateUrl: './opportunities-pipeline.page.html',
  styleUrl: './opportunities-pipeline.page.scss'
})
export class OpportunitiesPipelinePageComponent implements OnInit {
  private readonly api = inject(CrmOpportunitiesApi);
  private readonly lookups = inject(CrmLookups);
  private readonly session = inject(SessionStore);
  private readonly currencyService = inject(WorkspaceCurrencyService);
  private readonly message = inject(NzMessageService);
  private readonly fb = inject(FormBuilder);

  readonly stages = STAGES;
  readonly currency = this.currencyService.currency;
  readonly list = createPagedList<OpportunitySummaryDto>((q) => this.api.list(q));
  readonly entities = this.lookups.entities;
  readonly stageSummaries = signal<PipelineStageSummaryDto[]>([]);

  readonly view = signal<'board' | 'table'>(localStorage.getItem(VIEW_KEY) === 'table' ? 'table' : 'board');
  readonly deals = signal<OpportunitySummaryDto[]>([]);
  readonly boardLoading = signal(false);
  readonly boardFailed = signal(false);
  readonly boardTruncated = signal(false);

  private readonly totals = computed(() => pipelineTotals(this.stageSummaries()));
  readonly openCount = computed(() => this.totals().openCount);
  readonly openValue = computed(() => this.totals().openValue);
  readonly closedCount = computed(() => this.totals().closedCount);
  readonly winRate = computed(() => this.totals().winRate);

  readonly createVisible = signal(false);
  readonly closeModalVisible = signal(false);
  readonly isSubmitting = signal(false);
  readonly isClosing = signal(false);
  readonly selectedOpportunity = signal<OpportunitySummaryDto | null>(null);
  readonly canWin = computed(() => this.selectedOpportunity()?.stage === 'negotiation');

  readonly oppForm: FormGroup = this.fb.group({
    legalEntityId: ['', [Validators.required]],
    title: ['', [Validators.required]],
    customerName: ['', [Validators.required]],
    stage: ['qualification', [Validators.required]],
    probabilityPercent: [20, [Validators.required, Validators.min(0), Validators.max(100)]],
    expectedRevenue: [0, [Validators.required, Validators.min(0)]],
    expectedCloseDate: [null],
    notes: ['']
  });

  readonly closeForm: FormGroup = this.fb.group({
    isWon: [false, [Validators.required]],
    actualRevenue: [0, [Validators.required, Validators.min(0)]],
    closeReason: ['']
  });

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
    this.lookups.loadEntities(this.oppForm);
    this.reloadAll();
  }

  /** Follows live access changes: the board's drag and drop, the move buttons and the empty-state action. */
  readonly canManage = computed(() => this.session.hasPermission('crm.opportunities.manage'));
  deals_(n: number, kind = ''): string { return dealsText(n, kind); }
  stageName(stage: string): string { return displayLabel(stage); }
  movesOf(d: OpportunitySummaryDto): OpportunityStage[] { return STAGE_MOVES[d.stage] ?? []; }
  /** Moves that do not ask for an outcome. Winning and losing use Close Deal. */
  openMoves(d: OpportunitySummaryDto): OpportunityStage[] { return this.movesOf(d).filter((s) => !isClosedStage(s)); }
  stageSummary(stage: OpportunityStage): PipelineStageSummaryDto | undefined { return this.stageSummaries().find((s) => s.stage === stage); }
  stageValue(stage: OpportunityStage): number { return this.stageSummary(stage)?.totalExpectedRevenue ?? this.inStage(stage).reduce((sum, d) => sum + d.expectedRevenue, 0); }
  inStage(stage: OpportunityStage): OpportunitySummaryDto[] { return this.deals().filter((d) => d.stage === stage); }

  setView(view: 'board' | 'table'): void {
    this.view.set(view);
    try { localStorage.setItem(VIEW_KEY, view); } catch { /* storage unavailable */ }
  }

  loadStageSummaries(): void {
    loadInto(this.stageSummaries, this.api.getPipelineSummary());
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
    if (isClosedStage(stage)) {
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
    // Shown at once, undone when the server refuses.
    this.deals.set(before.map((d) => (d.id === row.id ? { ...d, stage: nextStage, probabilityPercent } : d)));
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
      revealErrors(this.oppForm, { onlySelf: true });
      return;
    }
    this.isSubmitting.set(true);
    const v = this.oppForm.value as OpportunityFormValue;

    this.api.create({
      legalEntityId: v.legalEntityId,
      title: v.title,
      customerName: v.customerName,
      stage: v.stage,
      probabilityPercent: v.probabilityPercent,
      expectedRevenue: v.expectedRevenue,
      expectedCloseDate: v.expectedCloseDate ? toUtcIsoDate(v.expectedCloseDate) : undefined,
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
    const v = this.closeForm.value as CloseDealFormValue;

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

  private reloadAll(): void {
    this.loadStageSummaries();
    this.loadBoard();
    this.list.reload();
  }
}
