import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { ProjectsConfig, ProjectsExtraApi, ScheduleDto, ScheduleTaskDto } from '@core/api/projects-extra.api';
import { ProjectTasksApi, taskToInput } from '@core/api/projects.api';
import { SessionStore } from '@core/session/session.store';
import { loadOrFallback } from '@shared/utils/load-or-fallback';
import { isoToday } from '../../projects-format';
import { Band, Bar, Column, DAY, HEAD, ROW, dayLeft, dependencyArrows, formatDay, ganttBands, ganttBars, ganttColumns, ganttRange, ganttWidth, milestonePoints, parseDay } from '../../projects-gantt';
import { errorText, PRJ_STYLES } from '../../projects-ui';

/**
 * The project timeline: bars on a real date axis, today's line, milestones, arrows for what waits for what, the critical path, and an optional baseline.
 * People who manage tasks drag a bar to move it, or its right edge to change the due date.
 */
@Component({
  selector: 'erp-project-gantt',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NzModalModule, NzButtonModule, NzIconModule],
  templateUrl: './project-gantt.component.html',
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
  private readonly range = computed(() => { const s = this.schedule(); return s ? ganttRange(s, parseDay(isoToday())) : null; });
  readonly totalWidth = computed(() => { const r = this.range(); return r ? ganttWidth(r, this.zoom()) : 0; });
  readonly bars = computed<Bar[]>(() => { const s = this.schedule(); const r = this.range(); return s && r ? ganttBars(s.tasks, r, this.zoom()) : []; });
  readonly milestoneTop = computed(() => HEAD + this.bars().length * ROW);
  readonly height = computed(() => this.milestoneTop() + (this.schedule()?.milestones.length ? ROW : 0) + 6);
  readonly milestonePoints = computed(() => { const s = this.schedule(); const r = this.range(); return s && r ? milestonePoints(s.milestones, r, this.zoom()) : []; });
  readonly todayLeft = computed(() => { const r = this.range(); return r ? dayLeft(parseDay(isoToday()), r, this.zoom()) : null; });
  /** The lower scale: days when there is room, otherwise the start of each week. */
  readonly columns = computed<Column[]>(() => { const r = this.range(); return r ? ganttColumns(r, this.zoom(), this.config()?.workDays ?? [1, 2, 3, 4, 5]) : []; });
  /** The upper scale: months. */
  readonly bands = computed<Band[]>(() => { const r = this.range(); return r ? ganttBands(r, this.zoom()) : []; });
  /** One path from the end of each task to the start of the task that waits for it. */
  readonly arrows = computed<string[]>(() => dependencyArrows(this.bars()));

  constructor() {
    loadOrFallback(this.config, this.api.config(), null);
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

  /** A press on a bar: a drag moves the task (or, on its right edge, its due date); a click opens it. */
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
    const start = mode === 'move' ? parseDay(t.start) + days * DAY : parseDay(t.start);
    const end = Math.max(start, parseDay(t.end) + days * DAY);
    this.api.task(t.id).subscribe({
      next: (dto) => this.tasksApi.update(t.id, { ...taskToInput(dto), startDate: formatDay(start), dueDate: formatDay(end) }).subscribe({
        next: () => { this.msg.success(`${t.number} now runs ${formatDay(start)} to ${formatDay(end)}`); this.load(); this.changed.emit(); },
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
