import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, effect, inject, input, output, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { SettlementPreview, TerminationApi } from '@core/api/hr-people.api';
import { toIsoDate } from '@core/format/dates';
import { asProblem } from '@core/http/problem-details';
import { SessionStore } from '@core/session/session.store';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { monthName } from '../../hr-format';

/** A picked date as the API takes it (the local calendar day), or null. */
const iso = (d: Date | null): string | null => (d ? toIsoDate(d) : null);

/** Ends an employment: pick the dates, look at what the leaver is owed, confirm, then prepare the final-settlement payroll run. */
@Component({
  selector: 'erp-terminate-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, FormsModule, HasPermissionDirective, NzButtonModule, NzDatePickerModule, NzInputModule, NzModalModule],
  styleUrl: './terminate-dialog.component.scss',
  templateUrl: './terminate-dialog.component.html',
})
export class TerminateDialogComponent {
  private readonly terminationApi = inject(TerminationApi);
  private readonly message = inject(NzMessageService);
  private readonly router = inject(Router);
  private readonly session = inject(SessionStore);
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
        this.done.set(true);
        if (this.session.hasPermission('hr.payroll.read')) this.loadPreview(ended, null);
      });
    });
  }

  month(m: number): string { return monthName(m); }
  close(): void { this.closed.emit(); if (this.done()) this.changed.emit(); this.done.set(false); this.preview.set(null); this.error.set(null); }

  doPreview(): void { this.loadPreview(iso(this.terminationDate)!, iso(this.noticeGivenOn)); }

  confirm(): void {
    this.busy.set(true); this.error.set(null);
    this.terminationApi.terminate(this.employeeId(), { terminationDate: iso(this.terminationDate)!, reason: this.reason || null, lastWorkingDay: iso(this.lastWorkingDay), noticeGivenOn: iso(this.noticeGivenOn) }).subscribe({
      next: (p) => { this.busy.set(false); this.preview.set(p); this.done.set(true); this.message.success(`Employment ended for ${this.employeeName()}`); this.changed.emit(); },
      error: (e) => { this.busy.set(false); this.error.set(asProblem(e)?.detail ?? 'Could not end the employment'); },
    });
  }

  settle(): void {
    this.busy.set(true); this.error.set(null);
    this.terminationApi.finalSettlement(this.employeeId()).subscribe({
      next: (run) => { this.busy.set(false); this.message.success(`Final settlement prepared: payroll ${run.number}`); this.closed.emit(); this.done.set(false); if (this.session.hasPermission('hr.payroll.read')) void this.router.navigate(['/hr/payroll', run.id]); },
      error: (e) => { this.busy.set(false); this.error.set(asProblem(e)?.detail ?? 'Could not prepare the final settlement'); },
    });
  }

  /** What the leaver is owed if the employment ends on `terminationDate`. */
  private loadPreview(terminationDate: string, noticeGivenOn: string | null): void {
    this.busy.set(true); this.error.set(null);
    this.terminationApi.preview(this.employeeId(), terminationDate, noticeGivenOn).subscribe({
      next: (p) => { this.busy.set(false); this.preview.set(p); },
      error: (e) => { this.busy.set(false); this.error.set(asProblem(e)?.detail ?? 'Could not prepare the preview'); },
    });
  }
}
