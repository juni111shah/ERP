import { toIsoDate } from '@core/format/dates';

/** Today as the API writes a date ("2026-11-10"), in the person's own time zone. */
export const isoToday = (): string => toIsoDate(new Date());

/** An API date ("2026-11-10", or the start of a longer string) as a local Date at midnight, for a date picker. */
export function localDate(value?: string | null): Date | null {
  if (!value) return null;
  const [y, m, d] = value.slice(0, 10).split('-').map(Number);
  return new Date(y, m - 1, d);
}

/** Up to two capital letters of a person's name, for an avatar. */
export function initials(name: string): string {
  return name.split(/\s+/).map((n) => n[0]).slice(0, 2).join('').toUpperCase();
}

/** A task's comma-separated labels as a list, without blanks. */
export function splitLabels(labels?: string | null): string[] {
  return (labels ?? '').split(',').map((x) => x.trim()).filter(Boolean);
}
