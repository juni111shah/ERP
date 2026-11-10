import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, OnDestroy, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { Observable, catchError, of } from 'rxjs';
import { PagedResult } from '@core/api/api.models';
import { ProjectsConfig, ProjectsExtraApi } from '@core/api/projects-extra.api';
import { ProjectTasksApi, ProjectTimesheetsApi, TaskDto, TimesheetDto } from '@core/api/projects.api';
import { toIsoDate } from '@core/format/dates';
import { SessionStore } from '@core/session/session.store';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { isoToday } from '../../projects-format';
import { injectOpenProjects, projectNameIn } from '../../projects-lookups';
import { errorText, PRJ_STYLES } from '../../projects-ui';
import { WeekCell, WeekRow, WeekTimer, addDays, approvalSummary, cellLockReason, changedEntries, elapsedText, firstWeekDay, newRows, readTimer, rowKey, startOfWeek, timerHours, weekRows, writeTimer } from '../../projects-week';

/** Your own week: a row per project and task, a column per day, a timer, copying last week's rows and sending the week for approval. */
@Component({
  selector: 'erp-project-week',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, ErpDatePipe, FormsModule, NzButtonModule, NzIconModule, NzSelectModule],
  templateUrl: './project-week.page.html',
  styles: [PRJ_STYLES + `
    .prj-sr { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }
    .timer { display:flex; gap:10px; align-items:center; flex-wrap:wrap; background:var(--erp-color-bg-surface,#fff); border:1px solid var(--erp-color-border-subtle,#e2e8f0); border-radius:10px; padding:10px 12px; margin-bottom:12px; }
    .pulse { width:10px; height:10px; border-radius:50%; background:#ef4444; animation:prj-pulse 1.4s infinite; } @keyframes prj-pulse { 50% { opacity:.3; } } @media (prefers-reduced-motion: reduce) { .pulse { animation:none; } }
    .grid-card { padding:0; } .grid { width:100%; border-collapse:collapse; min-width:820px; font-size:13px; } .grid th { font-weight:600; color:var(--erp-color-text-secondary,#475569); padding:9px 8px; border-bottom:1px solid var(--erp-color-border-subtle,#e2e8f0); }
    .grid td { padding:6px 8px; border-bottom:1px solid var(--erp-color-border-subtle,#f1f5f9); } .what { text-align:start; max-width:300px; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; } .day { text-align:center; width:84px; } .day.off { background:var(--erp-color-bg-subtle,#f8fafc); } .day.now { box-shadow:inset 0 2px 0 var(--erp-color-primary,#10955d); }
    .cell { width:68px; text-align:end; border:1px solid var(--erp-color-border-subtle,#cbd5e1); border-radius:6px; padding:4px 6px; background:var(--erp-color-bg-surface,#fff); color:var(--erp-color-text-primary,#0f172a); } .cell.locked { background:var(--erp-color-bg-subtle,#f1f5f9); color:var(--erp-color-text-secondary,#64748b); cursor:not-allowed; } .cell.rejected { border-color:#ef4444; }
    .sum td { font-weight:600; background:var(--erp-color-bg-subtle,#f8fafc); } .add { margin-top:12px; }
  `],
})
export class ProjectWeekPage implements OnDestroy {
  private readonly timesheets = inject(ProjectTimesheetsApi);
  private readonly tasksApi = inject(ProjectTasksApi);
  private readonly extra = inject(ProjectsExtraApi);
  private readonly msg = inject(NzMessageService);
  private readonly me = inject(SessionStore).me;

  readonly projects = injectOpenProjects();
  readonly config = signal<ProjectsConfig | null>(null);
  readonly weekStart = signal(startOfWeek(new Date(), 1));
  readonly rows = signal<WeekRow[]>([]);
  readonly tasks = signal<TaskDto[]>([]);
  readonly timerTasks = signal<TaskDto[]>([]);
  readonly saving = signal(false);
  readonly days = computed(() => Array.from({ length: 7 }, (_, i) => toIsoDate(addDays(this.weekStart(), i))));
  readonly timer = signal<WeekTimer | null>(readTimer());
  readonly elapsed = signal('');
  readonly todayIso = isoToday();
  readonly entries = computed(() => this.rows().flatMap((r) => Object.values(r.cells).flatMap((c) => c.entries)));
  readonly toSubmit = computed(() => this.entries().filter((e) => e.approvalStatus === 'draft' || e.approvalStatus === 'rejected'));
  readonly stateSummary = computed(() => approvalSummary(this.entries()));
  addProject: string | null = null; addTask: string | null = null; timerProject: string | null = null; timerTask: string | null = null;
  private tick?: ReturnType<typeof setInterval>;

