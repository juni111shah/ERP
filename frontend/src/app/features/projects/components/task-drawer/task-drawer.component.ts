import { doneText } from '@core/format/done-text';
import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, output, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { catchError, of } from 'rxjs';
import { CommentDto, ProjectsExtraApi, UserOption } from '@core/api/projects-extra.api';
import { MilestoneDto, ProjectMilestonesApi, ProjectsApi, ProjectSummaryDto, ProjectTaskStatus, ProjectTasksApi, TaskDto, TaskInput, TaskPriority } from '@core/api/projects.api';
import { toIsoDate } from '@core/format/dates';
import { SessionStore } from '@core/session/session.store';
import { AttachmentsComponent } from '@shared/components/attachments/attachments.component';
import { errorText, PRJ_STYLES, ProjectTagComponent, TASK_PRIORITIES, TASK_STATUSES, taskOptions } from '../../projects-ui';

const iso = (d: unknown): string | null => (d ? toIsoDate(d instanceof Date ? d : new Date(String(d))) : null);
const toDate = (s?: string | null): Date | null => { if (!s) return null; const [y, m, d] = s.slice(0, 10).split('-').map(Number); return new Date(y, m - 1, d); };

/** One task from start to finish: details, subtasks, what it waits for, discussion and files. Open it with open(id) or create(defaults). */
@Component({
  selector: 'erp-task-drawer',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, FormsModule, ReactiveFormsModule, NzButtonModule, NzDatePickerModule, NzDrawerModule, NzFormModule, NzIconModule, NzInputModule, NzInputNumberModule, NzModalModule, NzSelectModule, ProjectTagComponent, AttachmentsComponent],
  templateUrl: './task-drawer.component.html',
  styles: [PRJ_STYLES + `
    .grid2 { display:grid; grid-template-columns:1fr 1fr; column-gap:12px; } @media (max-width:560px) { .grid2 { grid-template-columns:1fr; } }
    section { border-top:1px solid var(--erp-color-border-subtle,#e2e8f0); margin-top:14px; padding-top:12px; }
    h4 { font-size:13px; font-weight:600; margin:0 0 8px; color:var(--erp-color-text-primary,#0f172a); }
    .row { display:flex; gap:8px; margin-top:8px; align-items:flex-start; } .sub { display:flex; gap:8px; align-items:center; padding:3px 0; } .sub a { cursor:pointer; } .sub-title { flex:1; min-width:0; }
    .comment { padding:6px 0; border-bottom:1px solid var(--erp-color-border-subtle,#f1f5f9); } .comment p { margin:3px 0 0; white-space:pre-wrap; }
    .hours { margin-top:4px; } .foot { display:flex; gap:8px; align-items:center; }
  `],
})
export class TaskDrawerComponent {
  private readonly fb = inject(FormBuilder);
  private readonly tasksApi = inject(ProjectTasksApi);
  private readonly milestonesApi = inject(ProjectMilestonesApi);
  private readonly projectsApi = inject(ProjectsApi);
  private readonly extra = inject(ProjectsExtraApi);
  private readonly msg = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly session = inject(SessionStore);

  /** Fires after any save or delete, so the screen behind can refresh. */
  readonly changed = output<void>();
  readonly priorities = TASK_PRIORITIES;
  readonly width = typeof window !== 'undefined' && window.innerWidth < 680 ? '100%' : 620;

  readonly visible = signal(false);
  readonly saving = signal(false);
  readonly task = signal<TaskDto | null>(null);
  readonly fixedProject = signal(false);
  readonly users = signal<UserOption[]>([]);
  readonly projects = signal<ProjectSummaryDto[]>([]);
  readonly milestones = signal<MilestoneDto[]>([]);
  readonly siblingTasks = signal<TaskDto[]>([]);
  readonly comments = signal<CommentDto[]>([]);
  readonly status = signal<ProjectTaskStatus>('todo');
  newSubtask = ''; newComment = '';

