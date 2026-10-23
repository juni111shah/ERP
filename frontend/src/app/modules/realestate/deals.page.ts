import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';
import { AccountDto, AccountsApi } from '../../core/api/admin.api';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { ProblemDetails } from '../../core/api/api.models';
import { createPagedList } from '../../core/api/paged-list';
import { asProblem } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { DEAL_TYPES, DealDto, DealsApi, RealEstateLookups, humanize } from './realestate.api';
import { StatusTagComponent } from './status-tag.component';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { RevealDirective } from '../../shared/reveal.directive';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';

const PIPELINE = ['lead', 'viewing', 'negotiation', 'agreed'];

@Component({
  selector: 'erp-deals',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzToolTipModule, RevealDirective, TranslatePipe, FormsModule, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, StatusTagComponent, NzButtonModule, NzIconModule, NzSelectModule],
  template: `
    <h1 class="erp-page-title">Deals</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search client" emptyText="No deals yet" (rowClick)="state.edit($event)">
      <nz-select toolbar style="width:170px" nzAllowClear nzPlaceHolder="All stages" [ngModel]="null" (ngModelChange)="filterStage($event)" data-testid="deal-stage-filter">
        @for (s of stages; track s) { <nz-option [nzValue]="s" [nzLabel]="humanize(s)"></nz-option> }
      </nz-select>
      <button toolbar *erpHasPermission="'realestate.deals.create'" nz-button erpReveal data-reveal="on" [attr.aria-label]="'ui.add.newDeal' | translate: 'New deal'" nz-tooltip [nzTooltipTitle]="'ui.add.newDeal' | translate: 'New deal'" nzType="primary" (click)="state.create()" data-testid="deal-new"><span nz-icon nzType="plus"></span>New deal</button>
      <ng-template #cell let-row let-column="column" let-value="value">
        @if (column.key === 'stage') { <erp-status-tag [status]="row.stage" /> } @else { {{ value }} }
      </ng-template>
    </erp-paged-table>

    <erp-record-drawer [title]="state.editing() ? (state.editing()!.number ?? 'Deal') : 'New deal'" [open]="state.open()" [fields]="fields" [value]="$any(state.editing())"
                       customFieldEntityType="realestate.deal" [saving]="state.saving()" [errors]="state.errors()" testId="deal-form"
                       (submitted)="state.submit($event)" (closed)="state.close()">
      @if (state.editing(); as d) {
        <div drawerActions style="margin-bottom:16px" data-testid="deal-actions">
          <div style="display:flex;gap:8px;align-items:center;flex-wrap:wrap">
            <erp-status-tag [status]="d.stage" />
            @if (next(d); as n) { <button *erpHasPermission="'realestate.deals.edit'" nz-button nzSize="small" nzType="primary" (click)="move(d, n)" data-testid="deal-next">{{ 'ui.re.moveTo' | translate: 'Move to' }} {{ humanize(n) }}</button> }
            @if (d.stage === 'agreed') { <button *erpHasPermission="'realestate.deals.invoice'" nz-button nzSize="small" nzType="primary" (click)="invoice(d)" data-testid="deal-invoice">Raise commission invoice</button> }
            @if (d.stage === 'collected') { <button *erpHasPermission="'realestate.deals.payagent'" nz-button nzSize="small" nzType="primary" (click)="payOpen.set(d)" data-testid="deal-pay-agent">Pay agent</button> }
            @if (d.stage !== 'lost' && isPipeline(d.stage)) { <button *erpHasPermission="'realestate.deals.lose'" nz-button nzSize="small" nzDanger (click)="losing.set(d)" data-testid="deal-lose">Mark as lost</button> }
          </div>
          <p style="margin:8px 0 0">Gross commission <strong>{{ d.grossCommission.toLocaleString() }} {{ d.currencyCode }}</strong> · {{ 'ui.re.agentLc' | translate: 'agent' }} {{ d.agentCommission.toLocaleString() }} · {{ 'ui.re.teamLeadLc' | translate: 'team lead' }} {{ d.teamLeadCommission.toLocaleString() }}</p>
          @if (d.lostReason) { <p>{{ 'ui.re.lost' | translate: 'Lost' }}: {{ d.lostReason }}</p> }
        </div>
      }
    </erp-record-drawer>
    <erp-record-drawer title="Pay agent" [open]="payOpen() !== null" [fields]="payFields()" [saving]="moveSaving()" [errors]="moveErrors()" [width]="420" testId="deal-pay-form"
                       submitLabel="Pay" (submitted)="payAgent($event)" (closed)="payOpen.set(null)" />
    <erp-record-drawer title="Mark deal as lost" [open]="losing() !== null" [fields]="lostFields" [saving]="moveSaving()" [errors]="moveErrors()" [width]="420" testId="deal-lost-form"
                       submitLabel="Mark as lost" (submitted)="lose($event)" (closed)="losing.set(null)" />`,
})
export class DealsPage {
  private readonly api = inject(DealsApi);
  private readonly message = inject(NzMessageService);
  private readonly lookups = inject(RealEstateLookups);
  readonly humanize = humanize;
  readonly stages = [...PIPELINE, 'lost'];
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-dealDate' });
  readonly state = new MasterDataState<DealDto>({
    save: (id, body) => (id ? this.api.update(id, body) : this.api.create(body)), reload: () => this.list.reload(), message: this.message, entityName: 'Deal',
  });
  readonly columns: TableColumn<DealDto>[] = [
    { key: 'number', label: 'Number', sortField: 'number', width: '150px' },
    { key: 'clientName', label: 'Client' },
    { key: 'dealType', label: 'Type', sortField: 'dealType', value: (d) => humanize(d.dealType) },
    { key: 'dealDate', label: 'Date', sortField: 'dealDate' },
    { key: 'stage', label: 'Stage', sortField: 'stage', filterField: 'stage', filterOptions: [...PIPELINE, 'lost'] },
    { key: 'dealValue', label: 'Deal value', sortField: 'dealValue', value: (d) => d.dealValue.toLocaleString() },
    { key: 'grossCommission', label: 'Commission', sortField: 'grossCommission', value: (d) => d.grossCommission.toLocaleString() },
  ];
  readonly fields: FieldSpec[] = [
    { key: 'legalEntityId', label: 'Brokerage legal entity', type: 'select', required: true, options: this.lookups.legalEntities(), readonlyOnEdit: true },
    { key: 'dealType', label: 'Deal type', type: 'select', required: true, options: DEAL_TYPES, default: 'sale' },
    { key: 'clientName', label: 'Client', type: 'text', required: true, maxLength: 200 },
    { key: 'clientPartyId', label: 'Client record', type: 'lookup', lookup: this.lookups.party('customer') },
    { key: 'propertyId', label: 'Property', type: 'lookup', lookup: this.lookups.property },
    { key: 'unitId', label: 'Unit', type: 'lookup', lookup: this.lookups.unit },
    { key: 'unitReference', label: 'Listing reference', type: 'text', maxLength: 100, hint: 'For units not managed in this system.' },
    { key: 'dealDate', label: 'Deal date', type: 'date', required: true },
    { key: 'dealValue', label: 'Deal value', type: 'number', required: true, min: 0 },
    { key: 'commissionRatePercent', label: 'Commission rate (%)', type: 'number', required: true, min: 0, max: 100, default: 2 },
    { key: 'agentPartyId', label: 'Agent', type: 'lookup', lookup: this.lookups.party('employee') },
    { key: 'externalAgentName', label: 'External agent', type: 'text', maxLength: 200 },
    { key: 'agentSharePercent', label: 'Agent share (%)', type: 'number', min: 0, max: 100, default: 0 },
    { key: 'teamLeadPartyId', label: 'Team lead', type: 'lookup', lookup: this.lookups.party('employee') },
    { key: 'teamLeadSharePercent', label: 'Team lead share (%)', type: 'number', min: 0, max: 100, default: 0 },
    { key: 'notes', label: 'Notes', type: 'textarea', maxLength: 2000 },
  ];
  readonly lostFields: FieldSpec[] = [{ key: 'lostReason', label: 'Reason', type: 'text', required: true, maxLength: 500 }];
  private readonly accountPage = toSignal(inject(AccountsApi).list({ page: 1, pageSize: 200, sort: 'code' }).pipe(catchError(() => of({ items: [] as AccountDto[] }))), { initialValue: { items: [] as AccountDto[] } });
  readonly payOpen = signal<DealDto | null>(null);
  readonly payFields = computed<FieldSpec[]>(() => [
    { key: 'bankAccountId', label: 'Pay from', type: 'select', required: true, options: this.accountPage().items.filter((a) => a.type === 'asset' && a.isPostable && a.isActive && (a.isBank || a.systemRole === 'cash' || a.systemRole === 'bank')).map((a) => ({ value: a.id, label: `${a.code} · ${a.name}` })) },
    { key: 'date', label: 'Payment date', type: 'date', required: true, default: new Date() },
  ]);
  readonly losing = signal<DealDto | null>(null);
  readonly moveSaving = signal(false);
  readonly moveErrors = signal<ProblemDetails | null>(null);

