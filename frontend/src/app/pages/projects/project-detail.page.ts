import { CommonModule, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { NzBadgeModule } from 'ng-zorro-antd/badge';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzDatePickerModule } from 'ng-zorro-antd/date-picker';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzToolTipModule } from 'ng-zorro-antd/tooltip';
import {
  MilestoneDto,
  MilestoneStatus,
  ProjectBillingApi,
  ProjectDto,
  ProjectMilestonesApi,
  ProjectsApi,
  ProjectStatus,
  ProjectTasksApi,
  ProjectTaskStatus,
  ProjectTimesheetsApi,
  TaskDto,
  TaskPriority,
  TimesheetDto,
} from '../../core/api/projects.api';
import { HasPermissionDirective } from '../../shared/has-permission.directive';

@Component({
  selector: 'erp-project-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    DecimalPipe,
    RouterModule,
    FormsModule,
    ReactiveFormsModule,
    NzGridModule,
    NzCardModule,
    NzTagModule,
    NzButtonModule,
    NzIconModule,
    NzToolTipModule,
    NzTabsModule,
    NzTableModule,
    NzProgressModule,
    NzBadgeModule,
    NzDrawerModule,
    NzModalModule,
    NzFormModule,
    NzInputModule,
    NzInputNumberModule,
    NzSelectModule,
    NzDatePickerModule,
    NzCheckboxModule,
    HasPermissionDirective,
  ],
  template: `
    @if (p(); as proj) {
      <div class="project-detail-container">
        <!-- Top Breadcrumbs & Actions -->
        <div class="detail-header">
          <div class="title-section">
            <div class="breadcrumb-row">
              <a routerLink="/projects/list" class="back-link"><span nz-icon nzType="arrow-left"></span> Back to Projects</a>
            </div>
            <div class="heading-row">
              <span class="code-pill">{{ proj.number }}</span>
              <h1 class="project-title">{{ proj.name }}</h1>
              <nz-tag [nzColor]="statusColor(proj.status)" class="status-badge">{{ proj.status }}</nz-tag>
            </div>
            <div class="meta-row">
              @if (proj.customerName) {
                <span class="meta-item"><span nz-icon nzType="user"></span> {{ proj.customerName }}</span>
              }
              @if (proj.managerName) {
                <span class="meta-item"><span nz-icon nzType="team"></span> PM: {{ proj.managerName }}</span>
              }
              <span class="meta-item"><span nz-icon nzType="calendar"></span> {{ proj.startDate || '—' }} to {{ proj.endDate || '—' }}</span>
              <span class="meta-item"><span nz-icon nzType="tag"></span> {{ formatBillingType(proj.billingType) }}</span>
            </div>
          </div>

          <div class="header-btns">
            <button *erpHasPermission="'projects.tasks.manage'" nz-button (click)="openTaskModal()">
              <span nz-icon nzType="plus"></span> Add Task
            </button>
            <button *erpHasPermission="'projects.milestones.manage'" nz-button (click)="openMilestoneModal()">
              <span nz-icon nzType="flag"></span> Add Milestone
            </button>
            <button *erpHasPermission="'projects.timesheets.manage'" nz-button nzType="primary" (click)="openTimesheetModal()">
              <span nz-icon nzType="clock-circle"></span> Log Time
            </button>
          </div>
        </div>

        <!-- Stat KPI Cards -->
        <div nz-row [nzGutter]="[16, 16]" class="kpi-row">
          <div nz-col [nzXs]="24" [nzSm]="12" [nzMd]="6">
            <nz-card class="stat-card">
              <div class="stat-header">
                <span class="stat-label">Budget Consumption</span>
                <span nz-icon nzType="dollar" class="stat-icon icon-budget"></span>
              </div>
              <div class="stat-value">AED {{ proj.totalTimesheetCost | number: '1.0-0' }}</div>
              <div class="stat-sub">of AED {{ proj.budgetAmount | number: '1.0-0' }} budget</div>
              <nz-progress [nzPercent]="calcPercent(proj.totalTimesheetCost, proj.budgetAmount)" [nzStatus]="proj.totalTimesheetCost > proj.budgetAmount && proj.budgetAmount > 0 ? 'exception' : 'active'"></nz-progress>
            </nz-card>
          </div>
          <div nz-col [nzXs]="24" [nzSm]="12" [nzMd]="6">
            <nz-card class="stat-card">
              <div class="stat-header">
                <span class="stat-label">Tasks Progress</span>
                <span nz-icon nzType="check-square" class="stat-icon icon-tasks"></span>
              </div>
              <div class="stat-value">{{ proj.completedTasks }} / {{ proj.totalTasks }}</div>
              <div class="stat-sub">tasks completed</div>
              <nz-progress [nzPercent]="calcPercent(proj.completedTasks, proj.totalTasks)" nzStrokeColor="#10955d"></nz-progress>
            </nz-card>
          </div>
          <div nz-col [nzXs]="24" [nzSm]="12" [nzMd]="6">
            <nz-card class="stat-card">
              <div class="stat-header">
                <span class="stat-label">Milestones</span>
                <span nz-icon nzType="flag" class="stat-icon icon-milestones"></span>
              </div>
              <div class="stat-value">{{ proj.achievedMilestones }} / {{ proj.totalMilestones }}</div>
              <div class="stat-sub">milestones reached</div>
              <nz-progress [nzPercent]="calcPercent(proj.achievedMilestones, proj.totalMilestones)" nzStrokeColor="#2563eb"></nz-progress>
            </nz-card>
          </div>
          <div nz-col [nzXs]="24" [nzSm]="12" [nzMd]="6">
            <nz-card class="stat-card">
              <div class="stat-header">
                <span class="stat-label">Logged Hours</span>
                <span nz-icon nzType="clock-circle" class="stat-icon icon-hours"></span>
              </div>
              <div class="stat-value">{{ proj.totalLoggedHours | number: '1.1-1' }}h</div>
              <div class="stat-sub">{{ proj.totalBilledHours | number: '1.1-1' }}h billed · {{ (proj.totalLoggedHours - proj.totalBilledHours) | number: '1.1-1' }}h unbilled</div>
              <nz-progress [nzPercent]="calcPercent(proj.totalBilledHours, proj.totalLoggedHours)" nzStrokeColor="#7c3aed"></nz-progress>
            </nz-card>
          </div>
        </div>

        <!-- Main Tabs Section -->
        <nz-card class="main-card">
          <nz-tabset [nzSelectedIndex]="activeTabIndex()" (nzSelectedIndexChange)="activeTabIndex.set($event)">
            <!-- TAB 1: OVERVIEW -->
            <nz-tab nzTitle="Overview">
              <div class="overview-grid">
                <div class="info-block">
                  <h3>Project Scope & Details</h3>
                  <p class="description-text">{{ proj.description || 'No project description provided yet.' }}</p>
                  <div class="props-list">
                    <div class="prop-row"><span class="prop-label">Project Code:</span> <span class="prop-val">{{ proj.number }}</span></div>
                    <div class="prop-row"><span class="prop-label">Client Name:</span> <span class="prop-val">{{ proj.customerName || 'None' }}</span></div>
                    <div class="prop-row"><span class="prop-label">Project Manager:</span> <span class="prop-val">{{ proj.managerName || 'Unassigned' }}</span></div>
                    <div class="prop-row"><span class="prop-label">Billing Model:</span> <span class="prop-val">{{ formatBillingType(proj.billingType) }}</span></div>
                    <div class="prop-row"><span class="prop-label">Total Allocated Budget:</span> <span class="prop-val">AED {{ proj.budgetAmount | number: '1.2-2' }}</span></div>
                    <div class="prop-row"><span class="prop-label">Start Date:</span> <span class="prop-val">{{ proj.startDate || '—' }}</span></div>
                    <div class="prop-row"><span class="prop-label">Target Completion:</span> <span class="prop-val">{{ proj.endDate || '—' }}</span></div>
                  </div>
                </div>
              </div>
            </nz-tab>

            <!-- TAB 2: TASKS -->
            <nz-tab [nzTitle]="'Tasks (' + tasks().length + ')'">
              <div class="tab-toolbar">
                <span class="tab-heading">Work Breakdown & Tasks</span>
                <button *erpHasPermission="'projects.tasks.manage'" nz-button nzType="primary" nzSize="small" (click)="openTaskModal()">
                  <span nz-icon nzType="plus"></span> Add Task
                </button>
              </div>
              <nz-table #taskTable [nzData]="tasks()" [nzShowPagination]="false" nzSize="middle">
                <thead>
                  <tr>
                    <th>Task Code</th>
                    <th>Title</th>
                    <th>Priority</th>
                    <th>Status</th>
                    <th>Assigned To</th>
                    <th>Est. / Actual Hours</th>
                    <th>Due Date</th>
                    <th>Action</th>
                  </tr>
                </thead>
                <tbody>
                  @for (t of taskTable.data; track t.id) {
                    <tr>
                      <td><span class="code-pill">{{ t.number }}</span></td>
                      <td>
                        <span class="item-title">{{ t.title }}</span>
                        @if (t.milestoneTitle) {
                          <span class="milestone-subtag"><span nz-icon nzType="flag"></span> {{ t.milestoneTitle }}</span>
                        }
                      </td>
                      <td><nz-tag [nzColor]="priorityColor(t.priority)">{{ t.priority }}</nz-tag></td>
                      <td><nz-tag [nzColor]="taskStatusColor(t.status)">{{ t.status }}</nz-tag></td>
                      <td>{{ t.assignedTo || '—' }}</td>
                      <td>{{ t.actualHours }}h / {{ t.estimatedHours }}h</td>
                      <td>{{ t.dueDate || '—' }}</td>
                      <td>
                        <nz-select [ngModel]="t.status" (ngModelChange)="updateTaskStatus(t.id, $event)" nzSize="small" style="width: 120px;">
                          <nz-option nzValue="Todo" nzLabel="To Do"></nz-option>
                          <nz-option nzValue="InProgress" nzLabel="In Progress"></nz-option>
                          <nz-option nzValue="Review" nzLabel="Review"></nz-option>
                          <nz-option nzValue="Done" nzLabel="Done"></nz-option>
                        </nz-select>
                      </td>
                    </tr>
                  }
                </tbody>
              </nz-table>
            </nz-tab>

            <!-- TAB 3: MILESTONES -->
            <nz-tab [nzTitle]="'Milestones (' + milestones().length + ')'">
              <div class="tab-toolbar">
                <span class="tab-heading">Delivery Milestones & Fixed Billing</span>
                <button *erpHasPermission="'projects.milestones.manage'" nz-button nzType="primary" nzSize="small" (click)="openMilestoneModal()">
                  <span nz-icon nzType="plus"></span> Add Milestone
                </button>
              </div>
              <nz-table #mTable [nzData]="milestones()" [nzShowPagination]="false" nzSize="middle">
                <thead>
                  <tr>
                    <th>Milestone</th>
                    <th>Due Date</th>
                    <th>Billing Amount</th>
                    <th>Status</th>
                    <th>Achieved Date</th>
                    <th>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  @for (m of mTable.data; track m.id) {
                    <tr>
                      <td><span class="item-title">{{ m.title }}</span></td>
                      <td>{{ m.dueDate }}</td>
                      <td><b>AED {{ m.amount | number: '1.2-2' }}</b></td>
                      <td><nz-tag [nzColor]="milestoneStatusColor(m.status)">{{ m.status }}</nz-tag></td>
                      <td>{{ m.achievedDate || '—' }}</td>
                      <td>
                        @if (m.status === 'Pending') {
                          <button *erpHasPermission="'projects.milestones.manage'" nz-button nzSize="small" nzType="default" (click)="achieveMilestone(m.id)">
                            <span nz-icon nzType="check"></span> Mark Achieved
                          </button>
                        } @else if (m.status === 'Achieved') {
                          <span class="status-info-text">Ready to bill</span>
                        } @else {
                          <nz-tag nzColor="green">Billed</nz-tag>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </nz-table>
            </nz-tab>

            <!-- TAB 4: TIMESHEETS -->
            <nz-tab [nzTitle]="'Timesheets (' + timesheets().length + ')'">
              <div class="tab-toolbar">
                <span class="tab-heading">Logged Hours & Work Logs</span>
                <button *erpHasPermission="'projects.timesheets.manage'" nz-button nzType="primary" nzSize="small" (click)="openTimesheetModal()">
                  <span nz-icon nzType="plus"></span> Log Time
                </button>
              </div>
              <nz-table #tsTable [nzData]="timesheets()" [nzShowPagination]="false" nzSize="middle">
                <thead>
                  <tr>
                    <th>Date</th>
                    <th>Worker</th>
                    <th>Task</th>
                    <th>Description</th>
                    <th>Hours</th>
                    <th>Hourly Rate</th>
                    <th>Total Cost</th>
                    <th>Billing Status</th>
                  </tr>
                </thead>
                <tbody>
                  @for (ts of tsTable.data; track ts.id) {
                    <tr>
                      <td>{{ ts.date }}</td>
                      <td><b>{{ ts.workerName }}</b></td>
                      <td>{{ ts.taskTitle || '—' }}</td>
                      <td>{{ ts.description || '—' }}</td>
                      <td>{{ ts.hours | number: '1.2-2' }}h</td>
                      <td>AED {{ ts.hourlyRate | number: '1.2-2' }}</td>
                      <td><b>AED {{ ts.totalAmount | number: '1.2-2' }}</b></td>
                      <td>
                        @if (!ts.isBillable) {
                          <nz-tag nzColor="default">Non-Billable</nz-tag>
                        } @else if (ts.isBilled) {
                          <nz-tag nzColor="green">Billed</nz-tag>
                        } @else {
                          <nz-tag nzColor="orange">Unbilled</nz-tag>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </nz-table>
            </nz-tab>

            <!-- TAB 5: BILLING -->
            <nz-tab nzTitle="Billing">
              <div class="billing-summary-box">
                <div class="billing-header">
                  <div>
                    <h3 style="margin:0 0 4px 0;">Project Billing Worksheet</h3>
                    <p class="tab-subtitle">Review unbilled time entries and completed milestones for client invoicing.</p>
                  </div>
                  <button *erpHasPermission="'projects.billing.manage'" nz-button nzType="primary" [disabled]="unbilledTimesheets().length === 0 && unbilledMilestones().length === 0" (click)="generateBilling()">
                    <span nz-icon nzType="dollar-circle"></span> Bill Work (AED {{ totalUnbilledAmount() | number: '1.2-2' }})
                  </button>
                </div>

                <div nz-row [nzGutter]="16" style="margin-top: 16px;">
                  <div nz-col [nzXs]="24" [nzMd]="12">
                    <nz-card nzTitle="Unbilled Hours" nzSize="small">
                      <p><b>{{ unbilledTimesheets().length }}</b> timesheet entries pending billing.</p>
                      <p class="billing-stat-amount">Total: AED {{ unbilledHoursTotal() | number: '1.2-2' }}</p>
                    </nz-card>
                  </div>
                  <div nz-col [nzXs]="24" [nzMd]="12">
                    <nz-card nzTitle="Achieved Milestones" nzSize="small">
                      <p><b>{{ unbilledMilestones().length }}</b> milestones reached and ready to invoice.</p>
                      <p class="billing-stat-amount">Total: AED {{ unbilledMilestonesTotal() | number: '1.2-2' }}</p>
                    </nz-card>
                  </div>
                </div>
              </div>
            </nz-tab>
          </nz-tabset>
        </nz-card>

        <!-- Task Modal -->
        <nz-modal [(nzVisible)]="taskModalVisible" nzTitle="Add Project Task" (nzOnCancel)="taskModalVisible = false" (nzOnOk)="saveTask()">
          <ng-container *nzModalContent>
            <form [formGroup]="taskForm" nz-form nzLayout="vertical">
              <nz-form-item>
                <nz-form-label nzRequired>Task Title</nz-form-label>
                <nz-form-control><input nz-input formControlName="title" placeholder="e.g. Design database schema" /></nz-form-control>
              </nz-form-item>
              <div nz-row [nzGutter]="12">
                <div nz-col [nzSpan]="12">
                  <nz-form-item>
                    <nz-form-label>Assigned To</nz-form-label>
                    <nz-form-control><input nz-input formControlName="assignedTo" placeholder="Developer name" /></nz-form-control>
                  </nz-form-item>
                </div>
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
            </form>
          </ng-container>
        </nz-modal>

        <!-- Milestone Modal -->
        <nz-modal [(nzVisible)]="milestoneModalVisible" nzTitle="Add Project Milestone" (nzOnCancel)="milestoneModalVisible = false" (nzOnOk)="saveMilestone()">
          <ng-container *nzModalContent>
            <form [formGroup]="milestoneForm" nz-form nzLayout="vertical">
              <nz-form-item>
                <nz-form-label nzRequired>Milestone Title</nz-form-label>
                <nz-form-control><input nz-input formControlName="title" placeholder="e.g. Phase 1 User Acceptance Testing" /></nz-form-control>
              </nz-form-item>
              <div nz-row [nzGutter]="12">
                <div nz-col [nzSpan]="12">
                  <nz-form-item>
                    <nz-form-label nzRequired>Billing Amount (AED)</nz-form-label>
                    <nz-form-control><nz-input-number formControlName="amount" [nzMin]="0" style="width:100%"></nz-input-number></nz-form-control>
                  </nz-form-item>
                </div>
                <div nz-col [nzSpan]="12">
                  <nz-form-item>
                    <nz-form-label nzRequired>Target Date</nz-form-label>
                    <nz-form-control><nz-date-picker formControlName="dueDate" style="width:100%"></nz-date-picker></nz-form-control>
                  </nz-form-item>
                </div>
              </div>
            </form>
          </ng-container>
        </nz-modal>

        <!-- Timesheet Modal -->
        <nz-modal [(nzVisible)]="timesheetModalVisible" nzTitle="Log Project Time" (nzOnCancel)="timesheetModalVisible = false" (nzOnOk)="saveTimesheet()">
          <ng-container *nzModalContent>
            <form [formGroup]="timesheetForm" nz-form nzLayout="vertical">
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
                <nz-form-control><input nz-input formControlName="description" placeholder="Summary of work completed" /></nz-form-control>
              </nz-form-item>
              <nz-form-item>
                <label nz-checkbox formControlName="isBillable">Billable to client</label>
              </nz-form-item>
            </form>
          </ng-container>
        </nz-modal>
      </div>
    }
  `,
  styles: [`
    .project-detail-container {
      display: flex;
      flex-direction: column;
      gap: 20px;
    }
    .detail-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      flex-wrap: wrap;
      gap: 16px;
    }
    .back-link {
      font-size: 13px;
      color: #64748b;
      display: inline-flex;
      align-items: center;
      gap: 6px;
      margin-bottom: 8px;
      text-decoration: none;
    }
    .back-link:hover {
      color: #10955d;
    }
    .heading-row {
      display: flex;
      align-items: center;
      gap: 12px;
      flex-wrap: wrap;
    }
    .project-title {
      font-size: 22px;
      font-weight: 700;
      color: #0f172a;
      margin: 0;
    }
    .meta-row {
      display: flex;
      gap: 18px;
      margin-top: 8px;
      flex-wrap: wrap;
    }
    .meta-item {
      font-size: 13px;
      color: #64748b;
      display: inline-flex;
      align-items: center;
      gap: 6px;
    }
    .header-btns {
      display: flex;
      gap: 8px;
    }
    .kpi-row {
      margin-bottom: 4px;
    }
    .stat-card {
      border-radius: 12px;
      border: 1px solid #e2e8f0;
      box-shadow: 0 1px 3px rgba(0,0,0,0.04);
    }
    .stat-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 4px;
    }
    .stat-label {
      font-size: 13px;
      font-weight: 600;
      color: #64748b;
    }
    .stat-icon {
      font-size: 18px;
      padding: 6px;
      border-radius: 8px;
    }
    .icon-budget { color: #d97706; background: #fef3c7; }
    .icon-tasks { color: #10955d; background: #e6f7ef; }
    .icon-milestones { color: #2563eb; background: #eff6ff; }
    .icon-hours { color: #7c3aed; background: #f5f3ff; }
    .stat-value {
      font-size: 20px;
      font-weight: 700;
      color: #0f172a;
      line-height: 1.2;
    }
    .stat-sub {
      font-size: 12px;
      color: #94a3b8;
      margin: 2px 0 8px 0;
    }
    .main-card {
      border-radius: 12px;
      border: 1px solid #e2e8f0;
    }
    .tab-toolbar {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 16px;
    }
    .tab-heading {
      font-size: 15px;
      font-weight: 600;
      color: #0f172a;
    }
    .overview-grid {
      padding: 12px 0;
    }
    .description-text {
      font-size: 14px;
      color: #334155;
      line-height: 1.6;
      background: #f8fafc;
      padding: 16px;
      border-radius: 8px;
      border: 1px solid #e2e8f0;
    }
    .props-list {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
      gap: 12px;
      margin-top: 16px;
    }
    .prop-row {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }
    .prop-label {
      font-size: 12px;
      color: #64748b;
      font-weight: 500;
    }
    .prop-val {
      font-size: 14px;
      font-weight: 600;
      color: #0f172a;
    }
    .item-title {
      font-weight: 600;
      color: #0f172a;
    }
    .milestone-subtag {
      display: block;
      font-size: 11.5px;
      color: #2563eb;
      margin-top: 2px;
    }
    .status-info-text {
      font-size: 12px;
      color: #d97706;
      font-weight: 500;
    }
    .billing-summary-box {
      padding: 12px 0;
    }
    .billing-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      flex-wrap: wrap;
      gap: 16px;
    }
    .tab-subtitle {
      font-size: 13px;
      color: #64748b;
      margin: 0;
    }
    .billing-stat-amount {
      font-size: 16px;
      font-weight: 700;
      color: #10955d;
      margin: 4px 0 0 0;
    }
  `],
})
export class ProjectDetailPage implements OnInit {
  readonly id = input.required<string>();

