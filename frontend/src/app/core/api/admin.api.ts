import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { LookupOption } from '../custom-fields/lookup-registry';
import { API_BASE_URL } from './api-config';
import { CrudApiService } from './crud-api.service';

type CustomFields = Record<string, unknown>;

// ---- Models (mirror the ERP.Application DTOs; enums are camelCase strings) -------------------------------------------

export interface ItemDto { id: string; sku: string; name: string; type: string; uom: string; salesPrice?: number; purchasePrice?: number; taxCodeId?: string; isActive: boolean; customFields: CustomFields; }
export interface AccountDto {
  id: string; code: string; name: string; type: string; reportingGroup?: string; systemRole?: string; isBank: boolean; isSystem: boolean; isActive: boolean;
  parentId?: string; isPostable: boolean; currencyCode?: string; customFields: CustomFields;
}
export interface TaxCodeDto { id: string; code: string; name: string; kind: string; rate: number; isDefault: boolean; isRecoverable: boolean; isActive: boolean; }
export interface LegalEntityDto {
  id: string; code: string; name: string; legalName?: string; countryCode: string; baseCurrency: string; taxRegistrationNo?: string; isTaxRegistered: boolean;
  registrationNo?: string; address?: string; email?: string; phone?: string; booksLockedTo?: string; isActive: boolean; customFields: CustomFields;
}
export interface BusinessUnitDto { id: string; legalEntityId: string; code: string; name: string; address?: string; isActive: boolean; customFields: CustomFields; }
export interface RoleDto { id: string; name: string; description?: string; isBuiltIn: boolean; moduleKey?: string; permissions: string[]; }
export interface PermissionDefinition { key: string; module: string; resource: string; action: string; description: string; }
export interface UserSummary { id: string; email: string; fullName: string; isActive: boolean; lastSignInAt?: string; roles: string[]; roleIds: string[]; }
export interface SettingView { key: string; label: string; module: string; type: string; description?: string; value: unknown; }
export interface AuditEvent {
  id: string; occurredAt: string; userId?: string; action: string; recordType: string; recordId: string; changedFields?: string; before?: string; after?: string; ipAddress?: string;
}
export interface ApprovalDto {
  id: string; legalEntityId?: string; documentType: string; documentId?: string; subject: string; justification?: string; amount?: number; currencyCode?: string;
  requestedBy: string; requestedAt: string; status: string; decidedBy?: string; decidedAt?: string; decisionComment?: string;
}
export interface CountryDto { code: string; name: string; defaultCurrency: string; defaultTimeZone: string; hasPack: boolean; }
export interface CurrencyOption { code: string; name: string; symbol?: string; decimals: number; }

// ---- Option lists -------------------------------------------------------------------------------------------------------

export const ITEM_TYPES = [{ value: 'product', label: 'Product' }, { value: 'service', label: 'Service' }];
export const ACCOUNT_TYPES = [
  { value: 'asset', label: 'Asset' }, { value: 'liability', label: 'Liability' }, { value: 'equity', label: 'Equity' },
  { value: 'income', label: 'Income' }, { value: 'expense', label: 'Expense' },
];
export const TAX_KINDS = [
  { value: 'vat', label: 'VAT' }, { value: 'gst', label: 'GST' }, { value: 'salesTax', label: 'Sales tax' },
  { value: 'withholding', label: 'Withholding' }, { value: 'excise', label: 'Excise' }, { value: 'other', label: 'Other' },
];

export const yesNo = (v: unknown): string => (v ? 'Yes' : 'No');

// ---- API clients (one line per resource) ----------------------------------------------------------------------------------

@Injectable({ providedIn: 'root' })
export class ItemsApi extends CrudApiService<ItemDto, unknown> { protected readonly resource = 'items'; }

@Injectable({ providedIn: 'root' })
export class AccountsApi extends CrudApiService<AccountDto, unknown> { protected readonly resource = 'finance/accounts'; }

@Injectable({ providedIn: 'root' })
export class TaxCodesApi extends CrudApiService<TaxCodeDto, unknown> { protected readonly resource = 'finance/tax-codes'; }

@Injectable({ providedIn: 'root' })
export class LegalEntitiesApi extends CrudApiService<LegalEntityDto, unknown> {
  protected readonly resource = 'legal-entities';
  lookup = (term: string): Observable<LookupOption[]> =>
    this.list({ page: 1, pageSize: 20, search: term }).pipe(map((r) => r.items.map((e) => ({ value: e.id, label: `${e.code} · ${e.name}` }))));
}

@Injectable({ providedIn: 'root' })
export class BusinessUnitsApi extends CrudApiService<BusinessUnitDto, unknown> { protected readonly resource = 'business-units'; }

@Injectable({ providedIn: 'root' })
export class RolesApi extends CrudApiService<RoleDto, unknown> {
  protected readonly resource = 'roles';
  permissions(): Observable<PermissionDefinition[]> { return this.http.get<PermissionDefinition[]>(`${this.baseUrl}/permissions`); }
  lookup = (term: string): Observable<LookupOption[]> =>
    this.list({ page: 1, pageSize: 50, search: term }).pipe(map((r) => r.items.map((x) => ({ value: x.id, label: x.name }))));
}

@Injectable({ providedIn: 'root' })
export class AuditApi extends CrudApiService<AuditEvent, never> { protected readonly resource = 'audit'; }

