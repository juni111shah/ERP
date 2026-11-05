import { CommonModule, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, OnDestroy, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { catchError, of } from 'rxjs';
import { ProjectTasksApi, ProjectTimesheetsApi, ProjectsApi, TaskDto } from '../../core/api/projects.api';
import { ProjectsExtraApi, WeekEntry } from '../../core/api/projects-extra.api';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { SessionStore } from '../../core/session/session.store';

interface Cell { id?: string; hours: number; original: number; }
interface Row { key: string; projectId: string; taskId: string | null; label: string; cells: Record<string, Cell>; }

const TIMER_KEY = 'erp.projects.timer';

/** Weekly time sheet for yourself: a row per project (and task), a column per day, plus a start/stop timer. */
@Component({
  selector: 'erp-project-week',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, DecimalPipe, FormsModule, NzButtonModule, NzIconModule, NzSelectModule, NzTableModule],
  template: `
    <h1 class="erp-page-title">My week</h1>
    <div class="bar">
      <button nz-button (click)="shift(-7)" aria-label="Previous week" data-testid="week-prev"><span nz-icon nzType="left"></span></button>
      <b data-testid="week-label">{{ days()[0] }} to {{ days()[6] }}</b>
      <button nz-button (click)="shift(7)" aria-label="Next week" data-testid="week-next"><span nz-icon nzType="right"></span></button>
      <button nz-button (click)="today()">This week</button>
    </div>

    <div class="timer" data-testid="timer">
      @if (timer(); as t) {
        <span>Timing <b>{{ projectName(t.projectId) }}</b> since {{ t.startedLabel }} ({{ elapsed() }})</span>
        <button nz-button nzType="primary" nzDanger (click)="stop()" data-testid="timer-stop"><span nz-icon nzType="pause-circle"></span> Stop and add</button>
      } @else {
        <nz-select [(ngModel)]="timerProject" nzShowSearch nzPlaceHolder="Project to time" style="width:300px" aria-label="Project to time" data-testid="timer-project">
          @for (p of projects().items; track p.id) { <nz-option [nzValue]="p.id" [nzLabel]="p.number + ' · ' + p.name"></nz-option> }
        </nz-select>
        <button nz-button nzType="primary" [disabled]="!timerProject" (click)="start()" data-testid="timer-start"><span nz-icon nzType="play-circle"></span> Start timer</button>
      }
    </div>

    <nz-table [nzData]="rows()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="week-grid">
      <thead>
        <tr><th>Project and task</th>@for (d of days(); track d) { <th style="text-align:center;width:84px">{{ dayLabel(d) }}</th> }<th style="text-align:right">Total</th></tr>
      </thead>
      <tbody>
        @for (r of rows(); track r.key) {
          <tr>
            <td>{{ r.label }}</td>
            @for (d of days(); track d) {
              <td style="text-align:center">
                <input type="number" min="0" max="24" step="0.25" class="cell" [ngModel]="r.cells[d]?.hours || null" (ngModelChange)="set(r, d, $event)"
                       [attr.aria-label]="r.label + ' on ' + d" [attr.data-testid]="'cell-' + r.projectId + '-' + d" />
              </td>
            }
            <td style="text-align:right"><b>{{ rowTotal(r) | number: '1.0-2' }}</b></td>
          </tr>
        } @empty { <tr><td [attr.colspan]="9">Add a project to start entering time.</td></tr> }
        <tr class="sum"><td>Total</td>@for (d of days(); track d) { <td style="text-align:center">{{ dayTotal(d) | number: '1.0-2' }}</td> }<td style="text-align:right"><b>{{ weekTotal() | number: '1.0-2' }}</b></td></tr>
      </tbody>
    </nz-table>

    <div class="bar" style="margin-top:12px">
      <nz-select [(ngModel)]="addProject" nzShowSearch nzPlaceHolder="Add a project" style="width:300px" (ngModelChange)="loadTasks($event)" aria-label="Add project" data-testid="week-add-project">
        @for (p of projects().items; track p.id) { <nz-option [nzValue]="p.id" [nzLabel]="p.number + ' · ' + p.name"></nz-option> }
      </nz-select>
      <nz-select [(ngModel)]="addTask" nzAllowClear nzShowSearch nzPlaceHolder="Task (optional)" style="width:260px" aria-label="Add task">
        @for (t of tasks(); track t.id) { <nz-option [nzValue]="t.id" [nzLabel]="t.number + ' · ' + t.title"></nz-option> }
      </nz-select>
      <button nz-button [disabled]="!addProject" (click)="addRow()" data-testid="week-add">Add row</button>
      <span style="flex:1"></span>
      <button nz-button nzType="primary" [disabled]="!dirty()" [nzLoading]="saving()" (click)="save()" data-testid="week-save"><span nz-icon nzType="save"></span> Save week</button>
    </div>
  `,
  styles: ['.bar { display:flex; gap:12px; align-items:center; margin-bottom:12px; flex-wrap:wrap } .timer { display:flex; gap:12px; align-items:center; background:#f8fafc; border:1px solid #e2e8f0; border-radius:10px; padding:10px 12px; margin-bottom:16px } .cell { width:70px; text-align:right; border:1px solid #cbd5e1; border-radius:6px; padding:4px } .sum td { font-weight:600; background:#f8fafc }'],
})
export class ProjectWeekPage implements OnDestroy {
  private readonly timesheets = inject(ProjectTimesheetsApi);
  private readonly projectsApi = inject(ProjectsApi);
  private readonly tasksApi = inject(ProjectTasksApi);
  private readonly extra = inject(ProjectsExtraApi);
  private readonly msg = inject(NzMessageService);
  private readonly me = inject(SessionStore).me;