  constructor() {
    this.extra.config().pipe(catchError(() => of(null))).subscribe((c) => {
      this.config.set(c);
      // the week starts on the first working day of the workspace: Sunday where the week is Sunday to Thursday
      if (c?.workDays?.length) this.weekStart.set(startOfWeek(new Date(), firstWeekDay(c.workDays)));
      this.load();
    });
    this.tick = setInterval(() => this.updateElapsed(), 1000);
    this.updateElapsed();
  }
  ngOnDestroy(): void { if (this.tick) clearInterval(this.tick); }

  isOff(iso: string): boolean { return !(this.config()?.workDays ?? [1, 2, 3, 4, 5]).includes(new Date(iso + 'T00:00:00').getDay()); }
  dayLabel(iso: string): string { return new Date(iso + 'T00:00:00').toLocaleDateString(undefined, { weekday: 'short', day: 'numeric' }); }
  shift(days: number): void { if (!this.confirmLeave()) return; this.weekStart.set(addDays(this.weekStart(), days)); this.load(); }
  today(): void { if (!this.confirmLeave()) return; this.weekStart.set(startOfWeek(new Date(), firstWeekDay(this.config()?.workDays))); this.load(); }
  rowTotal(r: WeekRow): number { return Object.values(r.cells).reduce((a, c) => a + (c.hours || 0), 0); }
  dayTotal(d: string): number { return this.rows().reduce((a, r) => a + (r.cells[d]?.hours || 0), 0); }
  weekTotal(): number { return this.rows().reduce((a, r) => a + this.rowTotal(r), 0); }
  overLimit(d: string): boolean { return this.dayTotal(d) > (this.config()?.maxHoursPerDay ?? 16); }
  dirty(): boolean { return this.rows().some((r) => Object.values(r.cells).some((c) => c.hours !== c.original)); }
  stateOf(c?: WeekCell): string | null { return c?.entries[0]?.approvalStatus ?? null; }
  rejectReason(c?: WeekCell): string { return c?.entries.find((e) => e.rejectReason)?.rejectReason ?? ''; }

  /** Why this day cannot be typed in, or empty when it can. */
  lockReason(c?: WeekCell): string { return cellLockReason(c, this.config()?.timesheetApproval); }

  load(): void {
    const [from, to] = [this.days()[0], this.days()[6]];
    this.query(from, to).subscribe({
      next: (r) => this.rows.set(weekRows(r.items)),
      error: (e) => this.msg.error(errorText(e, 'Could not load the week.')),
    });
  }

  loadTasks(projectId: string | null): void { this.addTask = null; this.loadTaskList(projectId, (t) => this.tasks.set(t)); }
  loadTimerTasks(projectId: string | null): void { this.timerTask = null; this.loadTaskList(projectId, (t) => this.timerTasks.set(t)); }

  addRow(): void {
    const projectId = this.addProject;
    if (!projectId) return;
    const key = rowKey(projectId, this.addTask);
    if (this.rows().some((r) => r.key === key)) return;
    this.rows.update((rs) => [...rs, { key, projectId, taskId: this.addTask, label: this.labelFor(projectId, this.tasks(), this.addTask), cells: {} }]);
  }

  /** Adds the projects and tasks of last week as empty rows, so regular work is one click away. */
  copyLastWeek(): void {
    const from = addDays(this.weekStart(), -7);
    this.query(toIsoDate(from), toIsoDate(addDays(from, 6))).subscribe((r) => {
      const fresh = newRows(r.items, new Set(this.rows().map((x) => x.key)));
      if (!fresh.length) { this.msg.info(r.items.length ? 'Those rows are already here.' : 'You logged nothing last week.'); return; }
      this.rows.update((rs) => [...rs, ...fresh]);
    });
  }

