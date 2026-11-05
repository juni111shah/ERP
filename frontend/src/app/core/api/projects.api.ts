import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { CrudApiService } from './crud-api.service';

export type ProjectStatus = 'Planning' | 'Active' | 'OnHold' | 'Completed' | 'Cancelled';
export type ProjectBillingType = 'TimeAndMaterials' | 'FixedFee' | 'NonBillable';
export type ProjectTaskStatus = 'Todo' | 'InProgress' | 'Review' | 'Done' | 'Blocked' | 'Cancelled';
export type TaskPriority = 'Low' | 'Medium' | 'High' | 'Urgent';
export type MilestoneStatus = 'Pending' | 'Achieved' | 'Invoiced' | 'Cancelled';

export interface ProjectSummaryDto {
  id: string;
  number: string;
  name: string;
  legalEntityId: string;
  customerId?: string | null;
  customerName?: string | null;
  managerName?: string | null;
  status: ProjectStatus;
  billingType: ProjectBillingType;
  budgetAmount: number;
  startDate?: string | null;
  endDate?: string | null;
  createdAt: string;
  managerUserId?: string | null;
  isTemplate?: boolean;
  isArchived?: boolean;
}

export interface ProjectDto extends ProjectSummaryDto {
  description?: string | null;
  totalLoggedHours: number;
  totalBilledHours: number;
  totalTimesheetCost: number;
  totalTasks: number;
  completedTasks: number;
  totalMilestones: number;
  achievedMilestones: number;
  customFields?: Record<string, unknown> | null;
  taxCodeId?: string | null;
  budgetHours: number;
  sourceType?: string | null;
  sourceId?: string | null;
  effectiveBudget: number;
  approvedChanges: number;
  billableValue: number;
  expenseCost: number;
  billedAmount: number;
  unbilledAmount: number;
  percentComplete: number;
  health: 'onTrack' | 'atRisk' | 'offTrack' | 'closed';
  overdueTasks: number;
}

export interface ProjectInput {
  legalEntityId: string;
  number?: string | null;
  name: string;
  description?: string | null;
  customerId?: string | null;
  customerName?: string | null;
  managerName?: string | null;
  status: ProjectStatus;
  billingType: ProjectBillingType;
  budgetAmount: number;
  startDate?: string | null;
  endDate?: string | null;
  customFields?: Record<string, unknown> | null;
  managerUserId?: string | null;
  taxCodeId?: string | null;
  budgetHours?: number;
  sourceType?: string | null;
  sourceId?: string | null;
  isTemplate?: boolean;
}

export interface ProjectKpiDto {
  totalProjects: number;
  activeProjects: number;
  completedProjects: number;
  totalBudget: number;
  totalLoggedHours: number;
  unbilledAmount: number;
}

export interface TaskDto {
  id: string;
  projectId: string;
  projectName: string;
  milestoneId?: string | null;
  milestoneTitle?: string | null;
  number: string;
  title: string;
  description?: string | null;
  status: ProjectTaskStatus;
  priority: TaskPriority;
  assignedTo?: string | null;
  estimatedHours: number;
  actualHours: number;
  dueDate?: string | null;
  createdAt: string;
  assignedUserId?: string | null;
  parentTaskId?: string | null;
  startDate?: string | null;
  labels?: string | null;
  dependsOn?: string[] | null;
}

export interface TaskInput {
  projectId: string;
  milestoneId?: string | null;
  title: string;
  description?: string | null;
  status: ProjectTaskStatus;
  priority: TaskPriority;
  assignedTo?: string | null;
  estimatedHours: number;
  actualHours: number;
  dueDate?: string | null;
  assignedUserId?: string | null;
  parentTaskId?: string | null;
  startDate?: string | null;
  labels?: string | null;
}

export interface MilestoneDto {
  id: string;
  projectId: string;
  projectName: string;
  title: string;
  dueDate: string;
  amount: number;
  status: MilestoneStatus;
  achievedDate?: string | null;
  createdAt: string;
}

export interface MilestoneInput {
  projectId: string;
  title: string;
  dueDate: string;
  amount: number;
  status?: MilestoneStatus;
  achievedDate?: string | null;
}

