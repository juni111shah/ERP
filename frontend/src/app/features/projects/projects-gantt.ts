import { ScheduleDto, ScheduleMilestoneDto, ScheduleTaskDto } from '@core/api/projects-extra.api';
import { toUtcIsoDate } from '@core/format/dates';

/** The timeline's geometry: one day in milliseconds, the height of a row and of the two scales above the rows. */
export const DAY = 864e5;
export const ROW = 34;
export const HEAD = 46;

/** A task's bar, and its baseline bar when one was saved. */
export interface Bar { task: ScheduleTaskDto; top: number; left: number; width: number; baseLeft: number | null; baseWidth: number; }
/** A cell of the lower scale (a day, or the start of a week). */
export interface Column { left: number; width: number; label: string; off: boolean; }
/** A month on the upper scale. */
export interface Band { left: number; width: number; label: string; }
/** The first and last day on the axis, in UTC milliseconds. */
export interface GanttRange { from: number; to: number; }

/** An API date ("2026-11-10...") as UTC milliseconds at midnight. */
export const parseDay = (s: string): number => { const [y, m, d] = s.slice(0, 10).split('-').map(Number); return Date.UTC(y, m - 1, d); };
/** UTC milliseconds as an API date. */
export const formatDay = (t: number): string => toUtcIsoDate(t);

/** The pixels from the start of the axis to a day. */
export const dayLeft = (day: number, range: GanttRange, zoom: number): number => ((day - range.from) / DAY) * zoom;

/** The axis around the plan and today, with three days of space before and a week after. */
export function ganttRange(schedule: ScheduleDto, today: number): GanttRange {
  const from = Math.min(parseDay(schedule.start), today);
  const to = Math.max(parseDay(schedule.finish), today);
  return { from: from - 3 * DAY, to: to + 7 * DAY };
}

/** The width of the whole axis in pixels. */
export const ganttWidth = (range: GanttRange, zoom: number): number => Math.round((range.to - range.from) / DAY + 1) * zoom;

/** Parents followed by their subtasks, the rest in plan order. */
export function orderedTasks(tasks: ScheduleTaskDto[]): ScheduleTaskDto[] {
  const tops = tasks.filter((t) => !t.parentTaskId).sort((a, b) => parseDay(a.start) - parseDay(b.start) || a.number.localeCompare(b.number));
  return tops.flatMap((t) => [t, ...tasks.filter((k) => k.parentTaskId === t.id).sort((a, b) => parseDay(a.start) - parseDay(b.start))]);
}

/** One bar per task, a row each; a bar is never narrower than most of a day. */
export function ganttBars(tasks: ScheduleTaskDto[], range: GanttRange, zoom: number): Bar[] {
  return orderedTasks(tasks).map((task, i) => ({
    task, top: HEAD + i * ROW,
    left: dayLeft(parseDay(task.start), range, zoom), width: Math.max(zoom * 0.6, ((parseDay(task.end) - parseDay(task.start)) / DAY + 1) * zoom),
    baseLeft: task.baselineStart ? dayLeft(parseDay(task.baselineStart), range, zoom) : null,
    baseWidth: task.baselineStart && task.baselineEnd ? ((parseDay(task.baselineEnd) - parseDay(task.baselineStart)) / DAY + 1) * zoom : 0,
  }));
}

/** Each milestone with its place on the axis. */
export function milestonePoints(milestones: ScheduleMilestoneDto[], range: GanttRange, zoom: number): (ScheduleMilestoneDto & { left: number })[] {
  return milestones.map((m) => ({ ...m, left: dayLeft(parseDay(m.date), range, zoom) }));
}

/** The lower scale: days when there is room, otherwise the start of each week. Days off are shaded. */
export function ganttColumns(range: GanttRange, zoom: number, workDays: number[]): Column[] {
  const out: Column[] = [];
  for (let t = range.from, i = 0; t <= range.to; t += DAY, i++) {
    const d = new Date(t);
    const off = !workDays.includes(d.getUTCDay());
    if (zoom >= 24) out.push({ left: i * zoom, width: zoom, label: String(d.getUTCDate()), off });
    else if (zoom >= 10) { if (d.getUTCDay() === 1 || off) out.push({ left: i * zoom, width: zoom, label: d.getUTCDay() === 1 ? String(d.getUTCDate()) : '', off }); }
    else if (d.getUTCDay() === 1) out.push({ left: i * zoom, width: 7 * zoom, label: '', off: false });
  }
  return out;
}

/** The upper scale: months. */
export function ganttBands(range: GanttRange, zoom: number): Band[] {
  const out: Band[] = [];
  let start = range.from;
  while (start <= range.to) {
    const d = new Date(start);
    const next = Date.UTC(d.getUTCFullYear(), d.getUTCMonth() + 1, 1);
    const end = Math.min(next, range.to + DAY);
    out.push({ left: dayLeft(start, range, zoom), width: ((end - start) / DAY) * zoom, label: d.toLocaleDateString(undefined, { month: 'short', year: 'numeric', timeZone: 'UTC' }) });
    start = next;
  }
  return out;
}

/** One SVG path from the end of each task to the start of the task that waits for it. */
export function dependencyArrows(bars: Bar[]): string[] {
  const byId = new Map(bars.map((b) => [b.task.id, b]));
  const out: string[] = [];
  for (const b of bars) {
    for (const dep of b.task.dependsOn) {
      const from = byId.get(dep); if (!from) continue;
      const x1 = from.left + from.width; const y1 = from.top + 16; const x2 = b.left; const y2 = b.top + 16;
      const mid = x2 >= x1 + 12 ? x2 - 8 : x1 + 8;
      out.push(x2 >= x1 + 12 ? `M${x1},${y1} H${mid} V${y2} H${x2 - 1}` : `M${x1},${y1} H${mid} V${y1 + 14} H${x2 - 10} V${y2} H${x2 - 1}`);
    }
  }
  return out;
}
