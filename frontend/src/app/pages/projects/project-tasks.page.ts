import { CommonModule, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import { catchError, of } from 'rxjs';
import { createPagedList } from '../../core/api/paged-list';
import { ProjectTasksApi, ProjectTaskStatus, ProjectsApi, TaskDto, TaskInput, TaskPriority } from '../../core/api/projects.api';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';

@Component({
  selector: 'erp-project-tasks',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    ReactiveFormsModule,
    NzGridModule,
    NzCardModule,
    NzTagModule,
    NzButtonModule,
    NzIconModule,
    NzToolTipModule,
    NzSelectModule,
    NzModalModule,
    NzFormModule,
    NzInputModule,
    NzInputNumberModule,
    NzDatePickerModule,
    PagedTableComponent,
    HasPermissionDirective,
  ],
  template: `
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search task code, title, or assignee..." emptyText="No tasks found.">
      <button toolbar *erpHasPermission="'projects.tasks.manage'" nz-button nzType="primary" (click)="openCreateModal()" data-testid="task-new">
        <span nz-icon nzType="plus"></span> New Task
      </button>
        <ng-template #cell let-row let-column="column" let-value="value">
          @switch (column.key) {
            @case ('number') {
              <span class="code-pill">{{ row.number }}</span>
            }
            @case ('title') {
              <div class="task-title-cell">
                <span class="task-title">{{ row.title }}</span>
                @if (row.description) {
                  <span class="task-desc">{{ row.description }}</span>
                }
              </div>
            }
            @case ('projectName') {
              <a [routerLink]="['/projects', row.projectId]" class="project-link">{{ row.projectName || 'View Project' }}</a>
            }
            @case ('priority') {
              <nz-tag [nzColor]="priorityColor(row.priority)">{{ row.priority }}</nz-tag>
            }
            @case ('status') {
              <nz-select [ngModel]="row.status" (ngModelChange)="updateStatus(row.id, $event)" nzSize="small" style="width: 120px;" (click)="$event.stopPropagation()">
                <nz-option nzValue="Todo" nzLabel="To Do"></nz-option>
                <nz-option nzValue="InProgress" nzLabel="In Progress"></nz-option>
                <nz-option nzValue="Review" nzLabel="Review"></nz-option>
                <nz-option nzValue="Done" nzLabel="Done"></nz-option>
              </nz-select>
            }
            @case ('hours') {
              <span class="hours-cell">{{ row.actualHours }}h / {{ row.estimatedHours }}h</span>
            }
            @case ('dueDate') {
              <span class="date-cell">{{ row.dueDate || '—' }}</span>
            }
            @default {
              {{ value }}
            }
          }
        </ng-template>
      </erp-paged-table>

      <!-- Task Modal -->
      <nz-modal [(nzVisible)]="modalVisible" nzTitle="Create Task" (nzOnCancel)="modalVisible = false" (nzOnOk)="save()">
        <ng-container *nzModalContent>
          <form [formGroup]="form" nz-form nzLayout="vertical">
            <nz-form-item>
              <nz-form-label nzRequired>Project</nz-form-label>
              <nz-form-control>
                <nz-select formControlName="projectId" nzPlaceHolder="Select project">
                  @for (p of projects().items; track p.id) {
                    <nz-option [nzValue]="p.id" [nzLabel]="p.number + ' · ' + p.name"></nz-option>
                  }
                </nz-select>
              </nz-form-control>
            </nz-form-item>
            <nz-form-item>
              <nz-form-label nzRequired>Task Title</nz-form-label>
              <nz-form-control><input nz-input formControlName="title" placeholder="e.g. Implement authentication middleware" /></nz-form-control>
            </nz-form-item>
            <div nz-row [nzGutter]="12">
              <div nz-col [nzSpan]="12">
                <nz-form-item>
                  <nz-form-label>Priority</nz-form-label>
                  <nz-form-control>
                    <nz-select formControlName="priority">
                      <nz-option nzValue="Low" nzLabel="Low"></nz-option>
                      <nz-option nzValue="Medium" nzLabel="Medium"></nz-option>
                      <nz-option nzValue="High" nzLabel="High"></nz-option>
                      <nz-option nzValue="Urgent" nzLabel="Urgent"></nz-option>
                    </nz-select>
                  </nz-form-control>
                </nz-form-item>
              </div>
              <div nz-col [nzSpan]="12">
                <nz-form-item>
                  <nz-form-label>Assigned To</nz-form-label>
                  <nz-form-control><input nz-input formControlName="assignedTo" placeholder="Assignee name" /></nz-form-control>
                </nz-form-item>
              </div>
            </div>
            <div nz-row [nzGutter]="12">
              <div nz-col [nzSpan]="12">
                <nz-form-item>
                  <nz-form-label>Estimated Hours</nz-form-label>
                  <nz-form-control><nz-input-number formControlName="estimatedHours" [nzMin]="0" style="width:100%"></nz-input-number></nz-form-control>
                </nz-form-item>
              </div>
              <div nz-col [nzSpan]="12">
                <nz-form-item>
                  <nz-form-label>Due Date</nz-form-label>
                  <nz-form-control><nz-date-picker formControlName="dueDate" style="width:100%"></nz-date-picker></nz-form-control>
                </nz-form-item>
              </div>
            </div>
            <nz-form-item>
              <nz-form-label>Description</nz-form-label>
              <nz-form-control><input nz-input formControlName="description" placeholder="Task details and scope" /></nz-form-control>
            </nz-form-item>
          </form>
        </ng-container>
      </nz-modal>
  `,
  styles: [`
    .task-title-cell {
      display: flex;
      flex-direction: column;
    }
    .task-title {
      font-weight: 600;
      color: #0f172a;
    }
    .task-desc {
      font-size: 12px;
      color: #64748b;
    }
    .project-link {
      font-weight: 600;
      color: #10955d;
      text-decoration: none;
    }
    .project-link:hover {
      text-decoration: underline;
    }
    .hours-cell {
      font-weight: 500;
      color: #334155;
    }
    .date-cell {
      font-size: 12.5px;
      color: #64748b;
    }
  `],
})
export class ProjectTasksPage {
  private readonly tasksApi = inject(ProjectTasksApi);
  private readonly projectsApi = inject(ProjectsApi);
  private readonly fb = inject(FormBuilder);
  private readonly msg = inject(NzMessageService);

