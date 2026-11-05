import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { catchError, of } from 'rxjs';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { MasterDataState } from '../../shared/master-data-state';
import { FieldSpec, RecordDrawerComponent } from '../../shared/record-drawer.component';
import { toTime } from './daily-attendance.page';
import { ShiftDto, ShiftsApi } from './hr-people.api';
import { EmployeeSummary, EmployeesApi } from './hr.api';

/** Work shifts: start time, late grace and the hours of a normal day; employees on a shift are measured against it. */
@Component({
  selector: 'erp-shifts',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, RecordDrawerComponent, HasPermissionDirective, NzButtonModule, NzIconModule, NzModalModule, NzSelectModule, NzTableModule],
  template: `
    <h1 class="erp-page-title">Shifts</h1>
    <div style="margin-bottom:12px">
      <button *erpHasPermission="'hr.attendance.manage'" nz-button nzType="primary" (click)="state.create()" data-testid="shift-new"><span nz-icon nzType="plus"></span>New shift</button>
    </div>
    <nz-table [nzData]="rows()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="shift-table">
      <thead><tr><th>Code</th><th>Name</th><th>Starts</th><th>Grace (min)</th><th>Hours a day</th><th>Employees</th><th>Active</th><th></th></tr></thead>
      <tbody>
        @for (s of rows(); track s.id) {
          <tr>
            <td>{{ s.code }}</td><td>{{ s.name }}</td><td>{{ s.startTime.slice(0, 5) }}</td><td>{{ s.graceMinutes }}</td><td>{{ s.hoursPerDay }}</td><td>{{ s.employees }}</td><td>{{ s.isActive ? 'Yes' : 'No' }}</td>
            <td *erpHasPermission="'hr.attendance.manage'">
              <button nz-button nzSize="small" (click)="edit(s)" [attr.data-testid]="'shift-edit-' + s.code">Edit</button>
              <button nz-button nzSize="small" (click)="openAssign(s)" [attr.data-testid]="'shift-assign-' + s.code">Assign employees</button>
              <button nz-button nzSize="small" nzDanger (click)="remove(s)" [attr.data-testid]="'shift-delete-' + s.code">Delete</button>
            </td>
          </tr>
        } @empty { <tr><td colspan="8">No shifts yet. Everyone uses the shift times in the HR settings.</td></tr> }
      </tbody>
    </nz-table>
    <erp-record-drawer [title]="state.editing() ? 'Edit shift' : 'New shift'" [open]="state.open()" [fields]="fields" [value]="$any(state.editing())"
                       [saving]="state.saving()" [errors]="state.errors()" testId="shift-form" (submitted)="state.submit($event)" (closed)="state.close()" />
    <nz-modal [nzVisible]="assigning() !== null" [nzTitle]="'Assign employees to ' + (assigning()?.name ?? '')" nzOkText="Assign" (nzOnOk)="assign()" (nzOnCancel)="assigning.set(null)">
      <ng-container *nzModalContent>
        <nz-select style="width:100%" nzMode="multiple" nzShowSearch nzPlaceHolder="Choose employees" aria-label="Employees" [(ngModel)]="picked" data-testid="shift-employees">
          @for (e of employees().items; track e.id) { <nz-option [nzValue]="e.id" [nzLabel]="e.number + ' · ' + e.fullName"></nz-option> }
        </nz-select>
        <p style="margin-top:8px;color:var(--erp-color-text-secondary)">Employees leave any shift they are on now. Late arrival and overtime follow the new shift from the next entry.</p>
      </ng-container>
    </nz-modal>`,
})
export class ShiftsPage {
  private readonly api = inject(ShiftsApi);
  private readonly message = inject(NzMessageService);
  readonly employees = toSignal(inject(EmployeesApi).list({ page: 1, pageSize: 200, sort: 'number' }).pipe(catchError(() => of({ items: [] as EmployeeSummary[] }))), { initialValue: { items: [] as EmployeeSummary[] } });
  readonly rows = signal<ShiftDto[]>([]);
  readonly assigning = signal<ShiftDto | null>(null);
  picked: string[] = [];

  readonly state = new MasterDataState<ShiftDto>({
    save: (id, body) => { const b = this.toBody(body as Record<string, unknown>); return id ? this.api.update(id, b) : this.api.create(b); },
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

  load(): void { this.api.list().subscribe((r) => this.rows.set(r)); }
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
  remove(s: ShiftDto): void {
    this.api.remove(s.id).subscribe({ next: () => { this.message.success('Shift deleted.'); this.load(); }, error: (e) => this.message.error(e?.error?.detail ?? 'Could not delete the shift.') });
  }

  private toBody(v: Record<string, unknown>): Record<string, unknown> {
    return { ...v, startTime: toTime(v['startTime']) ?? '08:00:00' };
  }
}
