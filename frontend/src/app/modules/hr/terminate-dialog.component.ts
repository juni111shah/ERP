import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, effect, inject, input, output, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { asProblem } from '../../core/http/problem-details';
import { SettlementPreview, TerminationApi } from './hr-people.api';
import { MONTHS } from './hr.api';

const iso = (d: Date | null): string | null => (d ? `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}` : null);

/** Ends an employment: pick the dates, look at what the leaver is owed, confirm, then prepare the final-settlement payroll run. */
@Component({
  selector: 'erp-terminate-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, FormsModule, NzButtonModule, NzDatePickerModule, NzInputModule, NzModalModule],
  styles: [`.grid { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; } label { display: block; font-size: 12px; color: var(--erp-color-text-secondary, #64748b); margin-bottom: 2px; } table.p { width: 100%; margin-top: 14px; border-collapse: collapse; } table.p td { padding: 4px 0; border-bottom: 1px solid var(--erp-color-border, #e2e8f0); } table.p td:last-child { text-align: end; } .total td { font-weight: 700; }`],
  template: `
    <nz-modal [nzVisible]="open()" [nzTitle]="(done() ? 'Employment ended · ' : 'End employment · ') + employeeName()" [nzWidth]="560" [nzFooter]="footer" (nzOnCancel)="close()">
      <ng-container *nzModalContent>
        @if (!done()) {
          <div class="grid">
            <div><label>Termination date *</label><nz-date-picker [(ngModel)]="terminationDate" (ngModelChange)="preview.set(null)" style="width:100%" data-testid="term-date"></nz-date-picker></div>
            <div><label>Last working day</label><nz-date-picker [(ngModel)]="lastWorkingDay" style="width:100%"></nz-date-picker></div>
            <div><label>Notice given on</label><nz-date-picker [(ngModel)]="noticeGivenOn" (ngModelChange)="preview.set(null)" style="width:100%"></nz-date-picker></div>
            <div><label>Reason</label><input nz-input [(ngModel)]="reason" maxlength="300" data-testid="term-reason" /></div>
          </div>
        }
        @if (preview(); as p) {
          <table class="p" data-testid="settlement-preview">
            <tr><td>Salary for {{ month(p.month) }} {{ p.year }} (prorated)</td><td>{{ p.finalBasic + p.finalAllowances | number: '1.2-2' }}</td></tr>
            <tr><td>Unused leave ({{ p.leaveDays }} days)</td><td>{{ p.leaveEncashment | number: '1.2-2' }}</td></tr>
            <tr><td>End of service @if (p.gratuityRule) { <small>· {{ p.gratuityRule }}</small> }</td><td>{{ p.endOfService | number: '1.2-2' }}</td></tr>
            <tr class="total"><td>Gross</td><td>{{ p.gross | number: '1.2-2' }}</td></tr>
            @if (p.recurringDeductions > 0) { <tr><td>Recurring deductions</td><td>−{{ p.recurringDeductions | number: '1.2-2' }}</td></tr> }
            @if (p.noticeShortfallDeduction > 0) { <tr><td>Notice not served ({{ p.noticeShortfallDays }} days)</td><td>−{{ p.noticeShortfallDeduction | number: '1.2-2' }}</td></tr> }
            @if (p.loanBalance > 0) { <tr><td>Loans and advances (balance {{ p.loanBalance | number: '1.2-2' }})</td><td>−{{ p.loanRecovery | number: '1.2-2' }}</td></tr> }
            <tr class="total"><td>Net to pay</td><td data-testid="settlement-net">{{ p.netPay | number: '1.2-2' }}</td></tr>
          </table>
          @if (p.unrecoveredLoan > 0) { <p style="color:var(--erp-color-warning-text);margin-top:8px">{{ p.unrecoveredLoan | number: '1.2-2' }} of loans cannot be recovered from this settlement.</p> }
          @for (n of p.notes; track n) { <p style="color:var(--erp-color-text-secondary);margin:6px 0 0">{{ n }}</p> }
        }
        @if (error()) { <p style="color:var(--erp-color-error-text);margin-top:8px">{{ error() }}</p> }
      </ng-container>
    </nz-modal>
    <ng-template #footer>
      <button nz-button (click)="close()">Close</button>
      @if (!done()) {
        <button nz-button (click)="doPreview()" [disabled]="!terminationDate || busy()" data-testid="term-preview">Preview settlement</button>
        <button nz-button nzType="primary" nzDanger (click)="confirm()" [disabled]="!terminationDate || busy()" data-testid="term-confirm">End employment</button>
      } @else {
        @if (preview()?.settlementRunId) { <span style="margin-inline-end:8px">A settlement already exists.</span> }
        @else { <button nz-button nzType="primary" (click)="settle()" [disabled]="busy()" data-testid="term-settle">Prepare final settlement</button> }
      }
    </ng-template>`,
})
export class TerminateDialogComponent {
  private readonly api = inject(TerminationApi);
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);
  readonly employeeId = input.required<string>();
  readonly employeeName = input('');
  readonly open = input(false);
  /** Set when the employment has already ended: the dialog goes straight to the settlement preview. */
  readonly endedOn = input<string | null>(null);
  readonly closed = output<void>();
  readonly changed = output<void>();

  terminationDate: Date | null = null;
  lastWorkingDay: Date | null = null;
  noticeGivenOn: Date | null = null;
  reason = '';
  readonly preview = signal<SettlementPreview | null>(null);
  readonly done = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  constructor() {
    effect(() => {
      const ended = this.endedOn();
      if (!this.open() || !ended) return;
      untracked(() => {
        this.done.set(true); this.busy.set(true); this.error.set(null);
        this.api.preview(this.employeeId(), ended, null).subscribe({
          next: (p) => { this.busy.set(false); this.preview.set(p); },
          error: (e) => { this.busy.set(false); this.error.set(asProblem(e)?.detail ?? 'Could not prepare the preview'); },
        });
      });
    });
  }

  month(m: number): string { return MONTHS[m - 1]; }
  close(): void { this.closed.emit(); if (this.done()) this.changed.emit(); this.done.set(false); this.preview.set(null); this.error.set(null); }

  doPreview(): void {
    this.busy.set(true); this.error.set(null);
    this.api.preview(this.employeeId(), iso(this.terminationDate)!, iso(this.noticeGivenOn)).subscribe({
      next: (p) => { this.busy.set(false); this.preview.set(p); },
      error: (e) => { this.busy.set(false); this.error.set(asProblem(e)?.detail ?? 'Could not prepare the preview'); },
    });
  }

  confirm(): void {
    this.busy.set(true); this.error.set(null);
    this.api.terminate(this.employeeId(), { terminationDate: iso(this.terminationDate)!, reason: this.reason || null, lastWorkingDay: iso(this.lastWorkingDay), noticeGivenOn: iso(this.noticeGivenOn) }).subscribe({
      next: (p) => { this.busy.set(false); this.preview.set(p); this.done.set(true); this.message.success('Employment ended'); this.changed.emit(); },
      error: (e) => { this.busy.set(false); this.error.set(asProblem(e)?.detail ?? 'Could not end the employment'); },
    });
  }

  settle(): void {
    this.busy.set(true); this.error.set(null);
    this.api.finalSettlement(this.employeeId()).subscribe({
      next: (run) => { this.busy.set(false); this.message.success('Final settlement prepared'); this.closed.emit(); this.done.set(false); void this.router.navigate(['/hr/payroll', run.id]); },
      error: (e) => { this.busy.set(false); this.error.set(asProblem(e)?.detail ?? 'Could not prepare the final settlement'); },
    });
  }
}
