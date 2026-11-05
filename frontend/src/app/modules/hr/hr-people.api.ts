import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api-config';
import { PagedResult } from '../../core/api/api.models';
import { CrudApiService } from '../../core/api/crud-api.service';
import { LeaveBalanceRow, LeaveRequestDto, LeaveRequestRow, PayrollDto } from './hr.api';

const BASE = 'm/hr';

// ---- Structure: departments, positions, org chart -------------------------------------------------------------------------

export interface DepartmentDto { id: string; code: string; name: string; parentDepartmentId?: string; headEmployeeId?: string; businessUnitId?: string; isActive: boolean; }
export interface PositionDto { id: string; code: string; title: string; grade?: string; isActive: boolean; }
export interface OrgMember { id: string; number: string; name: string; designation: string; }
export interface OrgDepartmentNode { id: string; code: string; name: string; headEmployeeId?: string; members: OrgMember[]; children: OrgDepartmentNode[]; }
export interface OrgPersonNode { id: string; number: string; name: string; designation: string; departmentId?: string; reports: OrgPersonNode[]; }
export interface OrgChartDto { departments: OrgDepartmentNode[]; unassigned: OrgMember[]; reportingLines: OrgPersonNode[]; }

@Injectable({ providedIn: 'root' })
export class DepartmentsApi extends CrudApiService<DepartmentDto, unknown> { protected readonly resource = `${BASE}/departments`; }

@Injectable({ providedIn: 'root' })
export class PositionsApi extends CrudApiService<PositionDto, unknown> { protected readonly resource = `${BASE}/positions`; }

@Injectable({ providedIn: 'root' })
export class OrgChartApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/${BASE}/org-chart`;
  get(): Observable<OrgChartDto> { return this.http.get<OrgChartDto>(this.url); }
}

// ---- Employee documents ----------------------------------------------------------------------------------------------------

export const DOCUMENT_TYPES = [
  { value: 'passport', label: 'Passport' }, { value: 'visa', label: 'Visa' }, { value: 'residenceId', label: 'Residence ID' }, { value: 'labourCard', label: 'Labour card' },
  { value: 'medical', label: 'Medical' }, { value: 'contract', label: 'Contract' }, { value: 'certificate', label: 'Certificate' }, { value: 'other', label: 'Other' },
];
export interface EmployeeDocumentRow { id: string; employeeId: string; type: string; number?: string; issueDate?: string; expiryDate?: string; issuedBy?: string; }
export interface EmployeeDocumentDto extends EmployeeDocumentRow { notes?: string; daysLeft?: number; isExpired: boolean; }
export interface ExpiringDocumentDto { id: string; employeeId: string; employeeNumber: string; employeeName: string; type: string; number?: string; expiryDate: string; daysLeft: number; isExpired: boolean; }

@Injectable({ providedIn: 'root' })
export class EmployeeDocumentsApi extends CrudApiService<EmployeeDocumentRow, unknown> {
  protected readonly resource = `${BASE}/documents`;
  detail(id: string): Observable<EmployeeDocumentDto> { return this.http.get<EmployeeDocumentDto>(`${this.url}/${id}`); }
  expiring(days: number): Observable<ExpiringDocumentDto[]> { return this.http.get<ExpiringDocumentDto[]>(`${this.url}/expiring`, { params: { days } }); }
}

// ---- Loans and advances ----------------------------------------------------------------------------------------------------

export const LOAN_KINDS = [{ value: 'loan', label: 'Loan' }, { value: 'advance', label: 'Salary advance' }];
export const LOAN_STATUSES = [{ value: 'active', label: 'Active' }, { value: 'settled', label: 'Settled' }, { value: 'cancelled', label: 'Cancelled' }];
export interface LoanRow { id: string; employeeId: string; kind: string; principal: number; remaining: number; startYear: number; startMonth: number; instalments: number; status: string; disbursedOn?: string; }
export interface LoanDto extends LoanRow { employeeName: string; instalmentAmount: number; reason?: string; disbursementJournalId?: string; }

@Injectable({ providedIn: 'root' })
export class LoansApi extends CrudApiService<LoanRow, unknown> {
  protected readonly resource = `${BASE}/loans`;
  detail(id: string): Observable<LoanDto> { return this.http.get<LoanDto>(`${this.url}/${id}`); }
  cancel(id: string): Observable<LoanDto> { return this.http.post<LoanDto>(`${this.url}/${id}/cancel`, {}); }
  disburse(id: string, bankAccountId: string, date: string): Observable<LoanDto> { return this.http.post<LoanDto>(`${this.url}/${id}/disburse`, { bankAccountId, date }); }
}

// ---- Termination and final settlement --------------------------------------------------------------------------------------

export interface SettlementPreview {
  employeeId: string; employeeName: string; terminationDate: string; lastWorkingDay?: string; year: number; month: number;
  finalBasic: number; finalAllowances: number; salaryAlreadyPaid: boolean; leaveDays: number; leaveEncashment: number; endOfService: number; gratuityRule?: string;
  recurringDeductions: number; noticeShortfallDays: number; noticeShortfallDeduction: number; loanBalance: number; loanRecovery: number; unrecoveredLoan: number;
  gross: number; deductions: number; netPay: number; settlementRunId?: string; notes: string[];
}

@Injectable({ providedIn: 'root' })
export class TerminationApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/${BASE}/employees`;
  preview(employeeId: string, terminationDate: string, noticeGivenOn?: string | null): Observable<SettlementPreview> {
    const params: Record<string, string> = { terminationDate };
    if (noticeGivenOn) params['noticeGivenOn'] = noticeGivenOn;
    return this.http.get<SettlementPreview>(`${this.url}/${employeeId}/settlement-preview`, { params });
  }
  terminate(employeeId: string, body: { terminationDate: string; reason?: string | null; lastWorkingDay?: string | null; noticeGivenOn?: string | null }): Observable<SettlementPreview> {
    return this.http.post<SettlementPreview>(`${this.url}/${employeeId}/terminate`, body);
  }
  finalSettlement(employeeId: string): Observable<PayrollDto> { return this.http.post<PayrollDto>(`${this.url}/${employeeId}/final-settlement`, {}); }
}

