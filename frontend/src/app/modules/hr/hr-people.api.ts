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
