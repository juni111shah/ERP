import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { catchError, of } from 'rxjs';
import { AccountDto, AccountsApi, LegalEntitiesApi, LegalEntityDto } from '../../core/api/admin.api';
import { createPagedList } from '../../core/api/paged-list';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { LookupOption } from '../../core/custom-fields/lookup-registry';
import { asProblem, controlError } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { matchText, TableToolsComponent } from '../../shared/table-tools.component';
import { AcquisitionOverview, AcquisitionSummary, AcquisitionsApi, FUNDING_TYPES, FundingSourceDto, PropertiesApi, humanize } from './realestate.api';
import { StatusTagComponent } from './status-tag.component';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { I18nService } from '../../core/i18n/i18n.service';
import { RevealDirective } from '../../shared/reveal.directive';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { FormsModule } from '@angular/forms';

/** Acquisitions and funding: KPI cards, the acquisitions table, funding schedules and the new-acquisition form (with its funding sources). */
@Component({
  selector: 'erp-acquisitions',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, TableToolsComponent, NzToolTipModule, RevealDirective, TranslatePipe, ReactiveFormsModule, DecimalPipe, PagedTableComponent, HasPermissionDirective, StatusTagComponent, NzButtonModule, NzDatePickerModule, NzDrawerModule, NzFormModule, NzIconModule,
    NzInputModule, NzInputNumberModule, NzSelectModule, NzTableModule],
  styles: [`.kpis { display:grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap:16px; margin-bottom:20px } .kpi { border:1px solid var(--erp-color-border, #e5e8ec); border-top:3px solid #bfa66a; border-radius:8px; padding:12px 16px; background:var(--erp-color-surface, #fff) } .kpi b { display:block; font-size:20px; margin:4px 0 } .kpi small { color: var(--erp-color-text-secondary, #667) }
            .fund { display:grid; grid-template-columns: 1.2fr 1.6fr 1.2fr .8fr .8fr auto; gap:8px; margin-bottom:8px; align-items:center }`],
  template: `
    <h1 class="erp-page-title">Acquisitions and funding</h1>
    @if (overview(); as o) {
      <div class="kpis" data-testid="acq-kpis">
        <div class="kpi">Acquisition consideration<b>{{ o.consideration | number: '1.0-0' }}</b><small>{{ o.count }} {{ 'ui.re.acquisitionsLc' | translate: 'acquisitions' }}</small></div>
        <div class="kpi">Down payments<b>{{ o.downPayments | number: '1.0-0' }}</b><small>equity contributed</small></div>
        <div class="kpi">Financing arranged<b>{{ o.financing | number: '1.0-0' }}</b><small>{{ 'ui.common.outstandingLc' | translate: 'outstanding' }} {{ o.outstanding | number: '1.0-0' }}</small></div>
        <div class="kpi">Unfunded balance<b>{{ o.unfunded | number: '1.0-0' }}</b><small>price less down payment and financing</small></div>
      </div>
    }
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search seller or reference" emptyText="No acquisitions yet" (rowClick)="open($event)">
      <ng-template #cell let-row let-column="column" let-value="value">
        @if (column.key === 'status') { <erp-status-tag [status]="row.status" /> } @else { {{ value }} }
      </ng-template>
      <button toolbar *erpHasPermission="'realestate.acquisitions.manage'" nz-button erpReveal data-reveal="on" [attr.aria-label]="'ui.add.newAcquisition' | translate: 'New acquisition'" nz-tooltip [nzTooltipTitle]="'ui.add.newAcquisition' | translate: 'New acquisition'" nzType="primary" (click)="openNew()" data-testid="acq-new"><span nz-icon nzType="plus"></span>New acquisition</button>
    </erp-paged-table>

    <h3 style="margin-top:24px">Funding and instalment schedules</h3>
    <erp-table-tools [(search)]="fundingQ" (refresh)="refresh()" testId="funding">
      <nz-select [ngModel]="fundingType()" (ngModelChange)="fundingType.set($event)" nzAllowClear [nzPlaceHolder]="'ui.common.filterType' | translate: 'Filter by type'" style="width:170px" data-testid="funding-type-filter">
        @for (t of types; track t.value) { <nz-option [nzValue]="t.value" [nzLabel]="t.label"></nz-option> }
      </nz-select>
    </erp-table-tools>
    <nz-table [nzData]="fundingShown()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="funding-table">
      <thead><tr><th>Source</th><th>Lender</th><th>Principal</th><th>Rate</th><th>Monthly</th><th>Repaid</th><th>Outstanding</th><th>Next due</th></tr></thead>
      <tbody>
        @for (f of fundingShown(); track f.id) {
          <tr><td>{{ humanize(f.type) }}</td><td>{{ f.lender }}</td><td>{{ f.principal | number: '1.0-0' }}</td><td>{{ f.annualRatePercent }}%</td><td>{{ f.monthlyPayment | number: '1.2-2' }}</td>
            <td>{{ f.paid }}/{{ f.instalmentCount }}</td><td>{{ f.outstanding | number: '1.0-0' }}</td><td>{{ f.nextDue ?? '—' }}</td></tr>
        }
      </tbody>
    </nz-table>

    <nz-drawer [nzVisible]="drawerOpen()" [nzWidth]="760" nzTitle="New property acquisition" (nzOnClose)="drawerOpen.set(false)" [nzFooter]="footer">
      <ng-container *nzDrawerContent>
        <form nz-form nzLayout="vertical" [formGroup]="form" data-testid="acq-form">
          <nz-form-item><nz-form-label nzRequired>Legal entity</nz-form-label><nz-form-control [nzErrorTip]="err('legalEntityId')">
            <nz-select formControlName="legalEntityId" data-testid="acq-entity">@for (e of entities(); track e.id) { <nz-option [nzValue]="e.id" [nzLabel]="e.name"></nz-option> }</nz-select></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Property</nz-form-label><nz-form-control [nzErrorTip]="err('propertyId')" nzExtra="Create the property record first if it is new to the portfolio.">
            <nz-select formControlName="propertyId" nzShowSearch nzServerSearch (nzOnSearch)="searchProperties($event)" data-testid="acq-property">@for (o of propertyOptions(); track o.value) { <nz-option [nzValue]="o.value" [nzLabel]="o.label"></nz-option> }</nz-select></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Seller / developer</nz-form-label><nz-form-control [nzErrorTip]="err('sellerName')"><input nz-input formControlName="sellerName" data-testid="acq-seller" /></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Purchase date</nz-form-label><nz-form-control><nz-date-picker formControlName="purchaseDate" style="width:100%"></nz-date-picker></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>SPA / contract reference</nz-form-label><nz-form-control><input nz-input formControlName="contractReference" /></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Purchase price</nz-form-label><nz-form-control [nzErrorTip]="err('purchasePrice')"><nz-input-number formControlName="purchasePrice" [nzMin]="0" [nzPrecision]="2" style="width:100%" data-testid="acq-price"></nz-input-number></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Down payment (equity)</nz-form-label><nz-form-control [nzErrorTip]="err('downPayment')"><nz-input-number formControlName="downPayment" [nzMin]="0" [nzPrecision]="2" style="width:100%" data-testid="acq-down"></nz-input-number></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Transfer and registration fees</nz-form-label><nz-form-control><nz-input-number formControlName="transferFees" [nzMin]="0" [nzPrecision]="2" style="width:100%"></nz-input-number></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Other capitalised costs</nz-form-label><nz-form-control nzExtra="Agency, valuation, legal fees"><nz-input-number formControlName="otherCosts" [nzMin]="0" [nzPrecision]="2" style="width:100%"></nz-input-number></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Pays down payment and fees from</nz-form-label><nz-form-control [nzErrorTip]="err('payFromAccountId')" nzExtra="Required before completion when cash is paid.">
            <nz-select formControlName="payFromAccountId" nzAllowClear data-testid="acq-bank">@for (a of banks(); track a.id) { <nz-option [nzValue]="a.id" [nzLabel]="a.code + ' · ' + a.name"></nz-option> }</nz-select></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Notes</nz-form-label><nz-form-control><input nz-input formControlName="notes" /></nz-form-control></nz-form-item>
          <h4>Funding sources</h4>
          <div formArrayName="funding">
            @for (f of fundingRows.controls; track $index) {
              <div class="fund" [formGroupName]="$index">
                <nz-select formControlName="type">@for (t of types; track t.value) { <nz-option [nzValue]="t.value" [nzLabel]="t.label"></nz-option> }</nz-select>
                <input nz-input formControlName="lender" placeholder="Lender" [attr.data-testid]="'fund-lender-' + $index" />
                <nz-input-number formControlName="principal" [nzMin]="0" [nzPrecision]="2" [attr.data-testid]="'fund-principal-' + $index"></nz-input-number>
                <nz-input-number formControlName="annualRatePercent" [nzMin]="0" [nzMax]="100" [nzPrecision]="2"></nz-input-number>
                <nz-input-number formControlName="termMonths" [nzMin]="1" [nzMax]="600" [nzPrecision]="0"></nz-input-number>
                <button nz-button nzType="text" type="button" (click)="removeFunding($index)"><span nz-icon nzType="delete"></span></button>
              </div>
            }
          </div>
          <button nz-button erpReveal data-reveal="on" [attr.aria-label]="'ui.add.addFundingSource' | translate: 'Add funding source'" nz-tooltip [nzTooltipTitle]="'ui.add.addFundingSource' | translate: 'Add funding source'" type="button" (click)="addFunding()" data-testid="fund-add"><span nz-icon nzType="plus"></span>Add funding source</button>
          <p style="margin-top:8px">Unfunded balance: <b data-testid="acq-unfunded">{{ unfunded() | number: '1.2-2' }}</b></p>
        </form>
      </ng-container>
    </nz-drawer>
    <ng-template #footer>
      <button nz-button (click)="drawerOpen.set(false)">Cancel</button>
      <button nz-button nzType="primary" [nzLoading]="saving()" (click)="save()" style="margin-inline-start:8px" data-testid="acq-save">Save draft</button>
    </ng-template>`,
})
export class AcquisitionsPage {
  private readonly i18n = inject(I18nService);
  private readonly api = inject(AcquisitionsApi);
  private readonly properties = inject(PropertiesApi);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly fb = inject(FormBuilder);
  private readonly entityPage = toSignal(inject(LegalEntitiesApi).list({ page: 1, pageSize: 100 }).pipe(catchError(() => of({ items: [] as LegalEntityDto[] }))), { initialValue: { items: [] as LegalEntityDto[] } });
  private readonly accountPage = toSignal(inject(AccountsApi).list({ page: 1, pageSize: 200, sort: 'code' }).pipe(catchError(() => of({ items: [] as AccountDto[] }))), { initialValue: { items: [] as AccountDto[] } });
  readonly entities = computed(() => this.entityPage().items);
  readonly banks = computed(() => this.accountPage().items.filter((a) => a.type === 'asset' && a.isPostable && a.isActive && (a.isBank || a.systemRole === 'bank')));
  readonly types = FUNDING_TYPES;
  readonly humanize = humanize;
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-purchaseDate' });
  readonly overview = signal<AcquisitionOverview | null>(null);
  readonly funding = signal<FundingSourceDto[]>([]);
  readonly fundingQ = signal('');
  readonly fundingType = signal<string | null>(null);
  /** Client-side filter: GET /realestate/funding takes no query parameters. */
  readonly fundingShown = computed(() => this.funding().filter((f) => matchText(this.fundingQ(), f.lender, humanize(f.type)) && (!this.fundingType() || f.type === this.fundingType())));
  readonly columns: TableColumn<AcquisitionSummary>[] = [
    { key: 'number', label: 'Reference', width: '150px' },
    { key: 'sellerName', label: 'Seller' },
    { key: 'purchaseDate', label: 'Purchase date', sortField: 'purchaseDate', width: '130px' },
    { key: 'purchasePrice', label: 'Price', sortField: 'purchasePrice', value: (a) => a.purchasePrice.toLocaleString() },
    { key: 'downPayment', label: 'Down payment', value: (a) => a.downPayment.toLocaleString() },
    { key: 'status', label: 'Status', width: '110px' },
  ];
  readonly drawerOpen = signal(false);
  readonly saving = signal(false);
  readonly propertyOptions = signal<LookupOption[]>([]);
  readonly form = this.fb.group({
    legalEntityId: ['', Validators.required], propertyId: ['', Validators.required], sellerName: ['', Validators.required], purchaseDate: [null as Date | null, Validators.required], contractReference: [''],
    purchasePrice: [null as number | null, [Validators.required, Validators.min(0.01)]], downPayment: [0], transferFees: [0], otherCosts: [0], payFromAccountId: [null as string | null], notes: [''],
    funding: this.fb.array<unknown>([]),
  });
  private readonly value = toSignal(this.form.valueChanges, { initialValue: this.form.value });
  readonly unfunded = computed(() => {
    const v = this.value(); const funding = ((v.funding ?? []) as { principal?: number | null }[]).reduce((s, f) => s + (f.principal ?? 0), 0);
    return (v.purchasePrice ?? 0) - (v.downPayment ?? 0) - funding;
  });

