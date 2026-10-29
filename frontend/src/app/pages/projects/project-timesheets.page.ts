import { CommonModule, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
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
import { ProjectTasksApi, ProjectTimesheetsApi, ProjectsApi, TimesheetDto } from '../../core/api/projects.api';
import { WorkspaceCurrencyService } from '../../core/services/workspace-currency.service';
import { HasPermissionDirective } from '../../shared/has-permission.directive';
import { PagedTableComponent, TableColumn } from '../../shared/paged-table.component';

@Component({
  selector: 'erp-project-timesheets',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    DecimalPipe,
    RouterModule,
    ReactiveFormsModule,
    NzGridModule,
    NzCardModule,
    NzTagModule,
    NzButtonModule,
    NzIconModule,
    NzSelectModule,
    NzModalModule,
    NzFormModule,
    NzInputModule,
    NzInputNumberModule,
    NzDatePickerModule,
    NzCheckboxModule,
    PagedTableComponent,
    HasPermissionDirective,
  ],
  template: `
    <erp-paged-table [list]="list" [columns]="columns" searchPlaceholder="Search worker name or description..." emptyText="No timesheets logged yet.">
      <button toolbar *erpHasPermission="'projects.timesheets.manage'" nz-button nzType="primary" (click)="openCreateModal()" data-testid="timesheet-new">
        <span nz-icon nzType="clock-circle"></span> Log Time
      </button>
        <ng-template #cell let-row let-column="column" let-value="value">
          @switch (column.key) {
            @case ('date') {
              <span class="date-cell">{{ row.date }}</span>
            }
            @case ('workerName') {
              <span class="worker-name">{{ row.workerName }}</span>
            }
            @case ('projectName') {
              <a [routerLink]="['/projects', row.projectId]" class="project-link">{{ row.projectName || 'View Project' }}</a>
            }
            @case ('hours') {
              <span class="hours-val">{{ row.hours | number: '1.2-2' }}h</span>
            }
            @case ('hourlyRate') {
              <span>{{ currency() }} {{ row.hourlyRate | number: '1.2-2' }}</span>
            }
            @case ('totalAmount') {
              <span class="amount-val">{{ currency() }} {{ row.totalAmount | number: '1.2-2' }}</span>
            }
            @case ('status') {
              @if (!row.isBillable) {
                <nz-tag nzColor="default">Non-Billable</nz-tag>
              } @else if (row.isBilled) {
                <nz-tag nzColor="green">Billed</nz-tag>
              } @else {
                <nz-tag nzColor="orange">Unbilled</nz-tag>
              }
            }
            @default {
              {{ value }}
            }
          }
        </ng-template>
      </erp-paged-table>

      <!-- Timesheet Modal -->
      <nz-modal [(nzVisible)]="modalVisible" nzTitle="Log Project Time" (nzOnCancel)="modalVisible = false" (nzOnOk)="save()">
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
            <div nz-row [nzGutter]="12">
              <div nz-col [nzSpan]="12">
                <nz-form-item>
                  <nz-form-label nzRequired>Worker Name</nz-form-label>
                  <nz-form-control><input nz-input formControlName="workerName" placeholder="Full name" /></nz-form-control>
                </nz-form-item>
              </div>
              <div nz-col [nzSpan]="12">
                <nz-form-item>
                  <nz-form-label nzRequired>Date</nz-form-label>
                  <nz-form-control><nz-date-picker formControlName="date" style="width:100%"></nz-date-picker></nz-form-control>
                </nz-form-item>
              </div>
            </div>
            <div nz-row [nzGutter]="12">
              <div nz-col [nzSpan]="12">
                <nz-form-item>
                  <nz-form-label nzRequired>Hours Spent</nz-form-label>
                  <nz-form-control><nz-input-number formControlName="hours" [nzMin]="0.1" [nzMax]="24" [nzStep]="0.5" style="width:100%"></nz-input-number></nz-form-control>
                </nz-form-item>
              </div>
              <div nz-col [nzSpan]="12">
                <nz-form-item>
                  <nz-form-label nzRequired>Hourly Rate (AED)</nz-form-label>
                  <nz-form-control><nz-input-number formControlName="hourlyRate" [nzMin]="0" style="width:100%"></nz-input-number></nz-form-control>
                </nz-form-item>
              </div>
            </div>
            <nz-form-item>
              <nz-form-label>Work Description</nz-form-label>
              <nz-form-control><input nz-input formControlName="description" placeholder="Summary of work performed" /></nz-form-control>
            </nz-form-item>
            <nz-form-item>
              <label nz-checkbox formControlName="isBillable">Billable to client</label>
            </nz-form-item>
          </form>
        </ng-container>
      </nz-modal>
  `,
  styles: [`
    .worker-name {
      font-weight: 600;
      color: #0f172a;
    }
    .project-link {
      font-weight: 600;
      color: #10955d;
      text-decoration: none;
    }
    .project-link:hover {
      text-decoration: underline;
    }
    .hours-val {
      font-weight: 600;
      color: #0f172a;
    }
    .amount-val {
      font-weight: 700;
      color: #10955d;
    }
    .date-cell {
      font-size: 12.5px;
      color: #64748b;
    }
  `],
})
export class ProjectTimesheetsPage {
  private readonly timesheetsApi = inject(ProjectTimesheetsApi);
  private readonly projectsApi = inject(ProjectsApi);
  private readonly fb = inject(FormBuilder);
  private readonly msg = inject(NzMessageService);
  private readonly currencyService = inject(WorkspaceCurrencyService);

