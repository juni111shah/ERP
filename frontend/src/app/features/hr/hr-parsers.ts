import { DailyInput } from '@core/api/hr-people.api';

/** One step of an onboarding or offboarding checklist. */
export interface ChecklistItem { title: string; dueOffsetDays: number; }

/** One band of a slab (progressive) statutory rule. */
export interface StatutorySlab { fromAmount: number; rate: number; }

/** One step per line, "title | days": days after the join date (onboarding) or before the last day (offboarding, negative). */
export function parseItems(text: unknown): ChecklistItem[] {
  return String(text ?? '').split(/\r?\n/).map((l) => l.trim()).filter(Boolean).map((line) => {
    const [title, days] = line.split('|').map((p) => p.trim());
    const n = Number(days ?? 0);
    return { title, dueOffsetDays: Number.isFinite(n) ? Math.trunc(n) : 0 };
  }).filter((i) => i.title);
}

/** The reverse of {@link parseItems}: one "title | days" line per step. */
export function itemsText(items: ChecklistItem[] | undefined): string {
  return (items ?? []).map((i) => `${i.title} | ${i.dueOffsetDays}`).join('\n');
}

/** "1000:10, 3000:20" -> [{ fromAmount: 1000, rate: 10 }, ...]; blank -> []. */
export function parseSlabs(text: unknown): StatutorySlab[] {
  return String(text ?? '').split(',').map((p) => p.trim()).filter(Boolean)
    .map((p) => p.split(':').map((n) => Number(n.trim())))
    .filter((p) => p.length === 2 && p.every((n) => Number.isFinite(n)))
    .map(([fromAmount, rate]) => ({ fromAmount, rate }));
}

/** The reverse of {@link parseSlabs}: "1000:10, 3000:20". */
export function slabsText(slabs: StatutorySlab[] | undefined): string {
  return (slabs ?? []).map((s) => `${s.fromAmount}:${s.rate}`).join(', ');
}

/** "8:5" or "08:05" -> "08:05:00"; blank -> null; anything else -> undefined (invalid). */
export function toTime(text: unknown): string | null | undefined {
  const t = String(text ?? '').trim();
  if (!t) return null;
  const m = /^(\d{1,2}):(\d{2})(?::(\d{2}))?$/.exec(t);
  if (!m || Number(m[1]) > 23 || Number(m[2]) > 59) return undefined;
  return `${m[1].padStart(2, '0')}:${m[2]}:${m[3] ?? '00'}`;
}

/** Lines "EMP-001,2026-10-05,08:00,17:30" (number, date, in, out) -> rows plus the lines that could not be read. */
export function parseImport(text: string, idByNumber: ReadonlyMap<string, string>): { rows: DailyInput[]; errors: string[] } {
  const rows: DailyInput[] = []; const errors: string[] = [];
  text.split(/\r?\n/).map((l) => l.trim()).filter(Boolean).forEach((line, i) => {
    const [number, date, inText, outText] = line.split(',').map((p) => p.trim());
    const employeeId = idByNumber.get((number ?? '').toLowerCase());
    const checkIn = toTime(inText); const checkOut = toTime(outText);
    if (!employeeId) errors.push(`Line ${i + 1}: unknown employee "${number}"`);
    else if (!/^\d{4}-\d{2}-\d{2}$/.test(date ?? '')) errors.push(`Line ${i + 1}: date must be yyyy-mm-dd`);
    else if (checkIn === undefined || checkOut === undefined) errors.push(`Line ${i + 1}: time must be hh:mm`);
    else rows.push({ employeeId, date, checkIn, checkOut, source: 'import' });
  });
  return { rows, errors };
}
