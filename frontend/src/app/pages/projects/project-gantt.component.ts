import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalService } from 'ng-zorro-antd/modal';
import { catchError, of } from 'rxjs';
import { ProjectsConfig, ProjectsExtraApi, ScheduleDto, ScheduleTaskDto } from '../../core/api/projects-extra.api';
import { ProjectTasksApi, taskToInput } from '../../core/api/projects.api';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { SessionStore } from '../../core/session/session.store';
import { errorText, PRJ_STYLES } from './projects-ui';

const DAY = 864e5;
const parse = (s: string): number => { const [y, m, d] = s.slice(0, 10).split('-').map(Number); return Date.UTC(y, m - 1, d); };
const fmt = (t: number): string => new Date(t).toISOString().slice(0, 10);
const ROW = 34;
const HEAD = 46;

interface Bar { task: ScheduleTaskDto; top: number; left: number; width: number; baseLeft: number | null; baseWidth: number; }
interface Column { left: number; width: number; label: string; off: boolean; }
interface Band { left: number; width: number; label: string; }

/**
 * The project timeline: bars on a real date axis, today's line, milestones, arrows for what waits for what, the critical path, and an optional baseline.
 * People who manage tasks drag a bar to move it, or its right edge to change the due date.
 */
@Component({
  selector: 'erp-project-gantt',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzButtonModule, NzIconModule],
  template: `
    <div class="prj-toolbar" data-testid="gantt-toolbar">
      <div class="prj-chips" role="group" aria-label="Zoom">
        @for (z of zooms; track z.value) { <button type="button" class="prj-chip" [class.on]="zoom() === z.value" (click)="zoom.set(z.value)">{{ z.label }}</button> }
      </div>
      <span class="legend"><i class="sw"></i> Task <i class="sw critical"></i> Critical path <i class="sw done"></i> Done <i class="sw late"></i> Late @if (schedule()?.hasBaseline) { <i class="sw base"></i> Baseline } <i class="diamond"></i> Milestone</span>
      <span class="prj-spacer"></span>
      @if (canManage()) {
        <button nz-button nzSize="small" (click)="baseline(false)" data-testid="gantt-baseline"><span nz-icon nzType="flag"></span> {{ schedule()?.hasBaseline ? 'Update baseline' : 'Save as baseline' }}</button>
        @if (schedule()?.hasBaseline) { <button nz-button nzSize="small" (click)="baseline(true)">Clear baseline</button> }
      }
    </div>

    @if (schedule(); as s) {
      @if (bars().length || s.milestones.length) {
        <div class="gantt" data-testid="gantt" dir="ltr">
          <div class="labels" [style.height.px]="height()">
            <div class="labels-head" [style.height.px]="HEAD">Task</div>
            @for (b of bars(); track b.task.id) {
              <button type="button" class="label" [class.sub]="!!b.task.parentTaskId" [style.top.px]="b.top" [title]="b.task.title" (click)="opened.emit(b.task.id)">
                <span class="num">{{ b.task.number }}</span> {{ b.task.title }}
              </button>
            }
            @if (s.milestones.length) { <div class="label ms-label" [style.top.px]="milestoneTop()">Milestones</div> }
          </div>
          <div class="track-scroll">
            <div class="track" [style.width.px]="totalWidth()" [style.height.px]="height()">
              @for (b of bands(); track b.left) { <div class="band" [style.left.px]="b.left" [style.width.px]="b.width">{{ b.label }}</div> }
              @for (c of columns(); track c.left) {
                <div class="col" [class.off]="c.off" [style.left.px]="c.left" [style.width.px]="c.width" [style.top.px]="22" [style.bottom.px]="0"><span>{{ c.label }}</span></div>
              }
              @if (todayLeft() !== null) { <div class="today" [style.left.px]="todayLeft()" title="Today"></div> }
              <svg class="arrows" [attr.width]="totalWidth()" [attr.height]="height()" aria-hidden="true">
                <defs><marker id="prj-arrow" markerWidth="7" markerHeight="7" refX="6" refY="3" orient="auto"><path d="M0,0 L6,3 L0,6 z" fill="#94a3b8" /></marker></defs>
                @for (a of arrows(); track a) { <path [attr.d]="a" fill="none" stroke="#94a3b8" stroke-width="1.2" marker-end="url(#prj-arrow)" /> }
              </svg>
              @for (b of bars(); track b.task.id) {
                @if (b.baseLeft !== null) { <div class="base-bar" [style.left.px]="b.baseLeft" [style.width.px]="b.baseWidth" [style.top.px]="b.top + 24" [title]="'Baseline: ' + b.task.baselineStart + ' to ' + b.task.baselineEnd"></div> }
                <div class="bar" role="button" tabindex="0" [class.critical]="b.task.critical" [class.done]="b.task.status === 'done'" [class.late]="b.task.late" [class.parent]="isParent(b.task)" [class.can-drag]="canDrag(b.task)"
                     [style.left.px]="b.left + (drag()?.id === b.task.id && drag()?.mode === 'move' ? drag()!.dx : 0)" [style.width.px]="barWidth(b)" [style.top.px]="b.top + 6"
                     [attr.data-testid]="'bar-' + b.task.number" [attr.aria-label]="b.task.number + ' ' + b.task.title + ', ' + b.task.start + ' to ' + b.task.end"
                     [title]="b.task.number + ' ' + b.task.title + '\n' + b.task.start + ' to ' + b.task.end + (b.task.assignedTo ? '\n' + b.task.assignedTo : '') + (b.task.critical ? '\nCritical path' : '\nSlack ' + b.task.slackDays + ' days')"
                     (pointerdown)="dragStart($event, b, 'move')" (keydown.enter)="opened.emit(b.task.id)">
                  <span class="bar-text">{{ barWidth(b) > 70 ? (b.task.assignedTo || '') : '' }}</span>
                  @if (canDrag(b.task)) { <span class="grip" (pointerdown)="dragStart($event, b, 'resize')" title="Drag to change the due date"></span> }
                </div>
              }
              @for (m of milestonePoints(); track m.id) {
                <div class="ms" [class.ms-done]="m.status !== 'pending'" [style.left.px]="m.left" [style.top.px]="milestoneTop() + 9" [title]="m.title + ', ' + m.date"></div>
                <span class="ms-text" [style.left.px]="m.left + 14" [style.top.px]="milestoneTop() + 8">{{ m.title }}</span>
              }
            </div>
          </div>
        </div>
      } @else { <div class="prj-empty">No tasks yet. Add tasks to see the plan.</div> }
    } @else { <div class="prj-empty">Loading the plan</div> }
  `,
  styles: [PRJ_STYLES + `
    .legend { display:inline-flex; align-items:center; gap:5px; font-size:12px; color:var(--erp-color-text-secondary,#64748b); flex-wrap:wrap; }
    .sw { display:inline-block; width:14px; height:9px; border-radius:3px; background:#60a5fa; margin-left:8px; } .sw.critical { background:#ef4444; } .sw.done { background:#94a3b8; } .sw.late { background:#60a5fa; outline:2px solid #f59e0b; } .sw.base { background:#475569; height:4px; }
    .diamond { display:inline-block; width:9px; height:9px; background:#7c3aed; transform:rotate(45deg); margin-left:8px; }
    .gantt { display:flex; border:1px solid var(--erp-color-border-subtle,#e2e8f0); border-radius:10px; background:var(--erp-color-bg-surface,#fff); overflow:hidden; }
    .labels { position:relative; flex:0 0 240px; border-right:1px solid var(--erp-color-border-subtle,#e2e8f0); } .labels-head { display:flex; align-items:flex-end; padding:0 10px 6px; font-size:12px; font-weight:600; color:var(--erp-color-text-secondary,#64748b); border-bottom:1px solid var(--erp-color-border-subtle,#e2e8f0); }
    .label { position:absolute; left:0; right:0; height:${ROW}px; padding:0 10px; background:none; border:0; text-align:left; font-size:12.5px; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; color:var(--erp-color-text-primary,#0f172a); cursor:pointer; border-bottom:1px solid var(--erp-color-border-subtle,#f1f5f9); }
    .label:hover { background:var(--erp-color-bg-subtle,#f8fafc); } .label.sub { padding-left:24px; color:var(--erp-color-text-secondary,#475569); } .num { color:var(--erp-color-text-secondary,#94a3b8); font-size:11.5px; } .ms-label { cursor:default; font-weight:600; line-height:${ROW}px; }
    .track-scroll { flex:1; overflow-x:auto; min-width:0; } .track { position:relative; }
    .band { position:absolute; top:0; height:22px; font-size:11.5px; font-weight:600; padding:3px 6px; color:var(--erp-color-text-secondary,#475569); border-left:1px solid var(--erp-color-border-subtle,#e2e8f0); white-space:nowrap; overflow:hidden; }
    .col { position:absolute; border-left:1px solid var(--erp-color-border-subtle,#f1f5f9); } .col span { display:block; text-align:center; font-size:11px; color:var(--erp-color-text-secondary,#94a3b8); height:24px; line-height:24px; border-bottom:1px solid var(--erp-color-border-subtle,#e2e8f0); overflow:hidden; } .col.off { background:var(--erp-color-bg-subtle,rgba(148,163,184,.1)); }
    .today { position:absolute; top:22px; bottom:0; width:2px; background:#10955d; z-index:3; }
    .arrows { position:absolute; left:0; top:0; pointer-events:none; z-index:2; }
    .bar { position:absolute; height:20px; min-width:6px; border-radius:5px; background:#60a5fa; z-index:4; display:flex; align-items:center; overflow:hidden; } .bar.can-drag { cursor:grab; touch-action:none; } .bar.critical { background:#ef4444; } .bar.done { background:#94a3b8; } .bar.late { box-shadow:0 0 0 2px #f59e0b; } .bar.parent { height:8px; margin-top:6px; border-radius:2px; background:#334155; }
    .bar-text { color:#fff; font-size:11px; padding:0 6px; white-space:nowrap; overflow:hidden; flex:1; } .grip { width:8px; align-self:stretch; cursor:ew-resize; background:rgba(0,0,0,.18); flex:none; }
    .base-bar { position:absolute; height:4px; border-radius:2px; background:#475569; z-index:3; }
    .ms { position:absolute; width:12px; height:12px; background:#7c3aed; transform:rotate(45deg); margin-left:-6px; z-index:4; } .ms.ms-done { background:#10955d; } .ms-text { position:absolute; font-size:11.5px; white-space:nowrap; color:var(--erp-color-text-secondary,#475569); z-index:4; }
    @media (max-width:720px) { .labels { flex-basis:130px; } }
  `],
})
export class ProjectGanttComponent {
  readonly projectId = input.required<string>();
  /** A task row or bar was chosen. */
  readonly opened = output<string>();
  /** The plan changed by dragging or saving a baseline. */
  readonly changed = output<void>();

