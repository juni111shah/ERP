import { AttendanceInput, AttendanceRow } from '@core/api/hr.api';

/** The editable figures of a monthly attendance row. */
export type GridField = 'workingDays' | 'daysPresent' | 'unpaidAbsentDays' | 'paidLeaveDays' | 'overtimeHours';

/** One number input of the attendance grid. */
export interface GridInput { key: GridField; label: string; min: number; max?: number; precision: number; step?: number; testId?: string; }

/** The inputs of a row, in column order. The approved-leave column sits between paid leave and overtime. */
export const GRID_INPUTS: GridInput[] = [
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

/** The KPI cards of the month: employees, on full attendance, with exceptions, and the overtime hours. */
export interface AttendanceStats { total: number; full: number; exceptions: number; totalOt: number; }

export function attendanceStats(rows: AttendanceRow[]): AttendanceStats {
  const exceptions = rows.filter(hasException).length;
  const totalOt = rows.reduce((sum, r) => sum + (r.overtimeHours || 0), 0);
  return { total: rows.length, full: rows.length - exceptions, exceptions, totalOt };
}

/** What is saved for a row of the month shown in `month`. */
export function attendanceInput(r: AttendanceRow, month: Date): AttendanceInput {
  return {
    employeeId: r.employeeId, year: month.getFullYear(), month: month.getMonth() + 1, workingDays: r.workingDays, daysPresent: r.daysPresent,
    unpaidAbsentDays: r.unpaidAbsentDays, paidLeaveDays: r.paidLeaveDays, overtimeHours: r.overtimeHours,
  };
}
