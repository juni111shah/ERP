import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { Observable, forkJoin } from 'rxjs';
import { AttendanceApi, AttendanceRow } from '@core/api/hr.api';
import { asProblem } from '@core/http/problem-details';
import { TranslatePipe } from '@core/i18n/translate.pipe';
import { SessionStore } from '@core/session/session.store';
import { StatCardComponent } from '@shared/components/stat-card/stat-card.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { matchText, TableToolsComponent } from '@shared/components/table-tools/table-tools.component';
import { GRID_INPUTS, GridField, attendanceInput, attendanceStats, hasException } from '../../hr-attendance';
import { initials } from '../../hr-format';

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
  private readonly attendanceApi = inject(AttendanceApi);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly session = inject(SessionStore);
  /** Entering, saving and clearing figures need hr.attendance.manage; it follows live access changes. */
  readonly canManage = computed(() => this.session.hasPermission('hr.attendance.manage'));
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
  readonly stats = computed(() => attendanceStats(this.rows().map((r) => this.saved.get(r.employeeId) ?? r)));

  readonly initials = initials;

  constructor() { this.load(); }

  load(): void {
    this.loading.set(true); this.failed.set(false);
    this.fetchMonth().subscribe({
      next: (r) => { this.remember(r); this.dirty.set(new Set()); this.rows.set(r); this.loading.set(false); },
      error: () => { this.loading.set(false); this.failed.set(true); },
    });
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
    this.restore(r.employeeId);
    this.clean(r.employeeId);
  }

  discardAll(): void { [...this.dirty()].forEach((id) => this.restore(id)); this.dirty.set(new Set()); }

  /** Saves one row and leaves the other unsaved rows as they are. */
  save(r: AttendanceRow): void {
    this.saving.set(true);
    this.attendanceApi.save(attendanceInput(r, this.month)).subscribe({
      next: (saved) => { this.saving.set(false); this.store(saved); this.message.success(`Attendance saved for ${r.employeeName}`); },
      error: (e) => this.fail(e),
    });
  }

  saveAll(): void {
    const rows = this.rows().filter((r) => this.dirty().has(r.employeeId));
    this.saving.set(true);
    forkJoin(rows.map((r) => this.attendanceApi.save(attendanceInput(r, this.month)))).subscribe({
      next: (all) => { this.saving.set(false); all.forEach((saved) => this.store(saved)); this.message.success(`Attendance saved for ${all.length} employee(s)`); },
      error: (e) => this.fail(e),
    });
  }

  remove(r: AttendanceRow): void {
    const id = r.recordId;
    if (!id) return;
    this.modal.confirm({
      nzTitle: `Clear the record of ${r.employeeName}?`, nzContent: 'The figures for this month are removed and the employee goes back to full attendance.', nzOkText: 'Clear', nzOkDanger: true,
      nzOnOk: () => new Promise<void>((resolve) => this.attendanceApi.remove(id).subscribe({
        next: () => { this.message.success(`Attendance record cleared for ${r.employeeName}, back to full attendance`); this.reloadKeepingEdits(); resolve(); },
        error: (e) => { this.message.error(asProblem(e)?.detail ?? 'Could not clear the record'); resolve(); },
      })),
    });
  }

  private fetchMonth(): Observable<AttendanceRow[]> { return this.attendanceApi.month(this.month.getFullYear(), this.month.getMonth() + 1); }

  /** Runs the action now, or after asking when it would throw away rows that are not saved yet. */
  private leave(action: () => void, stay?: () => void): void {
    if (!this.dirty().size) { action(); return; }
    this.modal.confirm({ nzTitle: 'Discard unsaved changes?', nzContent: 'The rows you changed have not been saved.', nzOkText: 'Discard', nzOkDanger: true, nzCancelText: 'Keep editing', nzOnOk: action, nzOnCancel: stay });
  }

  /** Keeps a copy of each row as the server sent it. */
  private remember(rows: AttendanceRow[]): void { this.saved = new Map(rows.map((x) => [x.employeeId, { ...x }])); }
  /** Puts the saved copy of the employee's row back (Undo). */
  private restore(employeeId: string): void { const original = this.saved.get(employeeId); if (original) this.replace({ ...original }); }
  private clean(id: string): void { this.dirty.update((s) => { const n = new Set(s); n.delete(id); return n; }); }
  private replace(row: AttendanceRow): void { this.rows.update((all) => all.map((x) => (x.employeeId === row.employeeId ? row : x))); }
  private store(row: AttendanceRow): void { this.saved.set(row.employeeId, { ...row }); this.replace(row); this.clean(row.employeeId); }
  private fail(e: unknown): void { this.saving.set(false); const p = asProblem(e); this.message.error(p?.errors ? Object.values(p.errors)[0][0] : (p?.detail ?? p?.title ?? 'Could not save')); }

  private reloadKeepingEdits(): void {
    const edited = new Map(this.rows().filter((x) => this.dirty().has(x.employeeId)).map((x) => [x.employeeId, x]));
    this.fetchMonth().subscribe((fresh) => {
      this.remember(fresh);
      this.rows.set(fresh.map((x) => edited.get(x.employeeId) ?? x));
    });
  }
}