  filterStage(stage: string | null): void { this.list.setFilters(stage ? [{ field: 'stage', op: 'eq', value: stage }] : []); }
  isPipeline(stage: string): boolean { return PIPELINE.includes(stage); }
  /** The next pipeline stage; invoicing, collection and the agent payout are driven by finance. */
  next(d: DealDto): string | null { const i = PIPELINE.indexOf(d.stage); return i >= 0 && i < PIPELINE.length - 1 ? PIPELINE[i + 1] : null; }

  move(d: DealDto, stage: string): void { this.send(d, { stage }); }
  lose(values: Record<string, unknown>): void { const d = this.losing(); if (d) this.send(d, { stage: 'lost', lostReason: values['lostReason'] as string }); }

  invoice(d: DealDto): void {
    this.moveSaving.set(true);
    this.api.invoiceCommission(d.id).subscribe({
      next: () => { this.moveSaving.set(false); this.state.close(); this.message.success('Commission invoice issued'); this.list.reload(); },
      error: (e) => { this.moveSaving.set(false); const p = asProblem(e); this.message.error(p?.detail ?? p?.title ?? 'Could not invoice the commission'); },
    });
  }

  payAgent(values: Record<string, unknown>): void {
    const d = this.payOpen();
    if (!d) return;
    this.moveSaving.set(true);
    this.api.payAgent(d.id, values['bankAccountId'] as string, values['date'] as string).subscribe({
      next: () => { this.moveSaving.set(false); this.payOpen.set(null); this.state.close(); this.message.success('Agent paid'); this.list.reload(); },
      error: (e) => { this.moveSaving.set(false); const p = asProblem(e); this.moveErrors.set(p); this.message.error(p?.detail ?? p?.title ?? 'Could not pay the agent'); },
    });
  }

  private send(d: DealDto, body: { stage: string; lostReason?: string | null }): void {
    this.moveSaving.set(true);
    this.api.moveStage(d.id, body).subscribe({
      next: () => { this.moveSaving.set(false); this.losing.set(null); this.state.close(); this.message.success('Deal updated'); this.list.reload(); },
      error: (e) => { this.moveSaving.set(false); const p = asProblem(e); this.moveErrors.set(p); this.message.error(p?.detail ?? p?.title ?? 'Could not update the deal'); },
    });
  }
}
