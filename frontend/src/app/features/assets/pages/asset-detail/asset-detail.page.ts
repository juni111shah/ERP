import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { catchError, of } from 'rxjs';
import { AccountsApi, BillsApi, BusinessUnitsApi } from '@core/api/admin.api';
import { AssetDto, AssetScheduleDto, AssetsApi } from '@core/api/assets.api';
import { PartiesApi } from '@core/api/parties.api';
import { displayDate } from '@core/format/display';
import { doneText } from '@core/format/done-text';
import { problemMessage } from '@core/http/problem-details';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { SessionStore } from '@core/session/session.store';
import { ActionButton, DescriptionItem, DetailPageComponent, KpiCard } from '@shared/components/detail-page/detail-page.component';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { CanPipe } from '@shared/pipes/can.pipe';
import { ActionFormState } from '@shared/utils/action-form-state';
import { defaultLegalEntityId, legalEntityOptions } from '@shared/utils/entity-options';
import { MasterDataState } from '@shared/utils/master-data-state';
import { assetFields, businessUnitLookup, capitaliseFields, disposeFields } from '../../asset-form';
import { assetDescriptions, assetMoney } from '../../asset-labels';
import { AssetLookups } from '../../asset-lookups';

/** One fixed asset: its values, depreciation schedule and the capitalise, dispose, edit and delete actions. */
@Component({
  selector: 'erp-asset-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, RouterLink, NzCardModule, NzModalModule, NzSpinModule, NzTableModule, DetailPageComponent, RecordDrawerComponent, StatePanelComponent, StatusTagComponent, CanPipe],
  providers: [AssetLookups],
  styleUrl: './asset-detail.page.scss',
  templateUrl: './asset-detail.page.html',
})
export class AssetDetailPage implements OnInit {
  private readonly assetsApi = inject(AssetsApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly session = inject(SessionStore);
  private readonly accountsApi = inject(AccountsApi);
  private readonly unitsApi = inject(BusinessUnitsApi);
  private readonly lookups = inject(AssetLookups);
  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly fmtDate = displayDate;

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly asset = signal<AssetDto | null>(null);
  readonly schedule = signal<AssetScheduleDto | null>(null);
  readonly categories = this.lookups.categories;

  readonly edit = new MasterDataState<AssetDto>({
    save: (id, body) => this.assetsApi.save(id, body), reload: () => this.load(false), message: this.message, entityName: 'Asset',
  });
  readonly capitalise = new ActionFormState<AssetDto>({
    run: (payload) => this.assetsApi.capitalise(this.asset()!.id, payload),
    success: () => doneText('completed', 'Capitalisation', this.asset()?.number),
    done: () => this.load(false), message: this.message, failure: 'Could not capitalise the asset',
  });
  readonly dispose = new ActionFormState<AssetDto>({
    run: (payload) => this.assetsApi.dispose(this.asset()!.id, payload),
    success: () => doneText('completed', 'Disposal', this.asset()?.number),
    done: () => this.load(false), message: this.message, failure: 'Could not dispose of the asset',
  });

  readonly editFields = computed<FieldSpec[]>(() => assetFields(legalEntityOptions(this.session), defaultLegalEntityId(this.session), this.categories(), businessUnitLookup(this.unitsApi)));
  readonly capitaliseFields: FieldSpec[] = capitaliseFields({ accounts: this.accountsApi, parties: inject(PartiesApi), bills: inject(BillsApi) });
  readonly disposeFields: FieldSpec[] = disposeFields(this.accountsApi);

  readonly actions = computed((): ActionButton[] => {
    const a = this.asset();
    if (!a) return [];
    return [
      { label: 'Capitalise', icon: 'check', type: 'primary', permission: 'assets.register.capitalise', visible: a.status === 'draft', handler: () => this.capitalise.show(a) },
      { label: 'Dispose', icon: 'export', permission: 'assets.register.dispose', visible: a.status === 'capitalised' || a.status === 'fullyDepreciated', handler: () => this.dispose.show(a) },
      { label: 'Edit', icon: 'edit', permission: 'assets.register.manage', visible: a.status === 'draft', handler: () => this.edit.edit(a) },
      { label: 'Delete', icon: 'delete', danger: true, permission: 'assets.register.manage', visible: a.status === 'draft', handler: () => this.confirmDelete(a) },
      { label: 'Capitalisation journal', icon: 'book', permission: 'finance.journals.read', visible: !!a.capitalisationJournalId, handler: () => this.router.navigate(['/finance/journals', a.capitalisationJournalId]) },
      { label: 'Source bill', icon: 'file-text', permission: 'finance.bills.read', visible: !!a.sourceBillId, handler: () => this.router.navigate(['/finance/bills', a.sourceBillId]) },
      { label: 'Disposal journal', icon: 'book', permission: 'finance.journals.read', visible: !!a.disposalJournalId, handler: () => this.router.navigate(['/finance/journals', a.disposalJournalId]) },
    ];
  });

  readonly kpiCards = computed((): KpiCard[] => {
    const a = this.asset();
    if (!a) return [];
    return [
      { title: 'Cost', value: assetMoney(a.cost), suffix: this.currency() },
      { title: 'Accumulated depreciation', value: assetMoney(a.accumulatedDepreciation), suffix: this.currency() },
      { title: 'Net book value', value: assetMoney(a.netBookValue), suffix: this.currency() },
      { title: 'Salvage value', value: assetMoney(a.salvageValue), suffix: this.currency() },
    ];
  });

  readonly descriptions = computed((): DescriptionItem[] => {
    const a = this.asset();
    return a ? assetDescriptions(a, this.lookups.category(a.categoryId)) : [];
  });

  ngOnInit(): void {
    this.lookups.loadCategories();
    this.load();
  }

  load(showSpinner = true): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) { this.error.set('Invalid asset'); this.loading.set(false); return; }
    if (showSpinner) { this.loading.set(true); this.error.set(null); }
    this.assetsApi.get(id).subscribe({
      next: (a) => {
        this.asset.set(a);
        this.loading.set(false);
        this.assetsApi.schedule(id).pipe(catchError(() => of(null))).subscribe((s) => this.schedule.set(s));
      },
      error: (e) => { this.error.set(problemMessage(e, 'The asset could not be loaded')); this.loading.set(false); },
    });
  }

  private confirmDelete(a: AssetDto): void {
    this.modal.confirm({
      nzTitle: 'Delete this draft asset?', nzContent: 'It has not been capitalised, so nothing is posted. It is removed from the register.', nzOkText: 'Delete', nzOkDanger: true,
      nzOnOk: () => this.assetsApi.delete(a.id).subscribe({
        next: () => { this.message.success(doneText('deleted', 'Asset', a.number)); void this.router.navigate(['/assets/register']); },
        error: (e) => this.message.error(problemMessage(e, 'Could not delete the asset')),
      }),
    });
  }
}
