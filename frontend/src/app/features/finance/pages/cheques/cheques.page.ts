import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { Observable } from 'rxjs';
import { CHEQUE_KINDS, ChequeDto, ChequeRow, ChequeSummary, ChequesApi, InvoiceSummary, InvoicesApi } from '@core/api/admin.api';
import { createPagedList } from '@core/api/paged-list';
import { LookupOption } from '@core/custom-fields/lookup-registry';
import { toIsoDate } from '@core/format/dates';
import { controlError, problemMessage } from '@core/http/problem-details';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { RecordHistoryComponent } from '@shared/components/record-history/record-history.component';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { revealErrors } from '@shared/forms/form-validation';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { formatMoney, isOpenStatus } from '../../finance-format';
import { countOf } from '@shared/utils/kpi-stats';
import { FinanceLookups, isBankAccount, mainBankAccount, partyOption } from '../../finance-lookups';
import { allocatedAmounts, openDocumentsQuery } from '../../finance-settlements';

const STATUSES = ['inCustody', 'deposited', 'cleared', 'bounced', 'replaced', 'cancelled'];
const COLORS: Record<string, string> = { inCustody: 'blue', deposited: 'gold', cleared: 'green', bounced: 'red', replaced: 'default', cancelled: 'default', issued: 'cyan' };
const LABELS: Record<string, string> = { inCustody: 'In custody', postDatedCheque: 'Post-dated' };
const ACTION_TITLES = { deposit: 'Deposit cheque', clear: 'Mark cheque cleared', bounce: 'Cheque bounced', followup: 'Log follow-up', null: '' };
type Action = 'deposit' | 'clear' | 'bounce' | 'followup' | null;

/** Cheque and post-dated cheque register for cheques received from customers. */
@Component({
  selector: 'erp-cheques',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DecimalPipe,
    FormsModule,
    ReactiveFormsModule,
    NzButtonModule,
    NzDatePickerModule,
    NzDrawerModule,
    NzFormModule,
    NzIconModule,
    NzInputModule,
    NzInputNumberModule,
    NzModalModule,
    NzSelectModule,
    NzTagModule,
    NzToolTipModule,
    TranslatePipe,
    PagedTableComponent,
    RecordHistoryComponent,
    StatCardComponent,
    HasPermissionDirective,
    ErpDatePipe,
  ],
  providers: [FinanceLookups],
  styleUrl: './cheques.page.scss',
  templateUrl: './cheques.page.html',
})
export class ChequesPage {
  private readonly api = inject(ChequesApi);
  private readonly invoices = inject(InvoicesApi);
  private readonly message = inject(NzMessageService);
  private readonly fb = inject(FormBuilder);
  private readonly modal = inject(NzModalService);
  private readonly lookups = inject(FinanceLookups).load({ entities: true, accounts: true });