  private readonly api = inject(ProjectsExtraApi);
  private readonly tasksApi = inject(ProjectTasksApi);
  private readonly msg = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly session = inject(SessionStore);

  readonly HEAD = HEAD;
  readonly zooms = [{ value: 34, label: 'Days' }, { value: 16, label: 'Weeks' }, { value: 6, label: 'Months' }];
  readonly zoom = signal(16);
  readonly schedule = signal<ScheduleDto | null>(null);
  readonly config = signal<ProjectsConfig | null>(null);
  readonly drag = signal<{ id: string; mode: 'move' | 'resize'; dx: number } | null>(null);

  /** First and last day on the axis, with some space around the plan. */
  private readonly range = computed(() => {
    const s = this.schedule(); if (!s) return null;
    const today = parse(toIsoDate(new Date()));
    const from = Math.min(parse(s.start), today);
    const to = Math.max(parse(s.finish), today);
    return { from: from - 3 * DAY, to: to + 7 * DAY };
  });
  readonly totalWidth = computed(() => { const r = this.range(); return r ? Math.round((r.to - r.from) / DAY + 1) * this.zoom() : 0; });
  readonly bars = computed<Bar[]>(() => {
    const s = this.schedule(); const r = this.range(); if (!s || !r) return [];
    const z = this.zoom();
    return this.ordered(s.tasks).map((task, i) => ({
      task, top: HEAD + i * ROW,
      left: ((parse(task.start) - r.from) / DAY) * z, width: Math.max(z * 0.6, ((parse(task.end) - parse(task.start)) / DAY + 1) * z),
      baseLeft: task.baselineStart ? ((parse(task.baselineStart) - r.from) / DAY) * z : null,
      baseWidth: task.baselineStart && task.baselineEnd ? ((parse(task.baselineEnd) - parse(task.baselineStart)) / DAY + 1) * z : 0,
    }));
  });
  readonly milestoneTop = computed(() => HEAD + this.bars().length * ROW);
  readonly height = computed(() => this.milestoneTop() + (this.schedule()?.milestones.length ? ROW : 0) + 6);
  readonly milestonePoints = computed(() => {
    const s = this.schedule(); const r = this.range(); if (!s || !r) return [];
    return s.milestones.map((m) => ({ ...m, left: ((parse(m.date) - r.from) / DAY) * this.zoom() }));
  });
  readonly todayLeft = computed(() => { const r = this.range(); return r ? ((parse(toIsoDate(new Date())) - r.from) / DAY) * this.zoom() : null; });
  /** The lower scale: days when there is room, otherwise the start of each week. */
  readonly columns = computed<Column[]>(() => {
    const r = this.range(); if (!r) return [];
    const z = this.zoom(); const out: Column[] = [];
    const work = this.config()?.workDays ?? [1, 2, 3, 4, 5];
    for (let t = r.from, i = 0; t <= r.to; t += DAY, i++) {
      const d = new Date(t);
      const off = !work.includes(d.getUTCDay());
      if (z >= 24) out.push({ left: i * z, width: z, label: String(d.getUTCDate()), off });
      else if (z >= 10) { if (d.getUTCDay() === 1 || off) out.push({ left: i * z, width: z, label: d.getUTCDay() === 1 ? String(d.getUTCDate()) : '', off }); }
      else if (d.getUTCDay() === 1) out.push({ left: i * z, width: 7 * z, label: '', off: false });
    }
    return out;
  });
  /** The upper scale: months. */
  readonly bands = computed<Band[]>(() => {
    const r = this.range(); if (!r) return [];
    const z = this.zoom(); const out: Band[] = [];
    let start = r.from;
    while (start <= r.to) {
      const d = new Date(start);
      const next = Date.UTC(d.getUTCFullYear(), d.getUTCMonth() + 1, 1);
      const end = Math.min(next, r.to + DAY);
      out.push({ left: ((start - r.from) / DAY) * z, width: ((end - start) / DAY) * z, label: d.toLocaleDateString(undefined, { month: 'short', year: 'numeric', timeZone: 'UTC' }) });
      start = next;
    }
    return out;
  });
  /** One path from the end of each task to the start of the task that waits for it. */
  readonly arrows = computed<string[]>(() => {
    const byId = new Map(this.bars().map((b) => [b.task.id, b]));
    const out: string[] = [];
    for (const b of this.bars()) {
      for (const dep of b.task.dependsOn) {
        const from = byId.get(dep); if (!from) continue;
        const x1 = from.left + from.width; const y1 = from.top + 16; const x2 = b.left; const y2 = b.top + 16;
        const mid = x2 >= x1 + 12 ? x2 - 8 : x1 + 8;
        out.push(x2 >= x1 + 12 ? `M${x1},${y1} H${mid} V${y2} H${x2 - 1}` : `M${x1},${y1} H${mid} V${y1 + 14} H${x2 - 10} V${y2} H${x2 - 1}`);
      }
    }
    return out;
  });

