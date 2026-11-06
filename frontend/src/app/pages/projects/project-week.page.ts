import { DecimalPipe } from '@angular/common';
import { ErpDatePipe } from '../../shared/display.pipes';
import { ChangeDetectionStrategy, Component, computed, inject, OnDestroy, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { catchError, of } from 'rxjs';
import { ProjectTasksApi, ProjectTimesheetsApi, ProjectsApi, TaskDto, TimesheetDto } from '../../core/api/projects.api';
import { ProjectsConfig, ProjectsExtraApi, WeekEntry } from '../../core/api/projects-extra.api';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { SessionStore } from '../../core/session/session.store';
import { errorText, PRJ_STYLES, statusLabel } from './projects-ui';

/** One day of one row. A day with several entries is a sum and is edited in Timesheets; hours on the entry itself are what the grid changes. */
interface Cell { entries: TimesheetDto[]; hours: number; original: number; }
interface Row { key: string; projectId: string; taskId: string | null; label: string; cells: Record<string, Cell>; }
interface Timer { projectId: string; taskId: string | null; label: string; startedAt: number; startedLabel: string; }

const TIMER_KEY = 'erp.projects.timer';

/** Your own week: a row per project and task, a column per day, a timer, copying last week's rows and sending the week for approval. */
@Component({
  selector: 'erp-project-week',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, ErpDatePipe, FormsModule, NzButtonModule, NzIconModule, NzSelectModule],
  template: `
    <div class="prj-toolbar">
      <button nz-button (click)="shift(-7)" aria-label="Previous week" data-testid="week-prev"><span nz-icon nzType="left"></span></button>
      <b data-testid="week-label">{{ days()[0] | erpDate }} to {{ days()[6] | erpDate }}</b>
      <button nz-button (click)="shift(7)" aria-label="Next week" data-testid="week-next"><span nz-icon nzType="right"></span></button>
      <button nz-button (click)="today()">This week</button>
      <button nz-button (click)="copyLastWeek()" data-testid="week-copy"><span nz-icon nzType="copy"></span> Copy rows from last week</button>
      <span class="prj-spacer"></span>
      @if (config()?.timesheetApproval) {
        <span class="prj-muted" data-testid="week-state">{{ stateSummary() }}</span>
        <button nz-button [disabled]="!toSubmit().length || dirty()" (click)="submitWeek()" data-testid="week-submit" [title]="dirty() ? 'Save the week first' : ''"><span nz-icon nzType="send"></span> Send week for approval</button>
      }
      <button nz-button nzType="primary" [disabled]="!dirty()" [nzLoading]="saving()" (click)="save()" data-testid="week-save"><span nz-icon nzType="save"></span> Save week</button>
    </div>

    <div class="timer" data-testid="timer">
      @if (timer(); as t) {
        <span class="pulse" aria-hidden="true"></span>
        <span>Timing <b>{{ t.label }}</b> since {{ t.startedLabel }} <b>({{ elapsed() }})</b></span>
        <span class="prj-spacer"></span>
        <button nz-button nzType="primary" nzDanger (click)="stop()" data-testid="timer-stop"><span nz-icon nzType="pause-circle"></span> Stop and log</button>
        <button nz-button (click)="discard()" data-testid="timer-discard">Discard</button>
      } @else {
        <label class="prj-sr" for="timer-project">Project to time</label>
        <nz-select nzId="timer-project" [(ngModel)]="timerProject" nzShowSearch nzPlaceHolder="Project to time" style="width:280px" (ngModelChange)="loadTimerTasks($event)" data-testid="timer-project">
          @for (p of projects().items; track p.id) { <nz-option [nzValue]="p.id" [nzLabel]="p.number + ' · ' + p.name"></nz-option> }
        </nz-select>
        <label class="prj-sr" for="timer-task">Task to time</label>
        <nz-select nzId="timer-task" [(ngModel)]="timerTask" nzAllowClear nzShowSearch nzPlaceHolder="Task (optional)" style="width:240px" [nzDisabled]="!timerProject">
          @for (t of timerTasks(); track t.id) { <nz-option [nzValue]="t.id" [nzLabel]="t.number + ' · ' + t.title"></nz-option> }
        </nz-select>
        <button nz-button nzType="primary" [disabled]="!timerProject" (click)="start()" data-testid="timer-start"><span nz-icon nzType="play-circle"></span> Start timer</button>
      }
    </div>

    <div class="prj-table-wrap prj-card grid-card">
      <table class="grid" data-testid="week-grid">
        <thead>
          <tr><th class="what">Project and task</th>@for (d of days(); track d) { <th class="day" [class.off]="isOff(d)" [class.now]="d === todayIso">{{ dayLabel(d) }}</th> }<th class="prj-num">Total</th></tr>
        </thead>
        <tbody>
          @for (r of rows(); track r.key) {
            <tr>
              <td class="what" [title]="r.label">{{ r.label }}</td>
              @for (d of days(); track d) {
                <td class="day" [class.off]="isOff(d)" [class.now]="d === todayIso">
                  <input type="number" min="0" max="24" step="0.25" class="cell" [class.rejected]="stateOf(r.cells[d]) === 'rejected'" [class.locked]="!!lockReason(r.cells[d])"
                         [ngModel]="r.cells[d]?.hours || null" (ngModelChange)="set(r.key, d, $event)" [disabled]="!!lockReason(r.cells[d])" [title]="lockReason(r.cells[d]) || rejectReason(r.cells[d])"
                         [attr.aria-label]="r.label + ' on ' + d" [attr.data-testid]="'cell-' + r.projectId + '-' + d" />
                </td>
              }
              <td class="prj-num"><b>{{ rowTotal(r) | number: '1.0-2' }}</b></td>
            </tr>
          } @empty { <tr><td [attr.colspan]="9" class="prj-empty">No time this week yet. Add a project below, or copy the rows from last week.</td></tr> }
          <tr class="sum">
            <td class="what">Total</td>
            @for (d of days(); track d) { <td class="day" [class.prj-danger]="overLimit(d)" [attr.data-testid]="'total-' + d" [title]="overLimit(d) ? 'Over the daily limit' : ''">{{ dayTotal(d) | number: '1.0-2' }}</td> }
            <td class="prj-num"><b data-testid="week-total">{{ weekTotal() | number: '1.0-2' }}</b>@if (config(); as c) { <span class="prj-muted"> / {{ c.weeklyCapacityHours | number: '1.0-0' }}</span> }</td>
          </tr>
        </tbody>
      </table>
    </div>

    <div class="prj-toolbar add">
      <label class="prj-sr" for="week-add-project">Add a project</label>
      <nz-select nzId="week-add-project" [(ngModel)]="addProject" nzShowSearch nzPlaceHolder="Add a project" style="width:280px" (ngModelChange)="loadTasks($event)" data-testid="week-add-project">
        @for (p of projects().items; track p.id) { <nz-option [nzValue]="p.id" [nzLabel]="p.number + ' · ' + p.name"></nz-option> }
      </nz-select>
      <label class="prj-sr" for="week-add-task">Task for the new row</label>
      <nz-select nzId="week-add-task" [(ngModel)]="addTask" nzAllowClear nzShowSearch nzPlaceHolder="Task (optional)" style="width:240px" [nzDisabled]="!addProject">
        @for (t of tasks(); track t.id) { <nz-option [nzValue]="t.id" [nzLabel]="t.number + ' · ' + t.title"></nz-option> }
      </nz-select>
      <button nz-button [disabled]="!addProject" (click)="addRow()" data-testid="week-add"><span nz-icon nzType="plus"></span> Add row</button>
    </div>
  `,
  styles: [PRJ_STYLES + `
    .prj-sr { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }
    .timer { display:flex; gap:10px; align-items:center; flex-wrap:wrap; background:var(--erp-color-bg-surface,#fff); border:1px solid var(--erp-color-border-subtle,#e2e8f0); border-radius:10px; padding:10px 12px; margin-bottom:12px; }
    .pulse { width:10px; height:10px; border-radius:50%; background:#ef4444; animation:prj-pulse 1.4s infinite; } @keyframes prj-pulse { 50% { opacity:.3; } } @media (prefers-reduced-motion: reduce) { .pulse { animation:none; } }
    .grid-card { padding:0; } .grid { width:100%; border-collapse:collapse; min-width:820px; font-size:13px; } .grid th { font-weight:600; color:var(--erp-color-text-secondary,#475569); padding:9px 8px; border-bottom:1px solid var(--erp-color-border-subtle,#e2e8f0); }
    .grid td { padding:6px 8px; border-bottom:1px solid var(--erp-color-border-subtle,#f1f5f9); } .what { text-align:start; max-width:300px; overflow:hidden; text-overflow:ellipsis; white-space:nowrap; } .day { text-align:center; width:84px; } .day.off { background:var(--erp-color-bg-subtle,#f8fafc); } .day.now { box-shadow:inset 0 2px 0 var(--erp-color-primary,#10955d); }
    .cell { width:68px; text-align:right; border:1px solid var(--erp-color-border-subtle,#cbd5e1); border-radius:6px; padding:4px 6px; background:var(--erp-color-bg-surface,#fff); color:var(--erp-color-text-primary,#0f172a); } .cell.locked { background:var(--erp-color-bg-subtle,#f1f5f9); color:var(--erp-color-text-secondary,#64748b); cursor:not-allowed; } .cell.rejected { border-color:#ef4444; }
    .sum td { font-weight:600; background:var(--erp-color-bg-subtle,#f8fafc); } .add { margin-top:12px; }
  `],
})
export class ProjectWeekPage implements OnDestroy {
  private readonly timesheets = inject(ProjectTimesheetsApi);
  private readonly projectsApi = inject(ProjectsApi);
  private readonly tasksApi = inject(ProjectTasksApi);
  private readonly extra = inject(ProjectsExtraApi);
  private readonly msg = inject(NzMessageService);
  private readonly me = inject(SessionStore).me;