  readonly currency = inject(WorkspaceCurrencyService).currency;
  readonly chequeCount = (n: number) => countOf(n, 'cheque');
  readonly entities = this.lookups.entities;
  readonly banks = computed(() => this.lookups.accounts().filter(isBankAccount));
  readonly kinds = CHEQUE_KINDS;
  readonly statuses = STATUSES;
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'instrumentDate' });
  readonly summary = signal<ChequeSummary | null>(null);
  readonly columns: TableColumn<ChequeRow>[] = [
    { key: 'number', label: 'Number', sortField: 'number', width: '140px' },
    { key: 'instrumentNo', label: 'Cheque no.', width: '120px' },
    { key: 'partyName', label: 'Customer' },
    { key: 'bankName', label: 'Bank' },
    { key: 'instrumentDate', label: 'Cheque date', sortField: 'instrumentDate', width: '120px' },
    { key: 'amount', label: 'Amount', sortField: 'amount', width: '120px' },
    { key: 'status', label: 'Status', width: '110px', filterField: 'status', filterOptions: STATUSES },
  ];
  readonly formOpen = signal(false);
  readonly detailOpen = signal(false);
  readonly saving = signal(false);
  readonly cheque = signal<ChequeDto | null>(null);
  readonly replacing = signal<ChequeDto | null>(null);
  readonly partyOptions = signal<LookupOption[]>([]);
  readonly openInvoices = signal<InvoiceSummary[]>([]);
  readonly amounts = signal<Record<string, number>>({});
  readonly action = signal<Action>(null);
  readonly actionTitle = computed(() => ACTION_TITLES[String(this.action()) as 'deposit']);
  actionDate: Date | null = new Date();
  actionBank: string | null = null;
  actionReason = '';
  readonly form = this.fb.group({
    legalEntityId: ['', Validators.required], partyId: ['', Validators.required], kind: ['postDatedCheque', Validators.required], instrumentNo: ['', Validators.required], bankName: [''],
    amount: [null as number | null, [Validators.required, Validators.min(0.01)]], instrumentDate: [null as Date | null, Validators.required], receivedOn: [null as Date | null], notes: [''],
  });

  constructor() { this.loadSummary(); }

  money(v: number, currency?: string | null): string { return formatMoney(v, currency ?? this.currency()); }
  color(s: string): string { return COLORS[s] ?? 'default'; }
  label(s: string): string { return LABELS[s] ?? s.charAt(0).toUpperCase() + s.slice(1); }
  err(name: string): string { return controlError(this.form.get(name)); }
  setAmount(id: string, v: number | null): void { this.amounts.update((a) => ({ ...a, [id]: v ?? 0 })); }
  filterStatus(s: string | null): void { this.list.setFilters(s ? [{ field: 'status', op: 'eq', value: s }] : []); }

  searchParties(term: string): void {
    this.lookups.findParties(term).subscribe((found) => this.partyOptions.set(found.map(partyOption)));
  }

  partyChanged(partyId: string | null): void {
    this.amounts.set({}); this.openInvoices.set([]);
    if (!partyId) return;
    this.invoices.list(openDocumentsQuery(partyId, [{ field: 'isCreditNote', op: 'eq', value: false }]))
      .subscribe((r) => this.openInvoices.set(r.items.filter((i) => isOpenStatus(i.status))));
  }

  open(row: ChequeRow): void { this.api.detail(row.id).subscribe((c) => { this.cheque.set(c); this.detailOpen.set(true); }); }

  /** The register form: a new cheque, or the replacement of `replace` (same customer, amount and bank). */
  openNew(replace: ChequeDto | null): void {
    this.replacing.set(replace);
    this.detailOpen.set(false);
    this.form.reset({
      legalEntityId: replace?.legalEntityId ?? this.lookups.onlyEntityId(), partyId: replace?.partyId ?? '', kind: 'cheque', instrumentNo: '', bankName: replace?.bankName ?? '',
      amount: replace?.amount ?? null, instrumentDate: new Date(), receivedOn: new Date(), notes: '',
    });
    this.partyOptions.set(replace?.partyId ? [{ value: replace.partyId, label: replace.partyName ?? replace.partyId }] : []);
    if (replace) this.form.controls.partyId.disable(); else { this.form.controls.partyId.enable(); this.searchParties(''); }
    this.amounts.set({}); this.openInvoices.set([]);
    if (replace?.partyId) this.partyChanged(replace.partyId);
    this.formOpen.set(true);
  }

  register(): void {
    if (this.form.invalid) { revealErrors(this.form, { touchAll: true }); return; }
    const v = this.form.getRawValue();
    const body = {
      ...v, instrumentDate: toIsoDate(v.instrumentDate!), receivedOn: v.receivedOn ? toIsoDate(v.receivedOn) : null, bankName: v.bankName || null, notes: v.notes || null,
      allocations: allocatedAmounts(this.amounts()).map(([invoiceId, amount]) => ({ invoiceId, amount })),
    };
    const replaced = this.replacing();
    this.saving.set(true);
    (replaced ? this.api.replace(replaced.id, body) : this.api.register(body)).subscribe({ next: () => this.done(replaced ? `Replacement cheque ${v.instrumentNo} registered` : `Cheque ${v.instrumentNo} registered`), error: (e) => this.fail(e) });
  }

  act(a: Action): void {
    this.action.set(a); this.actionDate = new Date(); this.actionReason = '';
    this.actionBank = mainBankAccount(this.banks())?.id ?? null;
  }

  runAction(): void {
    const c = this.cheque(); const a = this.action();
    if (!c || !a || !this.actionDate) return;
    const call = this.actionCall(c, a, toIsoDate(this.actionDate));
    if (!call) { this.message.warning(a === 'deposit' ? 'Choose a bank account' : a === 'followup' ? 'Enter the follow-up note' : 'Enter the reason'); return; }
    this.saving.set(true);
    call.subscribe({ next: () => { this.action.set(null); this.done(a === 'followup' ? `Follow-up logged for cheque ${c.instrumentNo}` : `Cheque ${c.instrumentNo} ${a === 'deposit' ? 'deposited' : a === 'clear' ? 'cleared' : 'marked as bounced'}`); }, error: (e) => this.fail(e) });
  }

  cancel(): void {
    const c = this.cheque();
    if (!c) return;
    this.modal.confirm({
      nzTitle: 'Cancel this cheque entry?', nzContent: 'It stays in the register as cancelled and cannot be deposited.', nzOkText: 'Cancel entry', nzOkDanger: true, nzCancelText: 'Keep it',
      nzOnOk: () => this.api.cancel(c.id).subscribe({ next: () => this.done(`Cheque ${c.instrumentNo} cancelled`), error: (e) => this.fail(e) }),
    });
  }

  /** The request for an action, or null while what it needs (bank account, note, reason) is missing. */
  private actionCall(c: ChequeDto, a: Exclude<Action, null>, date: string): Observable<ChequeDto> | null {
    const reason = this.actionReason.trim();
    switch (a) {
      case 'deposit': return this.actionBank ? this.api.deposit(c.id, this.actionBank, date) : null;
      case 'clear': return this.api.clear(c.id, date);
      case 'followup': return reason ? this.api.followUp(c.id, reason, date) : null;
      default: return reason ? this.api.bounce(c.id, date, reason) : null;
    }
  }

  private loadSummary(): void { this.api.summary().subscribe((s) => this.summary.set(s)); }
  private done(text: string): void { this.saving.set(false); this.formOpen.set(false); this.detailOpen.set(false); this.message.success(text); this.list.reload(); this.loadSummary(); }
  private fail(e: unknown): void { this.saving.set(false); this.message.error(problemMessage(e, 'Could not complete the action')); }
}