/** Endpoints that are not plain paged resources. */
@Injectable({ providedIn: 'root' })
export class AdminApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  users(): Observable<UserSummary[]> { return this.http.get<UserSummary[]>(`${this.base}/users`); }
  inviteUser(body: unknown): Observable<unknown> { return this.http.post(`${this.base}/users`, body); }
  setUserRoles(id: string, roleIds: string[]): Observable<unknown> { return this.http.put(`${this.base}/users/${id}/roles`, { roleIds }); }
  setUserActive(id: string, isActive: boolean): Observable<unknown> { return this.http.post(`${this.base}/users/${id}/active`, { isActive }); }

  settings(): Observable<SettingView[]> { return this.http.get<SettingView[]>(`${this.base}/settings`); }
  saveSettings(values: Record<string, unknown>): Observable<unknown> { return this.http.put(`${this.base}/settings`, values); }

  approvals(scope?: string): Observable<ApprovalDto[]> { return this.http.get<ApprovalDto[]>(`${this.base}/approvals`, { params: scope ? { scope } : {} }); }
  submitApproval(body: unknown): Observable<ApprovalDto> { return this.http.post<ApprovalDto>(`${this.base}/approvals`, body); }
  decide(id: string, approve: boolean, comment?: string): Observable<ApprovalDto> {
    return this.http.post<ApprovalDto>(`${this.base}/approvals/${id}/${approve ? 'approve' : 'reject'}`, { comment: comment ?? null });
  }
  withdraw(id: string): Observable<ApprovalDto> { return this.http.post<ApprovalDto>(`${this.base}/approvals/${id}/withdraw`, {}); }

  countries(): Observable<CountryDto[]> { return this.http.get<CountryDto[]>(`${this.base}/platform/countries`); }
  currencies(): Observable<CurrencyOption[]> { return this.http.get<CurrencyOption[]>(`${this.base}/platform/currencies`); }
}

// ---- Ledger ------------------------------------------------------------------------------------------------------------------

export interface JournalSummary { id: string; number: string; legalEntityId: string; date: string; memo?: string; sourceType: string; status: string; amount: number; currencyCode: string; }
export interface JournalLineDto { id: string; seq: number; accountId: string; accountCode: string; accountName: string; debit: number; credit: number; description?: string; partyId?: string; }
export interface JournalDto {
  id: string; number: string; legalEntityId: string; date: string; memo?: string; reference?: string; sourceType: string; sourceId?: string; currencyCode: string;
  status: string; amount: number; reversalOfId?: string; postedAt?: string; lines: JournalLineDto[];
}
export interface TrialBalanceRow { accountId: string; code: string; name: string; type: string; reportingGroup?: string; opening: number; debit: number; credit: number; closing: number; }
export interface TrialBalance { from?: string; to: string; rows: TrialBalanceRow[]; totalDebit: number; totalCredit: number; balanced: boolean; }
export interface LedgerLine { journalId: string; number: string; date: string; memo?: string; description?: string; debit: number; credit: number; balance: number; }
export interface GeneralLedger { accountId: string; code: string; name: string; opening: number; lines: LedgerLine[]; closing: number; }
export interface StatementLine { group: string; code: string; name: string; amount: number; }
export interface IncomeStatement { from: string; to: string; income: StatementLine[]; expenses: StatementLine[]; totalIncome: number; totalExpenses: number; netProfit: number; }
export interface BalanceSheet {
  asOf: string; assets: StatementLine[]; liabilities: StatementLine[]; equity: StatementLine[]; totalAssets: number; totalLiabilities: number; totalEquity: number;
  currentEarnings: number; balanced: boolean;
}

@Injectable({ providedIn: 'root' })
export class JournalsApi extends CrudApiService<JournalSummary, unknown> {
  protected readonly resource = 'finance/journals';
  detail(id: string): Observable<JournalDto> { return this.http.get<JournalDto>(`${this.url}/${id}`); }
  save(id: string | null, body: unknown): Observable<JournalDto> { return id ? this.http.put<JournalDto>(`${this.url}/${id}`, body) : this.http.post<JournalDto>(this.url, body); }
  post(id: string): Observable<JournalDto> { return this.http.post<JournalDto>(`${this.url}/${id}/post`, {}); }
  reverse(id: string, memo?: string): Observable<JournalDto> { return this.http.post<JournalDto>(`${this.url}/${id}/reverse`, { memo: memo ?? null }); }
  lockBooks(entityId: string, through: string | null): Observable<unknown> { return this.http.put(`${this.baseUrl}/legal-entities/${entityId}/books-lock`, { through }); }
}

@Injectable({ providedIn: 'root' })
export class LedgerReportsApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${inject(API_BASE_URL)}/finance/reports`;
  private params(p: Record<string, string | undefined>): Record<string, string> { return Object.fromEntries(Object.entries(p).filter(([, v]) => !!v)) as Record<string, string>; }
  trialBalance(p: { from?: string; to: string; legalEntityId?: string }): Observable<TrialBalance> { return this.http.get<TrialBalance>(`${this.base}/trial-balance`, { params: this.params(p) }); }
  generalLedger(p: { accountId: string; from?: string; to: string; legalEntityId?: string }): Observable<GeneralLedger> { return this.http.get<GeneralLedger>(`${this.base}/general-ledger`, { params: this.params(p) }); }
  incomeStatement(p: { from: string; to: string; legalEntityId?: string }): Observable<IncomeStatement> { return this.http.get<IncomeStatement>(`${this.base}/income-statement`, { params: this.params(p) }); }
  balanceSheet(p: { asOf: string; legalEntityId?: string }): Observable<BalanceSheet> { return this.http.get<BalanceSheet>(`${this.base}/balance-sheet`, { params: this.params(p) }); }
}
