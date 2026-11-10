import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { createPagedList } from '@core/api/paged-list';
import { ProjectTasksApi, ProjectTaskStatus, TaskDto } from '@core/api/projects.api';
import { toIsoDate } from '@core/format/dates';
import { SessionStore } from '@core/session/session.store';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { PagedTableComponent, TableColumn } from '@shared/components/paged-table/paged-table.component';
import { errorText, filterOptions, PRJ_STYLES, ProjectTagComponent, TASK_PRIORITIES, TASK_STATUSES, taskOptions } from '../../projects-ui';
import { TaskDrawerComponent } from '../../components/task-drawer/task-drawer.component';
import { ErpDatePipe } from '@shared/pipes/display.pipes';

/** Tasks of every project, or of one when projectId is given. Click a row to open it. */
@Component({
  selector: 'erp-project-tasks',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, FormsModule, RouterModule, NzButtonModule, NzIconModule, NzSelectModule, PagedTableComponent, HasPermissionDirective, ProjectTagComponent, TaskDrawerComponent],
  templateUrl: './project-tasks.page.html',
  styles: [PRJ_STYLES + '.title-cell { display:flex; flex-direction:column; } .title-cell.sub { padding-inline-start:16px; }'],
})
export class ProjectTasksPage {
  /** Set inside a project page, or by ?projectId= in the address. */
  readonly projectId = input<string | null>(null);

  private readonly tasksApi = inject(ProjectTasksApi);
  private readonly msg = inject(NzMessageService);
  private readonly session = inject(SessionStore);
  readonly drawer = viewChild(TaskDrawerComponent);

  readonly views = [{ key: 'open', label: 'Open' }, { key: 'mine', label: 'Mine' }, { key: 'overdue', label: 'Overdue' }, { key: 'all', label: 'All' }];
  readonly view = signal('open');

  readonly list = createPagedList((q) => {
    const id = this.projectId();
    const filters = [...(q.filters ?? []), ...(id ? [{ field: 'projectId', op: 'eq' as const, value: id }] : [])];
    const view = this.view();
    if (view === 'open' || view === 'overdue') filters.push({ field: 'status', op: 'in', value: ['todo', 'inProgress', 'review', 'blocked'] });
    if (view === 'overdue') filters.push({ field: 'dueDate', op: 'lt', value: toIsoDate(new Date()) });
    if (view === 'mine') { filters.push({ field: 'assignedUserId', op: 'eq', value: this.session.me()?.user?.id ?? '' }); filters.push({ field: 'status', op: 'in', value: ['todo', 'inProgress', 'review', 'blocked'] }); }
    return this.tasksApi.list({ ...q, filters, ...(id ? {} : { extra: { ...(q.extra ?? {}), live: true } }) });
  }, { sort: 'dueDate' });

  readonly columns = computed<TableColumn<TaskDto>[]>(() => [
    { key: 'number', label: 'Code', sortField: 'number', width: '120px', filter: false },
    { key: 'title', label: 'Task', sortField: 'title', width: '340px', filter: false },
    ...(this.projectId() ? [] : [{ key: 'projectName', label: 'Project', width: '190px', filter: false as const }]),
    { key: 'priority', label: 'Priority', sortField: 'priority', width: '110px', filterField: 'priority', filterOptions: filterOptions(TASK_PRIORITIES) },
    { key: 'status', label: 'Status', sortField: 'status', width: '160px', filterField: 'status', filterOptions: filterOptions(TASK_STATUSES) },
    { key: 'assignedTo', label: 'Assigned to', width: '150px', filter: 'text', filterField: 'assignedTo' },
    { key: 'hours', label: 'Hours', width: '120px', filter: false },
    { key: 'dueDate', label: 'Due', sortField: 'dueDate', width: '120px', filter: 'date', filterField: 'dueDate' },
  ]);

  constructor() {
    let first = true;
    effect(() => { this.projectId(); if (first) { first = false; return; } this.list.reload(); });
  }

  setView(key: string): void { this.view.set(key); this.list.setPage(1); }
  canManage(): boolean { return this.session.hasPermission('projects.tasks.manage'); }
  moves(t: TaskDto) { return taskOptions(t.status); }
  isLate(t: TaskDto): boolean { return !!t.dueDate && t.dueDate < toIsoDate(new Date()) && t.status !== 'done' && t.status !== 'cancelled'; }

  updateStatus(t: TaskDto, status: ProjectTaskStatus): void {
    if (t.status === status) return;
    this.tasksApi.setStatus(t.id, status).subscribe({
      next: () => { this.msg.success(`${t.number} is now ${String(status).replace(/_/g, ' ')}`); this.list.reload(); },
      error: (e) => { this.msg.error(errorText(e, 'That change is not allowed.')); this.list.reload(); },
    });
  }
}
