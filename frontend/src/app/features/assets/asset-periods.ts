/** Depreciation periods: calendar months named in full. */

export const MONTH_NAMES = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];

/** The month before today: the latest period depreciation can be run for. */
export function previousMonth(today = new Date()): { year: number; month: number } {
  const d = new Date(today.getFullYear(), today.getMonth() - 1, 1);
  return { year: d.getFullYear(), month: d.getMonth() + 1 };
}

/** "March 2026". */
export function periodLabel(r: { year: number; month: number }): string {
  return `${MONTH_NAMES[r.month - 1]} ${r.year}`;
}

/** The month picker's options, 1 to 12. */
export const MONTH_OPTIONS = MONTH_NAMES.map((n, i) => ({ value: i + 1, label: n }));
