import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { AccountsApi } from '@core/api/admin.api';
import { ProblemDetails } from '@core/api/api.models';
import { createPagedList } from '@core/api/paged-list';
import { DEAL_TYPES, DealDto, DealsApi, RealEstateLookups, humanize } from '@core/api/realestate.api';
import { doneText } from '@core/format/done-text';
import { asProblem } from '@core/http/problem-details';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { itemsSignal } from '@shared/utils/items-signal';
import { MasterDataState } from '@shared/utils/master-data-state';
import { accountOptions, bankOrCashAccounts } from '@shared/utils/payment-accounts';
import { StatusTagComponent } from '../../components/status-tag/status-tag.component';

/** The stages a deal moves through before it is won; "lost" can be reached from any of them. */
const PIPELINE = ['lead', 'viewing', 'negotiation', 'agreed'];

/** Brokerage deals (sales and leases): the pipeline, commission and the agent's share; invoicing and paying the agent. */
@Component({
  selector: 'erp-deals',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzToolTipModule, TranslatePipe, FormsModule, PagedTableComponent, RecordDrawerComponent, HasPermissionDirective, StatusTagComponent, NzButtonModule, NzIconModule, NzSelectModule],
  templateUrl: './deals.page.html',
})
export class DealsPage {
  private readonly dealsApi = inject(DealsApi);
  private readonly message = inject(NzMessageService);
  private readonly lookups = inject(RealEstateLookups);
  private readonly accountPage = itemsSignal(inject(AccountsApi).list({ page: 1, pageSize: 200, sort: 'code' }));
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly humanize = humanize;
  readonly stages = [...PIPELINE, 'lost'];
  readonly list = createPagedList((q) => this.dealsApi.list(q), { sort: '-dealDate' });
  readonly state = new MasterDataState<DealDto>({
    save: (id, body) => (id ? this.dealsApi.update(id, body) : this.dealsApi.create(body)), reload: () => this.list.reload(), message: this.message, entityName: 'Deal',
  });
  readonly columns: TableColumn<DealDto>[] = [
    { key: 'number', label: 'Number', sortField: 'number', width: '150px' },
    { key: 'clientName', label: 'Client' },
    { key: 'dealType', label: 'Type', sortField: 'dealType', value: (d) => humanize(d.dealType) },
    { key: 'dealDate', label: 'Date', sortField: 'dealDate' },
    { key: 'stage', label: 'Stage', sortField: 'stage', filterField: 'stage', filterOptions: [...PIPELINE, 'lost'] },
    { key: 'dealValue', label: 'Deal value', sortField: 'dealValue', align: 'right', value: (d) => d.dealValue.toLocaleString() },
    { key: 'grossCommission', label: 'Commission', sortField: 'grossCommission', align: 'right', value: (d) => d.grossCommission.toLocaleString() },
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
  readonly payOpen = signal<DealDto | null>(null);
  readonly payFields = computed<FieldSpec[]>(() => [
    { key: 'bankAccountId', label: 'Pay from', type: 'select', required: true, options: accountOptions(bankOrCashAccounts(this.accountPage().items)) },
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
    this.dealsApi.invoiceCommission(d.id).subscribe({
      next: () => { this.moveSaving.set(false); this.state.close(); this.message.success(`Commission invoice issued for deal ${d.number ?? d.clientName}`); this.list.reload(); },
      error: (e) => { this.moveSaving.set(false); const p = asProblem(e); this.message.error(p?.detail ?? p?.title ?? 'Could not invoice the commission'); },
    });
  }

  payAgent(values: Record<string, unknown>): void {
    const d = this.payOpen();
    if (!d) return;
    this.moveSaving.set(true);
    this.dealsApi.payAgent(d.id, values['bankAccountId'] as string, values['date'] as string).subscribe({
      next: () => { this.moveSaving.set(false); this.payOpen.set(null); this.state.close(); this.message.success(`Agent paid for deal ${d.number ?? d.clientName}`); this.list.reload(); },
      error: (e) => { this.moveSaving.set(false); const p = asProblem(e); this.moveErrors.set(p); this.message.error(p?.detail ?? p?.title ?? 'Could not pay the agent'); },
    });
  }

  private send(d: DealDto, body: { stage: string; lostReason?: string | null }): void {
    this.moveSaving.set(true);
    this.dealsApi.moveStage(d.id, body).subscribe({
      next: () => { this.moveSaving.set(false); this.losing.set(null); this.state.close(); this.message.success(doneText('updated', 'Deal', d.number ?? d.clientName)); this.list.reload(); },
      error: (e) => { this.moveSaving.set(false); const p = asProblem(e); this.moveErrors.set(p); this.message.error(p?.detail ?? p?.title ?? 'Could not update the deal'); },
    });
  }
}