  readonly projects = toSignal(this.projectsApi.open().pipe(catchError(() => of({ items: [] }))), { initialValue: { items: [] } });
  readonly config = signal<ProjectsConfig | null>(null);
  readonly weekStart = signal(this.startOf(new Date(), 1));
  readonly rows = signal<Row[]>([]);
  readonly tasks = signal<TaskDto[]>([]);
  readonly timerTasks = signal<TaskDto[]>([]);
  readonly saving = signal(false);
  readonly days = computed(() => Array.from({ length: 7 }, (_, i) => { const d = new Date(this.weekStart()); d.setDate(d.getDate() + i); return toIsoDate(d); }));
  readonly timer = signal<Timer | null>(this.readTimer());
  readonly elapsed = signal('');
  readonly todayIso = toIsoDate(new Date());
  readonly entries = computed(() => this.rows().flatMap((r) => Object.values(r.cells).flatMap((c) => c.entries)));
  readonly toSubmit = computed(() => this.entries().filter((e) => e.approvalStatus === 'draft' || e.approvalStatus === 'rejected'));
  readonly stateSummary = computed(() => {
    const all = this.entries(); if (!all.length) return 'Nothing logged';
    const count = (s: string) => all.filter((e) => e.approvalStatus === s).length;
    return (['draft', 'submitted', 'approved', 'rejected'] as const).filter((s) => count(s)).map((s) => `${count(s)} ${statusLabel('approval', s).toLowerCase()}`).join(' · ');
  });
  addProject: string | null = null; addTask: string | null = null; timerProject: string | null = null; timerTask: string | null = null;
  private tick?: ReturnType<typeof setInterval>;

