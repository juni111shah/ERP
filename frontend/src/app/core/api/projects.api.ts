import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { CrudApiService } from './crud-api.service';

// The API sends enum values in camelCase (see Program.cs), so that is what these types and every comparison use.
export type ProjectStatus = 'planning' | 'active' | 'onHold' | 'completed' | 'cancelled';
export type ProjectBillingType = 'timeAndMaterials' | 'fixedFee' | 'nonBillable';
export type ProjectTaskStatus = 'todo' | 'inProgress' | 'review' | 'done' | 'blocked' | 'cancelled';
export type TaskPriority = 'low' | 'medium' | 'high' | 'urgent';
export type MilestoneStatus = 'pending' | 'achieved' | 'invoiced' | 'cancelled';
export type ApprovalStatus = 'draft' | 'submitted' | 'approved' | 'rejected';
export type ProjectHealth = 'onTrack' | 'atRisk' | 'offTrack' | 'closed';

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
  percentComplete?: number;
  health?: ProjectHealth | null;
  cost?: number;
  effectiveBudget?: number;
  openTasks?: number;
  overdueTasks?: number;
  unbilledAmount?: number;
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
  health: ProjectHealth;
  overdueTasks: number;
  openTasks: number;
  effectiveBudgetHours: number;
  labourCost: number;
  nextStatuses?: ProjectStatus[] | null;
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
  budgetHours?: number | null;
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
  onHoldProjects: number;
  atRiskProjects: number;
  offTrackProjects: number;
  overdueTasks: number;
  totalCost: number;
  templateCount: number;
  archivedCount: number;
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

/** The body that saves a task exactly as it is, for changes that touch one or two fields. */
export function taskToInput(t: TaskDto): TaskInput {
  return {
    projectId: t.projectId, milestoneId: t.milestoneId ?? null, title: t.title, description: t.description ?? null, status: t.status, priority: t.priority, assignedTo: t.assignedTo ?? null,
    estimatedHours: t.estimatedHours, actualHours: t.actualHours, dueDate: t.dueDate ?? null, assignedUserId: t.assignedUserId ?? null, parentTaskId: t.parentTaskId ?? null, startDate: t.startDate ?? null, labels: t.labels ?? null,
  };
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
  approvalStatus: ApprovalStatus;
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

  /** Projects that can still take work, for pickers: no templates, nothing archived or closed. */
  open(): Observable<{ items: ProjectSummaryDto[] }> {
    return this.list({
      page: 1, pageSize: 200, sort: 'name',
      filters: [{ field: 'isTemplate', op: 'eq', value: false }, { field: 'isArchived', op: 'eq', value: false }, { field: 'status', op: 'in', value: ['planning', 'active', 'onHold'] }],
    });
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
}
