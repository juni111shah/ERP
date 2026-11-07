import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { CrudApiService } from './crud-api.service';

export type DepreciationMethod = 'straightLine' | 'decliningBalance';
export type AssetStatus = 'draft' | 'capitalised' | 'fullyDepreciated' | 'disposed';
export type CapitalisationSource = 'clearing' | 'bank' | 'vendor' | 'bill';

export const DEPRECIATION_METHODS = [{ value: 'straightLine', label: 'Straight line' }, { value: 'decliningBalance', label: 'Declining balance' }];
export const ASSET_STATUSES = [
  { value: 'draft', label: 'Draft' }, { value: 'capitalised', label: 'In service' }, { value: 'fullyDepreciated', label: 'Fully depreciated' }, { value: 'disposed', label: 'Disposed' },
];
export const CAPITALISATION_SOURCES = [
  { value: 'clearing', label: 'Clearing account' }, { value: 'bank', label: 'Bank or cash' }, { value: 'vendor', label: 'New vendor bill' }, { value: 'bill', label: 'Existing bill' },
];

export interface AssetCategoryDto {
  id: string; code: string; name: string; lifeMonths: number; method: DepreciationMethod; decliningRatePercent: number | null;
  assetAccountId: string; accumulatedDepreciationAccountId: string; depreciationExpenseAccountId: string; disposalGainLossAccountId: string | null; isActive: boolean;
}

export interface AssetDto {
  id: string; legalEntityId: string; number: string; name: string; description: string | null; categoryId: string; acquisitionDate: string; cost: number; salvageValue: number;
  method: DepreciationMethod; lifeMonths: number; decliningRatePercent: number | null; businessUnitId: string | null; custodian: string | null; status: AssetStatus;
  accumulatedDepreciation: number; netBookValue: number; capitalisedOn: string | null; capitalisationSource: CapitalisationSource | null; capitalisationJournalId: string | null;
  sourceBillId: string | null; sourceBillLineId: string | null; disposedOn: string | null; disposalProceeds: number | null; disposalGainLoss: number | null; disposalJournalId: string | null;
}

export interface AssetScheduleRow {
  year: number; month: number; periodEnd: string; amount: number; accumulated: number; netBookValue: number; posted: boolean; postedAmount: number | null; runId: string | null; journalEntryId: string | null;
}
export interface AssetScheduleDto {
  assetId: string; number: string; name: string; cost: number; salvageValue: number; method: DepreciationMethod; lifeMonths: number; status: AssetStatus; accumulatedDepreciation: number; rows: AssetScheduleRow[];
}

export interface DepreciationEntryDto { assetId: string; number: string; name: string; amount: number; accumulatedAfter: number; }
export interface DepreciationRunDto {
  id: string; legalEntityId: string; year: number; month: number; periodEnd: string; journalEntryId: string | null; total: number; assetCount: number; runAt: string; runBy: string | null;
  entries: DepreciationEntryDto[] | null; alreadyRun: boolean;
}

export interface AssetRegisterRow {
  assetId: string; legalEntityId: string; number: string; name: string; categoryId: string; categoryName: string; businessUnitId: string | null; custodian: string | null;
  acquisitionDate: string; capitalisedOn: string; cost: number; salvageValue: number; accumulatedDepreciation: number; netBookValue: number;
}
export interface AssetRegisterReport { asOf: string; rows: AssetRegisterRow[]; totalCost: number; totalAccumulatedDepreciation: number; totalNetBookValue: number; }

@Injectable({ providedIn: 'root' })
export class AssetCategoriesApi extends CrudApiService<AssetCategoryDto, unknown> { protected readonly resource = 'm/assets/categories'; }

@Injectable({ providedIn: 'root' })
export class AssetsApi extends CrudApiService<AssetDto, unknown> {
  protected readonly resource = 'm/assets/register';
  save(id: string | null, body: unknown): Observable<AssetDto> { return id ? this.http.put<AssetDto>(`${this.url}/${id}`, body) : this.http.post<AssetDto>(this.url, body); }
  capitalise(id: string, body: unknown): Observable<AssetDto> { return this.http.post<AssetDto>(`${this.url}/${id}/capitalise`, body); }
  dispose(id: string, body: unknown): Observable<AssetDto> { return this.http.post<AssetDto>(`${this.url}/${id}/dispose`, body); }
  schedule(id: string): Observable<AssetScheduleDto> { return this.http.get<AssetScheduleDto>(`${this.url}/${id}/schedule`); }
}

@Injectable({ providedIn: 'root' })
export class DepreciationRunsApi extends CrudApiService<DepreciationRunDto, unknown> {
  protected readonly resource = 'm/assets/depreciation-runs';
  run(body: { legalEntityId: string; year: number; month: number }): Observable<DepreciationRunDto> { return this.http.post<DepreciationRunDto>(this.url, body); }
}

@Injectable({ providedIn: 'root' })
export class AssetReportsApi extends CrudApiService<never, never> {
  protected readonly resource = 'm/assets/reports';
  register(asOf: string, legalEntityId?: string | null, categoryId?: string | null): Observable<AssetRegisterReport> {
    const params: Record<string, string> = { asOf };
    if (legalEntityId) params['legalEntityId'] = legalEntityId;
    if (categoryId) params['categoryId'] = categoryId;
    return this.http.get<AssetRegisterReport>(`${this.url}/register`, { params });
  }
}
