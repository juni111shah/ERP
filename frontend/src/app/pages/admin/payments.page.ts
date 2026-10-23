import { ActivatedRoute, Router } from '@angular/router';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { askReason } from '../../shared/reason-prompt';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { catchError, of } from 'rxjs';
import { AccountDto, AccountsApi, BillSummary, BillsApi, LegalEntitiesApi, LegalEntityDto, PaymentDto, PaymentSummary, PaymentsApi, PAYMENT_METHODS } from '../../core/api/admin.api';
import { createPagedList } from '../../core/api/paged-list';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { LookupOption } from '../../core/custom-fields/lookup-registry';
import { asProblem, controlError, problemMessage } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { PartiesApi } from '../parties.page';

/** Vendor payments: pay vendors by bank, cash or cheque, settle open bills at the same time (or later), clear issued cheques, and void. */
@Component({
  selector: 'erp-payments',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, FormsModule, NzModalModule, PagedTableComponent, HasPermissionDirective, NzButtonModule, NzDatePickerModule, NzDrawerModule, NzFormModule, NzIconModule, NzInputModule,
    NzInputNumberModule, NzSelectModule, NzTagModule],
  styles: [`.alloc { display:grid; grid-template-columns: 2fr 1fr; gap:8px; margin-bottom:8px; align-items:center }`],
  template: `
    <h1 class="erp-page-title">Payments</h1>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search number, vendor or reference" emptyText="No payments yet" (rowClick)="open($event)">
      <ng-template #cell let-row let-column="column" let-value="value">
        @if (column.key === 'status') { <nz-tag [nzColor]="row.status === 'voided' ? 'red' : 'green'">{{ row.status === 'voided' ? 'Voided' : 'Posted' }}</nz-tag> } @else { {{ value }} }
      </ng-template>
      <button toolbar *erpHasPermission="'finance.payments.write'" nz-button nzType="primary" (click)="open(null)" data-testid="payment-new"><span nz-icon nzType="plus"></span>New payment</button>
    </erp-paged-table>

    <nz-drawer [nzVisible]="drawerOpen()" [nzWidth]="640" [nzTitle]="receipt() ? receipt()!.number : 'New payment'" (nzOnClose)="drawerOpen.set(false)" [nzFooter]="footer">
      <ng-container *nzDrawerContent>
        <form nz-form nzLayout="vertical" [formGroup]="form" data-testid="payment-form">
          <nz-form-item><nz-form-label nzRequired>Legal entity</nz-form-label><nz-form-control [nzErrorTip]="err('legalEntityId')">
            <nz-select formControlName="legalEntityId" data-testid="pm-entity">@for (e of entities(); track e.id) { <nz-option [nzValue]="e.id" [nzLabel]="e.name"></nz-option> }</nz-select></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Vendor</nz-form-label><nz-form-control [nzErrorTip]="err('partyId')">
            <nz-select formControlName="partyId" nzShowSearch nzServerSearch (nzOnSearch)="searchParties($event)" (ngModelChange)="partyChanged($event)" data-testid="pm-party">@for (o of partyOptions(); track o.value) { <nz-option [nzValue]="o.value" [nzLabel]="o.label"></nz-option> }</nz-select></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Date</nz-form-label><nz-form-control [nzErrorTip]="err('date')"><nz-date-picker formControlName="date" style="width:100%" data-testid="pm-date"></nz-date-picker></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Method</nz-form-label><nz-form-control><nz-select formControlName="method">@for (m of methods; track m.value) { <nz-option [nzValue]="m.value" [nzLabel]="m.label"></nz-option> }</nz-select></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Pay from</nz-form-label><nz-form-control [nzErrorTip]="err('bankAccountId')">
            <nz-select formControlName="bankAccountId" data-testid="pm-bank">@for (a of banks(); track a.id) { <nz-option [nzValue]="a.id" [nzLabel]="a.code + ' · ' + a.name"></nz-option> }</nz-select></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Amount</nz-form-label><nz-form-control [nzErrorTip]="err('amount')"><nz-input-number formControlName="amount" [nzMin]="0.01" [nzPrecision]="2" style="width:100%" data-testid="pm-amount"></nz-input-number></nz-form-control></nz-form-item>
          @if (isCheque()) {
            <nz-form-item><nz-form-label nzRequired>Cheque number</nz-form-label><nz-form-control><input nz-input formControlName="chequeNo" data-testid="pm-chequeno" /></nz-form-control></nz-form-item>
            <nz-form-item><nz-form-label>Cheque date</nz-form-label><nz-form-control nzExtra="A later date makes it post-dated."><nz-date-picker formControlName="chequeDate" style="width:100%"></nz-date-picker></nz-form-control></nz-form-item>
            <nz-form-item><nz-form-label>Bank</nz-form-label><nz-form-control><input nz-input formControlName="bankName" /></nz-form-control></nz-form-item>
          }
          <nz-form-item><nz-form-label>Reference</nz-form-label><nz-form-control><input nz-input formControlName="reference" /></nz-form-control></nz-form-item>
        </form>
        @if (receipt(); as r) {
          <h4>Settled bills</h4>
          @for (a of r.allocations; track a.id) { <div>{{ a.billNumber }} — {{ a.amount.toFixed(2) }}</div> } @empty { <div>None yet.</div> }
          <div style="margin-top:8px">Unallocated: <b>{{ r.unallocated.toFixed(2) }}</b></div>
        }
        @if (editableAllocation()) {
          <h4 style="margin-top:16px">Settle bills</h4>
          @for (o of openInvoices(); track o.id) {
            <div class="alloc">
              <span>{{ o.number }} · due {{ o.dueDate }} · outstanding {{ o.outstanding.toFixed(2) }}</span>
              <nz-input-number [ngModel]="amounts()[o.id] ?? 0" (ngModelChange)="setAmount(o.id, $event)" [nzMin]="0" [nzMax]="o.outstanding" [nzPrecision]="2" [ngModelOptions]="{standalone: true}" [attr.data-testid]="'alloc-' + o.number"></nz-input-number>
            </div>
          } @empty { <div>No open bills for this vendor.</div> }
        }
      </ng-container>
    </nz-drawer>
    <ng-template #footer>
      <button nz-button (click)="drawerOpen.set(false)">Close</button>
      @if (receipt()?.id) { <button nz-button (click)="openDetails(receipt()!.id)" style="margin-inline-start:8px" data-testid="payment-details"><span nz-icon nzType="file-text"></span>Details</button><button nz-button (click)="printDoc(receipt()!.id)" style="margin-inline-start:8px" data-testid="payment-print"><span nz-icon nzType="printer"></span>Print / PDF</button> }
      @if (receipt()?.status === 'issued') {
        @if (receipt()!.paymentInstrumentId) { <button *erpHasPermission="'finance.cheques.approve'" nz-button (click)="clearCheque()" style="margin-inline-start:8px" data-testid="pm-clear">Cheque cleared</button> }
        <button *erpHasPermission="'finance.payments.write'" nz-button nzDanger (click)="void()" style="margin-inline-start:8px" data-testid="pm-void">Void</button>
        @if (receipt()!.unallocated > 0) { <button *erpHasPermission="'finance.payments.write'" nz-button nzType="primary" [nzLoading]="saving()" (click)="allocate()" style="margin-inline-start:8px" data-testid="pm-allocate">Allocate</button> }
      }
      @if (!receipt()) { <button *erpHasPermission="'finance.payments.write'" nz-button nzType="primary" [nzLoading]="saving()" (click)="create()" style="margin-inline-start:8px" data-testid="pm-save">Record payment</button> }
    </ng-template>`,
})
export class PaymentsPage {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(PaymentsApi);
  private readonly router = inject(Router);
  private readonly modal = inject(NzModalService);
  private readonly invoices = inject(BillsApi);
  private readonly parties = inject(PartiesApi);
  private readonly message = inject(NzMessageService);
  private readonly fb = inject(FormBuilder);
  private readonly entityPage = toSignal(inject(LegalEntitiesApi).list({ page: 1, pageSize: 100 }).pipe(catchError(() => of({ items: [] as LegalEntityDto[] }))), { initialValue: { items: [] as LegalEntityDto[] } });
  private readonly accountPage = toSignal(inject(AccountsApi).list({ page: 1, pageSize: 200, sort: 'code' }).pipe(catchError(() => of({ items: [] as AccountDto[] }))), { initialValue: { items: [] as AccountDto[] } });
  readonly entities = computed(() => this.entityPage().items);
  readonly banks = computed(() => this.accountPage().items.filter((a) => a.type === 'asset' && a.isPostable && a.isActive && (a.isBank || a.systemRole === 'cash' || a.systemRole === 'bank')));
  readonly methods = PAYMENT_METHODS;
  readonly list = createPagedList((q) => this.api.list(q), { sort: '-date' });
  readonly columns: TableColumn<PaymentSummary>[] = [
    { key: 'number', label: 'Number', sortField: 'number', width: '150px' },
    { key: 'partyName', label: 'Customer' },
    { key: 'date', label: 'Date', sortField: 'date', width: '120px', filterField: 'date' },
    { key: 'amount', label: 'Amount', sortField: 'amount', width: '120px' },
    { key: 'unallocated', label: 'Unallocated', width: '120px' },
    { key: 'status', label: 'Status', width: '100px', filterField: 'status', filterOptions: [{ value: 'issued', label: 'Posted' }, { value: 'voided', label: 'Voided' }] },
  ];
  readonly drawerOpen = signal(false);
  readonly saving = signal(false);
  readonly receipt = signal<PaymentDto | null>(null);
  readonly partyOptions = signal<LookupOption[]>([]);
  readonly openInvoices = signal<BillSummary[]>([]);
  readonly amounts = signal<Record<string, number>>({});
  readonly editableAllocation = computed(() => !this.receipt() || (this.receipt()!.status === 'issued' && this.receipt()!.unallocated > 0));
  readonly form = this.fb.group({
    legalEntityId: ['', Validators.required], partyId: ['', Validators.required], date: [null as Date | null, Validators.required], method: ['bankTransfer', Validators.required],
    bankAccountId: ['', Validators.required], amount: [null as number | null, [Validators.required, Validators.min(0.01)]], reference: [''],
    chequeNo: [''], chequeDate: [null as Date | null], bankName: [''],
  });
  private readonly method = toSignal(this.form.controls.method.valueChanges, { initialValue: 'bankTransfer' });
  readonly isCheque = computed(() => this.method() === 'instrument');