  readonly projects = toSignal(this.projectsApi.list({ page: 1, pageSize: 200 }).pipe(catchError(() => of({ items: [] }))), { initialValue: { items: [] } });
  readonly weekStart = signal(this.mondayOf(new Date()));
  readonly rows = signal<Row[]>([]);
  readonly tasks = signal<TaskDto[]>([]);
  readonly saving = signal(false);
  readonly days = computed(() => Array.from({ length: 7 }, (_, i) => { const d = new Date(this.weekStart()); d.setDate(d.getDate() + i); return toIsoDate(d); }));
  readonly timer = signal<{ projectId: string; startedAt: number; startedLabel: string } | null>(this.readTimer());
  readonly elapsed = signal('');
  addProject: string | null = null; addTask: string | null = null; timerProject: string | null = null;
  private tick?: ReturnType<typeof setInterval>;

  constructor() {
    this.load();
    this.tick = setInterval(() => this.updateElapsed(), 1000);
    this.updateElapsed();
  }
  ngOnDestroy(): void { if (this.tick) clearInterval(this.tick); }

  private mondayOf(d: Date): Date { const x = new Date(d.getFullYear(), d.getMonth(), d.getDate()); x.setDate(x.getDate() - ((x.getDay() + 6) % 7)); return x; }
  dayLabel(iso: string): string { return new Date(iso + 'T00:00:00').toLocaleDateString(undefined, { weekday: 'short', day: 'numeric' }); }
  shift(days: number): void { const d = new Date(this.weekStart()); d.setDate(d.getDate() + days); this.weekStart.set(d); this.load(); }
  today(): void { this.weekStart.set(this.mondayOf(new Date())); this.load(); }
  projectName(id: string): string { const p = this.projects().items.find((x) => x.id === id); return p ? `${p.number} · ${p.name}` : 'Project'; }
  rowTotal(r: Row): number { return Object.values(r.cells).reduce((a, c) => a + (c.hours || 0), 0); }
  dayTotal(d: string): number { return this.rows().reduce((a, r) => a + (r.cells[d]?.hours || 0), 0); }
  weekTotal(): number { return this.rows().reduce((a, r) => a + this.rowTotal(r), 0); }
  dirty(): boolean { return this.rows().some((r) => Object.values(r.cells).some((c) => c.hours !== c.original)); }

  load(): void {
    const uid = this.me()?.user?.id;
    const [from, to] = [this.days()[0], this.days()[6]];
    const filters = [{ field: 'date', op: 'gte' as const, value: from }, { field: 'date', op: 'lte' as const, value: to }, ...(uid ? [{ field: 'workerUserId', op: 'eq' as const, value: uid }] : [])];
    this.timesheets.list({ page: 1, pageSize: 500, filters }).subscribe((r) => {
      const rows = new Map<string, Row>();
      for (const t of r.items) {
        const key = `${t.projectId}|${t.taskId ?? ''}`;
        const row = rows.get(key) ?? { key, projectId: t.projectId, taskId: t.taskId ?? null, label: this.projectName(t.projectId) + (t.taskId ? ' · task' : ''), cells: {} };
        const cell = row.cells[t.date] ?? { hours: 0, original: 0 };
        row.cells[t.date] = { id: t.id, hours: cell.hours + t.hours, original: cell.original + t.hours };
        rows.set(key, row);
      }
      this.rows.set([...rows.values()]);
    });
  }

