/**
 * "2026-11-10" -> 10 Nov 2026 at local midnight, for a date picker (the reverse of `toIsoDate`). `dayOffset` moves the day, so
 * `fromIsoDate(end, 1)` is the day after `end`. `new Date("2026-11-10")` would be UTC midnight and can show the day before.
 */
export function fromIsoDate(iso: string, dayOffset = 0): Date {
  const [y, m, d] = iso.split('-').map(Number);
  return new Date(y, m - 1, d + dayOffset);
}
