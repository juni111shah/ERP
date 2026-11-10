import { ChangeDetectionStrategy, Component, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { ChecklistRow, ChecklistsApi, EmployeeLifeApi, EmployeeTask, HistoryEntry } from '@core/api/hr-people.api';
import { toIsoDate } from '@core/format/dates';
import { asProblem } from '@core/http/problem-details';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { ErpDatePipe } from '@shared/pipes/display.pipes';

const KIND_LABELS: Record<string, string> = {
  joined: 'Joined', salary: 'Pay', designation: 'Designation', department: 'Department', position: 'Position', manager: 'Manager', contract: 'Contract', status: 'Status', terminated: 'Left', note: 'Note',
};

/** One employee's history of changes, and their onboarding or offboarding tasks. */
@Component({
  selector: 'erp-employee-life',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ErpDatePipe, FormsModule, HasPermissionDirective, NzButtonModule, NzDatePickerModule, NzDrawerModule, NzInputModule, NzSelectModule, NzTabsModule, NzTagModule],
  templateUrl: './employee-life.component.html',
})
export class EmployeeLifeComponent {
  private readonly lifeApi = inject(EmployeeLifeApi);
  private readonly checklistsApi = inject(ChecklistsApi);
  private readonly message = inject(NzMessageService);
  readonly employeeId = input.required<string>();
  readonly employeeName = input('');
  readonly open = input(false);
  readonly closed = output<void>();

  readonly history = signal<HistoryEntry[]>([]);
  readonly tasks = signal<EmployeeTask[]>([]);
  readonly templates = signal<ChecklistRow[]>([]);
  readonly canManage = true;
  note = '';
  taskTitle = '';
  taskDue: Date | null = null;
  templateId: string | null = null;

  constructor() {
    effect(() => {
      if (!this.open()) return;
      this.loadHistory(); this.loadTasks();
      this.checklistsApi.list({ page: 1, pageSize: 100, filters: [{ field: 'isActive', op: 'eq', value: true }] }).subscribe({ next: (p) => this.templates.set(p.items), error: () => this.templates.set([]) });
    });
  }

  kind(k: string): string { return KIND_LABELS[k] ?? k; }

  addNote(): void {
    this.lifeApi.addNote(this.employeeId(), this.note.trim()).subscribe({ next: () => { this.note = ''; this.loadHistory(); }, error: (e) => this.message.error(asProblem(e)?.detail ?? 'Could not add the note') });
  }

  /** Adds the steps of the chosen checklist as tasks (steps already there are skipped by the server). */
  apply(): void {
    this.lifeApi.applyChecklist(this.employeeId(), this.templateId!).subscribe({
      next: (created) => { this.message.success(created.length ? `${created.length} task(s) added` : 'Those tasks were already there'); this.loadTasks(); },
      error: (e) => this.message.error(asProblem(e)?.detail ?? 'Could not apply the checklist'),
    });
  }

  addTask(): void {
    const due = this.taskDue ? toIsoDate(this.taskDue) : null;
    this.lifeApi.addTask(this.employeeId(), { title: this.taskTitle.trim(), dueDate: due, kind: 'onboarding' }).subscribe({
      next: () => { this.taskTitle = ''; this.taskDue = null; this.loadTasks(); },
      error: (e) => this.message.error(asProblem(e)?.detail ?? 'Could not add the task'),
    });
  }

  toggle(t: EmployeeTask): void {
    this.lifeApi.setDone(t.id, !t.isDone).subscribe({ next: () => this.loadTasks(), error: (e) => this.message.error(asProblem(e)?.detail ?? 'Could not update the task') });
  }

  private loadHistory(): void { this.lifeApi.history(this.employeeId()).subscribe((h) => this.history.set(h)); }
  private loadTasks(): void { this.lifeApi.tasks(this.employeeId()).subscribe((t) => this.tasks.set(t)); }
}
