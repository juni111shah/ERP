import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { CashMovementType, PosSessionsApi } from '@core/api/pos.api';
import { doneText } from '@core/format/done-text';
import { problemMessage } from '@core/http/problem-details';
import { SessionStore } from '@core/session/session.store';

/** A shift the cash can be moved in or out of, as the dialog lists it. */
export interface CashDialogShift {
  id: string;
  label: string;
  expectedCash: number;
}

const KINDS: { value: CashMovementType; label: string; hint: string }[] = [
  { value: 'dropOut', label: 'Safe drop', hint: 'Cash moved from the drawer to the safe. It stays in cash on hand in the ledger.' },
  { value: 'payOut', label: 'Pay-out', hint: 'Cash paid out, such as for supplies. It posts as an expense.' },
  { value: 'floatIn', label: 'Float in', hint: 'Change added to the drawer from the safe. It stays in cash on hand in the ledger.' },
  { value: 'payIn', label: 'Pay-in', hint: 'Other cash put into the drawer. It posts against the same account as pay-outs.' },
];

/** Cash in or out of the drawer, from the till or the shifts screen. */
@Component({
  selector: 'erp-pos-cash-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, NzAlertModule, NzButtonModule, NzInputModule, NzInputNumberModule, NzModalModule, NzRadioModule, NzSelectModule],
  templateUrl: './pos-cash-dialog.component.html',
  styleUrl: './pos-cash-dialog.component.scss',
})
export class PosCashDialogComponent {
  private readonly sessionsApi = inject(PosSessionsApi);
  private readonly message = inject(NzMessageService);
  private readonly access = inject(SessionStore);

  readonly open = input(false);
  readonly shifts = input<CashDialogShift[]>([]);
  readonly closed = output<void>();
  readonly recorded = output<void>();

  readonly kinds = KINDS;
  readonly shiftId = signal('');
  readonly type = signal<CashMovementType>('dropOut');
  readonly amount = signal<number | null>(null);
  readonly reason = signal('');
  readonly busy = signal(false);
  /** Kept until the movement is recorded, so a retry after a lost answer cannot take the cash twice. */
  private idempotencyKey = crypto.randomUUID();

  readonly hint = computed(() => KINDS.find((k) => k.value === this.type())?.hint ?? '');
  readonly selected = computed(() => this.shifts().find((s) => s.id === this.shiftId()) ?? null);
  /** Opened only for someone who may move cash; the permission can still be taken away while the dialog is open. */
  readonly valid = computed(() => this.access.hasPermission('pos.sessions.manage') && !!this.shiftId() &&(this.amount() ?? 0) > 0 && !!this.reason().trim());

  constructor() {
    effect(() => {
      if (!this.open()) return;
      const first = this.shifts()[0];
      if (first && !this.shifts().some((s) => s.id === this.shiftId())) this.shiftId.set(first.id);
    });
  }

  focusAmount(): void {
    setTimeout(() => document.getElementById('pos-cash-amount')?.focus());
  }

  submit(): void {
    if (!this.valid() || this.busy()) return;
    this.busy.set(true);
    this.sessionsApi.recordDrawerTransaction(this.shiftId(), { type: this.type(), amount: this.amount()!, reason: this.reason().trim() }, this.idempotencyKey).subscribe({
      next: () => {
        this.busy.set(false);
        this.message.success(doneText('recorded', 'Cash movement', `${this.amount()}, ${this.reason().trim()}`));
        this.idempotencyKey = crypto.randomUUID();
        this.amount.set(null);
        this.reason.set('');
        this.recorded.emit();
      },
      error: (err) => {
        this.busy.set(false);
        this.message.error(problemMessage(err, 'The cash movement could not be recorded.'));
      },
    });
  }
}
