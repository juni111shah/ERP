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
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { catchError, of } from 'rxjs';
import {
  AccountDto, AccountsApi, CHEQUE_KINDS, ChequeDto, ChequeRow, ChequeSummary, ChequesApi, InvoiceSummary, InvoicesApi, LegalEntitiesApi, LegalEntityDto,
} from '@core/api/admin.api';
import { createPagedList } from '@core/api/paged-list';
import { toIsoDate } from '@core/custom-fields/custom-field-form';
import { LookupOption } from '@core/custom-fields/lookup-registry';
import { asProblem, problemMessage, controlError } from '@core/http/problem-details';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { PartiesApi } from '@features/parties/pages/parties/parties.page';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { RevealDirective } from '@shared/directives/reveal.directive';
import { RecordHistoryComponent } from '@shared/components/record-history/record-history.component';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';

import { DecimalPipe } from '@angular/common';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { formatMoney } from '../../components/invoice-sheet/invoice-sheet.component';
import { countOf } from '../../finance-kpis';

const COLORS: Record<string, string> = { inCustody: 'blue', deposited: 'gold', cleared: 'green', bounced: 'red', replaced: 'default', cancelled: 'default', issued: 'cyan' };
const LABELS: Record<string, string> = { inCustody: 'In custody', postDatedCheque: 'Post-dated' };
type Action = 'deposit' | 'clear' | 'bounce' | 'followup' | null;

