import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, map } from 'rxjs';
import { AccountDto, AccountsApi, LegalEntitiesApi, LegalEntityDto, TaxCodeDto, TaxCodesApi } from '@core/api/admin.api';
import { PartiesApi, PartyDto } from '@core/api/parties.api';
import { LookupOption } from '@core/custom-fields/lookup-registry';
import { loadInto } from '@shared/utils/load-into';

/** Which pick lists a finance page needs. */
export interface FinanceLists { entities?: boolean; taxCodes?: boolean; accounts?: boolean; }

/**
 * The pick lists of the finance screens: legal entities, tax codes and the chart of accounts, plus the party search.
 * Provided per page (`providers: [FinanceLookups]`) and loaded once with only the lists that page needs; a list that fails stays empty.
 */
@Injectable()
export class FinanceLookups {
  private readonly entitiesApi = inject(LegalEntitiesApi);
  private readonly taxCodesApi = inject(TaxCodesApi);
  private readonly accountsApi = inject(AccountsApi);
  private readonly partiesApi = inject(PartiesApi);

  readonly entities = signal<LegalEntityDto[]>([]);
  /** Every tax code, active or not. */
  readonly taxCodes = signal<TaxCodeDto[]>([]);
  /** The first 200 accounts by code. */
  readonly accounts = signal<AccountDto[]>([]);
  /** The tax codes a new line can use. */
  readonly activeTaxes = computed(() => this.taxCodes().filter((t) => t.isActive));
  /** tax code id → rate (%), inactive codes included (old documents still show them). */
  readonly taxRates = computed(() => taxRateMap(this.taxCodes()));
  /** Bank and cash accounts money can be received into or paid from. */
  readonly cashAndBankAccounts = computed(() => this.accounts().filter(isCashOrBankAccount));

  /** Starts loading the chosen lists; returns itself so a page can write `inject(FinanceLookups).load({ ... })`. */
  load(lists: FinanceLists): this {
    if (lists.entities) loadInto(this.entities, this.entitiesApi.list({ page: 1, pageSize: 100 }), (r) => r.items);
    if (lists.taxCodes) loadInto(this.taxCodes, this.taxCodesApi.list({ page: 1, pageSize: 100 }), (r) => r.items);
    if (lists.accounts) loadInto(this.accounts, this.accountsApi.list({ page: 1, pageSize: 200, sort: 'code' }), (r) => r.items);
    return this;
  }

  /** The id of the only legal entity, or '' when there are several (or none yet). */
  onlyEntityId(): string {
    const entities = this.entities();
    return entities.length === 1 ? entities[0].id : '';
  }

  /** The first parties matching a search (party pickers). */
  findParties(term: string): Observable<PartyDto[]> {
    return this.partiesApi.list({ page: 1, pageSize: 20, search: term }).pipe(map((r) => r.items));
  }
}

/** tax code id → rate (%). */
export function taxRateMap(codes: TaxCodeDto[]): Record<string, number> {
  return Object.fromEntries(codes.map((t) => [t.id, t.rate]));
}

/** An account a journal line can post to. */
export const isPostableAccount = (a: AccountDto): boolean => a.isPostable && a.isActive;

/** A postable bank account (a cheque is deposited into one). */
export const isBankAccount = (a: AccountDto): boolean => a.type === 'asset' && a.isPostable && a.isActive && (a.isBank || a.systemRole === 'bank');

/** A postable bank or cash account. */
export const isCashOrBankAccount = (a: AccountDto): boolean => a.type === 'asset' && a.isPostable && a.isActive && (a.isBank || a.systemRole === 'cash' || a.systemRole === 'bank');

/** The account a new receipt, payment or deposit starts with: the main bank account. */
export const mainBankAccount = (accounts: AccountDto[]): AccountDto | undefined => accounts.find((b) => b.systemRole === 'bank');

/** A party in a picker: "code · name". */
export const partyOption = (p: Pick<PartyDto, 'id' | 'code' | 'name'>): LookupOption => ({ value: p.id, label: `${p.code} · ${p.name}` });

/** The party of an opened document, labelled with the name the document carries. */
export const documentPartyOption = (doc: { partyId: string; partyName?: string | null }): LookupOption => ({ value: doc.partyId, label: doc.partyName ?? doc.partyId });