  constructor() {
    this.extra.config().pipe(catchError(() => of(null))).subscribe((c) => {
      this.config.set(c);
      // the week starts on the first working day of the workspace: Sunday where the week is Sunday to Thursday
      if (c?.workDays?.length) { this.weekStart.set(this.startOf(new Date(), c.workDays.includes(0) && !c.workDays.includes(6) ? 0 : 1)); }
      this.load();
    });
    this.tick = setInterval(() => this.updateElapsed(), 1000);
    this.updateElapsed();
  }
  ngOnDestroy(): void { if (this.tick) clearInterval(this.tick); }

  private startOf(d: Date, firstDay: number): Date { const x = new Date(d.getFullYear(), d.getMonth(), d.getDate()); x.setDate(x.getDate() - ((x.getDay() - firstDay + 7) % 7)); return x; }
  private first(): number { const w = this.config()?.workDays; return w?.includes(0) && !w.includes(6) ? 0 : 1; }
  isOff(iso: string): boolean { return !(this.config()?.workDays ?? [1, 2, 3, 4, 5]).includes(new Date(iso + 'T00:00:00').getDay()); }
  dayLabel(iso: string): string { return new Date(iso + 'T00:00:00').toLocaleDateString(undefined, { weekday: 'short', day: 'numeric' }); }
  shift(days: number): void { if (!this.confirmLeave()) return; const d = new Date(this.weekStart()); d.setDate(d.getDate() + days); this.weekStart.set(d); this.load(); }
  today(): void { if (!this.confirmLeave()) return; this.weekStart.set(this.startOf(new Date(), this.first())); this.load(); }
  private confirmLeave(): boolean { return !this.dirty() || window.confirm('You have unsaved hours on this week. Leave without saving?'); }
  rowTotal(r: Row): number { return Object.values(r.cells).reduce((a, c) => a + (c.hours || 0), 0); }
  dayTotal(d: string): number { return this.rows().reduce((a, r) => a + (r.cells[d]?.hours || 0), 0); }
  weekTotal(): number { return this.rows().reduce((a, r) => a + this.rowTotal(r), 0); }
  overLimit(d: string): boolean { return this.dayTotal(d) > (this.config()?.maxHoursPerDay ?? 16); }
  dirty(): boolean { return this.rows().some((r) => Object.values(r.cells).some((c) => c.hours !== c.original)); }
  stateOf(c?: Cell): string | null { return c?.entries[0]?.approvalStatus ?? null; }
  rejectReason(c?: Cell): string { return c?.entries.find((e) => e.rejectReason)?.rejectReason ?? ''; }

