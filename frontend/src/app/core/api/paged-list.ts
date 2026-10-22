import { DestroyRef, inject, signal } from '@angular/core';
import { Observable, Subject, catchError, of, switchMap, tap } from 'rxjs';
import { FilterCriterion, ListQuery, PagedResult } from './api.models';

export type ListSource<T> = (q: ListQuery) => Observable<PagedResult<T>>;

/**
 * Reusable server-side list state (page, size, sort, search, filters → items/total/loading) for any endpoint.
 * Create in a component: `readonly list = createPagedList((q) => this.api.list(q))`, bind to <erp-paged-table>.
 */
export class PagedList<T> {
  readonly query = signal<ListQuery>({ page: 1, pageSize: 25 });
  readonly items = signal<T[]>([]);
  readonly total = signal(0);
  readonly loading = signal(false);
  readonly error = signal<unknown>(null);
  private readonly trigger = new Subject<void>();

  constructor(source: ListSource<T>, destroyRef: DestroyRef, initial?: Partial<ListQuery>) {
    if (initial) this.query.update((q) => ({ ...q, ...initial }));
    const sub = this.trigger.pipe(
      tap(() => { this.loading.set(true); this.error.set(null); }),
      switchMap(() => source(this.query()).pipe(catchError((e) => { this.error.set(e); return of(null); }))),
    ).subscribe((r) => {
      this.loading.set(false);
      if (r) { this.items.set(r.items); this.total.set(r.total); }
    });
    destroyRef.onDestroy(() => sub.unsubscribe());
    this.reload();
  }

  reload(): void { this.trigger.next(); }
  setPage(page: number, pageSize?: number): void { this.patch({ page, pageSize: pageSize ?? this.query().pageSize }, false); }
  setSort(sort: string | null): void { this.patch({ sort }); }
  setSearch(search: string): void { this.patch({ search }); }
  setFilters(filters: FilterCriterion[]): void { this.patch({ filters }); }
  setExtra(extra: ListQuery['extra']): void { this.patch({ extra }); }

  private patch(p: Partial<ListQuery>, resetPage = true): void {
    this.query.update((q) => ({ ...q, ...p, ...(resetPage ? { page: 1 } : {}) }));
    this.reload();
  }
}

export function createPagedList<T>(source: ListSource<T>, initial?: Partial<ListQuery>): PagedList<T> {
  return new PagedList<T>(source, inject(DestroyRef), initial);
}
