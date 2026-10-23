import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTableModule } from 'ng-zorro-antd/table';
import { catchError, of } from 'rxjs';
import { AccountDto, AccountsApi, InvoicesApi } from '../../core/api/admin.api';
import { ProblemDetails } from '../../core/api/api.models';
import { asProblem } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { DepositDto, LeaseDto, LeasesApi, humanize } from './realestate.api';

type Mode = 'record' | 'settle' | null;

/** The security deposit of a lease: balance, history, receiving it, settling it on move-out and rolling it over to a renewal. */
@Component({
  selector: 'erp-lease-deposit',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzCardModule, NzTableModule],
  template: `
    <nz-card style="margin-top:16px" nzTitle="Security deposit" data-testid="deposit-card">
      @if (deposit(); as d) {
        <p>Agreed <b>{{ d.agreed.toLocaleString() }}</b> · received <b>{{ d.received.toLocaleString() }}</b> · applied or refunded <b>{{ d.applied.toLocaleString() }}</b> · held <b data-testid="deposit-held">{{ d.held.toLocaleString() }}</b> {{ lease().currencyCode }}</p>
        <div style="display:flex;gap:8px;flex-wrap:wrap;margin-bottom:12px" *erpHasPermission="'realestate.deposits.manage'">
          @if (lease().status !== 'cancelled' && d.received < d.agreed) { <button nz-button nzType="primary" (click)="open('record')" data-testid="deposit-record">Record deposit</button> }
          @if (canSettle() && d.held > 0) { <button nz-button (click)="open('settle')" data-testid="deposit-settle">Settle deposit</button> }
          @if (lease().renewedAsLeaseId && d.held > 0) { <button nz-button (click)="rollOver()" data-testid="deposit-rollover">Roll over to renewal</button> }
        </div>
        @if (d.transactions.length) {
          <nz-table [nzData]="d.transactions" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small">
            <thead><tr><th>Date</th><th>Type</th><th>Amount</th><th>Note</th></tr></thead>
            <tbody>@for (t of d.transactions; track t.id) { <tr><td>{{ t.date }}</td><td>{{ humanize(t.kind) }}</td><td>{{ t.amount.toLocaleString() }}</td><td>{{ t.description }}</td></tr> }</tbody>
          </nz-table>
        }
      }
    </nz-card>
    <erp-record-drawer [title]="mode() === 'record' ? 'Record security deposit' : 'Settle security deposit'" [open]="mode() !== null" [fields]="fields()" [saving]="saving()" [errors]="errors()" [width]="480"
                       testId="deposit-form" (submitted)="submit($event)" (closed)="mode.set(null)" />`,
})
export class LeaseDepositComponent {
  readonly lease = input.required<LeaseDto>();
  readonly changed = output<void>();
  private readonly api = inject(LeasesApi);
  private readonly invoices = inject(InvoicesApi);
  private readonly message = inject(NzMessageService);
  private readonly accountPage = toSignal(inject(AccountsApi).list({ page: 1, pageSize: 200, sort: 'code' }).pipe(catchError(() => of({ items: [] as AccountDto[] }))), { initialValue: { items: [] as AccountDto[] } });
  readonly humanize = humanize;
  readonly deposit = signal<DepositDto | null>(null);
  readonly mode = signal<Mode>(null);
  readonly saving = signal(false);
  readonly errors = signal<ProblemDetails | null>(null);
  readonly canSettle = computed(() => ['ended', 'terminated', 'renewed'].includes(this.lease().status));
  private readonly banks = computed(() => this.accountPage().items.filter((a) => a.type === 'asset' && a.isPostable && a.isActive && (a.isBank || a.systemRole === 'bank' || a.systemRole === 'cash'))
    .map((a) => ({ value: a.id, label: `${a.code} · ${a.name}` })));