  err(name: string): string { return controlError(this.form.get(name)); }
  setAmount(id: string, v: number | null): void { this.amounts.update((a) => ({ ...a, [id]: v ?? 0 })); }

  searchParties(term: string): void {
    this.parties.list({ page: 1, pageSize: 20, search: term }).subscribe((r) => this.partyOptions.set(r.items.map((p) => ({ value: p.id, label: `${p.code} · ${p.name}` }))));
  }

  partyChanged(partyId: string | null): void {
    this.amounts.set({}); this.openInvoices.set([]);
    if (!partyId) return;
    this.loadOpen(partyId);
  }

  private loadOpen(partyId: string, prefillBillId?: string | null): void {
    this.invoices.list({ page: 1, pageSize: 100, sort: 'dueDate', filters: [{ field: 'partyId', op: 'eq', value: partyId }] })
      .subscribe((r) => {
        this.openInvoices.set(r.items.filter((i) => i.status === 'issued' || i.status === 'partlyPaid'));
        const bill = prefillBillId ? this.openInvoices().find((i) => i.id === prefillBillId) : undefined;
        if (bill) { this.setAmount(bill.id, bill.outstanding); this.form.patchValue({ amount: bill.outstanding }); }
      });
  }

  /** `?open=<id>` opens that record's drawer (used by the detail page's Edit/Allocate actions). */
  ngOnInit(): void {
    const id = this.route.snapshot.queryParamMap.get('open');
    if (id) this.open({ id } as never);
    const partyId = this.route.snapshot.queryParamMap.get('partyId');
    if (!id && partyId) {
      this.open(null);
      this.form.patchValue({ partyId });
      this.parties.get(partyId).subscribe((p) => this.partyOptions.update((o) => [{ value: p.id, label: `${p.code} · ${p.name}` }, ...o.filter((x) => x.value !== p.id)]));
      this.loadOpen(partyId, this.route.snapshot.queryParamMap.get('billId'));
    }
  }

