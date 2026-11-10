import { CdkDrag, CdkDragDrop, CdkDropList, DragDropModule } from '@angular/cdk/drag-drop';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, viewChild } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { catchError, of } from 'rxjs';
import { ProjectTaskStatus, ProjectTasksApi, ProjectsApi, TaskDto } from '@core/api/projects.api';
import { toIsoDate } from '@core/custom-fields/custom-field-form';
import { SessionStore } from '@core/session/session.store';
import { errorText, PRJ_STYLES, ProjectTagComponent, statusLabel, TASK_MOVES, TASK_PRIORITIES, TASK_STATUSES, taskOptions } from '../../projects-ui';
import { TaskDrawerComponent } from '../../components/task-drawer/task-drawer.component';
import { ErpDatePipe } from '@shared/pipes/display.pipes';

const BOARD: ProjectTaskStatus[] = ['todo', 'inProgress', 'review', 'blocked', 'done'];
type Group = 'none' | 'assignee' | 'priority' | 'project';

/** Task board: drag a card to another column (or use its "Move to" box) to change its status. Works across projects or inside one. */
@Component({
  selector: 'erp-project-board',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, DragDropModule, FormsModule, RouterModule, NzButtonModule, NzIconModule, NzInputModule, NzSelectModule, ProjectTagComponent, TaskDrawerComponent],
  template: `
    <div class="prj-toolbar">
      @if (!projectId()) {
        <label class="prj-sr" for="board-project">Project</label>
        <nz-select nzId="board-project" [ngModel]="pickedProject()" (ngModelChange)="pickProject($event)" nzShowSearch nzAllowClear nzPlaceHolder="All projects" style="width:280px" data-testid="board-project">
          @for (p of projects().items; track p.id) { <nz-option [nzValue]="p.id" [nzLabel]="p.number + ' · ' + p.name"></nz-option> }
        </nz-select>
      }
      <label class="prj-sr" for="board-assignee">Assignee</label>
      <nz-select nzId="board-assignee" [ngModel]="who()" (ngModelChange)="who.set($event)" nzAllowClear nzPlaceHolder="Everyone" style="width:180px" data-testid="board-assignee">
        <nz-option nzValue="__none" nzLabel="Unassigned"></nz-option>
        @for (n of assignees(); track n) { <nz-option [nzValue]="n" [nzLabel]="n"></nz-option> }
      </nz-select>
      <input nz-input [ngModel]="term()" (ngModelChange)="term.set($event)" placeholder="Search tasks" aria-label="Search tasks" style="width:180px" data-testid="board-search" />
      <button type="button" class="prj-chip" [class.on]="mineOnly()" (click)="mineOnly.set(!mineOnly())" data-testid="board-mine">My tasks</button>
      <label class="prj-sr" for="board-group">Group rows by</label>
      <nz-select nzId="board-group" [ngModel]="group()" (ngModelChange)="group.set($event)" style="width:170px" data-testid="board-group">
        <nz-option nzValue="none" nzLabel="One row"></nz-option><nz-option nzValue="assignee" nzLabel="Row per person"></nz-option><nz-option nzValue="priority" nzLabel="Row per priority"></nz-option>
        @if (!projectId()) { <nz-option nzValue="project" nzLabel="Row per project"></nz-option> }
      </nz-select>
      <span class="prj-spacer"></span>
      @if (canManage()) { <button nz-button nzType="primary" (click)="drawer()?.create({ projectId: projectId() || pickedProject() })" data-testid="board-new"><span nz-icon nzType="plus"></span> New task</button> }
    </div>

    <div class="scroller" data-testid="board">
      <div class="heads">
        @for (col of columns; track col.value) { <div class="head">{{ col.label }} <span class="count" [attr.data-testid]="'count-' + col.value">{{ countOf(col.value) }}</span></div> }
      </div>
      <div cdkDropListGroup>
        @for (lane of lanes(); track lane.key) {
          @if (group() !== 'none') { <div class="lane-title">{{ lane.label }} <span class="prj-muted">{{ lane.tasks.length }}</span></div> }
          <div class="lane">
            @for (col of columns; track col.value) {
              <section class="col" [attr.data-testid]="'col-' + col.value">
                <div class="list" cdkDropList [cdkDropListData]="$any(col.value)" [cdkDropListEnterPredicate]="canEnter" [cdkDropListDisabled]="!canManage()" (cdkDropListDropped)="drop($event, lane.key)">
                  @for (t of inColumn(lane.tasks, col.value); track t.id) {
                    <article class="card erp-lift" cdkDrag [cdkDragData]="t" [cdkDragDisabled]="!canManage()" [attr.data-testid]="'card-' + t.number" tabindex="0" (click)="drawer()?.open(t.id)" (keydown.enter)="drawer()?.open(t.id)">
                      <div class="top"><span class="num">{{ t.number }}</span><erp-prj-tag kind="priority" [value]="t.priority" /></div>
                      <div class="title">@if (t.parentTaskId) { <span class="prj-muted" title="Subtask">↳ </span> }{{ t.title }}</div>
                      @if (!projectId() && group() !== 'project') { <a class="project" [routerLink]="['/projects', t.projectId]" [queryParams]="{ tab: 'tasks' }" (click)="$event.stopPropagation()">{{ t.projectName }}</a> }
                      <div class="meta">
                        @if (t.assignedTo) { <span class="who" [title]="t.assignedTo">{{ initials(t.assignedTo) }}</span> }
                        @if (t.dueDate) { <span [class.late]="isLate(t)">{{ t.dueDate | erpDate }}</span> }
                        @if (t.estimatedHours) { <span>{{ t.actualHours }}/{{ t.estimatedHours }} h</span> }
                      </div>
                      @if (labels(t).length) { <div class="labels">@for (l of labels(t); track l) { <span>{{ l }}</span> }</div> }
                      @if (canManage()) {
                        <label class="prj-sr" [for]="'board-move-' + t.id">Move {{ t.number }} to</label>
                        <nz-select [nzId]="'board-move-' + t.id" [ngModel]="t.status" (ngModelChange)="move(t, $event)" nzSize="small" class="mover" (click)="$event.stopPropagation()">
                          @for (s of moves(t); track s.value) { <nz-option [nzValue]="s.value" [nzLabel]="s.value === t.status ? s.label : 'Move to: ' + s.label"></nz-option> }
                        </nz-select>
                      }
                    </article>
                  } @empty { <div class="empty">Nothing here</div> }
                </div>
                @if (canManage() && col.value === 'todo' && (projectId() || pickedProject()) && lane.key === lanes()[0].key) {
                  <input nz-input nzSize="small" class="quick" placeholder="+ Add a task and press Enter" aria-label="Add a task" maxlength="200" #quick (keyup.enter)="quickAdd(quick)" data-testid="board-quick-add" />
                }
              </section>
            }
          </div>
        } @empty { <div class="prj-empty">No tasks yet.</div> }
      </div>
    </div>
    @if (truncated()) { <p class="prj-muted">Showing the first 200 tasks. Choose a project to narrow the board.</p> }
    <erp-task-drawer (changed)="load()" />
  `,
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
  private readonly projectsApi = inject(ProjectsApi);
  private readonly msg = inject(NzMessageService);
  private readonly session = inject(SessionStore);
  readonly drawer = viewChild(TaskDrawerComponent);
  readonly columns = TASK_STATUSES.filter((s) => BOARD.includes(s.value as ProjectTaskStatus));
  readonly projects = toSignal(this.projectsApi.open().pipe(catchError(() => of({ items: [] }))), { initialValue: { items: [] } });
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
  moves(t: TaskDto) { return taskOptions(t.status); }
  labels(t: TaskDto): string[] { return (t.labels ?? '').split(',').map((x) => x.trim()).filter(Boolean); }
  initials(name: string): string { return name.split(/\s+/).map((n) => n[0]).slice(0, 2).join('').toUpperCase(); }
  isLate(t: TaskDto): boolean { return !!t.dueDate && t.dueDate < toIsoDate(new Date()) && t.status !== 'done'; }

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
