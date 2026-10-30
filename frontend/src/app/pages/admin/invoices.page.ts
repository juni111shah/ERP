import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormArray, FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzModalService } from 'ng-zorro-antd/modal';
import { API_BASE_URL } from '../../core/api/api-config';
import { INVOICE_STATUS_COLORS, InvoiceExtras, InvoiceExtrasService, InvoiceSheetComponent, daysOverdue, formatMoney, humanizeSource, invoiceStatusLabel, taxNameFor } from './invoice-sheet.component';
import { catchError, map, of } from 'rxjs';
import { InvoiceDto, InvoiceSummary, InvoicesApi, LegalEntitiesApi, LegalEntityDto, TaxCodeDto, TaxCodesApi } from '../../core/api/admin.api';
import { createPagedList } from '../../core/api/paged-list';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { LookupOption } from '../../core/custom-fields/lookup-registry';
import { asProblem, controlError, problemMessage } from '../../core/http/problem-details';
import { askReason } from '../../shared/reason-prompt';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { PartiesApi } from '../parties.page';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { I18nService } from '../../core/i18n/i18n.service';
import { RevealDirective } from '../../shared/reveal.directive';
import { StatCardComponent } from '../../shared/stat-card.component';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';

const STATUS_OPTIONS = ['draft', 'pendingApproval', 'issued', 'partlyPaid', 'paid', 'voided'];
/** Lease / property behind an invoice, supplied by the real-estate module only when it is installed (the core invoice knows nothing about leases). */
interface LeaseLink { invoiceId: string; leaseId: string; leaseNumber?: string | null; propertyId: string; propertyName?: string | null; unitNumber?: string | null; periodFrom: string; periodTo: string; }

