import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
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
import { ProblemDetails } from '@core/api/api.models';
import { DAILY_STATUSES, DailyAttendance, DailyAttendanceApi, RollUpRow } from '@core/api/hr-people.api';
import { EmployeesApi } from '@core/api/hr.api';
import { asProblem } from '@core/http/problem-details';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { itemsSignal } from '@shared/utils/items-signal';
import { dayText, monthName } from '../../hr-format';
import { currentEmployees, employeeOptions, optionLabel } from '../../hr-options';
import { parseImport, toTime } from '../../hr-parsers';

/** Check-in and check-out only apply to a day marked present or half day (or not marked yet). */
const withoutTimes = (v: Record<string, unknown>): boolean => !!v['status'] && v['status'] !== 'present' && v['status'] !== 'halfDay';

/** "8:05" from 485 minutes; blank for none. */
function hoursText(minutes: number): string {
  return minutes ? `${Math.floor(minutes / 60)}:${String(minutes % 60).padStart(2, '0')}` : '';
}

/** Day-by-day attendance: check-in and check-out, late and overtime, an import from a device export, and the roll-up into the monthly record payroll uses. */
@Component({
  selector: 'erp-daily-attendance',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, DecimalPipe, FormsModule, RecordDrawerComponent, HasPermissionDirective, StatePanelComponent, StatusTagComponent, NzButtonModule, NzDatePickerModule, NzIconModule, NzInputModule, NzModalModule, NzSelectModule, NzTableModule, NzTagModule],
  styleUrl: './daily-attendance.page.scss',
  templateUrl: './daily-attendance.page.html',
})
export class DailyAttendancePage {
  private readonly dailyApi = inject(DailyAttendanceApi);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  readonly employees = itemsSignal(inject(EmployeesApi).list({ page: 1, pageSize: 200, sort: 'number' }));
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
    { key: 'employeeId', label: 'Employee', type: 'select', required: true, readonlyOnEdit: true, options: employeeOptions(currentEmployees(this.employees().items)) },
    { key: 'date', label: 'Date', type: 'date', required: true, readonlyOnEdit: true, default: new Date() },
    { key: 'status', label: 'Status', type: 'select', options: DAILY_STATUSES, hint: 'Leave blank: present when there is a check-in, otherwise absent.' },
    { key: 'checkIn', label: 'Check-in (hh:mm)', type: 'text', maxLength: 8, hidden: withoutTimes },
    { key: 'checkOut', label: 'Check-out (hh:mm)', type: 'text', maxLength: 8, hidden: withoutTimes },
    { key: 'notes', label: 'Notes', type: 'text', maxLength: 300 },
  ]);

  constructor() { this.load(); }

  name(id: string): string { return this.byId().get(id)?.fullName ?? ''; }
  statusLabel(s: string): string { return optionLabel(DAILY_STATUSES, s); }
  hours(min: number): string { return hoursText(min); }

  load(): void {
    this.loading.set(true); this.failed.set(false);
    this.dailyApi.month(this.month.getFullYear(), this.month.getMonth() + 1, this.employeeId).subscribe({
      next: (r) => { this.rows.set(r); this.loading.set(false); },
      error: () => { this.loading.set(false); this.failed.set(true); },
    });
  }

  startAdd(): void { this.editing.set(null); this.errors.set(null); this.form.set(true); }
  startEdit(r: DailyAttendance): void { this.editing.set({ ...r, checkIn: r.checkIn?.slice(0, 5), checkOut: r.checkOut?.slice(0, 5) }); this.errors.set(null); this.form.set(true); }

  save(v: Record<string, unknown>): void {
    const checkIn = toTime(v['checkIn']); const checkOut = toTime(v['checkOut']);
    if (checkIn === undefined || checkOut === undefined) { this.message.warning('Enter times as hh:mm'); return; }
    const date = dayText(v['date']);
    this.busy.set(true); this.errors.set(null);
    this.dailyApi.save({ employeeId: v['employeeId'] as string, date, checkIn, checkOut, status: (v['status'] as string) || null, notes: (v['notes'] as string) || null }).subscribe({
      next: () => { this.busy.set(false); this.form.set(false); this.message.success(`Attendance saved for ${date}`); this.load(); },
      error: (e) => { this.busy.set(false); const p = asProblem(e); this.errors.set(p); this.message.error(p?.detail ?? p?.title ?? 'Could not save the day'); },
    });
  }

  remove(r: DailyAttendance): void {
    this.modal.confirm({
      nzTitle: 'Delete this day?', nzOkDanger: true, nzOkText: 'Delete',
      nzOnOk: () => new Promise<void>((resolve) => this.dailyApi.remove(r.id).subscribe({ next: () => { this.load(); resolve(); }, error: (e) => { this.message.error(asProblem(e)?.detail ?? 'Could not delete'); resolve(); } })),
    });
  }

  runImport(): void {
    const numbers = new Map(this.employees().items.map((e) => [e.number.toLowerCase(), e.id]));
    const { rows, errors } = parseImport(this.importText, numbers);
    this.importErrors.set(errors);
    if (errors.length || rows.length === 0) return;
    this.busy.set(true);
    this.dailyApi.bulk(rows).subscribe({
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
      nzTitle: `Roll up ${monthName(this.month.getMonth() + 1)} ${this.month.getFullYear()}?`,
      nzContent: 'For every employee with daily records, the monthly attendance is replaced by what the days add up to. Employees without daily records are left alone.',
      nzOkText: 'Roll up',
      nzOnOk: () => new Promise<void>((resolve) => {
        this.busy.set(true);
        this.dailyApi.rollUp(this.month.getFullYear(), this.month.getMonth() + 1).subscribe({
          next: (r) => { this.busy.set(false); if (r.length === 0) this.message.info(`No daily records in ${monthName(this.month.getMonth() + 1)} yet`); else this.summary.set(r); resolve(); },
          error: (e) => { this.busy.set(false); this.message.error(asProblem(e)?.detail ?? 'Could not roll up the month'); resolve(); },
        });
      }),
    });
  }
}
