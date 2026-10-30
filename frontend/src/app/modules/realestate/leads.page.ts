import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { Observable, map } from 'rxjs';
import { ProblemDetails } from '../../core/api/api.models';
import { createPagedList } from '../../core/api/paged-list';
import { LookupOption } from '../../core/custom-fields/lookup-registry';
import { asProblem } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { LEAD_STAGES, LeadDto, LeadPipeline, LeadsApi, ListingsApi, RealEstateLookups, humanize } from './realestate.api';
import { StatusTagComponent } from './status-tag.component';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { RevealDirective } from '../../shared/reveal.directive';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { StatCardComponent } from '../../shared/stat-card.component';
import { KeyboardClickDirective } from '../../shared/keyboard-click.directive';

const NEXT: Record<string, string> = { new: 'contacted', contacted: 'viewing', viewing: 'negotiating' };

/** Prospects with a stage filter and pipeline counts; a lead is moved along, lost with a reason, or converted into a brokerage deal. */
@Component({
  selector: 'erp-leads',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [KeyboardClickDirective, NzToolTipModule, RevealDirective, TranslatePipe, FormsModule, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, StatusTagComponent, NzButtonModule, NzIconModule, NzSelectModule, NzTagModule, StatCardComponent],
  styles: [`
    .pipeline-strip { display: flex; gap: 8px; flex-wrap: wrap; margin-bottom: 16px; align-items: center; }
    .stage-chip {
      cursor: pointer;
      border-radius: 16px;
      padding: 4px 12px;
      font-size: 12px;
      font-weight: 500;
      transition: all 0.2s;
      border: 1px solid var(--erp-color-border-subtle, #edf2f7);
      background: var(--erp-color-bg-surface, #ffffff);
    }
    .stage-chip:hover { border-color: var(--erp-color-primary, #10955d); }
    .stage-chip.active { background: #f0fdf4; border-color: var(--erp-color-primary, #10955d); color: var(--erp-color-primary, #10955d); }
    .overdue-pill { color: #e11d48; font-weight: 600; display: inline-flex; align-items: center; gap: 4px; }
  `],
  template: `
    <h1 class="erp-page-title">Leads</h1>

    @if (pipeline(); as p) {
      <div class="erp-stat-grid erp-stat-grid--auto" data-testid="lead-stats">
        <erp-stat-card label="Active pipeline"
                       [value]="activeLeadsCount()"
                       format="number"
                       [loading]="list.loading() && list.items().length === 0"
                       caption="leads in progress"
                       [tone]="0" />
        <erp-stat-card label="Win rate"
                       [value]="p.winRatePercent"
                       format="percent"
                       [loading]="list.loading() && list.items().length === 0"
                       caption="closed won ratio"
                       [tone]="1" />
        <erp-stat-card label="Follow-ups due"
                       [value]="p.followUpsDue"
                       format="number"
                       [loading]="list.loading() && list.items().length === 0"
                       [caption]="p.followUpsDue > 0 ? 'action required' : 'all caught up'"
                       [tone]="p.followUpsDue > 0 ? 2 : 'neutral'" />
        <erp-stat-card label="Won deals"
                       [value]="wonCount()"
                       format="number"
                       [loading]="list.loading() && list.items().length === 0"
                       caption="converted to deals"
                       [tone]="3" />
      </div>

      <div class="pipeline-strip" data-testid="lead-pipeline">
        @for (s of p.stages; track s.stage) {
          <div erpKeyboardClick class="stage-chip" [class.active]="selectedStage() === s.stage" (click)="filterStage(selectedStage() === s.stage ? null : s.stage)">
            <span>{{ humanize(s.stage) }}:</span>
            <b>{{ s.count }}</b>
          </div>
        }
      </div>
    }

    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search name, phone, email" emptyText="No leads yet" (rowClick)="state.edit($event)">
      <nz-select toolbar style="width:160px" nzAllowClear nzPlaceHolder="All stages" [ngModel]="selectedStage()" (ngModelChange)="filterStage($event)" data-testid="lead-stage-filter">
        @for (s of stages; track s) { <nz-option [nzValue]="s" [nzLabel]="humanize(s)"></nz-option> }
      </nz-select>
      <button toolbar *erpHasPermission="'realestate.crm.manage'" nz-button nzType="primary" (click)="state.create()" data-testid="lead-new"><span nz-icon nzType="plus"></span>New lead</button>

      <ng-template #cell let-row let-column="column" let-value="value">
        @switch (column.key) {
          @case ('name') {
            <span class="name-cell">{{ row.name }}</span>
          }
          @case ('phone') {
            @if (row.phone) {
              <a [href]="'tel:' + row.phone" class="email-link"><span nz-icon nzType="phone" style="font-size:12px"></span> {{ row.phone }}</a>
            } @else {
              <span class="muted-text">—</span>
            }
          }
          @case ('source') {
            @if (row.source) {
              <nz-tag>{{ row.source }}</nz-tag>
            } @else {
              <span class="muted-text">—</span>
            }
          }
          @case ('stage') {
            <erp-status-tag [status]="row.stage" />
          }
          @case ('followUpOn') {
            @if (row.followUpOn) {
              <span [class.overdue-pill]="isOverdue(row.followUpOn)">
                @if (isOverdue(row.followUpOn)) {
                  <span nz-icon nzType="alert" nzTheme="outline"></span>
                }
                {{ row.followUpOn }}
              </span>
            } @else {
              <span class="muted-text">—</span>
            }
          }
          @default {
            {{ value }}
          }
        }
      </ng-template>
    </erp-paged-table>
    <erp-record-drawer [title]="state.editing() ? state.editing()!.name : 'New lead'" [open]="state.open()" [fields]="fields" [value]="$any(state.editing())"
                       [saving]="state.saving()" [errors]="state.errors()" testId="lead-form" (submitted)="state.submit($event)" (closed)="state.close()">
      @if (state.editing(); as l) {
        <div drawerActions style="margin-bottom:16px" data-testid="lead-actions">
          <div style="display:flex;gap:8px;align-items:center;flex-wrap:wrap">
            <erp-status-tag [status]="l.stage" />
            <ng-container *erpHasPermission="'realestate.crm.manage'">
              @if (nextStage(l); as n) { <button nz-button nzSize="small" nzType="primary" (click)="move(l, n)" data-testid="lead-next">{{ 'ui.re.moveTo' | translate: 'Move to' }} {{ humanize(n) }}</button> }
              @if (l.stage !== 'won' && l.stage !== 'lost') {
                <button nz-button nzSize="small" nzType="primary" (click)="convert(l)" data-testid="lead-convert">Convert to deal</button>
                <button nz-button nzSize="small" nzDanger (click)="losing.set(l)" data-testid="lead-lose">Mark as lost</button>
              }
              @if (l.stage === 'lost') { <button nz-button nzSize="small" (click)="move(l, 'new')" data-testid="lead-reopen">Reopen</button> }
            </ng-container>
          </div>
          @if (l.lostReason) { <p style="margin:8px 0 0">{{ 'ui.re.lost' | translate: 'Lost' }}: {{ l.lostReason }}</p> }
          @if (l.dealId) { <p style="margin:8px 0 0">Converted to a brokerage deal (see Deals).</p> }
        </div>
      }
    </erp-record-drawer>
    <erp-record-drawer title="Mark lead as lost" [open]="losing() !== null" [fields]="lostFields" [saving]="busy()" [errors]="errors()" [width]="420" testId="lead-lost-form"
                       submitLabel="Mark as lost" (submitted)="lose($event)" (closed)="losing.set(null)" />`,
})
export class LeadsPage {
  private readonly api = inject(LeadsApi);
  private readonly listingsApi = inject(ListingsApi);
  private readonly message = inject(NzMessageService);
  private readonly lookups = inject(RealEstateLookups);
  readonly humanize = humanize;
  readonly stages = LEAD_STAGES;
  readonly pipeline = signal<LeadPipeline | null>(null);
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-createdAt' });
  readonly state = new MasterDataState<LeadDto>({
    save: (id, body) => (id ? this.api.update(id, body) : this.api.create(body)), reload: () => this.refresh(), message: this.message, entityName: 'Lead',
  });
  readonly columns: TableColumn<LeadDto>[] = [
    { key: 'name', label: 'Name' },
    { key: 'phone', label: 'Phone', width: '150px' },
    { key: 'source', label: 'Source', width: '130px' },
    { key: 'interest', label: 'Interest' },
    { key: 'stage', label: 'Stage', width: '120px', filterField: 'stage', filterOptions: LEAD_STAGES },
    { key: 'followUpOn', label: 'Follow up', sortField: 'followUpOn', width: '120px' },
  ];
  private readonly listingLookup = (term: string): Observable<LookupOption[]> =>
    this.listingsApi.list({ page: 1, pageSize: 20, search: term }).pipe(map((r) => r.items.map((l) => ({ value: l.id, label: l.title }))));
  readonly fields: FieldSpec[] = [
    { key: 'legalEntityId', label: 'Legal entity', type: 'select', required: true, options: this.lookups.legalEntities(), readonlyOnEdit: true },
    { key: 'name', label: 'Name', type: 'text', required: true, maxLength: 150 },
    { key: 'phone', label: 'Phone', type: 'text', maxLength: 40 },
    { key: 'email', label: 'Email', type: 'text', maxLength: 150, hint: 'A phone number or an email is required.' },
    { key: 'source', label: 'Source', type: 'text', maxLength: 60, hint: 'Website, walk-in, referral, portal...' },
    { key: 'interest', label: 'Interested in', type: 'text', maxLength: 300 },
    { key: 'listingId', label: 'Listing', type: 'lookup', lookup: this.listingLookup },
    { key: 'unitId', label: 'Unit', type: 'lookup', lookup: this.lookups.unit },
    { key: 'agentPartyId', label: 'Agent', type: 'lookup', lookup: this.lookups.party('employee') },
    { key: 'followUpOn', label: 'Follow up on', type: 'date' },
    { key: 'notes', label: 'Notes', type: 'textarea', maxLength: 4000 },
  ];
  readonly lostFields: FieldSpec[] = [{ key: 'lostReason', label: 'Why was it lost?', type: 'text', required: true, maxLength: 300 }];
  readonly losing = signal<LeadDto | null>(null);
  readonly busy = signal(false);
  readonly errors = signal<ProblemDetails | null>(null);

