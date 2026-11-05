import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-config';
import { CrudApiService } from './crud-api.service';
import { MilestoneStatus, ProjectBillingType, ProjectDto, ProjectHealth, ProjectStatus, ProjectTaskStatus, TaskDto, TimesheetDto } from './projects.api';

export type TimesheetApproval = 'draft' | 'submitted' | 'approved' | 'rejected';
export type ExpenseStatus = 'draft' | 'submitted' | 'approved' | 'rejected';
export type ChangeOrderStatus = 'draft' | 'approved' | 'rejected';
export type RiskKind = 'risk' | 'issue';
export type RiskSeverity = 'low' | 'medium' | 'high' | 'critical';
export type RiskStatus = 'open' | 'mitigated' | 'closed';
export type AcceptanceStatus = 'pending' | 'signed' | 'declined';

export const EXPENSE_CATEGORIES = ['Travel', 'Accommodation', 'Materials', 'Subcontractor', 'Software', 'Meals', 'Other'].map((c) => ({ value: c, label: c }));

export interface UserOption { id: string; fullName: string; email: string; isActive: boolean; }

export interface ExpenseDto {
  id: string; projectId: string; projectName: string; date: string; category: string; description: string; amount: number; isBillable: boolean; markupPercent: number; chargeAmount: number;
  workerUserId?: string | null; workerName?: string | null; status: ExpenseStatus; rejectReason?: string | null; isBilled: boolean; invoiceId?: string | null; writtenOff: boolean; createdAt: string;
}
export interface ExpenseInput {
  projectId: string; date: string; category: string; description: string; amount: number; isBillable?: boolean; markupPercent?: number; workerUserId?: string | null; workerName?: string | null;
}

export interface RateDto { id: string; projectId?: string | null; userId?: string | null; userName?: string | null; role?: string | null; billRate: number; costRate: number; }
export interface RateInput { projectId?: string | null; userId?: string | null; role?: string | null; billRate: number; costRate: number; }
export interface CommentDto { id: string; ownerType: string; ownerId: string; userId?: string | null; authorName: string; text: string; createdAt: string; }
export interface ChangeOrderDto { id: string; projectId: string; number: string; title: string; description?: string | null; amountChange: number; hoursChange: number; status: ChangeOrderStatus; decidedAt?: string | null; decisionNote?: string | null; createdAt: string; }
export interface ChangeOrderInput { projectId: string; title: string; description?: string | null; amountChange: number; hoursChange: number; }
export interface RiskDto { id: string; projectId: string; kind: RiskKind; title: string; notes?: string | null; severity: RiskSeverity; status: RiskStatus; ownerUserId?: string | null; ownerName?: string | null; dueDate?: string | null; createdAt: string; }
export interface RiskInput { projectId: string; kind: RiskKind; title: string; notes?: string | null; severity: RiskSeverity; status: RiskStatus; ownerUserId?: string | null; ownerName?: string | null; dueDate?: string | null; }
export interface MemberDto { id: string; projectId: string; userId: string; name: string; role?: string | null; }
export interface AllocationDto { id: string; projectId: string; userId: string; userName: string; fromDate: string; toDate: string; hoursPerWeek: number; }
export interface AllocationInput { projectId: string; userId: string; fromDate: string; toDate: string; hoursPerWeek: number; }
export interface AcceptanceDto { id: string; projectId: string; projectName: string; milestoneId?: string | null; title: string; status: AcceptanceStatus; requestedAt: string; signedByName?: string | null; signedAt?: string | null; comment?: string | null; drawnSignature: boolean; signedIp?: string | null; evidenceHash?: string | null; }

export interface ScheduleTaskDto { id: string; number: string; title: string; parentTaskId?: string | null; status: ProjectTaskStatus; start: string; end: string; durationDays: number; slackDays: number; critical: boolean; dependsOn: string[]; assignedTo?: string | null; dueDate?: string | null; late: boolean; baselineStart?: string | null; baselineEnd?: string | null; }
export interface ScheduleMilestoneDto { id: string; title: string; date: string; status: MilestoneStatus; }
export interface ScheduleDto { projectId: string; start: string; finish: string; tasks: ScheduleTaskDto[]; criticalPath: string[]; milestones: ScheduleMilestoneDto[]; hasBaseline: boolean; }