  readonly currency = this.currencyService.currency;

  readonly projects = toSignal(this.projectsApi.list({ page: 1, pageSize: 200 }).pipe(catchError(() => of({ items: [] }))), { initialValue: { items: [] } });
  readonly list = createPagedList((q) => this.timesheetsApi.list(q), { sort: 'date' });

  modalVisible = false;
  readonly form = this.fb.group({
    projectId: ['', Validators.required],
    workerName: ['', Validators.required],
    date: [new Date(), Validators.required],
    hours: [1, [Validators.required, Validators.min(0.1), Validators.max(24)]],
    hourlyRate: [150, [Validators.required, Validators.min(0)]],
    description: [''],
    isBillable: [true],
  });

  readonly columns: TableColumn<TimesheetDto>[] = [
    { key: 'date', label: 'Date', sortField: 'date', width: '130px' },
    { key: 'workerName', label: 'Worker', sortField: 'workerName' },
    { key: 'projectName', label: 'Project', width: '180px' },
    { key: 'description', label: 'Description' },
    { key: 'hours', label: 'Hours', sortField: 'hours', width: '110px' },
    { key: 'hourlyRate', label: 'Rate', width: '130px' },
    { key: 'totalAmount', label: 'Total Amount', sortField: 'totalAmount', width: '150px' },
    { key: 'status', label: 'Status', width: '130px' },
  ];

  openCreateModal(): void {
    this.form.reset({
      date: new Date(),
      hours: 1,
      hourlyRate: 150,
      isBillable: true,
    });
    this.modalVisible = true;
  }

  save(): void {
    if (this.form.invalid) return;
    const v = this.form.value;
    const dateStr = v.date ? new Date(v.date).toISOString().split('T')[0] : new Date().toISOString().split('T')[0];

    this.timesheetsApi.create({
      projectId: v.projectId!,
      workerName: v.workerName!,
      date: dateStr,
      hours: v.hours!,
      hourlyRate: v.hourlyRate!,
      description: v.description || null,
      isBillable: v.isBillable ?? true,
    }).subscribe(() => {
      this.msg.success('Timesheet logged');
      this.modalVisible = false;
      this.list.reload();
    });
  }
}