// ---- Reports ---------------------------------------------------------------------------------------------------------------

export interface HrCount { key: string; count: number; }
export interface HrMonthMovement { year: number; month: number; joiners: number; leavers: number; }
export interface HrMonthCost { year: number; month: number; runs: number; employees: number; gross: number; deductions: number; net: number; }
export interface HrSummaryReport {
  from: string; to: string; headcount: number; averageServiceYears: number; byDepartment: HrCount[]; byNationality: HrCount[]; byContractType: HrCount[];
  movements: HrMonthMovement[]; payrollCost?: HrMonthCost[]; leaveDaysByType: { leaveType: string; days: number }[]; absenceDays: number; absenceByMonth: { year: number; month: number; days: number }[];
}
export interface HrOccasion { employeeId: string; number: string; employee: string; date: string; daysAway: number; years: number; }
export interface HrOccasions { asOf: string; days: number; birthdays: HrOccasion[]; anniversaries: HrOccasion[]; }
export interface GratuityRow { employeeId: string; number: string; employee: string; legalEntityId: string; joined: string; basicSalary: number; serviceYears: number; eligible: boolean; entitlementDays: number; gratuity: number; }
export interface GratuityReport { asOf: string; rows: GratuityRow[]; totalLiability: number; rule?: string; }

@Injectable({ providedIn: 'root' })
export class HrReportsApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/${BASE}/reports`;
  summary(from?: string, to?: string): Observable<HrSummaryReport> {
    const params: Record<string, string> = {};
    if (from) params['from'] = from; if (to) params['to'] = to;
    return this.http.get<HrSummaryReport>(`${this.url}/summary`, { params });
  }
  occasions(days: number): Observable<HrOccasions> { return this.http.get<HrOccasions>(`${this.url}/birthdays-anniversaries`, { params: { days } }); }
  gratuity(asOf: string): Observable<GratuityReport> { return this.http.get<GratuityReport>(`${this.url}/gratuity`, { params: { asOf } }); }
}

// ---- Self-service (the signed-in user's own employee record) ---------------------------------------------------------------

export interface MyPayslip {
  lineId: string; runId: string; runNumber: string; year: number; month: number; status: string; paidOn?: string; employeeName: string; employeeNumber?: string;
  basic: number; allowances: number; overtime: number; absence: number; adjustment: number; adjustmentNote?: string; absentDays: number; overtimeHours: number;
  grossEarnings: number; deductions: number; netPay: number;
}
export interface MyEmployee {
  id: string; number: string; fullName: string; designation: string; department?: string; joinDate: string; email?: string; phone?: string; nationality?: string;
  managerName?: string; serviceYears: number; onLeaveToday?: boolean; contractType?: string; probationEndDate?: string; contractEndDate?: string; noticePeriodDays?: number;
}

@Injectable({ providedIn: 'root' })
export class MyHrApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/${BASE}/me`;
  me(): Observable<MyEmployee> { return this.http.get<MyEmployee>(this.url); }
  leave(page = 1, pageSize = 50): Observable<PagedResult<LeaveRequestRow>> { return this.http.get<PagedResult<LeaveRequestRow>>(`${this.url}/leave`, { params: { page, pageSize } }); }
  requestLeave(body: { leaveTypeId: string; fromDate: string; toDate: string; reason?: string | null; submit: boolean }): Observable<LeaveRequestDto> { return this.http.post<LeaveRequestDto>(`${this.url}/leave`, body); }
  submitLeave(id: string): Observable<LeaveRequestDto> { return this.http.post<LeaveRequestDto>(`${this.url}/leave/${id}/submit`, {}); }
  cancelLeave(id: string): Observable<LeaveRequestDto> { return this.http.post<LeaveRequestDto>(`${this.url}/leave/${id}/cancel`, {}); }
  balances(year?: number): Observable<LeaveBalanceRow[]> { return this.http.get<LeaveBalanceRow[]>(`${this.url}/balances`, { params: year ? { year } : {} }); }
  payslips(): Observable<MyPayslip[]> { return this.http.get<MyPayslip[]>(`${this.url}/payslips`); }
  teamLeave(status?: string, page = 1, pageSize = 50): Observable<PagedResult<LeaveRequestRow>> {
    const params: Record<string, string | number> = { page, pageSize };
    if (status) params['status'] = status;
    return this.http.get<PagedResult<LeaveRequestRow>>(`${this.url}/team/leave`, { params });
  }
}

