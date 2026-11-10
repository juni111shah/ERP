import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { doneText } from '@core/format/done-text';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzTableModule } from 'ng-zorro-antd/table';
import { asProblem } from '@core/http/problem-details';
import { SessionStore } from '@core/session/session.store';
import { matchText, TableToolsComponent } from '@shared/components/table-tools/table-tools.component';
import { AttendanceApi, AttendanceRow } from '@core/api/hr.api';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { NzIconModule } from 'ng-zorro-antd/icon';

type GridField = 'workingDays' | 'daysPresent' | 'unpaidAbsentDays' | 'paidLeaveDays' | 'overtimeHours';
interface GridInput { key: GridField; label: string; min: number; max?: number; precision: number; step?: number; testId?: string; }

/** The inputs of a row, in column order. The approved-leave column sits between paid leave and overtime. */
const GRID_INPUTS: GridInput[] = [
  { key: 'workingDays', label: 'Working days', min: 1, max: 31, precision: 0 },
  { key: 'daysPresent', label: 'Present', min: 0, max: 31, precision: 0 },
  { key: 'unpaidAbsentDays', label: 'Unpaid absence', min: 0, max: 31, precision: 1, step: 0.5, testId: 'att-unpaid-' },
  { key: 'paidLeaveDays', label: 'Paid leave', min: 0, max: 31, precision: 0 },
  { key: 'overtimeHours', label: 'Overtime hours', min: 0, precision: 1, testId: 'att-ot-' },
];

/** An exception is anything that changes the standard pay: absence, leave or overtime. A record with none of them is still full attendance. */
export function hasException(r: AttendanceRow): boolean {
  return r.unpaidAbsentDays > 0 || r.paidLeaveDays > 0 || r.overtimeHours > 0 || (r.leavePaidDays ?? 0) > 0 || (r.leaveUnpaidDays ?? 0) > 0;
}

/** Monthly attendance: record only the exceptions; employees without a record are paid in full. */
@Component({
  selector: 'erp-attendance',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, NzSelectModule, TableToolsComponent, FormsModule, NzAlertModule, NzButtonModule, NzDatePickerModule, NzInputNumberModule, NzModalModule, NzTableModule, StatCardComponent, StatePanelComponent, StatusTagComponent, NzIconModule],
  styleUrl: './attendance.page.scss',
  templateUrl: './attendance.page.html',
})
export class AttendancePage {
  private readonly api = inject(AttendanceApi);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  readonly canManage = inject(SessionStore).hasPermission('hr.attendance.manage');
  readonly inputs = GRID_INPUTS;
  readonly exception = hasException;
  month: Date = new Date();
  readonly rows = signal<AttendanceRow[]>([]);
  readonly q = signal('');
  readonly recorded = signal<'exceptions' | 'full' | null>(null);
  /** Client-side filter: GET /hr/attendance only takes the month (the picker above). */
  readonly shown = computed(() => this.rows().filter((r) => matchText(this.q(), r.employeeName, r.employeeNumber, r.designation) && (!this.recorded() || hasException(r) === (this.recorded() === 'exceptions'))));
  readonly loading = signal(false);
  readonly failed = signal(false);
  readonly saving = signal(false);
  readonly dirty = signal<ReadonlySet<string>>(new Set());
  /** The row as the server last sent it, for Undo. */
  private saved = new Map<string, AttendanceRow>();

  /** Saved figures: edits count once they are saved. */
  readonly stats = computed(() => {
    const all = this.rows().map((r) => this.saved.get(r.employeeId) ?? r);
    const exceptions = all.filter(hasException).length;
    const totalOt = all.reduce((sum, r) => sum + (r.overtimeHours || 0), 0);
    return { total: all.length, full: all.length - exceptions, exceptions, totalOt };
  });

  initials(name: string): string {
    return (name || '').split(' ').map((n) => n[0]).slice(0, 2).join('').toUpperCase() || 'E';
  }

  constructor() { this.load(); }

  load(): void {
    this.loading.set(true); this.failed.set(false);
    this.api.month(this.month.getFullYear(), this.month.getMonth() + 1).subscribe({
      next: (r) => { this.saved = new Map(r.map((x) => [x.employeeId, { ...x }])); this.dirty.set(new Set()); this.rows.set(r); this.loading.set(false); },
      error: () => { this.loading.set(false); this.failed.set(true); },
    });
  }

