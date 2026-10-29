import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { LookupOption } from '../custom-fields/lookup-registry';
import { API_BASE_URL } from './api-config';
import { ListQuery, PagedResult } from './api.models';
import { CrudApiService, toHttpParams } from './crud-api.service';

type CustomFields = Record<string, unknown>;

// ---- Models (mirror the ERP.Application DTOs; enums are camelCase strings) -------------------------------------------

export interface ItemDto { id: string; sku: string; name: string; type: string; uom: string; salesPrice?: number; purchasePrice?: number; taxCodeId?: string; isActive: boolean; customFields: CustomFields; }
export interface AccountDto {
  id: string; code: string; name: string; type: string; reportingGroup?: string; systemRole?: string; isBank: boolean; isSystem: boolean; isActive: boolean;
  parentId?: string; isPostable: boolean; currencyCode?: string; customFields: CustomFields;
}
export interface AccountCategoryDto {
  type: string; accountCount: number; activeCount: number; primaryCount: number; hasChildren: boolean; balance?: number | null;
}
export interface AccountNodeDto {
  id: string; code: string; name: string; type: string; parentId?: string | null; reportingGroup?: string | null; systemRole?: string | null;
  isPostable: boolean; isBank: boolean; isSystem: boolean; isActive: boolean; currencyCode?: string | null; childCount: number; hasChildren: boolean; balance?: number | null;
  /** derived sub-category key within the type (e.g. 'current'); for a nested account the panels use its top same-type ancestor's */
  subcategory?: string | null;
}
/** A derived sub-category of an account type (Current assets, Non-current assets, …); key is unique within the type. */
export interface AccountSubcategoryDto {
  key: string; type: string; name: string; order: number; accountCount: number; activeCount: number; primaryCount: number; hasChildren: boolean; balance?: number | null;
}
export interface TaxCodeDto { id: string; code: string; name: string; kind: string; rate: number; isDefault: boolean; isRecoverable: boolean; isActive: boolean; }
export interface LegalEntityDto {
  id: string; code: string; name: string; legalName?: string; countryCode: string; baseCurrency: string; taxRegistrationNo?: string; isTaxRegistered: boolean;
  registrationNo?: string; address?: string; email?: string; phone?: string; booksLockedTo?: string; isActive: boolean; customFields: CustomFields;
}
export interface PartyGroupDto { id: string; code: string; name: string; isActive: boolean; }
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

export interface CoaNumbering { mode: 'free' | 'hierarchical'; segments: number[]; separator: string; pattern: string; maxLength: number; }

@Injectable({ providedIn: 'root' })
export class AccountsApi extends CrudApiService<AccountDto, unknown> {
  protected readonly resource = 'finance/accounts';