/** Cheque and post-dated cheque register for cheques received from customers. */
@Component({
  selector: 'erp-cheques',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, DecimalPipe, RecordHistoryComponent, StatCardComponent, NzToolTipModule, RevealDirective, TranslatePipe, ReactiveFormsModule, FormsModule, PagedTableComponent, HasPermissionDirective, NzButtonModule, NzDatePickerModule, NzDrawerModule, NzFormModule, NzIconModule, NzInputModule,
    NzInputNumberModule, NzModalModule, NzSelectModule, NzTagModule],
  styleUrl: './cheques.page.scss',
  templateUrl: './cheques.page.html',
})
export class ChequesPage {
  private readonly currencyService = inject(WorkspaceCurrencyService);
  readonly currency = this.currencyService.currency;
  readonly chequeCount = (n: number) => countOf(n, 'cheque');
  money(v: number, currency?: string | null): string { return formatMoney(v, currency ?? this.currency()); }
  private readonly api = inject(ChequesApi);
  private readonly invoices = inject(InvoicesApi);
  private readonly parties = inject(PartiesApi);
  private readonly message = inject(NzMessageService);
  private readonly fb = inject(FormBuilder);
  private readonly modal = inject(NzModalService);
  private readonly entityPage = toSignal(inject(LegalEntitiesApi).list({ page: 1, pageSize: 100 }).pipe(catchError(() => of({ items: [] as LegalEntityDto[] }))), { initialValue: { items: [] as LegalEntityDto[] } });
  private readonly accountPage = toSignal(inject(AccountsApi).list({ page: 1, pageSize: 200, sort: 'code' }).pipe(catchError(() => of({ items: [] as AccountDto[] }))), { initialValue: { items: [] as AccountDto[] } });
  readonly entities = computed(() => this.entityPage().items);
  readonly banks = computed(() => this.accountPage().items.filter((a) => a.type === 'asset' && a.isPostable && a.isActive && (a.isBank || a.systemRole === 'bank')));
  readonly kinds = CHEQUE_KINDS;
  readonly statuses = ['inCustody', 'deposited', 'cleared', 'bounced', 'replaced', 'cancelled'];
  readonly list = createPagedList((q) => this.api.list(q), { sort: 'instrumentDate' });
  readonly summary = signal<ChequeSummary | null>(null);
  readonly columns: TableColumn<ChequeRow>[] = [
    { key: 'number', label: 'Number', sortField: 'number', width: '140px' },
    { key: 'instrumentNo', label: 'Cheque no.', width: '120px' },
    { key: 'partyName', label: 'Customer' },
    { key: 'bankName', label: 'Bank' },
    { key: 'instrumentDate', label: 'Cheque date', sortField: 'instrumentDate', width: '120px' },
    { key: 'amount', label: 'Amount', sortField: 'amount', width: '120px' },
    { key: 'status', label: 'Status', width: '110px', filterField: 'status', filterOptions: ['inCustody', 'deposited', 'cleared', 'bounced', 'replaced', 'cancelled'] },
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
  readonly actionTitle = computed(() => ({ deposit: 'Deposit cheque', clear: 'Mark cheque cleared', bounce: 'Cheque bounced', followup: 'Log follow-up', null: '' })[String(this.action()) as 'deposit']);
  actionDate: Date | null = new Date();
  actionBank: string | null = null;
  actionReason = '';
  readonly form = this.fb.group({
    legalEntityId: ['', Validators.required], partyId: ['', Validators.required], kind: ['postDatedCheque', Validators.required], instrumentNo: ['', Validators.required], bankName: [''],
    amount: [null as number | null, [Validators.required, Validators.min(0.01)]], instrumentDate: [null as Date | null, Validators.required], receivedOn: [null as Date | null], notes: [''],
  });

  constructor() { this.loadSummary(); }

  color(s: string): string { return COLORS[s] ?? 'default'; }
  label(s: string): string { return LABELS[s] ?? s.charAt(0).toUpperCase() + s.slice(1); }
  err(name: string): string { return controlError(this.form.get(name)); }
  setAmount(id: string, v: number | null): void { this.amounts.update((a) => ({ ...a, [id]: v ?? 0 })); }
  filterStatus(s: string | null): void { this.list.setFilters(s ? [{ field: 'status', op: 'eq', value: s }] : []); }
  private loadSummary(): void { this.api.summary().subscribe((s) => this.summary.set(s)); }

  searchParties(term: string): void {
    this.parties.list({ page: 1, pageSize: 20, search: term }).subscribe((r) => this.partyOptions.set(r.items.map((p) => ({ value: p.id, label: `${p.code} · ${p.name}` }))));
  }

  partyChanged(partyId: string | null): void {
    this.amounts.set({}); this.openInvoices.set([]);
    if (!partyId) return;
    this.invoices.list({ page: 1, pageSize: 100, sort: 'dueDate', filters: [{ field: 'partyId', op: 'eq', value: partyId }, { field: 'isCreditNote', op: 'eq', value: false }] })
      .subscribe((r) => this.openInvoices.set(r.items.filter((i) => i.status === 'issued' || i.status === 'partlyPaid')));
  }

  open(row: ChequeRow): void { this.api.detail(row.id).subscribe((c) => { this.cheque.set(c); this.detailOpen.set(true); }); }

  openNew(replace: ChequeDto | null): void {
    this.replacing.set(replace);
    this.detailOpen.set(false);
    const entities = this.entities();
    this.form.reset({
      legalEntityId: replace?.legalEntityId ?? (entities.length === 1 ? entities[0].id : ''), partyId: replace?.partyId ?? '', kind: 'cheque', instrumentNo: '', bankName: replace?.bankName ?? '',
      amount: replace?.amount ?? null, instrumentDate: new Date(), receivedOn: new Date(), notes: '',
    });
    this.partyOptions.set(replace?.partyId ? [{ value: replace.partyId, label: replace.partyName ?? replace.partyId }] : []);
    if (replace) this.form.controls.partyId.disable(); else { this.form.controls.partyId.enable(); this.searchParties(''); }
    this.amounts.set({}); this.openInvoices.set([]);
    if (replace?.partyId) this.partyChanged(replace.partyId);
    this.formOpen.set(true);
  }

  register(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); Object.values(this.form.controls).forEach((c) => { c.markAsDirty(); c.updateValueAndValidity(); }); return; }
    const v = this.form.getRawValue();
    const body = {
      ...v, instrumentDate: toIsoDate(v.instrumentDate!), receivedOn: v.receivedOn ? toIsoDate(v.receivedOn) : null, bankName: v.bankName || null, notes: v.notes || null,
      allocations: Object.entries(this.amounts()).filter(([, a]) => a > 0).map(([invoiceId, amount]) => ({ invoiceId, amount })),
    };
    const replaced = this.replacing();
    this.saving.set(true);
    (replaced ? this.api.replace(replaced.id, body) : this.api.register(body)).subscribe({ next: () => this.done(replaced ? `Replacement cheque ${v.instrumentNo} registered` : `Cheque ${v.instrumentNo} registered`), error: (e) => this.fail(e) });
  }

  act(a: Action): void {
    this.action.set(a); this.actionDate = new Date(); this.actionReason = '';
    this.actionBank = this.banks().find((b) => b.systemRole === 'bank')?.id ?? null;
  }

  runAction(): void {
    const c = this.cheque(); const a = this.action();
    if (!c || !a || !this.actionDate) return;
    const date = toIsoDate(this.actionDate);
    const call = a === 'deposit' ? (this.actionBank ? this.api.deposit(c.id, this.actionBank, date) : null)
      : a === 'clear' ? this.api.clear(c.id, date)
      : a === 'followup' ? (this.actionReason.trim() ? this.api.followUp(c.id, this.actionReason.trim(), date) : null)
      : (this.actionReason.trim() ? this.api.bounce(c.id, date, this.actionReason.trim()) : null);
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

  private done(text: string): void { this.saving.set(false); this.formOpen.set(false); this.detailOpen.set(false); this.message.success(text); this.list.reload(); this.loadSummary(); }
  private fail(e: unknown): void { this.saving.set(false); this.message.error(problemMessage(e, 'Could not complete the action')); }
}
