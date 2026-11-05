import { CommonModule, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { catchError, of } from 'rxjs';
import { createPagedList } from '../../core/api/paged-list';
import { ProjectTasksApi, ProjectTimesheetsApi, ProjectsApi, TaskDto, TimesheetDto } from '../../core/api/projects.api';
import { ProjectsExtraApi, UserOption } from '../../core/api/projects-extra.api';
import { toIsoDate } from '../../core/custom-fields/custom-field-form';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';

const APPROVAL_COLORS: Record<string, string> = { Draft: 'default', Submitted: 'blue', Approved: 'green', Rejected: 'red' };

/** Time entries: log, edit, submit for approval; the worker is a person of the workspace (or a typed name) and the rate comes from the rate cards unless typed. */
@Component({
  selector: 'erp-project-timesheets',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, DecimalPipe, RouterModule, ReactiveFormsModule, NzGridModule, NzTagModule, NzButtonModule, NzIconModule, NzSelectModule, NzModalModule, NzFormModule, NzInputModule, NzInputNumberModule, NzDatePickerModule, NzCheckboxModule, PagedTableComponent, HasPermissionDirective],
  template: `
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search worker name or description..." emptyText="No timesheets logged yet.">
      <button toolbar *erpHasPermission="'projects.timesheets.manage'" nz-button nzType="primary" (click)="openCreateModal()" data-testid="timesheet-new">
        <span nz-icon nzType="clock-circle"></span> Log Time
      </button>
      <a toolbar nz-button routerLink="/projects/week" data-testid="timesheet-week"><span nz-icon nzType="calendar"></span> Week view</a>
      <button toolbar *erpHasPermission="'projects.timesheets.manage'" nz-button (click)="submitDrafts()" data-testid="timesheet-submit-drafts"><span nz-icon nzType="send"></span> Submit my drafts</button>
      <ng-template #cell let-row let-column="column" let-value="value">
        @switch (column.key) {
          @case ('date') { <span class="date-cell">{{ row.date }}</span> }
          @case ('workerName') { <span class="worker-name">{{ row.workerName }}</span> }
          @case ('projectName') { <a [routerLink]="['/projects', row.projectId]" class="project-link">{{ row.projectName || 'View Project' }}</a> }
          @case ('hours') { <span class="hours-val">{{ row.hours | number: '1.2-2' }}h</span> }
          @case ('hourlyRate') { <span>{{ currency() }} {{ row.hourlyRate | number: '1.2-2' }}</span> }
          @case ('totalAmount') { <span class="amount-val">{{ currency() }} {{ row.totalAmount | number: '1.2-2' }}</span> }
          @case ('approval') {
            <nz-tag [nzColor]="color(row.approvalStatus)" [title]="row.rejectReason || ''">{{ row.approvalStatus }}</nz-tag>
          }
          @case ('status') {
            @if (row.writtenOff) { <nz-tag nzColor="default">Written off</nz-tag> }
            @else if (!row.isBillable) { <nz-tag nzColor="default">Non-Billable</nz-tag> }
            @else if (row.isBilled) { <nz-tag nzColor="green">Billed</nz-tag> }
            @else { <nz-tag nzColor="orange">Unbilled</nz-tag> }
          }
          @case ('actions') {
            @if (!row.isBilled && !row.writtenOff) {
              <span *erpHasPermission="'projects.timesheets.manage'">
                <button nz-button nzSize="small" (click)="openEditModal(row)" [attr.data-testid]="'timesheet-edit-' + row.id">Edit</button>
                @if (row.approvalStatus === 'Draft' || row.approvalStatus === 'Rejected') { <button nz-button nzSize="small" (click)="submitOne(row)">Submit</button> }
                <button nz-button nzSize="small" nzDanger (click)="remove(row)">Delete</button>
              </span>
            }
          }
          @default { {{ value }} }
        }
      </ng-template>
    </erp-paged-table>

    <nz-modal [(nzVisible)]="modalVisible" [nzTitle]="editingId ? 'Edit time' : 'Log Project Time'" (nzOnCancel)="modalVisible = false" (nzOnOk)="save()" nzOkText="Save">
      <ng-container *nzModalContent>
        <form [formGroup]="form" nz-form nzLayout="vertical" data-testid="timesheet-form">
          <nz-form-item>
            <nz-form-label nzRequired>Project</nz-form-label>
            <nz-form-control>
              <nz-select formControlName="projectId" nzShowSearch nzPlaceHolder="Select project" (ngModelChange)="loadTasks($event)" data-testid="ts-project">
                @for (p of projects().items; track p.id) { <nz-option [nzValue]="p.id" [nzLabel]="p.number + ' · ' + p.name"></nz-option> }
              </nz-select>
            </nz-form-control>
          </nz-form-item>
          <nz-form-item>
            <nz-form-label>Task</nz-form-label>
            <nz-form-control>
              <nz-select formControlName="taskId" nzAllowClear nzShowSearch nzPlaceHolder="No task">
                @for (t of tasks(); track t.id) { <nz-option [nzValue]="t.id" [nzLabel]="t.number + ' · ' + t.title"></nz-option> }
              </nz-select>
            </nz-form-control>
          </nz-form-item>
          <div nz-row [nzGutter]="12">
            <div nz-col [nzSpan]="12">
              <nz-form-item>
                <nz-form-label nzRequired>Who did the work</nz-form-label>
                <nz-form-control>
                  <nz-select formControlName="workerUserId" nzAllowClear nzShowSearch nzPlaceHolder="Choose a person" data-testid="ts-worker">
                    @for (u of users(); track u.id) { <nz-option [nzValue]="u.id" [nzLabel]="u.fullName"></nz-option> }
                  </nz-select>
                </nz-form-control>
              </nz-form-item>
            </div>
            <div nz-col [nzSpan]="12">
              <nz-form-item>
                <nz-form-label>or a name</nz-form-label>
                <nz-form-control><input nz-input formControlName="workerName" placeholder="For someone without an account" aria-label="Worker name" /></nz-form-control>
              </nz-form-item>
            </div>
          </div>
          <div nz-row [nzGutter]="12">
            <div nz-col [nzSpan]="8">
              <nz-form-item>
                <nz-form-label nzRequired>Date</nz-form-label>
                <nz-form-control><nz-date-picker formControlName="date" style="width:100%"></nz-date-picker></nz-form-control>
              </nz-form-item>
            </div>
            <div nz-col [nzSpan]="8">
              <nz-form-item>
                <nz-form-label nzRequired>Hours</nz-form-label>
                <nz-form-control><nz-input-number formControlName="hours" [nzMin]="0.1" [nzMax]="24" [nzStep]="0.25" style="width:100%" data-testid="ts-hours"></nz-input-number></nz-form-control>
              </nz-form-item>
            </div>
            <div nz-col [nzSpan]="8">
              <nz-form-item>
                <nz-form-label>Rate per hour ({{ currency() }})</nz-form-label>
                <nz-form-control nzExtra="Blank = from the rate cards"><nz-input-number formControlName="hourlyRate" [nzMin]="0" style="width:100%"></nz-input-number></nz-form-control>
              </nz-form-item>
            </div>
          </div>
          <nz-form-item>
            <nz-form-label>Work description</nz-form-label>
            <nz-form-control><input nz-input formControlName="description" placeholder="Summary of work performed" aria-label="Work description" /></nz-form-control>
          </nz-form-item>
          <nz-form-item><label nz-checkbox formControlName="isBillable">Billable to client</label></nz-form-item>
        </form>
      </ng-container>
    </nz-modal>
  `,
  styles: [`
    .worker-name, .hours-val { font-weight: 600; color: #0f172a; }
    .project-link { font-weight: 600; color: #10955d; text-decoration: none; }
    .project-link:hover { text-decoration: underline; }
    .amount-val { font-weight: 700; color: #10955d; }
    .date-cell { font-size: 12.5px; color: #64748b; }
  `],
})
export class ProjectTimesheetsPage {
  private readonly timesheetsApi = inject(ProjectTimesheetsApi);
  private readonly projectsApi = inject(ProjectsApi);
  private readonly tasksApi = inject(ProjectTasksApi);
  private readonly extra = inject(ProjectsExtraApi);
  private readonly fb = inject(FormBuilder);
  private readonly msg = inject(NzMessageService);
  readonly currency = inject(WorkspaceCurrencyService).currency;

