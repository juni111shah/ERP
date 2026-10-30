import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api-config';
import { CrudApiService } from '../../core/api/crud-api.service';

const BASE = 'm/hr';
type CustomFields = Record<string, unknown>;

export interface EmployeeSummary { id: string; number: string; fullName: string; designation: string; department?: string; legalEntityId: string; joinDate: string; status: string; basicSalary: number; housingAllowance: number; transportAllowance: number; otherAllowances: number; grossMonthly: number; }
export interface EmployeeDto {
  id: string; number: string; legalEntityId: string; fullName: string; designation: string; department?: string; joinDate: string; terminationDate?: string; email?: string; phone?: string; nationality?: string;
  status: string; basicSalary: number; housingAllowance: number; transportAllowance: number; otherAllowances: number; recurringDeductions: number; iban?: string; labourCardNumber?: string; bankRoutingCode?: string; grossMonthly: number;
  serviceYears: number; gratuityAccrued: number; customFields: CustomFields;
}
export interface AttendanceRow {
  employeeId: string; employeeNumber: string; employeeName: string; designation: string; legalEntityId: string; recordId?: string; recorded: boolean; workingDays: number; daysPresent: number;
  unpaidAbsentDays: number; paidLeaveDays: number; overtimeHours: number; leavePaidDays?: number; leaveUnpaidDays?: number;
}
export interface PayrollSummary { id: string; number: string; legalEntityId: string; year: number; month: number; status: string; employees: number; grossEarnings: number; deductions: number; netPay: number; paidOn?: string; }
export interface PayrollLine {
  id: string; employeeId: string; employeeNumber?: string; employeeName: string; absentDays: number; overtimeHours: number; basic: number; allowances: number; overtime: number; absence: number;
  adjustment: number; adjustmentNote?: string; grossEarnings: number; deductions: number; netPay: number;
}
export interface PayrollDto extends PayrollSummary { plannedPayDate?: string; notes?: string; approvalRequestId?: string; accrualJournalId?: string; paymentJournalId?: string; lines: PayrollLine[]; }

export const EMPLOYEE_STATUSES = [{ value: 'active', label: 'Active' }, { value: 'onLeave', label: 'On leave' }, { value: 'terminated', label: 'Terminated' }];
export const MONTHS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];

@Injectable({ providedIn: 'root' })
export class EmployeesApi extends CrudApiService<EmployeeSummary, unknown> {
  protected readonly resource = `${BASE}/employees`;
  detail(id: string): Observable<EmployeeDto> { return this.http.get<EmployeeDto>(`${this.url}/${id}`); }
  save(id: string | null, body: unknown): Observable<EmployeeDto> { return id ? this.http.put<EmployeeDto>(`${this.url}/${id}`, body) : this.http.post<EmployeeDto>(this.url, body); }
}