  get fundingRows(): FormArray { return this.form.controls.funding as FormArray; }

  constructor() { this.refresh(); }

  err(name: string): string { return controlError(this.form.get(name)); }

  refresh(): void {
    this.api.overview().subscribe((o) => this.overview.set(o));
    this.api.funding().subscribe((f) => this.funding.set(f));
  }

  open(a: AcquisitionSummary): void { void this.router.navigate(['/real-estate/acquisitions', a.id]); }

  searchProperties(term: string): void {
    this.properties.list({ page: 1, pageSize: 20, search: term }).subscribe((r) => this.propertyOptions.set(r.items.map((p) => ({ value: p.id, label: `${p.code} · ${p.name}` }))));
  }

  addFunding(): void {
    this.fundingRows.push(this.fb.group({ type: ['bankMortgage'], lender: ['', Validators.required], principal: [0], annualRatePercent: [0], termMonths: [12] }) as never);
  }
  removeFunding(i: number): void { this.fundingRows.removeAt(i); }

  openNew(): void {
    this.fundingRows.clear();
    const entities = this.entities();
    this.form.reset({ legalEntityId: entities.length === 1 ? entities[0].id : '', propertyId: '', sellerName: '', purchaseDate: new Date(), contractReference: '', purchasePrice: null, downPayment: 0, transferFees: 0, otherCosts: 0, payFromAccountId: null, notes: '' });
    this.propertyOptions.set([]); this.searchProperties('');
    this.drawerOpen.set(true);
  }