export interface BillPreviewLine { description: string; quantity: number; price: number; amount: number; }
export interface BillPreviewDto { projectId: string; lines: BillPreviewLine[]; subtotal: number; discount: number; total: number; timesheets: number; milestones: number; expenses: number; }
export interface BillInput { projectId: string; timesheetIds?: string[]; milestoneIds?: string[]; expenseIds?: string[]; grouping?: string; discountPercent?: number; progressPercent?: number | null; notes?: string | null; dueDate?: string | null; }
export interface BillResult { projectId: string; billedTimesheetsCount: number; billedMilestonesCount: number; billedExpensesCount: number; totalAmountBilled: number; invoiceId?: string; invoiceNumber?: string; }
export interface BillableTime { id: string; date: string; worker: string; task?: string | null; description?: string | null; hours: number; amount: number; }
export interface BillableExpense { id: string; date: string; category: string; description: string; amount: number; }
export interface BillableMilestone { id: string; title: string; achievedDate?: string | null; amount: number; }
export interface BillableWork { projectId: string; billingType: ProjectBillingType; fee: number; fixedFeeRemaining: number; time: BillableTime[]; expenses: BillableExpense[]; milestones: BillableMilestone[]; }
export interface ProjectInvoice { id: string; number: string; date: string; dueDate: string; status: string; total: number; paid: number; }

export interface ProfitabilityRow { projectId: string; number: string; name: string; customerName?: string; status: ProjectStatus; billingType: ProjectBillingType; hours: number; billableHours: number; billed: number; unbilled: number; labourCost: number; expenseCost: number; cost: number; margin: number; marginPercent: number; }
export interface UtilisationRow { person: string; userId?: string; loggedHours: number; billableHours: number; capacityHours: number; allocatedHours: number; utilisationPercent: number; billablePercent: number; }
export interface BudgetRow { projectId: string; number: string; name: string; status: ProjectStatus; billingType: ProjectBillingType; budget: number; approvedChanges: number; cost: number; usedPercent: number; budgetHours: number; loggedHours: number; percentComplete: number; forecastCost: number; forecastVariance: number; earnedRevenue: number; billed: number; workInProgress: number; health: ProjectHealth; plannedValue: number; earnedValue: number; costIndex?: number | null; scheduleIndex?: number | null; }
export interface AgeingRow { projectId: string; number: string; name: string; current: number; days31To60: number; days61To90: number; over90: number; total: number; }
export interface HoursRow { group: string; hours: number; billableHours: number; amount: number; cost: number; }

export interface WeekEntry { id?: string; projectId: string; taskId?: string | null; date: string; workerUserId?: string | null; workerName?: string | null; hours: number; description?: string | null; isBillable?: boolean; hourlyRate?: number | null; }

export interface ActivityDto { at: string; who?: string | null; action: string; kind: string; title: string; fields: string[]; }
export interface ProjectsConfig { timesheetApproval: boolean; expenseApproval: boolean; allowSelfApproval: boolean; maxHoursPerDay: number; weeklyCapacityHours: number; workDays: number[]; defaultHourlyRate: number; }
export interface BudgetLineDto { id?: string | null; category: string; amount: number; hours: number; actual: number; actualHours: number; }
export interface BudgetLineInput { projectId: string; category: string; amount: number; hours?: number; }

@Injectable({ providedIn: 'root' })
export class ProjectExpensesApi extends CrudApiService<ExpenseDto, ExpenseInput> {
  protected readonly resource = 'm/projects/expenses';
  submit(ids: string[]): Observable<{ submitted: number }> { return this.http.post<{ submitted: number }>(`${this.url}/submit`, { ids }); }
  decide(ids: string[], approve: boolean, reason?: string): Observable<{ decided: number }> { return this.http.post<{ decided: number }>(`${this.url}/decide`, { ids, approve, reason }); }
  writeOff(ids: string[]): Observable<{ writtenOff: number }> { return this.http.post<{ writtenOff: number }>(`${this.url}/write-off`, { ids }); }
}

/** Everything in the Projects module that is not a plain list-and-edit resource. */
@Injectable({ providedIn: 'root' })
export class ProjectsExtraApi {
  private readonly http = inject(HttpClient);
  private readonly root = inject(API_BASE_URL);
  private readonly m = `${this.root}/m/projects`;

  users(): Observable<UserOption[]> { return this.http.get<UserOption[]>(`${this.root}/users`); }
  config(): Observable<ProjectsConfig> { return this.http.get<ProjectsConfig>(`${this.m}/config`); }

