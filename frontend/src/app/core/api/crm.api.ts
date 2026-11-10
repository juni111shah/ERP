import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, switchMap } from 'rxjs';
import { API_BASE_URL } from './api-config';
import { CrudApiService } from './crud-api.service';

export type LeadSource = 'website' | 'referral' | 'tradeShow' | 'coldCall' | 'campaign' | 'other';
export type LeadStatus = 'new' | 'contacted' | 'qualified' | 'unqualified' | 'converted';
export type OpportunityStage = 'qualification' | 'proposal' | 'negotiation' | 'closedWon' | 'closedLost';
export type InteractionType = 'call' | 'meeting' | 'email' | 'note' | 'task';

export interface LeadDto {
  id: string;
  legalEntityId: string;
  leadNumber: string;
  fullName: string;
  companyName?: string | null;
  jobTitle?: string | null;
  email?: string | null;
  phone?: string | null;
  source: LeadSource;
  status: LeadStatus;
  estimatedValue: number;
  assignedUserId?: string | null;
  assignedUserName?: string | null;
  notes?: string | null;
  convertedPartyId?: string | null;
  convertedOpportunityId?: string | null;
  convertedAt?: string | null;
  customFields?: string | null;
  createdAt: string;
}

export interface LeadSummaryDto {
  id: string;
  legalEntityId: string;
  leadNumber: string;
  fullName: string;
  companyName?: string | null;
  jobTitle?: string | null;
  email?: string | null;
  phone?: string | null;
  source: LeadSource;
  status: LeadStatus;
  estimatedValue: number;
  assignedUserName?: string | null;
  notes?: string | null;
  createdAt: string;
}

export interface LeadInput {
  legalEntityId: string;
  fullName: string;
  companyName?: string | null;
  jobTitle?: string | null;
  email?: string | null;
  phone?: string | null;
  source: LeadSource;
  estimatedValue: number;
  assignedUserId?: string | null;
  assignedUserName?: string | null;
  notes?: string | null;
  customFields?: string | null;
}

export interface ConvertLeadInput {
  opportunityTitle: string;
  expectedRevenue: number;
  expectedCloseDate?: string | null;
}

export interface OpportunityDto {
  id: string;
  legalEntityId: string;
  opportunityNumber: string;
  title: string;
  customerPartyId?: string | null;
  customerName: string;
  stage: OpportunityStage;
  probabilityPercent: number;
  expectedRevenue: number;
  actualRevenue: number;
  expectedCloseDate?: string | null;
  actualCloseDate?: string | null;
  closeReason?: string | null;
  leadId?: string | null;
  assignedUserId?: string | null;
  assignedUserName?: string | null;
  notes?: string | null;
  customFields?: string | null;
  createdAt: string;
}

export interface OpportunitySummaryDto {
  id: string;
  legalEntityId: string;
  opportunityNumber: string;
  title: string;
  customerName: string;
  stage: OpportunityStage;
  probabilityPercent: number;
  expectedRevenue: number;
  expectedCloseDate?: string | null;
  assignedUserName?: string | null;
  createdAt: string;
}

export interface OpportunityInput {
  legalEntityId: string;
  title: string;
  customerPartyId?: string | null;
  customerName: string;
  stage: OpportunityStage;
  probabilityPercent: number;
  expectedRevenue: number;
  expectedCloseDate?: string | null;
  assignedUserId?: string | null;
  assignedUserName?: string | null;
  notes?: string | null;
  customFields?: string | null;
}

export interface PipelineStageSummaryDto {
  stage: OpportunityStage;
  stageName: string;
  count: number;
  totalExpectedRevenue: number;
}

export interface CustomerInteractionDto {
  id: string;
  legalEntityId: string;
  relatedEntityKind: string;
  relatedEntityId: string;
  type: InteractionType;
  subject: string;
  details?: string | null;
  interactionDate: string;
  performedByUserId?: string | null;
  performedByName?: string | null;
  followUpDate?: string | null;
  isResolved: boolean;
  customFields?: string | null;
  createdAt: string;
}

