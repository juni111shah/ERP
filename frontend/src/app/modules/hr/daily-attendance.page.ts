import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { catchError, of } from 'rxjs';
import { ProblemDetails } from '../../core/api/api.models';
import { asProblem } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { StatePanelComponent } from '../../shared/state-panel.component';
import { StatusTagComponent } from '../../shared/status-tag.component';
import { DAILY_STATUSES, DailyAttendance, DailyAttendanceApi, DailyInput, RollUpRow } from './hr-people.api';
import { EmployeeSummary, EmployeesApi, MONTHS } from './hr.api';
import { ErpDatePipe } from '../../shared/display.pipes';

/** "8:5" or "08:05" -> "08:05:00"; blank -> null; anything else -> undefined (invalid). */
export function toTime(text: unknown): string | null | undefined {
  const t = String(text ?? '').trim();
  if (!t) return null;
  const m = /^(\d{1,2}):(\d{2})(?::(\d{2}))?$/.exec(t);
  if (!m || Number(m[1]) > 23 || Number(m[2]) > 59) return undefined;
  return `${m[1].padStart(2, '0')}:${m[2]}:${m[3] ?? '00'}`;
}

/** Lines "EMP-001,2026-10-05,08:00,17:30" (number, date, in, out) -> rows plus the lines that could not be read. */
export function parseImport(text: string, idByNumber: ReadonlyMap<string, string>): { rows: DailyInput[]; errors: string[] } {
  const rows: DailyInput[] = []; const errors: string[] = [];
  text.split(/\r?\n/).map((l) => l.trim()).filter(Boolean).forEach((line, i) => {
    const [number, date, inText, outText] = line.split(',').map((p) => p.trim());
    const employeeId = idByNumber.get((number ?? '').toLowerCase());
    const checkIn = toTime(inText); const checkOut = toTime(outText);
    if (!employeeId) errors.push(`Line ${i + 1}: unknown employee "${number}"`);
    else if (!/^\d{4}-\d{2}-\d{2}$/.test(date ?? '')) errors.push(`Line ${i + 1}: date must be yyyy-mm-dd`);
    else if (checkIn === undefined || checkOut === undefined) errors.push(`Line ${i + 1}: time must be hh:mm`);
    else rows.push({ employeeId, date, checkIn, checkOut, source: 'import' });
  });
  return { rows, errors };
}