  // planning
  schedule(projectId: string): Observable<ScheduleDto> { return this.http.get<ScheduleDto>(`${this.m}/projects/${projectId}/schedule`); }
  setBaseline(projectId: string, clear: boolean): Observable<ScheduleDto> { return this.http.post<ScheduleDto>(`${this.m}/projects/${projectId}/baseline`, { clear }); }
  setDependencies(taskId: string, dependsOn: string[]): Observable<TaskDto> { return this.http.put<TaskDto>(`${this.m}/tasks/${taskId}/dependencies`, { dependsOn }); }
  task(id: string): Observable<TaskDto> { return this.http.get<TaskDto>(`${this.m}/tasks/${id}`); }
  clone(projectId: string, body: { name: string; startDate?: string | null; customerId?: string | null; includeTeam?: boolean; asTemplate?: boolean }): Observable<ProjectDto> { return this.http.post<ProjectDto>(`${this.m}/projects/${projectId}/clone`, body); }
  archive(projectId: string, archive: boolean): Observable<ProjectDto> { return this.http.post<ProjectDto>(`${this.m}/projects/${projectId}/archive`, { archive }); }
  activity(projectId: string): Observable<ActivityDto[]> { return this.http.get<ActivityDto[]>(`${this.m}/projects/${projectId}/activity`); }

  acceptances(projectId: string): Observable<AcceptanceDto[]> { return this.http.get<AcceptanceDto[]>(`${this.m}/acceptances`, { params: { projectId } }); }
  requestAcceptance(body: { projectId: string; milestoneId?: string | null; title: string }): Observable<AcceptanceDto> { return this.http.post<AcceptanceDto>(`${this.m}/acceptances`, body); }
  postCosts(projectId: string): Observable<{ projectId: string; entries: number; amount: number; journalId?: string; journalNumber?: string }> { return this.http.post<{ projectId: string; entries: number; amount: number; journalId?: string; journalNumber?: string }>(`${this.m}/billing/post-costs`, { projectId }); }

  // time
  saveWeek(items: WeekEntry[]): Observable<TimesheetDto[]> { return this.http.post<TimesheetDto[]>(`${this.m}/timesheets/week`, { items }); }
  submitTime(ids: string[]): Observable<{ submitted: number }> { return this.http.post<{ submitted: number }>(`${this.m}/timesheets/submit`, { ids }); }
  decideTime(ids: string[], approve: boolean, reason?: string): Observable<{ decided: number }> { return this.http.post<{ decided: number }>(`${this.m}/timesheets/decide`, { ids, approve, reason }); }

  // billing
  billable(projectId: string): Observable<BillableWork> { return this.http.get<BillableWork>(`${this.m}/billing/${projectId}/billable`); }
  projectInvoices(projectId: string): Observable<ProjectInvoice[]> { return this.http.get<ProjectInvoice[]>(`${this.m}/billing/${projectId}/invoices`); }
  previewBill(body: BillInput): Observable<BillPreviewDto> { return this.http.post<BillPreviewDto>(`${this.m}/billing/preview`, body); }
  bill(body: BillInput): Observable<BillResult> { return this.http.post<BillResult>(`${this.m}/billing/bill`, body); }
  writeOffProject(projectId: string): Observable<{ writtenOff: number }> { return this.http.post<{ writtenOff: number }>(`${this.m}/billing/write-off`, { projectId }); }

  // budget by category
  budgetLines(projectId: string): Observable<BudgetLineDto[]> { return this.http.get<BudgetLineDto[]>(`${this.m}/projects/${projectId}/budget-lines`); }
  saveBudgetLine(id: string | null, body: BudgetLineInput): Observable<BudgetLineDto> { return id ? this.http.put<BudgetLineDto>(`${this.m}/budget-lines/${id}`, body) : this.http.post<BudgetLineDto>(`${this.m}/budget-lines`, body); }
  deleteBudgetLine(id: string): Observable<void> { return this.http.delete<void>(`${this.m}/budget-lines/${id}`); }

  // rates
  rates(projectId?: string | null): Observable<RateDto[]> { return this.http.get<RateDto[]>(`${this.m}/rates`, { params: projectId ? { projectId } : {} }); }
  saveRate(id: string | null, body: RateInput): Observable<RateDto> { return id ? this.http.put<RateDto>(`${this.m}/rates/${id}`, body) : this.http.post<RateDto>(`${this.m}/rates`, body); }
  deleteRate(id: string): Observable<void> { return this.http.delete<void>(`${this.m}/rates/${id}`); }

