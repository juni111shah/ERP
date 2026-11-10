import { MONTHS } from '@core/api/hr.api';

/** 1 -> "January". */
export function monthName(month: number): string {
  return MONTHS[month - 1];
}

/** (2026, 11) -> "November 2026", the label of a payroll period or a loan's first recovery month. */
export function monthYear(year: number, month: number): string {
  return `${MONTHS[month - 1]} ${year}`;
}

/** "Sara Al Amri" -> "SA"; "E" when there is no name. */
export function initials(name: string): string {
  return (name || '').split(' ').map((n) => n[0]).slice(0, 2).join('').toUpperCase() || 'E';
}

/** The yyyy-MM-dd part of a date value from the record drawer ("2026-11-10T00:00:00" -> "2026-11-10"). */
export function dayText(value: unknown): string {
  return String(value).slice(0, 10);
}
