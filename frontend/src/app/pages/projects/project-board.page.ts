import { CdkDragDrop, DragDropModule } from '@angular/cdk/drag-drop';
import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { catchError, of } from 'rxjs';
import { ProjectTaskStatus, ProjectTasksApi, ProjectsApi, TaskDto } from '../../core/api/projects.api';
import { TASK_STATUSES } from '../../core/api/projects-extra.api';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';

const PRIORITY_COLORS: Record<string, string> = { Low: 'default', Medium: 'blue', High: 'orange', Urgent: 'red' };
const BOARD: ProjectTaskStatus[] = ['Todo', 'InProgress', 'Review', 'Blocked', 'Done'];

/** Task board: drag a card to another column (or use the "Move to" box) to change its status. */
@Component({
  selector: 'erp-project-board',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, DragDropModule, FormsModule, RouterModule, NzSelectModule, NzTagModule],
  template: `
    <h1 class="erp-page-title">Task board</h1>
    <div class="bar">
      <nz-select [ngModel]="projectId()" (ngModelChange)="pickProject($event)" nzShowSearch nzAllowClear nzPlaceHolder="All projects" style="width:320px" aria-label="Project" data-testid="board-project">
        @for (p of projects().items; track p.id) { <nz-option [nzValue]="p.id" [nzLabel]="p.number + ' · ' + p.name"></nz-option> }
      </nz-select>
      <nz-select [ngModel]="mine()" (ngModelChange)="mine.set($event)" nzAllowClear nzPlaceHolder="Everyone" style="width:200px" aria-label="Assignee">
        <nz-option nzValue="__none" nzLabel="Unassigned"></nz-option>
        @for (n of assignees(); track n) { <nz-option [nzValue]="n" [nzLabel]="n"></nz-option> }
      </nz-select>
    </div>
    <div class="board" cdkDropListGroup data-testid="board">
      @for (col of columns; track col.value) {
        <section class="col" [attr.data-testid]="'col-' + col.value">
          <header>{{ col.label }} <span class="count">{{ byStatus(col.value).length }}</span></header>
          <div class="list" cdkDropList [cdkDropListData]="col.value" (cdkDropListDropped)="drop($event)">
            @for (t of byStatus(col.value); track t.id) {
              <article class="card" cdkDrag [cdkDragData]="t" [attr.data-testid]="'card-' + t.number">
                <div class="top"><a [routerLink]="['/projects', t.projectId]">{{ t.number }}</a><nz-tag [nzColor]="priorityColor(t.priority)">{{ t.priority }}</nz-tag></div>
                <div class="title">{{ t.title }}</div>
                <div class="meta">
                  @if (t.assignedTo) { <span>{{ t.assignedTo }}</span> }
                  @if (t.dueDate) { <span [class.late]="isLate(t)">due {{ t.dueDate }}</span> }
                  @if (t.estimatedHours) { <span>{{ t.actualHours }}/{{ t.estimatedHours }}h</span> }
                </div>
                <nz-select [ngModel]="t.status" (ngModelChange)="move(t, $event)" nzSize="small" style="width:100%;margin-top:6px" [attr.aria-label]="'Move ' + t.number + ' to'">
                  @for (s of statuses; track s.value) { <nz-option [nzValue]="s.value" [nzLabel]="'Move to: ' + s.label"></nz-option> }
                </nz-select>
              </article>
            } @empty { <div class="empty">Nothing here</div> }
          </div>
        </section>
      }
    </div>
  `,
  styles: [`
    .bar { display:flex; gap:12px; margin-bottom:12px; }
    .board { display:grid; grid-template-columns: repeat(5, minmax(220px, 1fr)); gap:12px; overflow-x:auto; }
    .col { background:#f8fafc; border:1px solid #e2e8f0; border-radius:12px; padding:8px; min-height:320px; }
    .col header { font-weight:700; color:#334155; padding:4px 6px 8px; display:flex; justify-content:space-between; }
    .count { color:#64748b; font-weight:600; }
    .list { min-height:240px; display:flex; flex-direction:column; gap:8px; }
    .card { background:#fff; border:1px solid #e2e8f0; border-radius:10px; padding:10px; box-shadow:0 1px 2px rgba(0,0,0,.04); cursor:grab; }
    .card.cdk-drag-preview { box-shadow:0 8px 20px rgba(0,0,0,.18); }
    .top { display:flex; justify-content:space-between; align-items:center; font-size:12px; }
    .title { font-weight:600; margin:4px 0; color:#0f172a; }
    .meta { display:flex; gap:8px; flex-wrap:wrap; font-size:12px; color:#64748b; }
    .late { color:#cf1322; font-weight:600; }
    .empty { color:#94a3b8; font-size:12.5px; text-align:center; padding:16px 0; }
  `],
})
export class ProjectBoardPage {
  private readonly tasksApi = inject(ProjectTasksApi);
  private readonly projectsApi = inject(ProjectsApi);
  private readonly msg = inject(NzMessageService);
  readonly statuses = TASK_STATUSES;
  readonly columns = TASK_STATUSES.filter((s) => BOARD.includes(s.value));
  readonly projects = toSignal(this.projectsApi.list({ page: 1, pageSize: 200 }).pipe(catchError(() => of({ items: [] }))), { initialValue: { items: [] } });
  readonly projectId = signal<string | null>(null);
  readonly mine = signal<string | null>(null);
  readonly tasks = signal<TaskDto[]>([]);
  readonly assignees = computed(() => [...new Set(this.tasks().map((t) => t.assignedTo).filter((x): x is string => !!x))].sort());
  private readonly visible = computed(() => {
    const who = this.mine();
    return this.tasks().filter((t) => !who || (who === '__none' ? !t.assignedTo : t.assignedTo === who));
  });

  constructor() { this.load(); }

  load(): void {
    const id = this.projectId();
    const filters = id ? [{ field: 'projectId', op: 'eq' as const, value: id }] : [];
    this.tasksApi.list({ page: 1, pageSize: 500, filters }).subscribe((r) => this.tasks.set(r.items));
  }
  pickProject(id: string | null): void { this.projectId.set(id); this.load(); }
  byStatus(s: ProjectTaskStatus): TaskDto[] { return this.visible().filter((t) => t.status === s); }
  priorityColor(p: string): string { return PRIORITY_COLORS[p] ?? 'default'; }
  isLate(t: TaskDto): boolean { return !!t.dueDate && t.dueDate < toIsoDate(new Date()) && t.status !== 'Done'; }

  drop(e: CdkDragDrop<ProjectTaskStatus>): void {
    if (e.previousContainer === e.container) return;
    this.move(e.item.data as TaskDto, e.container.data);
  }

  move(t: TaskDto, status: ProjectTaskStatus): void {
    if (t.status === status) return;
    const before = this.tasks();
    this.tasks.set(before.map((x) => (x.id === t.id ? { ...x, status } : x)));   // show it at once, undo when the server refuses
    this.tasksApi.setStatus(t.id, status).subscribe({
      next: (saved) => this.tasks.set(this.tasks().map((x) => (x.id === saved.id ? { ...x, ...saved } : x))),
      error: (e) => { this.tasks.set(before); this.msg.error(e?.error?.detail ?? e?.error?.message ?? 'That move is not allowed.'); },
    });
  }
}
