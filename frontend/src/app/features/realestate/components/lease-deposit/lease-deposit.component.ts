import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTableModule } from 'ng-zorro-antd/table';
import { AccountsApi, InvoicesApi } from '@core/api/admin.api';
import { ProblemDetails } from '@core/api/api.models';
import { DepositAllocation, DepositDto, LeaseDto, LeasesApi, humanize } from '@core/api/realestate.api';
import { asProblem } from '@core/http/problem-details';
import { I18nService } from '@core/i18n/i18n.service';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { itemsSignal } from '@shared/utils/items-signal';
import { accountOptions, bankOrCashAccounts } from '@shared/utils/payment-accounts';
import { allocateArrears, depositSettlement } from '../../realestate-deposits';

type Mode = 'record' | 'settle' | null;

/** The security deposit of a lease: balance, history, receiving it, settling it on move-out and rolling it over to a renewal. */
@Component({
  selector: 'erp-lease-deposit',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzCardModule, NzTableModule],
  templateUrl: './lease-deposit.component.html',
})
export class LeaseDepositComponent {
  private readonly i18n = inject(I18nService);
  readonly lease = input.required<LeaseDto>();
  readonly changed = output<void>();
  private readonly leasesApi = inject(LeasesApi);
  private readonly invoicesApi = inject(InvoicesApi);
  private readonly message = inject(NzMessageService);
  private readonly accountPage = itemsSignal(inject(AccountsApi).list({ page: 1, pageSize: 200, sort: 'code' }));
  readonly humanize = humanize;
  readonly deposit = signal<DepositDto | null>(null);
  readonly mode = signal<Mode>(null);
  readonly saving = signal(false);
  readonly errors = signal<ProblemDetails | null>(null);
  readonly canSettle = computed(() => ['ended', 'terminated', 'renewed'].includes(this.lease().status));
  private readonly banks = computed(() => accountOptions(bankOrCashAccounts(this.accountPage().items)));

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

  load(): void { this.leasesApi.deposit(this.lease().id).subscribe((d) => this.deposit.set(d)); }

  /** The drawer's OK: records the deposit received, or settles it (unpaid rent first goes to the tenant's open invoices). */
  submit(v: Record<string, unknown>): void {
    const lease = this.lease();
    this.saving.set(true); this.errors.set(null);
    if (this.mode() === 'record') {
      this.leasesApi.recordDeposit(lease.id, { bankAccountId: v['bankAccountId'] as string, date: v['date'] as string, amount: v['amount'] as number }).subscribe({ next: () => this.done('Deposit recorded'), error: (e) => this.fail(e) });
      return;
    }
    const arrears = Number(v['arrearsAmount'] ?? 0);
    if (arrears <= 0) { this.settle(lease.id, v, []); return; }
    this.invoicesApi.list({ page: 1, pageSize: 100, sort: 'dueDate', filters: [{ field: 'partyId', op: 'eq', value: lease.lesseePartyId }, { field: 'isCreditNote', op: 'eq', value: false }] }).subscribe({
      next: (r) => {
        const { allocations, shortfall } = allocateArrears(r.items.filter((i) => i.status === 'issued' || i.status === 'partlyPaid'), arrears);
        if (shortfall > 0) { this.saving.set(false); this.message.error(`${this.i18n.t('ui.msg.only', 'Only')} ${(arrears - shortfall).toFixed(2)} ${this.i18n.t('ui.msg.unpaidRentOpen', 'of unpaid rent is open for this tenant.')}`); return; }
        this.settle(lease.id, v, allocations);
      },
      error: (e) => this.fail(e),
    });
  }

  rollOver(): void {
    const l = this.lease();
    if (l.renewedAsLeaseId) this.leasesApi.transferDeposit(l.id, l.renewedAsLeaseId).subscribe({ next: () => this.done('Deposit rolled over to the renewal'), error: (e) => this.fail(e) });
  }

  private settle(leaseId: string, v: Record<string, unknown>, allocations: DepositAllocation[]): void {
    this.leasesApi.settleDeposit(leaseId, depositSettlement(v, allocations)).subscribe({ next: () => this.done('Deposit settled'), error: (e) => this.fail(e) });
  }

  private done(text: string): void { this.saving.set(false); this.mode.set(null); this.message.success(`${text} for lease ${this.lease().number ?? this.lease().unitNumber}`); this.load(); this.changed.emit(); }
  private fail(e: unknown): void { this.saving.set(false); const p = asProblem(e); this.errors.set(p); if (!p?.errors) this.message.error(p?.detail ?? p?.title ?? 'Could not complete the action'); }
}