  readonly projects = toSignal(this.projectsApi.list({ page: 1, pageSize: 200 }).pipe(catchError(() => of({ items: [] }))), { initialValue: { items: [] } });
  readonly users = toSignal(this.extra.users().pipe(catchError(() => of([] as UserOption[]))), { initialValue: [] as UserOption[] });
  readonly tasks = signal<TaskDto[]>([]);
  readonly list = createPagedList((q) => this.timesheetsApi.list(q), { sort: '-date' });
  private readonly rows = computed(() => this.list.items());

  modalVisible = false;
  editingId: string | null = null;
  readonly form = this.fb.group({
    projectId: ['', Validators.required],
    taskId: [null as string | null],
    workerUserId: [null as string | null],
    workerName: [''],
    date: [new Date(), Validators.required],
    hours: [1, [Validators.required, Validators.min(0.1), Validators.max(24)]],
    hourlyRate: [null as number | null],
    description: [''],
    isBillable: [true],
  });

  readonly columns: TableColumn<TimesheetDto>[] = [
    { key: 'date', label: 'Date', sortField: 'date', width: '120px' },
    { key: 'workerName', label: 'Worker', sortField: 'workerName' },
    { key: 'projectName', label: 'Project', width: '170px' },
    { key: 'taskTitle', label: 'Task', width: '150px' },
    { key: 'description', label: 'Description' },
    { key: 'hours', label: 'Hours', sortField: 'hours', width: '90px' },
    { key: 'hourlyRate', label: 'Rate', width: '110px' },
    { key: 'totalAmount', label: 'Total', sortField: 'totalAmount', width: '120px' },
    { key: 'approval', label: 'Approval', width: '110px', filter: false },
    { key: 'status', label: 'Billing', width: '110px', filter: false },
    { key: 'actions', label: '', width: '230px' },
  ];

