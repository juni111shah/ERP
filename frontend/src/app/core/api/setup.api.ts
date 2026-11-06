import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-config';

export interface CapabilityView { key: string; label: string; description: string; enabled: boolean; }
export interface BusinessProfile {
  industryCode: string; industryLabel?: string | null; industryGroup?: string | null; suggestedModules: string[]; missingModules: string[];
  capabilities: CapabilityView[]; units: string[]; itemCategories: string[]; dashboardFocus: string[];
}
export interface ChangeIndustryResult { industryCode: string; modulesEnabled: string[]; rolesAdded: string[]; accountsAdded: string[]; modulesNotAllowed: string[]; }
export interface NumberSequenceView { documentType: string; prefix: string; pattern: string; resetYearly: boolean; nextValue: number; example: string; }
export interface NumberSequenceInput { prefix: string; pattern: string; resetYearly: boolean; }

/** Business setup: what kind of business the workspace is, which core areas it uses, its lists and its numbering. */
@Injectable({ providedIn: 'root' })
export class SetupApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/setup`;

  profile(): Observable<BusinessProfile> { return this.http.get<BusinessProfile>(`${this.url}/profile`); }
  changeIndustry(industry: string): Observable<ChangeIndustryResult> { return this.http.post<ChangeIndustryResult>(`${this.url}/industry`, { industry }); }
  setCapabilities(disabled: string[]): Observable<BusinessProfile> { return this.http.put<BusinessProfile>(`${this.url}/capabilities`, { disabled }); }
  setLists(lists: { units?: string[]; itemCategories?: string[] }): Observable<BusinessProfile> { return this.http.put<BusinessProfile>(`${this.url}/lists`, lists); }
  sequences(): Observable<NumberSequenceView[]> { return this.http.get<NumberSequenceView[]>(`${this.url}/sequences`); }
  saveSequence(documentType: string, input: NumberSequenceInput): Observable<NumberSequenceView> {
    return this.http.put<NumberSequenceView>(`${this.url}/sequences/${encodeURIComponent(documentType)}`, input);
  }
}

export type OwnerFormat = 'money' | 'percent' | 'number' | string;
export interface OwnerCard { key: string; label: string; value: number; format: OwnerFormat; caption?: string | null; link?: string | null; }
export interface OwnerTable { columns: string[]; rows: string[][]; columnFormats?: string[] | null; }
export interface OwnerChart { categories: string[]; series: { name: string; values: number[] }[]; }
export interface OwnerSection { key: string; title: string; cards: OwnerCard[]; table?: OwnerTable | null; chart?: OwnerChart | null; note?: string | null; }
export interface OwnerDashboardDto { from: string; to: string; currency?: string | null; sections: OwnerSection[]; }

@Injectable({ providedIn: 'root' })
export class OwnerDashboardApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/dashboard/owner`;
  get(period: { from?: string; to?: string; legalEntityId?: string | null }): Observable<OwnerDashboardDto> {
    const params = Object.fromEntries(Object.entries(period).filter(([, v]) => !!v)) as Record<string, string>;
    return this.http.get<OwnerDashboardDto>(this.url, { params });
  }
}