/** Sales invoices and credit notes: draft with a line editor, issue (posts to the ledger), void, and apply a credit note to an invoice. */
@Component({
  selector: 'erp-invoices',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [StatCardComponent, NzToolTipModule, RevealDirective, TranslatePipe, ReactiveFormsModule, FormsModule, PagedTableComponent, HasPermissionDirective, NzButtonModule, NzDatePickerModule, NzDrawerModule, NzFormModule, NzIconModule, NzInputModule,
    NzInputNumberModule, NzModalModule, NzSelectModule, NzTagModule, InvoiceSheetComponent],
  styles: [`.num { font-variant-numeric: tabular-nums } .sub { color:#8c8c8c; font-size:12px }
    .line { display:grid; grid-template-columns: 2.4fr .8fr 1fr 1.2fr auto; gap:8px; margin-bottom:8px; align-items:center } .totals { display:flex; gap:24px; justify-content:flex-end; margin:8px 0 }`],
  template: `
    <h1 class="erp-page-title">Invoices</h1>
    <div class="erp-stat-grid erp-stat-grid--auto" data-testid="invoice-stats">
      <erp-stat-card label="Total invoiced" [value]="stats().invoiced" [loading]="list.loading() && list.items().length === 0" [caption]="stats().count + ' ' + ('table.onThisPage' | translate: 'on this page')" [tone]="0" />
      <erp-stat-card label="Outstanding" [value]="stats().outstanding" [loading]="list.loading() && list.items().length === 0" [tone]="2" />
      <erp-stat-card label="Overdue" [value]="stats().overdue" [loading]="list.loading() && list.items().length === 0" [caption]="stats().overdueCount + ' ' + ('ui.inv.invoicesLc' | translate: 'invoices')" tone="danger" />
      <erp-stat-card label="Drafts" [value]="stats().drafts" [loading]="list.loading() && list.items().length === 0" [tone]="3" />
    </div>
    <erp-paged-table [list]="list" [columns]="columns()" searchPlaceholder="Search number or customer" emptyText="No invoices yet" exportFilename="invoices" [refreshable]="false" [scrollX]="links() && objectKeys(links()).length ? '1650px' : '1400px'" (rowClick)="open($event)">
      <ng-template #cell let-row let-column="column" let-value="value">
        @switch (column.key) {
          @case ('status') { <nz-tag [nzColor]="color(row.status)">{{ label(row.status) }}</nz-tag> }
          @case ('number') { <span class="code-pill">{{ row.number || 'Draft' }}</span> @if (row.isCreditNote) { <nz-tag nzColor="purple" style="margin-inline-start:4px">Credit</nz-tag> } @if (source(row)) { <div class="sub">{{ source(row) }}</div> } }
          @case ('dueDate') { {{ row.dueDate }} @if (overdue(row) > 0) { <nz-tag nzColor="red" style="margin-inline-start:4px">{{ overdue(row) }}{{ 'ui.common.dayShort' | translate: 'd' }} {{ 'ui.inv.overdueLc' | translate: 'overdue' }}</nz-tag> } }
          @case ('lease') { {{ link(row)?.leaseNumber ?? '' }} }
          @case ('property') { {{ link(row)?.propertyName ?? '' }} @if (link(row)?.unitNumber) { <div class="sub">{{ 'ui.re.unit' | translate: 'Unit' }} {{ link(row)!.unitNumber }}</div> } }
          @default { <span class="num">{{ value }}</span> }
        }
      </ng-template>
      <nz-select toolbar [ngModel]="statusFilter()" (ngModelChange)="filterStatus($event)" nzAllowClear nzPlaceHolder="All statuses" style="width:160px" data-testid="invoice-status-filter">
        @for (s of statusOptions; track s) { <nz-option [nzValue]="s" [nzLabel]="label(s)"></nz-option> }
      </nz-select>
      <button toolbar nz-button (click)="list.reload()" data-testid="invoice-refresh"><span nz-icon nzType="reload"></span>Refresh</button>
      <button toolbar *erpHasPermission="'realestate.billing.run'" nz-button (click)="runBilling()" [nzLoading]="billing()" data-testid="invoice-run-billing"><span nz-icon nzType="thunderbolt"></span>Run billing</button>
      <button toolbar *erpHasPermission="'finance.invoices.write'" nz-button nzType="primary" (click)="open(null)" data-testid="invoice-new"><span nz-icon nzType="plus"></span>Add invoice</button>
    </erp-paged-table>

    <nz-drawer [nzVisible]="drawerOpen()" [nzWidth]="860" [nzTitle]="title()" (nzOnClose)="drawerOpen.set(false)" [nzFooter]="footer">
      <ng-container *nzDrawerContent>
        @if (invoice(); as inv) { <erp-invoice-sheet [invoice]="sheetData()" [extras]="extras()" [taxRates]="taxRates()" [compact]="true" style="margin-bottom:16px" /> }
        @if (invoice() && editable()) { <h4 style="margin-top:8px">Edit draft</h4> }
        <form [hidden]="!editable()" nz-form nzLayout="vertical" [formGroup]="form" data-testid="invoice-form">
          <nz-form-item><nz-form-label nzRequired>Legal entity</nz-form-label><nz-form-control [nzErrorTip]="err('legalEntityId')">
            <nz-select formControlName="legalEntityId" data-testid="inv-entity">@for (e of entities(); track e.id) { <nz-option [nzValue]="e.id" [nzLabel]="e.name"></nz-option> }</nz-select></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Customer</nz-form-label><nz-form-control [nzErrorTip]="err('partyId')">
            <nz-select formControlName="partyId" nzShowSearch nzServerSearch (nzOnSearch)="searchParties($event)" data-testid="inv-party">@for (o of partyOptions(); track o.value) { <nz-option [nzValue]="o.value" [nzLabel]="o.label"></nz-option> }</nz-select></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Invoice date</nz-form-label><nz-form-control [nzErrorTip]="err('invoiceDate')"><nz-date-picker formControlName="invoiceDate" style="width:100%" data-testid="inv-date"></nz-date-picker></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Due date</nz-form-label><nz-form-control [nzErrorTip]="err('dueDate')" nzExtra="Follows the invoice date + the customer's payment terms until you change it. Leave empty to use the terms."><nz-date-picker formControlName="dueDate" style="width:100%"></nz-date-picker></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Credit note</nz-form-label><nz-form-control><label><input type="checkbox" formControlName="isCreditNote" data-testid="inv-credit" /> This document credits the customer</label></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Notes</nz-form-label><nz-form-control><input nz-input formControlName="notes" /></nz-form-control></nz-form-item>
          <h4>Lines</h4>
          <div formArrayName="lines">
            @for (l of lines.controls; track $index) {
              <div class="line" [formGroupName]="$index">
                <input nz-input formControlName="description" placeholder="Description" [attr.data-testid]="'inv-desc-' + $index" />
                <nz-input-number formControlName="quantity" [nzMin]="0.0001" [nzPrecision]="2" [attr.data-testid]="'inv-qty-' + $index"></nz-input-number>
                <nz-input-number formControlName="price" [nzMin]="0" [nzPrecision]="2" [attr.data-testid]="'inv-price-' + $index"></nz-input-number>
                <nz-select formControlName="taxCodeId" nzAllowClear nzPlaceHolder="No tax">@for (t of taxes(); track t.id) { <nz-option [nzValue]="t.id" [nzLabel]="t.code + ' ' + t.rate + '%'"></nz-option> }</nz-select>
                @if (editable()) { <button nz-button nzType="text" type="button" (click)="removeLine($index)"><span nz-icon nzType="delete"></span></button> }
              </div>
            }
          </div>
          @if (editable()) { <button nz-button nzType="dashed" type="button" (click)="addLine()" data-testid="inv-add-line"><span nz-icon nzType="plus"></span> Add line</button> }
          <div class="totals" data-testid="inv-totals">
            <span>Net <b>{{ totals().net.toFixed(2) }}</b></span><span>Tax <b>{{ totals().tax.toFixed(2) }}</b></span><span>Total <b>{{ (totals().net + totals().tax).toFixed(2) }}</b></span>
          </div>
        </form>
      </ng-container>
    </nz-drawer>
    <ng-template #footer>
      <button nz-button (click)="drawerOpen.set(false)">Close</button>
      @if (invoice(); as inv) {
        @if (inv.id) { <button nz-button (click)="openDetails(inv.id)" style="margin-inline-start:8px" data-testid="inv-details"><span nz-icon nzType="file-text"></span>Details</button><button nz-button (click)="printInvoice(inv.id)" style="margin-inline-start:8px" data-testid="inv-print"><span nz-icon nzType="printer"></span>Print / PDF</button> }
        @if (inv.status === 'issued' && inv.paid === 0) { <button *erpHasPermission="'finance.invoices.void'" nz-button nzDanger (click)="voidDocument()" style="margin-inline-start:8px" data-testid="inv-void">Void</button> }
        @if (inv.isCreditNote && (inv.status === 'issued' || inv.status === 'partlyPaid')) { <button *erpHasPermission="'finance.invoices.credit'" nz-button (click)="applyOpen.set(true)" style="margin-inline-start:8px" data-testid="inv-apply">Apply to invoice</button> }
      }
      @if (editable()) {
        <button *erpHasPermission="'finance.invoices.write'" nz-button [nzLoading]="saving()" (click)="save(false)" style="margin-inline-start:8px" data-testid="inv-save">Save draft</button>
        <button *erpHasPermission="'finance.invoices.issue'" nz-button nzType="primary" [nzLoading]="saving()" (click)="save(true)" style="margin-inline-start:8px" data-testid="inv-issue">Save and issue</button>
      }
    </ng-template>

    <nz-modal [nzVisible]="applyOpen()" nzTitle="Apply credit note" (nzOnCancel)="applyOpen.set(false)" (nzOnOk)="apply()" nzOkText="Apply">
      <ng-container *nzModalContent>
        <nz-select [(ngModel)]="applyInvoice" nzShowSearch nzServerSearch (nzOnSearch)="searchOpenInvoices($event)" style="width:100%;margin-bottom:12px" nzPlaceHolder="Invoice to settle" data-testid="apply-invoice">
          @for (o of openInvoices(); track o.value) { <nz-option [nzValue]="o.value" [nzLabel]="o.label"></nz-option> }
        </nz-select>
        <nz-input-number [(ngModel)]="applyAmount" [nzMin]="0.01" [nzPrecision]="2" style="width:100%" data-testid="apply-amount"></nz-input-number>
      </ng-container>
    </nz-modal>`,
})
export class InvoicesPage {
  private readonly i18n = inject(I18nService);
  private readonly api = inject(InvoicesApi);
  private readonly parties = inject(PartiesApi);
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);
  private readonly http = inject(HttpClient);
  private readonly apiBase = inject(API_BASE_URL);
  private readonly modal = inject(NzModalService);
  private readonly extrasApi = inject(InvoiceExtrasService);
  private readonly entityPage = toSignal(inject(LegalEntitiesApi).list({ page: 1, pageSize: 100 }).pipe(catchError(() => of({ items: [] as LegalEntityDto[] }))), { initialValue: { items: [] as LegalEntityDto[] } });
  private readonly taxPage = toSignal(inject(TaxCodesApi).list({ page: 1, pageSize: 100 }).pipe(catchError(() => of({ items: [] as TaxCodeDto[] }))), { initialValue: { items: [] as TaxCodeDto[] } });
  readonly entities = computed(() => this.entityPage().items);
  readonly taxes = computed(() => this.taxPage().items.filter((t) => t.isActive));
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-invoiceDate' });
  readonly statusOptions = STATUS_OPTIONS;
  readonly statusFilter = signal<string | null>(null);
  readonly billing = signal(false);
  readonly extras = signal<InvoiceExtras | null>(null);
  /** invoice id → lease link; filled only for rows the real-estate module raised */
  readonly links = signal<Record<string, LeaseLink>>({});
  /** True while invoice-links is loading, so Lease/Property show a placeholder instead of a blank cell. */
  readonly linksLoading = signal(false);
  readonly taxRates = computed(() => Object.fromEntries(this.taxPage().items.map((t) => [t.id, t.rate])) as Record<string, number>);
  /** VAT / GST / Sales tax when every legal entity is in one country, else plain "Tax" (same table as the print layout). */
  readonly taxLabel = computed(() => { const c = [...new Set(this.entities().map((e) => e.countryCode))]; return c.length === 1 ? taxNameFor(c[0]) : 'Tax'; });
  readonly objectKeys = Object.keys;
  readonly columns = computed((): TableColumn<InvoiceSummary>[] => {
    const hasLinks = Object.keys(this.links()).length > 0;
    const money = (f: (r: InvoiceSummary) => number | undefined) => (r: InvoiceSummary) => formatMoney(f(r) ?? 0, r.currencyCode);
    return [
      { key: 'number', label: 'Number', sortField: 'number', width: '150px' },
      { key: 'invoiceDate', label: 'Date', sortField: 'invoiceDate', width: '110px', filterField: 'invoiceDate' },
      { key: 'partyName', label: 'Customer', width: '220px', value: (r) => r.partyName || '—' },
      ...(hasLinks ? [
        { key: 'lease', label: 'Lease', width: '120px', value: (r: InvoiceSummary) => this.links()[r.id]?.leaseNumber ?? (this.linksLoading() ? '…' : '') },
        { key: 'property', label: 'Property', width: '200px', value: (r: InvoiceSummary) => this.links()[r.id]?.propertyName ?? (this.linksLoading() ? '…' : '') },
      ] : []),
      { key: 'dueDate', label: 'Due', sortField: 'dueDate', width: '150px', filterField: 'dueDate' },
      { key: 'net', label: 'Net', width: '130px', align: 'right', value: money((r) => r.net) },
      { key: 'tax', label: this.taxLabel(), width: '120px', align: 'right', value: money((r) => r.tax) },
      { key: 'total', label: 'Total', sortField: 'total', width: '130px', align: 'right', value: money((r) => r.total) },
      { key: 'outstanding', label: 'Balance', width: '130px', align: 'right', value: money((r) => r.outstanding) },
      { key: 'status', label: 'Status', width: '120px', value: (r) => invoiceStatusLabel(r.status), filterField: 'status', filterOptions: STATUS_OPTIONS.map((s) => ({ value: s, label: invoiceStatusLabel(s) })) },
    ];
  });
  /** Figures for the cards above the table, from the rows on the current page, grouped by currency. */
  readonly stats = computed(() => {
    const rows = this.list.items();
    const sum = (pick: (r: InvoiceSummary) => number, keep: (r: InvoiceSummary) => boolean) => {
      const by = new Map<string, number>();
      for (const r of rows) if (keep(r)) by.set(r.currencyCode, (by.get(r.currencyCode) ?? 0) + (r.isCreditNote ? -1 : 1) * pick(r));
      return by.size ? [...by].map(([c, v]) => formatMoney(v, c)).join(' · ') : '0.00';
    };
    const live = (r: InvoiceSummary) => r.status !== 'draft' && r.status !== 'voided';
    return {
      count: rows.length,
      invoiced: sum((r) => r.total, live),
      outstanding: sum((r) => r.outstanding, (r) => live(r) && r.outstanding !== 0),
      overdue: sum((r) => r.outstanding, (r) => daysOverdue(r) > 0),
      overdueCount: rows.filter((r) => daysOverdue(r) > 0).length,
      drafts: rows.filter((r) => r.status === 'draft').length,
    };
  });
  private readonly loadLinks = effect(() => {
    const ids = this.list.items().filter((r) => (r.sourceType ?? '').toLowerCase().startsWith('realestate.')).map((r) => r.id);
    if (!ids.length) { this.links.set({}); return; }
    this.linksLoading.set(true);
    this.http.get<LeaseLink[]>(`${this.apiBase}/m/realestate/billing/invoice-links`, { params: { ids } }).pipe(catchError(() => of([] as LeaseLink[])))
      .subscribe((r) => { this.links.set(Object.fromEntries((r ?? []).map((l) => [l.invoiceId, l]))); this.linksLoading.set(false); });
  });
  readonly drawerOpen = signal(false);
  readonly saving = signal(false);
  readonly invoice = signal<InvoiceDto | null>(null);
  readonly partyOptions = signal<LookupOption[]>([]);
  readonly applyOpen = signal(false);
  readonly openInvoices = signal<LookupOption[]>([]);
  applyInvoice: string | null = null;
  applyAmount = 0;
  readonly form = this.fb.group({
    legalEntityId: ['', Validators.required], partyId: ['', Validators.required], invoiceDate: [null as Date | null, Validators.required], dueDate: [null as Date | null],
    isCreditNote: [false], notes: [''], lines: this.fb.array<unknown>([]),
  });
  private readonly value = toSignal(this.form.valueChanges, { initialValue: this.form.value });
  /** Payment terms (days) per party id; null = the party has no terms. */
  private readonly partyTerms = new Map<string, number | null>();
  /** True once the user picked a due date themselves; then invoice date / customer changes no longer move it. */
  private dueDateTouched = false;
  private syncingDueDate = false;

  constructor() {
    const c = this.form.controls;
    c.partyId.valueChanges.pipe(takeUntilDestroyed()).subscribe((id) => { if (!this.syncingDueDate && id) this.withTerms(id, () => this.recalcDueDate()); });
    c.invoiceDate.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => { if (!this.syncingDueDate) this.recalcDueDate(); });
    c.dueDate.valueChanges.pipe(takeUntilDestroyed()).subscribe((d) => {
      if (this.syncingDueDate) return;
      // Clearing the due date hands it back to the payment terms (the API applies them to an empty due date).
      this.dueDateTouched = !!d;
      if (!d) this.recalcDueDate();
    });
  }

  /** Mirrors the API rule (Invoicing.cs): due date = invoice date + the customer's PaymentTermsDays. No terms: leave the due date alone. */
  private termsDueDate(invoiceDate: Date | null | undefined, partyId: string | null | undefined): Date | null {
    const days = partyId ? this.partyTerms.get(partyId) : undefined;
    if (!invoiceDate || days === undefined || days === null) return null;
    return new Date(invoiceDate.getFullYear(), invoiceDate.getMonth(), invoiceDate.getDate() + days);
  }

  private recalcDueDate(): void {
    if (this.dueDateTouched || !this.editable()) return;
    const v = this.form.getRawValue();
    const due = this.termsDueDate(v.invoiceDate, v.partyId);
    if (!due) return;
    this.syncingDueDate = true;
    this.form.controls.dueDate.setValue(due);
    this.syncingDueDate = false;
  }

  /** Runs `then` once the party's payment terms are known (from the search results or a single lookup). */
  private withTerms(partyId: string, then: () => void): void {
    if (this.partyTerms.has(partyId)) { then(); return; }
    this.parties.get(partyId).subscribe({
      next: (p) => { this.partyTerms.set(partyId, p.paymentTermsDays ?? null); if (this.form.controls.partyId.value === partyId) then(); },
      error: () => undefined,
    });
  }
  readonly totals = computed(() => {
    const lines = (this.value().lines ?? []) as { quantity?: number | null; price?: number | null; taxCodeId?: string | null }[];
    const rate = (id?: string | null) => this.taxes().find((t) => t.id === id)?.rate ?? 0;
    const net = lines.reduce((s, l) => s + Math.round((l.quantity ?? 0) * (l.price ?? 0) * 100) / 100, 0);
    const tax = lines.reduce((s, l) => s + Math.round(Math.round((l.quantity ?? 0) * (l.price ?? 0) * 100) / 100 * rate(l.taxCodeId)) / 100, 0);
    return { net, tax };
  });
  readonly sheetData = computed(() => { const i = this.invoice(); return i ? { ...i, lines: i.lines.map((l) => ({ ...l, taxCodeId: l.taxCodeId ?? null })) } : null; });
  readonly editable = computed(() => !this.invoice() || this.invoice()!.status === 'draft');
  readonly title = computed(() => this.invoice() ? `${this.invoice()!.isCreditNote ? 'Credit note' : 'Invoice'} ${this.invoice()!.number}` : 'New invoice');

  get lines(): FormArray { return this.form.controls.lines as FormArray; }
  color(s: string): string { return INVOICE_STATUS_COLORS[s] ?? 'default'; }
  label(s: string): string { return invoiceStatusLabel(s); }
  overdue(r: InvoiceSummary): number { return daysOverdue(r); }
  source(r: InvoiceSummary): string { return humanizeSource(r.sourceType); }
  link(r: InvoiceSummary): LeaseLink | undefined { return this.links()[r.id]; }

  filterStatus(s: string | null): void {
    this.statusFilter.set(s);
    this.list.setFilters(s ? [{ field: 'status', op: 'eq', value: s }] : []);
  }

  /** Runs the module's billing run (invoices every lease instalment due by today). Only shown to users holding that module's permission. */
  runBilling(): void {
    this.modal.confirm({
      nzTitle: 'Run billing now?', nzContent: 'Creates and issues an invoice for every scheduled instalment due by today.', nzOkText: 'Run billing',
      nzOnOk: () => {
        this.billing.set(true);
        this.http.post<{ invoiced: number }>(`${this.apiBase}/m/realestate/billing/run`, { asOf: toIsoDate(new Date()) }).subscribe({
          next: (r) => { this.billing.set(false); this.message.success(`${this.i18n.t('ui.msg.billingRunCreated', 'Billing run created')} ${r?.invoiced ?? 0} ${this.i18n.t('ui.msg.invoicesParen', 'invoice(s)')}`); this.list.reload(); },
          error: (e) => { this.billing.set(false); this.fail(e); },
        });
      },
    });
  }
  err(name: string): string { return controlError(this.form.get(name)); }

  private newLine(l?: Partial<{ description: string; quantity: number; price: number; taxCodeId: string }>) {
    return this.fb.group({ description: [l?.description ?? '', Validators.required], quantity: [l?.quantity ?? 1], price: [l?.price ?? 0], taxCodeId: [l?.taxCodeId ?? null as string | null] });
  }
  addLine(): void { this.lines.push(this.newLine() as never); }
  removeLine(i: number): void { if (this.lines.length > 1) this.lines.removeAt(i); }

  searchParties(term: string): void {
    this.parties.list({ page: 1, pageSize: 20, search: term }).subscribe((r) => { r.items.forEach((p) => this.partyTerms.set(p.id, p.paymentTermsDays ?? null)); this.partyOptions.update((cur) => this.merge(cur, r.items.map((p) => ({ value: p.id, label: `${p.code} · ${p.name}` })))); });
  }
  private merge(current: LookupOption[], fresh: LookupOption[]): LookupOption[] {
    const keep = current.filter((c) => c.value === this.form.value.partyId && !fresh.some((f) => f.value === c.value));
    return [...keep, ...fresh];
  }

  /** `?open=<id>` opens a drawer; `?duplicate=<id>` / `?creditFor=<id>` start a new draft prefilled from that invoice (detail page actions). */
  ngOnInit(): void {
    const q = this.route.snapshot.queryParamMap;
    const open = q.get('open'), dup = q.get('duplicate'), credit = q.get('creditFor');
    if (open) this.open({ id: open } as never);
    else if (dup || credit) this.api.detail((dup ?? credit)!).subscribe((src) => this.prefill(src, !!credit && !dup));
  }

  private prefill(src: InvoiceDto, asCredit: boolean): void {
    this.extras.set(null);
    this.show(null);
    this.lines.clear();
    for (const l of src.lines.length ? src.lines : [{} as never]) this.lines.push(this.newLine({ description: l.description, quantity: l.quantity, price: l.price, taxCodeId: l.taxCodeId ?? null } as never) as never);
    this.form.patchValue({
      legalEntityId: src.legalEntityId, partyId: src.partyId,
      isCreditNote: asCredit ? true : src.isCreditNote,
      // No credit-note-for field exists on the invoice, so the original is referenced in the notes.
      notes: asCredit ? `Credit note for invoice ${src.number}` : src.notes ?? '',
    });
    this.partyOptions.set([{ value: src.partyId, label: src.partyName ?? src.partyId }]);
  }

  openDetails(id: string): void { void this.router.navigate(['/finance/invoices', id]); }
  printInvoice(id: string): void { window.open(`/print/invoice/${id}`, '_blank'); }

  open(row: InvoiceSummary | null): void {
    if (!row) { this.extras.set(null); this.show(null); return; }
    this.extras.set(null);
    this.api.detail(row.id).subscribe((inv) => this.show(inv));
    this.extrasApi.load(row.id).subscribe((x) => this.extras.set(x));
  }

  private show(inv: InvoiceDto | null): void {
    this.invoice.set(inv);
    this.lines.clear();
    for (const l of inv?.lines ?? [{} as never]) this.lines.push(this.newLine(l as never) as never);
    const entities = this.entities();
    this.syncingDueDate = true;
    this.form.patchValue({
      legalEntityId: inv?.legalEntityId ?? (entities.length === 1 ? entities[0].id : ''), partyId: inv?.partyId ?? '', isCreditNote: inv?.isCreditNote ?? false, notes: inv?.notes ?? '',
      invoiceDate: inv ? new Date(inv.invoiceDate + 'T00:00:00') : new Date(), dueDate: inv ? new Date(inv.dueDate + 'T00:00:00') : null,
    });
    this.partyOptions.set(inv ? [{ value: inv.partyId, label: inv.partyName ?? inv.partyId }] : []);
    if (!inv) this.searchParties('');
    this.editable() ? this.form.enable() : this.form.disable();
    this.syncingDueDate = false;
    // A new invoice follows the terms; an opened draft keeps its due date as an override only if it differs from date + terms.
    this.dueDateTouched = !!inv;
    if (inv && this.editable()) this.withTerms(inv.partyId, () => {
      // Same rule as the API: no terms counts as 0 days, so such a draft is "automatic" when due = invoice date.
      const v = this.form.getRawValue(), days = this.partyTerms.get(inv.partyId) ?? 0, d = v.invoiceDate;
      const auto = d ? toIsoDate(new Date(d.getFullYear(), d.getMonth(), d.getDate() + days)) : null;
      this.dueDateTouched = !v.dueDate || toIsoDate(v.dueDate) !== auto;
    });
    this.drawerOpen.set(true);
  }

  save(issue: boolean): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); Object.values(this.form.controls).forEach((c) => { c.markAsDirty(); c.updateValueAndValidity(); }); return; }
    const v = this.form.getRawValue();
    const body = {
      legalEntityId: v.legalEntityId, partyId: v.partyId, invoiceDate: toIsoDate(v.invoiceDate!), dueDate: v.dueDate ? toIsoDate(v.dueDate) : null, isCreditNote: v.isCreditNote, notes: v.notes || null,
      lines: (v.lines as { description: string; quantity: number; price: number; taxCodeId: string | null }[]).map((l) => ({ ...l, taxCodeId: l.taxCodeId || null })),
    };
    this.saving.set(true);
    this.api.save(this.invoice()?.id ?? null, body).subscribe({
      next: (saved) => {
        if (!issue) { this.done('Draft saved'); return; }
        this.api.issue(saved.id).subscribe({ next: (i) => { this.done(`${this.i18n.t('ui.msg.issuedAs', 'Issued as')} ${i.number}`); for (const w of i.warnings ?? []) this.message.warning(w, { nzDuration: 8000 }); }, error: (e) => { this.saving.set(false); this.invoice.set(saved); this.list.reload(); this.fail(e); } });
      },
      error: (e) => { this.saving.set(false); this.fail(e); },
    });
  }

  voidDocument(): void {
    const inv = this.invoice();
    if (!inv) return;
    void askReason(this.modal, 'Void this invoice?', 'Void Invoice', 'This reverses its journal and cannot be undone. The invoice will be marked as voided.').then((reason) => {
      if (reason) this.api.void(inv.id, reason).subscribe({ next: () => this.done('Invoice voided'), error: (e) => this.fail(e) });
    });
  }

  searchOpenInvoices(term: string): void {
    const inv = this.invoice();
    if (!inv) return;
    this.api.list({ page: 1, pageSize: 20, search: term, filters: [{ field: 'partyId', op: 'eq', value: inv.partyId }, { field: 'isCreditNote', op: 'eq', value: false }] })
      .pipe(map((r) => r.items.filter((i) => i.status === 'issued' || i.status === 'partlyPaid')))
      .subscribe((items) => this.openInvoices.set(items.map((i) => ({ value: i.id, label: `${i.number} · ${this.i18n.t('ui.common.outstandingLc', 'outstanding')} ${i.outstanding.toFixed(2)}` }))));
  }

  apply(): void {
    const credit = this.invoice();
    if (!credit || !this.applyInvoice || !this.applyAmount) return;
    this.api.applyCredit(credit.id, this.applyInvoice, this.applyAmount).subscribe({
      next: () => { this.applyOpen.set(false); this.done('Credit applied'); },
      error: (e) => this.fail(e),
    });
  }

  private done(text: string): void { this.saving.set(false); this.drawerOpen.set(false); this.message.success(text); this.list.reload(); }
  private fail(e: unknown): void { this.message.error(problemMessage(e, 'Could not save the invoice', 'reason')); }
}