  color(status: string): string { return APPROVAL_COLORS[status] ?? 'default'; }

  loadTasks(projectId: string | null): void {
    if (!projectId) { this.tasks.set([]); return; }
    this.tasksApi.list({ page: 1, pageSize: 200, filters: [{ field: 'projectId', op: 'eq', value: projectId }] }).pipe(catchError(() => of({ items: [] as TaskDto[] }))).subscribe((r) => this.tasks.set(r.items));
  }

  openCreateModal(): void {
    this.editingId = null;
    this.form.reset({ date: new Date(), hours: 1, hourlyRate: null, isBillable: true, taskId: null, workerUserId: null, workerName: '', description: '' });
    this.tasks.set([]);
    this.modalVisible = true;
  }

  openEditModal(r: TimesheetDto): void {
    this.editingId = r.id;
    this.form.reset({ projectId: r.projectId, taskId: r.taskId ?? null, workerUserId: r.workerUserId ?? null, workerName: r.workerUserId ? '' : r.workerName, date: new Date(r.date + 'T00:00:00'), hours: r.hours, hourlyRate: r.hourlyRate, description: r.description ?? '', isBillable: r.isBillable });
    this.loadTasks(r.projectId);
    this.modalVisible = true;
  }

  save(): void {
    const v = this.form.value;
    if (this.form.invalid || (!v.workerUserId && !(v.workerName ?? '').trim())) { this.msg.warning('Choose who did the work.'); return; }
    const body = {
      projectId: v.projectId!, taskId: v.taskId || null, workerUserId: v.workerUserId || null, workerName: v.workerUserId ? null : (v.workerName ?? '').trim(),
      date: toIsoDate(v.date ? new Date(v.date) : new Date()), hours: v.hours!, hourlyRate: v.hourlyRate ?? null, description: v.description || null, isBillable: v.isBillable ?? true,
    };
    const call = this.editingId ? this.timesheetsApi.update(this.editingId, body) : this.timesheetsApi.create(body);
    call.subscribe({ next: () => { this.msg.success('Timesheet saved'); this.modalVisible = false; this.list.reload(); }, error: (e) => this.msg.error(e?.error?.detail ?? e?.error?.message ?? 'Could not save the time.') });
  }

  submitOne(r: TimesheetDto): void { this.submit([r.id]); }
  submitDrafts(): void {
    const ids = this.rows().filter((r) => r.approvalStatus === 'Draft' || r.approvalStatus === 'Rejected').map((r) => r.id);
    if (!ids.length) { this.msg.info('There is nothing to submit on this page.'); return; }
    this.submit(ids);
  }
  private submit(ids: string[]): void {
    this.extra.submitTime(ids).subscribe({ next: (r) => { this.msg.success(`${r.submitted} entr${r.submitted === 1 ? 'y' : 'ies'} submitted for approval`); this.list.reload(); }, error: (e) => this.msg.error(e?.error?.detail ?? 'Could not submit.') });
  }
  remove(r: TimesheetDto): void {
    this.timesheetsApi.delete(r.id).subscribe({ next: () => { this.msg.success('Entry deleted'); this.list.reload(); }, error: (e) => this.msg.error(e?.error?.detail ?? 'Could not delete the entry.') });
  }
}
