import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { doneText } from '../../core/format/done-text';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzTableModule } from 'ng-zorro-antd/table';
import { asProblem } from '../../core/http/problem-details';
import { SessionStore } from '../../core/session/session.store';
import { matchText, TableToolsComponent } from '../../shared/table-tools.component';
import { AttendanceApi, AttendanceRow } from './hr.api';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { StatCardComponent } from '../../shared/stat-card.component';
import { StatePanelComponent } from '../../shared/state-panel.component';
import { StatusTagComponent } from '../../shared/status-tag.component';
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
  styles: [`
    .attendance-table-card {
      background: var(--erp-card-bg); border: 1px solid var(--erp-card-border); box-shadow: var(--erp-card-shadow);
      border-radius: 12px;
      overflow: hidden;
      margin-top: 16px;
    }
    .emp-cell { display: flex; align-items: center; gap: 12px; }
    .emp-avatar {
      width: 36px;
      height: 36px;
      border-radius: 50%;
      background: linear-gradient(135deg, #10955d 0%, #05663d 100%);
      color: #fff;
      display: flex;
      align-items: center;
      justify-content: center;
      font-weight: 600;
      font-size: 13px;
      flex-shrink: 0;
    }
    .emp-info { display: flex; flex-direction: column; gap: 2px; }
    .emp-name { font-weight: 600; color: var(--erp-color-text-primary); font-size: 13px; }
    .emp-sub { display: flex; align-items: center; gap: 6px; font-size: 12px; color: var(--erp-color-text-secondary, #64748b); }
    .sr { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }
    tr.unsaved > td { background: var(--erp-color-warning-bg); }
    tr.unsaved > td:first-child { box-shadow: inset 3px 0 0 var(--erp-color-warning-solid); }
    :host-context([dir='rtl']) tr.unsaved > td:first-child { box-shadow: inset -3px 0 0 var(--erp-color-warning-solid); }
    .row-actions { display: flex; align-items: center; gap: 8px; white-space: nowrap; }
    .unsaved-bar { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; margin-bottom: 12px; padding: 8px 12px; border-radius: 8px; background: var(--erp-color-warning-bg); border: 1px solid var(--erp-color-warning-border); color: var(--erp-color-warning-text); }
    .unsaved-bar .grow { flex: 1; }
  `],
  template: `
    <h1 class="erp-page-title">Attendance</h1>

    <div class="erp-stat-grid erp-stat-grid--auto" data-testid="attendance-stats">
      <erp-stat-card label="Total staff"
                     [value]="stats().total"
                     format="number"
                     [loading]="loading() && rows().length === 0"
                     caption="on the payroll this month"
                     [tone]="0" />
      <erp-stat-card label="Full attendance"
                     [value]="stats().full"
                     format="number"
                     [loading]="loading() && rows().length === 0"
                     caption="no absence, leave or overtime"
                     [tone]="1" />
      <erp-stat-card label="With exceptions"
                     [value]="stats().exceptions"
                     format="number"
                     [loading]="loading() && rows().length === 0"
                     caption="absence, leave or overtime"
                     [tone]="stats().exceptions > 0 ? 2 : 'neutral'" />
      <erp-stat-card label="Overtime hours"
                     [value]="stats().totalOt"
                     format="number"
                     [loading]="loading() && rows().length === 0"
                     caption="extra hours logged"
                     [tone]="3" />
    </div>

    <div style="display:flex;gap:12px;margin:16px 0 12px;align-items:center">
      <label class="sr" for="att-month">Month</label>
      <nz-date-picker nzId="att-month" nzMode="month" [nzAllowClear]="false" [ngModel]="month" (ngModelChange)="changeMonth($event)" data-testid="att-month"></nz-date-picker>
    </div>

    <nz-alert nzType="info" nzShowIcon nzMessage="Employees without a record for the month are paid in full. Record only exceptions: unpaid absence, paid leave and overtime." style="margin-bottom:12px;border-radius:8px"></nz-alert>

    <erp-table-tools [(search)]="q" (refresh)="refresh()" [loading]="loading()" testId="att">
      <nz-select [ngModel]="recorded()" (ngModelChange)="recorded.set($event)" nzAllowClear nzPlaceHolder="All employees" style="width:170px" data-testid="att-recorded-filter"><nz-option nzValue="exceptions" nzLabel="With exceptions"></nz-option><nz-option nzValue="full" [nzLabel]="'ui.hr.fullAttendance' | translate: 'Full attendance'"></nz-option></nz-select>
    </erp-table-tools>

    @if (dirty().size) {
      <div class="unsaved-bar" role="status" data-testid="att-unsaved">
        <span nz-icon nzType="warning" aria-hidden="true"></span>
        <span class="grow">{{ dirty().size === 1 ? '1 row has' : dirty().size + ' rows have' }} unsaved changes.</span>
        <button nz-button nzSize="small" (click)="discardAll()" data-testid="att-discard-all">Discard</button>
        <button nz-button nzSize="small" nzType="primary" [nzLoading]="saving()" (click)="saveAll()" data-testid="att-save-all">Save {{ dirty().size === 1 ? 'row' : 'all ' + dirty().size }}</button>
      </div>
    }

    @if (failed()) {
      <erp-state-panel kind="error" title="Attendance could not be loaded" hint="Check your connection and try again." actionLabel="Try again" (action)="load()" />
    } @else if (!loading() && rows().length === 0) {
      <erp-state-panel title="Nobody is on the payroll in this month" hint="Attendance lists the employees who are employed in the chosen month. Pick another month, or add the employee first." icon="team" />
    } @else {
      <div class="attendance-table-card">
        <nz-table [nzData]="shown()" [nzLoading]="loading()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="middle" nzNoResult="No employees match this filter" data-testid="att-table">
          <thead>
            <tr>
              <th>Employee</th>
              <th>Attendance</th>
              <th>Working days</th>
              <th>Present</th>
              <th>Unpaid absence</th>
              <th>Paid leave</th>
              <th>Approved leave</th>
              <th>Overtime hrs</th>
              @if (canManage) { <th><span class="sr">Actions</span></th> }
            </tr>
          </thead>
          <tbody>
            @for (r of shown(); track r.employeeId) {
              <tr [class.unsaved]="dirty().has(r.employeeId)" [attr.data-testid]="'att-row-' + r.employeeNumber">
                <td>
                  <div class="emp-cell">
                    <div class="emp-avatar">{{ initials(r.employeeName) }}</div>
                    <div class="emp-info">
                      <span class="emp-name">{{ r.employeeName }}</span>
                      <span class="emp-sub"><span class="code-pill">{{ r.employeeNumber }}</span> {{ r.designation }}</span>
                    </div>
                  </div>
                </td>
                <td>
                  @if (dirty().has(r.employeeId)) {
                    <erp-status value="pending" label="Unsaved" data-testid="att-unsaved-tag" />
                  } @else if (exception(r)) {
                    <erp-status value="open" label="Exceptions" />
                  } @else {
                    <erp-status value="active" label="Full attendance" />
                  }
                </td>
                @for (c of inputs; track c.key) {
                  @if (c.key === 'overtimeHours') {
                    <td data-testid="att-leave">@if (r.leavePaidDays || r.leaveUnpaidDays) { <span>{{ r.leavePaidDays ?? 0 }} paid / {{ r.leaveUnpaidDays ?? 0 }} unpaid</span> } @else { <span style="opacity:.5">-</span> }</td>
                  }
                  <td>
                    <label class="sr" [for]="'att-' + c.key + '-' + r.employeeNumber">{{ c.label }}, {{ r.employeeName }}</label>
                    <nz-input-number [nzId]="'att-' + c.key + '-' + r.employeeNumber" [ngModel]="r[c.key]" (ngModelChange)="edit(r, c.key, $event)" [nzMin]="c.min" [nzMax]="c.max ?? 9999" [nzPrecision]="c.precision" [nzStep]="c.step ?? 1" [nzDisabled]="!canManage" style="width:75px" [attr.data-testid]="c.testId ? c.testId + r.employeeNumber : null"></nz-input-number>
                  </td>
                }
                @if (canManage) {
                  <td>
                    <div class="row-actions">
                      @if (dirty().has(r.employeeId)) {
                        <button nz-button nzSize="small" nzType="primary" [disabled]="saving()" (click)="save(r)" [attr.data-testid]="'att-save-' + r.employeeNumber">Save</button>
                        <button nz-button nzSize="small" nzType="link" (click)="undo(r)" [attr.data-testid]="'att-undo-' + r.employeeNumber">Undo</button>
                      } @else if (r.recordId) {
                        <button nz-button nzSize="small" nzType="link" (click)="remove(r)" [attr.aria-label]="'Clear the record of ' + r.employeeName" [attr.data-testid]="'att-clear-' + r.employeeNumber">Clear</button>
                      }
                    </div>
                  </td>
                }
              </tr>
            }
          </tbody>
        </nz-table>
      </div>
    }`,
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
