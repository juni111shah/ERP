import { ChangeDetectionStrategy, Component, computed, inject, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { ProjectsExtraApi, UserOption } from '@core/api/projects-extra.api';
import { ProjectsApi, ProjectSummaryDto, ProjectTasksApi, ProjectTimesheetsApi, TaskDto, TimesheetDto, TimesheetInput } from '@core/api/projects.api';
import { toIsoDate } from '@core/format/dates';
import { WorkspaceCurrencyService } from '@core/services/workspace-currency.service';
import { SessionStore } from '@core/session/session.store';
import { revealErrors } from '@shared/forms/form-validation';
import { loadOrFallback } from '@shared/utils/load-or-fallback';
import { errorText } from '../../projects-ui';

/**
 * One way to log or edit time everywhere. The person defaults to whoever is signed in and the rate comes from the rate cards unless typed,
 * so cost and margin stay right. open() for new time, optionally in a project; edit(entry) to change one.
 */
@Component({
  selector: 'erp-log-time',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, NzCheckboxModule, NzDatePickerModule, NzFormModule, NzInputModule, NzInputNumberModule, NzModalModule, NzSelectModule],
  templateUrl: './log-time.component.html',
  styleUrl: './log-time.component.scss',
})
export class LogTimeComponent {
  private readonly fb = inject(FormBuilder);
  private readonly timesheets = inject(ProjectTimesheetsApi);
  private readonly projectsApi = inject(ProjectsApi);
  private readonly tasksApi = inject(ProjectTasksApi);
  private readonly extra = inject(ProjectsExtraApi);
  private readonly msg = inject(NzMessageService);
  private readonly session = inject(SessionStore);
  readonly currency = inject(WorkspaceCurrencyService).currency;

  /** Fires after a save. */
  readonly saved = output<void>();

  readonly visible = signal(false);
  readonly saving = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly fixedProject = signal(false);
  readonly projects = signal<ProjectSummaryDto[]>([]);
  readonly users = signal<UserOption[]>([]);
  readonly tasks = signal<TaskDto[]>([]);
  private readonly me = computed(() => this.session.me()?.user?.id ?? null);

  readonly form = this.fb.group({
    projectId: ['', Validators.required],
    taskId: [null as string | null],
    workerUserId: [null as string | null],
    workerName: [''],
    date: [new Date() as Date | null, Validators.required],
    hours: [1 as number | null, [Validators.required, Validators.min(0.01), Validators.max(24)]],
    hourlyRate: [null as number | null],
    description: [''],
    isBillable: [true],
  });

  open(projectId?: string | null, taskId?: string | null): void {
    this.loadPickers(!!projectId);
    this.editingId.set(null);
    this.fixedProject.set(!!projectId);
    this.form.reset({ projectId: projectId ?? '', taskId: taskId ?? null, workerUserId: this.me(), workerName: '', date: new Date(), hours: 1, hourlyRate: null, description: '', isBillable: true });
    this.loadTasks(projectId ?? null);
    this.visible.set(true);
  }

  edit(r: TimesheetDto): void {
    this.loadPickers(true);
    this.editingId.set(r.id);
    this.fixedProject.set(true);
    this.form.reset({ projectId: r.projectId, taskId: r.taskId ?? null, workerUserId: r.workerUserId ?? null, workerName: r.workerUserId ? '' : r.workerName, date: new Date(r.date + 'T00:00:00'), hours: r.hours, hourlyRate: r.hourlyRate, description: r.description ?? '', isBillable: r.isBillable });
    this.loadTasks(r.projectId);
    this.visible.set(true);
  }

  private loadPickers(projectKnown: boolean): void {
    if (!this.users().length) loadOrFallback(this.users, this.extra.users(), []);
    if (!projectKnown && !this.projects().length) loadOrFallback(this.projects, this.projectsApi.open(), [], (r) => r.items);
  }

  /** The tasks of the chosen project that can still take time. */
  loadTasks(projectId: string | null): void {
    if (!projectId) { this.tasks.set([]); return; }
    const tasks$ = this.tasksApi.list({ page: 1, pageSize: 200, sort: 'number', filters: [{ field: 'projectId', op: 'eq', value: projectId }] });
    loadOrFallback(this.tasks, tasks$, [], (r) => r.items.filter((t) => t.status !== 'cancelled'));
  }

  /** Saves the entry; returns false so the modal stays open until the save succeeds. */
  save(): boolean {
    if (this.form.invalid) { revealErrors(this.form); return false; }
    const v = this.form.getRawValue();
    if (!v.workerUserId && !(v.workerName ?? '').trim()) { this.msg.warning('Choose who did the work.'); return false; }
    const body: TimesheetInput = {
      projectId: v.projectId!, taskId: v.taskId || null, workerUserId: v.workerUserId || null, workerName: v.workerUserId ? null : (v.workerName ?? '').trim(),
      date: toIsoDate(v.date ?? new Date()), hours: v.hours!, hourlyRate: v.hourlyRate ?? null, description: v.description?.trim() || null, isBillable: v.isBillable ?? true,
    };
    const id = this.editingId();
    this.saving.set(true);
    (id ? this.timesheets.update(id, body) : this.timesheets.create(body)).subscribe({
      next: () => { this.saving.set(false); this.msg.success(`${body.hours} h logged`); this.visible.set(false); this.saved.emit(); },
      error: (e) => { this.saving.set(false); this.msg.error(errorText(e, 'Could not save the time.')); },
    });
    return false;
  }
}
