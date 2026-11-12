import { AcceptanceDto, AllocationDto, BudgetLineDto, ExpenseDto } from '@core/api/projects-extra.api';
import { MilestoneDto, ProjectDto, TaskDto, TimesheetDto } from '@core/api/projects.api';
import { toIsoDate } from '@core/format/dates';
import { addDays, startOfWeek } from './projects-week';

/** A time entry or an expense waiting for approval, as one line of the approvals inbox. */
export interface ApprovalLine { id: string; person: string; date: string; project: string; projectId: string; what: string; hours: number | null; amount: number; }
/** The lines of one person, with their totals. */
export interface ApprovalGroup { person: string; lines: ApprovalLine[]; hours: number; amount: number; }

/** Time entries as approval lines: the task and description say what was done. */
export function timeApprovalLines(rows: TimesheetDto[]): ApprovalLine[] {
  return rows.map((r) => ({ id: r.id, person: r.workerName, date: r.date, project: r.projectName, projectId: r.projectId, what: [r.taskTitle, r.description].filter(Boolean).join(' · ') || 'Project work', hours: r.hours, amount: r.totalAmount }));
}

/** Expenses as approval lines. */
export function expenseApprovalLines(rows: ExpenseDto[]): ApprovalLine[] {
  return rows.map((r) => ({ id: r.id, person: r.workerName || 'Not stated', date: r.date, project: r.projectName, projectId: r.projectId, what: `${r.category} · ${r.description}`, hours: null, amount: r.amount }));
}

/** Approval lines grouped by person, people in name order. */
export function groupByPerson(lines: ApprovalLine[]): ApprovalGroup[] {
  const by = new Map<string, ApprovalLine[]>();
  for (const l of lines) by.set(l.person, [...(by.get(l.person) ?? []), l]);
  return [...by.entries()].sort(([a], [b]) => a.localeCompare(b))
    .map(([person, group]) => ({ person, lines: group, hours: group.reduce((a, l) => a + (l.hours ?? 0), 0), amount: group.reduce((a, l) => a + l.amount, 0) }));
}

/** One line of a project's "Needs attention" card, and the tab that deals with it. */
export interface AttentionItem { text: string; tab: string; }

const plural = (n: number, one: string, many: string): string => (n === 1 ? one : many);
const whole = (n: number): string => n.toLocaleString('en-US', { maximumFractionDigits: 0 });

/** What needs attention on a project: overdue tasks, overspend, a missed end date, work to bill, late milestones and declined sign-offs. */
export function attentionItems(p: ProjectDto, milestones: MilestoneDto[], acceptances: AcceptanceDto[], currency: string, todayIso: string): AttentionItem[] {
  const out: AttentionItem[] = [];
  if (p.overdueTasks) out.push({ text: `${p.overdueTasks} overdue ${plural(p.overdueTasks, 'task', 'tasks')}`, tab: 'tasks' });
  if (p.effectiveBudget > 0 && p.totalTimesheetCost > p.effectiveBudget) out.push({ text: `Over budget by ${currency} ${whole(p.totalTimesheetCost - p.effectiveBudget)}`, tab: 'money' });
  if (p.endDate && p.endDate < todayIso && p.openTasks) out.push({ text: `Past the target end date with ${p.openTasks} open ${plural(p.openTasks, 'task', 'tasks')}`, tab: 'tasks' });
  if (p.unbilledAmount > 0) out.push({ text: `${currency} ${whole(p.unbilledAmount)} ready to bill`, tab: 'money' });
  const late = milestones.filter((m) => m.status === 'pending' && m.dueDate < todayIso).length;
  if (late) out.push({ text: `${late} ${plural(late, 'milestone is', 'milestones are')} past due`, tab: 'milestones' });
  const declined = acceptances.filter((a) => a.status === 'declined').length;
  if (declined) out.push({ text: `The customer declined ${declined} sign-off${plural(declined, '', 's')}`, tab: 'milestones' });
  return out;
}

