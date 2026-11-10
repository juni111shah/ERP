import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { catchError, map, of } from 'rxjs';
import { BillsApi, BusinessUnitsApi } from '@core/api/admin.api';
import { ASSET_STATUSES, AssetCategoriesApi, AssetCategoryDto, AssetDto, AssetScheduleDto, AssetsApi, CAPITALISATION_SOURCES, DEPRECIATION_METHODS } from '@core/api/assets.api';
import { doneText } from '@core/format/done-text';
import { displayDate } from '@core/format/display';
import { problemMessage } from '@core/http/problem-details';
import { PartiesApi } from '@features/parties/pages/parties/parties.page';
import { SessionStore } from '@core/session/session.store';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { ActionFormState } from '@shared/utils/action-form-state';
import { ActionButton, DescriptionItem, DetailPageComponent, KpiCard } from '@shared/components/detail-page/detail-page.component';
import { defaultLegalEntityId, legalEntityOptions } from '@shared/utils/entity-options';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { MasterDataState } from '@shared/utils/master-data-state';
import { AccountsApi } from '@core/api/admin.api';
import { assetFields, businessUnitLookup } from '../../asset-form';

@Component({
  selector: 'erp-asset-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, RouterLink, NzCardModule, NzModalModule, NzSpinModule, NzTableModule, DetailPageComponent, RecordDrawerComponent, StatePanelComponent, StatusTagComponent],
  styles: [`
    .section { margin-top: 16px; }
    .r { text-align: end; white-space: nowrap; }
    .muted { color: var(--erp-color-text-muted); }
  `],
  template: `
    @if (loading()) {
      <div style="text-align:center;padding:48px"><nz-spin nzSimple /></div>
    } @else if (error()) {
      <erp-state-panel kind="error" title="Could not load this asset" [hint]="error()" actionLabel="Try again" (action)="load()" />
    } @else if (asset(); as a) {
      <erp-detail-page [backLink]="['/assets/register']" backLabel="Back to Asset register" [title]="a.number + ' · ' + a.name" [status]="a.status" [actions]="actions()"
                       [kpiCards]="kpiCards()" [descriptions]="descriptions()" entityName="Asset" recordType="FixedAsset" [recordId]="a.id" [showDocuments]="false" [createdAt]="a.acquisitionDate">
        <div content>
          <nz-card class="section" nzTitle="Depreciation schedule" nzSize="small" data-testid="asset-schedule">
            @if (schedule(); as s) {
              @if (s.rows.length) {
                <nz-table [nzData]="s.rows" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small">
                  <thead><tr><th>Period end</th><th class="r">Depreciation</th><th class="r">Accumulated</th><th class="r">Net book value</th><th>Status</th><th>Journal</th></tr></thead>
                  <tbody>
                    @for (row of s.rows; track row.periodEnd) {
                      <tr>
                        <td>{{ fmtDate(row.periodEnd) }}</td>
                        <td class="r">{{ (row.posted ? (row.postedAmount ?? row.amount) : row.amount) | number: '1.2-2' }}</td>
                        <td class="r">{{ row.accumulated | number: '1.2-2' }}</td>
                        <td class="r">{{ row.netBookValue | number: '1.2-2' }}</td>
                        <td><erp-status [value]="row.posted ? 'posted' : 'scheduled'" [label]="row.posted ? 'Posted' : 'Planned'" /></td>
                        <td>@if (row.journalEntryId) { <a [routerLink]="['/finance/journals', row.journalEntryId]">Open journal</a> } @else { <span class="muted">—</span> }</td>
                      </tr>
                    }
                  </tbody>
                </nz-table>
              } @else { <span class="muted">There is nothing to depreciate yet. The schedule appears once the asset is capitalised.</span> }
            } @else { <span class="muted">The schedule is not available.</span> }
          </nz-card>
        </div>
      </erp-detail-page>
      <erp-record-drawer title="Edit asset" [open]="edit.open()" [fields]="editFields()" [value]="$any(a)" [saving]="edit.saving()" [errors]="edit.errors()" testId="asset-form" (submitted)="edit.submit($event)" (closed)="edit.close()" />
      <erp-record-drawer title="Capitalise asset" submitLabel="Capitalise" [open]="capitalise.open()" [fields]="capitaliseFields" [saving]="capitalise.saving()" [errors]="capitalise.errors()"
                         testId="capitalise-form" (submitted)="capitalise.submit($event)" (closed)="capitalise.close()" />
      <erp-record-drawer title="Dispose of asset" submitLabel="Dispose" [open]="dispose.open()" [fields]="disposeFields" [saving]="dispose.saving()" [errors]="dispose.errors()"
                         testId="dispose-form" (submitted)="dispose.submit($event)" (closed)="dispose.close()" />
    }`,
})
export class AssetDetailPage implements OnInit {
  private readonly api = inject(AssetsApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly session = inject(SessionStore);
  private readonly accounts = inject(AccountsApi);
  private readonly bills = inject(BillsApi);
  private readonly parties = inject(PartiesApi);
  private readonly units = inject(BusinessUnitsApi);
  private readonly categoriesApi = inject(AssetCategoriesApi);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly fmtDate = displayDate;

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly asset = signal<AssetDto | null>(null);
  readonly schedule = signal<AssetScheduleDto | null>(null);
  readonly categories = signal<AssetCategoryDto[]>([]);

  readonly edit = new MasterDataState<AssetDto>({
    save: (id, body) => this.api.save(id, body), reload: () => this.load(false), message: this.message, entityName: 'Asset',
  });
  readonly capitalise = new ActionFormState<AssetDto>({
    run: (payload) => this.api.capitalise(this.asset()!.id, payload),
    success: () => doneText('completed', 'Capitalisation', this.asset()?.number),
    done: () => this.load(false), message: this.message, failure: 'Could not capitalise the asset',
  });
  readonly dispose = new ActionFormState<AssetDto>({
    run: (payload) => this.api.dispose(this.asset()!.id, payload),
    success: () => doneText('completed', 'Disposal', this.asset()?.number),
    done: () => this.load(false), message: this.message, failure: 'Could not dispose of the asset',
  });

  readonly editFields = computed<FieldSpec[]>(() => assetFields(legalEntityOptions(this.session), defaultLegalEntityId(this.session), this.categories(), businessUnitLookup(this.units)));

  /** The bill picked in the capitalise form: its lines are what the line picker offers. */
  private pickedBill: string | null = null;
  readonly capitaliseFields: FieldSpec[] = [
    { key: 'source', label: 'Paid or recorded through', type: 'select', required: true, options: CAPITALISATION_SOURCES, default: 'clearing' },
    { key: 'date', label: 'Date', type: 'date', hint: 'Left empty, the acquisition date is used.' },
    { key: 'accountId', label: 'Account', type: 'lookup', lookup: this.accounts.lookup, hint: 'The clearing account, or the bank or cash account the asset was paid from.', hidden: (v) => v['source'] !== 'clearing' && v['source'] !== 'bank' },
    { key: 'partyId', label: 'Vendor', type: 'lookup', lookup: (term) => this.parties.list({ page: 1, pageSize: 20, search: term, extra: { role: 'Vendor' } }).pipe(map((r) => r.items.map((p) => ({ value: p.id, label: p.name })))), hidden: (v) => v['source'] !== 'vendor', hint: 'A posted bill is raised for this vendor, so it is paid like any other bill.' },
    {
      key: 'billId', label: 'Bill', type: 'lookup', hidden: (v) => v['source'] !== 'bill',
      lookup: (term) => this.bills.list({ page: 1, pageSize: 20, search: term }).pipe(map((r) => r.items.map((b) => ({ value: b.id, label: `${b.number} · ${b.partyName ?? ''}` })))),
      onChange: (value) => { this.pickedBill = (value as string | null) ?? null; return null; },
    },
    {
      key: 'billLineId', label: 'Bill line', type: 'lookup', hidden: (v) => v['source'] !== 'bill', hint: 'Only needed when the bill has several lines.',
      lookup: () => (this.pickedBill ? this.bills.detail(this.pickedBill).pipe(map((b) => b.lines.filter((l) => !!l.id).map((l) => ({ value: l.id!, label: `${l.description} · ${l.quantity * l.price}` }))), catchError(() => of([]))) : of([])),
    },
    { key: 'reference', label: 'Reference', type: 'text', maxLength: 100 },
  ];
  readonly disposeFields: FieldSpec[] = [
    { key: 'date', label: 'Disposal date', type: 'date', required: true },
    { key: 'proceeds', label: 'Proceeds', type: 'number', min: 0, default: 0, hint: 'What was received. Zero for scrapping.' },
    { key: 'proceedsAccountId', label: 'Proceeds received into', type: 'lookup', lookup: this.accounts.lookup, hint: 'Required when proceeds are above zero.', hidden: (v) => !(Number(v['proceeds']) > 0) },
    { key: 'gainLossAccountId', label: 'Gain or loss account', type: 'lookup', lookup: this.accounts.lookup, hint: 'Left empty, the account of the category is used.' },
    { key: 'reason', label: 'Reason', type: 'textarea', maxLength: 400 },
  ];

  readonly actions = computed((): ActionButton[] => {
    const a = this.asset();
    if (!a) return [];
    return [
      { label: 'Capitalise', icon: 'check', type: 'primary', permission: 'assets.register.capitalise', visible: a.status === 'draft', handler: () => this.capitalise.show(a) },
      { label: 'Dispose', icon: 'export', permission: 'assets.register.dispose', visible: a.status === 'capitalised' || a.status === 'fullyDepreciated', handler: () => this.dispose.show(a) },
      { label: 'Edit', icon: 'edit', permission: 'assets.register.manage', visible: a.status === 'draft', handler: () => this.edit.edit(a) },
      { label: 'Delete', icon: 'delete', danger: true, permission: 'assets.register.manage', visible: a.status === 'draft', handler: () => this.confirmDelete(a) },
      { label: 'Capitalisation journal', icon: 'book', visible: !!a.capitalisationJournalId, handler: () => this.router.navigate(['/finance/journals', a.capitalisationJournalId]) },
      { label: 'Source bill', icon: 'file-text', visible: !!a.sourceBillId, handler: () => this.router.navigate(['/finance/bills', a.sourceBillId]) },
      { label: 'Disposal journal', icon: 'book', visible: !!a.disposalJournalId, handler: () => this.router.navigate(['/finance/journals', a.disposalJournalId]) },
    ];
  });

  readonly kpiCards = computed((): KpiCard[] => {
    const a = this.asset();
    if (!a) return [];
    const money = (v: number) => new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(v);
    return [
      { title: 'Cost', value: money(a.cost), suffix: this.currency() },
      { title: 'Accumulated depreciation', value: money(a.accumulatedDepreciation), suffix: this.currency() },
      { title: 'Net book value', value: money(a.netBookValue), suffix: this.currency() },
      { title: 'Salvage value', value: money(a.salvageValue), suffix: this.currency() },
    ];
  });

  readonly descriptions = computed((): DescriptionItem[] => {
    const a = this.asset();
    if (!a) return [];
    const items: DescriptionItem[] = [
      { label: 'Category', value: this.categories().find((c) => c.id === a.categoryId)?.name ?? '—' },
      { label: 'Status', value: ASSET_STATUSES.find((s) => s.value === a.status)?.label ?? a.status },
      { label: 'Acquired', value: a.acquisitionDate },
      { label: 'Method', value: DEPRECIATION_METHODS.find((m) => m.value === a.method)?.label ?? a.method },
      { label: 'Useful life (months)', value: a.lifeMonths },
      { label: 'Custodian', value: a.custodian },
    ];
    if (a.capitalisedOn) items.push({ label: 'In service since', value: a.capitalisedOn });
    if (a.description) items.push({ label: 'Description', value: a.description, span: 2 });
    if (a.disposedOn) {
      items.push({ label: 'Disposed on', value: a.disposedOn });
      items.push({ label: 'Proceeds', value: a.disposalProceeds ?? 0 });
      items.push({ label: (a.disposalGainLoss ?? 0) >= 0 ? 'Gain on disposal' : 'Loss on disposal', value: Math.abs(a.disposalGainLoss ?? 0) });
    }
    return items;
  });

  ngOnInit(): void {
    this.categoriesApi.list({ page: 1, pageSize: 200, sort: 'code' }).subscribe({ next: (r) => this.categories.set(r.items), error: () => undefined });
    this.load();
  }

  load(showSpinner = true): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) { this.error.set('Invalid asset'); this.loading.set(false); return; }
    if (showSpinner) { this.loading.set(true); this.error.set(null); }
    this.api.get(id).subscribe({
      next: (a) => {
        this.asset.set(a);
        this.loading.set(false);
        this.api.schedule(id).pipe(catchError(() => of(null))).subscribe((s) => this.schedule.set(s));
      },
      error: (e) => { this.error.set(problemMessage(e, 'The asset could not be loaded')); this.loading.set(false); },
    });
  }

  private confirmDelete(a: AssetDto): void {
    this.modal.confirm({
      nzTitle: 'Delete this draft asset?', nzContent: 'It has not been capitalised, so nothing is posted. It is removed from the register.', nzOkText: 'Delete', nzOkDanger: true,
      nzOnOk: () => this.api.delete(a.id).subscribe({
        next: () => { this.message.success(doneText('deleted', 'Asset', a.number)); void this.router.navigate(['/assets/register']); },
        error: (e) => this.message.error(problemMessage(e, 'Could not delete the asset')),
      }),
    });
  }
}
