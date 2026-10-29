import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { asProblem } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { matchText, TableToolsComponent } from '../../shared/table-tools.component';
import { AttendanceApi, AttendanceRow } from './hr.api';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { StatCardComponent } from '../../shared/stat-card.component';
import { NzIconModule } from 'ng-zorro-antd/icon';

/** Monthly attendance: record only the exceptions; employees without a record are paid in full. */
@Component({
  selector: 'erp-attendance',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslatePipe, NzSelectModule, TableToolsComponent, FormsModule, HasPermissionDirective, NzAlertModule, NzButtonModule, NzDatePickerModule, NzInputNumberModule, NzTableModule, NzTagModule, StatCardComponent, NzIconModule],
  styles: [`
    .attendance-table-card {
      background: var(--erp-color-bg-surface, #ffffff);
      border: 1px solid var(--erp-color-border-subtle, #edf2f7);
      border-radius: 12px;
      overflow: hidden;
      box-shadow: 0 1px 3px rgba(0,0,0,0.02);
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
    .emp-name { font-weight: 600; color: var(--erp-color-text-title, #0f172a); font-size: 13px; }
    .emp-sub { display: flex; align-items: center; gap: 6px; font-size: 12px; color: var(--erp-color-text-secondary, #64748b); }
  `],
  template: `
    <h1 class="erp-page-title">Attendance</h1>

    <div class="erp-stat-grid erp-stat-grid--auto" data-testid="attendance-stats">
      <erp-stat-card label="Total staff"
                     [value]="stats().total"
                     format="number"
                     [loading]="loading() && rows().length === 0"
                     caption="active employees"
                     [tone]="0" />
      <erp-stat-card label="Full attendance"
                     [value]="stats().full"
                     format="number"
                     [loading]="loading() && rows().length === 0"
                     caption="standard payout"
                     [tone]="1" />
      <erp-stat-card label="Exceptions recorded"
                     [value]="stats().recorded"
                     format="number"
                     [loading]="loading() && rows().length === 0"
                     caption="leave / absence"
                     [tone]="stats().recorded > 0 ? 2 : 'neutral'" />
      <erp-stat-card label="Overtime hours"
                     [value]="stats().totalOt"
                     format="number"
                     [loading]="loading() && rows().length === 0"
                     caption="extra hours logged"
                     [tone]="3" />
    </div>

    <div style="display:flex;gap:12px;margin:16px 0 12px;align-items:center">
      <nz-date-picker nzMode="month" [(ngModel)]="month" (ngModelChange)="load()" data-testid="att-month"></nz-date-picker>
    </div>

    <nz-alert nzType="info" nzShowIcon nzMessage="Employees without a record for the month are paid in full. Record only exceptions: unpaid absence, paid leave and overtime." style="margin-bottom:12px;border-radius:8px"></nz-alert>

    <erp-table-tools [(search)]="q" (refresh)="load()" [loading]="loading()" testId="att">
      <nz-select [ngModel]="recorded()" (ngModelChange)="recorded.set($event)" nzAllowClear [nzPlaceHolder]="'ui.common.filterStatus' | translate: 'Filter by status'" style="width:170px" data-testid="att-recorded-filter"><nz-option nzValue="recorded" [nzLabel]="'ui.hr.recorded' | translate: 'Recorded'"></nz-option><nz-option nzValue="full" [nzLabel]="'ui.hr.fullAttendance' | translate: 'Full attendance'"></nz-option></nz-select>
    </erp-table-tools>

    <div class="attendance-table-card">
      <nz-table [nzData]="shown()" [nzLoading]="loading()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="middle" data-testid="att-table">
        <thead>
          <tr>
            <th>Employee</th>
            <th>Recorded</th>
            <th>Working days</th>
            <th>Present</th>
            <th>Unpaid absence</th>
            <th>Paid leave</th>
            <th>Approved leave</th>
            <th>Overtime hrs</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          @for (r of shown(); track r.employeeId) {
            <tr [attr.data-testid]="'att-row-' + r.employeeNumber">
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
                <nz-tag [nzColor]="r.recorded ? 'blue' : 'green'">{{ r.recorded ? 'Recorded' : 'Full attendance' }}</nz-tag>
              </td>
              <td><nz-input-number [(ngModel)]="r.workingDays" [nzMin]="1" [nzMax]="31" [nzPrecision]="0" (ngModelChange)="touch(r)" style="width:70px"></nz-input-number></td>
              <td><nz-input-number [(ngModel)]="r.daysPresent" [nzMin]="0" [nzMax]="31" [nzPrecision]="0" (ngModelChange)="touch(r)" style="width:70px"></nz-input-number></td>
              <td><nz-input-number [(ngModel)]="r.unpaidAbsentDays" [nzMin]="0" [nzMax]="31" [nzPrecision]="0" (ngModelChange)="touch(r)" style="width:70px" [attr.data-testid]="'att-unpaid-' + r.employeeNumber"></nz-input-number></td>
              <td><nz-input-number [(ngModel)]="r.paidLeaveDays" [nzMin]="0" [nzMax]="31" [nzPrecision]="0" (ngModelChange)="touch(r)" style="width:70px"></nz-input-number></td>
              <td data-testid="att-leave">@if (r.leavePaidDays || r.leaveUnpaidDays) { <span>{{ r.leavePaidDays ?? 0 }} paid / {{ r.leaveUnpaidDays ?? 0 }} unpaid</span> } @else { <span style="opacity:.5">-</span> }</td>
              <td><nz-input-number [(ngModel)]="r.overtimeHours" [nzMin]="0" [nzPrecision]="1" (ngModelChange)="touch(r)" style="width:75px" [attr.data-testid]="'att-ot-' + r.employeeNumber"></nz-input-number></td>
              <td *erpHasPermission="'hr.attendance.manage'">
                <button nz-button nzSize="small" nzType="primary" [disabled]="!dirty().has(r.employeeId)" (click)="save(r)" [attr.data-testid]="'att-save-' + r.employeeNumber">Save</button>
                @if (r.recordId) { <a style="margin-inline-start:8px;font-size:12px" (click)="remove(r)">Clear</a> }
              </td>
            </tr>
          }
        </tbody>
      </nz-table>
    </div>`,
})
export class AttendancePage {
  private readonly api = inject(AttendanceApi);
  private readonly message = inject(NzMessageService);
  month: Date = new Date();
  readonly rows = signal<AttendanceRow[]>([]);
  readonly q = signal('');
  readonly recorded = signal<'recorded' | 'full' | null>(null);
  /** Client-side filter: GET /hr/attendance only takes the month (the picker above). */
  readonly shown = computed(() => this.rows().filter((r) => matchText(this.q(), r.employeeName, r.employeeNumber, r.designation) && (!this.recorded() || r.recorded === (this.recorded() === 'recorded'))));
  readonly loading = signal(false);
  readonly dirty = signal<ReadonlySet<string>>(new Set());