  private readonly projectsApi = inject(ProjectsApi);
  private readonly tasksApi = inject(ProjectTasksApi);
  private readonly milestonesApi = inject(ProjectMilestonesApi);
  private readonly timesheetsApi = inject(ProjectTimesheetsApi);
  private readonly billingApi = inject(ProjectBillingApi);
  private readonly fb = inject(FormBuilder);
  private readonly msg = inject(NzMessageService);

  readonly project = signal<ProjectDto | null>(null);
  readonly p = computed(() => this.project());
  readonly tasks = signal<TaskDto[]>([]);
  readonly milestones = signal<MilestoneDto[]>([]);
  readonly timesheets = signal<TimesheetDto[]>([]);
  readonly activeTabIndex = signal(0);

  taskModalVisible = false;
  milestoneModalVisible = false;
  timesheetModalVisible = false;

  readonly taskForm = this.fb.group({
    title: ['', Validators.required],
    assignedTo: [''],
    priority: ['Medium', Validators.required],
    estimatedHours: [0, [Validators.min(0)]],
    dueDate: [null],
  });

  readonly milestoneForm = this.fb.group({
    title: ['', Validators.required],
    amount: [0, [Validators.required, Validators.min(0)]],
    dueDate: [null, Validators.required],
  });

  readonly timesheetForm = this.fb.group({
    workerName: ['', Validators.required],
    date: [new Date(), Validators.required],
    hours: [1, [Validators.required, Validators.min(0.1), Validators.max(24)]],
    hourlyRate: [150, [Validators.required, Validators.min(0)]],
    description: [''],
    isBillable: [true],
  });