  set(key: string, day: string, value: number | null): void {
    const hours = Math.max(0, Math.min(24, Number(value) || 0));
    this.rows.update((rs) => rs.map((r) => (r.key === key ? { ...r, cells: { ...r.cells, [day]: { entries: r.cells[day]?.entries ?? [], hours, original: r.cells[day]?.original ?? 0 } } } : r)));
  }

  save(): void {
    const items = changedEntries(this.rows(), this.me()?.user?.id ?? null, this.config()?.timesheetApproval);
    if (!items.length) return;
    this.saving.set(true);
    this.extra.saveWeek(items).subscribe({
      next: () => { this.saving.set(false); this.msg.success(`${items.length} time ${items.length === 1 ? 'entry' : 'entries'} saved for the week`); this.load(); },
      error: (e) => { this.saving.set(false); this.msg.error(errorText(e, 'Could not save the week.')); },
    });
  }

  submitWeek(): void {
    const ids = this.toSubmit().map((e) => e.id); if (!ids.length) return;
    this.extra.submitTime(ids).subscribe({
      next: (r) => { this.msg.success(`${r.submitted} ${r.submitted === 1 ? 'entry' : 'entries'} sent for approval`); this.load(); },
      error: (e) => this.msg.error(errorText(e, 'Could not send the week.')),
    });
  }

  start(): void {
    const projectId = this.timerProject;
    if (!projectId) return;
    const now = new Date();
    const t: WeekTimer = {
      projectId, taskId: this.timerTask, label: this.labelFor(projectId, this.timerTasks(), this.timerTask),
      startedAt: now.getTime(), startedLabel: now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
    };
    this.timer.set(t); writeTimer(t); this.updateElapsed();
  }
  discard(): void { this.timer.set(null); writeTimer(null); }

  /** The time is saved straight away, so nothing is lost by leaving the page or looking at another week. */
  stop(): void {
    const t = this.timer(); if (!t) return;
    const hours = timerHours(t.startedAt, Date.now());
    this.timesheets.create({ projectId: t.projectId, taskId: t.taskId, date: toIsoDate(new Date()), workerUserId: this.me()?.user?.id ?? null, hours, isBillable: true }).subscribe({
      next: () => { this.timer.set(null); writeTimer(null); this.msg.success(`${hours} h logged for today`); this.load(); },
      error: (e) => this.msg.error(errorText(e, 'Could not log the time. The timer is still running.')),
    });
  }

  private confirmLeave(): boolean { return !this.dirty() || window.confirm('You have unsaved hours on this week. Leave without saving?'); }

  /** The person's own time between two days. */
  private query(from: string, to: string): Observable<PagedResult<TimesheetDto>> {
    const uid = this.me()?.user?.id;
    return this.timesheets.list({ page: 1, pageSize: 200, sort: 'date', filters: [{ field: 'date', op: 'gte', value: from }, { field: 'date', op: 'lte', value: to }, { field: 'workerUserId', op: 'eq', value: uid ?? '' }] });
  }

  /** The open tasks of a project, for one of the task pickers; empty when there is no project or they cannot be loaded. */
  private loadTaskList(projectId: string | null, target: (t: TaskDto[]) => void): void {
    if (!projectId) { target([]); return; }
    this.tasksApi.list({ page: 1, pageSize: 200, sort: 'number', filters: [{ field: 'projectId', op: 'eq', value: projectId }, { field: 'status', op: 'in', value: ['todo', 'inProgress', 'review', 'blocked'] }] })
      .subscribe({ next: (r) => target(r.items), error: () => target([]) });
  }

  /** The label of a new row or timer: the project, and the picked task when there is one. */
  private labelFor(projectId: string, tasks: TaskDto[], taskId: string | null): string {
    const task = tasks.find((t) => t.id === taskId);
    return projectNameIn(this.projects().items, projectId, 'Project') + (task ? ' · ' + task.title : '');
  }

  private updateElapsed(): void {
    const t = this.timer();
    this.elapsed.set(t ? elapsedText(t.startedAt, Date.now()) : '');
  }
}