  readonly stats = computed(() => {
    const all = this.rows();
    const total = all.length;
    const full = all.filter((r) => !r.recorded).length;
    const recorded = all.filter((r) => r.recorded).length;
    const totalOt = all.reduce((sum, r) => sum + (r.overtimeHours || 0), 0);
    return { total, full, recorded, totalOt };
  });

  initials(name: string): string {
    return (name || '').split(' ').map((n) => n[0]).slice(0, 2).join('').toUpperCase() || 'E';
  }

  constructor() { this.load(); }

  load(): void {
    this.loading.set(true); this.dirty.set(new Set());
    this.api.month(this.month.getFullYear(), this.month.getMonth() + 1).subscribe({ next: (r) => { this.rows.set(r); this.loading.set(false); }, error: () => this.loading.set(false) });
  }

  touch(r: AttendanceRow): void { this.dirty.update((s) => new Set(s).add(r.employeeId)); }

  save(r: AttendanceRow): void {
    this.api.save({ employeeId: r.employeeId, year: this.month.getFullYear(), month: this.month.getMonth() + 1, workingDays: r.workingDays, daysPresent: r.daysPresent, unpaidAbsentDays: r.unpaidAbsentDays, paidLeaveDays: r.paidLeaveDays, overtimeHours: r.overtimeHours }).subscribe({
      next: () => { this.message.success('Attendance saved'); this.load(); },
      error: (e) => { const p = asProblem(e); this.message.error(p?.errors ? Object.values(p.errors)[0][0] : (p?.detail ?? p?.title ?? 'Could not save')); },
    });
  }

  remove(r: AttendanceRow): void {
    if (r.recordId) this.api.remove(r.recordId).subscribe({ next: () => { this.message.success('Back to full attendance'); this.load(); } });
  }
}