// ---- Statutory payroll rules -----------------------------------------------------------------------------------------------

export const STATUTORY_KINDS = [{ value: 'employeeDeduction', label: 'Employee deduction' }, { value: 'employerCost', label: 'Employer cost' }];
export const STATUTORY_METHODS = [{ value: 'flat', label: 'Flat percent' }, { value: 'slab', label: 'Slabs (progressive)' }];
export const STATUTORY_BASES = [{ value: 'basic', label: 'Basic salary' }, { value: 'gross', label: 'Gross pay' }];
export interface StatutoryRuleRow { id: string; legalEntityId: string; code: string; name: string; kind: string; method: string; base: string; rate: number; isActive: boolean; }
export interface StatutoryRuleDto extends StatutoryRuleRow {
  threshold: number; cap: number; nationalities?: string; effectiveFrom?: string; effectiveTo?: string; slabs: { fromAmount: number; rate: number }[];
}
export interface StatutoryTotal { code: string; name: string; kind: string; employees: number; baseAmount: number; amount: number; }

@Injectable({ providedIn: 'root' })
export class StatutoryRulesApi extends CrudApiService<StatutoryRuleRow, unknown> {
  protected readonly resource = `${BASE}/statutory-rules`;
  detail(id: string): Observable<StatutoryRuleDto> { return this.http.get<StatutoryRuleDto>(`${this.url}/${id}`); }
  totals(runId: string): Observable<StatutoryTotal[]> { return this.http.get<StatutoryTotal[]>(`${this.baseUrl}/${BASE}/payroll/${runId}/statutory`); }
}