@Injectable({ providedIn: 'root' })
export class AttendanceApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/${BASE}/attendance`;
  month(year: number, month: number): Observable<AttendanceRow[]> { return this.http.get<AttendanceRow[]>(this.url, { params: { year, month } }); }
  save(body: unknown): Observable<AttendanceRow> { return this.http.put<AttendanceRow>(this.url, body); }
  remove(recordId: string): Observable<void> { return this.http.delete<void>(`${this.url}/${recordId}`); }
}

@Injectable({ providedIn: 'root' })
export class PayrollApi extends CrudApiService<PayrollSummary, unknown> {
  protected readonly resource = `${BASE}/payroll`;
  detail(id: string): Observable<PayrollDto> { return this.http.get<PayrollDto>(`${this.url}/${id}`); }
  generate(body: unknown): Observable<PayrollDto> { return this.http.post<PayrollDto>(this.url, body); }
  recalculate(id: string): Observable<PayrollDto> { return this.http.post<PayrollDto>(`${this.url}/${id}/recalculate`, {}); }
  adjust(id: string, lineId: string, adjustment: number, note: string | null): Observable<PayrollDto> { return this.http.put<PayrollDto>(`${this.url}/${id}/lines/${lineId}`, { adjustment, note }); }
  post(id: string): Observable<PayrollDto> { return this.http.post<PayrollDto>(`${this.url}/${id}/post`, {}); }
  pay(id: string, bankAccountId: string, date: string): Observable<PayrollDto> { return this.http.post<PayrollDto>(`${this.url}/${id}/pay`, { bankAccountId, date }); }
  remove(id: string): Observable<void> { return this.http.delete<void>(`${this.url}/${id}`); }
  bankFile(id: string): Observable<string> { return this.http.get(`${this.url}/${id}/bank-file`, { responseType: 'text' }); }
  wpsFile(id: string): Observable<string> { return this.http.get(`${this.url}/${id}/wps-sif`, { responseType: 'text' }); }
}

// ---- Performance reviews -------------------------------------------------------------------------------------------------

export const REVIEW_CYCLE_STATUSES = [{ value: 'open', label: 'Open' }, { value: 'closed', label: 'Closed' }];
export const RATINGS = [1, 2, 3, 4, 5].map((n) => ({ value: n, label: ['1 - Poor', '2 - Below expectations', '3 - Meets expectations', '4 - Exceeds expectations', '5 - Outstanding'][n - 1] }));
export interface ReviewCycleDto { id: string; name: string; periodStart: string; periodEnd: string; status: string; }
export interface CompetencyDto { id?: string; name: string; score: number; notes?: string; }
export interface ReviewRow { id: string; employeeId: string; cycleId: string; reviewerName?: string; overallRating?: number; status: string; submittedAt?: string; }
export interface ReviewDto {
  id: string; employeeId: string; employeeName: string; cycleId: string; cycleName: string; reviewerName?: string; overallRating?: number; goals?: string; comments?: string; status: string;
  submittedAt?: string; acknowledgedAt?: string; competencies: CompetencyDto[];
}

@Injectable({ providedIn: 'root' })
export class ReviewCyclesApi extends CrudApiService<ReviewCycleDto, unknown> { protected readonly resource = `${BASE}/review-cycles`; }

@Injectable({ providedIn: 'root' })
export class ReviewsApi extends CrudApiService<ReviewRow, unknown> {
  protected readonly resource = `${BASE}/reviews`;
  detail(id: string): Observable<ReviewDto> { return this.http.get<ReviewDto>(`${this.url}/${id}`); }
  save(id: string | null, body: unknown): Observable<ReviewDto> { return id ? this.http.put<ReviewDto>(`${this.url}/${id}`, body) : this.http.post<ReviewDto>(this.url, body); }
  submit(id: string): Observable<ReviewDto> { return this.http.post<ReviewDto>(`${this.url}/${id}/submit`, {}); }
  acknowledge(id: string): Observable<ReviewDto> { return this.http.post<ReviewDto>(`${this.url}/${id}/acknowledge`, {}); }
}

// ---- Leave ---------------------------------------------------------------------------------------------------------------

export const LEAVE_STATUS_LABELS: Record<string, string> = { draft: 'Draft', submitted: 'Submitted', approved: 'Approved', rejected: 'Rejected', cancelled: 'Cancelled' };
export interface LeaveTypeDto { id: string; code: string; name: string; isPaid: boolean; annualDays: number; carryForwardCap: number; requiresApproval: boolean; isActive: boolean; }
export interface HolidayDto { id: string; date: string; name: string; }
export interface LeaveRequestRow { id: string; employeeId: string; employeeName: string; leaveTypeId: string; fromDate: string; toDate: string; days: number; status: string; reason?: string; }
export interface LeaveRequestDto extends LeaveRequestRow { leaveTypeName: string; isPaid: boolean; submittedAt?: string; decidedAt?: string; decisionComment?: string; }
export interface LeaveBalanceRow { employeeId: string; employeeNumber: string; employeeName: string; leaveTypeId: string; leaveTypeCode: string; leaveTypeName: string; year: number; entitled: number; carried: number; taken: number; pending: number; remaining: number; }

@Injectable({ providedIn: 'root' })
export class LeaveTypesApi extends CrudApiService<LeaveTypeDto, unknown> { protected readonly resource = `${BASE}/leave-types`; }

@Injectable({ providedIn: 'root' })
export class HolidaysApi extends CrudApiService<HolidayDto, unknown> { protected readonly resource = `${BASE}/holidays`; }

@Injectable({ providedIn: 'root' })
export class LeaveApi extends CrudApiService<LeaveRequestRow, unknown> {
  protected readonly resource = `${BASE}/leave`;
  detail(id: string): Observable<LeaveRequestDto> { return this.http.get<LeaveRequestDto>(`${this.url}/${id}`); }
  save(id: string | null, body: unknown): Observable<LeaveRequestDto> { return id ? this.http.put<LeaveRequestDto>(`${this.url}/${id}`, body) : this.http.post<LeaveRequestDto>(this.url, body); }
  action(id: string, name: 'submit' | 'cancel' | 'approve' | 'reject', comment?: string): Observable<LeaveRequestDto> { return this.http.post<LeaveRequestDto>(`${this.url}/${id}/${name}`, { comment }); }
  days(from: string, to: string): Observable<{ workingDays: number }> { return this.http.get<{ workingDays: number }>(`${this.url}/days`, { params: { from, to } }); }
  balances(year: number): Observable<LeaveBalanceRow[]> { return this.http.get<LeaveBalanceRow[]>(`${this.url}/balances`, { params: { year } }); }
}
