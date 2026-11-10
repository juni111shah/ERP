import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { Observable, throwError } from 'rxjs';
import { RosterEntry, ShiftDto, ShiftsApi } from '@core/api/hr-people.api';
import { EmployeesApi } from '@core/api/hr.api';
import { toIsoDate } from '@core/format/dates';
import { doneText } from '@core/format/done-text';
import { asProblem } from '@core/http/problem-details';
import { FieldSpec, RecordDrawerComponent } from '@shared/components/record-drawer/record-drawer.component';
import { RowActionsComponent } from '@shared/components/row-actions/row-actions.component';
import { StatePanelComponent } from '@shared/components/state-panel/state-panel.component';
import { StatusTagComponent } from '@shared/components/status-tag/status-tag.component';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { itemsSignal } from '@shared/utils/items-signal';
import { MasterDataState } from '@shared/utils/master-data-state';
import { toTime } from '../../hr-parsers';

/** Work shifts: start time, late grace and the hours of a normal day; employees on a shift are measured against it. */
@Component({
  selector: 'erp-shifts',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RowActionsComponent, ErpDatePipe, FormsModule, RecordDrawerComponent, HasPermissionDirective, StatePanelComponent, StatusTagComponent, NzButtonModule, NzIconModule, NzModalModule, NzSelectModule, NzTableModule, NzDatePickerModule],
  styleUrl: './shifts.page.scss',
  templateUrl: './shifts.page.html',
})
export class ShiftsPage {
  private readonly shiftsApi = inject(ShiftsApi);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  readonly loading = signal(false);
  readonly failed = signal(false);
  readonly employees = itemsSignal(inject(EmployeesApi).list({ page: 1, pageSize: 200, sort: 'number' }));
  readonly rows = signal<ShiftDto[]>([]);
  readonly assigning = signal<ShiftDto | null>(null);
  readonly rostering = signal<ShiftDto | null>(null);
  readonly roster = signal<RosterEntry[]>([]);
  picked: string[] = [];
  range: Date[] = [];

  readonly state = new MasterDataState<ShiftDto>({ save: (id, body) => this.saveShift(id, body as Record<string, unknown>), reload: () => this.load(), message: this.message, entityName: 'Shift' });
  readonly fields: FieldSpec[] = [
    { key: 'code', label: 'Code', type: 'text', required: true, maxLength: 20 },
    { key: 'name', label: 'Name', type: 'text', required: true, maxLength: 100 },
    { key: 'startTime', label: 'Start time (hh:mm)', type: 'text', required: true, maxLength: 8, default: '08:00' },
    { key: 'graceMinutes', label: 'Late grace (minutes)', type: 'number', default: 10 },
    { key: 'hoursPerDay', label: 'Hours in a normal day', type: 'number', default: 8 },
    { key: 'isActive', label: 'Active', type: 'switch', default: true },
  ];

  constructor() { this.load(); }

  load(): void {
    this.loading.set(true); this.failed.set(false);
    this.shiftsApi.list().subscribe({ next: (r) => { this.rows.set(r); this.loading.set(false); }, error: () => { this.loading.set(false); this.failed.set(true); } });
    const now = new Date();
    this.shiftsApi.roster(now.getFullYear(), now.getMonth() + 1).subscribe((r) => this.roster.set(r));
  }
  employeeName(id: string): string { return this.employees().items.find((e) => e.id === id)?.fullName ?? ''; }
  shiftName(id: string): string { return this.rows().find((s) => s.id === id)?.name ?? ''; }
  openRoster(s: ShiftDto): void { this.picked = []; this.range = []; this.rostering.set(s); }
  addRoster(): void {
    const s = this.rostering();
    if (!s || !this.picked.length || this.range.length < 2) { this.message.warning('Choose the employees and the dates.'); return; }
    this.shiftsApi.addRoster(s.id, { employeeIds: this.picked, fromDate: toIsoDate(this.range[0]), toDate: toIsoDate(this.range[1]) }).subscribe({
      next: () => { this.message.success(`Roster dates added to ${s.name}.`); this.rostering.set(null); this.load(); },
      error: () => this.message.error('Could not add the roster dates.'),
    });
  }
  removeRoster(r: RosterEntry): void {
    this.shiftsApi.removeRoster(r.id).subscribe({ next: () => this.load(), error: (e) => this.message.error(asProblem(e)?.detail ?? 'Could not remove the roster dates.') });
  }
  edit(s: ShiftDto): void { this.state.edit({ ...s, startTime: s.startTime.slice(0, 5) }); }
  openAssign(s: ShiftDto): void { this.picked = []; this.assigning.set(s); }
  assign(): void {
    const s = this.assigning();
    if (!s || !this.picked.length) { this.assigning.set(null); return; }
    this.shiftsApi.assign(s.id, this.picked).subscribe({
      next: (r) => { this.message.success(`${r.assigned} employee(s) moved to ${s.name}.`); this.assigning.set(null); this.load(); },
      error: () => this.message.error('Could not assign the employees.'),
    });
  }
  unassign(): void {
    this.shiftsApi.unassign(this.picked).subscribe({
      next: (r) => { this.message.success(`${r.cleared} employee(s) taken off their shift.`); this.assigning.set(null); this.load(); },
      error: (e) => this.message.error(asProblem(e)?.detail ?? 'Could not take the employees off their shift.'),
    });
  }
  remove(s: ShiftDto): void {
    this.modal.confirm({
      nzTitle: `Delete ${s.name}?`, nzContent: s.employees ? `${s.employees} employee(s) are on this shift.` : undefined, nzOkText: 'Delete', nzOkDanger: true,
      nzOnOk: () => new Promise<void>((resolve) => this.shiftsApi.remove(s.id).subscribe({
        next: () => { this.message.success(doneText('deleted', 'Shift', s.name)); this.load(); resolve(); },
        error: (e) => { this.message.error(asProblem(e)?.detail ?? 'Could not delete the shift.'); resolve(); },
      })),
    });
  }

  /** Creates or updates a shift. A mistyped start time must not be saved as some other time: it comes back as a field error. */
  private saveShift(id: string | null, v: Record<string, unknown>): Observable<ShiftDto> {
    const startTime = toTime(v['startTime']);
    if (!startTime) return throwError(() => new HttpErrorResponse({ status: 400, error: { errors: { startTime: ['Enter the time as hh:mm, for example 08:00.'] } } }));
    const body = { ...v, startTime };
    return id ? this.shiftsApi.update(id, body) : this.shiftsApi.create(body);
  }
}