  readonly projects = toSignal(this.projectsApi.list({ page: 1, pageSize: 200 }).pipe(catchError(() => of({ items: [] }))), { initialValue: { items: [] } });
  readonly list = createPagedList((q) => this.tasksApi.list(q), { sort: 'createdAt' });

  modalVisible = false;
  readonly form = this.fb.group({
    projectId: ['', Validators.required],
    title: ['', Validators.required],
    assignedTo: [''],
    priority: ['Medium', Validators.required],
    estimatedHours: [0],
    dueDate: [null],
    description: [''],
  });

  readonly columns: TableColumn<TaskDto>[] = [
    { key: 'number', label: 'Code', sortField: 'number', width: '130px' },
    { key: 'title', label: 'Task Title', sortField: 'title' },
    { key: 'projectName', label: 'Project', width: '180px' },
    { key: 'priority', label: 'Priority', sortField: 'priority', width: '110px' },
    { key: 'status', label: 'Status', sortField: 'status', width: '140px' },
    { key: 'assignedTo', label: 'Assignee', sortField: 'assignedTo', width: '140px' },
    { key: 'hours', label: 'Hours (Act/Est)', width: '140px' },
    { key: 'dueDate', label: 'Due Date', sortField: 'dueDate', width: '130px' },
  ];

  priorityColor(p: TaskPriority): string {
    switch (p) {
      case 'Urgent': return 'error';
      case 'High': return 'warning';
      case 'Medium': return 'blue';
      default: return 'default';
    }
  }

  updateStatus(taskId: string, status: ProjectTaskStatus): void {
    this.tasksApi.setStatus(taskId, status).subscribe(() => {
      this.msg.success('Status updated');
      this.list.reload();
    });
  }

  openCreateModal(): void {
    this.form.reset({ priority: 'Medium', estimatedHours: 0 });
    this.modalVisible = true;
  }

  save(): void {
    if (this.form.invalid) return;
    const v = this.form.value;
    const dueDateStr = v.dueDate ? new Date(v.dueDate).toISOString().split('T')[0] : null;

    this.tasksApi.create({
      projectId: v.projectId!,
      title: v.title!,
      assignedTo: v.assignedTo || null,
      priority: v.priority as TaskPriority,
      estimatedHours: v.estimatedHours || 0,
      actualHours: 0,
      dueDate: dueDateStr,
      description: v.description || null,
      status: 'Todo',
    }).subscribe(() => {
      this.msg.success('Task created');
      this.modalVisible = false;
      this.list.reload();
    });
  }
}
