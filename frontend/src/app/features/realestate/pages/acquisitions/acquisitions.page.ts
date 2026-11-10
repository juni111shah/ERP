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
import { NzTagModule } from 'ng-zorro-antd/tag';
import { catchError, of } from 'rxjs';
import { AccountDto, AccountsApi, LegalEntitiesApi, LegalEntityDto } from '@core/api/admin.api';
import { createPagedList } from '@core/api/paged-list';
import { toIsoDate } from '@core/custom-fields/custom-field-form';
import { LookupOption } from '@core/custom-fields/lookup-registry';
import { asProblem, controlError } from '@core/http/problem-details';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { matchText, TableToolsComponent } from '@shared/components/table-tools/table-tools.component';
import { AcquisitionOverview, AcquisitionSummary, AcquisitionsApi, FUNDING_TYPES, FundingSourceDto, PropertiesApi, humanize } from '@core/api/realestate.api';
import { StatusTagComponent } from '../../components/status-tag/status-tag.component';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { I18nService } from '@core/i18n/i18n.service';
import { RevealDirective } from '@shared/directives/reveal.directive';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { FormsModule } from '@angular/forms';
import { ErpDatePipe } from '@shared/pipes/display.pipes';

/** Acquisitions and funding: KPI cards, the acquisitions table, funding schedules and the new-acquisition form (with its funding sources). */
@Component({
  selector: 'erp-acquisitions',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, StatCardComponent, FormsModule, TableToolsComponent, NzToolTipModule, RevealDirective, TranslatePipe, ReactiveFormsModule, DecimalPipe, PagedTableComponent, HasPermissionDirective, StatusTagComponent, NzButtonModule, NzDatePickerModule, NzDrawerModule, NzFormModule, NzIconModule,
    NzInputModule, NzInputNumberModule, NzSelectModule, NzTableModule, NzTagModule],
  styleUrl: './acquisitions.page.scss',
  templateUrl: './acquisitions.page.html',
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
    { key: 'purchasePrice', label: 'Price', sortField: 'purchasePrice', align: 'right', value: (a) => a.purchasePrice.toLocaleString() },
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