  // comments
  comments(ownerType: string, ownerId: string): Observable<CommentDto[]> { return this.http.get<CommentDto[]>(`${this.m}/comments`, { params: { ownerType, ownerId } }); }
  addComment(ownerType: string, ownerId: string, text: string): Observable<CommentDto> { return this.http.post<CommentDto>(`${this.m}/comments`, { ownerType, ownerId, text }); }
  deleteComment(id: string): Observable<void> { return this.http.delete<void>(`${this.m}/comments/${id}`); }

  // change orders
  changeOrders(projectId?: string | null): Observable<ChangeOrderDto[]> { return this.http.get<ChangeOrderDto[]>(`${this.m}/change-orders`, { params: projectId ? { projectId } : {} }); }
  saveChangeOrder(id: string | null, body: ChangeOrderInput): Observable<ChangeOrderDto> { return id ? this.http.put<ChangeOrderDto>(`${this.m}/change-orders/${id}`, body) : this.http.post<ChangeOrderDto>(`${this.m}/change-orders`, body); }
  deleteChangeOrder(id: string): Observable<void> { return this.http.delete<void>(`${this.m}/change-orders/${id}`); }
  decideChangeOrder(id: string, approve: boolean, note?: string): Observable<ChangeOrderDto> { return this.http.post<ChangeOrderDto>(`${this.m}/change-orders/${id}/decide`, { approve, note }); }

  // risks
  risks(projectId?: string | null): Observable<RiskDto[]> { return this.http.get<RiskDto[]>(`${this.m}/risks`, { params: projectId ? { projectId } : {} }); }
  saveRisk(id: string | null, body: RiskInput): Observable<RiskDto> { return id ? this.http.put<RiskDto>(`${this.m}/risks/${id}`, body) : this.http.post<RiskDto>(`${this.m}/risks`, body); }
  deleteRisk(id: string): Observable<void> { return this.http.delete<void>(`${this.m}/risks/${id}`); }

  // team and allocation
  members(projectId: string): Observable<MemberDto[]> { return this.http.get<MemberDto[]>(`${this.m}/projects/${projectId}/members`); }
  addMember(projectId: string, userId: string, role?: string | null): Observable<MemberDto> { return this.http.post<MemberDto>(`${this.m}/projects/${projectId}/members`, { userId, role }); }
  removeMember(memberId: string): Observable<void> { return this.http.delete<void>(`${this.m}/projects/members/${memberId}`); }
  allocations(projectId?: string | null, userId?: string | null): Observable<AllocationDto[]> {
    let params = new HttpParams(); if (projectId) params = params.set('projectId', projectId); if (userId) params = params.set('userId', userId);
    return this.http.get<AllocationDto[]>(`${this.m}/allocations`, { params });
  }
  saveAllocation(id: string | null, body: AllocationInput): Observable<AllocationDto> { return id ? this.http.put<AllocationDto>(`${this.m}/allocations/${id}`, body) : this.http.post<AllocationDto>(`${this.m}/allocations`, body); }
  deleteAllocation(id: string): Observable<void> { return this.http.delete<void>(`${this.m}/allocations/${id}`); }

  // reports
  profitability(from?: string, to?: string, projectId?: string): Observable<ProfitabilityRow[]> { return this.http.get<ProfitabilityRow[]>(`${this.m}/reports/profitability`, { params: this.p({ from, to, projectId }) }); }
  utilisation(from: string, to: string): Observable<UtilisationRow[]> { return this.http.get<UtilisationRow[]>(`${this.m}/reports/utilisation`, { params: { from, to } }); }
  budget(): Observable<BudgetRow[]> { return this.http.get<BudgetRow[]>(`${this.m}/reports/budget`); }
  ageing(asOf?: string): Observable<AgeingRow[]> { return this.http.get<AgeingRow[]>(`${this.m}/reports/unbilled-ageing`, { params: this.p({ asOf }) }); }
  hours(from: string, to: string, groupBy: string, projectId?: string): Observable<HoursRow[]> { return this.http.get<HoursRow[]>(`${this.m}/reports/hours`, { params: this.p({ from, to, groupBy, projectId }) }); }

  private p(values: Record<string, string | undefined>): Record<string, string> {
    return Object.fromEntries(Object.entries(values).filter(([, v]) => v)) as Record<string, string>;
  }
}