  /** A computed (stable identity) so the drawer keeps what the user typed. */
  readonly fields = computed<FieldSpec[]>(() => this.mode() === 'record'
    ? [
        { key: 'bankAccountId', label: 'Deposit to', type: 'select', required: true, options: this.banks() },
        { key: 'date', label: 'Date received', type: 'date', required: true, default: new Date() },
        { key: 'amount', label: 'Amount', type: 'number', required: true, min: 0.01, default: Math.max((this.lease().securityDepositAmount ?? 0) - (this.deposit()?.received ?? 0), 0) },
      ]
    : [
        { key: 'date', label: 'Settlement date', type: 'date', required: true, default: new Date() },
        { key: 'damages', label: 'Damages deducted', type: 'number', min: 0, default: 0 },
        { key: 'damagesDescription', label: 'Damages note', type: 'text', maxLength: 300 },
        { key: 'arrearsAmount', label: 'Applied to unpaid rent', type: 'number', min: 0, default: 0, hint: 'Settles the tenant\'s oldest open invoices first.' },
        { key: 'refund', label: 'Refund to tenant', type: 'number', min: 0, default: this.deposit()?.held ?? 0 },
        { key: 'refundBankAccountId', label: 'Refund paid from', type: 'select', options: this.banks() },
      ]);

  constructor() { queueMicrotask(() => this.load()); }

  open(m: Mode): void { this.errors.set(null); this.mode.set(m); }

  load(): void { this.api.deposit(this.lease().id).subscribe((d) => this.deposit.set(d)); }

  submit(v: Record<string, unknown>): void {
    const lease = this.lease();
    this.saving.set(true); this.errors.set(null);
    if (this.mode() === 'record') {
      this.api.recordDeposit(lease.id, { bankAccountId: v['bankAccountId'] as string, date: v['date'] as string, amount: v['amount'] as number }).subscribe({ next: () => this.done('Deposit recorded'), error: (e) => this.fail(e) });
      return;
    }
    const arrears = Number(v['arrearsAmount'] ?? 0);
    const build = (open: { id: string; outstanding: number }[]) => {
      let left = arrears; const allocations: { invoiceId: string; amount: number }[] = [];
      for (const i of open) { if (left <= 0) break; const take = Math.min(left, i.outstanding); allocations.push({ invoiceId: i.id, amount: take }); left -= take; }
      if (left > 0) { this.saving.set(false); this.message.error(`Only ${(arrears - left).toFixed(2)} of unpaid rent is open for this tenant.`); return null; }
      return allocations;
    };
    const send = (allocations: { invoiceId: string; amount: number }[]) => this.api.settleDeposit(lease.id, {
      date: v['date'], damages: v['damages'] ?? 0, damagesDescription: v['damagesDescription'] ?? null, arrears: allocations, refund: v['refund'] ?? 0, refundBankAccountId: v['refundBankAccountId'] ?? null,
    }).subscribe({ next: () => this.done('Deposit settled'), error: (e) => this.fail(e) });
    if (arrears <= 0) { send([]); return; }
    this.invoices.list({ page: 1, pageSize: 100, sort: 'dueDate', filters: [{ field: 'partyId', op: 'eq', value: lease.lesseePartyId }, { field: 'isCreditNote', op: 'eq', value: false }] }).subscribe({
      next: (r) => { const a = build(r.items.filter((i) => i.status === 'issued' || i.status === 'partlyPaid')); if (a) send(a); },
      error: (e) => this.fail(e),
    });
  }

  rollOver(): void {
    const l = this.lease();
    if (l.renewedAsLeaseId) this.api.transferDeposit(l.id, l.renewedAsLeaseId).subscribe({ next: () => this.done('Deposit rolled over to the renewal'), error: (e) => this.fail(e) });
  }

  private done(text: string): void { this.saving.set(false); this.mode.set(null); this.message.success(text); this.load(); this.changed.emit(); }
  private fail(e: unknown): void { this.saving.set(false); const p = asProblem(e); this.errors.set(p); this.message.error(p?.detail ?? p?.title ?? 'Could not complete the action'); }
}
