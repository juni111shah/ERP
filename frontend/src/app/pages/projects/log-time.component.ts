import { ChangeDetectionStrategy, Component, computed, inject, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { catchError, of } from 'rxjs';
import { ProjectsExtraApi, UserOption } from '../../core/api/projects-extra.api';
import { ProjectsApi, ProjectSummaryDto, ProjectTasksApi, ProjectTimesheetsApi, TaskDto, TimesheetDto } from '../../core/api/projects.api';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { SessionStore } from '../../core/session/session.store';
import { errorText } from './projects-ui';
import { ErpFormModule } from '../../core/forms/erp-form.module';
import { showFormErrors } from '../../core/forms/form-errors';

/**
 * One way to log or edit time everywhere. The person defaults to whoever is signed in and the rate comes from the rate cards unless typed,
 * so cost and margin stay right. open() for new time, optionally in a project; edit(entry) to change one.
 */
@Component({
  selector: 'erp-log-time',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, NzCheckboxModule, NzDatePickerModule, ErpFormModule, NzInputModule, NzInputNumberModule, NzModalModule, NzSelectModule],
  template: `
    <nz-modal [nzVisible]="visible()" [nzTitle]="editingId() ? 'Edit time' : 'Log time'" nzOkText="Save" [nzOkLoading]="saving()" (nzOnCancel)="visible.set(false)" (nzOnOk)="save()" [nzWidth]="560">
      <ng-container *nzModalContent>
        <form [formGroup]="form" nz-form nzLayout="vertical" data-testid="timesheet-form">
          @if (!fixedProject()) {
            <nz-form-item>
              <nz-form-label nzRequired nzFor="ts-project">Project</nz-form-label>
              <nz-form-control nzErrorTip="Choose a project">
                <nz-select nzId="ts-project" formControlName="projectId" nzShowSearch nzPlaceHolder="Select project" (ngModelChange)="loadTasks($event)" data-testid="ts-project">
                  @for (p of projects(); track p.id) { <nz-option [nzValue]="p.id" [nzLabel]="p.number + ' · ' + p.name"></nz-option> }
                </nz-select>
              </nz-form-control>
            </nz-form-item>
          }
          <nz-form-item>
            <nz-form-label nzFor="ts-task">Task</nz-form-label>
            <nz-form-control>
              <nz-select nzId="ts-task" formControlName="taskId" nzAllowClear nzShowSearch nzPlaceHolder="No particular task" data-testid="ts-task">
                @for (t of tasks(); track t.id) { <nz-option [nzValue]="t.id" [nzLabel]="t.number + ' · ' + t.title"></nz-option> }
              </nz-select>
            </nz-form-control>
          </nz-form-item>
          <div class="cols">
            <nz-form-item>
              <nz-form-label nzRequired nzFor="ts-worker">Who did the work</nz-form-label>
              <nz-form-control>
                <nz-select nzId="ts-worker" formControlName="workerUserId" nzAllowClear nzShowSearch nzPlaceHolder="Choose a person" data-testid="ts-worker">
                  @for (u of users(); track u.id) { <nz-option [nzValue]="u.id" [nzLabel]="u.fullName"></nz-option> }
                </nz-select>
              </nz-form-control>
            </nz-form-item>
            @if (!form.controls.workerUserId.value) {
              <nz-form-item>
                <nz-form-label nzFor="ts-name">Or a name</nz-form-label>
                <nz-form-control><input nz-input id="ts-name" formControlName="workerName" maxlength="100" placeholder="For someone without an account" /></nz-form-control>
              </nz-form-item>
            }
          </div>
          <div class="cols three">
            <nz-form-item>
              <nz-form-label nzRequired nzFor="ts-date">Date</nz-form-label>
              <nz-form-control><nz-date-picker nzId="ts-date" formControlName="date" style="width:100%"></nz-date-picker></nz-form-control>
            </nz-form-item>
            <nz-form-item>
              <nz-form-label nzRequired nzFor="ts-hours">Hours</nz-form-label>
              <nz-form-control nzErrorTip="Between 0.25 and 24"><nz-input-number nzId="ts-hours" formControlName="hours" [nzMin]="0.25" [nzMax]="24" [nzStep]="0.25" style="width:100%" data-testid="ts-hours"></nz-input-number></nz-form-control>
            </nz-form-item>
            <nz-form-item>
              <nz-form-label nzFor="ts-rate">Rate per hour ({{ currency() }})</nz-form-label>
              <nz-form-control nzExtra="Blank uses the rate cards"><nz-input-number nzId="ts-rate" formControlName="hourlyRate" [nzMin]="0" style="width:100%" data-testid="ts-rate"></nz-input-number></nz-form-control>
            </nz-form-item>
          </div>
          <nz-form-item>
            <nz-form-label nzFor="ts-desc">What was done</nz-form-label>
            <nz-form-control><input nz-input id="ts-desc" formControlName="description" maxlength="500" /></nz-form-control>
          </nz-form-item>
          <nz-form-item><label nz-checkbox formControlName="isBillable">Billable to the customer</label></nz-form-item>
        </form>
      </ng-container>
    </nz-modal>
  `,
  styles: ['.cols { display:grid; grid-template-columns:1fr 1fr; column-gap:12px; } .cols.three { grid-template-columns:1fr 1fr 1fr; } @media (max-width:560px) { .cols, .cols.three { grid-template-columns:1fr; } }'],
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
    if (!this.users().length) this.extra.users().pipe(catchError(() => of([] as UserOption[]))).subscribe((u) => this.users.set(u));
    if (!projectKnown && !this.projects().length) this.projectsApi.open().pipe(catchError(() => of({ items: [] as ProjectSummaryDto[] }))).subscribe((r) => this.projects.set(r.items));
  }

  loadTasks(projectId: string | null): void {
    if (!projectId) { this.tasks.set([]); return; }
    this.tasksApi.list({ page: 1, pageSize: 200, sort: 'number', filters: [{ field: 'projectId', op: 'eq', value: projectId }] }).pipe(catchError(() => of({ items: [] as TaskDto[] })))
      .subscribe((r) => this.tasks.set(r.items.filter((t) => t.status !== 'cancelled')));
  }

  save(): boolean {
    const v = this.form.getRawValue();
    if (this.form.invalid) { showFormErrors(this.form); return false; }
    if (!v.workerUserId && !(v.workerName ?? '').trim()) { this.msg.warning('Choose who did the work.'); return false; }
    const body = {
      projectId: v.projectId!, taskId: v.taskId || null, workerUserId: v.workerUserId || null, workerName: v.workerUserId ? null : (v.workerName ?? '').trim(),
      date: toIsoDate(v.date ?? new Date()), hours: v.hours!, hourlyRate: v.hourlyRate ?? null, description: v.description?.trim() || null, isBillable: v.isBillable ?? true,
    };
    const id = this.editingId();
    this.saving.set(true);
    (id ? this.timesheets.update(id, body) : this.timesheets.create(body)).subscribe({
      next: () => { this.saving.set(false); this.msg.success('Time saved'); this.visible.set(false); this.saved.emit(); },
      error: (e) => { this.saving.set(false); this.msg.error(errorText(e, 'Could not save the time.')); },
    });
    return false;
  }
}
