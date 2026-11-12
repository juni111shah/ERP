import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { Observable, map } from 'rxjs';
import { ProblemDetails } from '@core/api/api.models';
import { createPagedList } from '@core/api/paged-list';
import { LEAD_STAGES, LeadDto, LeadPipeline, LeadsApi, ListingsApi, RealEstateLookups, humanize } from '@core/api/realestate.api';
import { LookupOption } from '@core/custom-fields/lookup-registry';
import { toUtcIsoDate } from '@core/format/dates';
import { doneText } from '@core/format/done-text';
import { asProblem } from '@core/http/problem-details';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { KeyboardClickDirective } from '@shared/directives/keyboard-click.directive';
import { ErpDatePipe, ErpLabelPipe } from '@shared/pipes/display.pipes';
import { MasterDataState } from '@shared/utils/master-data-state';
import { StatusTagComponent } from '../../components/status-tag/status-tag.component';

/** The stage a lead moves to next; negotiating leads are won by converting them, or lost. */
const NEXT: Record<string, string> = { new: 'contacted', contacted: 'viewing', viewing: 'negotiating' };

/** Prospects with a stage filter and pipeline counts; a lead is moved along, lost with a reason, or converted into a brokerage deal. */
@Component({
  selector: 'erp-leads',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpLabelPipe, ErpDatePipe, KeyboardClickDirective, NzToolTipModule, TranslatePipe, FormsModule, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, StatusTagComponent, NzButtonModule, NzIconModule, NzSelectModule, NzTagModule, StatCardComponent],
  styleUrl: './leads.page.scss',
  templateUrl: './leads.page.html',
})
export class LeadsPage {
  private readonly leadsApi = inject(LeadsApi);
  private readonly listingsApi = inject(ListingsApi);
  private readonly message = inject(NzMessageService);
  private readonly lookups = inject(RealEstateLookups);
  readonly humanize = humanize;
  readonly stages = LEAD_STAGES;
  readonly pipeline = signal<LeadPipeline | null>(null);
  readonly list = createPagedList((q) => this.leadsApi.list(q), { sort: '-createdAt' });
  readonly state = new MasterDataState<LeadDto>({
    save: (id, body) => (id ? this.leadsApi.update(id, body) : this.leadsApi.create(body)), reload: () => this.refresh(), message: this.message, entityName: 'Lead',
  });
  readonly columns: TableColumn<LeadDto>[] = [
    { key: 'name', label: 'Name' },
    { key: 'phone', label: 'Phone', width: '150px' },
    { key: 'source', label: 'Source', width: '130px' },
    { key: 'interest', label: 'Interest' },
    { key: 'stage', label: 'Stage', width: '120px', filterField: 'stage', filterOptions: LEAD_STAGES },
    { key: 'followUpOn', label: 'Follow up', sortField: 'followUpOn', width: '120px' },
  ];
  /** Server-searched listings for the lead's "Listing" field. */
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
  readonly converting = signal<LeadDto | null>(null);
  /** The deal value comes from the listing when the lead has one; otherwise the server needs it from here. */
  readonly convertFields = computed<FieldSpec[]>(() => {
    const hasListing = !!this.converting()?.listingId;
    return [
      { key: 'dealType', label: 'Deal type', type: 'select', options: [{ value: 'lease', label: 'Lease' }, { value: 'sale', label: 'Sale' }], default: 'lease' },
      { key: 'dealValue', label: 'Deal value', type: 'number', min: 0, required: !hasListing, hint: hasListing ? 'Leave blank to use the listing price.' : 'Annual rent or sale price.' },
      { key: 'commissionRatePercent', label: 'Commission rate (%)', type: 'number', min: 0, max: 100, hint: 'Leave blank for the default: 5% on a lease, 2% on a sale.' },
    ];
  });
  readonly losing = signal<LeadDto | null>(null);
  readonly busy = signal(false);
  readonly errors = signal<ProblemDetails | null>(null);

  readonly selectedStage = signal<string | null>(null);

  /** Leads still in play: every stage but won and lost. */
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

  constructor() { this.refresh(); }

  /** A follow-up date before today (UTC). */
  isOverdue(d?: string): boolean {
    if (!d) return false;
    return d < toUtcIsoDate(new Date());
  }

  nextStage(l: LeadDto): string | null { return NEXT[l.stage] ?? null; }
  filterStage(stage: string | null): void {
    this.selectedStage.set(stage);
    this.list.setFilters(stage ? [{ field: 'stage', op: 'eq', value: stage }] : []);
  }

  move(l: LeadDto, stage: string, reason?: string): void {
    this.busy.set(true);
    this.leadsApi.moveStage(l.id, stage, reason).subscribe({
      next: () => { this.busy.set(false); this.losing.set(null); this.state.close(); this.message.success(doneText('updated', 'Lead', l.name)); this.refresh(); },
      error: (e) => { this.busy.set(false); const p = asProblem(e); this.errors.set(p); this.message.error(p?.detail ?? 'Could not move the lead'); },
    });
  }

  lose(values: Record<string, unknown>): void { const l = this.losing(); if (l) this.move(l, 'lost', values['lostReason'] as string); }

  startConvert(l: LeadDto): void { this.errors.set(null); this.converting.set(l); }

  convert(v: Record<string, unknown>): void {
    const l = this.converting();
    if (!l) return;
    this.busy.set(true); this.errors.set(null);
    this.leadsApi.convert(l.id, { dealType: (v['dealType'] as string) ?? null, dealValue: (v['dealValue'] as number) ?? null, commissionRatePercent: (v['commissionRatePercent'] as number) ?? null }).subscribe({
      next: () => { this.busy.set(false); this.converting.set(null); this.state.close(); this.message.success(`Lead ${l.name} converted to a deal`); this.refresh(); },
      error: (e) => { this.busy.set(false); const p = asProblem(e); this.errors.set(p); if (!p?.errors) this.message.error(p?.detail ?? 'Could not convert the lead'); },
    });
  }

  private refresh(): void { this.list.reload(); this.leadsApi.pipeline().subscribe((p) => this.pipeline.set(p)); }
}