  loadTasks(projectId: string | null): void {
    this.addTask = null;
    if (!projectId) { this.tasks.set([]); return; }
    this.tasksApi.list({ page: 1, pageSize: 200, filters: [{ field: 'projectId', op: 'eq', value: projectId }] }).pipe(catchError(() => of({ items: [] as TaskDto[] }))).subscribe((r) => this.tasks.set(r.items));
  }

  addRow(): void {
    if (!this.addProject) return;
    const key = `${this.addProject}|${this.addTask ?? ''}`;
    if (this.rows().some((r) => r.key === key)) return;
    const task = this.tasks().find((t) => t.id === this.addTask);
    this.rows.update((rs) => [...rs, { key, projectId: this.addProject!, taskId: this.addTask, label: this.projectName(this.addProject!) + (task ? ' · ' + task.title : ''), cells: {} }]);
  }

  set(row: Row, day: string, value: number | null): void {
    const hours = Math.max(0, Math.min(24, Number(value) || 0));
    this.rows.update((rs) => rs.map((r) => (r === row ? { ...r, cells: { ...r.cells, [day]: { id: r.cells[day]?.id, hours, original: r.cells[day]?.original ?? 0 } } } : r)));
  }

  save(): void {
    const uid = this.me()?.user?.id ?? null;
    const items: WeekEntry[] = [];
    for (const r of this.rows())
      for (const [date, c] of Object.entries(r.cells))
        if (c.hours !== c.original) items.push({ id: c.id, projectId: r.projectId, taskId: r.taskId, date, workerUserId: uid, hours: c.hours, isBillable: true });
    if (!items.length) return;
    this.saving.set(true);
    this.extra.saveWeek(items).subscribe({
      next: () => { this.saving.set(false); this.msg.success('Week saved'); this.load(); },
      error: (e) => { this.saving.set(false); this.msg.error(e?.error?.detail ?? e?.error?.errors?.hours?.[0] ?? 'Could not save the week.'); },
    });
  }

  // ---- timer (kept in the browser so it survives a reload) -------------------------------------------------------------------
  private readTimer(): { projectId: string; startedAt: number; startedLabel: string } | null {
    try { const raw = localStorage.getItem(TIMER_KEY); return raw ? JSON.parse(raw) : null; } catch { return null; }
  }
  private writeTimer(t: { projectId: string; startedAt: number; startedLabel: string } | null): void {
    try { if (t) localStorage.setItem(TIMER_KEY, JSON.stringify(t)); else localStorage.removeItem(TIMER_KEY); } catch { /* storage may be blocked; the timer then lasts until the page closes */ }
  }
  start(): void {
    if (!this.timerProject) return;
    const now = new Date();
    const t = { projectId: this.timerProject, startedAt: now.getTime(), startedLabel: now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) };
    this.timer.set(t); this.writeTimer(t); this.updateElapsed();
  }
  private updateElapsed(): void {
    const t = this.timer(); if (!t) { this.elapsed.set(''); return; }
    const s = Math.floor((Date.now() - t.startedAt) / 1000);
    this.elapsed.set(`${Math.floor(s / 3600)}h ${String(Math.floor((s % 3600) / 60)).padStart(2, '0')}m`);
  }
  stop(): void {
    const t = this.timer(); if (!t) return;
    const hours = Math.max(0.25, Math.round(((Date.now() - t.startedAt) / 3600000) * 4) / 4);   // to the nearest quarter hour, at least one
    this.timer.set(null); this.writeTimer(null);
    const day = toIsoDate(new Date());
    if (!this.days().includes(day)) this.today();
    const key = `${t.projectId}|`;
    if (!this.rows().some((r) => r.key === key)) this.rows.update((rs) => [...rs, { key, projectId: t.projectId, taskId: null, label: this.projectName(t.projectId), cells: {} }]);
    const row = this.rows().find((r) => r.key === key)!;
    this.set(row, day, (row.cells[day]?.hours ?? 0) + hours);
    this.msg.info(`${hours} h added to today. Save the week to keep it.`);
  }
}
