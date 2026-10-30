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
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { catchError, of } from 'rxjs';
import {
  AccountDto, AccountsApi, CHEQUE_KINDS, ChequeDto, ChequeRow, ChequeSummary, ChequesApi, InvoiceSummary, InvoicesApi, LegalEntitiesApi, LegalEntityDto,
} from '../../core/api/admin.api';
import { createPagedList } from '../../core/api/paged-list';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { LookupOption } from '../../core/custom-fields/lookup-registry';
import { asProblem, controlError } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';
import { PartiesApi } from '../parties.page';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { RevealDirective } from '../../shared/reveal.directive';
import { RecordHistoryComponent } from '../../shared/record-history.component';
import { StatCardComponent } from '../../shared/stat-card.component';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';

import { DecimalPipe } from '@angular/common';

const COLORS: Record<string, string> = { inCustody: 'blue', deposited: 'gold', cleared: 'green', bounced: 'red', replaced: 'default', cancelled: 'default', issued: 'cyan' };
const LABELS: Record<string, string> = { inCustody: 'In custody', postDatedCheque: 'Post-dated' };
type Action = 'deposit' | 'clear' | 'bounce' | 'followup' | null;

/** Cheque and post-dated cheque register for cheques received from customers. */
@Component({
  selector: 'erp-cheques',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, RecordHistoryComponent, StatCardComponent, NzToolTipModule, RevealDirective, TranslatePipe, ReactiveFormsModule, FormsModule, PagedTableComponent, HasPermissionDirective, NzButtonModule, NzDatePickerModule, NzDrawerModule, NzFormModule, NzIconModule, NzInputModule,
    NzInputNumberModule, NzModalModule, NzSelectModule, NzTagModule],
  styles: [`.alloc { display:grid; grid-template-columns: 2fr 1fr; gap:8px; margin-bottom:8px; align-items:center }`],
  template: `
    <h1 class="erp-page-title">Cheque register</h1>
    <div class="erp-stat-grid erp-stat-grid--auto" data-testid="cheque-kpis">
      @let s = summary();
      <erp-stat-card label="In custody" [loading]="!s" [value]="s ? s.inCustodyTotal.toFixed(2) : null" [caption]="s ? s.inCustodyCount + ' ' + ('ui.cheque.chequesLc' | translate: 'cheques') : null" [tone]="0" />
      <erp-stat-card label="Matured, to deposit" [loading]="!s" [value]="s ? s.maturedTotal.toFixed(2) : null" [caption]="s ? s.maturedCount + ' ' + ('ui.cheque.chequesLc' | translate: 'cheques') : null" [tone]="s && s.maturedCount > 0 ? 'warning' : 1" />
      <erp-stat-card label="Next 7 days" [loading]="!s" [value]="s ? s.next7DaysTotal.toFixed(2) : null" [tone]="2" />
      <erp-stat-card label="Next 30 days" [loading]="!s" [value]="s ? s.next30DaysTotal.toFixed(2) : null" [tone]="5" />
      <erp-stat-card label="Awaiting clearing" [loading]="!s" [value]="s ? s.depositedTotal.toFixed(2) : null" [tone]="4" />
      <erp-stat-card label="Bounced" [loading]="!s" [value]="s ? s.bouncedCount : null" format="number" [tone]="s && s.bouncedCount > 0 ? 'danger' : 'neutral'" />
    </div>
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search number, cheque no., customer or bank" emptyText="No cheques yet" (rowClick)="open($event)">
      <ng-template #cell let-row let-column="column" let-value="value">
        @switch (column.key) {
          @case ('number') {
            <span class="code-pill">{{ row.number }}</span>
          }
          @case ('instrumentNo') {
            <span class="code-pill">{{ row.instrumentNo }}</span>
          }
          @case ('partyName') {
            <span class="name-cell">{{ row.partyName }}</span>
          }
          @case ('amount') {
            <b>{{ currency() }} {{ row.amount | number: '1.2-2' }}</b>
          }
          @case ('status') {
            <nz-tag [nzColor]="color(row.status)">{{ label(row.status) }}</nz-tag>
            @if (row.status === 'bounced' && !row.followUpOn) { <nz-tag nzColor="orange" data-testid="ch-followup-due">Follow-up due</nz-tag> }
            @if (row.status === 'bounced' && row.followUpOn) { <nz-tag nzColor="blue">Followed up</nz-tag> }
          }
          @default {
            {{ value }}
          }
        }
      </ng-template>
      <nz-select toolbar style="width:160px" nzAllowClear nzPlaceHolder="All statuses" [ngModel]="null" [ngModelOptions]="{standalone: true}" (ngModelChange)="filterStatus($event)" data-testid="cheque-status-filter">
        @for (s of statuses; track s) { <nz-option [nzValue]="s" [nzLabel]="label(s)"></nz-option> }
      </nz-select>
      <button toolbar *erpHasPermission="'finance.cheques.write'" nz-button nzType="primary" (click)="openNew(null)" data-testid="cheque-new"><span nz-icon nzType="plus"></span>Register cheque</button>
    </erp-paged-table>

    <nz-drawer [nzVisible]="formOpen()" [nzWidth]="640" [nzTitle]="replacing() ? 'Replacement for ' + replacing()!.instrumentNo : 'Register cheque'" (nzOnClose)="formOpen.set(false)" [nzFooter]="formFooter">
      <ng-container *nzDrawerContent>
        <form nz-form nzLayout="vertical" [formGroup]="form" data-testid="cheque-form">
          <nz-form-item><nz-form-label nzRequired>Legal entity</nz-form-label><nz-form-control [nzErrorTip]="err('legalEntityId')">
            <nz-select formControlName="legalEntityId" data-testid="ch-entity">@for (e of entities(); track e.id) { <nz-option [nzValue]="e.id" [nzLabel]="e.name"></nz-option> }</nz-select></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Customer</nz-form-label><nz-form-control [nzErrorTip]="err('partyId')">
            <nz-select formControlName="partyId" nzShowSearch nzServerSearch (nzOnSearch)="searchParties($event)" (ngModelChange)="partyChanged($event)" data-testid="ch-party">@for (o of partyOptions(); track o.value) { <nz-option [nzValue]="o.value" [nzLabel]="o.label"></nz-option> }</nz-select></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Type</nz-form-label><nz-form-control><nz-select formControlName="kind">@for (k of kinds; track k.value) { <nz-option [nzValue]="k.value" [nzLabel]="k.label"></nz-option> }</nz-select></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Cheque number</nz-form-label><nz-form-control [nzErrorTip]="err('instrumentNo')"><input nz-input formControlName="instrumentNo" data-testid="ch-no" /></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Bank</nz-form-label><nz-form-control><input nz-input formControlName="bankName" data-testid="ch-bank-name" /></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Amount</nz-form-label><nz-form-control [nzErrorTip]="err('amount')"><nz-input-number formControlName="amount" [nzMin]="0.01" [nzPrecision]="2" style="width:100%" data-testid="ch-amount"></nz-input-number></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label nzRequired>Cheque date</nz-form-label><nz-form-control [nzErrorTip]="err('instrumentDate')" nzExtra="The date written on it. A later date makes it post-dated."><nz-date-picker formControlName="instrumentDate" style="width:100%" data-testid="ch-date"></nz-date-picker></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Received on</nz-form-label><nz-form-control><nz-date-picker formControlName="receivedOn" style="width:100%"></nz-date-picker></nz-form-control></nz-form-item>
          <nz-form-item><nz-form-label>Notes</nz-form-label><nz-form-control><input nz-input formControlName="notes" /></nz-form-control></nz-form-item>
        </form>
        <h4>Invoices this cheque pays</h4>
        @for (o of openInvoices(); track o.id) {
          <div class="alloc">
            <span>{{ o.number }} · {{ 'ui.re.dueLc' | translate: 'due' }} {{ o.dueDate }} · {{ 'ui.common.outstandingLc' | translate: 'outstanding' }} {{ o.outstanding.toFixed(2) }}</span>
            <nz-input-number [ngModel]="amounts()[o.id] ?? 0" (ngModelChange)="setAmount(o.id, $event)" [nzMin]="0" [nzMax]="o.outstanding" [nzPrecision]="2" [ngModelOptions]="{standalone: true}" [attr.data-testid]="'ch-alloc-' + o.number"></nz-input-number>
          </div>
        } @empty { <div>No open invoices for this customer; the cheque will stay as customer credit.</div> }
      </ng-container>
    </nz-drawer>
    <ng-template #formFooter>
      <button nz-button (click)="formOpen.set(false)">Cancel</button>
      <button nz-button nzType="primary" [nzLoading]="saving()" (click)="register()" style="margin-inline-start:8px" data-testid="ch-save">{{ replacing() ? 'Register replacement' : 'Register' }}</button>
    </ng-template>

    <nz-drawer [nzVisible]="detailOpen()" [nzWidth]="480" [nzTitle]="cheque() ? 'Cheque ' + cheque()!.instrumentNo : ''" (nzOnClose)="detailOpen.set(false)">
      <ng-container *nzDrawerContent>
        @if (cheque(); as c) {
          <p><nz-tag [nzColor]="color(c.status)">{{ label(c.status) }}</nz-tag> {{ c.number }}</p>
          <p>{{ c.partyName }} · {{ c.bankName }} · <b>{{ c.amount.toFixed(2) }} {{ c.currencyCode }}</b></p>
          <p>{{ 'ui.cheque.date' | translate: 'Cheque date' }} {{ c.instrumentDate }} @if (c.depositedOn) { · {{ 'ui.cheque.depositedLc' | translate: 'deposited' }} {{ c.depositedOn }} } @if (c.clearedOn) { · {{ 'ui.cheque.clearedLc' | translate: 'cleared' }} {{ c.clearedOn }} }</p>
          @if (c.bounceReason) { <p>{{ 'ui.cheque.returned' | translate: 'Returned' }}: {{ c.bounceReason }} @if (c.bouncedOn) { · <span>Bounced on</span> <bdi dir="ltr">{{ c.bouncedOn }}</bdi> }</p> }
          @if (c.followUpOn) { <p data-testid="ch-followup-note"><span>Followed up on</span> <bdi dir="ltr">{{ c.followUpOn }}</bdi>: {{ c.followUpNote }}</p> }
          @if (c.replacesInstrumentId) { <p><span>Replaces an earlier cheque that bounced</span></p> }
          @if (c.replacedByChequeId) { <p data-testid="ch-replaced-by"><span>Replaced by a new cheque</span></p> }
          @for (a of c.allocations; track a.id) { <div>{{ 'ui.cheque.pays' | translate: 'Pays' }} {{ a.invoiceNumber }}: {{ a.amount.toFixed(2) }}</div> }
          <div style="margin-top:16px;display:flex;gap:8px;flex-wrap:wrap" *erpHasPermission="'finance.cheques.approve'">
            @if (c.status === 'inCustody') { <button nz-button nzType="primary" (click)="act('deposit')" data-testid="ch-deposit">Deposit</button> }
            @if (c.status === 'deposited') { <button nz-button nzType="primary" (click)="act('clear')" data-testid="ch-clear">Mark cleared</button> }
            @if (c.status === 'inCustody' || c.status === 'deposited') { <button nz-button nzDanger (click)="act('bounce')" data-testid="ch-bounce">Bounced</button> }
            @if (c.status === 'bounced') { <button nz-button (click)="act('followup')" data-testid="ch-followup">Log follow-up</button> }
            @if (c.status === 'bounced') { <button nz-button nzType="primary" (click)="openNew(c)" data-testid="ch-replace">Register replacement</button> }
          </div>
          @if (c.status === 'inCustody') { <button *erpHasPermission="'finance.cheques.write'" nz-button style="margin-top:12px" (click)="cancel()" data-testid="ch-cancel">Cancel this entry</button> }
          <div *erpHasPermission="'core.audit.read'" style="margin-top:20px"><erp-record-history recordType="PaymentInstrument" [recordId]="c.id" /></div>
        }
      </ng-container>
    </nz-drawer>

    <nz-modal [nzVisible]="action() !== null" [nzTitle]="actionTitle()" (nzOnCancel)="action.set(null)" (nzOnOk)="runAction()" [nzOkLoading]="saving()" nzOkText="Confirm">
      <ng-container *nzModalContent>
        <nz-date-picker [(ngModel)]="actionDate" style="width:100%;margin-bottom:12px" data-testid="act-date"></nz-date-picker>
        @if (action() === 'deposit') {
          <nz-select [(ngModel)]="actionBank" style="width:100%" nzPlaceHolder="Deposit to bank account" data-testid="act-bank">@for (a of banks(); track a.id) { <nz-option [nzValue]="a.id" [nzLabel]="a.code + ' · ' + a.name"></nz-option> }</nz-select>
        }
        @if (action() === 'bounce') { <input nz-input [(ngModel)]="actionReason" placeholder="Reason given by the bank" data-testid="act-reason" /> }
        @if (action() === 'followup') { <textarea nz-input rows="3" maxlength="500" [(ngModel)]="actionReason" placeholder="What was agreed with the customer?" data-testid="act-note"></textarea> }
      </ng-container>
    </nz-modal>`,
})
export class ChequesPage {
  private readonly currencyService = inject(WorkspaceCurrencyService);
  readonly currency = this.currencyService.currency;
  private readonly api = inject(ChequesApi);
  private readonly invoices = inject(InvoicesApi);
  private readonly parties = inject(PartiesApi);
  private readonly message = inject(NzMessageService);
  private readonly fb = inject(FormBuilder);
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
    (replaced ? this.api.replace(replaced.id, body) : this.api.register(body)).subscribe({ next: () => this.done(replaced ? 'Replacement registered' : 'Cheque registered'), error: (e) => this.fail(e) });
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
    call.subscribe({ next: () => { this.action.set(null); this.done(a === 'followup' ? 'Follow-up logged' : 'Cheque updated'); }, error: (e) => this.fail(e) });
  }

  cancel(): void {
    const c = this.cheque();
    if (c) this.api.cancel(c.id).subscribe({ next: () => this.done('Entry cancelled'), error: (e) => this.fail(e) });
  }

  private done(text: string): void { this.saving.set(false); this.formOpen.set(false); this.detailOpen.set(false); this.message.success(text); this.list.reload(); this.loadSummary(); }
  private fail(e: unknown): void { this.saving.set(false); const p = asProblem(e); this.message.error(p?.detail ?? p?.title ?? 'Could not complete the action'); }
}