/** Day-by-day attendance: check-in and check-out, late and overtime, an import from a device export, and the roll-up into the monthly record payroll uses. */
@Component({
  selector: 'erp-daily-attendance',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, DecimalPipe, FormsModule, RecordDrawerComponent, HasPermissionDirective, StatePanelComponent, StatusTagComponent, NzButtonModule, NzDatePickerModule, NzIconModule, NzInputModule, NzModalModule, NzSelectModule, NzTableModule, NzTagModule],
  styles: [`.num { text-align: end; white-space: nowrap; } .sr { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; } .import-error { color: var(--erp-color-error-text); margin: 4px 0 0; } .gap { margin-inline-end: 10px; }`],
  template: `
    <h1 class="erp-page-title">Daily attendance</h1>
    <div style="display:flex;gap:12px;flex-wrap:wrap;align-items:center;margin:12px 0">
      <label class="sr" for="daily-month">Month</label>
      <nz-date-picker nzId="daily-month" nzMode="month" [nzAllowClear]="false" [(ngModel)]="month" (ngModelChange)="load()" data-testid="daily-month"></nz-date-picker>
      <label class="sr" for="daily-employee">Employee</label>
      <nz-select nzId="daily-employee" style="width:240px" nzAllowClear nzShowSearch nzPlaceHolder="All employees" [(ngModel)]="employeeId" (ngModelChange)="load()" data-testid="daily-employee">
        @for (e of employees().items; track e.id) { <nz-option [nzValue]="e.id" [nzLabel]="e.number + ' · ' + e.fullName"></nz-option> }
      </nz-select>
      <span style="flex:1"></span>
      <button *erpHasPermission="'hr.attendance.manage'" nz-button (click)="startAdd()" data-testid="daily-add"><span nz-icon nzType="plus"></span>Add a day</button>
      <button *erpHasPermission="'hr.attendance.manage'" nz-button (click)="importOpen.set(true)" data-testid="daily-import"><span nz-icon nzType="upload"></span>Import</button>
      <button *erpHasPermission="'hr.attendance.manage'" nz-button nzType="primary" [disabled]="busy()" (click)="rollUp()" data-testid="daily-rollup">Roll up to the month</button>
    </div>
    @if (failed()) {
      <erp-state-panel kind="error" title="Daily attendance could not be loaded" hint="Check your connection and try again." actionLabel="Try again" (action)="load()" />
    } @else {
      <nz-table [nzData]="rows()" [nzLoading]="loading()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" [nzNoResult]="noDays" data-testid="daily-table">
        <thead><tr><th>Date</th><th>Employee</th><th>In</th><th>Out</th><th>Status</th><th class="num">Worked</th><th class="num">Late (min)</th><th class="num">Overtime (min)</th><th></th></tr></thead>
        <tbody>
          @for (r of rows(); track r.id) {
            <tr>
              <td>{{ r.date | erpDate }}</td><td>{{ name(r.employeeId) }}</td><td>{{ r.checkIn?.slice(0, 5) }}</td><td>{{ r.checkOut?.slice(0, 5) }}</td>
              <td><erp-status [value]="r.status" [label]="statusLabel(r.status)" /></td>
              <td class="num">{{ hours(r.workedMinutes) }}</td><td class="num">{{ r.lateMinutes || '' }}</td><td class="num">{{ r.overtimeMinutes || '' }}</td>
              <td><a class="gap" *erpHasPermission="'hr.attendance.manage'" (click)="startEdit(r)">Edit</a><a *erpHasPermission="'hr.attendance.manage'" (click)="remove(r)">Delete</a></td>
            </tr>
          }
        </tbody>
      </nz-table>
      <ng-template #noDays><erp-state-panel title="No days recorded for this month" hint="Add a day by hand, or import the export from your attendance device." icon="calendar" /></ng-template>
    }

    <erp-record-drawer [title]="editing() ? 'Edit day' : 'Add a day'" [open]="form()" [fields]="fields()" [value]="$any(editing())" [saving]="busy()" [errors]="errors()" [width]="440"
                       testId="daily-form" (submitted)="save($event)" (closed)="form.set(false)" />

    <nz-modal [nzVisible]="importOpen()" nzTitle="Import attendance" [nzWidth]="560" nzOkText="Import" [nzOkLoading]="busy()" (nzOnOk)="runImport()" (nzOnCancel)="importOpen.set(false)">
      <ng-container *nzModalContent>
        <p>One line per day: <code>employee number, date, check-in, check-out</code>, for example <code>EMP-001,2026-10-05,08:00,17:30</code>. Leave the times blank for an absent day.</p>
        <textarea nz-input rows="8" aria-label="Attendance lines to import" [(ngModel)]="importText" data-testid="daily-import-text"></textarea>
        @for (e of importErrors(); track e) { <p class="import-error">{{ e }}</p> }
      </ng-container>
    </nz-modal>

    <nz-modal [nzVisible]="summary() !== null" nzTitle="Monthly attendance updated" [nzWidth]="720" [nzFooter]="null" (nzOnCancel)="summary.set(null)">
      <ng-container *nzModalContent>
        <nz-table [nzData]="summary() ?? []" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="rollup-table">
          <thead><tr><th>Employee</th><th>Working days</th><th>Present</th><th>Unpaid absent</th><th>On leave</th><th>Overtime (h)</th><th>Late days</th></tr></thead>
          <tbody>@for (r of summary() ?? []; track r.employeeId) { <tr><td>{{ r.employeeName }}</td><td>{{ r.workingDays }}</td><td>{{ r.daysPresent }}</td><td>{{ r.unpaidAbsentDays }}</td><td>{{ r.paidLeaveDays }}</td><td>{{ r.overtimeHours | number: '1.2-2' }}</td><td>{{ r.lateDays }}</td></tr> }</tbody>
        </nz-table>
        <p style="margin-top:8px;color:var(--erp-color-text-secondary)">Payroll reads these figures. Recalculate a draft payroll to pick them up.</p>
      </ng-container>
    </nz-modal>`,
})
export class DailyAttendancePage {
  private readonly api = inject(DailyAttendanceApi);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  readonly employees = toSignal(inject(EmployeesApi).list({ page: 1, pageSize: 200, sort: 'number' }).pipe(catchError(() => of({ items: [] as EmployeeSummary[] }))), { initialValue: { items: [] as EmployeeSummary[] } });
  private readonly byId = computed(() => new Map(this.employees().items.map((e) => [e.id, e])));

  month: Date = new Date();
  employeeId: string | null = null;
  importText = '';
  readonly rows = signal<DailyAttendance[]>([]);
  readonly loading = signal(false);
  readonly failed = signal(false);
  readonly form = signal(false);
  readonly editing = signal<DailyAttendance | null>(null);
  readonly busy = signal(false);
  readonly errors = signal<ProblemDetails | null>(null);
  readonly importOpen = signal(false);
  readonly importErrors = signal<string[]>([]);
  readonly summary = signal<RollUpRow[] | null>(null);

