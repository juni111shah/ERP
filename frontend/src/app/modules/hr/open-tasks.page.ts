import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { asProblem } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { EmployeeLifeApi, OpenTask } from './hr-people.api';

/** Onboarding and offboarding steps nobody has finished yet, overdue ones first. */
@Component({
  selector: 'erp-open-tasks',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [HasPermissionDirective, NzTableModule, NzTagModule],
  template: `
    <h1 class="erp-page-title">HR tasks</h1>
    <nz-table [nzData]="tasks()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" data-testid="open-tasks">
      <thead><tr><th>Employee</th><th>Task</th><th>Kind</th><th>Due</th><th></th></tr></thead>
      <tbody>
        @for (t of tasks(); track t.id) {
          <tr>
            <td>{{ t.employeeName }}</td><td>{{ t.title }}</td><td>{{ t.kind === 'onboarding' ? 'Onboarding' : 'Offboarding' }}</td>
            <td>@if (t.dueDate) { <nz-tag [nzColor]="t.overdue ? 'red' : 'default'">{{ t.dueDate }}</nz-tag> }</td>
            <td><a *erpHasPermission="'hr.employees.manage'" (click)="done(t)" data-testid="task-done">Mark done</a></td>
          </tr>
        } @empty { <tr><td colspan="5" style="text-align:center;color:var(--erp-color-text-secondary)">Nothing is waiting</td></tr> }
      </tbody>
    </nz-table>`,
})
export class OpenTasksPage {
  private readonly api = inject(EmployeeLifeApi);
  private readonly message = inject(NzMessageService);
  readonly tasks = signal<OpenTask[]>([]);
  constructor() { this.load(); }
  done(t: OpenTask): void {
    this.api.setDone(t.id, true).subscribe({ next: () => this.load(), error: (e) => this.message.error(asProblem(e)?.detail ?? 'Could not update the task') });
  }
  private load(): void { this.api.openTasks().subscribe((r) => this.tasks.set(r)); }
}