  constructor() {
    this.api.config().pipe(catchError(() => of(null))).subscribe((c) => this.config.set(c));
    effect(() => { this.projectId(); this.load(); });
  }

  load(): void {
    this.api.schedule(this.projectId()).subscribe({ next: (s) => this.schedule.set(s), error: (e) => this.msg.error(errorText(e, 'Could not load the plan.')) });
  }

  canManage(): boolean { return this.session.hasPermission('projects.tasks.manage'); }
  isParent(t: ScheduleTaskDto): boolean { return this.schedule()?.tasks.some((x) => x.parentTaskId === t.id) ?? false; }
  canDrag(t: ScheduleTaskDto): boolean { return this.canManage() && t.status !== 'done' && !this.isParent(t); }
  barWidth(b: Bar): number {
    const d = this.drag();
    return d?.id === b.task.id && d.mode === 'resize' ? Math.max(this.zoom() * 0.6, b.width + d.dx) : b.width;
  }

  /** Parents followed by their subtasks, the rest in plan order. */
  private ordered(tasks: ScheduleTaskDto[]): ScheduleTaskDto[] {
    const tops = tasks.filter((t) => !t.parentTaskId).sort((a, b) => parse(a.start) - parse(b.start) || a.number.localeCompare(b.number));
    return tops.flatMap((t) => [t, ...tasks.filter((k) => k.parentTaskId === t.id).sort((a, b) => parse(a.start) - parse(b.start))]);
  }