  // ---- Step-by-step chart of accounts: one level per request -------------------------------------------------------------
  /** The five account categories with counts (and natural-side balances when asked and the user may read reports). */
  categories(includeBalances = false): Observable<AccountCategoryDto[]> {
    return this.http.get<AccountCategoryDto[]>(`${this.url}/tree/categories`, { params: includeBalances ? { includeBalances: true } : {} });
  }
  /** Primary accounts of one category: no parent, or a parent of another type. Paged and searchable (code, name). */
  categoryAccounts(type: string, query: ListQuery): Observable<PagedResult<AccountNodeDto>> {
    return this.http.get<PagedResult<AccountNodeDto>>(`${this.url}/tree/categories/${encodeURIComponent(type)}/accounts`, { params: toHttpParams(query) });
  }
  /** The sub-categories of one category (always all of them, in order), with counts. */
  subcategories(type: string, includeBalances = false): Observable<AccountSubcategoryDto[]> {
    return this.http.get<AccountSubcategoryDto[]>(`${this.url}/tree/categories/${encodeURIComponent(type)}/subcategories`, { params: includeBalances ? { includeBalances: true } : {} });
  }
  /** Primary accounts of one sub-category. Paged and searchable (code, name). */
  subcategoryAccounts(type: string, key: string, query: ListQuery): Observable<PagedResult<AccountNodeDto>> {
    return this.http.get<PagedResult<AccountNodeDto>>(`${this.url}/tree/categories/${encodeURIComponent(type)}/subcategories/${encodeURIComponent(key)}/accounts`, { params: toHttpParams(query) });
  }
  /** Direct children of one account. */
  children(id: string, query: ListQuery): Observable<PagedResult<AccountNodeDto>> {
    return this.http.get<PagedResult<AccountNodeDto>>(`${this.url}/${id}/children`, { params: toHttpParams(query) });
  }
  /** Numbering mask of the chart of accounts (free or hierarchical). */
  numbering(): Observable<CoaNumbering> { return this.http.get<CoaNumbering>(`${this.baseUrl}/finance/coa-numbering`); }
  setNumbering(body: { mode: string; segments: number[]; separator: string }): Observable<CoaNumbering> { return this.http.put<CoaNumbering>(`${this.baseUrl}/finance/coa-numbering`, body); }
  /** Next free code under a parent (or the next top-level code of a type). Empty in free mode. */
  nextCode(parentId: string | null, type: string | null): Observable<{ code: string }> {
    const params: Record<string, string> = {};
    if (parentId) params['parentId'] = parentId;
    if (type) params['type'] = type;
    return this.http.get<{ code: string }>(`${this.url}/next-code`, { params });
  }
  /** Root-first ancestors of an account, the account itself last (breadcrumbs, re-opening a path). */
  path(id: string): Observable<AccountNodeDto[]> {
    return this.http.get<AccountNodeDto[]>(`${this.url}/${id}/path`);
  }
}

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
export class PartyGroupsApi extends CrudApiService<PartyGroupDto, unknown> { protected readonly resource = 'party-groups'; }

@Injectable({ providedIn: 'root' })
export class RolesApi extends CrudApiService<RoleDto, unknown> {
  protected readonly resource = 'roles';
  permissions(): Observable<PermissionDefinition[]> { return this.http.get<PermissionDefinition[]>(`${this.baseUrl}/permissions`); }
  lookup = (term: string): Observable<LookupOption[]> =>
    this.list({ page: 1, pageSize: 50, search: term }).pipe(map((r) => r.items.map((x) => ({ value: x.id, label: x.name }))));
}

@Injectable({ providedIn: 'root' })
export class AuditApi extends CrudApiService<AuditEvent, never> { protected readonly resource = 'audit'; }

export interface MessageLogRow { id: string; channel: string; recipient: string; subject: string; body?: string; link?: string; status: string; error?: string; sentAt: string; attempts?: number; }
@Injectable({ providedIn: 'root' })
export class MessageLogApi extends CrudApiService<MessageLogRow, never> {
  protected readonly resource = 'messages';
  sendTestEmail(to: string): Observable<{ status: string; error?: string }> { return this.http.post<{ status: string; error?: string }>(`${this.url}/test-email`, { to }); }
  sendReminders(): Observable<{ sent: number }> { return this.http.post<{ sent: number }>(`${this.url}/send-reminders`, {}); }
}

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
  arAgeing(p: { asOf: string; legalEntityId?: string; businessUnitId?: string; sourceType?: string; groupBy?: string; partyGroupId?: string }): Observable<ArAgeing> { return this.http.get<ArAgeing>(`${this.base}/ar-ageing`, { params: this.params(p) }); }
  apAgeing(p: { asOf: string; legalEntityId?: string }): Observable<ArAgeing> { return this.http.get<ArAgeing>(`${this.base}/ap-ageing`, { params: this.params(p) }); }
}

// ---- Receivables -------------------------------------------------------------------------------------------------------------

