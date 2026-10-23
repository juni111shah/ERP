import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, of, switchMap, tap } from 'rxjs';
import { AccountCategoryDto, AccountDto, AccountNodeDto, AccountsApi } from '../../../core/api/admin.api';
import { ListQuery, PagedResult } from '../../../core/api/api.models';
import { SessionStore } from '../../../core/session/session.store';

export type CoaPanelKind = 'categories' | 'accounts' | 'detail';

/** One column of the step-by-step chart of accounts. Each column holds exactly one level, fetched when it opens. */
export interface CoaPanel {
  /** 'root' | 'type:<type>' (primary accounts of a category) | 'acct:<id>' (children of an account) | 'detail:<id>' (a leaf account) */
  key: string;
  kind: CoaPanelKind;
  /** category of a 'type:' panel */
  type: string | null;
  /** owning account of an 'acct:' panel, or the account shown by a 'detail:' panel */
  parent: AccountNodeDto | null;
  categories: AccountCategoryDto[];
  items: AccountNodeDto[];
  total: number;
  page: number;
  search: string;
  loading: boolean;
  loadingMore: boolean;
  error: boolean;
  /** selected category type (root panel) or account id (account panels): the next column belongs to it */
  selected: string | null;
  /** an account that must stay visible in this column even when it is beyond the loaded page (re-opened paths) */
  pin: AccountNodeDto | null;
}

export interface CoaCrumb { label: string; code?: string; target: number; }

const PAGE_SIZE = 50;

function panel(key: string, kind: CoaPanelKind, extra: Partial<CoaPanel> = {}): CoaPanel {
  return {
    key, kind, type: null, parent: null, categories: [], items: [], total: 0, page: 0, search: '',
    loading: kind !== 'detail', loadingMore: false, error: false, selected: null, pin: null, ...extra,
  };
}

/**
 * Session-wide state of the step-by-step chart of accounts (Miller columns): category → primary accounts → children → …
 * Only one path is open: selecting another item closes the deeper columns. Every level is requested on demand and cached
 * for the session (per legal entity), so going back and forth costs no requests; any save clears the cache and re-opens the path.
 */
@Injectable({ providedIn: 'root' })
export class AccountTreeStore {
  private readonly api = inject(AccountsApi);
  private readonly session = inject(SessionStore);
  private readonly cache = new Map<string, unknown>();
  private readonly tokens = new Map<string, number>();
  private scope: string | null = null;

  readonly panels = signal<CoaPanel[]>([]);
  /** natural-side balances are shown only to users who may read financial reports */
  readonly withBalances = computed(() => this.session.hasPermission('finance.reports.read'));

  /** Breadcrumb of the open path; `target` is the column index to go back to. */
  readonly crumbs = computed<CoaCrumb[]>(() => {
    const ps = this.panels();
    const crumbs: CoaCrumb[] = [{ label: '', target: 0 }];
    ps.forEach((p, i) => {
      if (!p.selected || i + 1 >= ps.length) return;
      if (p.kind === 'categories') crumbs.push({ label: p.selected, target: i + 1 });
      else {
        const node = this.nodeIn(p, p.selected);
        if (node) crumbs.push({ label: node.name, code: node.code, target: i + 1 });
      }
    });
    return crumbs;
  });

  /** Opens the screen: keeps the session's open path and cache, unless the legal entity (or balance access) changed. */
  init(): void {
    const scope = `${this.session.currentEntityId() ?? '*'}|${this.withBalances()}`;
    if (scope !== this.scope) {
      this.scope = scope;
      this.cache.clear();
      this.panels.set([]);
    }
    if (!this.panels().length) {
      this.panels.set([panel('root', 'categories')]);
      this.loadCategories();
    }
  }

  selectCategory(type: string): void {
    const ps = this.panels();
    if (ps[0]?.selected === type && ps.length > 1) return;
    const next = panel(`type:${type}`, 'accounts', { type });
    this.panels.set([{ ...ps[0], selected: type }, next]);
    this.load(next.key);
  }

  selectAccount(index: number, node: AccountNodeDto): void {
    const ps = this.panels();
    if (ps[index]?.selected === node.id && ps.length > index + 1) return;
    const kept = ps.slice(0, index + 1);
    kept[index] = { ...kept[index], selected: node.id };
    const next = node.hasChildren
      ? panel(`acct:${node.id}`, 'accounts', { parent: node })
      : panel(`detail:${node.id}`, 'detail', { parent: node });
    this.panels.set([...kept, next]);
    if (node.hasChildren) this.load(next.key);
  }

  /** Breadcrumb navigation: keep columns 0..target and clear the selection in the last one. */
  goTo(target: number): void {
    const kept = this.panels().slice(0, target + 1);
    if (!kept.length) return;
    kept[target] = { ...kept[target], selected: null };
    this.panels.set(kept);
  }

  search(key: string, term: string): void {
    const p = this.find(key);
    if (!p || p.search === term) return;
    this.patch(key, { search: term, pin: null });
    if (p.kind === 'accounts') this.load(key);
  }

  loadMore(key: string): void { this.load(key, true); }

  retry(key: string): void {
    if (key === 'root') this.loadCategories();
    else this.load(key);
  }

  /**
   * After a create, edit or (de)activation: drop the cache and re-open the path to `focusId` (the saved account), or to the
   * deepest open account, so the columns show fresh data and the saved account stays selected.
   */
  refresh(focusId?: string | null): void {
    this.cache.clear();
    const target = focusId ?? this.deepestAccountId();
    if (target) {
      this.api.path(target).subscribe({ next: (path) => this.openPath(path), error: () => this.reset() });
      return;
    }
    const ps = this.panels();
    this.loadCategories();
    ps.slice(1).forEach((p) => p.kind === 'accounts' && this.load(p.key));
  }

