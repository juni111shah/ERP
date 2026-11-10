import { WeekEntry } from '@core/api/projects-extra.api';
import { TimesheetDto } from '@core/api/projects.api';
import { statusLabel } from './projects-ui';

/** One day of one row. A day with several entries is a sum and is edited in Timesheets; hours on the entry itself are what the grid changes. */
export interface WeekCell { entries: TimesheetDto[]; hours: number; original: number; }
/** A row of the week grid: one project, or one task of it, with a cell per day. */
export interface WeekRow { key: string; projectId: string; taskId: string | null; label: string; cells: Record<string, WeekCell>; }
/** A running timer, kept in the browser so it survives a reload. */
export interface WeekTimer { projectId: string; taskId: string | null; label: string; startedAt: number; startedLabel: string; }

const TIMER_KEY = 'erp.projects.timer';

/** The first working day of the week: Sunday where the week is Sunday to Thursday, else Monday. */
export function firstWeekDay(workDays: number[] | null | undefined): number {
  return workDays?.includes(0) && !workDays.includes(6) ? 0 : 1;
}

/** Midnight of the day the week of `d` starts on. */
export function startOfWeek(d: Date, firstDay: number): Date {
  const x = new Date(d.getFullYear(), d.getMonth(), d.getDate());
  x.setDate(x.getDate() - ((x.getDay() - firstDay + 7) % 7));
  return x;
}

/** A date moved by a number of days. */
export function addDays(d: Date, days: number): Date {
  const x = new Date(d);
  x.setDate(x.getDate() + days);
  return x;
}

/** The key of the row for a project and task. */
export const rowKey = (projectId: string, taskId: string | null | undefined): string => `${projectId}|${taskId ?? ''}`;

/** A row's label: the project, and the task when there is one. */
export const entryLabel = (t: { projectName: string; taskTitle?: string | null }): string => t.projectName + (t.taskTitle ? ' · ' + t.taskTitle : '');

/** An empty row for the project and task of a time entry. */
export const emptyRowFor = (t: TimesheetDto): WeekRow => ({ key: rowKey(t.projectId, t.taskId), projectId: t.projectId, taskId: t.taskId ?? null, label: entryLabel(t), cells: {} });

/** The week's entries as grid rows: one per project and task, the hours of a day summed. */
export function weekRows(entries: TimesheetDto[]): WeekRow[] {
  const rows = new Map<string, WeekRow>();
  for (const t of entries) {
    const row = rows.get(rowKey(t.projectId, t.taskId)) ?? emptyRowFor(t);
    const cell = row.cells[t.date] ?? { entries: [], hours: 0, original: 0 };
    row.cells[t.date] = { entries: [...cell.entries, t], hours: cell.hours + t.hours, original: cell.original + t.hours };
    rows.set(row.key, row);
  }
  return [...rows.values()];
}

/** Empty rows for the projects and tasks of the entries that are not in `known` yet, one per row key. */
export function newRows(entries: TimesheetDto[], known: Set<string>): WeekRow[] {
  const fresh = new Map<string, WeekRow>();
  for (const t of entries) {
    const key = rowKey(t.projectId, t.taskId);
    if (!known.has(key) && !fresh.has(key)) fresh.set(key, emptyRowFor(t));
  }
  return [...fresh.values()];
}

/** Why a day cannot be typed in, or empty when it can. */
export function cellLockReason(c: WeekCell | undefined, timesheetApproval: boolean | undefined): string {
  if (!c?.entries.length) return '';
  if (c.entries.length > 1) return `${c.entries.length} entries on this day. Change them in Timesheets.`;
  const e = c.entries[0];
  if (e.isBilled) return 'Already billed';
  if (e.writtenOff) return 'Written off';
  if (e.approvalStatus === 'submitted') return 'Waiting for approval';
  if (e.approvalStatus === 'approved' && timesheetApproval) return 'Approved';
  return '';
}

/** The changed days that can still be saved, as week entries of the person. */
export function changedEntries(rows: WeekRow[], workerUserId: string | null, timesheetApproval: boolean | undefined): WeekEntry[] {
  const items: WeekEntry[] = [];
  for (const r of rows)
    for (const [date, c] of Object.entries(r.cells))
      if (c.hours !== c.original && !cellLockReason(c, timesheetApproval)) items.push({ id: c.entries[0]?.id, projectId: r.projectId, taskId: r.taskId, date, workerUserId, hours: c.hours, isBillable: true });
  return items;
}

/** How many entries are in each approval state, in words ("2 draft · 1 approved"). */
export function approvalSummary(entries: TimesheetDto[]): string {
  if (!entries.length) return 'Nothing logged';
  const count = (s: string) => entries.filter((e) => e.approvalStatus === s).length;
  return (['draft', 'submitted', 'approved', 'rejected'] as const).filter((s) => count(s)).map((s) => `${count(s)} ${statusLabel('approval', s).toLowerCase()}`).join(' · ');
}

/** Time since a moment as "1h 05m". */
export function elapsedText(since: number, now: number): string {
  const s = Math.floor((now - since) / 1000);
  return `${Math.floor(s / 3600)}h ${String(Math.floor((s % 3600) / 60)).padStart(2, '0')}m`;
}

/** The hours a timer ran, to the nearest quarter hour and at least one quarter. */
export function timerHours(since: number, now: number): number {
  return Math.max(0.25, Math.round(((now - since) / 3600000) * 4) / 4);
}

/** The saved timer, or null when there is none or storage is blocked. */
export function readTimer(): WeekTimer | null {
  try { const raw = localStorage.getItem(TIMER_KEY); return raw ? (JSON.parse(raw) as WeekTimer) : null; } catch { return null; }
}

/** Saves the timer, or forgets it when null. */
export function writeTimer(t: WeekTimer | null): void {
  try { if (t) localStorage.setItem(TIMER_KEY, JSON.stringify(t)); else localStorage.removeItem(TIMER_KEY); } catch { /* storage may be blocked; the timer then lasts until the page closes */ }
}
