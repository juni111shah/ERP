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
import { asProblem } from '@core/http/problem-details';
import { HasPermissionDirective } from '@shared/directives/has-permission.directive';
import { ChecklistRow, ChecklistsApi, EmployeeLifeApi, EmployeeTask, HistoryEntry } from '@core/api/hr-people.api';
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
  template: `
    <nz-drawer [nzVisible]="open()" [nzWidth]="560" [nzTitle]="'History and tasks · ' + employeeName()" (nzOnClose)="closed.emit()">
      <ng-container *nzDrawerContent>
        <nz-tabs>
          <nz-tab nzTitle="History">
            <div *erpHasPermission="'hr.employees.manage'" style="display:flex;gap:8px;margin-bottom:12px">
              <input nz-input aria-label="Add a note" placeholder="Add a note (course completed, warning given ...)" [(ngModel)]="note" maxlength="500" data-testid="history-note" />
              <button nz-button nzType="primary" [disabled]="!note.trim()" (click)="addNote()" data-testid="history-add">Add</button>
            </div>
            @for (h of history(); track h.id) {
              <div style="padding:8px 0;border-bottom:1px solid var(--erp-color-border,#e2e8f0)" data-testid="history-row">
                <nz-tag>{{ kind(h.kind) }}</nz-tag> <b>{{ h.summary }}</b>
                <div style="font-size:12px;color:var(--erp-color-text-secondary)">
                  {{ h.effectiveDate | erpDate }}
                  @if (h.oldValue || h.newValue) { · {{ h.oldValue || '—' }} → {{ h.newValue || '—' }} }
                  @if (h.isPay && !h.oldValue && !h.newValue) { · pay details hidden }
                </div>
              </div>
            } @empty { <p style="color:var(--erp-color-text-secondary)">Nothing recorded yet.</p> }
          </nz-tab>
          <nz-tab nzTitle="Tasks">
            <div *erpHasPermission="'hr.employees.manage'" style="display:flex;gap:8px;margin-bottom:12px">
              <label for="life-template" style="position:absolute;width:1px;height:1px;overflow:hidden;clip:rect(0,0,0,0)">Start a checklist</label>
              <nz-select nzId="life-template" style="flex:1" nzAllowClear nzPlaceHolder="Start a checklist" [(ngModel)]="templateId" data-testid="task-template">
                @for (t of templates(); track t.id) { <nz-option [nzValue]="t.id" [nzLabel]="t.name + ' (' + (t.kind === 'onboarding' ? 'Onboarding' : 'Offboarding') + ')'"></nz-option> }
              </nz-select>
              <button nz-button [disabled]="!templateId" (click)="apply()" data-testid="task-apply">Apply</button>
            </div>
            <div *erpHasPermission="'hr.employees.manage'" style="display:flex;gap:8px;margin-bottom:12px">
              <input nz-input aria-label="Task title" placeholder="Add a task" [(ngModel)]="taskTitle" maxlength="200" data-testid="task-title" />
              <label for="life-due" style="position:absolute;width:1px;height:1px;overflow:hidden;clip:rect(0,0,0,0)">Task due date</label>
              <nz-date-picker nzId="life-due" nzPlaceHolder="Due date" [(ngModel)]="taskDue" style="width:150px"></nz-date-picker>
              <button nz-button [disabled]="!taskTitle.trim()" (click)="addTask()" data-testid="task-add">Add</button>
            </div>
            @for (t of tasks(); track t.id) {
              <div style="display:flex;align-items:center;gap:8px;padding:6px 0;border-bottom:1px solid var(--erp-color-border,#e2e8f0)" data-testid="task-row">
                <input type="checkbox" [attr.aria-label]="'Done: ' + t.title" [checked]="t.isDone" (change)="toggle(t)" [disabled]="!canManage" />
                <span [style.text-decoration]="t.isDone ? 'line-through' : 'none'" style="flex:1">{{ t.title }}</span>
                @if (t.dueDate) { <nz-tag [nzColor]="t.overdue ? 'red' : 'default'">{{ t.dueDate | erpDate }}</nz-tag> }
              </div>
            } @empty { <p style="color:var(--erp-color-text-secondary)">No tasks.</p> }
          </nz-tab>
        </nz-tabs>
      </ng-container>
    </nz-drawer>`,
})
export class EmployeeLifeComponent {
  private readonly api = inject(EmployeeLifeApi);
  private readonly checklists = inject(ChecklistsApi);
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
      this.checklists.list({ page: 1, pageSize: 100, filters: [{ field: 'isActive', op: 'eq', value: true }] }).subscribe({ next: (p) => this.templates.set(p.items), error: () => this.templates.set([]) });
    });
  }

  kind(k: string): string { return KIND_LABELS[k] ?? k; }

  addNote(): void {
    this.api.addNote(this.employeeId(), this.note.trim()).subscribe({ next: () => { this.note = ''; this.loadHistory(); }, error: (e) => this.message.error(asProblem(e)?.detail ?? 'Could not add the note') });
  }

  apply(): void {
    this.api.applyChecklist(this.employeeId(), this.templateId!).subscribe({
      next: (created) => { this.message.success(created.length ? `${created.length} task(s) added` : 'Those tasks were already there'); this.loadTasks(); },
      error: (e) => this.message.error(asProblem(e)?.detail ?? 'Could not apply the checklist'),
    });
  }

  addTask(): void {
    const d = this.taskDue;
    const due = d ? `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}` : null;
    this.api.addTask(this.employeeId(), { title: this.taskTitle.trim(), dueDate: due, kind: 'onboarding' }).subscribe({
      next: () => { this.taskTitle = ''; this.taskDue = null; this.loadTasks(); },
      error: (e) => this.message.error(asProblem(e)?.detail ?? 'Could not add the task'),
    });
  }

  toggle(t: EmployeeTask): void {
    this.api.setDone(t.id, !t.isDone).subscribe({ next: () => this.loadTasks(), error: (e) => this.message.error(asProblem(e)?.detail ?? 'Could not update the task') });
  }

  private loadHistory(): void { this.api.history(this.employeeId()).subscribe((h) => this.history.set(h)); }
  private loadTasks(): void { this.api.tasks(this.employeeId()).subscribe((t) => this.tasks.set(t)); }
}