  readonly unbilledTimesheets = computed(() => this.timesheets().filter((ts) => ts.isBillable && !ts.isBilled));
  readonly unbilledHoursTotal = computed(() => this.unbilledTimesheets().reduce((acc, ts) => acc + ts.totalAmount, 0));
  readonly unbilledMilestones = computed(() => this.milestones().filter((m) => m.status === 'Achieved'));
  readonly unbilledMilestonesTotal = computed(() => this.unbilledMilestones().reduce((acc, m) => acc + m.amount, 0));
  readonly totalUnbilledAmount = computed(() => this.unbilledHoursTotal() + this.unbilledMilestonesTotal());

  ngOnInit(): void {
    this.loadAll();
  }

  loadAll(): void {
    const projId = this.id();
    this.projectsApi.get(projId).subscribe((res) => this.project.set(res));
    this.tasksApi.list({ page: 1, pageSize: 200, filters: [{ field: 'projectId', op: 'eq', value: projId }] }).subscribe((res) => this.tasks.set(res.items));
    this.milestonesApi.list({ page: 1, pageSize: 100, filters: [{ field: 'projectId', op: 'eq', value: projId }] }).subscribe((res) => this.milestones.set(res.items));
    this.timesheetsApi.list({ page: 1, pageSize: 200, filters: [{ field: 'projectId', op: 'eq', value: projId }] }).subscribe((res) => this.timesheets.set(res.items));
  }