  /** Why this day cannot be typed in, or empty when it can. */
  lockReason(c?: Cell): string {
    if (!c?.entries.length) return '';
    if (c.entries.length > 1) return `${c.entries.length} entries on this day. Change them in Timesheets.`;
    const e = c.entries[0];
    if (e.isBilled) return 'Already billed';
    if (e.writtenOff) return 'Written off';
    if (e.approvalStatus === 'submitted') return 'Waiting for approval';
    if (e.approvalStatus === 'approved' && this.config()?.timesheetApproval) return 'Approved';
    return '';
  }

  private query(from: string, to: string) {
    const uid = this.me()?.user?.id;
    return this.timesheets.list({ page: 1, pageSize: 200, sort: 'date', filters: [{ field: 'date', op: 'gte', value: from }, { field: 'date', op: 'lte', value: to }, { field: 'workerUserId', op: 'eq', value: uid ?? '' }] });
  }

  private labelOf(t: { projectName: string; taskTitle?: string | null }): string { return t.projectName + (t.taskTitle ? ' · ' + t.taskTitle : ''); }

  load(): void {
    const [from, to] = [this.days()[0], this.days()[6]];
    this.query(from, to).subscribe({
      next: (r) => {
        const rows = new Map<string, Row>();
        for (const t of r.items) {
          const key = `${t.projectId}|${t.taskId ?? ''}`;
          const row = rows.get(key) ?? { key, projectId: t.projectId, taskId: t.taskId ?? null, label: this.labelOf(t), cells: {} };
          const cell = row.cells[t.date] ?? { entries: [], hours: 0, original: 0 };
          row.cells[t.date] = { entries: [...cell.entries, t], hours: cell.hours + t.hours, original: cell.original + t.hours };
          rows.set(key, row);
        }
        this.rows.set([...rows.values()]);
      },
      error: (e) => this.msg.error(errorText(e, 'Could not load the week.')),
    });
  }

  private loadTaskList(projectId: string | null, target: (t: TaskDto[]) => void): void {
    if (!projectId) { target([]); return; }
    this.tasksApi.list({ page: 1, pageSize: 200, sort: 'number', filters: [{ field: 'projectId', op: 'eq', value: projectId }, { field: 'status', op: 'in', value: ['todo', 'inProgress', 'review', 'blocked'] }] }).pipe(catchError(() => of({ items: [] as TaskDto[] }))).subscribe((r) => target(r.items));
  }
  loadTasks(projectId: string | null): void { this.addTask = null; this.loadTaskList(projectId, (t) => this.tasks.set(t)); }
  loadTimerTasks(projectId: string | null): void { this.timerTask = null; this.loadTaskList(projectId, (t) => this.timerTasks.set(t)); }

  private projectName(id: string): string { const p = this.projects().items.find((x) => x.id === id); return p ? `${p.number} · ${p.name}` : 'Project'; }

  addRow(): void {
    if (!this.addProject) return;
    const key = `${this.addProject}|${this.addTask ?? ''}`;
    if (this.rows().some((r) => r.key === key)) return;
    const task = this.tasks().find((t) => t.id === this.addTask);
    this.rows.update((rs) => [...rs, { key, projectId: this.addProject!, taskId: this.addTask, label: this.projectName(this.addProject!) + (task ? ' · ' + task.title : ''), cells: {} }]);
  }