  /** An older task can name someone without an account; the name is kept until a person is chosen. */
  readonly typedAssignee = computed(() => { const t = this.task(); return t && !t.assignedUserId ? t.assignedTo ?? '' : ''; });
  readonly statusChoices = computed(() => (this.task() ? taskOptions(this.task()!.status) : TASK_STATUSES.filter((s) => s.value === 'todo' || s.value === 'inProgress')));
  readonly subtasks = computed(() => this.siblingTasks().filter((t) => t.parentTaskId === this.task()?.id));
  readonly siblings = computed(() => this.siblingTasks().filter((t) => t.id !== this.task()?.id));
  /** Only one level of subtasks: a parent cannot itself be a subtask. */
  readonly parentChoices = computed(() => this.siblings().filter((t) => !t.parentTaskId));

  readonly form = this.fb.group({
    projectId: ['', Validators.required],
    title: ['', [Validators.required, Validators.maxLength(200)]],
    status: ['todo' as ProjectTaskStatus],
    priority: ['medium' as TaskPriority],
    assignedUserId: [null as string | null],
    estimatedHours: [0],
    startDate: [null as Date | null],
    dueDate: [null as Date | null],
    milestoneId: [null as string | null],
    parentTaskId: [null as string | null],
    labels: [[] as string[]],
    description: [''],
  });

  canManage(): boolean { return this.session.hasPermission('projects.tasks.manage'); }

  /** A new task, optionally already in a project and with a parent or a status. */
  create(defaults: { projectId?: string | null; parentTaskId?: string | null; status?: ProjectTaskStatus; assignedUserId?: string | null } = {}): void {
    this.prepare();
    this.task.set(null);
    this.comments.set([]);
    this.fixedProject.set(!!defaults.projectId);
    this.form.reset({ projectId: defaults.projectId ?? '', title: '', status: defaults.status ?? 'todo', priority: 'medium', assignedUserId: defaults.assignedUserId ?? null, estimatedHours: 0, startDate: null, dueDate: null, milestoneId: null, parentTaskId: defaults.parentTaskId ?? null, labels: [], description: '' });
    this.form.enable();
    this.loadProjectLists(defaults.projectId ?? null);
    if (!defaults.projectId) this.projectsApi.open().pipe(catchError(() => of({ items: [] as ProjectSummaryDto[] }))).subscribe((r) => this.projects.set(r.items));
    this.visible.set(true);
  }

  open(id: string): void {
    this.prepare();
    this.extra.task(id).subscribe({
      next: (t) => {
        this.show(t);
        this.visible.set(true);
      },
      error: (e) => this.msg.error(errorText(e, 'Could not open the task.')),
    });
  }

  close(): void { this.visible.set(false); }

  private prepare(): void {
    this.newSubtask = ''; this.newComment = '';
    if (!this.users().length) this.extra.users().pipe(catchError(() => of([] as UserOption[]))).subscribe((u) => this.users.set(u));
  }

  private show(t: TaskDto): void {
    this.task.set(t);
    this.fixedProject.set(true);
    this.form.reset({ projectId: t.projectId, title: t.title, status: t.status, priority: t.priority, assignedUserId: t.assignedUserId ?? null, estimatedHours: t.estimatedHours, startDate: toDate(t.startDate), dueDate: toDate(t.dueDate), milestoneId: t.milestoneId ?? null, parentTaskId: t.parentTaskId ?? null, labels: (t.labels ?? '').split(',').map((x) => x.trim()).filter(Boolean), description: t.description ?? '' });
    if (this.canManage()) this.form.enable(); else this.form.disable();
    this.loadProjectLists(t.projectId);
    this.loadComments(t.id);
  }

  projectChanged(id: string | null): void { this.loadProjectLists(id); }