  openDetails(id: string): void { void this.router.navigate(['/finance/payments', id]); }
  printDoc(id: string): void { window.open(`/print/payment/${id}`, '_blank'); }

  open(row: PaymentSummary | null): void {
    this.amounts.set({}); this.openInvoices.set([]);
    if (!row) {
      this.receipt.set(null);
      const entities = this.entities();
      this.form.reset({ legalEntityId: entities.length === 1 ? entities[0].id : '', partyId: '', date: new Date(), method: 'bankTransfer', bankAccountId: this.banks().find((b) => b.systemRole === 'bank')?.id ?? '', amount: null, reference: '', chequeNo: '', chequeDate: null, bankName: '' });
      this.form.enable();
      this.searchParties('');
      this.drawerOpen.set(true);
      return;
    }
    this.api.detail(row.id).subscribe((r) => {
      this.receipt.set(r);
      this.partyOptions.set([{ value: r.partyId, label: r.partyName ?? r.partyId }]);
      this.form.reset({ legalEntityId: r.legalEntityId, partyId: r.partyId, date: new Date(r.date + 'T00:00:00'), method: r.method, bankAccountId: r.bankAccountId ?? '', amount: r.amount, reference: r.reference ?? '', chequeNo: '', chequeDate: null, bankName: '' });
      this.form.disable();
      if (r.status === 'issued' && r.unallocated > 0) this.loadOpen(r.partyId);
      this.drawerOpen.set(true);
    });
  }

