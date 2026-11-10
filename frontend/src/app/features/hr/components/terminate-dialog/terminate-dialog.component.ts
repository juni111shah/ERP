import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, effect, inject, input, output, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { asProblem } from '@core/http/problem-details';
import { SettlementPreview, TerminationApi } from '@core/api/hr-people.api';
import { MONTHS } from '@core/api/hr.api';

const iso = (d: Date | null): string | null => (d ? `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}` : null);

/** Ends an employment: pick the dates, look at what the leaver is owed, confirm, then prepare the final-settlement payroll run. */
@Component({
  selector: 'erp-terminate-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, FormsModule, NzButtonModule, NzDatePickerModule, NzInputModule, NzModalModule],
  styleUrl: './terminate-dialog.component.scss',
  templateUrl: './terminate-dialog.component.html',
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
      next: (p) => { this.busy.set(false); this.preview.set(p); this.done.set(true); this.message.success(`Employment ended for ${this.employeeName()}`); this.changed.emit(); },
      error: (e) => { this.busy.set(false); this.error.set(asProblem(e)?.detail ?? 'Could not end the employment'); },
    });
  }

  settle(): void {
    this.busy.set(true); this.error.set(null);
    this.api.finalSettlement(this.employeeId()).subscribe({
      next: (run) => { this.busy.set(false); this.message.success(`Final settlement prepared: payroll ${run.number}`); this.closed.emit(); this.done.set(false); void this.router.navigate(['/hr/payroll', run.id]); },
      error: (e) => { this.busy.set(false); this.error.set(asProblem(e)?.detail ?? 'Could not prepare the final settlement'); },
    });
  }
}