  calcPercent(current: number, total: number): number {
    if (!total || total <= 0) return 0;
    return Math.min(100, Math.round((current / total) * 100));
  }

  statusColor(status: ProjectStatus): string {
    switch (status) {
      case 'Active': return 'green';
      case 'Planning': return 'blue';
      case 'OnHold': return 'warning';
      case 'Completed': return 'purple';
      case 'Cancelled': return 'error';
      default: return 'default';
    }
  }

  taskStatusColor(status: ProjectTaskStatus): string {
    switch (status) {
      case 'Done': return 'green';
      case 'InProgress': return 'blue';
      case 'Review': return 'purple';
      default: return 'default';
    }
  }

  priorityColor(priority: TaskPriority): string {
    switch (priority) {
      case 'Urgent': return 'error';
      case 'High': return 'warning';
      case 'Medium': return 'blue';
      default: return 'default';
    }
  }

  milestoneStatusColor(status: MilestoneStatus): string {
    switch (status) {
      case 'Achieved': return 'blue';
      case 'Invoiced': return 'green';
      default: return 'default';
    }
  }

  formatBillingType(type: string): string {
    switch (type) {
      case 'TimeAndMaterials': return 'Time & Materials';
      case 'FixedFee': return 'Fixed Fee';
      case 'NonBillable': return 'Non-Billable';
      default: return type;
    }
  }

