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
import { CashMovementType, PosSessionsApi } from '../../core/api/pos.api';
import { problemMessage } from '../../core/http/problem-details';

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
  template: `
    <nz-modal [nzVisible]="open()" nzTitle="Cash in or out" nzOkText="Record" [nzOkLoading]="busy()" [nzOkDisabled]="!valid()" (nzAfterOpen)="focusAmount()" (nzOnCancel)="closed.emit()" (nzOnOk)="submit()">
      <ng-container *nzModalContent>
        <div class="stack">
          @if (!shifts().length) {
            <nz-alert nzType="warning" nzShowIcon nzMessage="No shift is open. Open one from the terminal first."></nz-alert>
          } @else if (shifts().length > 1) {
            <div>
              <label class="field" for="pos-cash-shift">Shift</label>
              <nz-select nzId="pos-cash-shift" [ngModel]="shiftId()" (ngModelChange)="shiftId.set($event)" style="width:100%">
                @for (s of shifts(); track s.id) { <nz-option [nzValue]="s.id" [nzLabel]="s.label"></nz-option> }
              </nz-select>
            </div>
          }
          <nz-radio-group [ngModel]="type()" (ngModelChange)="type.set($event)" nzButtonStyle="solid" aria-label="Kind of movement" class="kinds">
            @for (k of kinds; track k.value) { <label nz-radio-button [nzValue]="k.value">{{ k.label }}</label> }
          </nz-radio-group>
          <p class="hint">{{ hint() }}</p>
          <div>
            <label class="field" for="pos-cash-amount">Amount</label>
            <nz-input-number nzId="pos-cash-amount" [ngModel]="amount()" (ngModelChange)="amount.set($event)" [nzMin]="0" [nzStep]="10" style="width:100%" data-testid="pos-cash-amount"></nz-input-number>
            @if (selected(); as s) { <small class="hint">Expected in the drawer now: {{ s.expectedCash.toFixed(2) }}</small> }
          </div>
          <div>
            <label class="field" for="pos-cash-reason">Reason</label>
            <input id="pos-cash-reason" nz-input aria-label="Reason" [ngModel]="reason()" (ngModelChange)="reason.set($event)" (keydown.enter)="submit()" maxlength="250" placeholder="e.g. Midday drop" data-testid="pos-cash-reason" />
          </div>
        </div>
      </ng-container>
    </nz-modal>
  `,
  styles: [`
    .stack { display: flex; flex-direction: column; gap: 12px; }
    .field { display: block; margin-bottom: 4px; font-weight: 600; }
    .kinds { display: flex; }
    .kinds label { flex: 1; text-align: center; }
    .hint { margin: 0; font-size: 12px; color: var(--erp-color-text-muted); }
  `],
})
export class PosCashDialogComponent {
  private readonly api = inject(PosSessionsApi);
  private readonly message = inject(NzMessageService);

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
  readonly valid = computed(() => !!this.shiftId() && (this.amount() ?? 0) > 0 && !!this.reason().trim());

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
    this.api.recordDrawerTransaction(this.shiftId(), { type: this.type(), amount: this.amount()!, reason: this.reason().trim() }, this.idempotencyKey).subscribe({
      next: () => {
        this.busy.set(false);
        this.idempotencyKey = crypto.randomUUID();
        this.amount.set(null);
        this.reason.set('');
        this.message.success('Cash movement recorded.');
        this.recorded.emit();
      },
      error: (err) => {
        this.busy.set(false);
        this.message.error(problemMessage(err, 'The cash movement could not be recorded.'));
      },
    });
  }
}