  /** Runs the action now, or after asking when it would throw away rows that are not saved yet. */
  private leave(action: () => void, stay?: () => void): void {
    if (!this.dirty().size) { action(); return; }
    this.modal.confirm({ nzTitle: 'Discard unsaved changes?', nzContent: 'The rows you changed have not been saved.', nzOkText: 'Discard', nzOkDanger: true, nzCancelText: 'Keep editing', nzOnOk: action, nzOnCancel: stay });
  }

  refresh(): void { this.leave(() => this.load()); }

  changeMonth(next: Date): void {
    const previous = this.month;
    this.month = next;
    this.leave(() => this.load(), () => { this.month = previous; this.rows.update((r) => [...r]); });
  }

  edit(r: AttendanceRow, key: GridField, value: number | null): void {
    r[key] = value ?? 0;
    this.dirty.update((s) => new Set(s).add(r.employeeId));
  }

  undo(r: AttendanceRow): void {
    const original = this.saved.get(r.employeeId);
    if (original) this.replace({ ...original });
    this.clean(r.employeeId);
  }

  discardAll(): void { [...this.dirty()].forEach((id) => { const original = this.saved.get(id); if (original) this.replace({ ...original }); }); this.dirty.set(new Set()); }

  private clean(id: string): void { this.dirty.update((s) => { const n = new Set(s); n.delete(id); return n; }); }
  private replace(row: AttendanceRow): void { this.rows.update((all) => all.map((x) => (x.employeeId === row.employeeId ? row : x))); }
  private store(row: AttendanceRow): void { this.saved.set(row.employeeId, { ...row }); this.replace(row); this.clean(row.employeeId); }
  private body(r: AttendanceRow) {
    return { employeeId: r.employeeId, year: this.month.getFullYear(), month: this.month.getMonth() + 1, workingDays: r.workingDays, daysPresent: r.daysPresent, unpaidAbsentDays: r.unpaidAbsentDays, paidLeaveDays: r.paidLeaveDays, overtimeHours: r.overtimeHours };
  }
  private fail(e: unknown): void { this.saving.set(false); const p = asProblem(e); this.message.error(p?.errors ? Object.values(p.errors)[0][0] : (p?.detail ?? p?.title ?? 'Could not save')); }

  /** Saves one row and leaves the other unsaved rows as they are. */
  save(r: AttendanceRow): void {
    this.saving.set(true);
    this.api.save(this.body(r)).subscribe({
      next: (saved) => { this.saving.set(false); this.store(saved); this.message.success(`Attendance saved for ${r.employeeName}`); },
      error: (e) => this.fail(e),
    });
  }

  saveAll(): void {
    const rows = this.rows().filter((r) => this.dirty().has(r.employeeId));
    this.saving.set(true);
    forkJoin(rows.map((r) => this.api.save(this.body(r)))).subscribe({
      next: (all) => { this.saving.set(false); all.forEach((saved) => this.store(saved)); this.message.success(`Attendance saved for ${all.length} employee(s)`); },
      error: (e) => this.fail(e),
    });
  }

  remove(r: AttendanceRow): void {
    const id = r.recordId;
    if (!id) return;
    this.modal.confirm({
      nzTitle: `Clear the record of ${r.employeeName}?`, nzContent: 'The figures for this month are removed and the employee goes back to full attendance.', nzOkText: 'Clear', nzOkDanger: true,
      nzOnOk: () => new Promise<void>((resolve) => this.api.remove(id).subscribe({
        next: () => { this.message.success(`Attendance record cleared for ${r.employeeName}, back to full attendance`); this.reloadKeepingEdits(); resolve(); },
        error: (e) => { this.message.error(asProblem(e)?.detail ?? 'Could not clear the record'); resolve(); },
      })),
    });
  }

  private reloadKeepingEdits(): void {
    const edited = new Map(this.rows().filter((x) => this.dirty().has(x.employeeId)).map((x) => [x.employeeId, x]));
    this.api.month(this.month.getFullYear(), this.month.getMonth() + 1).subscribe((fresh) => {
      this.saved = new Map(fresh.map((x) => [x.employeeId, { ...x }]));
      this.rows.set(fresh.map((x) => edited.get(x.employeeId) ?? x));
    });
  }
}
