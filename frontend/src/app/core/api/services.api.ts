import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-config';
import { CrudApiService } from './crud-api.service';

export type EngagementType = 'FixedPrice' | 'TimeAndMaterials' | 'Retainer';
export type EngagementStatus = 'Draft' | 'Active' | 'OnHold' | 'Completed' | 'Cancelled';
export type RetainerCycle = 'Monthly' | 'Quarterly' | 'Annual';
export type RetainerStatus = 'Active' | 'Paused' | 'Terminated';
export type MilestoneStatus = 'Planned' | 'ReadyToInvoice' | 'Invoiced' | 'Paid';

export interface EngagementDto {
  id: string;
  legalEntityId: string;
  engagementCode: string;
  title: string;
  clientId: string;
  clientName: string;
  type: EngagementType;
  status: EngagementStatus;
  contractValue: number;
  budgetedHours: number;
  consumedHours: number;
  consumedCost: number;
  startDate: string;
  targetEndDate?: string | null;
  actualEndDate?: string | null;
  leadConsultantUserId?: string | null;
  leadConsultantName?: string | null;
  description?: string | null;
  customFields?: string | null;
  createdAt: string;
}

export interface EngagementSummaryDto {
  id: string;
  legalEntityId: string;
  engagementCode: string;
  title: string;
  clientId: string;
  clientName: string;
  type: EngagementType;
  status: EngagementStatus;
  contractValue: number;
  budgetedHours: number;
  consumedHours: number;
  startDate: string;
  targetEndDate?: string | null;
  leadConsultantName?: string | null;
  createdAt: string;
}

export interface EngagementInput {
  legalEntityId: string;
  title: string;
  clientId: string;
  clientName: string;
  type: EngagementType;
  contractValue: number;
  budgetedHours: number;
  startDate: string;
  targetEndDate?: string | null;
  leadConsultantUserId?: string | null;
  leadConsultantName?: string | null;
  description?: string | null;
  customFields?: string | null;
}

export interface RetainerDto {
  id: string;
  legalEntityId: string;
  retainerNumber: string;
  engagementId: string;
  clientId: string;
  clientName: string;
  title: string;
  cycle: RetainerCycle;
  status: RetainerStatus;
  recurringFee: number;
  includedHours: number;
  overageHourlyRate: number;
  startDate: string;
  endDate?: string | null;
  terms?: string | null;
  customFields?: string | null;
  createdAt: string;
}

export interface RetainerSummaryDto {
  id: string;
  legalEntityId: string;
  retainerNumber: string;
  engagementId: string;
  clientName: string;
  title: string;
  cycle: RetainerCycle;
  status: RetainerStatus;
  recurringFee: number;
  includedHours: number;
  startDate: string;
  createdAt: string;
}

export interface RetainerInput {
  legalEntityId: string;
  engagementId: string;
  clientId: string;
  clientName: string;
  title: string;
  cycle: RetainerCycle;
  recurringFee: number;
  includedHours: number;
  overageHourlyRate: number;
  startDate: string;
  endDate?: string | null;
  terms?: string | null;
  customFields?: string | null;
}

export interface MilestoneDto {
  id: string;
  legalEntityId: string;
  milestoneCode: string;
  engagementId: string;
  title: string;
  amount: number;
  dueDate: string;
  completionDate?: string | null;
  status: MilestoneStatus;
  invoiceId?: string | null;
  notes?: string | null;
  customFields?: string | null;
  createdAt: string;
}

export interface MilestoneSummaryDto {
  id: string;
  legalEntityId: string;
  milestoneCode: string;
  engagementId: string;
  title: string;
  amount: number;
  dueDate: string;
  completionDate?: string | null;
  status: MilestoneStatus;
  createdAt: string;
}

export interface MilestoneInput {
  legalEntityId: string;
  engagementId: string;
  title: string;
  amount: number;
  dueDate: string;
  notes?: string | null;
  customFields?: string | null;
}

export interface TimeEntryDto {
  id: string;
  legalEntityId: string;
  engagementId: string;
  consultantUserId: string;
  consultantName: string;
  activityDate: string;
  hours: number;
  hourlyRate: number;
  totalAmount: number;
  taskDescription: string;
  isBillable: boolean;
  isApproved: boolean;
  approvedByUserId?: string | null;
  approvedAt?: string | null;
  customFields?: string | null;
  createdAt: string;
}

export interface TimeEntrySummaryDto {
  id: string;
  legalEntityId: string;
  engagementId: string;
  consultantName: string;
  activityDate: string;
  hours: number;
  hourlyRate: number;
  totalAmount: number;
  taskDescription: string;
  isBillable: boolean;
  isApproved: boolean;
  createdAt: string;
}

export interface TimeEntryInput {
  legalEntityId: string;
  engagementId: string;
  consultantUserId: string;
  consultantName: string;
  activityDate: string;
  hours: number;
  hourlyRate: number;
  taskDescription: string;
  isBillable: boolean;
  customFields?: string | null;
}

export interface ServicesKpisDto {
  activeEngagements: number;
  totalContractValue: number;
  retainerMonthlyRecurringRevenue: number;
  pendingMilestonesValue: number;
  readyToInvoiceMilestonesValue: number;
  totalLoggedHours: number;
  billableHours: number;
  billableUtilizationRate: number;
}

@Injectable({ providedIn: 'root' })
export class ServicesEngagementsApi extends CrudApiService<EngagementSummaryDto, EngagementInput> {
  protected readonly resource = 'm/services/engagements';

  getEngagement(id: string): Observable<EngagementDto> {
    return this.http.get<EngagementDto>(`${this.url}/${id}`);
  }

  updateStatus(id: string, status: EngagementStatus): Observable<EngagementDto> {
    return this.http.post<EngagementDto>(`${this.url}/${id}/status`, { id, status });
  }
}

@Injectable({ providedIn: 'root' })
export class ServicesRetainersApi extends CrudApiService<RetainerSummaryDto, RetainerInput> {
  protected readonly resource = 'm/services/retainers';

  getRetainer(id: string): Observable<RetainerDto> {
    return this.http.get<RetainerDto>(`${this.url}/${id}`);
  }

  updateStatus(id: string, status: RetainerStatus): Observable<RetainerDto> {
    return this.http.post<RetainerDto>(`${this.url}/${id}/status`, { id, status });
  }
}

@Injectable({ providedIn: 'root' })
export class ServicesMilestonesApi extends CrudApiService<MilestoneSummaryDto, MilestoneInput> {
  protected readonly resource = 'm/services/milestones';

  updateStatus(id: string, status: MilestoneStatus, invoiceId?: string | null): Observable<MilestoneDto> {
    return this.http.post<MilestoneDto>(`${this.url}/${id}/status`, { id, status, invoiceId });
  }

  invoice(id: string): Observable<unknown> {
    return this.http.post<unknown>(`${this.url}/${id}/invoice`, {});
  }
}

@Injectable({ providedIn: 'root' })
export class ServicesTimeEntriesApi extends CrudApiService<TimeEntrySummaryDto, TimeEntryInput> {
  protected readonly resource = 'm/services/time-entries';

  approve(id: string, approvedByUserId: string): Observable<TimeEntryDto> {
    return this.http.post<TimeEntryDto>(`${this.url}/${id}/approve`, { id, approvedByUserId });
  }
}

@Injectable({ providedIn: 'root' })
export class ServicesKpiApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  getKpis(legalEntityId: string): Observable<ServicesKpisDto> {
    return this.http.get<ServicesKpisDto>(`${this.baseUrl}/m/services/analytics/kpis?legalEntityId=${legalEntityId}`);
  }
}