export interface InvoiceLine { id?: string; seq?: number; itemId?: string; description: string; quantity: number; price: number; taxCodeId?: string; taxAmount?: number; net?: number; accountId?: string; }
export interface InvoiceSummary { id: string; number: string; isCreditNote: boolean; partyId: string; partyName?: string; invoiceDate: string; dueDate: string; total: number; paid: number; outstanding: number; status: string; currencyCode: string; net?: number; tax?: number; sourceType?: string | null; }
export interface InvoiceDto extends InvoiceSummary { legalEntityId: string; net: number; tax: number; sourceType?: string | null; sourceId?: string; journalEntryId?: string; notes?: string; lines: InvoiceLine[]; warnings?: string[]; }
export interface AllocationLine { id: string; invoiceId: string; invoiceNumber: string; amount: number; }
export interface ReceiptSummary { id: string; number: string; partyId: string; partyName?: string; date: string; method: string; amount: number; unallocated: number; status: string; currencyCode: string; }
export interface ReceiptDto extends ReceiptSummary { legalEntityId: string; bankAccountId?: string; reference?: string; journalEntryId?: string; allocations: AllocationLine[]; }
export interface AgeingRow { partyId: string; partyName?: string; current: number; days1To30: number; days31To60: number; days61To90: number; over90: number; total: number; }
export interface ArAgeing { asOf: string; rows: AgeingRow[]; totals: AgeingRow; }
export interface BillingResult { invoiced: number; totalBilled: number; invoiceIds: string[]; }

export const SETTLEMENT_METHODS = [
  { value: 'cash', label: 'Cash' }, { value: 'bankTransfer', label: 'Bank transfer' }, { value: 'card', label: 'Card' }, { value: 'other', label: 'Other' },
];

@Injectable({ providedIn: 'root' })
export class InvoicesApi extends CrudApiService<InvoiceSummary, unknown> {
  protected readonly resource = 'finance/invoices';
  detail(id: string): Observable<InvoiceDto> { return this.http.get<InvoiceDto>(`${this.url}/${id}`); }
  save(id: string | null, body: unknown): Observable<InvoiceDto> { return id ? this.http.put<InvoiceDto>(`${this.url}/${id}`, body) : this.http.post<InvoiceDto>(this.url, body); }
  issue(id: string): Observable<InvoiceDto> { return this.http.post<InvoiceDto>(`${this.url}/${id}/issue`, {}); }
  /** The API requires a reason (3–500 characters); it is stored in the audit log and the reversal memo. */
  void(id: string, reason: string): Observable<InvoiceDto> { return this.http.post<InvoiceDto>(`${this.url}/${id}/void`, { reason }); }
  /** Draft credit note linked to this invoice (all lines at full quantity unless lines are given). */
  creditNote(id: string, body: { date?: string; lines?: { invoiceLineId: string; quantity: number }[]; notes?: string } = {}): Observable<InvoiceDto> { return this.http.post<InvoiceDto>(`${this.url}/${id}/credit-note`, body); }
  applyCredit(creditId: string, invoiceId: string, amount: number): Observable<InvoiceDto> { return this.http.post<InvoiceDto>(`${this.url}/${creditId}/apply-credit`, { invoiceId, amount }); }
  remove(id: string): Observable<void> { return this.http.delete<void>(`${this.url}/${id}`); }
}

@Injectable({ providedIn: 'root' })
export class ReceiptsApi extends CrudApiService<ReceiptSummary, unknown> {
  protected readonly resource = 'finance/receipts';
  detail(id: string): Observable<ReceiptDto> { return this.http.get<ReceiptDto>(`${this.url}/${id}`); }
  allocate(id: string, allocations: { invoiceId: string; amount: number }[]): Observable<ReceiptDto> { return this.http.post<ReceiptDto>(`${this.url}/${id}/allocate`, { allocations }); }
  /** The API requires a reason (3–500 characters); it is stored in the audit log and the reversal memo. */
  void(id: string, reason: string): Observable<ReceiptDto> { return this.http.post<ReceiptDto>(`${this.url}/${id}/void`, { reason }); }
}

