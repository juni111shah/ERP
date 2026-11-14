import { CdkDrag, CdkDragDrop, CdkDropList, DragDropModule } from '@angular/cdk/drag-drop';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { ProjectTaskStatus, ProjectTasksApi, TaskDto } from '@core/api/projects.api';
import { SessionStore } from '@core/session/session.store';
import { ErpDatePipe } from '@shared/pipes/display.pipes';
import { ProjectTagComponent } from '../../components/project-tag/project-tag.component';
import { TaskDrawerComponent } from '../../components/task-drawer/task-drawer.component';
import { initials, isoToday, splitLabels } from '../../projects-format';
import { injectOpenProjects } from '../../projects-lookups';
import { errorText, PRJ_STYLES, statusLabel, TASK_MOVES, TASK_PRIORITIES, TASK_STATUSES, taskOptions, StatusOption } from '../../projects-ui';

const BOARD: ProjectTaskStatus[] = ['todo', 'inProgress', 'review', 'blocked', 'done'];
type Group = 'none' | 'assignee' | 'priority' | 'project';

/** Task board: drag a card to another column (or use its "Move to" box) to change its status. Works across projects or inside one. */
@Component({
  selector: 'erp-project-board',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, DragDropModule, FormsModule, RouterModule, NzButtonModule, NzIconModule, NzInputModule, NzSelectModule, ProjectTagComponent, TaskDrawerComponent],
  templateUrl: './project-board.page.html',
  styles: [PRJ_STYLES + `
    .prj-sr { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }
    :host { display:block; min-width:0; } .scroller { overflow-x:auto; padding-bottom:6px; max-width:100%; } .heads, .lane { display:grid; grid-template-columns:repeat(5, minmax(210px, 1fr)); gap:10px; min-width:1090px; }
    .head { font-weight:600; color:var(--erp-color-text-primary,#334155); padding:6px 8px; display:flex; justify-content:space-between; font-size:13px; }
    .count { color:var(--erp-color-text-secondary,#64748b); font-weight:600; } .lane-title { font-weight:600; font-size:13px; margin:10px 0 6px; color:var(--erp-color-text-primary,#0f172a); min-width:1090px; }
    .col { background:var(--erp-color-bg-subtle,#f1f5f9); border-radius:10px; padding:6px; min-height:120px; display:flex; flex-direction:column; gap:6px; }
    .list { min-height:90px; display:flex; flex-direction:column; gap:7px; flex:1; }
    .card { background: var(--erp-card-bg); border: 1px solid var(--erp-card-border); box-shadow: var(--erp-card-shadow); border-radius:9px; padding:9px; cursor:pointer; } .card:focus-visible { outline:2px solid var(--erp-color-primary,#10955d); }
    .card.cdk-drag-preview { --erp-card-shadow: var(--erp-shadow-3); } .cdk-drag-placeholder { opacity:.35; } .cdk-drop-list-receiving { outline:2px dashed var(--erp-color-primary,#10955d); outline-offset:2px; border-radius:8px; }
    .top { display:flex; justify-content:space-between; align-items:center; } .num { font-size:11.5px; color:var(--erp-color-text-secondary,#64748b); } .title { font-weight:600; margin:4px 0; color:var(--erp-color-text-primary,#0f172a); font-size:13px; }
    .project { display:block; font-size:11.5px; color:var(--erp-color-primary,#10955d); margin-bottom:4px; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; }
    .meta { display:flex; gap:8px; flex-wrap:wrap; align-items:center; font-size:11.5px; color:var(--erp-color-text-secondary,#64748b); } .late { color:#cf1322; font-weight:600; }
    .who { display:inline-flex; width:22px; height:22px; border-radius:50%; align-items:center; justify-content:center; background:var(--erp-color-primary,#10955d); color:#fff; font-size:10px; font-weight:600; }
    .labels { display:flex; gap:4px; flex-wrap:wrap; margin-top:5px; } .labels span { font-size:10.5px; background:var(--erp-color-bg-subtle,#eef2f6); color:var(--erp-color-text-secondary,#475569); border-radius:4px; padding:1px 6px; }
    .mover { width:100%; margin-top:7px; } .empty { color:var(--erp-color-text-secondary,#94a3b8); font-size:12px; text-align:center; padding:14px 0; } .quick { background:var(--erp-color-bg-surface,#fff); }
  `],
})
export class ProjectBoardPage {
  /** Set inside a project page, or by ?projectId= in the address. */
  readonly projectId = input<string | null>(null);