  /** Adds the projects and tasks of last week as empty rows, so regular work is one click away. */
  copyLastWeek(): void {
    const s = new Date(this.weekStart()); s.setDate(s.getDate() - 7);
    const e = new Date(s); e.setDate(e.getDate() + 6);
    this.query(toIsoDate(s), toIsoDate(e)).subscribe((r) => {
      const known = new Set(this.rows().map((x) => x.key));
      const fresh = new Map<string, Row>();
      for (const t of r.items) {
        const key = `${t.projectId}|${t.taskId ?? ''}`;
        if (!known.has(key) && !fresh.has(key)) fresh.set(key, { key, projectId: t.projectId, taskId: t.taskId ?? null, label: this.labelOf(t), cells: {} });
      }
      if (!fresh.size) { this.msg.info(r.items.length ? 'Those rows are already here.' : 'You logged nothing last week.'); return; }
      this.rows.update((rs) => [...rs, ...fresh.values()]);
    });
  }

  set(key: string, day: string, value: number | null): void {
    const hours = Math.max(0, Math.min(24, Number(value) || 0));
    this.rows.update((rs) => rs.map((r) => (r.key === key ? { ...r, cells: { ...r.cells, [day]: { entries: r.cells[day]?.entries ?? [], hours, original: r.cells[day]?.original ?? 0 } } } : r)));
  }

  save(): void {
    const uid = this.me()?.user?.id ?? null;
    const items: WeekEntry[] = [];
    for (const r of this.rows())
      for (const [date, c] of Object.entries(r.cells))
        if (c.hours !== c.original && !this.lockReason(c)) items.push({ id: c.entries[0]?.id, projectId: r.projectId, taskId: r.taskId, date, workerUserId: uid, hours: c.hours, isBillable: true });
    if (!items.length) return;
    this.saving.set(true);
    this.extra.saveWeek(items).subscribe({
      next: () => { this.saving.set(false); this.msg.success('Week saved'); this.load(); },
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

  // ---- timer (kept in the browser so it survives a reload) -------------------------------------------------------------------
  private readTimer(): Timer | null {
    try { const raw = localStorage.getItem(TIMER_KEY); return raw ? (JSON.parse(raw) as Timer) : null; } catch { return null; }
  }
  private writeTimer(t: Timer | null): void {
    try { if (t) localStorage.setItem(TIMER_KEY, JSON.stringify(t)); else localStorage.removeItem(TIMER_KEY); } catch { /* storage may be blocked; the timer then lasts until the page closes */ }
  }
  start(): void {
    if (!this.timerProject) return;
    const now = new Date();
    const task = this.timerTasks().find((t) => t.id === this.timerTask);
    const t: Timer = { projectId: this.timerProject, taskId: this.timerTask, label: this.projectName(this.timerProject) + (task ? ' · ' + task.title : ''), startedAt: now.getTime(), startedLabel: now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) };
    this.timer.set(t); this.writeTimer(t); this.updateElapsed();
  }
  private updateElapsed(): void {
    const t = this.timer(); if (!t) { this.elapsed.set(''); return; }
    const s = Math.floor((Date.now() - t.startedAt) / 1000);
    this.elapsed.set(`${Math.floor(s / 3600)}h ${String(Math.floor((s % 3600) / 60)).padStart(2, '0')}m`);
  }
  discard(): void { this.timer.set(null); this.writeTimer(null); }

  /** The time is saved straight away, so nothing is lost by leaving the page or looking at another week. */
  stop(): void {
    const t = this.timer(); if (!t) return;
    const hours = Math.max(0.25, Math.round(((Date.now() - t.startedAt) / 3600000) * 4) / 4);   // to the nearest quarter hour, at least one
    this.timesheets.create({ projectId: t.projectId, taskId: t.taskId, date: toIsoDate(new Date()), workerUserId: this.me()?.user?.id ?? null, hours, isBillable: true }).subscribe({
      next: () => { this.timer.set(null); this.writeTimer(null); this.msg.success(`${hours} h logged for today`); this.load(); },
      error: (e) => this.msg.error(errorText(e, 'Could not log the time. The timer is still running.')),
    });
  }
}