  openTaskModal(): void {
    this.taskForm.reset({ priority: 'Medium', estimatedHours: 0 });
    this.taskModalVisible = true;
  }

  saveTask(): void {
    if (this.taskForm.invalid) return;
    const v = this.taskForm.value;
    const dueDateStr = v.dueDate ? new Date(v.dueDate).toISOString().split('T')[0] : null;

    this.tasksApi.create({
      projectId: this.id(),
      title: v.title!,
      assignedTo: v.assignedTo || null,
      priority: v.priority as TaskPriority,
      estimatedHours: v.estimatedHours || 0,
      actualHours: 0,
      dueDate: dueDateStr,
      status: 'Todo',
    }).subscribe(() => {
      this.msg.success('Task created');
      this.taskModalVisible = false;
      this.loadAll();
    });
  }

  updateTaskStatus(taskId: string, status: ProjectTaskStatus): void {
    this.tasksApi.setStatus(taskId, status).subscribe(() => {
      this.msg.success('Task status updated');
      this.loadAll();
    });
  }

  openMilestoneModal(): void {
    this.milestoneForm.reset({ amount: 0 });
    this.milestoneModalVisible = true;
  }

  saveMilestone(): void {
    if (this.milestoneForm.invalid) return;
    const v = this.milestoneForm.value;
    const dueDateStr = v.dueDate ? new Date(v.dueDate).toISOString().split('T')[0] : new Date().toISOString().split('T')[0];

    this.milestonesApi.create({
      projectId: this.id(),
      title: v.title!,
      amount: v.amount!,
      dueDate: dueDateStr,
      status: 'Pending',
    }).subscribe(() => {
      this.msg.success('Milestone created');
      this.milestoneModalVisible = false;
      this.loadAll();
    });
  }