  private allocations(): { billId: string; amount: number }[] {
    return Object.entries(this.amounts()).filter(([, a]) => a > 0).map(([billId, amount]) => ({ billId, amount }));
  }

  create(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); Object.values(this.form.controls).forEach((c) => { c.markAsDirty(); c.updateValueAndValidity(); }); return; }
    const v = this.form.getRawValue();
    this.saving.set(true);
    this.api.create({ ...v, date: toIsoDate(v.date!), reference: v.reference || null, chequeNo: v.method === 'instrument' ? v.chequeNo : null, chequeDate: v.method === 'instrument' && v.chequeDate ? toIsoDate(v.chequeDate) : null, bankName: v.bankName || null, allocations: this.allocations() }).subscribe({
      next: (r) => this.done(`Paid ${(r as PaymentDto).number}`),
      error: (e) => this.fail(e),
    });
  }

  allocate(): void {
    const r = this.receipt();
    const allocations = this.allocations();
    if (!r || allocations.length === 0) { this.message.warning('Enter an amount against at least one invoice'); return; }
    this.saving.set(true);
    this.api.allocate(r.id, allocations).subscribe({ next: () => this.done('Payment allocated'), error: (e) => this.fail(e) });
  }

  clearCheque(): void {
    const r = this.receipt();
    if (r?.paymentInstrumentId) this.api.clearCheque(r.paymentInstrumentId, toIsoDate(new Date())).subscribe({ next: () => this.done('Cheque marked as cleared'), error: (e) => this.fail(e) });
  }

  void(): void {
    const r = this.receipt();
    if (!r) return;
    // Same required-reason prompt as the detail page; the void endpoint takes no body, so the reason is not sent.
    void askReason(this.modal, 'Void this payment?', 'Void Payment', 'This reverses its journal and removes its allocations.').then((reason) => {
      if (reason) this.api.void(r.id, reason).subscribe({ next: () => this.done('Payment voided'), error: (e) => this.fail(e) });
    });
  }

  private done(text: string): void { this.saving.set(false); this.drawerOpen.set(false); this.message.success(text); this.list.reload(); }
  private fail(e: unknown): void { this.saving.set(false); this.message.error(problemMessage(e, 'Could not save the payment', 'reason')); }
}