/** The next six open tasks with a due date, soonest first. */
export function nextDueTasks(tasks: TaskDto[]): TaskDto[] {
  return tasks.filter((t) => t.dueDate && !['done', 'cancelled'].includes(t.status)).sort((a, b) => a.dueDate!.localeCompare(b.dueDate!)).slice(0, 6);
}

/** Where a project came from, in words: a sales order, an opportunity, or the source type as sent. */
export function projectSourceLabel(sourceType: string | null | undefined): string {
  return !sourceType ? '' : sourceType.startsWith('sales') ? 'A sales order' : sourceType.startsWith('crm') ? 'An opportunity' : sourceType;
}

/** The milestones line: their total (and the fixed fee it is part of) and what is invoiced. Cancelled milestones do not count. */
export function milestonesSummary(rows: MilestoneDto[], project: ProjectDto, currency: string): string {
  const live = rows.filter((m) => m.status !== 'cancelled');
  const total = live.reduce((a, m) => a + m.amount, 0);
  const invoiced = live.filter((m) => m.status === 'invoiced').reduce((a, m) => a + m.amount, 0);
  const fee = project.billingType === 'fixedFee' ? ` of the ${currency} ${project.effectiveBudget.toLocaleString('en-US')} fee` : '';
  return `${currency} ${total.toLocaleString('en-US')}${fee} in milestones · ${currency} ${invoiced.toLocaleString('en-US')} invoiced`;
}

/** How much of a budget line is spent, in percent; spending without a budget counts as over (101). */
export function budgetLineShare(l: BudgetLineDto): number {
  return l.amount > 0 ? (l.actual / l.amount) * 100 : (l.actual > 0 ? 101 : 0);
}

/** A week of the capacity heatmap. */
export interface PlanWeek { key: string; from: string; to: string; label: string; }
/** One person's planned hours per week. */
export interface HeatRow { userId: string; name: string; cells: { key: string; hours: number; ratio: number; hint: string }[]; }

/** `count` weeks from the one `offset` weeks away from this one, each starting on `firstDay`. */
export function planWeeks(firstDay: number, offset: number, count: number): PlanWeek[] {
  const start = addDays(startOfWeek(new Date(), firstDay), offset * 7);
  return Array.from({ length: count }, (_, i) => {
    const from = addDays(start, i * 7);
    const to = addDays(from, 6);
    return { key: toIsoDate(from), from: toIsoDate(from), to: toIsoDate(to), label: from.toLocaleDateString(undefined, { day: 'numeric', month: 'short' }) };
  });
}

/** Everyone with an allocation, and what is planned for them each week. An allocation counts in a week by the share of its days that fall in it. */
export function capacityHeat(allocations: AllocationDto[], weeks: PlanWeek[], capacity: number, projectName: (id: string) => string): HeatRow[] {
  const byUser = new Map<string, AllocationDto[]>();
  for (const a of allocations) byUser.set(a.userId, [...(byUser.get(a.userId) ?? []), a]);
  return [...byUser.entries()].map(([userId, rows]) => ({
    userId, name: rows[0].userName,
    cells: weeks.map((w) => {
      const hits = rows.filter((a) => a.fromDate <= w.to && a.toDate >= w.from);
      const hours = hits.reduce((sum, a) => {
        const from = a.fromDate > w.from ? a.fromDate : w.from; const to = a.toDate < w.to ? a.toDate : w.to;
        const days = Math.round((new Date(to).getTime() - new Date(from).getTime()) / 864e5) + 1;
        return sum + (a.hoursPerWeek * Math.min(days, 7)) / 7;
      }, 0);
      return { key: w.key, hours, ratio: capacity ? hours / capacity : 0, hint: hits.map((a) => `${projectName(a.projectId)}: ${a.hoursPerWeek} h/week`).join('\n') };
    }),
  })).sort((a, b) => a.name.localeCompare(b.name));
}

/** A status word with a capital first letter. */
export function capitalised(status: string): string {
  return status ? status[0].toUpperCase() + status.slice(1) : '';
}

/** A field name as words: "dueDate" becomes "due date". */
export function fieldWords(field: string): string {
  return field.replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase();
}