  achieveMilestone(id: string): void {
    this.milestonesApi.achieve(id).subscribe(() => {
      this.msg.success('Milestone marked as achieved');
      this.loadAll();
    });
  }

  openTimesheetModal(): void {
    this.timesheetForm.reset({
      date: new Date(),
      hours: 1,
      hourlyRate: 150,
      isBillable: true,
    });
    this.timesheetModalVisible = true;
  }

  saveTimesheet(): void {
    if (this.timesheetForm.invalid) return;
    const v = this.timesheetForm.value;
    const dateStr = v.date ? new Date(v.date).toISOString().split('T')[0] : new Date().toISOString().split('T')[0];

    this.timesheetsApi.create({
      projectId: this.id(),
      workerName: v.workerName!,
      date: dateStr,
      hours: v.hours!,
      hourlyRate: v.hourlyRate!,
      description: v.description || null,
      isBillable: v.isBillable ?? true,
    }).subscribe(() => {
      this.msg.success('Timesheet logged');
      this.timesheetModalVisible = false;
      this.loadAll();
    });
  }

  generateBilling(): void {
    const unbilledTsIds = this.unbilledTimesheets().map((ts) => ts.id);
    const unbilledMsIds = this.unbilledMilestones().map((m) => m.id);

    this.billingApi.bill({
      projectId: this.id(),
      timesheetIds: unbilledTsIds,
      milestoneIds: unbilledMsIds,
    }).subscribe((res) => {
      this.msg.success(`Billed ${res.billedTimesheetsCount} timesheets and ${res.billedMilestonesCount} milestones (AED ${res.totalAmountBilled.toFixed(2)})`);
      this.loadAll();
    });
  }
}