  readonly selectedStage = signal<string | null>(null);

  readonly activeLeadsCount = computed(() => {
    const p = this.pipeline();
    if (!p) return 0;
    return p.stages.filter((s) => s.stage !== 'lost' && s.stage !== 'won').reduce((sum, s) => sum + s.count, 0);
  });

  readonly wonCount = computed(() => {
    const p = this.pipeline();
    if (!p) return 0;
    return p.stages.find((s) => s.stage === 'won')?.count ?? 0;
  });

  isOverdue(d?: string): boolean {
    if (!d) return false;
    const today = new Date().toISOString().slice(0, 10);
    return d < today;
  }

  constructor() { this.refresh(); }

  private refresh(): void { this.list.reload(); this.api.pipeline().subscribe((p) => this.pipeline.set(p)); }

  nextStage(l: LeadDto): string | null { return NEXT[l.stage] ?? null; }
  filterStage(stage: string | null): void {
    this.selectedStage.set(stage);
    this.list.setFilters(stage ? [{ field: 'stage', op: 'eq', value: stage }] : []);
  }

  move(l: LeadDto, stage: string, reason?: string): void {
    this.busy.set(true);
    this.api.moveStage(l.id, stage, reason).subscribe({
      next: () => { this.busy.set(false); this.losing.set(null); this.state.close(); this.message.success('Lead updated'); this.refresh(); },
      error: (e) => { this.busy.set(false); const p = asProblem(e); this.errors.set(p); this.message.error(p?.detail ?? 'Could not move the lead'); },
    });
  }

  lose(values: Record<string, unknown>): void { const l = this.losing(); if (l) this.move(l, 'lost', values['lostReason'] as string); }

  convert(l: LeadDto): void {
    this.busy.set(true);
    this.api.convert(l.id, {}).subscribe({
      next: () => { this.busy.set(false); this.state.close(); this.message.success('Lead converted to a deal'); this.refresh(); },
      error: (e) => { this.busy.set(false); this.message.error(asProblem(e)?.detail ?? 'Could not convert the lead'); },
    });
  }
}
