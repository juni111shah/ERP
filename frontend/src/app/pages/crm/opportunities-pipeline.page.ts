import { CreateProjectButtonComponent } from '../../shared/create-project-button.component';
import { problemMessage } from '../../core/http/problem-details';
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
import { LegalEntitiesApi } from '../../core/api/admin.api';
import {
  CrmOpportunitiesApi,
  OpportunityStage,
  OpportunitySummaryDto,
  PipelineStageSummaryDto
} from '../../core/api/crm.api';
import { displayLabel } from '../../core/format/display';
import { createPagedList } from '../../core/api/paged-list';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { SessionStore } from '../../core/session/session.store';
import { ErpDatePipe } from '../../shared/display.pipes';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { StatCardComponent } from '../../shared/stat-card.component';
import { StatePanelComponent } from '../../shared/state-panel.component';
import { StatusTagComponent } from '../../shared/status-tag.component';

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
  template: `
      <div class="erp-stat-grid erp-stat-grid--auto mb-4" data-testid="opp-stats">
        <erp-stat-card label="Open Pipeline" [value]="openValue()" format="money" [caption]="deals_(openCount(), 'open') + ', not weighted'" [tone]="1" />
        <erp-stat-card label="Closed Won" [value]="stageSummary('closedWon')?.totalExpectedRevenue ?? 0" format="money" [caption]="deals_(stageSummary('closedWon')?.count ?? 0) + ', expected value'" tone="success" />
        <erp-stat-card label="Closed Lost" [value]="stageSummary('closedLost')?.totalExpectedRevenue ?? 0" format="money" [caption]="deals_(stageSummary('closedLost')?.count ?? 0) + ', expected value'" tone="danger" />
        <erp-stat-card label="Win Rate" [value]="winRate()" format="percent" [caption]="closedCount() ? 'of ' + closedCount() + ' closed deals' : 'no closed deals yet'" [tone]="4" />
      </div>

      <div class="view-bar">
        <nz-radio-group [ngModel]="view()" (ngModelChange)="setView($event)" nzButtonStyle="solid" aria-label="View" data-testid="opp-view">
          <label nz-radio-button nzValue="board" data-testid="opp-view-board"><span nz-icon nzType="appstore"></span> Board</label>
          <label nz-radio-button nzValue="table" data-testid="opp-view-table"><span nz-icon nzType="unordered-list"></span> Table</label>
        </nz-radio-group>
        @if (view() === 'board') {
          <span class="spacer"></span>
          <button *erpHasPermission="'crm.opportunities.manage'" nz-button nzType="primary" (click)="openCreateDrawer()" data-testid="opp-new-board">
            <span nz-icon nzType="plus"></span> New Opportunity
          </button>
        }
      </div>

      @if (view() === 'board') {
        @if (boardFailed()) {
          <erp-state-panel kind="error" title="The pipeline could not be loaded." hint="Check your connection and try again." actionLabel="Try again" (action)="loadBoard()" />
        } @else if (boardLoading() && !deals().length) {
          <nz-skeleton [nzActive]="true" [nzParagraph]="{ rows: 8 }" />
        } @else if (!deals().length) {
          <erp-state-panel title="No opportunities yet" hint="Add a deal, or convert a lead, to start your pipeline." icon="fund" [actionLabel]="canManage() ? 'New Opportunity' : null" (action)="openCreateDrawer()" />
        } @else {
          <div class="board" cdkDropListGroup data-testid="opp-board">
            @for (stage of stages; track stage) {
              <section class="col" [attr.data-testid]="'opp-col-' + stage">
                <header class="col-head">
                  <div class="col-title"><erp-status [value]="stage" /> <span class="col-count" [attr.data-testid]="'opp-count-' + stage">{{ stageSummary(stage)?.count ?? inStage(stage).length }}</span></div>
                  <div class="col-value">{{ currency() }} {{ stageValue(stage) | number:'1.0-0' }}</div>
                </header>
                <div class="list" cdkDropList [cdkDropListData]="stage" [cdkDropListEnterPredicate]="canEnter" [cdkDropListDisabled]="!canManage()" (cdkDropListDropped)="drop($event)">
                  @for (d of inStage(stage); track d.id) {
                    <article class="card" cdkDrag [cdkDragData]="d" [cdkDragDisabled]="!canManage() || !movesOf(d).length" [attr.data-testid]="'opp-card-' + d.opportunityNumber">
                      <div class="card-top"><span class="num">{{ d.opportunityNumber }}</span><span class="prob">{{ d.probabilityPercent }}%</span></div>
                      <div class="card-title">{{ d.title }}</div>
                      <div class="card-customer">{{ d.customerName }}</div>
                      <div class="card-foot">
                        <strong class="amount">{{ currency() }} {{ d.expectedRevenue | number:'1.0-0' }}</strong>
                        @if (d.expectedCloseDate) { <span class="card-date">{{ d.expectedCloseDate | erpDate }}</span> }
                      </div>
                      @if (canManage() && movesOf(d).length) {
                        <button nz-button nzSize="small" nzBlock class="mover" type="button" nz-dropdown nzTrigger="click" [nzDropdownMenu]="moveMenu" [attr.aria-label]="'Move ' + d.opportunityNumber + ' to'" [attr.data-testid]="'opp-move-' + d.opportunityNumber">
                          Move to <span nz-icon nzType="down"></span>
                        </button>
                        <nz-dropdown-menu #moveMenu="nzDropdownMenu">
                          <ul nz-menu>
                            @for (s of movesOf(d); track s) { <li nz-menu-item (click)="moveTo(d, s)">{{ stageName(s) }}</li> }
                          </ul>
                        </nz-dropdown-menu>
                      }
                    </article>
                  } @empty { <div class="empty">No deals</div> }
                </div>
              </section>
            }
          </div>
          @if (boardTruncated()) { <p class="board-note" data-testid="opp-board-note">The board shows the first {{ deals().length }} deals. The counts and values above each column cover all of them.</p> }
        }
      } @else {
      <erp-paged-table
        #table
        [list]="list"
        [columns]="columns"
        searchPlaceholder="Search deal, title or customer"
        [clickable]="false"
        emptyText="No opportunities yet. Add one to start your pipeline."
        exportFilename="opportunities">
        <button toolbar *erpHasPermission="'crm.opportunities.manage'" nz-button nzType="primary" (click)="openCreateDrawer()" data-testid="opp-new">
          <span nz-icon nzType="plus"></span> New Opportunity
        </button>
          <ng-template #cell let-row let-column="column" let-value="value">
            @switch (column.key) {
              @case ('opportunityNumber') { <span class="num-cell">{{ row.opportunityNumber }}</span> }
              @case ('title') {
                <div class="deal-cell"><strong>{{ row.title }}</strong><span class="card-customer">{{ row.customerName }}</span></div>
              }
              @case ('stage') { <erp-status [value]="row.stage" /> }
              @case ('probabilityPercent') { <span class="amount">{{ row.probabilityPercent }}%</span> }
              @case ('expectedRevenue') { <strong class="amount">{{ currency() }} {{ row.expectedRevenue | number:'1.0-0' }}</strong> }
              @case ('assignedUserName') { {{ row.assignedUserName || 'Unassigned' }} }
              @case ('actions') {
                <div class="table-actions">
                  @if (row.stage === 'closedWon') {
                    <erp-create-project-button sourceType="crm.opportunity" [sourceId]="row.id" [sourceLabel]="'opportunity ' + row.opportunityNumber" [defaultName]="row.title" [legalEntityId]="row.legalEntityId" [amount]="row.expectedRevenue" />
                  }
                  @if (openMoves(row).length) {
                    <button
                      *erpHasPermission="'crm.opportunities.manage'"
                      nz-button nzType="link" nzSize="small" nz-dropdown [nzDropdownMenu]="stageMenu">
                      <span nz-icon nzType="step-forward"></span> Move
                    </button>
                    <nz-dropdown-menu #stageMenu="nzDropdownMenu">
                      <ul nz-menu>
                        @for (s of openMoves(row); track s) {
                          <li nz-menu-item (click)="advanceStage(row, s)">{{ stageName(s) }}</li>
                        }
                      </ul>
                    </nz-dropdown-menu>
                  }
                  @if (row.stage !== 'closedWon' && row.stage !== 'closedLost') {
                    <button
                      *erpHasPermission="'crm.opportunities.manage'"
                      nz-button nzType="link" nzSize="small"
                      (click)="openCloseModal(row)">
                      <span nz-icon nzType="check-circle"></span> Close Deal
                    </button>
                  }
                </div>
              }
              @default { {{ value }} }
            }
          </ng-template>
        </erp-paged-table>
      }

      <!-- Create Opportunity Drawer -->
      <nz-drawer
        [nzVisible]="createVisible()"
        [nzWidth]="620"
        nzTitle="New Opportunity"
        (nzOnClose)="createVisible.set(false)">
        <ng-container *nzDrawerContent>
          <form [formGroup]="oppForm" nz-form nzLayout="vertical" (ngSubmit)="saveOpportunity()">
            <nz-form-item>
              <nz-form-label nzRequired>Legal Entity</nz-form-label>
              <nz-form-control nzErrorTip="Please select an entity">
                <nz-select formControlName="legalEntityId" nzPlaceHolder="Select Entity">
                  <nz-option *ngFor="let ent of entities()" [nzValue]="ent.id" [nzLabel]="ent.name"></nz-option>
                </nz-select>
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-label nzRequired>Deal Title</nz-form-label>
              <nz-form-control nzErrorTip="Title is required">
                <input nz-input formControlName="title" placeholder="e.g. Annual supply contract" />
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-label nzRequired>Customer</nz-form-label>
              <nz-form-control nzErrorTip="Customer name is required">
                <input nz-input formControlName="customerName" placeholder="Customer or company name" />
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
                <nz-form-label nzRequired>Stage</nz-form-label>
                <nz-form-control>
                  <nz-select formControlName="stage" (ngModelChange)="onStageChange($event)">
                    <nz-option nzValue="qualification" nzLabel="Qualification (20%)"></nz-option>
                    <nz-option nzValue="proposal" nzLabel="Proposal (50%)"></nz-option>
                    <nz-option nzValue="negotiation" nzLabel="Negotiation (80%)"></nz-option>
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
              <nz-form-label>Notes</nz-form-label>
              <nz-form-control>
                <textarea nz-input formControlName="notes" rows="3" placeholder="Decision makers, competition, pricing..."></textarea>
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
        nzTitle="Close Deal"
        [nzOkLoading]="isClosing()"
        (nzOnCancel)="closeModalVisible.set(false)"
        (nzOnOk)="submitCloseDeal()">
        <ng-container *nzModalContent>
          <form [formGroup]="closeForm" nz-form nzLayout="vertical">
            <div *ngIf="selectedOpportunity() as opp" class="mb-4 close-summary-box">
              <p><strong>Deal:</strong> {{ opp.title }}</p>
              <p><strong>Customer:</strong> {{ opp.customerName }}</p>
              <p><strong>Expected value:</strong> {{ currency() }} {{ opp.expectedRevenue | number:'1.0-0' }}</p>
            </div>

            <nz-form-item>
              <nz-form-label nzRequired>Outcome</nz-form-label>
              <nz-form-control [nzExtra]="canWin() ? undefined : 'A deal can be won only from Negotiation. Move it there first.'">
                <nz-radio-group formControlName="isWon">
                  <label nz-radio [nzValue]="true" [nzDisabled]="!canWin()" data-testid="opp-close-won">Won</label>
                  <label nz-radio [nzValue]="false" data-testid="opp-close-lost">Lost</label>
                </nz-radio-group>
              </nz-form-control>
            </nz-form-item>

            <nz-form-item *ngIf="closeForm.value.isWon">
              <nz-form-label nzRequired>Final Value ({{ currency() }})</nz-form-label>
              <nz-form-control>
                <nz-input-number formControlName="actualRevenue" [nzMin]="0" class="w-full"></nz-input-number>
              </nz-form-control>
            </nz-form-item>

            <nz-form-item>
              <nz-form-label>Reason</nz-form-label>
              <nz-form-control>
                <textarea nz-input formControlName="closeReason" rows="2" placeholder="Why did we win or lose this deal?"></textarea>
              </nz-form-control>
            </nz-form-item>
          </form>
        </ng-container>
      </nz-modal>
  `,
  styles: [`
    .view-bar { display: flex; align-items: center; gap: 12px; margin-bottom: 12px; }
    .spacer { flex: 1; }
    .board { display: grid; grid-template-columns: repeat(5, minmax(210px, 1fr)); gap: 10px; overflow-x: auto; padding-bottom: 6px; align-items: start; }
    .col { background: var(--erp-color-bg-subtle); border-radius: 10px; padding: 8px; min-width: 0; display: flex; flex-direction: column; gap: 8px; }
    .col-head { padding: 2px 4px 0; }
    .col-title { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
    .col-count { font-weight: 600; font-variant-numeric: tabular-nums; color: var(--erp-color-text-secondary); }
    .col-value { margin-top: 4px; font-size: 15px; font-weight: 700; font-variant-numeric: tabular-nums; white-space: nowrap; color: var(--erp-color-text-primary); }
    .list { min-height: 90px; max-height: calc(100vh - 380px); overflow-y: auto; display: flex; flex-direction: column; gap: 8px; }
    .card { background: var(--erp-card-bg); border: 1px solid var(--erp-card-border); box-shadow: var(--erp-card-shadow); border-radius: 9px; padding: 9px; cursor: grab; }
    .card.cdk-drag-disabled { cursor: default; }
    .card.cdk-drag-preview { --erp-card-shadow: var(--erp-shadow-3); }
    .cdk-drag-placeholder { opacity: .35; }
    .cdk-drop-list-receiving { outline: 2px dashed var(--erp-color-primary); outline-offset: 2px; border-radius: 8px; }
    .card-top { display: flex; justify-content: space-between; gap: 8px; font-size: 11.5px; color: var(--erp-color-text-muted); }
    .num { white-space: nowrap; } .prob { font-variant-numeric: tabular-nums; }
    .card-title { margin: 3px 0 1px; font-weight: 600; font-size: 13px; line-height: 1.3; color: var(--erp-color-text-primary); }
    .card-customer { display: block; font-size: 12px; color: var(--erp-color-text-muted); }
    .card-foot { display: flex; justify-content: space-between; align-items: baseline; flex-wrap: wrap; gap: 4px 8px; margin-top: 6px; }
    .card-date { font-size: 11.5px; color: var(--erp-color-text-muted); white-space: nowrap; }
    .mover { width: 100%; margin-top: 7px; }
    .empty { color: var(--erp-color-text-muted); font-size: 12px; text-align: center; padding: 14px 0; }
    .board-note { margin: 8px 0 0; color: var(--erp-color-text-muted); }
    .amount { font-variant-numeric: tabular-nums; white-space: nowrap; }
    .num-cell { white-space: nowrap; font-weight: 600; color: var(--erp-color-primary-text); }
    .deal-cell { display: block; width: 300px; white-space: normal; }
    .deal-cell strong { display: block; }
    .table-actions { display: flex; justify-content: flex-end; align-items: center; gap: 4px; white-space: nowrap; }
    .close-summary-box { background: var(--erp-color-bg-subtle); padding: 0.75rem; border-radius: 6px; }
    .close-summary-box p { margin: 0 0 0.25rem 0; font-size: 0.9rem; }
    .drawer-footer { display: flex; justify-content: flex-end; gap: 0.75rem; margin-top: 2rem; }
    .grid { display: grid; }
    .grid-cols-2 { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    .gap-4 { gap: 1rem; }
    .w-full { width: 100%; }
    .mb-4 { margin-bottom: 1rem; }
  `]
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
        this.message.success('Deal added to the pipeline.');
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
        this.message.success(v.isWon ? 'Deal closed as won.' : 'Deal closed as lost.');
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
