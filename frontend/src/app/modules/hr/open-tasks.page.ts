import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { asProblem } from '../../core/http/problem-details';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { StatePanelComponent } from '../../shared/state-panel.component';
import { EmployeeLifeApi, OpenTask } from './hr-people.api';
import { ErpDatePipe } from '../../shared/display.pipes';

/** Onboarding and offboarding steps nobody has finished yet, overdue ones first. */
@Component({
  selector: 'erp-open-tasks',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, HasPermissionDirective, StatePanelComponent, NzTableModule, NzTagModule],
  template: `
    <h1 class="erp-page-title">HR tasks</h1>
    @if (failed()) {
      <erp-state-panel kind="error" title="HR tasks could not be loaded" hint="Check your connection and try again." actionLabel="Try again" (action)="load()" />
    } @else {
    <nz-table [nzData]="tasks()" [nzLoading]="loading()" [nzFrontPagination]="false" [nzShowPagination]="false" nzSize="small" nzNoResult="Nothing is waiting. Onboarding and offboarding steps appear here." data-testid="open-tasks">
      <thead><tr><th>Employee</th><th>Task</th><th>Kind</th><th>Due</th><th></th></tr></thead>
      <tbody>
        @for (t of tasks(); track t.id) {
          <tr>
            <td>{{ t.employeeName }}</td><td>{{ t.title }}</td><td>{{ t.kind === 'onboarding' ? 'Onboarding' : 'Offboarding' }}</td>
            <td>@if (t.dueDate) { <nz-tag [nzColor]="t.overdue ? 'red' : 'default'">{{ t.dueDate | erpDate }}</nz-tag> }</td>
            <td><a *erpHasPermission="'hr.employees.manage'" (click)="done(t)" data-testid="task-done">Mark done</a></td>
          </tr>
        }
      </tbody>
    </nz-table>
    }`,
})
export class OpenTasksPage {
  private readonly api = inject(EmployeeLifeApi);
  private readonly message = inject(NzMessageService);
  readonly tasks = signal<OpenTask[]>([]);
  readonly loading = signal(false);
  readonly failed = signal(false);
  constructor() { this.load(); }
  done(t: OpenTask): void {
    this.api.setDone(t.id, true).subscribe({ next: () => this.load(), error: (e) => this.message.error(asProblem(e)?.detail ?? 'Could not update the task') });
  }
  load(): void {
    this.loading.set(true); this.failed.set(false);
    this.api.openTasks().subscribe({ next: (r) => { this.tasks.set(r); this.loading.set(false); }, error: () => { this.loading.set(false); this.failed.set(true); } });
  }
}