  /** Forget everything (a save made elsewhere, e.g. the list view): the next visit starts fresh at the categories. */
  invalidate(): void {
    this.cache.clear();
    this.scope = null;
    this.panels.set([]);
  }

  /** Full account (with custom fields) for the edit drawer. */
  account(id: string): Observable<AccountDto> { return this.api.get(id); }

  /** Activates or deactivates an account, keeping every other field (and its custom fields) as they are. */
  setActive(id: string, isActive: boolean): Observable<AccountDto> {
    return this.api.get(id).pipe(
      switchMap((a) => this.api.update(id, {
        code: a.code, name: a.name, type: a.type, reportingGroup: a.reportingGroup ?? null, parentId: a.parentId ?? null, isPostable: a.isPostable,
        isBank: a.isBank, currencyCode: a.currencyCode ?? null, isActive, customFields: a.customFields,
      })),
      tap(() => this.refresh(id)),
    );
  }

  /** Re-opens the columns along a root-first path (from GET accounts/{id}/path). */
  openPath(path: AccountNodeDto[]): void {
    const last = path[path.length - 1];
    if (!last) { this.reset(); return; }
    // The account sits in its own category's column under the top of its same-type chain (a parent of another type starts a new chain).
    let start = path.length - 1;
    while (start > 0 && path[start - 1].type === last.type) start--;
    const chain = path.slice(start);
    const ps: CoaPanel[] = [panel('root', 'categories', { selected: last.type })];
    ps.push(panel(`type:${last.type}`, 'accounts', { type: last.type, selected: chain[0].id, pin: chain[0] }));
    for (let k = 1; k < chain.length; k++) ps.push(panel(`acct:${chain[k - 1].id}`, 'accounts', { parent: chain[k - 1], selected: chain[k].id, pin: chain[k] }));
    ps.push(last.hasChildren ? panel(`acct:${last.id}`, 'accounts', { parent: last }) : panel(`detail:${last.id}`, 'detail', { parent: last }));
    this.panels.set(ps);
    this.loadCategories();
    ps.forEach((p) => p.kind === 'accounts' && this.load(p.key));
  }

  private reset(): void {
    this.cache.clear();
    this.panels.set([panel('root', 'categories')]);
    this.loadCategories();
  }

  private loadCategories(): void {
    const key = `cats|${this.withBalances()}`;
    const cached = this.cache.get(key) as AccountCategoryDto[] | undefined;
    if (cached) { this.patch('root', { categories: cached, loading: false, error: false }); return; }
    const token = this.bump('root');
    this.patch('root', { loading: true, error: false });
    this.api.categories(this.withBalances()).subscribe({
      next: (cats) => {
        if (this.tokens.get('root') !== token) return;
        this.cache.set(key, cats);
        this.patch('root', { categories: cats, loading: false });
      },
      error: () => this.tokens.get('root') === token && this.patch('root', { loading: false, error: true }),
    });
  }

  private load(key: string, append = false): void {
    const p = this.find(key);
    if (!p || p.kind !== 'accounts') return;
    const page = append ? p.page + 1 : 1;
    const token = this.bump(key);
    this.patch(key, append ? { loadingMore: true, error: false } : { loading: true, error: false });
    this.fetch(p, page).subscribe({
      next: (r) => {
        if (this.tokens.get(key) !== token) return;
        const current = this.find(key);
        if (!current) return;
        let items = append ? [...current.items, ...r.items.filter((n) => !current.items.some((c) => c.id === n.id))] : r.items;
        // A re-opened path keeps its selected account visible even when it is past the first page or outside the search.
        if (current.pin && !items.some((n) => n.id === current.pin!.id)) items = [current.pin, ...items];
        this.patch(key, { items, total: r.total, page, loading: false, loadingMore: false });
      },
      error: () => this.tokens.get(key) === token && this.patch(key, { loading: false, loadingMore: false, error: true }),
    });
  }

  private fetch(p: CoaPanel, page: number): Observable<PagedResult<AccountNodeDto>> {
    const query: ListQuery = { page, pageSize: PAGE_SIZE, sort: 'code', search: p.search || null, extra: this.withBalances() ? { includeBalances: true } : undefined };
    const cacheKey = `${p.key}|${p.search}|${page}|${this.withBalances()}`;
    const cached = this.cache.get(cacheKey) as PagedResult<AccountNodeDto> | undefined;
    if (cached) return of(cached);
    const request = p.type ? this.api.categoryAccounts(p.type, query) : this.api.children(p.parent!.id, query);
    return request.pipe(tap((r) => this.cache.set(cacheKey, r)));
  }

  private deepestAccountId(): string | null {
    const ps = this.panels();
    for (let i = ps.length - 1; i >= 1; i--) if (ps[i].selected) return ps[i].selected;
    const last = ps[ps.length - 1];
    return last && last.kind !== 'categories' && last.parent ? last.parent.id : null;
  }

  private nodeIn(p: CoaPanel, id: string): AccountNodeDto | undefined {
    return p.items.find((n) => n.id === id) ?? (p.pin?.id === id ? p.pin : undefined);
  }

  private find(key: string): CoaPanel | undefined { return this.panels().find((p) => p.key === key); }

  private patch(key: string, changes: Partial<CoaPanel>): void {
    this.panels.update((ps) => ps.map((p) => (p.key === key ? { ...p, ...changes } : p)));
  }

  private bump(key: string): number {
    const next = (this.tokens.get(key) ?? 0) + 1;
    this.tokens.set(key, next);
    return next;
  }
}