  readonly fields = computed<FieldSpec[]>(() => [
    { key: 'employeeId', label: 'Employee', type: 'select', required: true, readonlyOnEdit: true, options: this.employees().items.filter((e) => e.status !== 'terminated').map((e) => ({ value: e.id, label: `${e.number} · ${e.fullName}` })) },
    { key: 'date', label: 'Date', type: 'date', required: true, readonlyOnEdit: true, default: new Date() },
    { key: 'status', label: 'Status', type: 'select', options: DAILY_STATUSES, hint: 'Leave blank: present when there is a check-in, otherwise absent.' },
    { key: 'checkIn', label: 'Check-in (hh:mm)', type: 'text', maxLength: 8, hidden: (v) => !!v['status'] && v['status'] !== 'present' && v['status'] !== 'halfDay' },
    { key: 'checkOut', label: 'Check-out (hh:mm)', type: 'text', maxLength: 8, hidden: (v) => !!v['status'] && v['status'] !== 'present' && v['status'] !== 'halfDay' },
    { key: 'notes', label: 'Notes', type: 'text', maxLength: 300 },
  ]);

  constructor() { this.load(); }

  name(id: string): string { return this.byId().get(id)?.fullName ?? ''; }
  statusLabel(s: string): string { return DAILY_STATUSES.find((x) => x.value === s)?.label ?? s; }
  hours(min: number): string { return min ? `${Math.floor(min / 60)}:${String(min % 60).padStart(2, '0')}` : ''; }

  load(): void {
    this.loading.set(true); this.failed.set(false);
    this.api.month(this.month.getFullYear(), this.month.getMonth() + 1, this.employeeId).subscribe({
      next: (r) => { this.rows.set(r); this.loading.set(false); },
      error: () => { this.loading.set(false); this.failed.set(true); },
    });
  }

  startAdd(): void { this.editing.set(null); this.errors.set(null); this.form.set(true); }
  startEdit(r: DailyAttendance): void { this.editing.set({ ...r, checkIn: r.checkIn?.slice(0, 5), checkOut: r.checkOut?.slice(0, 5) }); this.errors.set(null); this.form.set(true); }

  save(v: Record<string, unknown>): void {
    const checkIn = toTime(v['checkIn']); const checkOut = toTime(v['checkOut']);
    if (checkIn === undefined || checkOut === undefined) { this.message.warning('Enter times as hh:mm'); return; }
    this.busy.set(true); this.errors.set(null);
    this.api.save({ employeeId: v['employeeId'] as string, date: String(v['date']).slice(0, 10), checkIn, checkOut, status: (v['status'] as string) || null, notes: (v['notes'] as string) || null }).subscribe({
      next: () => { this.busy.set(false); this.form.set(false); this.message.success(`Attendance saved for ${String(v['date']).slice(0, 10)}`); this.load(); },
      error: (e) => { this.busy.set(false); const p = asProblem(e); this.errors.set(p); this.message.error(p?.detail ?? p?.title ?? 'Could not save the day'); },
    });
  }

  remove(r: DailyAttendance): void {
    this.modal.confirm({
      nzTitle: 'Delete this day?', nzOkDanger: true, nzOkText: 'Delete',
      nzOnOk: () => new Promise<void>((resolve) => this.api.remove(r.id).subscribe({ next: () => { this.load(); resolve(); }, error: (e) => { this.message.error(asProblem(e)?.detail ?? 'Could not delete'); resolve(); } })),
    });
  }

  runImport(): void {
    const numbers = new Map(this.employees().items.map((e) => [e.number.toLowerCase(), e.id]));
    const { rows, errors } = parseImport(this.importText, numbers);
    this.importErrors.set(errors);
    if (errors.length || rows.length === 0) return;
    this.busy.set(true);
    this.api.bulk(rows).subscribe({
      next: (r) => {
        this.busy.set(false); this.importErrors.set(r.rejected);
        this.message.success(`${r.saved} day(s) imported${r.rejected.length ? `, ${r.rejected.length} rejected` : ''}`);
        if (!r.rejected.length) { this.importOpen.set(false); this.importText = ''; }
        this.load();
      },
      error: (e) => { this.busy.set(false); this.message.error(asProblem(e)?.detail ?? 'Import failed'); },
    });
  }

  rollUp(): void {
    this.modal.confirm({
      nzTitle: `Roll up ${MONTHS[this.month.getMonth()]} ${this.month.getFullYear()}?`,
      nzContent: 'For every employee with daily records, the monthly attendance is replaced by what the days add up to. Employees without daily records are left alone.',
      nzOkText: 'Roll up',
      nzOnOk: () => new Promise<void>((resolve) => {
        this.busy.set(true);
        this.api.rollUp(this.month.getFullYear(), this.month.getMonth() + 1).subscribe({
          next: (r) => { this.busy.set(false); if (r.length === 0) this.message.info(`No daily records in ${MONTHS[this.month.getMonth()]} yet`); else this.summary.set(r); resolve(); },
          error: (e) => { this.busy.set(false); this.message.error(asProblem(e)?.detail ?? 'Could not roll up the month'); resolve(); },
        });
      }),
    });
  }
}