  private readonly tasksApi = inject(ProjectTasksApi);
  private readonly msg = inject(NzMessageService);
  private readonly session = inject(SessionStore);
  readonly drawer = viewChild(TaskDrawerComponent);
  readonly columns = TASK_STATUSES.filter((s) => BOARD.includes(s.value as ProjectTaskStatus));
  readonly projects = injectOpenProjects();
  readonly pickedProject = signal<string | null>(null);
  readonly who = signal<string | null>(null);
  readonly term = signal('');
  readonly mineOnly = signal(false);
  readonly group = signal<Group>('none');
  readonly tasks = signal<TaskDto[]>([]);
  readonly truncated = signal(false);
  readonly assignees = computed(() => [...new Set(this.tasks().map((t) => t.assignedTo).filter((x): x is string => !!x))].sort());
  private readonly visible = computed(() => {
    const who = this.who(); const term = this.term().trim().toLowerCase(); const me = this.session.me()?.user?.id; const mine = this.mineOnly();
    return this.tasks().filter((t) => (!who || (who === '__none' ? !t.assignedTo : t.assignedTo === who))
      && (!term || t.title.toLowerCase().includes(term) || t.number.toLowerCase().includes(term))
      && (!mine || t.assignedUserId === me));
  });
  readonly lanes = computed(() => {
    const rows = this.visible(); const g = this.group();
    if (g === 'none') return [{ key: 'all', label: '', tasks: rows }];
    const keyOf = (t: TaskDto): string => (g === 'assignee' ? t.assignedTo || 'Unassigned' : g === 'project' ? t.projectName : statusLabel('priority', t.priority));
    const keys = g === 'priority' ? [...TASK_PRIORITIES].reverse().map((p) => p.label).filter((l) => rows.some((t) => keyOf(t) === l)) : [...new Set(rows.map(keyOf))].sort();
    return keys.map((key) => ({ key, label: key, tasks: rows.filter((t) => keyOf(t) === key) }));
  });

  constructor() {
    effect(() => { this.projectId(); this.load(); });
  }

  canManage(): boolean { return this.session.hasPermission('projects.tasks.manage'); }
  /** The project page needs read access to projects, which a tasks-only user may not have. */
  canOpenProject(): boolean { return this.session.hasPermission('projects.projects.read'); }

  load(): void {
    const id = this.projectId() || this.pickedProject();
    const filters = [{ field: 'status', op: 'ne' as const, value: 'cancelled' }, ...(id ? [{ field: 'projectId', op: 'eq' as const, value: id }] : [])];
    this.tasksApi.list({ page: 1, pageSize: 200, sort: 'dueDate', filters, ...(id ? {} : { extra: { live: true } }) }).subscribe({
      next: (r) => { this.tasks.set(r.items); this.truncated.set(r.total > r.items.length); },
      error: (e) => this.msg.error(errorText(e, 'Could not load the tasks.')),
    });
  }
  pickProject(id: string | null): void { this.pickedProject.set(id); this.load(); }
  inColumn(tasks: TaskDto[], s: string): TaskDto[] { return tasks.filter((t) => t.status === s); }
  countOf(s: string): number { return this.visible().filter((t) => t.status === s).length; }
  moves(t: TaskDto): StatusOption[] { return taskOptions(t.status); }
  labels(t: TaskDto): string[] { return splitLabels(t.labels); }
  initials(name: string): string { return initials(name); }
  isLate(t: TaskDto): boolean { return !!t.dueDate && t.dueDate < isoToday() && t.status !== 'done'; }

  /** A card can only be dropped where the status flow allows, so the column shows that before the drop. */
  readonly canEnter = (drag: CdkDrag<TaskDto>, drop: CdkDropList<ProjectTaskStatus>): boolean => {
    const t = drag.data;
    return t.status === drop.data || (TASK_MOVES[t.status]?.includes(drop.data) ?? false);
  };

  drop(e: CdkDragDrop<ProjectTaskStatus>, _lane: string): void {
    if (e.previousContainer === e.container) return;
    this.move(e.item.data as TaskDto, e.container.data);
  }

  move(t: TaskDto, status: ProjectTaskStatus): void {
    if (t.status === status) return;
    const before = this.tasks();
    this.tasks.set(before.map((x) => (x.id === t.id ? { ...x, status } : x)));   // show it at once, undo when the server refuses
    this.tasksApi.setStatus(t.id, status).subscribe({
      next: (saved) => this.tasks.set(this.tasks().map((x) => (x.id === saved.id ? { ...x, ...saved, projectName: x.projectName || saved.projectName } : x))),
      error: (e) => { this.tasks.set(before); this.msg.error(errorText(e, 'That move is not allowed.')); },
    });
  }

  quickAdd(el: HTMLInputElement): void {
    const title = el.value.trim(); const id = this.projectId() || this.pickedProject();
    if (!title || !id) return;
    this.tasksApi.create({ projectId: id, title, status: 'todo', priority: 'medium', estimatedHours: 0, actualHours: 0 }).subscribe({
      next: () => { el.value = ''; this.load(); },
      error: (e) => this.msg.error(errorText(e, 'Could not add the task.')),
    });
  }
}