  dragStart(e: PointerEvent, b: Bar, mode: 'move' | 'resize'): void {
    e.stopPropagation();
    if (!this.canDrag(b.task)) { if (mode === 'move') this.opened.emit(b.task.id); return; }
    const startX = e.clientX;
    let moved = false;
    this.drag.set({ id: b.task.id, mode, dx: 0 });
    const onMove = (ev: PointerEvent) => { const dx = ev.clientX - startX; if (Math.abs(dx) > 3) moved = true; this.drag.set({ id: b.task.id, mode, dx }); };
    const onUp = (ev: PointerEvent) => {
      window.removeEventListener('pointermove', onMove); window.removeEventListener('pointerup', onUp);
      this.drag.set(null);
      const days = Math.round((ev.clientX - startX) / this.zoom());
      if (!moved) { this.opened.emit(b.task.id); return; }
      if (days !== 0) this.applyDrag(b.task, mode, days);
    };
    window.addEventListener('pointermove', onMove); window.addEventListener('pointerup', onUp);
  }

  private applyDrag(t: ScheduleTaskDto, mode: 'move' | 'resize', days: number): void {
    const start = mode === 'move' ? parse(t.start) + days * DAY : parse(t.start);
    const end = Math.max(start, parse(t.end) + days * DAY);
    this.api.task(t.id).subscribe({
      next: (dto) => this.tasksApi.update(t.id, { ...taskToInput(dto), startDate: fmt(start), dueDate: fmt(end) }).subscribe({
        next: () => { this.msg.success(`${t.number} now runs ${fmt(start)} to ${fmt(end)}`); this.load(); this.changed.emit(); },
        error: (e) => { this.msg.error(errorText(e, 'Could not move the task.')); this.load(); },
      }),
      error: (e) => this.msg.error(errorText(e, 'Could not move the task.')),
    });
  }

  baseline(clear: boolean): void {
    const go = () => this.api.setBaseline(this.projectId(), clear).subscribe({
      next: (s) => { this.schedule.set(s); this.msg.success(clear ? 'Baseline cleared' : 'Baseline saved'); },
      error: (e) => this.msg.error(errorText(e, 'Could not save the baseline.')),
    });
    if (!clear && this.schedule()?.hasBaseline) this.modal.confirm({ nzTitle: 'Replace the saved baseline?', nzContent: 'The current plan becomes what later changes are compared with.', nzOkText: 'Replace', nzOnOk: go });
    else go();
  }
}
