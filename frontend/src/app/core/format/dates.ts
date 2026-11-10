/** Dates as the API expects them ("2026-11-10"). Two flavours, because pages differ in which calendar day they mean. */

const pad = (n: number) => String(n).padStart(2, '0');

/** The calendar day the person picked, in their own time zone: a date picker value of 10 Nov stays "2026-11-10". */
export function toIsoDate(d: Date): string {
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

/** The UTC calendar day of a moment (`toISOString()`): near midnight it can differ from {@link toIsoDate}. */
export function toUtcIsoDate(d: Date | string | number): string {
  return new Date(d).toISOString().slice(0, 10);
}
