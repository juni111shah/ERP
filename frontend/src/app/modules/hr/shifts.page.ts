import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { HttpErrorResponse } from '@angular/common/http';
import { catchError, of, throwError } from 'rxjs';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { StatePanelComponent } from '../../shared/state-panel.component';
import { StatusTagComponent } from '../../shared/status-tag.component';
import { asProblem } from '../../core/http/problem-details';
import { toTime } from './daily-attendance.page';
import { RosterEntry, ShiftDto, ShiftsApi } from './hr-people.api';
import { EmployeeSummary, EmployeesApi } from './hr.api';
import { ErpDatePipe } from '../../shared/display.pipes';
import { ErpTableModule } from '../../shared/erp-table.module';

/** Work shifts: start time, late grace and the hours of a normal day; employees on a shift are measured against it. */
@Component({
  selector: 'erp-shifts',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, FormsModule, RecordDrawerComponent, HasPermissionDirective, StatePanelComponent, StatusTagComponent, NzButtonModule, NzIconModule, NzModalModule, NzSelectModule, ErpTableModule, NzDatePickerModule],
  styles: [`.num { text-align: end; } .row-actions { display: flex; gap: 6px; flex-wrap: wrap; } .sr { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }`],
  template: `
    <h1 class="erp-page-title">Shifts</h1>
    <div style="margin-bottom:12px">
      <button *erpHasPermission="'hr.attendance.manage'" nz-button nzType="primary" (click)="state.create()" data-testid="shift-new"><span nz-icon nzType="plus"></span>New shift</button>
    </div>
    @if (failed()) {
      <erp-state-panel kind="error" title="Shifts could not be loaded" hint="Check your connection and try again." actionLabel="Try again" (action)="load()" />
    } @else {
    <nz-table [nzData]="rows()" [nzLoading]="loading()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" nzNoResult="No shifts yet. Everyone uses the shift times in the HR settings." data-testid="shift-table">
      <thead><tr><th>Code</th><th>Name</th><th>Starts</th><th class="num">Grace (min)</th><th class="num">Hours a day</th><th class="num">Employees</th><th>Status</th><th><span class="sr">Actions</span></th></tr></thead>
      <tbody>
        @for (s of rows(); track s.id) {
          <tr>
            <td>{{ s.code }}</td><td>{{ s.name }}</td><td>{{ s.startTime.slice(0, 5) }}</td><td class="num">{{ s.graceMinutes }}</td><td class="num">{{ s.hoursPerDay }}</td><td class="num">{{ s.employees }}</td><td><erp-status [value]="s.isActive ? 'active' : 'inactive'" /></td>
            <td *erpHasPermission="'hr.attendance.manage'">
              <div class="row-actions">
                <button nz-button nzSize="small" (click)="edit(s)" [attr.data-testid]="'shift-edit-' + s.code">Edit</button>
                <button nz-button nzSize="small" (click)="openAssign(s)" [attr.data-testid]="'shift-assign-' + s.code">Assign employees</button>
                <button nz-button nzSize="small" (click)="openRoster(s)" [attr.data-testid]="'shift-roster-' + s.code">Roster dates</button>
                <button nz-button nzSize="small" nzDanger (click)="remove(s)" [attr.data-testid]="'shift-delete-' + s.code">Delete</button>
              </div>
            </td>
          </tr>
        }
      </tbody>
    </nz-table>
    <h2 style="margin-top:24px">Dated roster this month</h2>
    <nz-table [nzData]="roster()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" nzNoResult="No dated shifts this month. Use Roster dates on a shift to add some." data-testid="roster-table">
      <thead><tr><th>Employee</th><th>Shift</th><th>From</th><th>To</th><th><span class="sr">Actions</span></th></tr></thead>
      <tbody>
        @for (r of roster(); track r.id) {
          <tr><td>{{ employeeName(r.employeeId) }}</td><td>{{ shiftName(r.shiftId) }}</td><td>{{ r.fromDate | erpDate }}</td><td>{{ r.toDate | erpDate }}</td>
            <td *erpHasPermission="'hr.attendance.manage'"><button nz-button nzSize="small" nzDanger (click)="removeRoster(r)">Remove</button></td></tr>
        }
      </tbody>
    </nz-table>
    }
    <erp-record-drawer [title]="state.editing() ? 'Edit shift' : 'New shift'" [open]="state.open()" [fields]="fields" [value]="$any(state.editing())"
                       [saving]="state.saving()" [errors]="state.errors()" testId="shift-form" (submitted)="state.submit($event)" (closed)="state.close()" />
    <nz-modal [nzVisible]="assigning() !== null" [nzTitle]="'Assign employees to ' + (assigning()?.name ?? '')" nzOkText="Assign" (nzOnOk)="assign()" (nzOnCancel)="assigning.set(null)">
      <ng-container *nzModalContent>
        <label class="sr" for="shift-employees">Employees</label>
        <nz-select nzId="shift-employees" style="width:100%" nzMode="multiple" nzShowSearch nzPlaceHolder="Choose employees" [(ngModel)]="picked" data-testid="shift-employees">
          @for (e of employees().items; track e.id) { <nz-option [nzValue]="e.id" [nzLabel]="e.number + ' · ' + e.fullName"></nz-option> }
        </nz-select>
        <p style="margin-top:8px;color:var(--erp-color-text-secondary)">Employees leave any shift they are on now. Late arrival and overtime follow the new shift from the next entry.</p>
        <button nz-button nzType="link" type="button" style="padding-inline:0" [disabled]="!picked.length" (click)="unassign()" data-testid="shift-unassign">Take the chosen employees off their shift</button>
      </ng-container>
    </nz-modal>
    <nz-modal [nzVisible]="rostering() !== null" [nzTitle]="'Roster dates for ' + (rostering()?.name ?? '')" nzOkText="Add" (nzOnOk)="addRoster()" (nzOnCancel)="rostering.set(null)">
      <ng-container *nzModalContent>
        <label class="sr" for="roster-employees">Employees</label>
        <nz-select nzId="roster-employees" style="width:100%;margin-bottom:8px" nzMode="multiple" nzShowSearch nzPlaceHolder="Choose employees" [(ngModel)]="picked" data-testid="roster-employees">
          @for (e of employees().items; track e.id) { <nz-option [nzValue]="e.id" [nzLabel]="e.number + ' · ' + e.fullName"></nz-option> }
        </nz-select>
        <nz-range-picker style="width:100%" [(ngModel)]="range" data-testid="roster-range"></nz-range-picker>
        <p style="margin-top:8px;color:var(--erp-color-text-secondary)">On these dates the shift replaces the employee's default shift.</p>
      </ng-container>
    </nz-modal>`,
})
export class ShiftsPage {
  private readonly api = inject(ShiftsApi);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  readonly loading = signal(false);
  readonly failed = signal(false);
  readonly employees = toSignal(inject(EmployeesApi).list({ page: 1, pageSize: 200, sort: 'number' }).pipe(catchError(() => of({ items: [] as EmployeeSummary[] }))), { initialValue: { items: [] as EmployeeSummary[] } });
  readonly rows = signal<ShiftDto[]>([]);
  readonly assigning = signal<ShiftDto | null>(null);
  readonly rostering = signal<ShiftDto | null>(null);
  readonly roster = signal<RosterEntry[]>([]);
  picked: string[] = [];
  range: Date[] = [];