  save(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); Object.values(this.form.controls).forEach((c) => { c.markAsDirty(); c.updateValueAndValidity(); }); return; }
    const v = this.form.getRawValue();
    const firstDue = new Date(v.purchaseDate!); firstDue.setMonth(firstDue.getMonth() + 1);
    const body = {
      legalEntityId: v.legalEntityId, propertyId: v.propertyId, sellerName: v.sellerName, purchaseDate: toIsoDate(v.purchaseDate!), contractReference: v.contractReference || null, purchasePrice: v.purchasePrice,
      downPayment: v.downPayment ?? 0, transferFees: v.transferFees ?? 0, otherCosts: v.otherCosts ?? 0, payFromAccountId: v.payFromAccountId, notes: v.notes || null,
      funding: (v.funding as { type: string; lender: string; principal: number; annualRatePercent: number; termMonths: number }[]).map((f) => ({ ...f, firstDueDate: toIsoDate(firstDue) })),
    };
    this.saving.set(true);
    this.api.save(null, body).subscribe({
      next: (a) => { this.saving.set(false); this.drawerOpen.set(false); this.message.success(`${this.i18n.t('ui.msg.draftSaved', 'Draft saved')}: ${a.number}`); void this.router.navigate(['/real-estate/acquisitions', a.id]); },
      error: (e) => { this.saving.set(false); const p = asProblem(e); this.message.error(p?.detail ?? p?.title ?? 'Could not save the acquisition'); },
    });
  }
}