  private loadProjectLists(projectId: string | null): void {
    if (!projectId) { this.milestones.set([]); this.siblingTasks.set([]); return; }
    const filters = [{ field: 'projectId', op: 'eq' as const, value: projectId }];
    this.milestonesApi.list({ page: 1, pageSize: 100, filters }).pipe(catchError(() => of({ items: [] as MilestoneDto[] }))).subscribe((r) => this.milestones.set(r.items.filter((m) => m.status !== 'cancelled')));
    this.tasksApi.list({ page: 1, pageSize: 200, filters }).pipe(catchError(() => of({ items: [] as TaskDto[] }))).subscribe((r) => this.siblingTasks.set(r.items));
  }

  private loadComments(id: string): void {
    this.extra.comments('task', id).pipe(catchError(() => of([] as CommentDto[]))).subscribe((c) => this.comments.set(c));
  }

  private body(): TaskInput {
    const v = this.form.getRawValue();
    return {
      projectId: v.projectId!, title: (v.title ?? '').trim(), status: v.status!, priority: v.priority!, assignedUserId: v.assignedUserId || null, assignedTo: null, estimatedHours: v.estimatedHours ?? 0, actualHours: this.task()?.actualHours ?? 0,
      startDate: iso(v.startDate), dueDate: iso(v.dueDate), milestoneId: v.milestoneId || null, parentTaskId: v.parentTaskId || null, labels: (v.labels ?? []).join(',') || null, description: v.description?.trim() || null,
    };
  }

  save(): void {
    if (this.form.invalid) { Object.values(this.form.controls).forEach((c) => { c.markAsDirty(); c.updateValueAndValidity(); }); return; }
    const current = this.task();
    const body = this.body();
    // keep the typed name of an assignee who has no account, unless someone was chosen
    if (current && !body.assignedUserId && !current.assignedUserId) body.assignedTo = current.assignedTo ?? null;
    this.saving.set(true);
    (current ? this.tasksApi.update(current.id, body) : this.tasksApi.create(body)).subscribe({
      next: () => { this.saving.set(false); this.msg.success(doneText('saved', 'Task', current?.number ?? body.title)); this.visible.set(false); this.changed.emit(); },
      error: (e) => { this.saving.set(false); this.msg.error(errorText(e, 'Could not save the task.')); },
    });
  }

  remove(): void {
    const t = this.task(); if (!t) return;
    this.modal.confirm({
      nzTitle: `Delete ${t.number}?`, nzContent: t.title, nzOkText: 'Delete', nzOkDanger: true,
      nzOnOk: () => this.tasksApi.delete(t.id).subscribe({
        next: () => { this.msg.success(doneText('deleted', 'Task', t.number)); this.visible.set(false); this.changed.emit(); },
        error: (e) => this.msg.error(errorText(e, 'Could not delete the task.')),
      }),
    });
  }

  addSubtask(): void {
    const t = this.task(); const title = this.newSubtask.trim(); if (!t || !title) return;
    this.tasksApi.create({ projectId: t.projectId, parentTaskId: t.id, title, status: 'todo', priority: t.priority, estimatedHours: 0, actualHours: 0 }).subscribe({
      next: () => { this.newSubtask = ''; this.loadProjectLists(t.projectId); this.changed.emit(); },
      error: (e) => this.msg.error(errorText(e, 'Could not add the subtask.')),
    });
  }

  setDeps(ids: string[]): void {
    const t = this.task(); if (!t) return;
    this.extra.setDependencies(t.id, ids).subscribe({
      next: (saved) => { this.task.set({ ...t, dependsOn: saved.dependsOn }); this.changed.emit(); },
      error: (e) => { this.msg.error(errorText(e, 'Those dependencies are not allowed.')); this.task.set({ ...t }); },
    });
  }

  addComment(): void {
    const t = this.task(); const text = this.newComment.trim(); if (!t || !text) return;
    this.extra.addComment('task', t.id, text).subscribe({
      next: () => { this.newComment = ''; this.loadComments(t.id); },
      error: (e) => this.msg.error(errorText(e, 'Could not post the comment.')),
    });
  }
}