// ---- Daily attendance ------------------------------------------------------------------------------------------------------

export const DAILY_STATUSES = [
  { value: 'present', label: 'Present' }, { value: 'absent', label: 'Absent' }, { value: 'leave', label: 'On leave' }, { value: 'holiday', label: 'Public holiday' }, { value: 'weeklyOff', label: 'Weekly off' }, { value: 'halfDay', label: 'Half day' },
];
export interface DailyAttendance { id: string; employeeId: string; date: string; checkIn?: string; checkOut?: string; status: string; workedMinutes: number; lateMinutes: number; overtimeMinutes: number; notes?: string; source?: string; }
export interface DailyInput { employeeId: string; date: string; checkIn?: string | null; checkOut?: string | null; status?: string | null; notes?: string | null; source?: string | null; }
export interface RollUpRow { employeeId: string; employeeName: string; workingDays: number; daysPresent: number; unpaidAbsentDays: number; paidLeaveDays: number; overtimeHours: number; lateDays: number; lateMinutes: number; }

@Injectable({ providedIn: 'root' })
export class DailyAttendanceApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/${BASE}/attendance/daily`;
  month(year: number, month: number, employeeId?: string | null): Observable<DailyAttendance[]> {
    const params: Record<string, string | number> = { year, month };
    if (employeeId) params['employeeId'] = employeeId;
    return this.http.get<DailyAttendance[]>(this.url, { params });
  }
  save(body: DailyInput): Observable<DailyAttendance> { return this.http.put<DailyAttendance>(this.url, body); }
  bulk(items: DailyInput[]): Observable<{ saved: number; rejected: string[] }> { return this.http.post<{ saved: number; rejected: string[] }>(`${this.url}/bulk`, { items }); }
  rollUp(year: number, month: number, legalEntityId?: string | null): Observable<RollUpRow[]> { return this.http.post<RollUpRow[]>(`${this.url}/roll-up`, { year, month, legalEntityId: legalEntityId ?? null }); }
  remove(id: string): Observable<void> { return this.http.delete<void>(`${this.url}/${id}`); }
}

// ---- History, onboarding checklists and tasks ------------------------------------------------------------------------------

export interface HistoryEntry { id: string; effectiveDate: string; kind: string; summary: string; oldValue?: string; newValue?: string; isPay: boolean; }
export const CHECKLIST_KINDS = [{ value: 'onboarding', label: 'Onboarding' }, { value: 'offboarding', label: 'Offboarding' }];
export interface ChecklistRow { id: string; name: string; kind: string; isActive: boolean; }
export interface ChecklistDto extends ChecklistRow { items: { title: string; dueOffsetDays: number }[]; }
export interface EmployeeTask { id: string; employeeId: string; kind: string; title: string; dueDate?: string; isDone: boolean; doneAt?: string; notes?: string; overdue: boolean; }
export interface OpenTask { id: string; employeeId: string; employeeName: string; kind: string; title: string; dueDate?: string; overdue: boolean; }

@Injectable({ providedIn: 'root' })
export class EmployeeLifeApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/${BASE}`;
  history(employeeId: string): Observable<HistoryEntry[]> { return this.http.get<HistoryEntry[]>(`${this.url}/employees/${employeeId}/history`); }
  addNote(employeeId: string, summary: string): Observable<HistoryEntry> { return this.http.post<HistoryEntry>(`${this.url}/employees/${employeeId}/history`, { summary }); }
  tasks(employeeId: string): Observable<EmployeeTask[]> { return this.http.get<EmployeeTask[]>(`${this.url}/employees/${employeeId}/tasks`); }
  addTask(employeeId: string, body: { title: string; dueDate?: string | null; kind: string }): Observable<EmployeeTask> { return this.http.post<EmployeeTask>(`${this.url}/employees/${employeeId}/tasks`, body); }
  applyChecklist(employeeId: string, templateId: string): Observable<EmployeeTask[]> { return this.http.post<EmployeeTask[]>(`${this.url}/employees/${employeeId}/checklists/${templateId}`, {}); }
  setDone(taskId: string, done: boolean): Observable<EmployeeTask> { return this.http.post<EmployeeTask>(`${this.url}/tasks/${taskId}/done`, { done }); }
  removeTask(taskId: string): Observable<void> { return this.http.delete<void>(`${this.url}/tasks/${taskId}`); }
  openTasks(): Observable<OpenTask[]> { return this.http.get<OpenTask[]>(`${this.url}/tasks/open`); }
}