export interface TimesheetDto {
  id: string;
  projectId: string;
  projectName: string;
  taskId?: string | null;
  taskTitle?: string | null;
  date: string;
  workerName: string;
  hours: number;
  hourlyRate: number;
  totalAmount: number;
  description?: string | null;
  isBillable: boolean;
  isBilled: boolean;
  invoiceId?: string | null;
  createdAt: string;
  workerUserId?: string | null;
  costRate: number;
  costAmount: number;
  approvalStatus: 'Draft' | 'Submitted' | 'Approved' | 'Rejected';
  rejectReason?: string | null;
  writtenOff: boolean;
}

export interface TimesheetInput {
  projectId: string;
  taskId?: string | null;
  date: string;
  workerName?: string | null;
  hours: number;
  hourlyRate?: number | null;
  description?: string | null;
  isBillable?: boolean;
  workerUserId?: string | null;
  costRate?: number | null;
}

export interface ProjectBillingSummaryDto {
  projectId: string;
  projectNumber: string;
  projectName: string;
  billingType: ProjectBillingType;
  unbilledHours: number;
  unbilledHoursAmount: number;
  achievedMilestonesCount: number;
  achievedMilestonesAmount: number;
  totalUnbilledAmount: number;
  unbilledExpensesAmount: number;
  awaitingApprovalAmount: number;
  fixedFeeRemaining: number;
}

export interface BillProjectInput {
  projectId: string;
  timesheetIds?: string[];
  milestoneIds?: string[];
  expenseIds?: string[];
  grouping?: string;
  discountPercent?: number;
  progressPercent?: number | null;
  notes?: string | null;
}

export interface BillProjectResultDto {
  projectId: string;
  billedTimesheetsCount: number;
  billedMilestonesCount: number;
  totalAmountBilled: number;
  invoiceId?: string;
  invoiceNumber?: string;
  billedExpensesCount?: number;
}

@Injectable({ providedIn: 'root' })
export class ProjectsApi extends CrudApiService<ProjectSummaryDto, ProjectInput> {
  protected readonly resource = 'm/projects/projects';

  override get(id: string): Observable<ProjectDto> {
    return this.http.get<ProjectDto>(`${this.url}/${id}`);
  }

  getKpis(): Observable<ProjectKpiDto> {
    return this.http.get<ProjectKpiDto>(`${this.url}/kpis`);
  }

  setStatus(id: string, status: ProjectStatus): Observable<ProjectDto> {
    return this.http.put<ProjectDto>(`${this.url}/${id}/status`, { status });
  }
}

@Injectable({ providedIn: 'root' })
export class ProjectTasksApi extends CrudApiService<TaskDto, TaskInput> {
  protected readonly resource = 'm/projects/tasks';

  setStatus(id: string, status: ProjectTaskStatus): Observable<TaskDto> {
    return this.http.put<TaskDto>(`${this.url}/${id}/status`, { status });
  }
}

@Injectable({ providedIn: 'root' })
export class ProjectMilestonesApi extends CrudApiService<MilestoneDto, MilestoneInput> {
  protected readonly resource = 'm/projects/milestones';

  achieve(id: string, achievedDate?: string): Observable<MilestoneDto> {
    return this.http.post<MilestoneDto>(`${this.url}/${id}/achieve`, { achievedDate });
  }
}

@Injectable({ providedIn: 'root' })
export class ProjectTimesheetsApi extends CrudApiService<TimesheetDto, TimesheetInput> {
  protected readonly resource = 'm/projects/timesheets';
}

@Injectable({ providedIn: 'root' })
export class ProjectBillingApi {
  constructor(private readonly api: ProjectsApi) {}

  getOverview(): Observable<ProjectBillingSummaryDto[]> {
    return this.api['http'].get<ProjectBillingSummaryDto[]>(`${this.api['baseUrl']}/m/projects/billing/overview`);
  }

  bill(body: BillProjectInput): Observable<BillProjectResultDto> {
    return this.api['http'].post<BillProjectResultDto>(`${this.api['baseUrl']}/m/projects/billing/bill`, body);
  }
}