// ---- Cheque register ---------------------------------------------------------------------------------------------------------

export interface ChequeRow { id: string; number: string; kind: string; direction: string; instrumentNo: string; bankName?: string; partyId?: string; partyName?: string; amount: number; instrumentDate: string; status: string; currencyCode: string; bouncedOn?: string; followUpOn?: string; }
export interface ChequeDto extends ChequeRow {
  legalEntityId: string; receivedOn?: string; bankAccountId?: string; settlementId?: string; depositedOn?: string; clearedOn?: string; bounceReason?: string; replacesInstrumentId?: string; notes?: string; bouncedOn?: string; followUpOn?: string; followUpNote?: string; replacedByChequeId?: string;
  allocations: AllocationLine[];
}
export interface ChequeSummary { inCustodyTotal: number; inCustodyCount: number; maturedTotal: number; maturedCount: number; next7DaysTotal: number; next30DaysTotal: number; depositedTotal: number; bouncedCount: number; }
export const CHEQUE_KINDS = [
  { value: 'cheque', label: 'Cheque' }, { value: 'postDatedCheque', label: 'Post-dated cheque' }, { value: 'bankDraft', label: 'Bank draft' }, { value: 'promissoryNote', label: 'Promissory note' },
];

@Injectable({ providedIn: 'root' })
export class ChequesApi extends CrudApiService<ChequeRow, unknown> {
  protected readonly resource = 'finance/cheques';
  detail(id: string): Observable<ChequeDto> { return this.http.get<ChequeDto>(`${this.url}/${id}`); }
  summary(): Observable<ChequeSummary> { return this.http.get<ChequeSummary>(`${this.url}/summary`); }
  register(body: unknown): Observable<ChequeDto> { return this.http.post<ChequeDto>(this.url, body); }
  cancel(id: string): Observable<ChequeDto> { return this.http.post<ChequeDto>(`${this.url}/${id}/cancel`, {}); }
  deposit(id: string, bankAccountId: string, date: string): Observable<ChequeDto> { return this.http.post<ChequeDto>(`${this.url}/${id}/deposit`, { bankAccountId, date }); }
  clear(id: string, date: string): Observable<ChequeDto> { return this.http.post<ChequeDto>(`${this.url}/${id}/clear`, { date }); }
  followUp(id: string, note: string, date: string): Observable<ChequeDto> { return this.http.post<ChequeDto>(`${this.url}/${id}/follow-up`, { note, date }); }
  bounce(id: string, date: string, reason: string): Observable<ChequeDto> { return this.http.post<ChequeDto>(`${this.url}/${id}/bounce`, { date, reason }); }
  replace(id: string, body: unknown): Observable<ChequeDto> { return this.http.post<ChequeDto>(`${this.url}/${id}/replace`, body); }
}

// ---- Bank and cash -----------------------------------------------------------------------------------------------------------

export interface CashPositionRow { accountId: string; code: string; name: string; isBank: boolean; balance: number; }
export interface CashPosition { asOf: string; accounts: CashPositionRow[]; total: number; }
export interface StatementLine { id: string; accountId: string; date: string; description: string; reference?: string; amount: number; status: string; matchedJournalLineId?: string; }
export interface LedgerItem { journalLineId: string; journalId: string; number: string; date: string; description?: string; amount: number; }
export interface Reconciliation {
  accountId: string; code: string; name: string; asOf: string; ledgerBalance: number; matched: number; unmatchedStatement: StatementLine[]; unmatchedLedger: LedgerItem[];
  unmatchedStatementTotal: number; unmatchedLedgerTotal: number; reconciled: boolean;
}