  readonly state = new MasterDataState<ShiftDto>({
    save: (id, body) => {
      const v = body as Record<string, unknown>;
      const startTime = toTime(v['startTime']);
      // A mistyped time must not be saved as some other time.
      if (!startTime) return throwError(() => new HttpErrorResponse({ status: 400, error: { errors: { startTime: ['Enter the time as hh:mm, for example 08:00.'] } } }));
      const b = { ...v, startTime };
      return id ? this.api.update(id, b) : this.api.create(b);
    },
    reload: () => this.load(), message: this.message, entityName: 'Shift',
  });
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
    this.api.list().subscribe({ next: (r) => { this.rows.set(r); this.loading.set(false); }, error: () => { this.loading.set(false); this.failed.set(true); } });
    const now = new Date();
    this.api.roster(now.getFullYear(), now.getMonth() + 1).subscribe((r) => this.roster.set(r));
  }
  employeeName(id: string): string { return this.employees().items.find((e) => e.id === id)?.fullName ?? ''; }
  shiftName(id: string): string { return this.rows().find((s) => s.id === id)?.name ?? ''; }
  openRoster(s: ShiftDto): void { this.picked = []; this.range = []; this.rostering.set(s); }
  addRoster(): void {
    const s = this.rostering();
    if (!s || !this.picked.length || this.range.length < 2) { this.message.warning('Choose the employees and the dates.'); return; }
    const iso = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    this.api.addRoster(s.id, { employeeIds: this.picked, fromDate: iso(this.range[0]), toDate: iso(this.range[1]) }).subscribe({
      next: () => { this.message.success('Roster dates added.'); this.rostering.set(null); this.load(); },
      error: () => this.message.error('Could not add the roster dates.'),
    });
  }
  removeRoster(r: RosterEntry): void {
    this.api.removeRoster(r.id).subscribe({ next: () => this.load(), error: (e) => this.message.error(asProblem(e)?.detail ?? 'Could not remove the roster dates.') });
  }
  edit(s: ShiftDto): void { this.state.edit({ ...s, startTime: s.startTime.slice(0, 5) } as ShiftDto); }
  openAssign(s: ShiftDto): void { this.picked = []; this.assigning.set(s); }
  assign(): void {
    const s = this.assigning();
    if (!s || !this.picked.length) { this.assigning.set(null); return; }
    this.api.assign(s.id, this.picked).subscribe({
      next: (r) => { this.message.success(`${r.assigned} employee(s) moved to ${s.name}.`); this.assigning.set(null); this.load(); },
      error: () => this.message.error('Could not assign the employees.'),
    });
  }
  unassign(): void {
    this.api.unassign(this.picked).subscribe({
      next: (r) => { this.message.success(`${r.cleared} employee(s) taken off their shift.`); this.assigning.set(null); this.load(); },
      error: (e) => this.message.error(asProblem(e)?.detail ?? 'Could not take the employees off their shift.'),
    });
  }
  remove(s: ShiftDto): void {
    this.modal.confirm({
      nzTitle: `Delete ${s.name}?`, nzContent: s.employees ? `${s.employees} employee(s) are on this shift.` : undefined, nzOkText: 'Delete', nzOkDanger: true,
      nzOnOk: () => new Promise<void>((resolve) => this.api.remove(s.id).subscribe({
        next: () => { this.message.success('Shift deleted.'); this.load(); resolve(); },
        error: (e) => { this.message.error(asProblem(e)?.detail ?? 'Could not delete the shift.'); resolve(); },
      })),
    });
  }
}