@Injectable({ providedIn: 'root' })
export class ChecklistsApi extends CrudApiService<ChecklistRow, unknown> {
  protected readonly resource = `${BASE}/checklists`;
  detail(id: string): Observable<ChecklistDto> { return this.http.get<ChecklistDto>(`${this.url}/${id}`); }
}

// ---- Shifts ----------------------------------------------------------------------------------------------------------------

export interface ShiftDto { id: string; code: string; name: string; startTime: string; graceMinutes: number; hoursPerDay: number; isActive: boolean; employees: number; }

@Injectable({ providedIn: 'root' })
export class ShiftsApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/${BASE}/shifts`;
  list(): Observable<ShiftDto[]> { return this.http.get<ShiftDto[]>(this.url); }
  create(body: unknown): Observable<ShiftDto> { return this.http.post<ShiftDto>(this.url, body); }
  update(id: string, body: unknown): Observable<ShiftDto> { return this.http.put<ShiftDto>(`${this.url}/${id}`, body); }
  remove(id: string): Observable<void> { return this.http.delete<void>(`${this.url}/${id}`); }
  assign(id: string, employeeIds: string[]): Observable<{ assigned: number }> { return this.http.post<{ assigned: number }>(`${this.url}/${id}/assign`, { employeeIds }); }
  unassign(employeeIds: string[]): Observable<{ cleared: number }> { return this.http.post<{ cleared: number }>(`${this.url}/unassign`, { employeeIds }); }
}

// ---- Recruitment -----------------------------------------------------------------------------------------------------------

export const OPENING_STATUSES = [{ value: 'open', label: 'Open' }, { value: 'onHold', label: 'On hold' }, { value: 'closed', label: 'Closed' }];
export const CANDIDATE_STAGES = [
  { value: 'applied', label: 'Applied' }, { value: 'screening', label: 'Screening' }, { value: 'interview', label: 'Interview' },
  { value: 'offer', label: 'Offer' }, { value: 'hired', label: 'Hired' }, { value: 'rejected', label: 'Rejected' },
];
export interface OpeningRow { id: string; title: string; departmentId?: string; headcount: number; status: string; closingDate?: string; }
export interface OpeningDto extends OpeningRow { positionId?: string; description?: string; candidates: number; hired: number; }
export interface CandidateRow { id: string; openingId: string; fullName: string; email?: string; stage: string; stageChangedAt: string; }
export interface CandidateDto extends CandidateRow { phone?: string; source?: string; expectedSalary?: number; notes?: string; employeeId?: string; }

@Injectable({ providedIn: 'root' })
export class OpeningsApi extends CrudApiService<OpeningRow, unknown> {
  protected readonly resource = `${BASE}/openings`;
  detail(id: string): Observable<OpeningDto> { return this.http.get<OpeningDto>(`${this.url}/${id}`); }
}

@Injectable({ providedIn: 'root' })
export class CandidatesApi extends CrudApiService<CandidateRow, unknown> {
  protected readonly resource = `${BASE}/candidates`;
  detail(id: string): Observable<CandidateDto> { return this.http.get<CandidateDto>(`${this.url}/${id}`); }
  move(id: string, stage: string, notes?: string | null): Observable<CandidateDto> { return this.http.post<CandidateDto>(`${this.url}/${id}/move`, { stage, notes: notes ?? null }); }
  hire(id: string, body: unknown): Observable<{ employeeId: string; employeeNumber: string; tasksCreated: number }> {
    return this.http.post<{ employeeId: string; employeeNumber: string; tasksCreated: number }>(`${this.url}/${id}/hire`, body);
  }
}