@Injectable({ providedIn: 'root' })
export class BankApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${inject(API_BASE_URL)}/finance/bank`;
  position(asOf: string, legalEntityId?: string): Observable<CashPosition> { return this.http.get<CashPosition>(`${this.base}/position`, { params: { asOf, ...(legalEntityId ? { legalEntityId } : {}) } }); }
  reconciliation(accountId: string, asOf: string, legalEntityId?: string): Observable<Reconciliation> {
    return this.http.get<Reconciliation>(`${this.base}/accounts/${accountId}/reconciliation`, { params: { asOf, ...(legalEntityId ? { legalEntityId } : {}) } });
  }
  import(accountId: string, legalEntityId: string, lines: { date: string; description: string; reference: string | null; amount: number }[]): Observable<{ imported: number; duplicates: number }> {
    return this.http.post<{ imported: number; duplicates: number }>(`${this.base}/accounts/${accountId}/import`, { legalEntityId, lines });
  }
  autoMatch(accountId: string, legalEntityId: string): Observable<{ matched: number }> { return this.http.post<{ matched: number }>(`${this.base}/accounts/${accountId}/auto-match`, { legalEntityId }); }
  match(lineId: string, journalLineId: string): Observable<StatementLine> { return this.http.post<StatementLine>(`${this.base}/statement-lines/${lineId}/match`, { journalLineId }); }
  unmatch(lineId: string): Observable<StatementLine> { return this.http.post<StatementLine>(`${this.base}/statement-lines/${lineId}/unmatch`, {}); }
  book(lineId: string, counterAccountId: string, memo?: string): Observable<StatementLine> { return this.http.post<StatementLine>(`${this.base}/statement-lines/${lineId}/book`, { counterAccountId, memo: memo ?? null }); }
  transfer(body: unknown): Observable<unknown> { return this.http.post(`${this.base}/transfers`, body); }
}

// ---- Payables ----------------------------------------------------------------------------------------------------------------

export interface BillLine { id?: string; seq?: number; description: string; quantity: number; price: number; taxCodeId?: string; taxAmount?: number; taxRecoverable?: boolean; net?: number; accountId?: string; }
export interface BillSummary { id: string; number: string; partyId: string; partyName?: string; vendorReference?: string; billDate: string; dueDate: string; total: number; paid: number; outstanding: number; status: string; currencyCode: string; }
export interface BillDto extends BillSummary { legalEntityId: string; net: number; tax: number; approvalRequestId?: string; journalEntryId?: string; notes?: string; lines: BillLine[]; }
export interface PaymentSummary { id: string; number: string; partyId: string; partyName?: string; date: string; method: string; amount: number; unallocated: number; status: string; currencyCode: string; }
export interface PaymentDto extends PaymentSummary { legalEntityId: string; bankAccountId?: string; reference?: string; paymentInstrumentId?: string; allocations: { id: string; billId: string; billNumber: string; amount: number }[]; }
export interface ApprovalPolicy { id: string; documentType: string; minAmount?: number; approverPermission: string; isActive: boolean; }
export const PAYMENT_METHODS = [...SETTLEMENT_METHODS, { value: 'instrument', label: 'Cheque issued' }];

@Injectable({ providedIn: 'root' })
export class BillsApi extends CrudApiService<BillSummary, unknown> {
  protected readonly resource = 'finance/bills';
  detail(id: string): Observable<BillDto> { return this.http.get<BillDto>(`${this.url}/${id}`); }
  save(id: string | null, body: unknown): Observable<BillDto> { return id ? this.http.put<BillDto>(`${this.url}/${id}`, body) : this.http.post<BillDto>(this.url, body); }
  post(id: string): Observable<BillDto> { return this.http.post<BillDto>(`${this.url}/${id}/post`, {}); }
  /** The API requires a reason (3–500 characters); it is stored in the audit log and the reversal memo. */
  void(id: string, reason: string): Observable<BillDto> { return this.http.post<BillDto>(`${this.url}/${id}/void`, { reason }); }
}

@Injectable({ providedIn: 'root' })
export class PaymentsApi extends CrudApiService<PaymentSummary, unknown> {
  protected readonly resource = 'finance/payments';
  detail(id: string): Observable<PaymentDto> { return this.http.get<PaymentDto>(`${this.url}/${id}`); }
  allocate(id: string, allocations: { billId: string; amount: number }[]): Observable<PaymentDto> { return this.http.post<PaymentDto>(`${this.url}/${id}/allocate`, { allocations }); }
  /** The API requires a reason (3–500 characters); it is stored in the audit log and the reversal memo. */
  void(id: string, reason: string): Observable<PaymentDto> { return this.http.post<PaymentDto>(`${this.url}/${id}/void`, { reason }); }
  clearCheque(instrumentId: string, date: string): Observable<PaymentDto> { return this.http.post<PaymentDto>(`${this.baseUrl}/finance/cheques/${instrumentId}/clear-issued`, { date }); }
}

@Injectable({ providedIn: 'root' })
export class ApprovalPoliciesApi {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/approval-policies`;
  list(): Observable<ApprovalPolicy[]> { return this.http.get<ApprovalPolicy[]>(this.url); }
  save(id: string | null, body: unknown): Observable<ApprovalPolicy> { return id ? this.http.put<ApprovalPolicy>(`${this.url}/${id}`, body) : this.http.post<ApprovalPolicy>(this.url, body); }
  remove(id: string): Observable<void> { return this.http.delete<void>(`${this.url}/${id}`); }
}

