import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormArray, FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
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
import { catchError, map, of } from 'rxjs';
import { InvoiceDto, InvoiceSummary, InvoicesApi, LegalEntitiesApi, LegalEntityDto, TaxCodeDto, TaxCodesApi } from '../../core/api/admin.api';
import { createPagedList } from '../../core/api/paged-list';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { LookupOption } from '../../core/custom-fields/lookup-registry';
import { asProblem, controlError } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { PartiesApi } from '../parties.page';

const STATUS_COLORS: Record<string, string> = { draft: 'default', issued: 'blue', partlyPaid: 'gold', paid: 'green', voided: 'red' };
const STATUS_LABELS: Record<string, string> = { partlyPaid: 'Part paid' };

/** Sales invoices and credit notes: draft with a line editor, issue (posts to the ledger), void, and apply a credit note to an invoice. */
@Component({
  selector: 'erp-invoices',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, FormsModule, PagedTableComponent, HasPermissionDirective, NzButtonModule, NzDatePickerModule, NzDrawerModule, NzFormModule, NzIconModule, NzInputModule,
    NzInputNumberModule, NzModalModule, NzSelectModule, NzTagModule],
  styles: [`.line { display:grid; grid-template-columns: 2.4fr .8fr 1fr 1.2fr auto; gap:8px; margin-bottom:8px; align-items:center } .totals { display:flex; gap:24px; justify-content:flex-end; margin:8px 0 }`],
  template: `
    <h1 class="erp-page-title">Invoices</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search number or customer" emptyText="No invoices yet" (rowClick)="open($event)">
      <ng-template #cell let-row let-column="column" let-value="value">
        @if (column.key === 'status') { <nz-tag [nzColor]="color(row.status)">{{ label(row.status) }}</nz-tag> } @else { {{ value }} }
      </ng-template>
      <button toolbar *erpHasPermission="'finance.invoices.write'" nz-button nzType="primary" (click)="open(null)" data-testid="invoice-new"><span nz-icon nzType="plus"></span>New invoice</button>
    </erp-paged-table>

    <nz-drawer [nzVisible]="drawerOpen()" [nzWidth]="860" [nzTitle]="title()" (nzOnClose)="drawerOpen.set(false)" [nzFooter]="footer">
      <ng-container *nzDrawerContent>
        <form nz-form nzLayout="vertical" [formGroup]="form" data-testid="invoice-form">
          <nz-form-item><nz-form-label nzRequired>Legal entity</nz-form-label><nz-form-control [nzErrorTip]="err('legalEntityId')">
            <nz-select formControlName="legalEntityId" data-testid="inv-entity">@for (e of entities(); track e.id) { <nz-option [nzValue]="e.id" [nzLabel]="e.name"></nz-option> }</nz-select></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Customer</nz-form-label><nz-form-control [nzErrorTip]="err('partyId')">
            <nz-select formControlName="partyId" nzShowSearch nzServerSearch (nzOnSearch)="searchParties($event)" data-testid="inv-party">@for (o of partyOptions(); track o.value) { <nz-option [nzValue]="o.value" [nzLabel]="o.label"></nz-option> }</nz-select></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Invoice date</nz-form-label><nz-form-control [nzErrorTip]="err('invoiceDate')"><nz-date-picker formControlName="invoiceDate" style="width:100%" data-testid="inv-date"></nz-date-picker></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Due date</nz-form-label><nz-form-control [nzErrorTip]="err('dueDate')" nzExtra="Leave empty to use the customer's payment terms."><nz-date-picker formControlName="dueDate" style="width:100%"></nz-date-picker></nz-form-control></nz-form-item>
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
          @if (editable()) { <button nz-button type="button" (click)="addLine()" data-testid="inv-add-line"><span nz-icon nzType="plus"></span>Add line</button> }
          <div class="totals" data-testid="inv-totals">
            <span>Net <b>{{ totals().net.toFixed(2) }}</b></span><span>Tax <b>{{ totals().tax.toFixed(2) }}</b></span><span>Total <b>{{ (totals().net + totals().tax).toFixed(2) }}</b></span>
          </div>
        </form>
      </ng-container>
    </nz-drawer>
    <ng-template #footer>
      <button nz-button (click)="drawerOpen.set(false)">Close</button>
      @if (invoice(); as inv) {
        @if (inv.id) { <button nz-button (click)="printInvoice(inv.id)" style="margin-inline-start:8px" data-testid="inv-print"><span nz-icon nzType="printer"></span>Print / PDF</button> }
        @if (inv.status === 'issued' && inv.paid === 0) { <button *erpHasPermission="'finance.invoices.issue'" nz-button nzDanger (click)="void()" style="margin-inline-start:8px" data-testid="inv-void">Void</button> }
        @if (inv.isCreditNote && (inv.status === 'issued' || inv.status === 'partlyPaid')) { <button *erpHasPermission="'finance.invoices.issue'" nz-button (click)="applyOpen.set(true)" style="margin-inline-start:8px" data-testid="inv-apply">Apply to invoice</button> }
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
  private readonly api = inject(InvoicesApi);
  private readonly parties = inject(PartiesApi);
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly entityPage = toSignal(inject(LegalEntitiesApi).list({ page: 1, pageSize: 100 }).pipe(catchError(() => of({ items: [] as LegalEntityDto[] }))), { initialValue: { items: [] as LegalEntityDto[] } });
  private readonly taxPage = toSignal(inject(TaxCodesApi).list({ page: 1, pageSize: 100 }).pipe(catchError(() => of({ items: [] as TaxCodeDto[] }))), { initialValue: { items: [] as TaxCodeDto[] } });
  readonly entities = computed(() => this.entityPage().items);
  readonly taxes = computed(() => this.taxPage().items.filter((t) => t.isActive));
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-invoiceDate' });
  readonly columns: TableColumn<InvoiceSummary>[] = [
    { key: 'number', label: 'Number', sortField: 'number', width: '150px' },
    { key: 'partyName', label: 'Customer' },
    { key: 'invoiceDate', label: 'Date', sortField: 'invoiceDate', width: '120px' },
    { key: 'dueDate', label: 'Due', sortField: 'dueDate', width: '120px' },
    { key: 'total', label: 'Total', sortField: 'total', width: '120px' },
    { key: 'outstanding', label: 'Outstanding', width: '120px' },
    { key: 'status', label: 'Status', width: '110px' },
  ];
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
  readonly totals = computed(() => {
    const lines = (this.value().lines ?? []) as { quantity?: number | null; price?: number | null; taxCodeId?: string | null }[];
    const rate = (id?: string | null) => this.taxes().find((t) => t.id === id)?.rate ?? 0;
    const net = lines.reduce((s, l) => s + Math.round((l.quantity ?? 0) * (l.price ?? 0) * 100) / 100, 0);
    const tax = lines.reduce((s, l) => s + Math.round(Math.round((l.quantity ?? 0) * (l.price ?? 0) * 100) / 100 * rate(l.taxCodeId)) / 100, 0);
    return { net, tax };
  });
  readonly editable = computed(() => !this.invoice() || this.invoice()!.status === 'draft');
  readonly title = computed(() => this.invoice() ? `${this.invoice()!.isCreditNote ? 'Credit note' : 'Invoice'} ${this.invoice()!.number}` : 'New invoice');

  get lines(): FormArray { return this.form.controls.lines as FormArray; }
  color(s: string): string { return STATUS_COLORS[s] ?? 'default'; }
  label(s: string): string { return STATUS_LABELS[s] ?? s.charAt(0).toUpperCase() + s.slice(1); }
  err(name: string): string { return controlError(this.form.get(name)); }

  private newLine(l?: Partial<{ description: string; quantity: number; price: number; taxCodeId: string }>) {
    return this.fb.group({ description: [l?.description ?? '', Validators.required], quantity: [l?.quantity ?? 1], price: [l?.price ?? 0], taxCodeId: [l?.taxCodeId ?? null as string | null] });
  }
  addLine(): void { this.lines.push(this.newLine() as never); }
  removeLine(i: number): void { if (this.lines.length > 1) this.lines.removeAt(i); }

  searchParties(term: string): void {
    this.parties.list({ page: 1, pageSize: 20, search: term }).subscribe((r) => this.partyOptions.update((cur) => this.merge(cur, r.items.map((p) => ({ value: p.id, label: `${p.code} · ${p.name}` })))));
  }
  private merge(current: LookupOption[], fresh: LookupOption[]): LookupOption[] {
    const keep = current.filter((c) => c.value === this.form.value.partyId && !fresh.some((f) => f.value === c.value));
    return [...keep, ...fresh];
  }

  printInvoice(id: string): void { window.open(`/print/invoice/${id}`, '_blank'); }

  open(row: InvoiceSummary | null): void {
    if (!row) { this.show(null); return; }
    this.router.navigate(['/finance/invoices', row.id]);
  }

  private show(inv: InvoiceDto | null): void {
    this.invoice.set(inv);
    this.lines.clear();
    for (const l of inv?.lines ?? [{} as never]) this.lines.push(this.newLine(l as never) as never);
    const entities = this.entities();
    this.form.patchValue({
      legalEntityId: inv?.legalEntityId ?? (entities.length === 1 ? entities[0].id : ''), partyId: inv?.partyId ?? '', isCreditNote: inv?.isCreditNote ?? false, notes: inv?.notes ?? '',
      invoiceDate: inv ? new Date(inv.invoiceDate + 'T00:00:00') : new Date(), dueDate: inv ? new Date(inv.dueDate + 'T00:00:00') : null,
    });
    this.partyOptions.set(inv ? [{ value: inv.partyId, label: inv.partyName ?? inv.partyId }] : []);
    if (!inv) this.searchParties('');
    this.editable() ? this.form.enable() : this.form.disable();
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
        this.api.issue(saved.id).subscribe({ next: (i) => this.done(`Issued as ${i.number}`), error: (e) => { this.saving.set(false); this.invoice.set(saved); this.list.reload(); this.fail(e); } });
      },
      error: (e) => { this.saving.set(false); this.fail(e); },
    });
  }

  void(): void {
    const inv = this.invoice();
    if (inv) this.api.void(inv.id).subscribe({ next: () => this.done('Invoice voided'), error: (e) => this.fail(e) });
  }

  searchOpenInvoices(term: string): void {
    const inv = this.invoice();
    if (!inv) return;
    this.api.list({ page: 1, pageSize: 20, search: term, filters: [{ field: 'partyId', op: 'eq', value: inv.partyId }, { field: 'isCreditNote', op: 'eq', value: false }] })
      .pipe(map((r) => r.items.filter((i) => i.status === 'issued' || i.status === 'partlyPaid')))
      .subscribe((items) => this.openInvoices.set(items.map((i) => ({ value: i.id, label: `${i.number} · outstanding ${i.outstanding.toFixed(2)}` }))));
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
  private fail(e: unknown): void { const p = asProblem(e); this.message.error(p?.detail ?? p?.title ?? 'Could not save the invoice'); }
}