export interface CustomerInteractionInput {
  legalEntityId: string;
  relatedEntityKind: string;
  relatedEntityId: string;
  type: InteractionType;
  subject: string;
  details?: string | null;
  interactionDate?: string | null;
  performedByUserId?: string | null;
  performedByName?: string | null;
  followUpDate?: string | null;
  isResolved: boolean;
  customFields?: string | null;
}

export interface CrmKpisDto {
  totalLeads: number;
  qualifiedLeads: number;
  leadConversionRatePercentage: number;
  openOpportunitiesCount: number;
  openPipelineValue: number;
  wonOpportunitiesCount: number;
  wonOpportunitiesRevenue: number;
  winRatePercentage: number;
}

/** Leads: CRUD, status changes and conversion to a deal. */
@Injectable({ providedIn: 'root' })
export class CrmLeadsApi extends CrudApiService<LeadSummaryDto, Partial<LeadInput>> {
  protected readonly resource = 'm/crm/leads';

  getDetail(id: string): Observable<LeadDto> {
    return this.http.get<LeadDto>(`${this.url}/${id}`);
  }

  /** The API has no status endpoint: it updates the whole lead, so this sends the current record with the new status. */
  setStatus(id: string, status: LeadStatus): Observable<LeadDto> {
    return this.getDetail(id).pipe(switchMap((lead) => this.http.put<LeadDto>(`${this.url}/${id}`, { ...lead, id, status })));
  }

  /** Returns the id of the new opportunity. */
  convert(id: string, payload: ConvertLeadInput): Observable<string> {
    return this.http.post<string>(`${this.url}/${id}/convert`, { id, ...payload });
  }
}

/** Deals (opportunities): CRUD, stage moves, closing and the pipeline summary. */
@Injectable({ providedIn: 'root' })
export class CrmOpportunitiesApi extends CrudApiService<OpportunitySummaryDto, Partial<OpportunityInput>> {
  protected readonly resource = 'm/crm/opportunities';

  getDetail(id: string): Observable<OpportunityDto> {
    return this.http.get<OpportunityDto>(`${this.url}/${id}`);
  }

  updateStage(id: string, stage: OpportunityStage, probabilityPercent: number): Observable<OpportunityDto> {
    return this.http.post<OpportunityDto>(`${this.url}/${id}/stage`, { id, stage, probabilityPercent });
  }

  closeDeal(id: string, isWon: boolean, actualRevenue: number, closeReason?: string): Observable<OpportunityDto> {
    return this.http.post<OpportunityDto>(`${this.url}/${id}/close`, { id, isWon, actualRevenue, closeReason });
  }

  getPipelineSummary(legalEntityId?: string): Observable<PipelineStageSummaryDto[]> {
    const params = legalEntityId ? `?legalEntityId=${legalEntityId}` : '';
    return this.http.get<PipelineStageSummaryDto[]>(`${this.url}/pipeline-summary${params}`);
  }
}

/** Customer activities (calls, meetings, emails...) and resolving them. */
@Injectable({ providedIn: 'root' })
export class CrmInteractionsApi extends CrudApiService<CustomerInteractionDto, Partial<CustomerInteractionInput>> {
  protected readonly resource = 'm/crm/interactions';

  resolve(id: string): Observable<boolean> {
    return this.http.post<boolean>(`${this.url}/${id}/resolve`, {});
  }

  getByEntity(kind: string, id: string): Observable<CustomerInteractionDto[]> {
    return this.http.get<CustomerInteractionDto[]>(`${this.url}/by-entity?kind=${kind}&id=${id}`);
  }
}

/** The CRM KPI summary. */
@Injectable({ providedIn: 'root' })
export class CrmKpiApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  getKpis(legalEntityId?: string): Observable<CrmKpisDto> {
    const params = legalEntityId ? `?legalEntityId=${legalEntityId}` : '';
    return this.http.get<CrmKpisDto>(`${this.baseUrl}/m/crm/analytics/kpis${params}`);
  }
}