// ---- Dashboard ---------------------------------------------------------------------------------------------------------------

export interface KpiCard { key: string; label: string; value: number; format: 'money' | 'percent' | 'number'; caption?: string; order: number; link?: string; }
export interface ChartSeriesDto { name: string; values: number[]; }
export interface DashboardChart { key: string; title: string; kind: 'bar' | 'stacked' | 'hbar'; categories: string[]; series: ChartSeriesDto[]; subtitle?: string; order: number; }
export interface DashboardAlert { severity: 'critical' | 'warning' | 'info'; category: string; urgency: 'action' | 'upcoming' | 'info'; message: string; link?: string; rank: number; }
export interface DashboardDto { asOf: string; kpis: KpiCard[]; charts: DashboardChart[]; alerts: DashboardAlert[]; }

@Injectable({ providedIn: 'root' })
export class DashboardApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  get(): Observable<DashboardDto> { return this.http.get<DashboardDto>(`${this.base}/dashboard`); }
}

// ---- Budgets -------------------------------------------------------------------------------------------------------------

export interface BudgetSummary { id: string; legalEntityId: string; name: string; fiscalYear: number; isActive: boolean; }
export interface BudgetLineValue { accountId: string; accountCode: string; accountName: string; month: number; amount: number; }
export interface BudgetDetail extends BudgetSummary { notes?: string; totalIncome: number; totalExpense: number; lines: BudgetLineValue[]; }
export interface BudgetVsActualRow { accountId: string; code: string; name: string; type: string; budget: number; actual: number; variance: number; variancePercent?: number; }
export interface BudgetVsActual {
  budgetId: string; name: string; fiscalYear: number; fromMonth: number; toMonth: number; rows: BudgetVsActualRow[];
  budgetIncome: number; actualIncome: number; budgetExpense: number; actualExpense: number; budgetNet: number; actualNet: number;
}
@Injectable({ providedIn: 'root' })
export class BudgetsApi extends CrudApiService<BudgetSummary, unknown> {
  protected readonly resource = 'finance/budgets';
  detail(id: string): Observable<BudgetDetail> { return this.http.get<BudgetDetail>(`${this.url}/${id}`); }
  vsActual(id: string, fromMonth: number, toMonth: number): Observable<BudgetVsActual> { return this.http.get<BudgetVsActual>(`${this.url}/${id}/vs-actual`, { params: { fromMonth, toMonth } }); }
}
